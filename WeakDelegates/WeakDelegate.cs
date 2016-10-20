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

        /// <summary>
        ///     Concatenates the invocation lists of two delegates.
        /// </summary>
        /// <typeparam name="TDelegate">The type of delegate to combine.</typeparam>
        /// <param name="first">The delegate whose invocation list comes first.</param>
        /// <param name="last">The delegate whose invocation list comes last.</param>
        /// <returns>A new delegate with a weak connection to both <paramref name="first"/> and <paramref name="last"/>. If one is <see langword="null"/> but the other is not, returns a delegate with a weak connection to just that the parameter that is not <see langword="null"/>. If both are <see langword="null"/>, returns <see langword="null"/>.</returns>
        public static TDelegate Combine<[DelegateConstraint]TDelegate>(TDelegate first, TDelegate last) where TDelegate : class
        {
            if (ReferenceEquals(first, null) && ReferenceEquals(last, null))
            {
                return null;
            }
            Delegate firstDelegate = first as Delegate;
            Delegate lastDelegate = last as Delegate;
            Delegate finalDelegate = CreateDynamicDelegate(first, last, firstDelegate, lastDelegate);
            WeakDelegateSuragate firstSuragate = null;
            if (firstDelegate != null)
            {
                GetCombinedHolder(firstDelegate.Method, out firstSuragate);
            }
            WeakDelegateSuragate lastSuragate = null;
            if (lastDelegate != null)
            {
                GetCombinedHolder(lastDelegate.Method, out lastSuragate);
            }
            WeakDelegateSuragate finalSuragate = firstSuragate != null ? lastSuragate != null ? firstSuragate.Add(lastSuragate) : lastDelegate != null ? firstSuragate.Add(firstDelegate) : firstSuragate.Clone() : lastSuragate != null ? firstDelegate != null ? lastSuragate.Add(firstDelegate) : lastSuragate.Clone() : firstDelegate != null ? lastDelegate != null ? new WeakDelegateSuragate(firstDelegate, lastDelegate) : new WeakDelegateSuragate(firstDelegate) : new WeakDelegateSuragate(lastDelegate);
            suragates.Add(finalDelegate.Method, new WeakReference<WeakDelegateSuragate>(finalSuragate));
            if (firstDelegate != null && firstDelegate.Target != null)
            {
                weakReference.Add(firstDelegate.Target, finalSuragate);
            }
            if (lastDelegate != null && lastDelegate.Target != null)
            {
                weakReference.Add(lastDelegate.Target, finalSuragate);
            }
            return finalDelegate as TDelegate;
        }

        private static Delegate CreateDynamicDelegate<TDelegate>(TDelegate first, TDelegate last, Delegate firstDelegate, Delegate lastDelegate) where TDelegate : class
        {
            MethodInfo signature = firstDelegate?.Method ?? lastDelegate.Method;
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
            il.Emit(OpCodes.Castclass, typeof(TDelegate));
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Brfalse_S, noDelegates);
            for (int argIndex = 0; argIndex < dynamic.GetParameters().Length; argIndex++)
            {
                il.Emit(OpCodes.Ldarg, argIndex);
            }
            il.Emit(OpCodes.Callvirt, typeof(TDelegate).GetMethod("Invoke"));
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
            return dynamic.CreateDelegate((first ?? last).GetType());
        }

        public static TDelegate Remove<[DelegateConstraint]TDelegate>(TDelegate source, TDelegate value) where TDelegate : class
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
                return newDelegate as TDelegate;
            }
            return Delegate.Remove(sourceDelegate, valueDelegate) as TDelegate;
        }
    }
}