using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using Neo.IO;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.VM;
using ApplicationExecuted = Neo.Ledger.Blockchain.ApplicationExecuted;
using System.Diagnostics;

namespace Neo.BlockchainToolkit.Plugins
{
    class ToolkitPersistencePlugin : Plugin, INotificationsProvider
    {
        const string APP_LOGS_STORE_PATH = "app-logs-store";
        const string NOTIFICATIONS_STORE_PATH = "notifications-store";

        IStore? appLogsStore;
        IStore? notificationsStore;
        ISnapshot? appLogsSnapshot;
        ISnapshot? notificationsSnapshot;

        public ToolkitPersistencePlugin()
        {
            Blockchain.Committing += OnCommitting;
            Blockchain.Committed += OnCommitted;
        }

        public override void Dispose()
        {
            Blockchain.Committing -= OnCommitting;
            Blockchain.Committed -= OnCommitted;
            appLogsSnapshot?.Dispose();
            appLogsStore?.Dispose();
            notificationsSnapshot?.Dispose();
            notificationsStore?.Dispose();
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (appLogsStore is not null) throw new Exception($"{nameof(OnSystemLoaded)} already called");
            if (notificationsStore is not null) throw new Exception($"{nameof(OnSystemLoaded)} already called");

            appLogsStore = system.LoadStore(APP_LOGS_STORE_PATH);
            notificationsStore = system.LoadStore(NOTIFICATIONS_STORE_PATH);

            base.OnSystemLoaded(system);
        }

        public JObject? GetAppLog(UInt256 hash)
        {
            if (appLogsStore is null) throw new NullReferenceException(nameof(appLogsStore));
            var value = appLogsStore.TryGet(hash.ToArray());
            return value is not null && value.Length != 0
                ? JToken.Parse(Neo.Utility.StrictUTF8.GetString(value)) as JObject
                : null;
        }

