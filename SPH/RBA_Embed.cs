
namespace SPH
{
    using System;
    using System.IO.Ports;
    using System.Threading;

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
    public class RBA_Embed : SPH_IngenicoRBA_RS232, IStub
    {
        /// <summary>
        /// The parent constructor will open a connection
        /// to the device. This immediately closes it again
        /// and waits for permission to use the device.
        /// </summary>
        /// <param name="p">A port name</param>
        public RBA_Embed(string p) : base(p)
        {
            try
            {
                sp.Close();
            }
            catch (Exception) { }
        }

        public void SetEMV(RbaButtons emv)
        {
        }

        /// <summary>
        /// Create a SerialPort object with appropriate
        /// settings
        /// </summary>
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

        /// <summary>
        /// Take control of the device. This effectively
        /// just lets SPH_IngenicoRBA_RS232 run normally
        /// </summary>
        public void stubStart()
        {
            try {
                initPort();
                sp.Open();
                this.sphRunning = true;
                this.SPHThread = new Thread(new ThreadStart(this.Read));
                this.SPHThread.Start();
            } catch (Exception) {}
        }

        /// <summary>
        /// Release control of the device
        /// </summary>
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

        /// <summary>
        /// Catch any exceptions that arise from
        /// SPH_IngeicoRBA_RS232.HandleMsg. The most
        /// likely cause of exceptions is calling this
        /// method when the stub is not connected to
        /// the device
        /// </summary>
        /// <param name="msg"></param>
        public override void HandleMsg(string msg)
        {
            try
            {
                base.HandleMsg(msg);
            }
            catch (Exception) { }
        }
    }
}
