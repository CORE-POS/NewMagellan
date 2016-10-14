//-------------------------------------------------------------
// <copyright file="EmvWrapper.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//------------------------------------------------------------
namespace AxLayer
{
    using DSIEMVXLib;

    /// <summary>
    /// ActiveX wrapper for EMVX
    /// </summary>
    public class EmvWrapper : IAxWrapper
    {
        /// <summary>
        /// Instance of ActiveX control
        /// </summary>
        private DsiEMVX control;

        /// <summary>
        /// Initializes a new instance of the 
        /// <see cref="EmvWrapper"/> class
        /// </summary>
        public EmvWrapper()
        {
            this.control = new DsiEMVX();
        }

        /// <summary>
        /// Unused. method for this control
        /// </summary>
        /// <param name="str">The parameter is not used.</param>
        /// <param name="code">The parameter is not used.</param>
        public void ServerIPConfig(string str, short code)
        {
        }

        /// <summary>
        /// Unused. method for this control
        /// </summary>
        /// <param name="timeout">The parameter is not used.</param>
        public void SetResponseTimeout(short timeout)
        {
        }

        /// <summary>
        /// Unused. method for this control
        /// </summary>
        public void CancelRequest()
        {
        }

        /// <summary>
        /// Preferred method to run a transaction
        /// </summary>
        /// <param name="xml">Transaction XML</param>
        /// <returns>XML result</returns>
        public string ProcessTransaction(string xml)
        {
            return this.control.ProcessTransaction(xml);
        }

        /// <summary>
        /// Alternate method to run a transaction
        /// </summary>
        /// <param name="xml">Transaction XML</param>
        /// <param name="code">The parameter is not used.</param>
        /// <param name="csp">The parameter is not used.</param>
        /// <param name="utd">The parameter is not used.</param>
        /// <returns>XML result</returns>
        public string ProcessTransaction(string xml, short code, string csp, string utd)
        {
            return this.control.ProcessTransaction(xml);
        }
    }
}
