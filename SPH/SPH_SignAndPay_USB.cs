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

using System;
using System.IO;
using System.Threading;

using USBLayer;

namespace SPH {

    /// <summary>
    /// This is the recommended handler for a Sign&Pay device
    /// It utilizes HIDSharp, which has been more reliable than
    /// homegrown USB layers, and uses the preferred non-automatic
    /// state change when the screen is redrawn only whne prompted
    /// by the POS.
    /// 
    /// The device has a very minimal OS/firmware. Everything that
    /// appears on screen involves sending a command telling it
    /// to draw something.
    /// </summary>
    public class SPH_SignAndPay_USB : SerialPortHandler {

        /// <summary>
        /// Output directory for signature bitmaps
        /// </summary>
        protected static String MAGELLAN_OUTPUT_DIR = "ss-output/";

        /// <summary>
        /// Interface to access USB devices
        /// </summary>
        protected IUSBWrapper usb_port;

        /// <summary>
        /// Indicates we're currently receiving a message
        /// that's split across multiple USB packets
        /// Packets are also called "reports"
        /// </summary>
        protected bool read_continues;

        /// <summary>
        /// Buffer to collect mutli-packet messages
        /// </summary>
        protected byte[] long_buffer;

        /// <summary>
        /// The number of bytes currently stores in long_buffer
        /// </summary>
        protected int long_length;

        /// <summary>
        /// Current position within long_buffer
        /// </summary>
        protected int long_pos;

        /// <summary>
        /// Stream to read and write from the device via USB
        /// </summary>
        protected Stream usb_fs;

        /// <summary>
        /// USB report size in bytes. 
        /// This is 65 on windows and 64 on Linux.
        /// </summary>
        protected int usb_report_size;

        /// <summary>
        /// If a logo is present in this directory it will
        /// be shown on-screen
        /// </summary>
        protected bool logo_available = false;

        /// <summary>
        /// Screen resolution X
        /// </summary>
        protected const int LCD_X_RES = 320;

        /// <summary>
        /// Screen resolution Y
        /// </summary>
        protected const int LCD_Y_RES = 240;

        /// <summary>
        /// Pseudo state used when the device is between
        /// known good states
        /// </summary>
        protected const int STATE_CHANGING_STATE = -1;

        /// <summary>
        /// Initial state for transaction, the swipe
        /// card screen
        /// </summary>
        protected const int STATE_START_TRANSACTION = 1;

        /// <summary>
        /// 2nd state of the transaction, card type
        /// selection
        /// </summary>
        protected const int STATE_SELECT_CARD_TYPE = 2;

        /// <summary>
        /// Optionally state of the transaction,
        /// PIN entry
        /// </summary>
        protected const int STATE_ENTER_PIN = 3;

        /// <summary>
        /// Final state before authorization showing
        /// wait for total
        /// </summary>
        protected const int STATE_WAIT_FOR_CASHIER = 4;

        /// <summary>
        /// Optional state to choose EBT card subtype
        /// </summary>
        protected const int STATE_SELECT_EBT_TYPE = 5;

        /// <summary>
        /// Optional state to choose a cash back amount
        /// </summary>
        protected const int STATE_SELECT_CASHBACK = 6;

        /// <summary>
        /// State getting signature. This state is not
        /// part of the normal flow and will always be
        /// triggered by a request from POS
        /// </summary>
        protected const int STATE_GET_SIGNATURE = 7;

        /// <summary>
        /// Put the device in manual PAN entry mode.
        /// Not really used much.
        /// </summary>
        protected const int STATE_MANUAL_PAN = 11;

        /// <summary>
        /// Manual expiration entry state follows manual
        /// PAN state
        /// </summary>
        protected const int STATE_MANUAL_EXP = 12;

        /// <summary>
        /// The CVV entry state follows the manual
        /// expiration state
        /// </summary>
        protected const int STATE_MANUAL_CVV = 13;

        /// <summary>
        /// Button ID for card type credit
        /// </summary>
        protected const int BUTTON_CREDIT = 5;

        /// <summary>
        /// Button ID for card type debit
        /// </summary>
        protected const int BUTTON_DEBIT = 6;

        /// <summary>
        /// Button ID for card type general EBT
        /// </summary>
        protected const int BUTTON_EBT = 7;

        /// <summary>
        /// Button ID for card type gift card
        /// </summary>
        protected const int BUTTON_GIFT = 8;

        /// <summary>
        /// Button ID for card type EBT Food
        /// </summary>
        protected const int BUTTON_EBT_FOOD = 9;

        /// <summary>
        /// Button ID for card type EBT Cash
        /// </summary>
        protected const int BUTTON_EBT_CASH = 10;

        /// <summary>
        /// Cashback zero option
        /// </summary>
        protected const int BUTTON_CB_000  = 0;

        /// <summary>
        /// Cashback option 1 of 5. 
        /// Amounts may be adjusted as needed.
        /// </summary>
        protected const int BUTTON_CB_OPT1  = 5;

        /// <summary>
        /// Cashback option 2 of 5. 
        /// </summary>
        protected const int BUTTON_CB_OPT2 = 10;

        /// <summary>
        /// Cashback option 3 of 5. 
        /// </summary>
        protected const int BUTTON_CB_OPT3 = 20;

        /// <summary>
        /// Cashback option 4 of 5. 
        /// </summary>
        protected const int BUTTON_CB_OPT4 = 30;

        /// <summary>
        /// Cashback option 5 of 5. 
        /// </summary>
        protected const int BUTTON_CB_OPT5 = 40;

        /// <summary>
        /// Button ID to accept signature
        /// </summary>
        protected const int BUTTON_SIG_ACCEPT = 1;
        
        /// <summary>
        /// Button ID to clear signature
        /// </summary>
        protected const int BUTTON_SIG_RESET = 2;

        /// <summary>
        /// Button ID for the hardware red X button
        /// </summary>
        protected const int BUTTON_HARDWARE_BUTTON = 0xff;

        /// <summary>
        /// Pause in milliseconds before retrying some operations
        /// </summary>
        protected const int DEFAULT_WAIT_TIMEOUT = 1000;

        /// <summary>
        /// Font character set for text
        /// </summary>
        protected const int FONT_SET = 4;

        /// <summary>
        /// Font width probably in pixels
        /// </summary>
        protected const int FONT_WIDTH = 16;

        /// <summary>
        /// Font height probably in pixels
        /// </summary>
        protected const int FONT_HEIGHT = 18;

        /// <summary>
        /// Last message received from device. If
        /// an exception occurs while handling the message
        /// it may be retried
        /// </summary>
        protected string last_message = "";

        /// <summary>
        /// Text to display on the get signature screen
        /// </summary>
        protected string sig_message = "";

        /// <summary>
        /// Current state of the device
        /// </summary>
        protected int current_state;

        /// <summary>
        /// Certain messages from the device will use the same
        /// message identifier repeatedly but each message will
        /// have a different type of data. Counting ACKs keeps
        /// track of where we are in that stream
        /// </summary>
        protected int ack_counter;

        /// <summary>
        /// Flag whether to display EBT buttons on
        /// the card selection screen. Messages from
        /// POS can alter this setting at runtime
        /// </summary>
        protected bool type_include_fs = true;

        /// <summary>
        /// Identifier used to locate the correct USB device
        /// </summary>
        protected string usb_devicefile;

        /// <summary>
        /// Lock to prevent concurrent access to the device
        /// </summary>
        protected Object usb_lock;

        /// <summary>
        /// Event that's triggered when the device
        /// send an ACK
        /// </summary>
        protected AutoResetEvent ack_event;

        /// <summary>
        /// Create the serial port handler. The port isn't
        /// really required since the device can be found
        /// by querying the USB system. By convention the value
        /// "USB" is typically used for p but really it can
        /// be anything
        /// </summary>
        /// <param name="p">Irrelevant</param>
        public SPH_SignAndPay_USB(string p) : base(p)
        { 
            read_continues = false;
            long_length = 0;
            long_pos = 0;
            ack_counter = 0;
            usb_fs = null;
            usb_lock = new Object();
            ack_event = new AutoResetEvent(false);
            
            int vid = 0xacd;
            int pid = 0x2310;
            usb_devicefile = string.Format("{0}&{1}",vid,pid);
        }

        /// <summary>
        /// Get a connection to the device. After calling this
        /// usb_fs is available for reads and writes
        /// </summary>
        protected virtual void GetHandle()
        {
            usb_fs = null;
            usb_port = new USBWrapper_HidSharp();
            Console.WriteLine("  USB Layer: HidSharp");
            usb_report_size = 65;
            while (usb_fs == null) {
                usb_fs = usb_port.GetUSBHandle(usb_devicefile,usb_report_size);
                if (usb_fs == null) {
                    if (this.verbose_mode > 0) {
                        Console.WriteLine("No device");
                    }
                    Thread.Sleep(5000);
                } else { 
                    if (this.verbose_mode > 0) {
                        Console.WriteLine("USB device found");
                    }
                }
            }
        }

