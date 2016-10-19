using System;
using System.Reflection;
using static WeakDelegates.DependentHandlerStatic;

namespace WeakDelegates
{
    internal struct DelegateBreakout : IDisposable, IEquatable<DelegateBreakout>
    {
        private readonly object delegateOrMethodInfo;
        private readonly DependentHandle<object, GarbageAlerter>? target;
        private readonly string delegateString;

        public DelegateBreakout(Delegate @delegate, Action onCollection)
        {
            delegateString = @delegate.Method.ToString();
            if (@delegate.Target != null && onCollection != null)
            {
                delegateOrMethodInfo = @delegate.Method;
                target = new DependentHandle<object, GarbageAlerter>(@delegate.Target, new GarbageAlerter(onCollection));
            }
            else
            {
                delegateOrMethodInfo = new WeakReference<Delegate>(@delegate);
                target = null;
            }
        }

        public void Dispose()
        {
            if (target.HasValue)
            {
                target.Value.Dispose();
            }
        }

        public bool Equals(DelegateBreakout other)
        {
            MethodInfo methodInfo = delegateOrMethodInfo as MethodInfo;
            MethodInfo methodInfoOther = other.delegateOrMethodInfo as MethodInfo;
            if (methodInfo != null && methodInfoOther != null && string.Equals(methodInfo.ToString(), methodInfoOther.ToString(), StringComparison.Ordinal))
            {
                return true;
            }
            Delegate @delegate;
            Delegate delegateOther;
            return ((WeakReference<Delegate>)delegateOrMethodInfo).TryGetTarget(out @delegate) && ((WeakReference<Delegate>)other.delegateOrMethodInfo).TryGetTarget(out delegateOther) && @delegate == delegateOther;
        }

        public bool TryGetDelegate(Type delegateType, out Delegate @delegate)
        {
            object currentTarget = null;
            if (target != null && !target.Value.TryGetPrimary(out currentTarget))
            {
                @delegate = null;
                return false;
            }
            MethodInfo methodInfo = delegateOrMethodInfo as MethodInfo;
            if (methodInfo != null)
            {
                @delegate = target == null ? methodInfo.CreateDelegate(delegateType) : methodInfo.CreateDelegate(delegateType, currentTarget);
                return true;
            }
            if (((WeakReference<Delegate>)delegateOrMethodInfo).TryGetTarget(out @delegate))
            {
                return true;
            }
            @delegate = null;
            return false;
        }

        public static bool operator ==(DelegateBreakout left, DelegateBreakout right) => left.Equals(right);

        public static bool operator !=(DelegateBreakout left, DelegateBreakout right) => !left.Equals(right);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
            {
                return false;
            }
            if (obj is DelegateBreakout)
            {
                return Equals((DelegateBreakout)obj);
            }
            return false;
        }

        public override int GetHashCode() => delegateOrMethodInfo.GetHashCode();

        public override string ToString() => delegateString;

        public bool Alive
        {
            get
            {
                object currentTarget;
                if (target != null && !target.Value.TryGetPrimary(out currentTarget))
                {
                    return false;
                }
                MethodInfo methodInfo = delegateOrMethodInfo as MethodInfo;
                if (methodInfo != null)
                {
                    return true;
                }
                Delegate @delegate;
                if (((WeakReference<Delegate>)delegateOrMethodInfo).TryGetTarget(out @delegate))
                {
                    return true;
                }
                return false;
            }
        }
    }
}