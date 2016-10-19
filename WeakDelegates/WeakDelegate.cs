using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace WeakDelegates
{
    public static partial class WeakDelegate
    {
        private static readonly ConditionalWeakTable<MethodBase, WeakReference<WeakDelegateSuragate>> suragates = new ConditionalWeakTable<MethodBase, WeakReference<WeakDelegateSuragate>>();
        private static readonly ConditionalWeakTable<object, WeakDelegateSuragate> weakReference = new ConditionalWeakTable<object, WeakDelegateSuragate>();
        private static readonly Func<ParameterInfo, Type> getParameterType = new Func<ParameterInfo, Type>(GetParameterType);

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
            Delegate cDelegate = CreateDynamicDelegate(a, b, aDelegate, bDelegate);
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
            WeakDelegateSuragate cSuragate = aSuragate != null ? bSuragate != null ? aSuragate.Add(bSuragate) : bDelegate != null ? aSuragate.Add(aDelegate) : aSuragate.Clone() : bSuragate != null ? aDelegate != null ? bSuragate.Add(aDelegate) : bSuragate.Clone() : aDelegate != null ? bDelegate != null ? new WeakDelegateSuragate(aDelegate, bDelegate) : new WeakDelegateSuragate(aDelegate) : new WeakDelegateSuragate(bDelegate);
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

        private static Delegate CreateDynamicDelegate<T>(T a, T b, Delegate aDelegate, Delegate bDelegate) where T : class
        {
            MethodInfo signature = aDelegate?.Method ?? bDelegate.Method;
            DynamicMethod dynamic = new DynamicMethod(string.Empty, signature.ReturnType == typeof(void) ? null : signature.ReturnType, signature.GetParameters().Select(getParameterType).ToArray(), typeof(WeakDelegate));
            ILGenerator il = dynamic.GetILGenerator();
            LocalBuilder holder = il.DeclareLocal(typeof(WeakDelegateSuragate));
            Label noSuragate = il.DefineLabel();
            Label noDelegates = il.DefineLabel();
            il.Emit(OpCodes.Call, typeof(MethodBase).GetMethod(nameof(MethodBase.GetCurrentMethod)));
            il.Emit(OpCodes.Ldloca_S, holder);
            il.Emit(OpCodes.Call, typeof(WeakDelegate).GetMethod(nameof(GetCombinedHolder), BindingFlags.NonPublic | BindingFlags.Static));
            il.Emit(OpCodes.Brfalse_S, noSuragate);
            il.Emit(OpCodes.Ldloc_S, holder);
            il.Emit(OpCodes.Callvirt, typeof(WeakDelegateSuragate).GetMethod(nameof(WeakDelegateSuragate.GetInvocationList), BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public));
            il.Emit(OpCodes.Castclass, typeof(T));
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Brfalse_S, noDelegates);
            for (int argIndex = 0; argIndex < dynamic.GetParameters().Length; argIndex++)
            {
                il.Emit(OpCodes.Ldarg, argIndex);
            }
            il.Emit(OpCodes.Callvirt, typeof(T).GetMethod("Invoke"));
            il.Emit(OpCodes.Ret);
            if (dynamic.ReturnType == null || dynamic.ReturnType == typeof(void))
            {
                il.MarkLabel(noDelegates);
                il.Emit(OpCodes.Pop);
                il.MarkLabel(noSuragate);
            }
            else
            {
                il.MarkLabel(noSuragate);
                il.Emit(OpCodes.Ldnull);
                il.MarkLabel(noDelegates);
            }
            il.Emit(OpCodes.Ret);
            return dynamic.CreateDelegate((a ?? b).GetType());
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
                Delegate newDelegate = CreateDynamicDelegate(source, null, sourceDelegate, null);
                WeakDelegateSuragate valueSuragate;
                suragates.Add(newDelegate.Method, new WeakReference<WeakDelegateSuragate>(GetCombinedHolder(valueDelegate.Method, out valueSuragate) ? sourceSuragate.Remove(valueSuragate) : sourceSuragate.Remove(valueDelegate)));
                return newDelegate as T;
            }
            return Delegate.Remove(sourceDelegate, valueDelegate) as T;
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