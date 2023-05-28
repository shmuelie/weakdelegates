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

            public bool IsAllocated => handle != IntPtr.Zero;

            [SecurityCritical]
            public DependentHandle(TPrimary primary, TSecondary secondary)
            {
                InternalMethods.nInitialize(primary, secondary, out IntPtr intPtr);
                this.handle = intPtr;
            }

            [SecurityCritical]
            public bool TryGetPrimary(out TPrimary primary)
            {
                if (!IsAllocated)
                {
                    primary = default;
                    return false;
                }
                InternalMethods.nGetPrimary(handle, out object obj);
                if (obj == null)
                {
                    primary = default;
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
                    primary = default;
                    secondary = default;
                    return false;
                }
                InternalMethods.nGetPrimaryAndSecondary(handle, out object p, out object s);
                if (p == null)
                {
                    primary = default;
                    secondary = default;
                    return false;
                }
                primary = (TPrimary)p;
                secondary = (TSecondary)s;
                return true;
            }

            [SecurityCritical]
            public void Dispose()
            {
                if (handle != IntPtr.Zero)
                {
                    IntPtr intPtr = handle;
                    handle = IntPtr.Zero;
                    InternalMethods.nFree(intPtr);
                }
            }

            public override string ToString() => IsAllocated.ToString();
        }

        private static class InternalMethods
        {
            private const BindingFlags searchFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

            public static readonly nFree nFree;

            public static readonly nGetPrimary nGetPrimary;

            public static readonly nGetPrimaryAndSecondary nGetPrimaryAndSecondary;

            public static readonly nInitialize nInitialize;

            static InternalMethods()
            {
                Type type = Type.GetType("System.Runtime.CompilerServices.DependentHandle");
                nInitialize = (nInitialize)type.GetMethod(nameof(nInitialize), searchFlags).CreateDelegate(typeof(nInitialize));
                nGetPrimaryAndSecondary = (nGetPrimaryAndSecondary)type.GetMethod(nameof(nGetPrimaryAndSecondary), searchFlags).CreateDelegate(typeof(nGetPrimaryAndSecondary));
                nGetPrimary = (nGetPrimary)type.GetMethod(nameof(nGetPrimary), searchFlags).CreateDelegate(typeof(nGetPrimary));
                nFree = (nFree)type.GetMethod(nameof(nFree), searchFlags).CreateDelegate(typeof(nFree));
            }
        }
    }
}