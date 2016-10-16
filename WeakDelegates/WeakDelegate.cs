using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace WeakDelegates
{
    public static class WeakDelegate
    {
        private static readonly ConditionalWeakTable<MethodBase, WeakReference<WeakDelegateSuragate>> suragates = new ConditionalWeakTable<MethodBase, WeakReference<WeakDelegateSuragate>>();
        private static readonly ConditionalWeakTable<object, WeakDelegateSuragate> weakReference = new ConditionalWeakTable<object, WeakDelegateSuragate>();
        private static readonly Func<ParameterInfo, Type> getParameterType = new Func<ParameterInfo, Type>(GetParameterType);

        [ComVisible(false)]
        private struct DependentHandle<TPrimary, TSecondary> where TPrimary : class where TSecondary : class
        {
            private IntPtr handle;

            public bool IsAllocated => handle != (IntPtr)0;

            [SecurityCritical]
            public DependentHandle(TPrimary primary, TSecondary secondary)
            {
                IntPtr intPtr = (IntPtr)0;
                nInitialize(primary, secondary, out intPtr);
                this.handle = intPtr;
            }

            [SecurityCritical]
            public void Free()
            {
                if (handle != (IntPtr)0)
                {
                    IntPtr intPtr = handle;
                    handle = (IntPtr)0;
                    nFree(intPtr);
                }
            }

            [SecurityCritical]
            public bool TryGetPrimary(out TPrimary primary)
            {
                object obj;
                nGetPrimary(handle, out obj);
                if (obj == null)
                {
                    primary = default(TPrimary);
                    return false;
                }
                primary = (TPrimary)obj;
                return true;
            }

            [SecurityCritical]
            public TPrimary GetPrimary()
            {
                object obj;
                nGetPrimary(handle, out obj);
                return (TPrimary)obj;
            }

            [SecurityCritical]
            public void GetPrimaryAndSecondary(out TPrimary primary, out TSecondary secondary)
            {
                object p;
                object s;
                nGetPrimaryAndSecondary(handle, out p, out s);
                primary = (TPrimary)p;
                secondary = (TSecondary)s;
            }

            [MethodImpl(MethodImplOptions.InternalCall)]
            [SecurityCritical]
            private static extern void nFree(IntPtr dependentHandle);

            [MethodImpl(MethodImplOptions.InternalCall)]
            [SecurityCritical]
            private static extern void nGetPrimary(IntPtr dependentHandle, out object primary);

            [MethodImpl(MethodImplOptions.InternalCall)]
            [SecurityCritical]
            private static extern void nGetPrimaryAndSecondary(IntPtr dependentHandle, out object primary, out object secondary);

            [MethodImpl(MethodImplOptions.InternalCall)]
            [SecurityCritical]
            private static extern void nInitialize(object primary, object secondary, out IntPtr dependentHandle);
        }

        private class GarbageAlerter
        {
            private readonly Action onAlert;

            public GarbageAlerter(Action onAlert)
            {
                this.onAlert = onAlert;
            }

            ~GarbageAlerter()
            {
                Task.Run(onAlert);
            }
        }

        private struct DelegateBreakout
        {
            private readonly object delegateOrMethodInfo;
            private readonly DependentHandle<object, GarbageAlerter>? target;

            public DelegateBreakout(Delegate @delegate, Action onCollection)
            {
                if (@delegate.Target != null)
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
        }

        private sealed class WeakDelegateSuragate
        {
            private readonly Type delegateType;
            private DelegateBreakout[] delegates;

            public WeakDelegateSuragate(Delegate @delegate)
            {
                delegateType = @delegate.GetType();
                delegates = new DelegateBreakout[] { new DelegateBreakout(@delegate, Clean) };
            }

            public WeakDelegateSuragate(Delegate a, Delegate b)
            {
                delegateType = a.GetType();
                delegates = new DelegateBreakout[] { new DelegateBreakout(a, Clean), new DelegateBreakout(b, Clean) };
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
                        Delegate @delegate;
                        if (breakout.TryGetDelegate(delegateType, out @delegate))
                        {
                            stillAlive.Add(breakout);
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
                        Delegate.Combine(resultDelegate, @delegate);
                    }
                }
                return resultDelegate;
            }

            public WeakDelegateSuragate Remove(Delegate @delegate)
            {
                List<DelegateBreakout> breakouts = new List<DelegateBreakout>(delegates.Length);
                DelegateBreakout[] currentDelegates = delegates;
                for (int i = 0; i < currentDelegates.Length; i++)
                {
                    DelegateBreakout breakout = currentDelegates[i];

                }
                WeakDelegateSuragate suragate = new WeakDelegateSuragate(delegateType, delegates.Length);
            }
        }

        private static Type GetParameterType(ParameterInfo p) => p.ParameterType;

        private static bool GetCombinedHolder(MethodBase method, out WeakDelegateSuragate holder)
        {
            WeakReference<WeakDelegateSuragate> wr;
            if (suragates.TryGetValue(method, out wr))
            {
                return wr.TryGetTarget(out holder);
            }
            holder = null;
            return false;
        }

        public static T Combine<T>(T a, T b) where T : class
        {
            if (ReferenceEquals(a, null) && ReferenceEquals(b, null))
            {
                throw new ArgumentException("Both arguments cannot be null");
            }
            CheckIsDelegate<T>();
            Delegate aDelegate = a as Delegate;
            Delegate bDelegate = b as Delegate;
            MethodInfo signature = aDelegate?.Method ?? bDelegate.Method;
            DynamicMethod dynamic = new DynamicMethod(string.Empty, signature.ReturnType == typeof(void) ? null : signature.ReturnType, signature.GetParameters().Select(getParameterType).ToArray(), typeof(WeakDelegate));
            ILGenerator il = dynamic.GetILGenerator();
            LocalBuilder holder = il.DeclareLocal(typeof(WeakDelegateSuragate));
            Label @return = il.DefineLabel();
            il.Emit(OpCodes.Call, typeof(MethodBase).GetMethod(nameof(MethodBase.GetCurrentMethod)));
            il.Emit(OpCodes.Ldloca_S, holder);
            il.Emit(OpCodes.Call, typeof(WeakDelegate).GetMethod(nameof(GetCombinedHolder), BindingFlags.NonPublic | BindingFlags.Static));
            il.Emit(OpCodes.Brfalse_S, @return);
            il.Emit(OpCodes.Ldloc_S, holder);
            il.Emit(OpCodes.Call, typeof(WeakDelegateSuragate).GetMethod(nameof(WeakDelegateSuragate.GetInvocationList), BindingFlags.NonPublic | BindingFlags.Instance));
            il.Emit(OpCodes.Brfalse_S, @return);
            il.Emit(OpCodes.Castclass, typeof(T));
            for (int argIndex = 0; argIndex < dynamic.GetParameters().Length; argIndex++)
            {
                il.Emit(OpCodes.Ldarg, argIndex);
            }
            il.Emit(OpCodes.Callvirt, typeof(T).GetMethod("Invoke"));
            if (dynamic.ReturnType == null || dynamic.ReturnType == typeof(void))
            {
                il.MarkLabel(@return);
            }
            else
            {
                il.Emit(OpCodes.Ret);
                il.MarkLabel(@return);
                il.Emit(OpCodes.Ldnull);
            }
            il.Emit(OpCodes.Ret);
            Delegate cDelegate = dynamic.CreateDelegate((a ?? b).GetType());
            WeakDelegateSuragate aSuragate = null;
            if (aDelegate != null)
            {
                GetCombinedHolder(aDelegate.Method, out aSuragate);
            }
            WeakDelegateSuragate bSuragate = null;
            if (bDelegate != null)
            {
                GetCombinedHolder(bDelegate.Method, out bSuragate);
            }
            WeakDelegateSuragate cSuragate;
            if (aSuragate != null && bSuragate != null)
            {
                cSuragate = aSuragate.Add(bSuragate);
            }
            else if (aDelegate != null && bDelegate != null)
            {
                cSuragate = new WeakDelegateSuragate(aDelegate, bDelegate);
            }
            else if (aSuragate != null && bDelegate != null)
            {
                cSuragate = aSuragate.Add(bDelegate);
            }
            else if (bSuragate != null && aDelegate != null)
            {
                cSuragate = bSuragate.Add(aDelegate);
            }
            else if (aSuragate != null)
            {
                cSuragate = aSuragate.Clone();
            }
            else if (bSuragate != null)
            {
                cSuragate = bSuragate.Clone();
            }
            else if (aDelegate != null)
            {
                cSuragate = new WeakDelegateSuragate(aDelegate);
            }
            else
            {
                cSuragate = new WeakDelegateSuragate(bDelegate);
            }
            suragates.Add(cDelegate.Method, new WeakReference<WeakDelegateSuragate>(cSuragate));
            if (aDelegate != null && aDelegate.Target != null)
            {
                weakReference.Add(aDelegate.Target, cSuragate);
            }
            if (bDelegate != null && bDelegate.Target != null)
            {
                weakReference.Add(bDelegate.Target, cSuragate);
            }
            return cDelegate as T;
        }

        public static T Remove<T>(T source, T value) where T : class
        {
            CheckIsDelegate<T>();
            if (ReferenceEquals(source, null))
            {
                return null;
            }
            Delegate sourceDelegate = source as Delegate;
            Delegate valueDelegate = value as Delegate;
            WeakDelegateSuragate sourceSuragate;
            if (GetCombinedHolder(sourceDelegate.Method, out sourceSuragate))
            {
                WeakDelegateSuragate valueSuragate;
                if (GetCombinedHolder(valueDelegate.Method, out valueSuragate))
                {

                }
            }
            return null;
        }

        private static void CheckIsDelegate<T>() where T : class
        {
            if (!typeof(T).IsSubclassOf(typeof(Delegate)))
            {
                throw new InvalidOperationException($"The generic type {nameof(T)} must be a delegate type. {typeof(T).FullName} is not.");
            }
        }
    }
}