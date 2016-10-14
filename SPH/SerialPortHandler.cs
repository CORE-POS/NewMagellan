//-------------------------------------------------------------
// <copyright file="SerialPortHandler.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------
/*******************************************************************************

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
 * SerialPortHandler
 *     Abstract class to manage a serial port in a separate
 * thread. Allows top-level app to interact with multiple, 
 * different serial devices through one class interface.
 * 
 * Provides Stop() and SetParent(DelegateForm) functions.
 *
 * Subclasses must implement Read() and PageLoaded(Uri).
 * Read() is the main polling loop, if reading serial data is
 * required. PageLoaded(Uri) is called on every WebBrowser
 * load event and provides the Url that was just loaded.
 *
*************************************************************/

namespace SPH
{
    using System.IO.Ports;
    using System.Threading;
    using MsgInterface;

    /// <summary>
    /// Base class for serial port handlers
    /// </summary>
    public class SerialPortHandler
    {
        /// <summary>
        /// Thread to run handler in
        /// </summary>
        public Thread SPHThread;

        /// <summary>
        /// Handler is running
        /// </summary>
        protected bool sphRunning;

        /// <summary>
        /// A serial port
        /// </summary>
        protected SerialPort sp;

        /// <summary>
        /// Wrapped serial port
        /// </summary>
        protected IPortWrapper wsp;

        /// <summary>
        /// Parent message handler
        /// </summary>
        protected IDelegateForm parent;

        /// <summary>
        /// Port number
        /// </summary>
        protected string port;

        /// <summary>
        /// Output level setting
        /// </summary>
        protected int verbose_mode;

        /// <summary>
        /// Initializes a new instance of the <see cref="SerialPortHandler"/> class.
        /// Required for RBA_Stub.
        /// </summary>
        public SerialPortHandler()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SerialPortHandler"/> class.
        /// </summary>
        /// <param name="p">port identifier</param>
        public SerialPortHandler(string p)
        { 
            this.SPHThread = new Thread(new ThreadStart(this.Read));    
            this.sphRunning = true;
            this.port = p;
            this.verbose_mode = 0;
        }

        /// <summary>
        /// Setter for port wrapper
        /// </summary>
        /// <param name="p">new wrapper</param>
        public void SetWrapper(IPortWrapper p)
        {
            this.wsp = p;
        }

        /// <summary>
        /// Get device status
        /// </summary>
        /// <returns>status string</returns>
        public string Status()
        {
            return this.GetType().Name + ": " + this.port;
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
        /// Setter for output level
        /// </summary>
        /// <param name="v">new output level</param>
        public void SetVerbose(int v)
        {
            this.verbose_mode = v;
        }

        /// <summary>
        /// Main method running in the device handler thread
        /// </summary>
        public virtual void Read()
        {
        }

        /// <summary>
        /// Handler for incoming messages
        /// </summary>
        /// <param name="msg">the message</param>
        public virtual void HandleMsg(string msg)
        {
        }

        /// <summary>
        /// Stop running device thread
        /// </summary>
        public void Stop()
        {
            this.sphRunning = false;
            this.SPHThread.Join();
            System.Console.WriteLine("SPH Stopped");
        }

        protected void LogOrNot(string msg, int level = 1)
        {
            if (this.verbose_mode >= level)
            {
                System.Console.WriteLine(msg);
            }
        }
    }
}
