/*******************************************************************************

    Copyright 2014 Whole Foods Co-op

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

namespace SPH
{

    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Xml;
    using System.Drawing;
    using System.Linq;
    using System.Collections.Generic;
    using AxLayer;

    public class SPH_Datacap_EMVX : SerialPortHandler
    {
        /// <summary>
        /// EMV ActiveX control
        /// </summary>
        private IAxWrapper emv_ax_control = null;

        /// <summary>
        /// Non-EMV ActiveX control for EBT and sig capture
        /// </summary>
        private IAxWrapper pdc_ax_control = null;

        /// <summary>
        /// The device identifier is automatically inserted into
        /// XML messages from POS so the POS does not need to
        /// keep track of it
        /// </summary>
        private string device_identifier = null;

        /// <summary>
        /// The com port number is handled the same way as the
        /// device identifier
        /// </summary>
        private string com_port = "0";

        /// <summary>
        /// Processor server(s) with semicolon delimiter
        /// </summary>
        protected string server_list = "x1.mercurypay.com;x2.backuppay.com";

        /// <summary>
        /// Port where serial port handler will accept HTTP messages
        /// </summary>
        protected int LISTEN_PORT = 8999; // acting as a Datacap stand-in

        /// <summary>
        /// Timeout in seconds to wait for response from processor
        /// </summary>
        protected short CONNECT_TIMEOUT = 60;

        /// <summary>
        /// This is used to rewrite sequence numbers in XML
        /// requests to ensure they're actually sequential
        /// without requiring the POS to keep track of them
        /// </summary>
        protected string sequence_no = null;

        /// <summary>
        /// Stub for sharing RBA-compatible devices
        /// </summary>
        private IStub rba = null;

        /// <summary>
        /// File name of optional XML log
        /// </summary>
        private string xml_log = null;

        /// <summary>
        /// Enable logging of sent and received XML
        /// </summary>
        private bool enable_xml_log = false;

        /// <summary>
        /// Track whether this is actively listening for 
        /// HTTP messages. Used to avoid timing issues
        /// in testing
        /// </summary>
        private bool listening = false;

        /// <summary>
        /// The HTTP listener
        /// </summary>
        TcpListener http = null;

        /// <summary>
        /// Port is not simply "COM#" here. It should be a
        /// device identifer followed by a colon followed by
        /// a number indicating the COM port
        /// </summary>
        /// <param name="p">Device and COM IDs</param>
        public SPH_Datacap_EMVX(string p) : base(p)
        {
            device_identifier = p;
            if (p.Contains(":"))
            {
                string[] parts = p.Split(new char[] { ':' }, 2);
                device_identifier = parts[0];
                com_port = parts[1];
            }
            if (device_identifier == "INGENICOISC250_MERCURY_E2E")
            {
                rba = new RBA_Embed("COM" + com_port);
            }

            string my_location = AppDomain.CurrentDomain.BaseDirectory;
            char sep = Path.DirectorySeparatorChar;
            xml_log = my_location + sep + "xml.log";
        }

        /// <summary>
        /// Initialize both ActiveX controls and the 
        /// RBA stub, if applicable
        /// </summary>
        /// <returns>Always true</returns>
        protected bool initDevice()
        {
            if (pdc_ax_control == null)
            {
                var d = new Discover.Discover();
                try
                {
                    var type = d.GetType("AxLayer.PdcxWrapper");
                    pdc_ax_control = (IAxWrapper)Activator.CreateInstance(type);
                }
                catch (Exception)
                {
                    pdc_ax_control = new FakeAx();
                }
                pdc_ax_control.ServerIPConfig(server_list, 0);
                pdc_ax_control.SetResponseTimeout(CONNECT_TIMEOUT);
                InitPDCX();
            }
            pdc_ax_control.CancelRequest();

            if (emv_ax_control == null)
            {
                var d = new Discover.Discover();
                try
                {
                    var type = d.GetType("AxLayer.EmvWrapper");
                    emv_ax_control = (IAxWrapper)Activator.CreateInstance(type);
                }
                catch (Exception)
                {
                    emv_ax_control = new FakeAx();
                }
            }
            PadReset();

            if (rba != null)
            {
                rba.SetParent(this.parent);
                rba.SetVerbose(this.verbose_mode);
                rba.stubStart();
            }

            return true;
        }

        /// <summary>
        /// Inject ActiveX controls. This is used soley
        /// for testing.
        /// </summary>
        /// <param name="emv">The EMV ActiveX control</param>
        /// <param name="pdc">The non-EMV ActiveX control</param>
        public void SetControls(IAxWrapper emv, IAxWrapper pdc)
        {
            emv_ax_control = emv;
            pdc_ax_control = pdc;
        }

        /// <summary>
        /// Driver listens over TCP for incoming HTTP data. Driver
        /// is providing a web-service style endpoint so POS behavior
        /// does not have to change. Rather than POSTing information to
        /// a remote processor it POSTs information to the driver.
        /// 
        /// Driver strips off headers, feeds XML into the dsiEMVX control,
        /// then sends the response back to the client.
        /// </summary>
        public override void Read()
        {
            initDevice();
            http = new TcpListener(IPAddress.Loopback, LISTEN_PORT);
            http.Start();
            byte[] buffer = new byte[10];
            while (sphRunning)
            {
                try
                {
                    listening = true;
                    using (TcpClient client = http.AcceptTcpClient())
                    {
                        client.ReceiveTimeout = 100;
                        using (NetworkStream stream = client.GetStream())
                        {
                            string message = "";
                            int bytes_read = 0;
                            do
                            {
                                bytes_read = stream.Read(buffer, 0, buffer.Length);
                                message += System.Text.Encoding.ASCII.GetString(buffer, 0, bytes_read);
                            } while (stream.DataAvailable);

                            if (rba != null)
                            {
                                rba.stubStop();
                            }

                            message = GetHttpBody(message);

                            // Send EMV messages to EMVX, others
                            // to PDCX
                            string result = "";
                            if (message.Contains("EMV"))
                            {
                                PadReset();
                                result = ProcessEMV(message);
                            }
                            else if (message != "")
                            {
                                result = ProcessPDC(message);
                            }
                            result = WrapHttpResponse(result);
                            if (this.verbose_mode > 0)
                            {
                                Console.WriteLine(result);
                            }

                            byte[] response = System.Text.Encoding.ASCII.GetBytes(result);
                            stream.Write(response, 0, response.Length);
                        }
                        client.Close();
                    }
                }
                catch (Exception ex)
                {
                    if (verbose_mode > 0)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }
            listening = false;
        }

        /// <summary>
        /// Check if HTTP listener is active
        /// </summary>
        /// <returns>boolean is listening</returns>
        public bool IsListening()
        {
            return listening;
        }

        /// <summary>
        /// Pull HTTP body out of string. Simply looking
        /// for blank line between headers and body
        /// </summary>
        protected string GetHttpBody(string http_request)
        {
            StringReader sr = new StringReader(http_request);
            string line;
            string ret = "";
            bool headers_over = false;
            while ((line = sr.ReadLine()) != null)
            {
                if (!headers_over && line == "")
                {
                    headers_over = true;
                }
                else if (headers_over)
                {
                    ret += line;
                }
            }

            return ret;
        }

        /// <summary>
        /// Add simple HTTP headers to content string
        /// </summary>
        protected string WrapHttpResponse(string http_response)
        {
            string headers = "HTTP/1.0 200 OK\r\n"
                + "Connection: close\r\n"
                + "Content-Type: text/xml\r\n"
                + "Content-Length: " + http_response.Length + "\r\n"
                + "Access-Control-Allow-Origin: http://localhost\r\n"
                + "\r\n";

            return headers + http_response;
        }

        /// <summary>
        /// Handle message recived from POS via the parent,
        /// typically NewMagellan
        /// 
        /// Request and Reboot messages will reset both
        /// ActiveX controls and the RBA stub, if applicable
        ///
        /// Signature requests are handled via the non-EMV
        /// ActiveX control
        ///
        /// All other messages are passed through to the RBA
        /// stub, if applicable or ignored otherwise
        /// </summary>
        /// <param name="msg"></param>
        public override void HandleMsg(string msg)
        {
            // optional predicate for "termSig" message
            // predicate string is displayed on sig capture screen
            if (msg.Length > 7 && msg.Substring(0, 7) == "termSig")
            {
                //sig_message = msg.Substring(7);
                msg = "termSig";
            }
            switch (msg)
            {
                case "termReset":
                case "termReboot":
                    if (rba != null)
                    {
                        rba.stubStop();
                    }
                    initDevice();
                    break;
                case "termSig":
                    if (rba != null)
                    {
                        rba.stubStop();
                    }
                    GetSignature();
                    break;
                case "termManual":
                case "termApproved":
                case "termGetType":
                case "termGetTypeWithFS":
                case "termGetPin":
                case "termWait":
                    if (rba != null)
                    {
                        rba.HandleMsg(msg);
                    }
                    break;
            }
        }

        /// <summary>
        /// Process a transaction using the EMV ActiveX control
        /// </summary>
        /// <param name="xml">XML transaction</param>
        /// <returns>XML response</returns>
        protected string ProcessEMV(string xml)
        {
            /* 
               Substitute values into the XML request
               This is so the driver can handle any change
               in which hardware device is connected as well
               as so tracking SequenceNo values is not POS'
               problem.
            */
            xml = xml.Trim(new char[] { '"' });
            xml = xml.Replace("{{SequenceNo}}", SequenceNo());
            if (IsCanadianDeviceType(this.device_identifier))
            {
                // tag name is different in this case;
                // replace placeholder then the open/close tags
                xml = xml.Replace("{{SecureDevice}}", this.device_identifier);
                xml = xml.Replace("SecureDevice", "PadType");
            }
            else
            {
                xml = xml.Replace("{{SecureDevice}}", SecureDeviceToEmvType(this.device_identifier));
            }
            xml = xml.Replace("{{ComPort}}", com_port);
            if (this.verbose_mode > 0)
            {
                Console.WriteLine(xml);
            }

            try
            {
                /**
                  Extract HostOrIP field and split it on commas
                  to allow multiple IPs
                */
                XmlDocument request = new XmlDocument();
                request.LoadXml(xml);
                var IPs = request.SelectSingleNode("TStream/Transaction/HostOrIP").InnerXml.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                string result = "";
                foreach (string IP in IPs)
                {
                    // try request with an IP
                    request.SelectSingleNode("TStream/Transaction/HostOrIP").InnerXml = IP;
                    result = emv_ax_control.ProcessTransaction(request.OuterXml);
                    if (enable_xml_log)
                    {
                        using (StreamWriter sw = new StreamWriter(xml_log, true))
                        {
                            sw.WriteLine(DateTime.Now.ToString() + " (send emv): " + request.OuterXml);
                            sw.WriteLine(DateTime.Now.ToString() + " (recv emv): " + result);
                        }
                    }
                    XmlDocument doc = new XmlDocument();
                    try
                    {
                        doc.LoadXml(result);
                        // track SequenceNo values in responses
                        XmlNode sequence = doc.SelectSingleNode("RStream/CmdResponse/SequenceNo");
                        sequence_no = sequence.InnerXml;
                        XmlNode return_code = doc.SelectSingleNode("RStream/CmdResponse/DSIXReturnCode");
                        XmlNode origin = doc.SelectSingleNode("RStream/CmdResponse/ResponseOrigin");
                        /**
                          On anything that is not a local connectivity failure, exit the
                          loop and return the result without trying any further IPs.
                        */
                        if (origin.InnerXml != "Client" || return_code.InnerXml != "003006")
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // response was invalid xml
                        if (this.verbose_mode > 0)
                        {
                            Console.WriteLine(ex);
                        }
                        // status is unclear so do not attempt 
                        // another transaction
                        break;
                    }
                }

                return result;

            }
            catch (Exception ex)
            {
                // request was invalid xml
                if (this.verbose_mode > 0)
                {
                    Console.WriteLine(ex);
                }
            }

            return "";
        }

        /// <summary>
        /// Process a transaction using the non-EMV ActiveX
        /// control
        /// </summary>
        /// <param name="xml">XML transaction</param>
        /// <returns>XML response</returns>
        protected string ProcessPDC(string xml)
        {
            xml = xml.Trim(new char[] { '"' });
            xml = xml.Replace("{{SequenceNo}}", SequenceNo());
            xml = xml.Replace("{{SecureDevice}}", this.device_identifier);
            xml = xml.Replace("{{ComPort}}", com_port);
            if (this.verbose_mode > 0)
            {
                Console.WriteLine(xml);
            }

            string ret = "";
            ret = pdc_ax_control.ProcessTransaction(xml, 1, null, null);
            if (enable_xml_log)
            {
                using (StreamWriter sw = new StreamWriter(xml_log, true))
                {
                    sw.WriteLine(DateTime.Now.ToString() + " (send pdc): " + xml);
                    sw.WriteLine(DateTime.Now.ToString() + " (recv pdc): " + ret);
                }
            }

            return ret;
        }

        /// <summary>
        /// Get the current sequence number OR the default
        /// </summary>
        protected string SequenceNo()
        {
            return sequence_no != null ? sequence_no : "0010010010";
        }

        /// <summary>
        /// Send device initialization message to the
        /// non-EMV ActiveX control
        /// </summary>
        /// <returns>XML response</returns>
        protected string InitPDCX()
        {
            string xml = "<?xml version=\"1.0\"?>"
                + "<TStream>"
                + "<Admin>"
                + "<MerchantID>MerchantID</MerchantID>"
                + "<TranCode>SecureDeviceInit</TranCode>"
                + "<TranType>Setup</TranType>"
                + "<SecureDevice>" + this.device_identifier + "</SecureDevice>"
                + "<ComPort>" + this.com_port + "</ComPort>"
                + "<PadType>" + SecureDeviceToPadType(device_identifier) + "</PadType>"
                + "</Admin>"
                + "</TStream>";

            return ProcessPDC(xml);
        }

        /// <summary>
        /// Send a reset message to the EMV ActiveX control
        /// </summary>
        /// <returns>XML response</returns>
        protected string PadReset()
        {
            string xml = "<?xml version=\"1.0\"?>"
                + "<TStream>"
                + "<Transaction>"
                + "<HostOrIP>127.0.0.1</HostOrIP>"
                + "<MerchantID>MerchantID</MerchantID>"
                + "<TranCode>EMVPadReset</TranCode>"
                + "<SecureDevice>" + SecureDeviceToEmvType(this.device_identifier) + "</SecureDevice>"
                + "<ComPort>" + this.com_port + "</ComPort>"
                + "<SequenceNo>" + SequenceNo() + "</SequenceNo>"
                + "</Transaction>"
                + "</TStream>";

            return ProcessEMV(xml);
        }

        /// <summary>
        /// Tell the device to enter signature capture mode.
        /// On success this will write a bitmap to the filesystem and
        /// send a message to the POS notifying it of the image
        /// </summary>
        /// <returns>Always null</returns>
        protected string GetSignature()
        {
            var reset = PadReset();

            string xml = "<?xml version=\"1.0\"?>"
                + "<TStream>"
                + "<Transaction>"
                + "<MerchantID>MerchantID</MerchantID>"
                + "<TranCode>GetSignature</TranCode>"
                + "<SecureDevice>" + this.device_identifier + "</SecureDevice>"
                + "<ComPort>" + this.com_port + "</ComPort>"
                + "<Account>"
                + "<AcctNo>SecureDevice</AcctNo>"
                + "</Account>"
                + "</Transaction>"
                + "</TStream>";
            string result = ProcessPDC(xml);
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.LoadXml(result);
                XmlNode status = doc.SelectSingleNode("RStream/CmdResponse/CmdStatus");
                if (status.InnerText != "Success")
                {
                    return null;
                }
                string sigdata = doc.SelectSingleNode("RStream/Signature").InnerText;
                List<Point> points = SigDataToPoints(sigdata);

                int ticks = Environment.TickCount;
                string my_location = AppDomain.CurrentDomain.BaseDirectory;
                char sep = Path.DirectorySeparatorChar;
                while (File.Exists(my_location + sep + "ss-output/" + sep + ticks))
                {
                    ticks++;
                }
                string filename = my_location + sep + "ss-output" + sep + "tmp" + sep + ticks + ".bmp";
                BitmapBPP.Signature sig = new BitmapBPP.Signature(filename, points);
                parent.MsgSend("TERMBMP" + ticks + ".bmp");
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// Devices actually often have two slightly different
        /// identifiers. This maps the primary device identifier stored
        /// in this object to a secondary pad type identifier
        /// </summary>
        /// <param name="device">Device identifier</param>
        /// <returns>Pad type identifier</returns>
        protected string SecureDeviceToPadType(string device)
        {
            switch (device)
            {
                case "VX805XPI":
                case "VX805XPI_MERCURY_E2E":
                    return "VX805";
                case "INGENICOISC250":
                case "INGENICOISC250_MERCURY_E2E":
                    return "ISC250";
                default:
                    return device;
            }
        }

        /// <summary>
        /// The EMV ActiveX controller potentially adds a 3rd
        /// device identifier. This is another mapping like
        /// SecureDeviceToPadType
        /// </summary>
        /// <param name="device">Primary device identifier</param>
        /// <returns>Tertiary device identifier</returns>
        protected string SecureDeviceToEmvType(string device)
        {
            switch (device)
            {
                case "VX805XPI":
                case "VX805XPI_MERCURY_E2E":
                    return "EMV_VX805_MERCURY";
                case "INGENICOISC250_MERCURY_E2E":
                    return "EMV_ISC250_MERCURY";
                default:
                    return "EMV_" + device;
            }

        }

        /// <summary>
        /// Determine if the device identifier is Canadian
        /// </summary>
        /// <param name="device">Device identifier</param>
        /// <returns>boolean; true means Canadian</returns>
        protected bool IsCanadianDeviceType(string device)
        {
            switch (device)
            {
                case "Paymentech1":
                case "Global1":
                case "Moneris1":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Signature data comes in as a series of points 
        /// e.g. x1,y1:x2,y2:x3,y3:...
        /// This converts them to a list of points
        /// </summary>
        /// <param name="data">Signature data</param>
        /// <returns>List of corresponding points</returns>
        protected List<Point> SigDataToPoints(string data)
        {
            char[] comma = new char[] { ',' };
            char[] colon = new char[] { ':' };
            var pairs = from pair in data.Split(colon)
                        select pair.Split(comma);
            var points = from pair in pairs
                         where pair.Length == 2
                         select new Point(CoordsToInt(pair[0]), CoordsToInt(pair[1]));

            return points.ToList();
        }

        /// <summary>
        /// Convert a string coordinate to an integer.
        /// The special value "#" representing pen lifted
        /// is translated to zero
        /// </summary>
        /// <param name="coord">String coordinate</param>
        /// <returns>Integer coordinate</returns>
        protected int CoordsToInt(string coord)
        {
            if (coord == "#")
            {
                return 0;
            }
            else
            {
                return Int32.Parse(coord);
            }
        }

        /// <summary>
        /// Ensure that the HTTP listener is stopped, too
        /// </summary>
        new public void Stop()
        {
            sphRunning = false;
            if (http != null) {
                http.Stop();
            }
            SPHThread.Join();
            Console.WriteLine("SPH Stopped");
        }


    }

}