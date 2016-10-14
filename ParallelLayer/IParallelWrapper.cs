//-------------------------------------------------------------
// <copyright file="IParallelWrapper.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------

namespace ParallelLayer
{
    using System.IO;

    /// <summary>
    /// Interface for accessing parallel devices
    /// </summary>
    public interface IParallelWrapper 
    {
         /// <summary>
         /// Get handle for a parallel port
         /// </summary>
         /// <param name="filename">device file</param>
         /// <returns>handle for device</returns>
        FileStream GetLpHandle(string filename);

        /// <summary>
        /// Close device handle
        /// </summary>
        void CloseLpHandle();
    }
}
