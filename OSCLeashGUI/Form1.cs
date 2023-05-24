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
using OSCLeashGUI.Properties;

namespace OSCLeashGUI
{
    public partial class Form1 : Form
    {
        OscClient Client = new OscClient("127.0.0.1", 9000);
        static OscReceiver receiver;
        static Thread thread;
        static Thread threader;
        //OscServer server = new OscServer(9001);
        public float XPos;
        public float XNeg;
        public float ZPos;
        public float ZNeg;
        float stretchy;
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
            leasher.isGrabbed = true;
            int port = 9001;
            localhost = IPAddress.Parse("127.0.0.1");
            // Create the receiver
            receiver = new OscReceiver(port);
            // Create a thread to do the listening
            thread = new Thread(new ThreadStart(ListenLoop));
            threader = new Thread(new ThreadStart(sendDeets));
            // Connect the receiver
            receiver.Connect();
            // Start the listen thread
            thread.Start();
            threader.Start();
            runDeadzone.Text = Settings.Default.runDeadzone.ToString();
            walkDeadzone.Text = Settings.Default.walkDeadzone.ToString();
            strengthMult.Text = Settings.Default.strengthMult.ToString();
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
                            //Console.WriteLine(packet.ToString().Substring(packet.ToString().LastIndexOf(",") + 1));
                            if (bool.Parse(packet.ToString().Substring(packet.ToString().LastIndexOf(",") + 1)))
                            {
                                leashgrabbedLbl.Invoke(new Action(() => leashgrabbedLbl.Text = "Leash IS grabbed"));
                                leasher.isGrabbed = true;
                            }
                            else
                            {
                                leashgrabbedLbl.Invoke(new Action(() => leashgrabbedLbl.Text = "Leash not grabbed"));
                                leasher.isGrabbed = false;
                                HorizontalOutput = 0f;
                                VerticalOutput = 0f;
                            }
                        }
                        if (packet.ToString().Contains("Leash_Stretch"))
                        {
                            //Debug.WriteLine(packet.ToString().Substring(packet.ToString().LastIndexOf(",") + 1).TrimEnd('f'));
                            stretchy = float.Parse(packet.ToString().Substring(packet.ToString().LastIndexOf(",") + 1).TrimEnd('f'));
                            leasher.Stretch = stretchy;
                        }
                        if (stretchy > float.Parse(runDeadzone.Text))
                        {
                            //Debug.WriteLine(stretchy.ToString());
                            if (packet.ToString().Contains("Leash_X+"))
                            {
                                XPos = float.Parse(packet.ToString().Substring(packet.ToString().LastIndexOf(",") + 1).TrimEnd('f'));
                            }
                            else if (packet.ToString().Contains("Leash_X-"))
                            {
                                XNeg = float.Parse(packet.ToString().Substring(packet.ToString().LastIndexOf(",") + 1).TrimEnd('f'));
                            }
                            else if (packet.ToString().Contains("Leash_Z+"))
                            {
                                ZPos = float.Parse(packet.ToString().Substring(packet.ToString().LastIndexOf(",") + 1).TrimEnd('f'));
                            }
                            else if (packet.ToString().Contains("Leash_Z-"))
                            {
                                ZNeg = float.Parse(packet.ToString().Substring(packet.ToString().LastIndexOf(",") + 1).TrimEnd('f'));
                            }
                            //vertical.Invoke(new Action(() => vertical.Text = VerticalOutput.ToString()));
                            //horizontal.Invoke(new Action(() => horizontal.Text = HorizontalOutput.ToString()));
                        }
                            VerticalOutput = Clamp((ZPos - ZNeg) * stretchy * float.Parse(strengthMult.Text));
                            HorizontalOutput = Clamp((XPos - XNeg) * stretchy * float.Parse(strengthMult.Text));
                    }
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
        }

        private float Clamp(float v)
        {
            return (v < -1) ? -1 : (v > 1) ? 1 : v;
        }

        private void sendDeets()
        {
            Debug.WriteLine("started");
            try
            {
                while (true)
                {
                    if (leasher.isGrabbed)
                    {
                        if (leasher.Stretch > float.Parse(runDeadzone.Text))
                        {
                            Client.Send("input/Run", 1);
                            Client.Send("/input/Vertical", VerticalOutput);
                            Client.Send("/input/Horizontal", HorizontalOutput);
                        }
                        else if(leasher.Stretch > 0 & leasher.Stretch > float.Parse(walkDeadzone.Text) & leasher.Stretch < float.Parse(runDeadzone.Text))
                        {
                            Client.Send("input/Run", 0);
                            Client.Send("/input/Vertical", VerticalOutput);
                            Client.Send("/input/Horizontal", HorizontalOutput);
                        }
                       
                    } 
                    else
                    {
                        VerticalOutput = 0f;
                        HorizontalOutput = 0f;
                        Client.Send("input/Run", 0);
                    }
                    vertical.Invoke(new Action(() => vertical.Text = VerticalOutput.ToString()));
                    horizontal.Invoke(new Action(() => horizontal.Text = HorizontalOutput.ToString()));
                    Client.Send("/input/Vertical", VerticalOutput);
                    Client.Send("/input/Horizontal", HorizontalOutput);
                    Thread.Sleep(100);
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
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            thread.Interrupt();
            threader.Abort();
            receiver.Close();
            Close();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            about about = new about();
            about.ShowDialog();
        }

        private void walkDeadzone_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.walkDeadzone = float.Parse(walkDeadzone.Text);
            Settings.Default.Save();
        }

        private void runDeadzone_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.runDeadzone = float.Parse(runDeadzone.Text);
            Settings.Default.Save();
        }

        private void strengthMult_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.strengthMult = float.Parse(strengthMult.Text);
            Settings.Default.Save();
        }
    }
}
