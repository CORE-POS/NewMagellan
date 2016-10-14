//-------------------------------------------------------------
// <copyright file="USBWrapper_Posix.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------

namespace USBLayer
{
    using System.IO;

    /// <summary>
    /// Wrapper for non-win32 environments
    /// </summary>
    public class USBWrapper_Posix : IUSBWrapper
    {
        /// <summary>
        /// Get the device handle
        /// </summary>
        /// <param name="filename">file name</param>
        /// <param name="report_size">report size</param>
        /// <returns>the handle</returns>
        public Stream GetUSBHandle(string filename, int report_size)
        {
            if (!File.Exists(filename))
            {
                return null;
            }

            return new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, report_size, FileOptions.Asynchronous);
        }

        /// <summary>
        /// Close the handle
        /// </summary>
        public void CloseUSBHandle()
        {
        }
    }
}
