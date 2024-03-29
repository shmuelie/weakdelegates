﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ExtraConstraints;

namespace WeakDelegates
{
    public static class WeakDelegate
    {
        private static readonly ConditionalWeakTable<MethodBase, WeakReference<WeakDelegateSuragate>> suragates = new ConditionalWeakTable<MethodBase, WeakReference<WeakDelegateSuragate>>();
        private static readonly ConditionalWeakTable<object, WeakDelegateSuragate> weakReference = new ConditionalWeakTable<object, WeakDelegateSuragate>();
        private static readonly Func<ParameterInfo, Type> getParameterType = new Func<ParameterInfo, Type>(GetParameterType);
        private static readonly Type voidType = typeof(void);
        private static readonly MethodInfo getCurrentMethodMethodInfo = typeof(MethodBase).GetMethod(nameof(MethodBase.GetCurrentMethod));
        private static readonly MethodInfo getCombinedHolderMethodInfo;
        private static readonly MethodInfo getInvocationListMethodInfo;
        private static readonly Type weakDelegateType;
        private static readonly Type weakDelegateSuragateType;

        static WeakDelegate()
        {
            weakDelegateType = typeof(WeakDelegate);
            weakDelegateSuragateType = typeof(WeakDelegateSuragate);
            getCombinedHolderMethodInfo = weakDelegateType.GetMethod(nameof(GetCombinedHolder), BindingFlags.NonPublic | BindingFlags.Static);
            getInvocationListMethodInfo = weakDelegateSuragateType.GetMethod(nameof(WeakDelegateSuragate.GetInvocationList), BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        }

        private static Type GetParameterType(ParameterInfo p) => p.ParameterType;

        private static bool GetCombinedHolder(MethodBase method, out WeakDelegateSuragate holder)
        {
            if (suragates.TryGetValue(method, out WeakReference<WeakDelegateSuragate> wr))
            {
                return wr.TryGetTarget(out holder);
            }
            holder = null;
            return false;
        }

        /// <summary>
        /// Concatenates the invocation lists of two delegates.
        /// </summary>
        /// <typeparam name="TDelegate">The type of delegate to combine.</typeparam>
        /// <param name="first">The delegate whose invocation list comes first.</param>
        /// <param name="last">The delegate whose invocation list comes last.</param>
        /// <returns>A new delegate with a weak connection to both <paramref name="first"/> and <paramref name="last"/>. If one is <see langword="null"/> but the other is not, returns a delegate with a weak connection to just that the parameter that is not <see langword="null"/>. If both are <see langword="null"/>, returns <see langword="null"/>.</returns>
        /// <seealso cref="Delegate.Combine(Delegate, Delegate)"/>
        public static TDelegate Combine<[DelegateConstraint] TDelegate>(TDelegate first, TDelegate last) where TDelegate : class
        {
            if (first is null && last is null)
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
            DynamicMethod dynamic = new DynamicMethod(string.Empty, signature.ReturnType == voidType ? null : signature.ReturnType, signature.GetParameters().Select(getParameterType).ToArray(), weakDelegateType);
            ILGenerator il = dynamic.GetILGenerator();
            LocalBuilder suragate = il.DeclareLocal(weakDelegateSuragateType);
            Label noSuragate = il.DefineLabel();
            Label noDelegates = il.DefineLabel();
            il.Emit(OpCodes.Call, getCurrentMethodMethodInfo);
            il.Emit(OpCodes.Ldloca_S, suragate);
            il.Emit(OpCodes.Call, getCombinedHolderMethodInfo);
            il.Emit(OpCodes.Brfalse_S, noSuragate);
            il.Emit(OpCodes.Ldloc_S, suragate);
            il.Emit(OpCodes.Callvirt, getInvocationListMethodInfo);
            il.Emit(OpCodes.Castclass, typeof(TDelegate));
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Brfalse_S, noDelegates);
            for (int argIndex = 0; argIndex < dynamic.GetParameters().Length; argIndex++)
            {
                il.Emit(OpCodes.Ldarg, argIndex);
            }
            il.Emit(OpCodes.Callvirt, typeof(TDelegate).GetMethod("Invoke"));
            il.Emit(OpCodes.Ret);
            if (dynamic.ReturnType == null || dynamic.ReturnType == voidType)
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

        /// <summary>
        ///	Removes the last occurrence of the invocation list of a delegate from the invocation list of another delegate.
        /// </summary>
        /// <typeparam name="TDelegate">The type of the delegate.</typeparam>
        /// <param name="source">The delegate from which to remove the invocation list of <paramref name="value"/>.</param>
        /// <param name="value">The delegate that supplies the invocation list to remove from the invocation list of <paramref name="source"/>.</param>
        /// <returns>A new delegate with an invocation list formed by taking the invocation list of <paramref name="source"/> and removing the last occurrence of the invocation list of <paramref name="value"/>, if the invocation list of <paramref name="value"/> is found within the invocation list of <paramref name="source"/>. Returns <paramref name="source"/> if <paramref name="value"/> is <see langword="null"/> or if the invocation list of <paramref name="value"/> is not found within the invocation list of <paramref name="source"/>. Returns <see langword="null"/> if the invocation list of <paramref name="value"/> is equal to the invocation list of <paramref name="source"/> or if <paramref name="source"/> is <see langword="null"/>.</returns>
        /// <seealso cref="Delegate.Remove(Delegate, Delegate)"/>
        public static TDelegate Remove<[DelegateConstraint] TDelegate>(TDelegate source, TDelegate value) where TDelegate : class
        {
            if (source is null)
            {
                return null;
            }
            Delegate sourceDelegate = source as Delegate;
            Delegate valueDelegate = value as Delegate;
            if (GetCombinedHolder(sourceDelegate.Method, out WeakDelegateSuragate sourceSuragate))
            {
                Delegate newDelegate = CreateDynamicDelegate(source, null, sourceDelegate, null);
                suragates.Add(newDelegate.Method, new WeakReference<WeakDelegateSuragate>(GetCombinedHolder(valueDelegate.Method, out WeakDelegateSuragate valueSuragate) ? sourceSuragate.Remove(valueSuragate) : sourceSuragate.Remove(valueDelegate)));
                return newDelegate as TDelegate;
            }
            return Delegate.Remove(sourceDelegate, valueDelegate) as TDelegate;
        }

        /// <summary>
        /// 	Removes the last occurrence of the invocation list of a delegate from the invocation list of an event's backing field.
        /// </summary>
        /// <typeparam name="TDelegate">The type of the delegate.</typeparam>
        /// <param name="eventContainer">The instance that has the event.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="value">The delegate that supplies the invocation list to remove from the invocation list of the event.</param>
        /// <exception cref="ArgumentNullException"><paramref name="eventContainer"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="eventName"/> is <see langword="null"/>, empty, only whitespace, or is not the name of an event on <paramref name="eventContainer"/>.</exception>
        /// <remarks>This only works on auto events.</remarks>
        public static void Remove<[DelegateConstraint] TDelegate>(object eventContainer, string eventName, TDelegate value) where TDelegate : class
        {
            if (eventContainer is null)
            {
                throw new ArgumentNullException(nameof(eventContainer));
            }
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException($"{nameof(eventName)} is null, empty or only whitespace.", nameof(eventName));
            }

            if (value is null)
            {
                return;
            }

            Type containingType = eventContainer.GetType();
            FieldInfo eventBackingField = containingType.GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (eventBackingField is null)
            {
                throw new ArgumentException($"No event backing field matches {nameof(eventName)}.", nameof(eventName));
            }

            TDelegate source = eventBackingField.GetValue(eventContainer) as TDelegate;
            source = Remove(source, value);
            eventBackingField.SetValue(eventContainer, source);
        }
    }
}