using System;
using System.Reflection;
using static WeakDelegates.DependentHandlerStatic;

namespace WeakDelegates
{
    /// <summary>
    ///	A <see cref="Delegate"/> broken out into parts.
    /// </summary>
    /// <seealso cref="IDisposable" />
    /// <seealso cref="IEquatable{T}" />
    internal readonly struct DelegateBreakout : IDisposable, IEquatable<DelegateBreakout>
    {
        /// <summary>
        ///	The delegate or method information
        /// </summary>
        private readonly object delegateOrMethodInfo;
        /// <summary>
        ///	The target of the delegate
        /// </summary>
        private readonly DependentHandle<object, GarbageAlerter>? target;
        /// <summary>
        ///	Text representation of the original delegate.
        /// </summary>
        private readonly string delegateString;

        /// <summary>
        ///	Initializes a new instance of the <see cref="DelegateBreakout"/> struct.
        /// </summary>
        /// <param name="delegate">The delegate.</param>
        /// <param name="onCollection">The method to invoke when <paramref name="delegate"/>'s target is garbage collected.</param>
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

        /// <summary>
        ///	Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (target.HasValue)
            {
                target.Value.Dispose();
            }
        }

        /// <summary>
        ///	Indicates whether the current <see cref="DelegateBreakout"/> is equal to another <see cref="DelegateBreakout"/>.
        /// </summary>
        /// <param name="other">A <see cref="DelegateBreakout"/> to compare with this <see cref="DelegateBreakout"/>.</param>
        /// <returns><see langword="true"/> if the current <see cref="DelegateBreakout"/> is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false"/>.</returns>
        public bool Equals(DelegateBreakout other)
        {
            MethodInfo methodInfo = delegateOrMethodInfo as MethodInfo;
            MethodInfo methodInfoOther = other.delegateOrMethodInfo as MethodInfo;
            if (methodInfo != null && methodInfoOther != null && string.Equals(methodInfo.ToString(), methodInfoOther.ToString(), StringComparison.Ordinal))
            {
                return true;
            }
            return ((WeakReference<Delegate>)delegateOrMethodInfo).TryGetTarget(out Delegate @delegate) && ((WeakReference<Delegate>)other.delegateOrMethodInfo).TryGetTarget(out Delegate delegateOther) && @delegate == delegateOther;
        }

        /// <summary>
        ///	Tries to get the delegate.
        /// </summary>
        /// <param name="delegateType">Type of the delegate.</param>
        /// <param name="delegate">The delegate.</param>
        /// <returns><see langword="true"/> if delegate is still alive; otherwise <see langword="false"/>.</returns>
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

        /// <summary>
        ///	Determines whether the specified <see cref="object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="object" /> to compare with this instance.</param>
        /// <returns><see langword="true"/> if the specified <see cref="object" /> is equal to this instance; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object obj)
        {
            if (obj is DelegateBreakout breakout)
            {
                return Equals(breakout);
            }
            return false;
        }

        /// <summary>
        ///	Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
        public override int GetHashCode() => delegateOrMethodInfo.GetHashCode();

        /// <summary>
        ///	Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString() => delegateString;

        /// <summary>
        ///	Gets a value indicating whether this <see cref="DelegateBreakout"/> is alive.
        /// </summary>
        /// <value><see langword="true"/> if alive; otherwise, <see langword="false"/>.</value>
        public bool Alive
        {
            get
            {
                if (target != null && !target.Value.TryGetPrimary(out _))
                {
                    return false;
                }
                MethodInfo methodInfo = delegateOrMethodInfo as MethodInfo;
                if (methodInfo != null)
                {
                    return true;
                }

                if (((WeakReference<Delegate>)delegateOrMethodInfo).TryGetTarget(out _))
                {
                    return true;
                }
                return false;
            }
        }
    }
}