        /// <summary>
        /// Main read loop to listen for messages from device
        /// </summary>
        public override void Read()
        {
            Console.WriteLine("Loading Sign and Pay module");
            Console.WriteLine("  Screen Control: POS");
            Console.WriteLine("  Paycards Communication: Messages");
            PushOutput("TERMAUTODISABLE");

            GetHandle();
            SendReport(BuildCommand(LcdSetBacklightTimeout(0)));
            SendReport(BuildCommand(EnableAudio()));
            /**
              Loading the logo is somewhat time consuming, so you may
              want to change logo_available to true and recompile once
              it's on the device. Otherwise it's loaded onto the device
              each time the driver starts up

              Logo is assumed to be 180x200. Max file size is 32KB.
            */
            if (!this.logo_available && File.Exists("logo.jpg")) {
                SendReport(BuildCommand(LcdStoreImage(1, "logo.jpg")));
                this.logo_available = true;
            }
            SetStateStart();

            ReRead();
        }

        /// <summary>
        /// Reboot the terminal. It disconnects from USB
        /// briefly during this process so the connection
        /// has to be re-established, too, within this
        /// method
        /// </summary>
        protected virtual void RebootTerminal()
        {
            try {
                SendReport(BuildCommand(ResetDevice()));
            }
            catch (Exception ex){
                if (this.verbose_mode > 0){
                    Console.WriteLine("Reboot error:");
                    Console.WriteLine(ex);
                }
            }
            try {
                usb_fs.Dispose();
            }
            catch (Exception ex){
                if (this.verbose_mode > 0){
                    Console.WriteLine("Dispose error:");
                    Console.WriteLine(ex);
                }
            }
            try {
                usb_port.CloseUSBHandle();
            }
            catch (Exception ex){
                if (this.verbose_mode > 0){
                    Console.WriteLine("Dispose error:");
                    Console.WriteLine(ex);
                }
            }
            Thread.Sleep(DEFAULT_WAIT_TIMEOUT);
            GetHandle();
            SetStateStart();
            ReRead();
        }

        /// <summary>
        /// Draw the START_TRANSATION_STATE
        /// </summary>
        protected void SetStateStart(){
            current_state = STATE_CHANGING_STATE;
            SendReport(BuildCommand(LcdStopCapture()));
            SendReport(BuildCommand(LcdClearSignature()));
            //SendReport(BuildCommand(LcdSetClipArea(0,0,1,1)));
            // 10Mar14 - undo bordered sig capture clip area
            SendReport(BuildCommand(LcdSetClipArea(5,28,310,140, false, new byte[]{0,0,0})));
            SendReport(BuildCommand(PinpadCancelGetPIN()));
            SendReport(BuildCommand(LcdFillColor(0xff,0xff,0xff)));
            SendReport(BuildCommand(LcdFillRectangle(0,0,LCD_X_RES-1,LCD_Y_RES-1)));

            SendReport(BuildCommand(LcdTextFont(FONT_SET, FONT_WIDTH, FONT_HEIGHT)));
            SendReport(BuildCommand(LcdTextColor(0,0,0)));
            SendReport(BuildCommand(LcdTextBackgroundColor(0xff,0xff,0xff)));
            SendReport(BuildCommand(LcdTextBackgroundMode(false)));
            int x = 75;
            int y = 100;
            if (this.logo_available) {
                SendReport(BuildCommand(LcdShowImage(1, 60, 0, 240, 200)));
                y = 190;
            }
            SendReport(BuildCommand(LcdDrawText("Swipe Card", x, y)));

            current_state = STATE_START_TRANSACTION;
        }
        /// <summary>
        /// Draw the START_TRANSATION_STATE again,
        /// but this time saying "Swipe Card Again". This
        /// is triggered if the device sends invalid card
        /// swipe data
        /// </summary>
        protected void SetStateReStart(){
            current_state = STATE_CHANGING_STATE;
            SendReport(BuildCommand(LcdStopCapture()));
            SendReport(BuildCommand(PinpadCancelGetPIN()));
            SendReport(BuildCommand(LcdFillColor(0xff,0xff,0xff)));
            SendReport(BuildCommand(LcdFillRectangle(0,0,LCD_X_RES-1,LCD_Y_RES-1)));

            SendReport(BuildCommand(LcdTextFont(FONT_SET, FONT_WIDTH, FONT_HEIGHT)));
            SendReport(BuildCommand(LcdTextColor(0,0,0)));
            SendReport(BuildCommand(LcdTextBackgroundColor(0xff,0xff,0xff)));
            SendReport(BuildCommand(LcdTextBackgroundMode(false)));
            SendReport(BuildCommand(LcdDrawText("Swipe Card Again",35,100)));

            current_state = STATE_START_TRANSACTION;
        }

        /// <summary>
        /// Draw the SELECT_CARD_TYPE state
        /// EBT buttons may or may not be shown
        /// depending on type_include_fs
        /// </summary>
        protected void SetStateCardType(){
            current_state = STATE_CHANGING_STATE;
            SendReport(BuildCommand(LcdFillColor(0xff,0xff,0xff)));
            SendReport(BuildCommand(LcdFillRectangle(0,0,LCD_X_RES-1,LCD_Y_RES-1)));

            SendReport(BuildCommand(LcdStopCapture()));
            SendReport(BuildCommand(LcdClearSignature()));
            SendReport(BuildCommand(LcdSetClipArea(0,0,1,1)));
            //SendReport(BuildCommand(LcdCreateButton(BUTTON_CREDIT,"Credit",5,5,145,95)));
            SendReport(BuildCommand(LcdCreateColoredButton(BUTTON_CREDIT,"Credit",5,5,145,95, new byte[]{0x0,0x0,0x0}, new byte[]{0x0,0xbb,0x0})));
            //SendReport(BuildCommand(LcdCreateButton(BUTTON_DEBIT,"Debit",224,5,314,95)));
            SendReport(BuildCommand(LcdCreateColoredButton(BUTTON_DEBIT,"Debit",174,144,314,234, new byte[]{0x0,0x0,0x0}, new byte[]{0xee,0x0,0x0})));
            if (this.type_include_fs) {
                //SendReport(BuildCommand(LcdCreateButton(BUTTON_EBT,"EBT",5,144,95,234)));
                SendReport(BuildCommand(LcdCreateColoredButton(BUTTON_EBT,"EBT",5,144,145,234, new byte[]{0x0,0x0,0x0}, new byte[]{0xbb,0xbb,0x0})));
            }
            //SendReport(BuildCommand(LcdCreateButton(BUTTON_GIFT,"Gift",224,144,314,234)));
            SendReport(BuildCommand(LcdStartCapture(4)));

            current_state = STATE_SELECT_CARD_TYPE;
        }

        /// <summary>
        /// Draw the SELECT_EBT_TYPE screen
        /// </summary>
        protected void SetStateEbtType(){
            current_state = STATE_CHANGING_STATE;
            SendReport(BuildCommand(LcdFillColor(0xff,0xff,0xff)));
            SendReport(BuildCommand(LcdFillRectangle(0,0,LCD_X_RES-1,LCD_Y_RES-1)));

            SendReport(BuildCommand(LcdStopCapture()));
            SendReport(BuildCommand(LcdClearSignature()));
            SendReport(BuildCommand(LcdSetClipArea(0,0,1,1)));
            SendReport(BuildCommand(LcdCreateButton(BUTTON_EBT_FOOD,"Food Side",5,5,115,95)));
            SendReport(BuildCommand(LcdCreateButton(BUTTON_EBT_CASH,"Cash Side",204,5,314,95)));
            SendReport(BuildCommand(LcdStartCapture(4)));

            current_state = STATE_SELECT_EBT_TYPE;
        }

        /// <summary>
        /// Draw the SELECT_CASHBACK screen
        /// </summary>
        protected void SetStateCashBack(){
            current_state = STATE_CHANGING_STATE;
            SendReport(BuildCommand(LcdFillColor(0xff,0xff,0xff)));
            SendReport(BuildCommand(LcdFillRectangle(0,0,LCD_X_RES-1,LCD_Y_RES-1)));

            SendReport(BuildCommand(LcdStopCapture()));
            SendReport(BuildCommand(LcdClearSignature()));
            SendReport(BuildCommand(LcdSetClipArea(0,0,1,1)));
            SendReport(BuildCommand(LcdTextFont(3,12,14)));
            SendReport(BuildCommand(LcdDrawText("Select Cash Back",60,5)));
            SendReport(BuildCommand(LcdCreateButton(BUTTON_CB_000,"None",5,40,95,130)));
            SendReport(BuildCommand(LcdCreateButton(BUTTON_CB_OPT1,"5.00",113,40,208,130)));
            SendReport(BuildCommand(LcdCreateButton(BUTTON_CB_OPT2,"10.00",224,40,314,130)));
            SendReport(BuildCommand(LcdCreateButton(BUTTON_CB_OPT3,"20.00",5,144,95,234)));
            SendReport(BuildCommand(LcdCreateButton(BUTTON_CB_OPT4,"30.00",113,144,208,234)));
            SendReport(BuildCommand(LcdCreateButton(BUTTON_CB_OPT5,"40.00",224,144,314,234)));
            SendReport(BuildCommand(LcdStartCapture(4)));

            current_state = STATE_SELECT_CASHBACK;
        }

        /// <summary>
        /// Draw the ENTER_PIN screen
        /// </summary>
        protected void SetStateGetPin(){
            current_state = STATE_CHANGING_STATE;
            SendReport(BuildCommand(LcdStopCapture()));
            ack_counter = 0;

            SendReport(BuildCommand(PinpadGetPIN()));
            current_state = STATE_ENTER_PIN;
        }

