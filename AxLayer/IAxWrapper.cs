//-------------------------------------------------------------
// <copyright file="IAxWrapper.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------
namespace AxLayer
{
    /// <summary>
    /// Interface for ActiveX controls used in
    /// this solution
    /// </summary>
    public interface IAxWrapper
    {
        /// <summary>
        /// Set control's servers
        /// </summary>
        /// <param name="str">list of IPs</param>
        /// <param name="code">Show UI</param>
        void ServerIPConfig(string str, short code);

        /// <summary>
        /// Set control's response timeout
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds</param>
        void SetResponseTimeout(short timeout);

        /// <summary>
        /// Cancel control's current request
        /// </summary>
        void CancelRequest();

        /// <summary>
        /// Process a simple transaction
        /// </summary>
        /// <param name="xml">Transaction XML</param>
        /// <returns>Response XML</returns>
        string ProcessTransaction(string xml);

        /// <summary>
        /// Process a full transaction
        /// </summary>
        /// <param name="xml">Transaction XML</param>
        /// <param name="code">Show UI</param>
        /// <param name="csp">No idea</param>
        /// <param name="utd">Does not matter</param>
        /// <returns>Response XML</returns>
        string ProcessTransaction(string xml, short code, string csp, string utd);
    }
}
