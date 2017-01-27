//-------------------------------------------------------------
// <copyright file="SPH_Magellan_Scale.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------
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

namespace SPH
{
    using System.IO.Ports;
    using System.Threading;

    /// <summary>
    /// Preferred handler for Magellan scales
    /// </summary>
    public class SPH_Magellan_Scale : SerialPortHandler 
    {
        /// <summary>
        /// Current state of the scale
        /// </summary>
        private WeighState scaleState;

        /// <summary>
        /// Concurrency lock
        /// </summary>
        private object writeLock = new object();

        /// <summary>
        /// Last weight received from scale
        /// </summary>
        private string lastWeight;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="SPH_Magellan_Scale"/> class.
        /// </summary>
        /// <param name="p">port file descriptor</param>
        public SPH_Magellan_Scale(string p) : base(p)
        {
            this.sp = new SerialPort();
            this.sp.PortName = this.port;
            this.sp.BaudRate = 9600;
            this.sp.DataBits = 7;
            this.sp.StopBits = StopBits.One;
            this.sp.Parity = Parity.Odd;
            this.sp.RtsEnable = true;
            this.sp.Handshake = Handshake.None;
            this.sp.ReadTimeout = 500;
            this.wsp = new WrappedSerialPort(this.sp);
            
            this.scaleState = WeighState.None;
            this.lastWeight = "0000";
        }

        /// <summary>
        /// States of the scale
        /// </summary>
        private enum WeighState
        {
            /// <summary>
            /// State not known
            /// </summary>
            None,

            /// <summary>
            /// Scale in motion
            /// </summary>
            Motion,

            /// <summary>
            /// Scale over max weight
            /// </summary>
            Over,

            /// <summary>
            /// Scale at zero weight
            /// </summary>
            Zero,

            /// <summary>
            /// Scale at non-zero weight
            /// </summary>
            NonZero,

            /// <summary>
            /// Scale below zero weight
            /// </summary>
            Under
        }

        /// <summary>
        /// Handle message from parent
        /// </summary>
        /// <param name="msg">the message</param>
        public override void HandleMsg(string msg)
        {
            if (msg == "errorBeep")
            {
                this.Beeps(3);
            }
            else if (msg == "beepTwice")
            {
                this.Beeps(2);
            }
            else if (msg == "goodBeep")
            {
                this.Beeps(1);
            }
            else if (msg == "twoPairs")
            {
                Thread.Sleep(300);
                this.Beeps(2);
                Thread.Sleep(300);
                this.Beeps(2);
            }
            else if (msg == "rePoll")
            {
                /* ignore these commands on purpose
                scaleState = WeighState.None;
                GetStatus();
                */
            }
            else if (msg == "wakeup")
            {
                this.scaleState = WeighState.None;
                this.GetStatus();
            }
            else if (msg == "reBoot")
            {
                this.scaleState = WeighState.None;
                lock (this.writeLock)
                {
                    this.wsp.Write("S10\r");
                    Thread.Sleep(5000);
                    this.wsp.Write("S14\r");
                }
            }
        }