        /// <summary>
        /// Draw the WAIT_FOR_CASHIER screen
        /// </summary>
        protected void SetStateWaitForCashier(){
            current_state = STATE_CHANGING_STATE;
            SendReport(BuildCommand(LcdStopCapture()));
            SendReport(BuildCommand(LcdFillColor(0xff,0xff,0xff)));
            SendReport(BuildCommand(LcdFillRectangle(0,0,LCD_X_RES-1,LCD_Y_RES-1)));

            SendReport(BuildCommand(LcdTextFont(FONT_SET, FONT_WIDTH, FONT_HEIGHT)));
            SendReport(BuildCommand(LcdTextColor(0,0,0)));
            SendReport(BuildCommand(LcdTextBackgroundColor(0xff,0xff,0xff)));
            SendReport(BuildCommand(LcdTextBackgroundMode(false)));

            // if logo load NACKs the output is the same as if
            // the users presses red X button. Change state early
            // so logo load problem is not misinterpreted by 
            // previous state handler
            current_state = STATE_WAIT_FOR_CASHIER;

            int x = 25;
            int y = 100;
            if (this.logo_available) {
                SendReport(BuildCommand(LcdShowImage(1, 60, 0, 240, 200)));
                y = 190;
            }
            SendReport(BuildCommand(LcdDrawText("waiting for total", x, y)));
        }

        /// <summary>
        /// Draw approved on the screen. This isn't currently
        /// tracked as a real state
        /// </summary>
        protected void SetStateApproved(){
            current_state = STATE_CHANGING_STATE;
            SendReport(BuildCommand(LcdStopCapture()));
            SendReport(BuildCommand(LcdFillColor(0xff,0xff,0xff)));
            SendReport(BuildCommand(LcdFillRectangle(0,0,LCD_X_RES-1,LCD_Y_RES-1)));

            SendReport(BuildCommand(LcdTextFont(FONT_SET, FONT_WIDTH, FONT_HEIGHT)));
            SendReport(BuildCommand(LcdTextColor(0,0,0)));
            SendReport(BuildCommand(LcdTextBackgroundColor(0xff,0xff,0xff)));
            SendReport(BuildCommand(LcdTextBackgroundMode(false)));
            SendReport(BuildCommand(LcdDrawText("approved",90,80)));
            SendReport(BuildCommand(LcdDrawText("thank you",85,120)));
        }

        /// <summary>
        /// Draw the GET_SIGNATURE screen
        /// </summary>
        protected void SetStateGetSignature(){
            current_state = STATE_CHANGING_STATE;
            SendReport(BuildCommand(LcdStopCapture()));
            SendReport(BuildCommand(LcdClearSignature()));
            SendReport(BuildCommand(LcdFillColor(0xff,0xff,0xff)));
            SendReport(BuildCommand(LcdFillRectangle(0,0,LCD_X_RES-1,LCD_Y_RES-1)));

            SendReport(BuildCommand(LcdTextFont(3,12,14)));
            SendReport(BuildCommand(LcdTextColor(0,0,0)));

            SendReport(BuildCommand(LcdDrawText(sig_message, 1, 1)));
            SendReport(BuildCommand(LcdCreateColoredButton(BUTTON_SIG_RESET,"Clear",204,28,314,73, new byte[]{0x0,0x0,0x0}, new byte[]{0xee,0x0,0x0})));
            SendReport(BuildCommand(LcdCreateColoredButton(BUTTON_SIG_ACCEPT,"Done",5,28,115,73, new byte[]{0x0,0x0,0x0}, new byte[]{0x0,0xbb,0x0})));
            SendReport(BuildCommand(LcdTextFont(3,12,14)));
            SendReport(BuildCommand(LcdDrawText("please sign",100,83)));
            SendReport(BuildCommand(LcdSetClipArea(5,118,310,230,true,new byte[]{0,0,0})));
            //SendReport(BuildCommand(LcdCreateColoredButton(BUTTON_SIG_RESET,"Clear",5,180,115,225, new byte[]{0x0,0x0,0x0}, new byte[]{0xee,0x0,0x0})));
            //SendReport(BuildCommand(LcdCreateColoredButton(BUTTON_SIG_ACCEPT,"Done",204,180,314,225, new byte[]{0x0,0x0,0x0}, new byte[]{0x0,0xbb,0x0})));

            SendReport(BuildCommand(LcdStartCapture(5)));

            current_state = STATE_GET_SIGNATURE;
        }

        /// <summary>
        /// Remove the signature buttons from the screen
        /// after the signature is complete by drawing a
        /// "blank" colored rectangle over them.
        /// </summary>
        protected void RemoveSignatureButtons(){
            SendReport(BuildCommand(LcdFillColor(0xff,0xff,0xff)));
            SendReport(BuildCommand(LcdFillRectangle(0,27,LCD_X_RES-1,115)));
            SendReport(BuildCommand(LcdTextFont(3,12,14)));
            SendReport(BuildCommand(LcdTextColor(0,0,0)));
            SendReport(BuildCommand(LcdDrawText("approved - thank you",40,83)));
        }

        /// <summary>
        /// Draw the MANUAL_PAN screen
        /// </summary>
        protected void SetStateGetManualPan(){
            SendReport(BuildCommand(LcdStopCapture()));
            ack_counter = 0;
            SendReport(BuildCommand(ManualEntryPAN()));
            current_state = STATE_MANUAL_PAN;
        }

        /// <summary>
        /// Draw the MANUAL_PAN screen
        /// </summary>
        protected void SetStateGetManualExp(){
            ack_counter = 0;
            SendReport(BuildCommand(ManualEntryExp()));
            current_state = STATE_MANUAL_EXP;
        }

        /// <summary>
        /// Draw the MANUAL_CVV screen
        /// </summary>
        protected void SetStateGetManualCVV(){
            ack_counter = 0;
            SendReport(BuildCommand(ManualEntryCVV()));
            current_state = STATE_MANUAL_CVV;
        }

        /// <summary>
        /// Parse a USB packet. If it contains a full message
        /// that message will be handled immediately. Otherwise
        /// the bytes are buffered until the last packet of
        /// a multi-part message arrives
        /// </summary>
        /// <param name="input">the packet</param>
        protected void HandleReadData(byte[] input){
            int msg_sum = 0;
            if (usb_report_size == 64){
                byte[] temp_in = new byte[65];
                temp_in[0] = 0;
                for (int i=0; i < input.Length; i++){
                    temp_in[i+1] = input[i];
                    msg_sum += input[i];
                }
                input = temp_in;
            }

            /* Data received, as bytes
            */
            if (this.verbose_mode > 1){
                System.Console.WriteLine("");
                System.Console.WriteLine("IN BYTES:");
                for(int i=0;i<input.Length;i++){
                    if (i>0 && i %16==0) System.Console.WriteLine("");
                    System.Console.Write("{0:x} ",input[i]);
                }
                System.Console.WriteLine("");
                System.Console.WriteLine("");
            }

            int report_length = input[1] & (0x80-1);
            /*
             * Bit 7 turned on means a multi-report message
             */
            if ( (input[1] & 0x80) != 0){
                read_continues = true;
            }

            byte[] data = null;
            if (report_length > 3 && (long_pos > 0 || input[2] == 0x02)) { // protcol messages
                int data_length = input[3] + (input[4] << 8);

                int d_start = 5;
                if (input[d_start] == 0x6 && data_length > 1){ 
                    d_start++; // ACK byte
                    data_length--;
                }

                /*
                 * New multi-report message; init class members
                 */
                if (read_continues && long_length == 0){
                    long_length = data_length;
                    long_pos = 0;
                    long_buffer = new byte[data_length];
                    if (data_length > report_length){
                        data_length = report_length-3;
                        // re-skip the ACK byte
                        if (d_start == 6) data_length--;
                    }
                }
                else if (read_continues){
                    // subsequent messages start immediately after
                    // report ID & length fields
                    d_start = 2;
                }

                if (data_length > report_length) data_length = report_length;

                data = new byte[data_length];
                for (int i=0; i<data_length && i+d_start<report_length+2;i++)
                    data[i] = input[i+d_start];

                /**
                 * Append data from multi-report messages to the
                 * class member byte buffer
                 */
                if (read_continues) {
                    // last message will contain checksum bytes and
                    // End Tx byte, so don't copy entire data array
                    int d_len = ((input[1]&0x80)!=0) ? data.Length : data.Length-3;
                    for(int i=0;i<d_len;i++){
                        long_buffer[long_pos++] = data[i];
                    }
                }

                if (this.verbose_mode > 1){
                    System.Console.Write("Received: ");
                    foreach(byte b in data)
                        System.Console.Write((char)b);
                    System.Console.WriteLine("");
                }
            }
            else if (report_length > 3){ // non-protcol messages
                data = new byte[report_length];
                for(int i=0; i<report_length && i+2<input.Length; i++){
                    data[i] = input[i+2];
                }
            }

            if (data != null && data.Length == 1 && data[0] == 0x6) {
                ack_event.Set();
            }

            if ( (input[1] & 0x80) == 0){
                if (long_buffer != null){
                    if (this.verbose_mode > 0){
                        System.Console.Write("Big Msg: ");
                        foreach(byte b in long_buffer)
                            System.Console.Write((char)b);
                        System.Console.WriteLine("");
                    }

                    HandleDeviceMessage(long_buffer);
                }
                else {
                    HandleDeviceMessage(data);
                }
                read_continues = false;
                long_length = 0;
                long_pos = 0;
                long_buffer = null;
            }
        }

