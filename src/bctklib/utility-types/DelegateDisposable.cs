using System;

namespace Neo.BlockchainToolkit.Utilities
{
    class DelegateDisposable : IDisposable
    {
        readonly Action action;
        bool disposed = false;

        public DelegateDisposable(Action action)
        {
            this.action = action;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                action();
                disposed = true;
            }
        }
    }
}
