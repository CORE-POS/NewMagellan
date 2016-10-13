using DSIEMVXLib;
namespace AxLayer
{
    public class EmvWrapper : AxWrapper
    {
        private DsiEMVX control;
        public EmvWrapper()
        {
            control = new DsiEMVX();
        }

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
            return control.ProcessTransaction(xml);
        }

        public string ProcessTransaction(string xml, short code, string csp, string utd)
        {
            return control.ProcessTransaction(xml);
        }
    }
}
