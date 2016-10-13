
namespace AxLayer
{
    public class FakeAx : AxWrapper
    {
        public void ServerIPConfig(string str, short code)
        {

        }

        public void SetResponseTimeout(short timeout)
        {

        }

        public void CancelRequest()
        {

        }

        public string ProcessTransaction(string xml)
        {
            return xml;
        }

        public string ProcessTransaction(string xml, short code, string csp, string utd)
        {
            return xml;
        }
    }
}
