using System;
using System.Threading.Tasks;

namespace WeakDelegates
{
    internal class GarbageAlerter
    {
        private readonly Action onAlert;

        public GarbageAlerter(Action onAlert)
        {
            this.onAlert = onAlert;
        }

        ~GarbageAlerter()
        {
            if (onAlert != null)
            {
                Task.Run(onAlert);
            }
        }
    }
}