using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;

namespace WeakDelegates
{
    internal static class DependentHandlerStatic
    {
        private delegate void nInitialize(object primary, object secondary, out IntPtr dependentHandle);

        private delegate void nGetPrimaryAndSecondary(IntPtr dependentHandle, out object primary, out object secondary);

        private delegate void nGetPrimary(IntPtr dependentHandle, out object primary);

        private delegate void nFree(IntPtr dependentHandle);

        [ComVisible(false)]
        public struct DependentHandle<TPrimary, TSecondary> : IDisposable where TPrimary : class where TSecondary : class
        {
            private IntPtr handle;

            public bool IsAllocated => handle != (IntPtr)0;

            [SecurityCritical]
            public DependentHandle(TPrimary primary, TSecondary secondary)
            {
                IntPtr intPtr = (IntPtr)0;
                InternalMethods.nInitialize(primary, secondary, out intPtr);
                this.handle = intPtr;
            }

            [SecurityCritical]
            public void Free()
            {
                if (handle != (IntPtr)0)
                {
                    IntPtr intPtr = handle;
                    handle = (IntPtr)0;
                    InternalMethods.nFree(intPtr);
                }
            }

            [SecurityCritical]
            public bool TryGetPrimary(out TPrimary primary)
            {
                if (!IsAllocated)
                {
                    primary = default(TPrimary);
                    return false;
                }
                object obj;
                InternalMethods.nGetPrimary(handle, out obj);
                if (obj == null)
                {
                    primary = default(TPrimary);
                    return false;
                }
                primary = (TPrimary)obj;
                return true;
            }

            [SecurityCritical]
            public bool TryGetPrimaryAndSecondary(out TPrimary primary, out TSecondary secondary)
            {
                if (!IsAllocated)
                {
                    primary = default(TPrimary);
                    secondary = default(TSecondary);
                    return false;
                }
                object p;
                object s;
                InternalMethods.nGetPrimaryAndSecondary(handle, out p, out s);
                if (p == null)
                {
                    primary = default(TPrimary);
                    secondary = default(TSecondary);
                    return false;
                }
                primary = (TPrimary)p;
                secondary = (TSecondary)s;
                return true;
            }

            public void Dispose()
            {
                Free();
            }

            public override string ToString() => IsAllocated.ToString();
        }

        private static class InternalMethods
        {
            public static readonly nFree nFree;

            public static readonly nGetPrimary nGetPrimary;

            public static readonly nGetPrimaryAndSecondary nGetPrimaryAndSecondary;

            public static readonly nInitialize nInitialize;

            static InternalMethods()
            {
                Type type = Type.GetType("System.Runtime.CompilerServices.DependentHandle");
                nInitialize = (nInitialize)type.GetMethod(nameof(nInitialize), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).CreateDelegate(typeof(nInitialize));
                nGetPrimaryAndSecondary = (nGetPrimaryAndSecondary)type.GetMethod(nameof(nGetPrimaryAndSecondary), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).CreateDelegate(typeof(nGetPrimaryAndSecondary));
                nGetPrimary = (nGetPrimary)type.GetMethod(nameof(nGetPrimary), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).CreateDelegate(typeof(nGetPrimary));
                nFree = (nFree)type.GetMethod(nameof(nFree), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).CreateDelegate(typeof(nFree));
            }
        }
    }
}