using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace WeakDelegates
{
    [ComVisible(false)]
    internal struct DependentHandle<TPrimary, TSecondary> : IDisposable where TPrimary : class where TSecondary : class
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
            if (!IsAllocated)
            {
                primary = default(TPrimary);
                return false;
            }
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
            nGetPrimaryAndSecondary(handle, out p, out s);
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

        public void Dispose()
        {
            Free();
        }

        public override string ToString() => IsAllocated.ToString();
    }
}