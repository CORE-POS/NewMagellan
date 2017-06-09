//-------------------------------------------------------------
// <copyright file="Magellan.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------
/********************************************************************************

    Copyright 2009 Whole Foods Co-op

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
 * Magellan
 *     Main app. Starts all requested Serial Port Handlers
 * and monitors UDP for messages
 *
 * Note that exit won't work cleanly if a SerialPortHandler
 * blocks indefinitely. Use timeouts in polling reads.
*************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

using MsgInterface;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using SPH;

/// <summary>
/// Main class for the CLI app
/// </summary>
public class Magellan : IDelegateForm 
{
    /// <summary>
    /// All serial port handler modules
    /// </summary>
    private List<SerialPortHandler> sph;

    /// <summary>
    /// UDP message inbox
    /// </summary>
    private UDPMsgBox.UDPMsgBox u;

    /// <summary>
    /// UDP listening mode
    /// </summary>
    private bool asyncUDP = true;

    /// <summary>
    /// Disable RBA device control entirely
    /// </summary>
    private bool disableRBA = false;

    /// <summary>
    /// Disable RBA interactive buttons
    /// but not on-screen messaging
    /// </summary>
    private bool disableButtons = false;

    /// <summary>
    /// Concurrency lock for sending messages
    /// </summary>
    private object msgLock = new object();

    /// <summary>
    /// Counter for sent messages
    /// </summary>
    private ushort msgCount = 0;

    /// <summary>
    /// RabbitMQ transmission is enabled
    /// </summary>
    private bool mqEnabled = false;

    /// <summary>
    /// Bi-directional UDP is enabled
    /// </summary>
    private bool fullUdp = false;

    /// <summary>
    /// RabbitMQ is up and running
    /// </summary>
    private bool mqAvailable = true;

    /// <summary>
    /// UDP message handler
    /// </summary>
    private UdpClient udpClient = null;

    /// <summary>
    /// RabbitMQ connection factory
    /// </summary>
    private ConnectionFactory rabbitFactory;

    /// <summary>
    /// Connection to RabbitMQ
    /// </summary>
    private IConnection rabbitCon;

    /// <summary>
    /// RabbitMQ channel
    /// </summary>
    private IModel rabbitChannel;

    /// <summary>
    /// Initializes a new instance of the <see cref="Magellan"/> class.
    /// </summary>
    /// <param name="verbosity">output level</param>
    public Magellan(int verbosity)
    {
        var d = new Discover.Discover();
        var modules = d.GetSubClasses("SPH.SerialPortHandler");

        List<MagellanConfigPair> conf = this.ReadConfig();
        this.sph = new List<SerialPortHandler>();
        foreach (var pair in conf)
        {
            try
            {
                if (modules.Any(m => m.Name == pair.Module))
                {
                    var type = d.GetType("SPH." + pair.Module);
                    Console.WriteLine(pair.Module + ":" + pair.Port);
                    SerialPortHandler s = (SerialPortHandler)Activator.CreateInstance(type, new object[] { pair.Port });
                    s.SetParent(this);
                    s.SetVerbose(verbosity);
                    this.sph.Add(s);
                }
                else
                {
                    throw new Exception("unknown module: " + pair.Module);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine("Warning: could not initialize " + pair.Port);
                Console.WriteLine("Ensure the device is connected and you have permission to access it.");
            }
        }

        this.MonitorSerialPorts();
        this.UdpListen();
        this.FactorRabbits();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Magellan"/> class.
    /// Alternate constructor that specifies modules
    /// at compile-time
    /// </summary>
    /// <param name="args">serial port handlers</param>
    public Magellan(SerialPortHandler[] args)
    {
        this.sph = new List<SerialPortHandler>(args);
        this.MonitorSerialPorts();
        this.UdpListen();
    }

    /// <summary>
    /// Entry point method
    /// </summary>
    /// <param name="args">CLI arguments</param>
    public static void Main(string[] args)
    {
        int verbosity = 0;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-v")
            {
                verbosity = 1;
                if (i + 1 < args.Length)
                {
                    try
                    {
                        verbosity = int.Parse(args[i + 1]);
                    }
                    catch
                    {
                    }
                }
            }
        }

        new Magellan(verbosity);
        Thread.Sleep(Timeout.Infinite);
    }

    /// <summary>
    /// Handle incoming messages
    /// </summary>
    /// <param name="msg">the message</param>
    public void MsgRecv(string msg)
    {
        if (msg == "exit")
        {
            this.ShutDown();
        }
        else if (msg == "full_udp")
        {
            this.fullUdp = true;
        }
        else if (msg == "mq_up" && this.mqAvailable)
        {
            this.mqEnabled = true;
        }
        else if (msg == "mq_down")
        {
            this.mqEnabled = false;
        }
        else if (msg == "status")
        {
            byte[] body = System.Text.Encoding.ASCII.GetBytes(this.Status());
            this.GetClient().Send(body, body.Length); 
        }
        else
        {
            this.sph.ForEach(s => { s.HandleMsg(msg); });
        }
    }

    /// <summary>
    /// Send an outgoing message
    /// </summary>
    /// <param name="msg">the message</param>
    public void MsgSend(string msg)
    {
        if (this.fullUdp)
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(msg);
            this.GetClient().Send(body, body.Length); 
        }
        else if (this.mqAvailable && this.mqEnabled)
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(msg);
            this.rabbitChannel.BasicPublish(string.Empty, "core-pos", null, body);
        }
        else
        {
            lock (this.msgLock)
            {
                string filename = System.Guid.NewGuid().ToString();
                string my_location = AppDomain.CurrentDomain.BaseDirectory;
                char sep = Path.DirectorySeparatorChar;
                /**
                  Depending on msg rate I may replace "1" with a bigger value
                  as long as the counter resets at least once per 65k messages
                  there shouldn't be sequence issues. But real world disk I/O
                  may be trivial with a serial message source
                */
                if (this.msgCount % 1 == 0 && Directory.GetFiles(my_location + sep + "ss-output/").Length == 0)
                {
                    this.msgCount = 0;
                }

                filename = this.msgCount.ToString("D5") + filename;
                this.msgCount++;

                TextWriter sw = new StreamWriter(my_location + sep + "ss-output/" + sep + "tmp" + sep + filename);
                sw = TextWriter.Synchronized(sw);
                sw.WriteLine(msg);
                sw.Close();
                File.Move(
                    my_location + sep + "ss-output/" + sep + "tmp" + sep + filename,
                    my_location + sep + "ss-output/" + sep + filename);
            }
        }
    }

    /// <summary>
    /// Stop all the driver threads
    /// </summary>
    public void ShutDown()
    {
        try
        {
            this.sph.ForEach(s => { s.Stop(); });
            this.u.Stop();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    /// <summary>
    /// Read JSON configuration
    /// </summary>
    /// <returns>configuration values</returns>
    private List<MagellanConfigPair> JsonConfig()
    {
        string my_location = AppDomain.CurrentDomain.BaseDirectory;
        char sep = Path.DirectorySeparatorChar;
        string ini_file = my_location + sep + ".." + sep + ".." + sep + ".." + sep + "ini.json";
        List<MagellanConfigPair> conf = new List<MagellanConfigPair>();
        if (!File.Exists(ini_file))
        {
            return conf;
        }

        string ini_json = File.ReadAllText(ini_file);
        try
        {
            JObject o = JObject.Parse(ini_json);

            // filter list to valid entries
            var valid = o["NewMagellanPorts"].Where(p => p["port"] != null && p["module"] != null);

            // map entries to ConfigPair objects
            var pairs = valid.Select(p => new MagellanConfigPair() { Port = (string)p["port"], Module = (string)p["module"] });
            conf = pairs.ToList();

            // print errors for invalid entries
            o["NewMagellanPorts"].Where(p => p["port"] == null).ToList().ForEach(p => 
            {
                Console.WriteLine("Missing the \"port\" setting. JSON:");
                Console.WriteLine(p);
            });

            // print errors for invalid entries
            o["NewMagellanPorts"].Where(p => p["module"] == null).ToList().ForEach(p => 
            {
                Console.WriteLine("Missing the \"module\" setting. JSON:");
                Console.WriteLine(p);
            });
        }
        catch (NullReferenceException)
        {
            // probably means no NewMagellanPorts key in ini.json
            // not a fatal problem
        }
        catch (Exception ex)
        {
            // unexpected exception
            Console.WriteLine(ex);
        }
        try {
            JObject o = JObject.Parse(ini_json);
            var ua = (bool)o["asyncUDP"];
            this.asyncUDP = ua;
        } catch (Exception ex) {}
        try {
            JObject o = JObject.Parse(ini_json);
            var drb = (bool)o["disableRBA"];
            this.disableRBA = drb;
        } catch (Exception) {}
        try {
            JObject o = JObject.Parse(ini_json);
            var dbt = (bool)o["disableButtons"];
            this.disableButtons = dbt;
        } catch (Exception) {}

        return conf;
    }

    /// <summary>
    /// Read old-style <c>ports.conf</c>
    /// </summary>
    /// <returns>configuration values</returns>
    private List<MagellanConfigPair> ReadConfig()
    {
        /**
         * Look for settings in ini.json if it exists
         * and the library DLL exists
         */
        List<MagellanConfigPair> json_ports = this.JsonConfig();
        if (json_ports.Count > 0)
        {
            return json_ports;
        }

        string my_location = AppDomain.CurrentDomain.BaseDirectory;
        char sep = Path.DirectorySeparatorChar;
        StreamReader fp = new StreamReader(my_location + sep + "ports.conf");
        List<MagellanConfigPair> conf = new List<MagellanConfigPair>();
        HashSet<string> hs = new HashSet<string>();
        string line;
        while ((line = fp.ReadLine()) != null)
        {
            line = line.TrimStart(null);
            if (line == string.Empty || line[0] == '#')
            {
                continue;
            }

            string[] pieces = line.Split(null);
            if (pieces.Length != 2)
            {
                Console.WriteLine("Warning: malformed port.conf line: " + line);
                Console.WriteLine("Format: <port_string> <handler_class_name>");
            }
            else if (hs.Contains(pieces[0]))
            {
                Console.WriteLine("Warning: device already has a module attached.");
                Console.WriteLine("Line will be ignored: " + line);
            }
            else
            {
                var pair = new MagellanConfigPair();
                pair.Port = pieces[0];
                pair.Module = pieces[1];
                conf.Add(pair);
                hs.Add(pieces[0]);
            }
        }    

        return conf;
    }

    /// <summary>
    /// Start all the serial port handler threads
    /// </summary>
    private void MonitorSerialPorts()
    {
        var valid = this.sph.Where(s => s != null);
        valid.ToList().ForEach(s => { s.SPHThread.Start(); });
    }

    /// <summary>
    /// Get current serial port status
    /// </summary>
    /// <returns>Status information</returns>
    private string Status()
    {
        string ret = string.Empty;
        foreach (var s in this.sph)
        {
            ret += s.Status() + "\n";
        }

        return ret;
    }

    /// <summary>
    /// Listen for messages over UDP
    /// </summary>
    private void UdpListen()
    {
        this.u = new UDPMsgBox.UDPMsgBox(9450, this.asyncUDP);
        this.u.SetParent(this);
        this.u.MyThread.Start();
    }

    /// <summary>
    /// Initialize UDP client
    /// </summary>
    /// <returns>the client</returns>
    private UdpClient GetClient()
    {
        if (this.udpClient == null)
        {
            this.udpClient = new UdpClient();
            this.udpClient.Connect(System.Net.IPAddress.Parse("127.0.0.1"), 9451);
        }

        return this.udpClient;
    }

    /// <summary>
    /// Connect to RabbitMQ
    /// </summary>
    private void FactorRabbits()
    {
        try
        {
            this.rabbitFactory = new ConnectionFactory();
            this.rabbitFactory.HostName = "localhost";
            this.rabbitCon = this.rabbitFactory.CreateConnection();
            this.rabbitChannel = this.rabbitCon.CreateModel();
            this.rabbitChannel.QueueDeclare("core-pos", false, false, false, null);
        }
        catch (Exception)
        {
            this.mqAvailable = false;
        }
    }
}

/// <summary>
/// Simple class to hold config key/value pairs
/// </summary>
public class MagellanConfigPair
{
    /// <summary>
    /// Gets or sets port device file name
    /// </summary>
    public string Port { get; set; }

    /// <summary>
    /// Gets or sets serial port handler module
    /// </summary>
    public string Module { get; set; }
}
