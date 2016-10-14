//-------------------------------------------------------------
// <copyright file="UDPMsgBox.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------
namespace UDPMsgBox
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using MsgInterface;

    /// <summary>
    /// Class to receive UDP messages
    /// </summary>
    public class UDPMsgBox
    {
        /// <summary>
        /// Thread to run in
        /// </summary>
        public Thread MyThread;

        /// <summary>
        /// Is currently running
        /// </summary>
        private bool running;

        /// <summary>
        /// Parent that can handle messages
        /// </summary>
        private IDelegateForm parent;

        /// <summary>
        /// UDP Port
        /// </summary>
        private int port;

        /// <summary>
        /// Client instance
        /// </summary>
        private UdpClient u;

        /// <summary>
        /// Is actively listening for packets
        /// </summary>
        private bool listening  = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="UDPMsgBox"/> class.
        /// </summary>
        /// <param name="p">port number</param>
        public UDPMsgBox(int p)
        {
            this.MyThread = new Thread(new ThreadStart(this.Read));
            this.running = true;
            this.port = p;
        }

        /// <summary>
        /// Setter for parent
        /// </summary>
        /// <param name="p">new parent</param>
        public void SetParent(IDelegateForm p)
        {
            this.parent = p;
        }

        /// <summary>
        /// Listen for messages
        /// </summary>
        public void Read()
        {
            IPEndPoint e = new IPEndPoint(IPAddress.Any, 0);
            this.u = new UdpClient(this.port);

            while (this.running)
            {
                try
                {
                    this.listening = true;
                    byte[] b = this.u.Receive(ref e);
                    this.SendBytes(b);
                }
                catch (Exception ex)
                { 
                    System.Console.WriteLine(ex.ToString());
                }
            }

            this.listening = false;
        }

        /// <summary>
        /// Is currently listening
        /// </summary>
        /// <returns>yes or no</returns>
        public bool IsListening()
        {
            return this.listening;
        }

        /// <summary>
        /// Stop the listener
        /// </summary>
        public void Stop()
        {
            this.running = false;
            this.u.Close();
            this.MyThread.Join();
        }

        /// <summary>
        /// Send bytes to parent
        /// </summary>
        /// <param name="receiveBytes">bytes to send</param>
        private void SendBytes(byte[] receiveBytes)
        {
            string receiveString = System.Text.Encoding.ASCII.GetString(receiveBytes);
            Console.WriteLine("Received: " + receiveString);
            this.parent.MsgRecv(receiveString);
        }
    }
}
