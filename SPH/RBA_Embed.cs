using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading;

namespace SPH
{
    /// <summary>
    /// Embed a raw serial port handler in a 3rd-party
    /// managed handler. Another handler, e.g. EMVX,
    /// can create an instance of this class and use
    /// stubStart() / stubStop() to share control of the
    /// port between itself and this class. Unlike
    /// RBA_Stub this class will accept swipes and PIN
    /// entries. Doing so allows for streamlined EBT
    /// handling without changing PA-DSS or EMV
    /// certificate scope.
    /// </summary>
    public class RBA_Embed : SPH_IngenicoRBA_RS232
    {
        public RBA_Embed(string p) : base(p)
        {
            // Close inherited serial port and
            // wait for explicit initialization
            try
            {
                sp.Close();
            }
            catch (Exception) { }
        }

        private void initPort()
        {
            sp = new SerialPort();
            sp.PortName = this.port;
            sp.BaudRate = 19200;
            sp.DataBits = 8;
            sp.StopBits = StopBits.One;
            sp.Parity = Parity.None;
            sp.RtsEnable = true;
            sp.Handshake = Handshake.None;
            sp.ReadTimeout = 500;
        }

        public void stubStart()
        {
            initPort();
            sp.Open();
            this.sphRunning = true;
            this.SPHThread = new Thread(new ThreadStart(this.Read));
            this.SPHThread.Start();
        }

        public void stubStop()
        {
            this.sphRunning = false;
            try
            {
                sp.Close();
            }
            catch (Exception) { }
            this.SPHThread.Join();
        }
    }
}
