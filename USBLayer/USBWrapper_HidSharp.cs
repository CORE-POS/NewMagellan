//-------------------------------------------------------------
// <copyright file="USBWrapper_HidSharp.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------

namespace USBLayer 
{
    using System;
    using System.IO;
    using System.Linq;

    using HidSharp;

    /// <summary>
    /// Cross-platform wrapper using <c>HidSharp</c>
    /// </summary>
    public class USBWrapper_HidSharp : IUSBWrapper 
    {
        /// <summary>
        /// Get the handle
        /// </summary>
        /// <param name="filename">device file</param>
        /// <param name="report_size">report size</param>
        /// <returns>the handle</returns>
        public Stream GetUSBHandle(string filename, int report_size)
        { 
            HidDeviceLoader loader = new HidDeviceLoader();
            int vid = 0;
            int pid = 0;
            if (filename.IndexOf("&") > 0)
            {
                string[] parts = filename.Split(new char[] { '&' });
                vid = Convert.ToInt32(parts[0]);
                pid = Convert.ToInt32(parts[1]);
            }
            else
            {
                System.Console.WriteLine("Invalid device specification: " + filename);
                return null;
            }

            HidDevice dev = loader.GetDeviceOrDefault(vid, pid, null, null);
            if (dev == null)
            {
                System.Console.WriteLine("Could not find requested device: " + filename);
                var devices = loader.GetDevices().ToArray();
                foreach (HidDevice d in devices)
                {
                    System.Console.WriteLine(d);
                }

                return null;
            }

            HidStream stream;
            if (!dev.TryOpen(out stream))
            {
                System.Console.WriteLine("Found requested device but cannot connect: " + filename);
                return null;
            }

            return stream;
        }

        /// <summary>
        /// Close the handle
        /// </summary>
        public void CloseUSBHandle()
        { 
        }
    }
}
