using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime;
using BuildSoft.OscCore;
using BlobHandles;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Rug.Osc;
using System.Net;
using System.Text.RegularExpressions;

namespace OSCLeashGUI
{
    public partial class Form1 : Form
    {
        ThreadStart leashSender;
        Thread leashSend;
        ThreadStart leashChecker;
        Thread leashCheck;
        OscClient Client = new OscClient("127.0.0.1", 9000);
        static OscReceiver receiver;
        static OscSender OSCsender;
        static Thread thread;
        static Thread threader;
        //OscServer server = new OscServer(9001);
        bool MessageValue;
        public float XPos;
        public float XNeg;
        public float ZPos;
        public float ZNeg;
        float leashStretch;
        static float VerticalOutput;
        static float HorizontalOutput;
        IPAddress localhost;
        public Form1()
        {
            InitializeComponent();
        }

        class leasher
        {
            public static bool isGrabbed { get; set; }
            public static float Stretch { get; set; }
            public static float WalkDeadzone {get; set;}
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            int port = 9001;
            localhost = IPAddress.Parse("127.0.0.1");
            // Create the receiver
            receiver = new OscReceiver(port);
            OSCsender = new OscSender(localhost, 9000);
            // Create a thread to do the listening
            thread = new Thread(new ThreadStart(ListenLoop));
            threader = new Thread(new ThreadStart(sendDeets));
            // Connect the receiver
            receiver.Connect();

            // Start the listen thread
            thread.Start();


        }

        private void ListenLoop()
        {
            try
            {
                while (receiver.State != OscSocketState.Closed)
                {
                    // if we are in a state to recieve
                    if (receiver.State == OscSocketState.Connected)
                    {
                        // get the next message 
                        // this will block until one arrives or the socket is closed
                        OscPacket packet = receiver.Receive();
                        if (packet.ToString().Contains("Leash_IsGrabbed"))
                        {
                            Console.WriteLine(packet.ToString().Substring(packet.ToString().LastIndexOf(",") + 1));
                            if (bool.Parse(packet.ToString().Substring(packet.ToString().LastIndexOf(",") + 1)))
                            {
                                leashgrabbedLbl.Invoke(new Action(() => leashgrabbedLbl.Text = "Leash IS grabbed"));
                                if (packet.ToString().Contains("Leash_Stretch"))
                                {
                                    float stretchy = float.Parse(packet.ToString());
                                    if (stretchy > float.Parse(runDeadzone.Text))
                                    {
                                        Debug.WriteLine(packet.ToString());
                                        if (packet.ToString().Contains("Leash"))
                                        {
                                            int index = packet.ToString().IndexOf(",") + 2;
                                            string floaty = packet.ToString();
                                            string onlyFloat = "";
                                            int length = floaty.Length - 1;
                                            if (index >= 0)
                                            onlyFloat = floaty.Substring(index, length - index);
                                            if (packet.ToString().Contains("Leash_X+"))
                                            {
                                                XPos = float.Parse(onlyFloat);
                                            }
                                            else if (packet.ToString().Contains("Leash_X-"))
                                            {
                                                XNeg = float.Parse(onlyFloat);
                                            }
                                            else if (packet.ToString().Contains("Leash_Z+"))
                                            {
                                                ZPos = float.Parse(onlyFloat);
                                            }
                                            else if (packet.ToString().Contains("Leash_Z-"))
                                            {
                                                ZNeg = float.Parse(onlyFloat);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                leashgrabbedLbl.Invoke(new Action(() => leashgrabbedLbl.Text = "Leash is NOT grabbed"));
                            }
                        }
                    }

                    VerticalOutput = Clamp((ZPos - ZNeg) * leashStretch * float.Parse(strengthMult.Text));
                    HorizontalOutput = Clamp((XPos - XNeg) * leashStretch * float.Parse(strengthMult.Text));
                    vertical.Invoke(new Action(() => vertical.Text = VerticalOutput.ToString()));
                    horizontal.Invoke(new Action(() => horizontal.Text = HorizontalOutput.ToString()));
                }
            }
            catch (Exception ex)
            {
                // if the socket was connected when this happens
                // then tell the user
                if (receiver.State == OscSocketState.Connected)
                {
                    Debug.WriteLine("Exception in listen loop");
                    Debug.WriteLine(ex.Message);
                }
            }
            Thread.Sleep(200);
        }

        private float Clamp(float v)
        {
            return (v < -1) ? -1 : (v > 1) ? 1 : v;
        }

        private void sendDeets()
        {
            if (leasher.isGrabbed == true)
            {
                if (leasher.Stretch > float.Parse(runDeadzone.Text))
                {
                    Client.Send("/input/Vertical", VerticalOutput);
                    Client.Send("/input/Horizontal", HorizontalOutput);
                }
                vertical.Invoke(new Action(() => vertical.Text = VerticalOutput.ToString()));
                horizontal.Invoke(new Action(() => vertical.Text = HorizontalOutput.ToString()));
            }

        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            thread.Abort();
            threader.Abort();
            receiver.Close();
            Close();
        }
    }
}
