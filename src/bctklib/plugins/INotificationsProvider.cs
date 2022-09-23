using System.Collections.Generic;
using Neo.Persistence;

namespace Neo.BlockchainToolkit.Plugins
{
    public interface INotificationsProvider
    {
        IEnumerable<NotificationInfo> GetNotifications(
            SeekDirection direction = SeekDirection.Forward, 
            IReadOnlySet<UInt160>? contracts = null, 
            IReadOnlySet<string>? eventNames = null);
    }
}
