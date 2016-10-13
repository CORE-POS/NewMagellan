
using DSIPDCXLib;

namespace AxLayer
{
    public class PdcxWrapper : AxWrapper
    {
        private DsiPDCX control;
        public PdcxWrapper()
        {
            control = new DsiPDCX();
        }

        public void ServerIPConfig(string str, short code)
        {
            control.ServerIPConfig(str, code);
        }

        public void SetResponseTimeout(short timeout)
        {
            control.SetResponseTimeout(timeout);
        }

        public void CancelRequest()
        {
            control.CancelRequest();
        }

        public string ProcessTransaction(string xml)
        {
            return control.ProcessTransaction(xml, 1, null, null);
        }

        public string ProcessTransaction(string xml, short code, string csp, string utd)
        {
            return control.ProcessTransaction(xml, code, csp, utd);
        }
    }
}
