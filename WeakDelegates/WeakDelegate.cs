using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ExtraConstraints;

namespace WeakDelegates
{
    [SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "Class is designed to mimic Delegate's API surface so name is there to help with that")]
    public static class WeakDelegate
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

        public static T Combine<[DelegateConstraint]T>(T left, T right) where T : class
        {
            if (ReferenceEquals(left, null) && ReferenceEquals(right, null))
            {
                throw new ArgumentException("Both arguments cannot be null");
            }
            Delegate leftDelegate = left as Delegate;
            Delegate rightDelegate = right as Delegate;
            Delegate finalDelegate = CreateDynamicDelegate(left, right, leftDelegate, rightDelegate);
            WeakDelegateSuragate leftSuragate = null;
            if (leftDelegate != null)
            {
                GetCombinedHolder(leftDelegate.Method, out leftSuragate);
            }
            WeakDelegateSuragate rightSuragate = null;
            if (rightDelegate != null)
            {
                GetCombinedHolder(rightDelegate.Method, out rightSuragate);
            }
            WeakDelegateSuragate finalSuragate = leftSuragate != null ? rightSuragate != null ? leftSuragate.Add(rightSuragate) : rightDelegate != null ? leftSuragate.Add(leftDelegate) : leftSuragate.Clone() : rightSuragate != null ? leftDelegate != null ? rightSuragate.Add(leftDelegate) : rightSuragate.Clone() : leftDelegate != null ? rightDelegate != null ? new WeakDelegateSuragate(leftDelegate, rightDelegate) : new WeakDelegateSuragate(leftDelegate) : new WeakDelegateSuragate(rightDelegate);
            suragates.Add(finalDelegate.Method, new WeakReference<WeakDelegateSuragate>(finalSuragate));
            if (leftDelegate != null && leftDelegate.Target != null)
            {
                weakReference.Add(leftDelegate.Target, finalSuragate);
            }
            if (rightDelegate != null && rightDelegate.Target != null)
            {
                weakReference.Add(rightDelegate.Target, finalSuragate);
            }
            return finalDelegate as T;
        }

        private static Delegate CreateDynamicDelegate<T>(T left, T right, Delegate leftDelegate, Delegate rightDelegate) where T : class
        {
            MethodInfo signature = leftDelegate?.Method ?? rightDelegate.Method;
            DynamicMethod dynamic = new DynamicMethod(string.Empty, signature.ReturnType == typeof(void) ? null : signature.ReturnType, signature.GetParameters().Select(getParameterType).ToArray(), typeof(WeakDelegate));
            ILGenerator il = dynamic.GetILGenerator();
            LocalBuilder suragate = il.DeclareLocal(typeof(WeakDelegateSuragate));
            Label noSuragate = il.DefineLabel();
            Label noDelegates = il.DefineLabel();
            il.Emit(OpCodes.Call, typeof(MethodBase).GetMethod(nameof(MethodBase.GetCurrentMethod)));
            il.Emit(OpCodes.Ldloca_S, suragate);
            il.Emit(OpCodes.Call, typeof(WeakDelegate).GetMethod(nameof(GetCombinedHolder), BindingFlags.NonPublic | BindingFlags.Static));
            il.Emit(OpCodes.Brfalse_S, noSuragate);
            il.Emit(OpCodes.Ldloc_S, suragate);
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
            return dynamic.CreateDelegate((left ?? right).GetType());
        }

        public static T Remove<[DelegateConstraint]T>(T source, T value) where T : class
        {
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
    }
}