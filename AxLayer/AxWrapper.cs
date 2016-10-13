
namespace AxLayer
{
    public interface AxWrapper
    {
        void ServerIPConfig(string str, short code);
        void SetResponseTimeout(short timeout);
        void CancelRequest();
        string ProcessTransaction(string xml);
        string ProcessTransaction(string xml, short code, string csp, string utd);
    }
}