        /// <summary>
        /// Run the main device thread
        /// </summary>
        public override void Read()
        {
            this.wsp.Open();
            string buffer = string.Empty;
            this.LogOrNot("Reading serial data");
            this.GetStatus();
            while (this.sphRunning)
            {
                try
                {
                    int b = wsp.ReadByte();
                    if (b == 13)
                    {
                        this.LogOrNot("RECV FROM SCALE: " + buffer);
                        buffer = this.ParseData(buffer);
                        if (buffer != null)
                        {
                            this.LogOrNot("PASS TO POS: " + buffer);
                            this.PushOutput(buffer);
                        }

                        buffer = string.Empty;
                    }
                    else
                    {
                        buffer += ((char)b).ToString();
                    }
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
        }

        /// <summary>
        /// Play scale beeps
        /// </summary>
        /// <param name="num">number of beeps</param>
        private void Beeps(int num)
        {
            lock (this.writeLock)
            {
                int count = 0;
                while (count < num)
                {
                    this.wsp.Write("S334\r");
                    Thread.Sleep(150);
                    count++;
                }
            }
        }

        /// <summary>
        /// Get current scale status
        /// </summary>
        private void GetStatus()
        {
            lock (this.writeLock)
            {
                wsp.Write("S14\r");
            }
        }

        /// <summary>
        /// Send output via parent
        /// </summary>
        /// <param name="s">output message</param>
        private void PushOutput(string s)
        {
            this.parent.MsgSend(s);
        }

        /// <summary>
        /// Parse scanner-scale message
        /// </summary>
        /// <param name="s">the message</param>
        /// <returns>parsed result</returns>
        private string ParseData(string s)
        {
            // scanner message
            if (s.Substring(0, 2) == "S0")
            { 
                if (s.Substring(0, 4) == "S08A" || s.Substring(0, 4) == "S08F")
                { 
                    // UPC-A or EAN-13
                    return s.Substring(4);
                }
                else if (s.Substring(0, 4) == "S08E")
                { 
                    // UPC-E
                    return this.ExpandUPCE(s.Substring(4));
                }
                else if (s.Substring(0, 4) == "S08R")
                { 
                    // GTIN / GS1
                    return "GS1~" + s.Substring(3);
                }
                else if (s.Substring(0, 5) == "S08B1")
                { 
                    // Code39
                    return s.Substring(5);
                }
                else if (s.Substring(0, 5) == "S08B2")
                { 
                    // Interleaved 2 of 5
                    return s.Substring(5);
                }
                else if (s.Substring(0, 5) == "S08B3")
                { 
                    // Code128
                    return s.Substring(5);
                }
                else
                {
                    return s; // catch all
                }
            }
            else if (s.Substring(0, 2) == "S1")
            {
                /**
                  The scale supports two primary commands:
                  S11 is "get stable weight". This tells the scale to return
                  the next stable non-zero weight.
                  S14 is "get state". This tells the scale to return its
                  current state.

                  The "scaleState" variable tracks all six known scale states.
                  The state is only changed if the status response is different
                  than the current state and this only returns a non-null string
                  when the state changes. The goal is to only pass a message back
                  to POS once per state change. The "lastWeight" is tracked in
                  case the scale jumps directly from one stable, non-zero weight
                  to another without passing through another state in between.
                */
                if (s.Substring(0, 3) == "S11")
                { 
                    // stable weight following weight request
                    this.GetStatus();
                    if (this.scaleState != WeighState.NonZero || this.lastWeight != s.Substring(3))
                    {
                        this.scaleState = WeighState.NonZero;
                        this.lastWeight = s.Substring(3);
                        return s;
                    }
                }
                else if (s.Substring(0, 4) == "S140")
                { 
                    // scale not ready
                    this.GetStatus();
                    if (this.scaleState != WeighState.None)
                    {
                        this.scaleState = WeighState.None;
                        return "S140";
                    }
                }
                else if (s.Substring(0, 4) == "S141")
                { 
                    // weight not stable
                    this.GetStatus();
                    if (this.scaleState != WeighState.Motion)
                    {
                        this.scaleState = WeighState.Motion;
                        return "S141";
                    }
                }
                else if (s.Substring(0, 4) == "S142")
                { 
                    // weight over max
                    this.GetStatus();
                    if (this.scaleState != WeighState.Over)
                    {
                        this.scaleState = WeighState.Over;
                        return "S142";
                    }
                }
                else if (s.Substring(0, 4) == "S143")
                { 
                    // stable zero weight
                    this.GetStatus();
                    if (this.scaleState != WeighState.Zero)
                    {
                        this.scaleState = WeighState.Zero;
                        return "S110000";
                    }
                }
                else if (s.Substring(0, 4) == "S144")
                { 
                    // stable non-zero weight
                    this.GetStatus();
                    if (this.scaleState != WeighState.NonZero || this.lastWeight != s.Substring(4))
                    {
                        this.scaleState = WeighState.NonZero;
                        this.lastWeight = s.Substring(4);
                        return "S11" + s.Substring(4);
                    }
                }
                else if (s.Substring(0, 4) == "S145")
                { 
                    // scale under zero weight
                    this.GetStatus();
                    if (this.scaleState != WeighState.Under)
                    {
                        this.scaleState = WeighState.Under;
                        return "S145";
                    }
                }
                else
                {
                    this.GetStatus();
                    return s; // catch all
                }
            }
            else
            { // not scanner or scale message
                return s; // catch all
            }

            return null;
        }

        /// <summary>
        /// Convert UPC-E to UPC-A
        /// </summary>
        /// <param name="upc">the UPC</param>
        /// <returns>expanded UPC</returns>
        private string ExpandUPCE(string upc)
        {
            string lead = upc.Substring(0, upc.Length - 1);
            string tail = upc.Substring(upc.Length - 1);

            if (tail == "0" || tail == "1" || tail == "2")
            {
                return lead.Substring(0, 3) + tail + "0000" + lead.Substring(3);
            }
            else if (tail == "3")
            {
                return lead.Substring(0, 4) + "00000" + lead.Substring(4);
            }
            else if (tail == "4")
            {
                return lead.Substring(0, 5) + "00000" + lead.Substring(5);
            }
            else
            {
                return lead + "0000" + tail;
            }
        }
    }
}