        /// <summary>
        /// Callback for async read from device
        /// Handles the received packet then initiates 
        /// another async read.
        /// </summary>
        /// <param name="iar">The async read result</param>
        protected void ReadCallback(IAsyncResult iar){
            byte[] input = (byte[])iar.AsyncState;
            try {
                usb_fs.EndRead(iar);
                HandleReadData(input);        
            } catch (TimeoutException){
            } catch (Exception ex){
                if (this.verbose_mode > 0) {
                    Console.WriteLine("ReadCallback()");
                    Console.WriteLine(ex);
                }
            }

            ReRead();
        }

        /// <summary>
        /// Synchronous version. Do not automatically start
        /// another read. The calling method will handle that.
        ///
        /// The wait on excpetion is important. Exceptions
        /// are generally the result of a write that occurs
        /// during a blocking read. Waiting a second lets any
        /// subsequent writes complete without more blocking.
        /// 
        /// This is legacy functionality to handle issues on 
        /// mono+linux where async reads from the device actually
        /// block. The callback is then eventually invoked within
        /// the same method and initiates another async read.
        /// Eventually the stack blows up. The distinction doesn't
        /// matter with HIDSharp handling low-level details but
        /// old implementations using raw device file I/O need
        /// this.
        /// </summary>
        protected void MonoReadCallback(IAsyncResult iar){
            /* Revision: 7May13 - use locks instead
            */
            try {
            byte[] input = (byte[])iar.AsyncState;
                HandleReadData(input);        
            }
            catch (Exception ex){
                if (this.verbose_mode > 0)
                    Console.WriteLine(ex);
                    Thread.Sleep(DEFAULT_WAIT_TIMEOUT);
            } finally {
                usb_fs.EndRead(iar);
            }
        }

        /// <summary>
        /// Handle a device message. Called after successfully
        /// parsing a device message
        /// </summary>
        /// <param name="msg">the message</param>
        protected virtual void HandleDeviceMessage(byte[] msg){
            if (this.verbose_mode > 0)
                Console.Write("DMSG: {0}: ",current_state);

            if (msg == null) msg = new byte[0];

            if (this.verbose_mode > 0){
                foreach(byte b in msg)
                    System.Console.Write("{0:x} ",b);
                System.Console.WriteLine();
            }
            switch(current_state){
            case STATE_SELECT_CARD_TYPE:
                if (msg.Length == 4 && msg[0] == 0x7a){
                    SendReport(BuildCommand(DoBeep()));
                    if (msg[1] == BUTTON_CREDIT){
                        PushOutput("TERM:Credit");
                    }
                    else if (msg[1] == BUTTON_DEBIT){
                        PushOutput("TERM:Debit");
                    }
                    else if (msg[1] == BUTTON_EBT){
                        // purposely autochanged. no message to pos
                        // until ebt sub-selection is made
                        SetStateEbtType();
                    }
                    else if (msg[1] == BUTTON_GIFT){
                        PushOutput("TERM:Gift");
                    }
                    else if (msg[1] == BUTTON_HARDWARE_BUTTON && msg[3] == 0x43){
                        SetStateStart();
                        PushOutput("TERMCLEARALL");
                    }
                }
                break;
            case STATE_SELECT_EBT_TYPE:
                if (msg.Length == 4 && msg[0] == 0x7a){
                    SendReport(BuildCommand(DoBeep()));
                    if (msg[1] == BUTTON_EBT_FOOD){
                        PushOutput("TERM:EbtFood");
                    }
                    else if (msg[1] == BUTTON_EBT_CASH){
                        PushOutput("TERM:EbtCash");
                    }
                    else if (msg[1] == BUTTON_HARDWARE_BUTTON && msg[3] == 0x43){
                        SetStateStart();
                        PushOutput("TERMCLEARALL");
                    }
                }
                break;
            case STATE_SELECT_CASHBACK:
                if (msg.Length == 4 && msg[0] == 0x7a){
                    SendReport(BuildCommand(DoBeep()));
                    if (msg[1] == BUTTON_HARDWARE_BUTTON && msg[3] == 0x43){
                        SetStateStart();
                        PushOutput("TERMCLEARALL");
                    }
                    else if(msg[1] != 0x6){
                        // 0x6 might be a serial protocol ACK
                        // timing issue means we got here too soon
                        // and should wait for next input
        
                        // Pressed green or yellow button
                        // Proceed to PIN entry but don't
                        // request 0xFF as cash back
                        if (msg[1] != BUTTON_HARDWARE_BUTTON)
                            PushOutput("TERMCB:"+msg[1]);
                    }
                }
                break;
            case STATE_ENTER_PIN:
                if (msg.Length == 3 && msg[0] == 0x15){
                    SetStateStart();
                    PushOutput("TERMCLEARALL");
                }
                else if (msg.Length == 36){
                    string pinhex = "";
                    foreach(byte b in msg)
                        pinhex += ((char)b);
                    PushOutput("PINCACHE:"+pinhex);
                }
                break;
            case STATE_GET_SIGNATURE:
                if (msg.Length == 4 && msg[0] == 0x7a){
                    //SendReport(BuildCommand(DoBeep()));
                    if (msg[1] == BUTTON_SIG_RESET){
                        SendReport(BuildCommand(LcdClearSignature()));
                    }
                    else if (msg[1] == BUTTON_SIG_ACCEPT){
                        RemoveSignatureButtons();
                        SendReport(BuildCommand(LcdGetBitmapSig()));
                    }
                }
                else if (msg.Length > 2 && msg[0] == 0x42 && msg[1] == 0x4d){
                    BitmapOutput(msg);
                    sig_message = "";
                }
                break;
            case STATE_MANUAL_PAN:
                if (msg.Length == 1 && msg[0] == 0x6){
                    ack_counter++;
                    if (this.verbose_mode > 0)
                        System.Console.WriteLine(ack_counter);
                    if (ack_counter == 1)
                        SetStateGetManualExp();
                }
                else if (msg.Length == 3 && msg[0] == 0x15){
                    SetStateStart();
                }
                break;
            case STATE_MANUAL_EXP:
                if (msg.Length == 1 && msg[0] == 0x6){
                    ack_counter++;
                    if (this.verbose_mode > 0)
                        System.Console.WriteLine(ack_counter);
                    if (ack_counter == 2)
                        SetStateGetManualCVV();
                }
                else if (msg.Length == 3 && msg[0] == 0x15){
                    SetStateStart();
                }
                break;
            case STATE_MANUAL_CVV:
                if (msg.Length > 63 && msg[0] == 0x80){
                    string block = FixupCardBlock(msg);
                    PushOutput("PANCACHE:"+block);
                    SetStateCardType();
                }

                else if (msg.Length == 3 && msg[0] == 0x15){
                    SetStateStart();
                }
                break;
            case STATE_START_TRANSACTION:
                if (msg.Length > 63 && msg[0] == 0x80 ){
                    SendReport(BuildCommand(DoBeep()));
                    string block = FixupCardBlock(msg);
                    if (block.Length == 0){
                        SetStateReStart();
                    }
                    else {
                        PushOutput("PANCACHE:"+block);
                    }
                }
                else if (msg.Length > 1){
                    if (this.verbose_mode > 0)
                        Console.WriteLine(msg.Length+" "+msg[0]+" "+msg[1]);
                }
                break;
            }
        }

        /// <summary>
        /// Convert encrypted card block to a hex string
        /// and add the serial protcol controcl characters back
        /// to the beginning and end. PHP-side software expects
        /// this format
        /// </summary>
        protected string FixupCardBlock(byte[] data){
            // no track 2 means bad read
            if (data.Length < 3 || data[3] == 0) return "";
            string hex = BitConverter.ToString(data).Replace("-","");
            hex = "02E600"+hex+"XXXX03";
            if (hex.Length < 24) return "";
            // something went wrong with the KSN/key
            if(hex.Substring(hex.Length-16,10) == "0000000000") return "";
            if (this.verbose_mode > 0)
                Console.WriteLine(hex);
            return hex;
        }

        /// <summary>
        /// Default method for looping on device input.
        /// Starts an async read and relies on the callback
        /// to call this method again
        /// </summary>
        protected void ReRead(){
            byte[] buf = new byte[usb_report_size];
            try {
                usb_fs.BeginRead(buf, 0, usb_report_size, new AsyncCallback(ReadCallback), buf);
            }
            catch(Exception ex){
                if (this.verbose_mode > 0){
                    Console.WriteLine("Read exception:");
                    Console.WriteLine(ex);
                }
            }
        }

        /// <summary>
        /// Mono doesn't support asynchronous reads correctly.
        /// BeginRead will block. Using ReRead with Mono will
        /// eventually make the stack blow up as ReRead and
        /// ReadCallback calls build up one after the other.
        ///
        /// This is a non-issue when using HIDSharp. It only
        /// occurs with raw device file I/O
        /// </summary>
        protected void MonoRead(){
            while(sphRunning){
                byte[] buf = new byte[usb_report_size];
                try {
                    usb_fs.BeginRead(buf, 0, usb_report_size, new AsyncCallback(MonoReadCallback), buf);
                }
                catch(Exception ex){
                    // 7May13 use locks
                    // wait until writes are complete
                    lock(usb_lock){}
                    if (this.verbose_mode > 0){
                        System.Console.WriteLine("Read exception:");
                        System.Console.WriteLine(ex);
                    }
                    // locking is not foolproof, unfortunately
                    // when the other thread receives a message and
                    // starts writing the usb handle, an exception
                    // will occur here. the lock ensures this thread
                    // will wait until the other has finished writing,
                    // but those writes may not be successful. if the
                    // exception in this thread happens in the middle
                    // of a multi-report write, the terminal gets stuck.
                    // So this waits until the other thread is done writing,
                    // then re-does the same state change in this thread.
                    // it's inefficient but more resilient
                    HandleMsg(last_message);
                    last_message = "";
                }
                lock(usb_lock){}
            }
        }

