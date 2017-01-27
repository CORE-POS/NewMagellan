/*******************************************************************************

    Copyright 2010 Whole Foods Co-op

    This file is part of IT CORE.

    IT CORE is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.

    IT CORE is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    in the file license.txt along with IT CORE; if not, write to the Free Software
    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

*********************************************************************************/

/*************************************************************
 * SPH_Ingenico_i6550
 *     SerialPortHandler implementation for the Ingenico 
 *     signature capture devices using Retail Base
 *     Application (RBA). Tested with i6550, should work
 *     with i6580, i6770, and i6780 as well. Two other devices,
 *    i3070 and i6510, speak the same language but minor
 *    tweaking would need to be done to account for those
 *    devices not doing signature capture.
 *
 * Sets up a serial connection in the constructor
 *
 * Polls for data in Read(), writing responses back to the
 * device as needed
 *
*************************************************************/
using System;
using System.IO.Ports;
using System.Threading;
using System.Collections;

namespace SPH
{

    /// <summary>
    /// A simplified stub that displays a payment selection
    /// screen, passes that selection to POS, and displays
    /// a "wait" message
    /// </summary>
    public class RBA_Stub : SPH_IngenicoRBA_Common, IStub
    {
        new private SerialPort sp = null;

        /// <summary>
        /// Can safely skip the parent constructor in this case
        /// </summary>
        /// <param name="p"></param>
        public RBA_Stub(string p)
        {
            this.port = p;
        }

        /// <summary>
        /// Initialize SerialPort with correct settings
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
        /// Take control of the device
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
            try {
                sp.Close();
            } catch (Exception) { }
            this.SPHThread.Join();
        }

        public void addScreenMessage(string message)
        {
            try {
                WriteMessageToDevice(SetVariableMessage("104", message));
            } catch (Exception) { }
        }

        /// <summary>
        /// Write a message to the device
        /// </summary>
        /// <param name="b">The message</param>
        private void ByteWrite(byte[] b)
        {
            if (this.verbose_mode > 1) {
                Console.WriteLine("Sent:");
                foreach (byte a in b) {
                    if (a < 10) {
                        Console.Write("0{0} ",a);
                        } else {
                            Console.Write("{0} ",a);
                        }
                }
                Console.WriteLine();
            }

            sp.Write(b,0,b.Length);

            System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
            Console.WriteLine(enc.GetString(b));
        }

        /// <summary>
        /// Attempt to ensure each message is ACK'd before
        /// sending an additional message
        /// </summary>
        /// <param name="b">The message</param>
        private void ConfirmedWrite(byte[] b)
        {
            if (this.verbose_mode > 0) {
                Console.WriteLine("Tried to write");
            }

            int count=0;
            while (last_message != null && count++ < 5) {
                Thread.Sleep(10);
            }
            last_message = b;
            ByteWrite(b);

            if (this.verbose_mode > 0) {
                System.Console.WriteLine("wrote");
            }
        }

        /// <summary>
        /// Calculate message checksum
        /// </summary>
        /// <param name="b">The message</param>
        /// <returns>Checksum byte</returns>
        private byte[] GetLRC(byte[] b)
        {
            byte[] ret = new byte[b.Length+1];
            ret[0] = b[0]; // STX
            byte lrc = 0;
            for (int i=1; i < b.Length; i++) {
                lrc ^= b[i];
                ret[i] = b[i];
            }
            ret[b.Length] = lrc;

            return ret;
        }

        /// <summary>
        /// Tell terminal to display payment-selection screen
        /// Correct sleep time here may be hardware-dependent
        /// </summary>
        private void showPaymentScreen()
        {
            try {
                WriteMessageToDevice(GetCardType());
                Thread.Sleep(1500);
                addPaymentButtons();
            } catch (Exception) {
            }
        }

