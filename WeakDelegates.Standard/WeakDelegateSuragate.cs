using System;
using System.Collections.Generic;
using System.Threading;

namespace WeakDelegates
{
    internal sealed class WeakDelegateSuragate
    {
        private readonly Type delegateType;
        private DelegateBreakout[] delegates;

        public WeakDelegateSuragate(Delegate @delegate)
        {
            delegateType = @delegate.GetType();
#pragma warning disable CC0022 // Should dispose object
            delegates = new DelegateBreakout[] { new DelegateBreakout(@delegate, Clean) };
#pragma warning restore CC0022 // Should dispose object
        }

        public WeakDelegateSuragate(Delegate a, Delegate b)
        {
            delegateType = a.GetType();
#pragma warning disable CC0022 // Should dispose object
            delegates = new DelegateBreakout[] { new DelegateBreakout(a, Clean), new DelegateBreakout(b, Clean) };
#pragma warning restore CC0022 // Should dispose object
        }

        [Obsolete("", true)]
        private WeakDelegateSuragate()
        {
            throw new NotSupportedException();
        }

        public WeakDelegateSuragate Add(Delegate @delegate)
        {
            WeakDelegateSuragate suragate = new WeakDelegateSuragate(delegateType, delegates.Length + 1);
            delegates.CopyTo(suragate.delegates, 0);
            delegates[delegates.Length - 1] = new DelegateBreakout(@delegate, Clean);
            return suragate;
        }

        public WeakDelegateSuragate Clone()
        {
            WeakDelegateSuragate suragate = new WeakDelegateSuragate(delegateType, delegates.Length);
            delegates.CopyTo(suragate.delegates, 0);
            return suragate;
        }

        public WeakDelegateSuragate Add(WeakDelegateSuragate other)
        {
            WeakDelegateSuragate suragate = new WeakDelegateSuragate(delegateType, delegates.Length + other.delegates.Length);
            delegates.CopyTo(suragate.delegates, 0);
            other.delegates.CopyTo(suragate.delegates, delegates.Length);
            return suragate;
        }

        private WeakDelegateSuragate(Type delegateType, int delegatesSize)
        {
            this.delegateType = delegateType;
            delegates = new DelegateBreakout[delegatesSize];
        }

        private void Clean()
        {
            lock (this)
            {
                DelegateBreakout[] currentDelegates = delegates;
                List<DelegateBreakout> stillAlive = new List<DelegateBreakout>(currentDelegates.Length);
                for (int delegateIndex = 0; delegateIndex < currentDelegates.Length; delegateIndex++)
                {
                    DelegateBreakout breakout = currentDelegates[delegateIndex];
                    if (breakout.Alive)
                    {
                        stillAlive.Add(breakout);
                    }
                    else
                    {
                        breakout.Dispose();
                    }
                }
                if (stillAlive.Count != currentDelegates.Length)
                {
                    Interlocked.Exchange(ref delegates, stillAlive.ToArray());
                }
            }
        }

        public Delegate GetInvocationList()
        {
            DelegateBreakout[] currentDelegates = delegates;
            Delegate resultDelegate = null;
            for (int delegateIndex = 0; delegateIndex < currentDelegates.Length; delegateIndex++)
            {
                DelegateBreakout breakout = currentDelegates[delegateIndex];
                Delegate @delegate;
                if (breakout.TryGetDelegate(delegateType, out @delegate))
                {
                    resultDelegate = Delegate.Combine(resultDelegate, @delegate);
                }
            }
            return resultDelegate;
        }

        public WeakDelegateSuragate Remove(Delegate @delegate)
        {
            using (DelegateBreakout toRemove = new DelegateBreakout(@delegate, null))
            {
                List<DelegateBreakout> breakouts = new List<DelegateBreakout>(delegates.Length);
                DelegateBreakout[] currentDelegates = delegates;
                for (int i = 0; i < currentDelegates.Length; i++)
                {
                    DelegateBreakout breakout = currentDelegates[i];
                    if (breakout != toRemove && breakout.Alive)
                    {
                        breakouts.Add(breakout);
                    }
                }
                WeakDelegateSuragate suragate = new WeakDelegateSuragate(delegateType, breakouts.Count);
                breakouts.CopyTo(suragate.delegates);
                return suragate;
            }
        }

        public WeakDelegateSuragate Remove(WeakDelegateSuragate other)
        {
            DelegateBreakout[] toRemove = other.delegates;
            List<DelegateBreakout> breakouts = new List<DelegateBreakout>(delegates.Length);
            DelegateBreakout[] currentDelegates = delegates;
            for (int i = 0; i < currentDelegates.Length; i++)
            {
                DelegateBreakout breakout = currentDelegates[i];
                if (Array.IndexOf(toRemove, breakout) == -1 && breakout.Alive)
                {
                    breakouts.Add(breakout);
                }
            }
            WeakDelegateSuragate suragate = new WeakDelegateSuragate(delegateType, breakouts.Count);
            breakouts.CopyTo(suragate.delegates);
            return suragate;
        }
    }
}