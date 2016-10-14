//-------------------------------------------------------------
// <copyright file="IUSBWrapper.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------

namespace USBLayer
{
    using System.IO;

    /// <summary>
    /// Interface for a USB device
    /// </summary>
    public interface IUSBWrapper
    {
        /// <summary>
        /// Get handle to device
        /// </summary>
        /// <param name="filename">device file name</param>
        /// <param name="report_size">report size</param>
        /// <returns>the handle</returns>
        Stream GetUSBHandle(string filename, int report_size);

        /// <summary>
        /// Close the device handle
        /// </summary>
        void CloseUSBHandle();
    }
}