        /// <summary>
        /// Handle a message from POS
        /// </summary>
        /// <param name="msg">The message</param>
        public override void HandleMsg(string msg){

            // optional predicate for "termSig" message
            // predicate string is displayed on sig capture screen
            if (msg.Length > 7 && msg.Substring(0, 7) == "termSig") {
                sig_message = msg.Substring(7);
                msg = "termSig";
            }

            // 7May13 use locks
            last_message = msg;
            switch(msg){
            case "termReset":
                lock(usb_lock){
                    SetStateStart();
                }
                break;
            case "termReboot":
                lock(usb_lock){
                    RebootTerminal();
                }
                break;
            case "termManual":
                lock(usb_lock){
                    SetStateGetManualPan();
                }
                break;
            case "termApproved":
                lock(usb_lock){
                    SetStateApproved();
                }
                break;
            case "termSig":
                lock(usb_lock){
                    SetStateGetSignature();
                }
                break;
            case "termGetType":
                lock(usb_lock){
                    this.type_include_fs = false;
                    SetStateCardType();
                }
                break;
            case "termGetTypeWithFS":
                lock(usb_lock){
                    this.type_include_fs = true;
                    SetStateCardType();
                }
                break;
            case "termCashBack":
                lock(usb_lock){
                    SetStateCashBack();
                }
                break;
            case "termGetPin":
                lock(usb_lock){
                    SetStateGetPin();
                }
                break;
            case "termWait":
                lock(usb_lock){
                    SetStateWaitForCashier();
                }
                break;
            }
        }

        /// <summary>
        /// Wrap command in proper leading and ending bytes
        /// Adds STX, ETX, checksums, and total message
        /// length indicator
        /// </summary>
        /// <param name="data">command to send</param>
        /// <returns>command with bytes added</returns>
        protected byte[] BuildCommand(byte[] data){
            int size = data.Length + 6;
            if (data.Length > 0x8000) size++;

            byte[] cmd = new byte[size];

            int pos = 0;
            cmd[pos] = 0x2;
            pos++;

            if (data.Length > 0x8000){
                cmd[pos] = (byte)(data.Length & 0xff);
                pos++;
                cmd[pos] = (byte)((data.Length >> 8) & 0xff);
                pos++;
                cmd[pos] = (byte)((data.Length >> 16) & 0xff);
                pos++;
            }
            else {
                cmd[pos] = (byte)(data.Length & 0xff);
                pos++;
                cmd[pos] = (byte)((data.Length >> 8) & 0xff);
                pos++;
            }

            for (int i=0; i < data.Length; i++){
                cmd[pos+i] = data[i];
            }
            pos += data.Length;

            cmd[pos] = LrcXor(data);
            pos++;

            cmd[pos] = LrcSum(data);
            pos++;

            cmd[pos] = 0x3;

            return cmd;
        }

        /// <summary>
        /// LRC type 1: sum of data bytes
        /// </summary>
        /// <param name="data">data bytes</param>
        /// <returns>checksum byte</returns>
        protected byte LrcSum(byte[] data){
            int lrc = 0;
            foreach(byte b in data)
                lrc = (lrc + b) & 0xff;
            return (byte)lrc;
        }

        /// <summary>
        /// LRC type 2: xor of data bytes
        /// </summary>
        /// <param name="data">data bytes</param>
        /// <returns>checksum byte</returns>
        protected byte LrcXor(byte[] data){
            byte lrc = 0;
            foreach(byte b in data)
                lrc = (byte)(lrc ^ b);
            return lrc;
        }

        /// <summary>
        /// Write data to device. Messages longer than
        /// usb_report_size are split into appropriately
        /// sized packets
        /// </summary>
        /// <param name="data"></param>
        protected void SendReport(byte[] data){
            if (this.verbose_mode > 0){
                Console.WriteLine("Full Report "+data.Length);
                for(int j=0;j<data.Length;j++){
                    if (j % 16 == 0 && j > 0)
                        Console.WriteLine("");
                    Console.Write("{0:x} ",data[j]);
                }
                Console.WriteLine("");
                Console.WriteLine("");
            }

            ack_event.Reset();

            byte[] report = new byte[usb_report_size];
            int size_field = (usb_report_size == 65) ? 1 : 0;

            for(int j=0;j<usb_report_size;j++) report[j] = 0;
            int size=0;

            for (int i=0;i<data.Length;i++) {
                if (i > 0 && i % 63 == 0){
                    report[size_field] = 63 | 0x80;

                    if (this.verbose_mode > 1){
                        for(int j=0;j<usb_report_size;j++){
                            if (j % 16 == 0 && j > 0)
                                Console.WriteLine("");
                            Console.Write("{0:x} ", report[j]);
                        }
                        Console.WriteLine("");
                        Console.WriteLine("");
                    }

                    usb_fs.Write(report,0,usb_report_size);

                    for(int j=0;j<usb_report_size;j++) report[j] = 0;
                    size=0;
                }
                report[(i%63)+size_field+1] = data[i];
                size++;
            }

            report[size_field] = (byte)size;

            if (this.verbose_mode > 1){
                for(int i=0;i<usb_report_size;i++){
                    if (i % 16 == 0 && i > 0)
                        Console.WriteLine("");
                    Console.Write("{0:x} ", report[i]);
                }
                Console.WriteLine("");
                Console.WriteLine("");
            }

            usb_fs.Write(report,0,usb_report_size);
            Thread.Sleep(50);
        }

        /// <summary>
        /// Write a bitmap out to the filesystem
        /// </summary>
        /// <param name="file">the bitmap</param>
        protected void BitmapOutput(byte[] file){
            int ticks = Environment.TickCount;
            char sep = Path.DirectorySeparatorChar;
            while(File.Exists(MAGELLAN_OUTPUT_DIR+sep+"tmp"+sep+ticks+".bmp"))
                ticks++;
            File.WriteAllBytes(MAGELLAN_OUTPUT_DIR+sep+"tmp"+sep+ticks+".bmp", file);
            PushOutput("TERMBMP"+ticks+".bmp");
        }

        /// <summary>
        /// Send message to POS
        /// </summary>
        /// <param name="s">the message</param>
        protected void PushOutput(string s)
        {
            parent.MsgSend(s);
        }

        /**
         * Device Command Functions
         */

        /// <summary>
        /// Set font attributes for writing text on screen
        /// </summary>
        protected byte[] LcdTextFont(int charset, int width, int height){
            byte[] ret = new byte[9];

            // command head
            ret[0] = 0x8a;
            ret[1] = 0x46;
            ret[2] = 0x40;

            ret[3] = (byte)(0xff & height);
            ret[4] = (byte)(0xff & width);
            ret[5] = 0x0; // bold
            ret[6] = 0x0; // italic
            ret[7] = 0x0; // underlined
            ret[8] = (charset > 6) ? (byte)6 : (byte)charset;

            return ret;
        }

        /// <summary>
        /// Set text color for writing text on screen
        /// </summary>
        /// <param name="red">0-255</param>
        /// <param name="green">0-255</param>
        /// <param name="blue">0-255</param>
        /// <returns>message bytes</returns>
        protected byte[] LcdTextColor(int red, int green, int blue){
            byte[] ret = new byte[6];

            // Command head
            ret[0] = 0x8a;
            ret[1] = 0x46;
            ret[2] = 0x41;

            ret[3] = (byte)(red & 0xff);
            ret[4] = (byte)(green & 0xff);
            ret[5] = (byte)(blue & 0xff);

            return ret;
        }

        /// <summary>
        /// Set background color for writing text on screen
        /// </summary>
        /// <param name="red">0-255</param>
        /// <param name="green">0-255</param>
        /// <param name="blue">0-255</param>
        /// <returns>message bytes</returns>
        protected byte[] LcdTextBackgroundColor(int red, int green, int blue){
            byte[] ret = new byte[6];

            // Command head
            ret[0] = 0x8a;
            ret[1] = 0x46;
            ret[2] = 0x42;

            ret[3] = (byte)(red & 0xff);
            ret[4] = (byte)(green & 0xff);
            ret[5] = (byte)(blue & 0xff);

            return ret;
        }

        /// <summary>
        /// Toggle background transparency
        /// </summary>
        /// <param name="is_transparent">transparency setting</param>
        /// <returns>message bytes</returns>
        protected byte[] LcdTextBackgroundMode(bool is_transparent){
            byte[] ret = new byte[4];

            // Command head
            ret[0] = 0x8a;
            ret[1] = 0x46;
            ret[2] = 0x43;
            
            ret[3] = (is_transparent) ? (byte)1 : (byte)0;

            return ret;
        }

