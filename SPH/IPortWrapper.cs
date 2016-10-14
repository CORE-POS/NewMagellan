//-------------------------------------------------------------
// <copyright file="IPortWrapper.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------

namespace SPH
{
    /// <summary>
    /// Wrapper interface for serial port
    /// </summary>
    public interface IPortWrapper
    {
        /// <summary>
        /// Open the port
        /// </summary>
        void Open();

        /// <summary>
        /// Close the port
        /// </summary>
        void Close();

        /// <summary>
        ///  Read one byte
        /// </summary>
        /// <returns>the byte</returns>
        int ReadByte();

        /// <summary>
        /// Write to the port
        /// </summary>
        /// <param name="msg">the message</param>
        void Write(string msg);

        /// <summary>
        /// Write to the port
        /// </summary>
        /// <param name="msg">message bytes</param>
        /// <param name="offset">offset amount</param>
        /// <param name="count">number of bytes</param>
        void Write(byte[] msg, int offset, int count);
    }
}
