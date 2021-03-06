﻿//-------------------------------------------------------------
// <copyright file="PdcxWrapper.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------

namespace AxLayer
{
    using DSIPDCXLib;

    /// <summary>
    /// Wrapper for PDCX control
    /// </summary>
    public class PdcxWrapper : IAxWrapper
    {
        /// <summary>
        /// Instance of ActiveX control
        /// </summary>
        private DsiPDCX control;

        /// <summary>
        /// Initializes a new instance of the <see cref="PdcxWrapper"/> class.
        /// </summary>
        public PdcxWrapper()
        {
            this.control = new DsiPDCX();
        }

        /// <summary>
        /// Set control's servers
        /// </summary>
        /// <param name="str">list of IPs</param>
        /// <param name="code">Show UI</param>
        public void ServerIPConfig(string str, short code)
        {
            this.control.ServerIPConfig(str, code);
        }

        /// <summary>
        /// Set control's response timeout
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds</param>
        public void SetResponseTimeout(short timeout)
        {
            this.control.SetResponseTimeout(timeout);
        }

        /// <summary>
        /// Cancel control's current request
        /// </summary>
        public void CancelRequest()
        {
            this.control.CancelRequest();
        }

        /// <summary>
        /// Process a simple transaction
        /// </summary>
        /// <param name="xml">Transaction XML</param>
        /// <returns>Response XML</returns>
        public string ProcessTransaction(string xml)
        {
            return this.control.ProcessTransaction(xml, 1, null, null);
        }

        /// <summary>
        /// Process a full transaction
        /// </summary>
        /// <param name="xml">Transaction XML</param>
        /// <param name="code">Show UI</param>
        /// <param name="csp">No idea</param>
        /// <param name="utd">Does not matter</param>
        /// <returns>Response XML</returns>
        public string ProcessTransaction(string xml, short code, string csp, string utd)
        {
            return this.control.ProcessTransaction(xml, code, csp, utd);
        }
    }
}
