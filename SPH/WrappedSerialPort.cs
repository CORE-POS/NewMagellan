using System;
using System.IO.Ports;

namespace SPH
{
    public class WrappedSerialPort : IPortWrapper
    {
        private SerialPort sp;
        public WrappedSerialPort(SerialPort port)
        {
            sp = port;
        }

        public SerialPort Raw()
        {
            return sp;
        }

        public void Open()
        {
            sp.Open();
        }
        
        public void Close()
        {
            sp.Close();
        }

        public int ReadByte()
        {
            return sp.ReadByte();
        }

        public void Write(string msg)
        {
            sp.Write(msg);
        }

        public void Write(byte[] msg, int offset, int count)
        {
            sp.Write(msg, offset, count);
        }
    }
}
