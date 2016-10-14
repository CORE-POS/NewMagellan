//-------------------------------------------------------------
// <copyright file="IDelegateForm.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------

namespace MsgInterface
{
    /// <summary>
    /// Interface to handle messages
    /// </summary>
    public interface IDelegateForm
    {
        /// <summary>
        /// Receive a message
        /// </summary>
        /// <param name="msg">the message</param>
        void MsgRecv(string msg);

        /// <summary>
        /// Send a message
        /// </summary>
        /// <param name="msg">the message</param>
        void MsgSend(string msg);
    }
}