        /// <summary>
        /// Write text on screen
        /// </summary>
        /// <param name="text">text to display</param>
        /// <param name="x">coord where the text starts</param>
        /// <param name="y">coord where the text starts</param>
        /// <returns>message bytes</returns>
        protected byte[] LcdDrawText(string text, int x, int y){
            byte[] ret = new byte[9 + text.Length];

            // Command head
            ret[0] = 0x8a;
            ret[1] = 0x46;
            ret[2] = 0x4f;

            ret[3] = (byte)(x & 0xff);
            ret[4] = (byte)( (x >> 8) & 0xff);

            ret[5] = (byte)(y & 0xff);
            ret[6] = (byte)( (y >> 8) & 0xff);

            ret[7] = (byte)(text.Length & 0xff);
            ret[8] = (byte)( (text.Length >> 8) & 0xff);

            int pos = 9;
            foreach(byte b in System.Text.Encoding.ASCII.GetBytes(text)){
                ret[pos] = b;
                pos++;
            }

            return ret;
        }

        /**
          Implemented based on spec but not used
          Commented out to avoid compliation warnings.
          29Dec2014
        protected byte[] LcdDrawTextInRectangle(string text, int x_top_left, int y_top_left,
                int x_bottom_right, int y_bottom_right){

            byte[] ret = new byte[13 + text.Length];

            // Command head
            ret[0] = 0x8a;
            ret[1] = 0x46;
            ret[2] = 0x4e;

            ret[3] = (byte)(x_top_left & 0xff);
            ret[4] = (byte)( (x_top_left >> 8) & 0xff);

            ret[5] = (byte)(y_top_left & 0xff);
            ret[6] = (byte)( (y_top_left >> 8) & 0xff);

            ret[7] = (byte)(x_bottom_right & 0xff);
            ret[8] = (byte)( (x_bottom_right >> 8) & 0xff);

            ret[9] = (byte)(y_bottom_right & 0xff);
            ret[10] = (byte)( (y_bottom_right >> 8) & 0xff);

            ret[11] = (byte)(text.Length & 0xff);
            ret[12] = (byte)( (text.Length >> 8) & 0xff);

            int pos = 13;
            foreach(byte b in System.Text.Encoding.ASCII.GetBytes(text)){
                ret[pos] = b;
                pos++;
            }

            return ret;
        }
        */

        /// <summary>
        /// Set fill color for filled rectangles
        /// </summary>
        protected byte[] LcdFillColor(int red, int green, int blue){
            byte[] ret = new byte[6];

            // Command head
            ret[0] = 0x8a;
            ret[1] = 0x46;
            ret[2] = 0x20;

            ret[3] = (byte)(0xff & red);
            ret[4] = (byte)(0xff & green);
            ret[5] = (byte)(0xff & blue);

            return ret;
        }

        /// <summary>
        /// Draw a filled rectangle
        /// </summary>
        /// <param name="x_top_left">top left coord</param>
        /// <param name="y_top_left">top left coord</param>
        /// <param name="x_bottom_right">bottom right coord</param>
        /// <param name="y_bottom_right">bottom right coord</param>
        /// <returns>message bytes</returns>
        protected byte[] LcdFillRectangle(int x_top_left, int y_top_left,
                int x_bottom_right, int y_bottom_right){
            byte[] ret = new byte[11];

            // Command head
            ret[0] = 0x8a;
            ret[1] = 0x46;
            ret[2] = 0x22;

            ret[3] = (byte)(x_top_left & 0xff);
            ret[4] = (byte)( (x_top_left >> 8) & 0xff);

            ret[5] = (byte)(y_top_left & 0xff);
            ret[6] = (byte)( (y_top_left >> 8) & 0xff);

            ret[7] = (byte)(x_bottom_right & 0xff);
            ret[8] = (byte)( (x_bottom_right >> 8) & 0xff);

            ret[9] = (byte)(y_bottom_right & 0xff);
            ret[10] = (byte)( (y_bottom_right >> 8) & 0xff);

            return ret;
        }

        /// <summary>
        /// Set timeout for turning off backlight
        /// Interval is 5 second increments.
        /// 1 means 5 seconds, 2 means 10, etc.
        /// 0 is always on
        /// </summary>
        /// <param name="interval">backlight timer</param>
        /// <returns>message bytes</returns>
        protected byte[] LcdSetBacklightTimeout(int interval){
            byte[] ret = new byte[5];

            // Command head
            ret[0] = 0x8a;
            ret[1] = 0x53;
            ret[2] = 0x80;

            ret[3] = 0x1;
            ret[4] = (byte)(interval & 0xff);

            return ret;
        }

        /// <summary>
        /// Set LCD clip area. This is the part of the screen where
        /// touches will be registered
        /// </summary>
        /// <param name="x_top_left">top left coord</param>
        /// <param name="y_top_left">top left coord</param>
        /// <param name="x_bottom_right">bottom right coord</param>
        /// <param name="y_bottom_right">bottom right coord</param>
        /// <returns>message bytes</returns>
        protected byte[] LcdSetClipArea(int x_top_left, int y_top_left, int x_bottom_right, int y_bottom_right){
            byte[] ret = new byte[15];

            // Command head
            ret[0] = 0x7a;
            ret[1] = 0x46;
            ret[2] = 0x03;

            ret[3] = (byte)(x_top_left & 0xff);
            ret[4] = (byte)( (x_top_left >> 8) & 0xff);

            ret[5] = (byte)(y_top_left & 0xff);
            ret[6] = (byte)( (y_top_left >> 8) & 0xff);

            ret[7] = (byte)(x_bottom_right & 0xff);
            ret[8] = (byte)( (x_bottom_right >> 8) & 0xff);

            ret[9] = (byte)(y_bottom_right & 0xff);
            ret[10] = (byte)( (y_bottom_right >> 8) & 0xff);

            ret[11] = 0x0; // don't show lines around area

            // rgb for border lines
            ret[12] = 0x0;
            ret[13] = 0x0;
            ret[14] = 0x0;

            return ret;
        }

        /// <summary>
        /// Set clip area where touches will register and include a
        /// border. This is the signature box.
        /// </summary>
        /// <param name="x_top_left">top left coord</param>
        /// <param name="y_top_left">top left coord</param>
        /// <param name="x_bottom_right">bottom right coord</param>
        /// <param name="y_bottom_right">bottom right coord</param>
        /// <param name="border">boolean show border</param>
        /// <param name="rgb">border color as 3 RGB bytes</param>
        /// <returns></returns>
        protected byte[] LcdSetClipArea(int x_top_left, int y_top_left, int x_bottom_right, int y_bottom_right, bool border, byte[] rgb){
            byte[] ret = new byte[15];

            // Command head
            ret[0] = 0x7a;
            ret[1] = 0x46;
            ret[2] = 0x03;

            ret[3] = (byte)(x_top_left & 0xff);
            ret[4] = (byte)( (x_top_left >> 8) & 0xff);

            ret[5] = (byte)(y_top_left & 0xff);
            ret[6] = (byte)( (y_top_left >> 8) & 0xff);

            ret[7] = (byte)(x_bottom_right & 0xff);
            ret[8] = (byte)( (x_bottom_right >> 8) & 0xff);

            ret[9] = (byte)(y_bottom_right & 0xff);
            ret[10] = (byte)( (y_bottom_right >> 8) & 0xff);

            ret[11] = (byte)(border ? 0xf : 0x0); // don't show lines around area

            // rgb for border lines
            ret[12] = (rgb.Length >= 1) ? rgb[0] : (byte)0x0;
            ret[13] = (rgb.Length >= 2) ? rgb[1] : (byte)0x0;
            ret[14] = (rgb.Length >= 3) ? rgb[2] : (byte)0x0;

            return ret;
        }

        /// <summary>
        /// Draw a button on screen
        /// </summary>
        /// <param name="id">Button ID; returned when pressed</param>
        /// <param name="label">Text on button</param>
        /// <param name="x_top_left">Top left coord</param>
        /// <param name="y_top_left">Top left coord</param>
        /// <param name="x_bottom_right">Bottom right coord</param>
        /// <param name="y_bottom_right">Bottom right coord</param>
        /// <returns>message bytes</returns>
        protected byte[] LcdCreateButton(int id, string label, int x_top_left, int y_top_left,
                int x_bottom_right, int y_bottom_right){

            byte[] ret = new byte[16 + label.Length];

            // Command head
            ret[0] = 0x7a;
            ret[1] = 0x46;
            ret[2] = 0x04;

            ret[3] = (byte)(id & 0xff);
            ret[4] = 0x1; // button is type 1
            ret[5] = 0xf;

            ret[6] = (byte)(x_top_left & 0xff);
            ret[7] = (byte)( (x_top_left >> 8) & 0xff);

            ret[8] = (byte)(y_top_left & 0xff);
            ret[9] = (byte)( (y_top_left >> 8) & 0xff);

            ret[10] = (byte)(x_bottom_right & 0xff);
            ret[11] = (byte)( (x_bottom_right >> 8) & 0xff);

            ret[12] = (byte)(y_bottom_right & 0xff);
            ret[13] = (byte)( (y_bottom_right >> 8) & 0xff);

            ret[14] = (byte)(label.Length & 0xff);
            ret[15] = (byte)( (label.Length >> 8) & 0xff);

            int pos = 16;
            foreach(byte b in System.Text.Encoding.ASCII.GetBytes(label)){
                ret[pos] = b;
                pos++;
            }

            return ret;
        }

