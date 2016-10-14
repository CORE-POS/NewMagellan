using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using MsgInterface;

namespace UDPMsgBox
{
    public class UdpState
    {
        public IPEndPoint e;
        public UdpClient u;
    }

    public class UDPMsgBox
    {
        public Thread My_Thread;
        protected bool running;
        protected DelegateForm parent;
        protected int port;
        protected UdpClient u;
        protected bool listening  = false;

        public UDPMsgBox(int p)
        {
            this.My_Thread = new Thread(new ThreadStart(this.Read));
            this.running = true;
            this.port = p;
        }

        public void SetParent(DelegateForm p) { parent = p; }

        public void Read()
        {
            IPEndPoint e = new IPEndPoint(IPAddress.Any, 0);
            u = new UdpClient(this.port);

            while (running) {
                try {
                    listening = true;
                    Byte[] b = u.Receive(ref e);
                    this.SendBytes(b);
                
                } catch (Exception ex) { 
                    System.Console.WriteLine(ex.ToString());
                }
            }
            listening = false;
        }

        public bool IsListening()
        {
            return listening;
        }

        private void SendBytes(Byte[] receiveBytes)
        {
            string receiveString = System.Text.Encoding.ASCII.GetString(receiveBytes);
            Console.WriteLine("Received: " + receiveString);
            parent.MsgRecv(receiveString);
        }

        public void Stop()
        {
            running = false;
            u.Close();
            My_Thread.Join();
        }
    }
}
