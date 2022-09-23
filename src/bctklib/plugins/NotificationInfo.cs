namespace Neo.BlockchainToolkit.Plugins
{
    public readonly record struct NotificationInfo(
        uint BlockIndex, 
        ushort TxIndex,
        ushort NotificationIndex,
        NotificationRecord Notification);
}