        /// <summary>
        /// Draw a button with specific background color
        /// </summary>
        /// <param name="id">button ID; returned when pressed</param>
        /// <param name="label">text on button</param>
        /// <param name="x_top_left">top left coord</param>
        /// <param name="y_top_left">top left coord</param>
        /// <param name="x_bottom_right">bottom right coord</param>
        /// <param name="y_bottom_right">bottom right coord</param>
        /// <param name="foreground">Forground color as 3 byte RGB</param>
        /// <param name="background">Background color as 3 byte RGB</param>
        /// <returns>message bytes</returns>
        protected byte[] LcdCreateColoredButton(int id, string label, int x_top_left, int y_top_left,
                int x_bottom_right, int y_bottom_right, byte[] foreground, byte[] background){

            byte[] ret = new byte[33 + label.Length];

            // Command head
            ret[0] = 0x7a;
            ret[1] = 0x46;
            ret[2] = 0x04;

            ret[3] = (byte)(id & 0xff);
            ret[4] = 0x11; // text is type 3
            ret[5] = 0xf;

            ret[6] = (byte)(x_top_left & 0xff);
            ret[7] = (byte)( (x_top_left >> 8) & 0xff);

            ret[8] = (byte)(y_top_left & 0xff);
            ret[9] = (byte)( (y_top_left >> 8) & 0xff);

            ret[10] = (byte)(x_bottom_right & 0xff);
            ret[11] = (byte)( (x_bottom_right >> 8) & 0xff);

            ret[12] = (byte)(y_bottom_right & 0xff);
            ret[13] = (byte)( (y_bottom_right >> 8) & 0xff);

            ret[14] = (byte)((17+label.Length) & 0xff);
            ret[15] = (byte)( ((17+label.Length) >> 8) & 0xff);

            ret[16] = FONT_WIDTH; // font width
            ret[17] = FONT_HEIGHT; // font height
            ret[18] = 1; // weight
            ret[19] = 0; // italic
            ret[20] = 0; // underline
            ret[21] = FONT_SET; // charset

            ret[22] = foreground[0];
            ret[23] = foreground[1];
            ret[24] = foreground[2];

            ret[25] = 1; // bg mode

            ret[26] = background[0];
            ret[27] = background[1];
            ret[28] = background[2];

            int y_offset = ((y_bottom_right - y_top_left) / 2) - 15;
            int x_offset = ((x_bottom_right - x_top_left) - (16*label.Length)) / 2;
            ret[29] = (byte)(x_offset & 0xff);
            ret[30] = (byte)( (x_offset >> 8) & 0xff);
            ret[31] = (byte)(y_offset & 0xff);
            ret[32] = (byte)( (y_offset >> 8) & 0xff);

            int pos = 33;
            foreach(byte b in System.Text.Encoding.ASCII.GetBytes(label)){
                ret[pos] = b;
                pos++;
            }

            return ret;
        }

        /**
          Implemented based on spec but not used
          Commented out to avoid compliation warnings.
          29Dec2014
        protected byte[] LcdCalibrateTouch(){
            return new byte[3]{ 0x7a, 0x46, 0x1 };
        }
        */

        /// <summary>
        /// Start capturing touches from the screen
        /// Mode 5 will buffer data until a full message 
        /// is available. Other modes aren't currently used.
        /// </summary>
        /// <param name="mode">the capture mode</param>
        /// <returns>message bytes</returns>
        protected byte[] LcdStartCapture(int mode){
            byte[] ret = new byte[11];

            ret[0] = 0x7a;
            ret[1] = 0x46;
            ret[2] = 0x10;

            ret[3] = (byte)(0xff & mode);

            ret[4] = 0x7a;

            ret[5] = 0x0;
            ret[6] = 0x0;
            ret[7] = 0x0;

            ret[8] = 0xff;
            ret[9] = 0xff;
            ret[10] = 0xff;

            return ret;
        }

        /// <summary>
        /// Clear signature from screen
        /// </summary>
        /// <returns>message bytes</returns>
        protected byte[] LcdClearSignature(){
            return new byte[3]{ 0x7a, 0x46, 0x19 };
        }

        /// <summary>
        /// Get signature data as bitmap
        /// </summary>
        /// <returns>message bytes</returns>
        protected byte[] LcdGetBitmapSig(){
            return new byte[3]{ 0x7a, 0x46, 0x23 };
        }

        /// <summary>
        /// Stop capturing touches from the screen
        /// </summary>
        /// <returns>message bytes</returns>
        protected byte[] LcdStopCapture(){
            return new byte[3]{ 0x7a, 0x46, 0x1f };
        }

        /// <summary>
        /// Trigger PIN entry mode
        /// </summary>
        /// <returns>message bytes</returns>
        protected byte[] PinpadGetPIN(){
            byte[] ret = new byte[112];
            int pos = 0;

            // Command head
            ret[pos++] = 0x75;
            ret[pos++] = 0x46;
            ret[pos++] = 0x07;

            ret[pos++] = 0x31; // key type DUKPT
            ret[pos++] = 0xc; // max pin length
            ret[pos++] = 0x4; // min pin length
            ret[pos++] = 0x0; // no account #
            ret[pos++] = 0x3; // clear display, show messages

            // background color
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            ret[pos++] = 0xcc;

            // font setting
            ret[pos++] = 0x16;
            ret[pos++] = 0x18;
            ret[pos++] = 0;
            ret[pos++] = 0;
            ret[pos++] = 0;
            ret[pos++] = 5;

            // color for something
            ret[pos++] = 0;
            ret[pos++] = 0;
            ret[pos++] = 0;

            ret[pos++] = 0x1; // transparent

            // text color
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;

            // xy set 1
            ret[pos++] = 0x20;
            ret[pos++] = 0;
            ret[pos++] = 0x60;
            ret[pos++] = 0;

            // xy set 2
            ret[pos++] = 0x10;
            ret[pos++] = 0x1;
            ret[pos++] = 0x90;
            ret[pos++] = 0;

            ret[pos++] = 0xf; // show 4 lines
            ret[pos++] = 2; // 2 messages follow
            ret[pos++] = 0;
            ret[pos++] = 0x1f; // message length
            ret[pos++] = 0;

            // another font
            ret[pos++] = 0x10;
            ret[pos++] = 0x10;
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0x3;

            // text rgb
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0xff;

            ret[pos++] = 0x1; // transparent

            // background rgb
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;

            // text xy
            ret[pos++] = 0x40;
            ret[pos++] = 0x0;
            ret[pos++] = 0x20;
            ret[pos++] = 0x0;

            // text length
            ret[pos++] = 0xa;
            ret[pos++] = 0;

            string msg = "Enter PIN:";
            foreach(byte b in System.Text.Encoding.ASCII.GetBytes(msg)){
                ret[pos] = b;
                pos++;
            }

            // next message length
            ret[pos++] = 0x2e;
            ret[pos++] = 0x0;

            // another font
            ret[pos++] = 0xc;
            ret[pos++] = 0xc;
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0x3;

            // another rgb
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0xff;

            ret[pos++] = 1; // transparent

            // background rgb
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;

            // xy
            ret[pos++] = 0x6;
            ret[pos++] = 0x0;
            ret[pos++] = 0xb4;
            ret[pos++] = 0x0;

            // text length
            ret[pos++] = 0x19;
            ret[pos++] = 0x0;
            
            msg = "Press Enter Key When Done";

            foreach(byte b in System.Text.Encoding.ASCII.GetBytes(msg)){
                ret[pos] = b;
                pos++;
            }

            return ret;
        }

        /// <summary>
        /// Cancel PIN entry mode
        /// </summary>
        /// <returns>message bytes</returns>
        protected byte[] PinpadCancelGetPIN(){
            return new byte[3]{ 0x75, 0x46, 0x9 };
        }

        /// <summary>
        /// Start manual PAN entry mode
        /// </summary>
        /// <returns>message bytes</returns>
        protected byte[] ManualEntryPAN(){
            byte[] ret = new byte[68];

            int pos = 0;
            ret[pos++] = 0x75;
            ret[pos++] = 0x46;
            ret[pos++] = 0x40;

            ret[pos++] = 0x0; // card type
            ret[pos++] = 0x1; // PAN mode
            ret[pos++] = 23; // max length
            ret[pos++] = 6; // min length
            ret[pos++] = 0x3; // redraw screen

            // rgb background
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            
            // font setting
            ret[pos++] = 0x16;
            ret[pos++] = 0x18;
            ret[pos++] = 0;
            ret[pos++] = 0;
            ret[pos++] = 0;
            ret[pos++] = 5;

            // color for something
            ret[pos++] = 0;
            ret[pos++] = 0;
            ret[pos++] = 0;

            ret[pos++] = 0x1; // transparent

            // text color
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;

            // xy set 1
            ret[pos++] = 0x20;
            ret[pos++] = 0;
            ret[pos++] = 0x60;
            ret[pos++] = 0;

            // xy set 2
            ret[pos++] = 0x10;
            ret[pos++] = 0x1;
            ret[pos++] = 0x90;
            ret[pos++] = 0;

            ret[pos++] = 0xf; // show 4 lines
            ret[pos++] = 1; // 1 messages follow
            ret[pos++] = 0; 
            ret[pos++] = 33;
            ret[pos++] = 0; 

            // another font
            ret[pos++] = 0x10;
            ret[pos++] = 0x10;
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0x3;

            // text rgb
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0xff;

            ret[pos++] = 0x1; // transparent

            // background rgb
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;

            // text xy
            ret[pos++] = 0x40;
            ret[pos++] = 0x0;
            ret[pos++] = 0x20;
            ret[pos++] = 0x0;

            string msg = "Enter Card #";
            ret[pos++] = 12;
            ret[pos++] = 0;

            foreach(byte b in System.Text.Encoding.ASCII.GetBytes(msg)){
                ret[pos] = b;
                pos++;
            }

            return ret;
        }