        /// <summary>
        /// Force the device to display payment buttons
        /// </summary>
        private void addPaymentButtons()
        {
            try {
                char fs = (char)0x1c;

                // standard credit/debit/ebt/gift
                //string buttons = "Bbtna,S"+fs+"Bbtnb,S"+fs+"Bbtnc,S"+fs+"Bbtnd,S";

                // CHIP+PIN button in place of credit & debit
                string buttons = "Bbtnb,CHIP+PIN"+fs+"Bbtnb,S"+fs+"Bbtnc,S"+fs+"Bbtnd,S";

                WriteMessageToDevice(UpdateScreenMessage(buttons));
            } catch (Exception) {
            }
        }

        /// <summary>
        /// The main thread of exection just reads from
        /// the device. The only messages it pays attention to
        /// are ACKs, NAKs, and payment type selections
        /// </summary>
        override public void Read()
        {
            showPaymentScreen();
            System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
            int ackCount = 0;

            ArrayList bytes = new ArrayList();
            while (sphRunning) {
                try {
                    int b = sp.ReadByte();
                    if (b == 0x06) {
                        // ACK
                        if (this.verbose_mode > 0) {
                            Console.WriteLine("ACK!");
                        }
                        last_message = null;
                        ackCount++;
                    } else if (b == 0x15) {
                        // NAK
                        // Do not re-send
                        // RBA_Stub is not vital functionality
                        if (this.verbose_mode > 0) {
                            Console.WriteLine("NAK!");
                        }
                        last_message = null;
                    } else {
                        // part of a message
                        // force to be byte-sized
                        bytes.Add(b & 0xff); 
                    }
                    if (bytes.Count > 2 && (int)bytes[bytes.Count-2] == 0x3) {
                        // end of message, send ACK
                        ByteWrite(new byte[1]{0x6}); 
                        // convoluted casting required to get
                        // values out of ArrayList and into
                        // a byte array
                        byte[] buffer = new byte[bytes.Count];
                        for (int i=0; i<bytes.Count; i++) {
                            buffer[i] = (byte)((int)bytes[i] & 0xff);
                            Console.Write(buffer[i] + " ");
                        }
                        if (Choice(enc.GetString(buffer))) {
                            WriteMessageToDevice(SimpleMessageScreen("Waiting for total"));
                        }
                        bytes.Clear();
                    }
                } catch (TimeoutException) {
                    // expected; not an issue
                } catch (Exception ex) {
                    if (this.verbose_mode > 0) {
                        Console.WriteLine(ex);
                    }
                }
            }
        }

        /// <summary>
        /// Send payment selection choice to the POS
        /// </summary>
        /// <param name="str">selection from the device</param>
        /// <returns>boolean indicator if message was sent to POS</returns>
        private bool Choice(string str)
        {
            bool ret = false;
            if (str.Substring(1,4) == "24.0") {
                switch (str.Substring(5,1)) {
                    case "A":
                        // debit
                        ret = true;
                        parent.MsgSend("TERM:DCDC");
                        break;
                    case "B":
                        // credit
                        ret = true;
                        parent.MsgSend("TERM:DCCC");
                        break;
                    case "C":
                        // ebt cash
                        parent.MsgSend("TERM:DCEC");
                        ret = true;
                        break;
                    case "D":
                        // ebt food
                        parent.MsgSend("TERM:DCEF");
                        ret = true;
                        break;
                    default:
                        break;
                }
            }

            return ret;
        }

        /// <summary>
        /// Wrapper to checksum and ACK-confirm a message
        /// to the device
        /// </summary>
        /// <param name="msg">The message</param>
        public override void WriteMessageToDevice(byte[] msg)
        {
            ConfirmedWrite(GetLRC(msg));
        }

        /// <summary>
        /// The primary SerialPortHandler may pass POS messages
        /// through to the stub. This stub simply ignores most of
        /// them with the exception of termApproved
        /// </summary>
        /// <param name="msg">Message from the POS</param>
        public override void HandleMsg(string msg)
        {
            if (msg != "termApproved")
            {
                return;
            }

            try {
                stubStop();
                initPort();
                sp.Open();
                WriteMessageToDevice(SimpleMessageScreen("Approved"));
                sp.Close();
            } catch (Exception) {}
        }
    }

}