        static readonly Lazy<byte[]> backwardsNotificationsPrefix = new(() =>
        {
            var buffer = new byte[sizeof(uint) + sizeof(ushort)];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, uint.MaxValue);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(sizeof(uint)), ushort.MaxValue);
            return buffer;
        });

        public IEnumerable<NotificationInfo> GetNotifications(
            SeekDirection direction = SeekDirection.Forward,
            IReadOnlySet<UInt160>? contracts = null,
            IReadOnlySet<string>? eventNames = null)
        {
            if (notificationsStore is null) throw new NullReferenceException(nameof(notificationsStore));

            var prefix = direction == SeekDirection.Forward
                ? Array.Empty<byte>()
                : backwardsNotificationsPrefix.Value;

            return notificationsStore.Seek(prefix, direction)
                .Select(t => ParseNotification(t.Key, t.Value))
                .Where(t => contracts is null || contracts.Contains(t.Notification.ScriptHash))
                .Where(t => eventNames is null || eventNames.Contains(t.Notification.EventName));

            static NotificationInfo ParseNotification(byte[] key, byte[] value)
            {
                var blockIndex = BinaryPrimitives.ReadUInt32BigEndian(key.AsSpan(0, sizeof(uint)));
                var txIndex = BinaryPrimitives.ReadUInt16BigEndian(key.AsSpan(sizeof(uint), sizeof(ushort)));
                var notIndex = BinaryPrimitives.ReadUInt16BigEndian(key.AsSpan(sizeof(uint) + sizeof(ushort), sizeof(ushort)));
                return new NotificationInfo(blockIndex, txIndex, notIndex, value.AsSerializable<NotificationRecord>());
            }
        }

        void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<ApplicationExecuted> executions)
        {
            if (appLogsStore is null) throw new NullReferenceException(nameof(appLogsStore));
            if (notificationsStore is null) throw new NullReferenceException(nameof(notificationsStore));

            appLogsSnapshot?.Dispose();
            notificationsSnapshot?.Dispose();
            appLogsSnapshot = appLogsStore.GetSnapshot();
            notificationsSnapshot = notificationsStore.GetSnapshot();

            if (executions.Count > ushort.MaxValue) throw new Exception("ApplicationExecuted List too big");

            var notificationIndex = new byte[sizeof(uint) + (2 * sizeof(ushort))];
            BinaryPrimitives.WriteUInt32BigEndian(
                notificationIndex.AsSpan(0, sizeof(uint)),
                block.Index);

            for (int i = 0; i < executions.Count; i++)
            {
                ApplicationExecuted appExec = executions[i];
                if (appExec.Transaction is null) continue;

                var txJson = TxLogToJson(appExec);
                appLogsSnapshot.Put(appExec.Transaction.Hash.ToArray(), Neo.Utility.StrictUTF8.GetBytes(txJson.ToString()));

                if (appExec.VMState != VMState.FAULT)
                {
                    if (appExec.Notifications.Length > ushort.MaxValue) throw new Exception("appExec.Notifications too big");

                    BinaryPrimitives.WriteUInt16BigEndian(notificationIndex.AsSpan(sizeof(uint), sizeof(ushort)), (ushort)i);

                    for (int j = 0; j < appExec.Notifications.Length; j++)
                    {
                        BinaryPrimitives.WriteUInt16BigEndian(
                            notificationIndex.AsSpan(sizeof(uint) + sizeof(ushort), sizeof(ushort)),
                            (ushort)j);
                        var record = new NotificationRecord(appExec.Notifications[j]);
                        notificationsSnapshot.Put(notificationIndex.ToArray(), record.ToArray());
                    }
                }
            }

            var blockJson = BlockLogToJson(block, executions);
            if (blockJson is not null)
            {
                appLogsSnapshot.Put(block.Hash.ToArray(), Neo.Utility.StrictUTF8.GetBytes(blockJson.ToString()));
            }
        }

        void OnCommitted(NeoSystem system, Block block)
        {
            appLogsSnapshot?.Commit();
            notificationsSnapshot?.Commit();
        }

        // TxLogToJson and BlockLogToJson copied from Neo.Plugins.LogReader in the ApplicationLogs plugin
        // to avoid dependency on LevelDBStore package

        static JObject TxLogToJson(ApplicationExecuted appExec)
        {
            Debug.Assert(appExec.Transaction is not null);

            JObject execution = ApplicationExecutedToJson(appExec);
            JObject txJson = new();
            txJson["txid"] = appExec.Transaction.Hash.ToString();
            txJson["executions"] = new List<JObject>() { execution }.ToArray();
            return txJson;
        }

        static JObject? BlockLogToJson(Block block, IReadOnlyList<ApplicationExecuted> executions)
        {
            var executionsJson = new JArray();
            foreach (var execution in executions)
            {
                if (execution.Transaction is null) continue;
                executionsJson.Add(ApplicationExecutedToJson(execution));
            }
            if (executionsJson.Count == 0) return null;

            JObject blockJson = new();
            blockJson["blockhash"] = block.Hash.ToString();
            blockJson["executions"] = executionsJson;
            return blockJson;
        }

        static JObject ApplicationExecutedToJson(ApplicationExecuted appExec)
        {
            JObject json = new();
            json["trigger"] = appExec.Trigger;
            json["vmstate"] = appExec.VMState;
            json["exception"] = appExec.Exception?.GetBaseException().Message;
            json["gasconsumed"] = appExec.GasConsumed.ToString();
            try
            {
                json["stack"] = appExec.Stack
                    .Select(q => q.ToJson())
                    .ToArray();
            }
            catch (InvalidOperationException)
            {
                json["stack"] = "error: recursive reference";
            }
            json["notifications"] = appExec.Notifications
                .Select(q => {
                    JObject notification = new();
                    notification["contract"] = q.ScriptHash.ToString();
                    notification["eventname"] = q.EventName;
                    try
                    {
                        notification["state"] = q.State.ToJson();
                    }
                    catch (InvalidOperationException)
                    {
                        notification["state"] = "error: recursive reference";
                    }
                    return notification;
                })
                .ToArray();
            return json;
        }
    }
}
