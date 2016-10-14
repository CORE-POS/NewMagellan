//-------------------------------------------------------------
// <copyright file="ParallelWrapper_Posix.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------

namespace ParallelLayer
{
    using System;
    using System.IO;

    /// <summary>
    /// Create parallel device handle on non-win32
    /// </summary>
    public class ParallelWrapper_Posix : IParallelWrapper
    {
        /// <summary>
        /// Create a device handle
        /// </summary>
        /// <param name="filename"> device file name</param>
        /// <returns>the handle</returns>
        public FileStream GetLpHandle(string filename)
        { 
            FileStream fs = null;
            try
            {
                fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            }
            catch (Exception)
            {
            }

            return fs;
        }

        /// <summary>
        /// Close device file
        /// </summary>
        public void CloseLpHandle()
        {
        }
    }
}
