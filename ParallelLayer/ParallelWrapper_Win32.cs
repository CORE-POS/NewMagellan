//-------------------------------------------------------------
// <copyright file="ParallelWrapper_Win32.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------

namespace ParallelLayer
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// Parallel wrapper for win32 platforms
    /// </summary>
    public class ParallelWrapper_Win32 : IParallelWrapper 
    {
        /// <summary>
        /// Read constant
        /// </summary>
        protected const uint GENERIC_READ = 0x80000000;

        /// <summary>
        /// Write constant
        /// </summary>
        protected const uint GENERIC_WRITE = 0x40000000;

        /// <summary>
        /// Open file constant
        /// </summary>
        protected const uint OPEN_EXISTING = 3;

        /// <summary>
        /// Integer pointer to null
        /// </summary>
        private static IntPtr NullHandle = IntPtr.Zero;

        /// <summary>
        /// Integer pointer to invalid handle
        /// </summary>
        private static IntPtr InvalidHandleValue = new IntPtr(-1);

        /// <summary>
        /// A native file handle
        /// </summary>
        private IntPtr nativeHandle;

        /// <summary>
        /// Safe wrapper for native handle
        /// </summary>
        private SafeFileHandle safeHandle;

        /// <summary>
        /// Create handle
        /// </summary>
        /// <param name="filename">device file name</param>
        /// <returns>the handle</returns>
        public FileStream GetLpHandle(string filename)
        {
            this.nativeHandle = CreateFile(filename, GENERIC_WRITE, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (this.nativeHandle != InvalidHandleValue)
            {
                this.safeHandle = new SafeFileHandle(this.nativeHandle, true);
                return new FileStream(this.safeHandle, FileAccess.Write);
            }

            return null;
        }

        /// <summary>
        /// Close the handle
        /// </summary>
        public void CloseLpHandle()
        {
            try
            {
                this.safeHandle.Close();
                CloseHandle(this.nativeHandle);
            }
            catch (Exception)
            {
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)] protected static extern IntPtr CreateFile([MarshalAs(UnmanagedType.LPStr)] string strName, uint nAccess, uint nShareMode, IntPtr lpSecurity, uint nCreationFlags, uint nAttributes, IntPtr lpTemplate);
        /** alt; use safehandles?
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)] 
            static extern SafeFileHandle CreateFile(
                string fileName,
                [MarshalAs(UnmanagedType.U4)] FileAccess fileAccess,
                [MarshalAs(UnmanagedType.U4)] FileShare fileShare,
                IntPtr securityAttributes,
                [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
                [MarshalAs(UnmanagedType.U4)] FileAttributes flags,
                IntPtr template
        ); */
        [DllImport("kernel32.dll", SetLastError = true)] protected static extern int CloseHandle(IntPtr hFile);
    } // end class
} // end namespace
