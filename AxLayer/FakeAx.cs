//-------------------------------------------------------------
// <copyright file="FakeAx.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//------------------------------------------------------------

namespace AxLayer
{
    /// <summary>
    /// Mock wrapper that contains no ActiveX control
    /// </summary>
    public class FakeAx : IAxWrapper
    {
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
        /// Echoes back the same XML
        /// </summary>
        /// <param name="xml">Transaction XML</param>
        /// <returns>XML result</returns>
        public string ProcessTransaction(string xml)
        {
            return xml;
        }

        /// <summary>
        /// Echoes back the same XML too
        /// </summary>
        /// <param name="xml">Transaction XML</param>
        /// <param name="code">The parameter is not used.</param>
        /// <param name="csp">The parameter is not used.</param>
        /// <param name="utd">The parameter is not used.</param>
        /// <returns>XML result</returns>
        public string ProcessTransaction(string xml, short code, string csp, string utd)
        {
            return xml;
        }
    }
}
