
namespace SPH
{
    using MsgInterface;

    /// <summary>
    /// Display no buttons, credit/debit buttons, or EMV buttons
    /// </summary>
    public enum RbaButtons { None, Credit, EMV };

    /// <summary>
    /// A stub is a SerialPortHandler that can be embedded within a 
    /// second SerialPortHandler. The primary SPH may hand control of
    /// the device to the stub by calling stubStart() and may take
    /// back control of the device by calling stubStop()
    /// </summary>
    public interface IStub
    {
        /// <summary>
        /// Let the stub take control of the device
        /// </summary>
        void stubStart();

        /// <summary>
        /// Relinquish control of the device
        /// </summary>
        void stubStop();

        /// <summary>
        /// The parent allows the stub to send messages
        /// to the POS
        /// </summary>
        /// <param name="parent">a message interface, typically NewMagellan</param>
        void SetParent(IDelegateForm parent);

        /// <summary>
        /// Set verbosity to enable or disable debug messages
        /// </summary>
        /// <param name="v">Verbosity level. Higher means more output</param>
        void SetVerbose(int v);

        /// <summary>
        /// Accept incoming messages from the POS
        /// </summary>
        /// <param name="msg"></param>
        void HandleMsg(string msg);

        void SetEMV(RbaButtons emv);
    }
}