        /// <summary>
        /// Start manual expiration entry mode
        /// </summary>
        /// <returns>message bytes</returns>
        protected byte[] ManualEntryExp(){
            byte[] ret = new byte[70];

            int pos = 0;
            ret[pos++] = 0x75;
            ret[pos++] = 0x46;
            ret[pos++] = 0x40;

            ret[pos++] = 0x0; // card type
            ret[pos++] = 0x2; // exp mode
            ret[pos++] = 4; // max length
            ret[pos++] = 4; // min length
            ret[pos++] = 0x3; // redraw screen

            // rgb background
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            
            // font setting
            ret[pos++] = 0x16;
            ret[pos++] = 0x18;
            ret[pos++] = 0;
            ret[pos++] = 0;
            ret[pos++] = 0;
            ret[pos++] = 5;

            // color for something
            ret[pos++] = 0;
            ret[pos++] = 0;
            ret[pos++] = 0;

            ret[pos++] = 0x1; // transparent

            // text color
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;

            // xy set 1
            ret[pos++] = 0x20;
            ret[pos++] = 0;
            ret[pos++] = 0x60;
            ret[pos++] = 0;

            // xy set 2
            ret[pos++] = 0x10;
            ret[pos++] = 0x1;
            ret[pos++] = 0x90;
            ret[pos++] = 0;

            ret[pos++] = 0xf; // show 4 lines
            ret[pos++] = 1; // 1 messages follow
            ret[pos++] = 0; 
            ret[pos++] = 35;
            ret[pos++] = 0; 

            // another font
            ret[pos++] = 0x10;
            ret[pos++] = 0x10;
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0x3;

            // text rgb
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0xff;

            ret[pos++] = 0x1; // transparent

            // background rgb
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;

            // text xy
            ret[pos++] = 0x40;
            ret[pos++] = 0x0;
            ret[pos++] = 0x20;
            ret[pos++] = 0x0;

            string msg = "Enter Exp MMYY";
            ret[pos++] = 14;
            ret[pos++] = 0;

            foreach(byte b in System.Text.Encoding.ASCII.GetBytes(msg)){
                ret[pos] = b;
                pos++;
            }

            return ret;
        }

        /// <summary>
        /// Start manual CVV entry mode
        /// </summary>
        /// <returns>message bytes</returns>
        protected byte[] ManualEntryCVV(){
            byte[] ret = new byte[75];

            int pos = 0;
            ret[pos++] = 0x75;
            ret[pos++] = 0x46;
            ret[pos++] = 0x40;

            ret[pos++] = 0x0; // card type
            ret[pos++] = 0x3; // CVV mode
            ret[pos++] = 4; // max length
            ret[pos++] = 3; // min length
            ret[pos++] = 0x3; // redraw screen

            // rgb background
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            
            // font setting
            ret[pos++] = 0x16;
            ret[pos++] = 0x18;
            ret[pos++] = 0;
            ret[pos++] = 0;
            ret[pos++] = 0;
            ret[pos++] = 5;

            // color for something
            ret[pos++] = 0;
            ret[pos++] = 0;
            ret[pos++] = 0;

            ret[pos++] = 0x1; // transparent

            // text color
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;

            // xy set 1
            ret[pos++] = 0x20;
            ret[pos++] = 0;
            ret[pos++] = 0x60;
            ret[pos++] = 0;

            // xy set 2
            ret[pos++] = 0x10;
            ret[pos++] = 0x1;
            ret[pos++] = 0x90;
            ret[pos++] = 0;

            ret[pos++] = 0xf; // show 4 lines
            ret[pos++] = 1; // 1 messages follow
            ret[pos++] = 0; 
            ret[pos++] = 40;
            ret[pos++] = 0; 

            // another font
            ret[pos++] = 0x10;
            ret[pos++] = 0x10;
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0x3;

            // text rgb
            ret[pos++] = 0x0;
            ret[pos++] = 0x0;
            ret[pos++] = 0xff;

            ret[pos++] = 0x1; // transparent

            // background rgb
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;
            ret[pos++] = 0xff;

            // text xy
            ret[pos++] = 0x40;
            ret[pos++] = 0x0;
            ret[pos++] = 0x20;
            ret[pos++] = 0x0;

            string msg = "Enter Security Code";
            ret[pos++] = 19;
            ret[pos++] = 0;

            foreach(byte b in System.Text.Encoding.ASCII.GetBytes(msg)){
                ret[pos] = b;
                pos++;
            }

            return ret;
        }

        /// <summary>
        /// Soft reset the device
        /// </summary>
        /// <returns>message reset</returns>
        protected byte[] ResetDevice(){
            return new byte[7]{0x78, 0x46, 0x0a, 0x49, 0x52, 0x46, 0x57};
        }

        /// <summary>
        /// Enable audio beeps
        /// </summary>
        /// <returns>message bytes</returns>
        protected byte[] EnableAudio(){
            return new byte[4]{0x7b, 0x46, 0x1, 0x1};
        }
        
        /// <summary>
        /// Play a beep
        /// </summary>
        /// <returns>message bytes</returns>
        protected byte[] DoBeep(){
            return new byte[7]{0x7b, 0x46, 0x02, 0xff, 0x0, 0xff, 0};
        }

        /// <summary>
        /// Get device serial number
        /// </summary>
        /// <returns>message bytes</returns>
        protected byte[] GetSerialNumber(){
            return new byte[3]{0x78, 0x46, 0x2};
        }
        
        /// <summary>
        /// Store an image in the device
        /// Supported formats are BMP and JPG
        /// </summary>
        /// <param name="image_id">save under this ID</param>
        /// <param name="file_name">image file name</param>
        /// <returns></returns>
        protected byte[] LcdStoreImage(int image_id, string file_name) {
            string ext;
            int type = 0;
            try {
                ext = Path.GetExtension(file_name);
            } catch (Exception ex) {
                if (this.verbose_mode > 0) {
                    System.Console.WriteLine(ex);
                }
                return GetSerialNumber(); // using as a no-op
            }
            if (ext.ToLower() != ".bmp" && ext.ToLower() != ".jpg" && ext.ToLower() != ".jpeg") {
                if (this.verbose_mode > 0) {
                    System.Console.WriteLine("Image format " + ext + " not supported");
                }
                return GetSerialNumber(); // using as a no-op
            }
            if (!File.Exists(file_name)) {
                if (this.verbose_mode > 0) {
                    System.Console.WriteLine("File not found: " + file_name);
                }
                return GetSerialNumber(); // using as a no-op
            }

            if (ext.ToLower() == ".bmp") {
                type = 0x1;
            } else {
                type = 0x2;
            }
            
            byte[] file_data = File.ReadAllBytes(file_name);
            byte[] ret = new byte[7 + file_data.Length];

            // Command head
            ret[0] = 0x8a;
            ret[1] = 0x46;
            ret[2] = 0x70;

            ret[3] = (byte)(image_id & 0xff);
            ret[4] = (byte)( (image_id >> 8) & 0xff);

            ret[5] = (byte)(type & 0xff);
            ret[6] = (byte)( (type >> 8) & 0xff);


            Array.Copy(file_data, 0, ret, 7, file_data.Length);

            return ret;
        }

        /// <summary>
        /// Display a stored image
        /// </summary>
        /// <param name="image_id">image ID used when saving the image</param>
        /// <param name="x_top_left">top left coord</param>
        /// <param name="y_top_left">top left coord</param>
        /// <param name="x_bottom_right">bottom right coord</param>
        /// <param name="y_bottom_right">bottom right coord</param>
        /// <returns>message bytes</returns>
        protected byte[] LcdShowImage(int image_id, int x_top_left, int y_top_left, int x_bottom_right, int y_bottom_right){
            byte[] ret = new byte[13];
            // Command head
            ret[0] = 0x8a;
            ret[1] = 0x46;
            ret[2] = 0x71;

            ret[3] = (byte)(image_id & 0xff);
            ret[4] = (byte)( (image_id >> 8) & 0xff);

            ret[5] = (byte)(x_top_left & 0xff);
            ret[6] = (byte)( (x_top_left >> 8) & 0xff);

            ret[7] = (byte)(y_top_left & 0xff);
            ret[8] = (byte)( (y_top_left >> 8) & 0xff);

            ret[9] = (byte)(x_bottom_right & 0xff);
            ret[10] = (byte)( (x_bottom_right >> 8) & 0xff);

            ret[11] = (byte)(y_bottom_right & 0xff);
            ret[12] = (byte)( (y_bottom_right >> 8) & 0xff);

            return ret;
        }

        /*
        protected byte[] GetAmount(){
            byte[] ret = new byte[];
            int pos = 0;

            // command head
            ret[pos++] = 0x75;
            ret[pos++] = 0x46;
            ret[pos++] = 0x23;
            
            // min length
            ret[pos++] = 1;
            // max length
            ret[pos++] = 2;

            // serious manual translation breakdown occurs here
        }
        */
    }

}
