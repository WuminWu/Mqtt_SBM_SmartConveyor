﻿/*
 * 2015/10/06
 * MQTT client for Cell Controller
 * 
 * by Wumin
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Net.Sockets;    //use this namespace for sockets
using System.Net;            //for ip addressing
using System.IO;             //for streaming io
using System.Threading;      //for running threads
using System.Reflection;
using System.Globalization;
using System.Security.Cryptography;

// Add robot and Mqtt library
using IACT_RobotLibrary;
using uPLibrary.Networking.M2Mqtt;

namespace Mqtt_SBM_SmartConveyer
{
    public partial class Form1 : Form
    {
        private bool InitControllerResult = false, InitControllerResult2 = false, InitRobotResult, InitMessagePort;
        private string pwd = "1234";
        private string hostIP = "192.168.0.2";
        private string robotIP = "192.168.0.3";
        private int robotPort = 5000;
        private static bool actionFinished = false;
        private string actionDone = "RobotActionDone\r\n";

        // variable of Mqtt
        const string ipAddress = "127.0.0.1";
        const string TOPIC_command = "Demo/Robot/1/Command";
        const string TOPIC_result = "Demo/Robot/1/Result";
        const string PAYLOAD_move12 = "mov 1,2";
        const string PAYLOAD_move21 = "mov 2,1";
        const string PAYLOAD_respOK = "OK";
        const string PAYLOAD_respNG = "NG";
        static ushort publishOK ;

        private Thread MsgThread;
        public TcpClient tcpClient = new TcpClient();
        static MqttClient client = new MqttClient(IPAddress.Parse(ipAddress));
        public static IACT_RobotLibrary.IACT_RobotLibrary robot = new IACT_RobotLibrary.IACT_RobotLibrary();

        public Form1()
        {
            InitializeComponent();
            this.Closing += new System.ComponentModel.CancelEventHandler(this.Form1_Closing);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Console.WriteLine("Form1 Loaded");
        }

        // Form is closed
        private void Form1_Closing(object sender, EventArgs e)
        {
            client.Disconnect();
            Console.WriteLine("Form1_Load MQTT Disconnect ");
        }

        // Receive Message, As a Server
        private void communicateMessageLoop()
        {
            IPAddress ip = IPAddress.Parse(hostIP);
            TcpListener tcpListener = new TcpListener(ip, 36000);
            tcpListener.Start();
            Console.WriteLine("communicateMessageLoop Server Waiting.............");

            while (true)
            {
                TcpClient tcpClient = tcpListener.AcceptTcpClient();
                Console.WriteLine("AcceptTcpClient()");
                try
                {
                    if (tcpClient.Connected)
                    {
                        Console.WriteLine("communicateMessageLoop Success !!");
                        string receiveMsg = string.Empty;
                        byte[] receiveBytes = new byte[tcpClient.ReceiveBufferSize];
                        int numberOfBytesRead = 0;
                        NetworkStream networkStream = tcpClient.GetStream();

                        if (networkStream.CanRead)
                        {
                            do
                            {
                                // Read Data
                                numberOfBytesRead = networkStream.Read(receiveBytes, 0, tcpClient.ReceiveBufferSize);
                                receiveMsg = Encoding.Default.GetString(receiveBytes, 0, numberOfBytesRead);
                                if (receiveMsg != "")
                                    Console.WriteLine("Get receiveMsg = " + receiveMsg);
                                if (receiveMsg.Equals(actionDone))
                                {
                                    Console.WriteLine("Receive actionDone and set actionFinished to TRUE");
                                    actionFinished = true ;
                                }

                                /*
                                // Write Data and return it to Client
                                String strTest = "Send Msg from Server";
                                Byte[] myBytes = Encoding.ASCII.GetBytes(strTest);
                                networkStream = tcpClient.GetStream();
                                networkStream.Write(myBytes, 0, myBytes.Length);
                                */
                            }
                            while (networkStream.DataAvailable);                  // return to AcceptTcpClient() if false
                            // while(true)                                        // keep receive
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Catch Exception = " + e);
                    tcpClient.Close();
                    Console.WriteLine("Server communicateMessageLoop Close");
                    Console.Read();
                }
            }
        }

        // Mqtt Protocol Function
        public void creatMqttClient(string ip)
        {
            client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;     // Define what function to call when a message arrives
            client.MqttMsgSubscribed += client_MqttMsgSubscribed;               // Define what function to call when a subscription is acknowledged
            client.MqttMsgPublished += client_MqttMsgPublished;                 // Define what function to call when a message is published

            string clientID = Guid.NewGuid().ToString();
            byte connection = client.Connect(clientID);

            // Wumin : if "RetainedOne" subscribe here and you will get msg twice
            ushort subscribe = client.Subscribe(new string[] { TOPIC_command, TOPIC_result }, new byte[] { 0, 0 });
            Console.WriteLine("subscribe = " + subscribe);
        }

        private static void client_MqttMsgPublished(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishedEventArgs e)
        {
            Console.Write("Message " + e.MessageId + " has been sent.\n");
        }

        // Handle subscription acknowledgements
        private static void client_MqttMsgSubscribed(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgSubscribedEventArgs e)
        {
            Console.WriteLine("Subscribed!");
        }

        // Handle incoming messages
        private static void client_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            string publishReceived = Encoding.UTF8.GetString(e.Message);
            Console.WriteLine("Message received: "+publishReceived);

            switch (publishReceived)
            {
                case PAYLOAD_move12 :
                case "Pallet to Tester":
                    action_movAtoB();
                    while (true)
                    { 
                        if (actionFinished == true)
                        {
                            publishOK = client.Publish(TOPIC_result, Encoding.UTF8.GetBytes(PAYLOAD_respOK));
                            actionFinished = false;
                            Console.WriteLine("publish OK and set actionFinished to false");
                            break;
                        }
                        //else if (actionFinished == false)
                            //Console.WriteLine("Waiting for Flag changed");
                    }               
                    break;

                case PAYLOAD_move21 :
                case "Tester to Pallet":
                    action_movBtoA();                    
                    while (true)
                    {
                        if (actionFinished==true)
                        {
                            publishOK = client.Publish(TOPIC_result, Encoding.UTF8.GetBytes(PAYLOAD_respOK));
                            actionFinished = false;
                            Console.WriteLine("publish OK and set actionFinished to false");
                            break;
                        }
                        //else if (actionFinished == false)
                            //Console.WriteLine("Waiting for Flag changed");
                    }
                    break;

                default:
                    //Console.WriteLine("publishReceived has no event");
                    break;
            }
        }

        private static void action_movAtoB()
        {
            if (robot != null && robot.IsRobotConnected() == true)
            {
                string response;
                // Select Script 0
                response = robot.SendCommand("$Start,0");
                Thread.Sleep(100);
                if (response == null)
                {
                    MessageBox.Show("No Programing File");
                    return;
                }
            }
            else
                MessageBox.Show("Robot Connect Fail");
        }

        private static void action_movBtoA()
        {
            if (robot != null && robot.IsRobotConnected() == true)
            {
                string response;
                // Select Script 1
                response = robot.SendCommand("$Start,1");
                Thread.Sleep(100);
                if (response == null)
                {
                    MessageBox.Show("No Programing File");
                    return;
                }
            }
            else
                MessageBox.Show("Robot Connect Fail");
        }

        private void button_robotConnect(object sender, EventArgs e)
        {
            // Connect with Robot Controller
            InitRobotResult = robot.InitRobotOnly(robotIP, robotPort);

            // GetMessage While Form_Main activate
            MsgThread = new Thread(new ThreadStart(communicateMessageLoop));
            MsgThread.Start();

            if (InitRobotResult == true)
            {
                MessageBox.Show("Robot Connect Successfully");
            }
            else
                MessageBox.Show("Robot Connect Fail");
        }

        private void button_stop(object sender, EventArgs e)
        {
            string response = robot.StopRunRobot();
            if (response == null)
                MessageBox.Show("Stop Robot Fail");
        }

        private void button_login(object sender, EventArgs e)
        {
            string response = robot.EpsonLoginRobot(pwd);
            if (response == null)
                MessageBox.Show("Login Fail");
            if (response.Equals("#Login,0"))
                MessageBox.Show("Login Success");
        }

        private void button_logout(object sender, EventArgs e)
        {
            string response = robot.EpsonLogoutRobot();
            if (response == null)
                MessageBox.Show("Logout Fail");
        }

        private void button_motorOn(object sender, EventArgs e)
        {
            string response = robot.ServerOn(true);
            if (response == null)
                MessageBox.Show("MotorOn Fail");
        }

        private void button_motorOff(object sender, EventArgs e)
        {
            string response = robot.ServerOn(false);
            if (response == null)
                MessageBox.Show("MotorOff Fail");
        }

        private void button_mqttConnect(object sender, EventArgs e)
        {
            creatMqttClient(ipAddress);
        }

        private void button_movAtoB(object sender, EventArgs e)
        {
            //If you are using QoS Level 1 or 2 to publish a message on a specified topic, 
            //you can also register to MqttMsgPublished event that will be raised 
            //when the message will be delivered (exactly once) to all subscribers on the topic
            ushort publishAtoB = client.Publish(TOPIC_command, Encoding.UTF8.GetBytes(PAYLOAD_move12));
        }

        private void button_movBtoA(object sender, EventArgs e)
        {
            ushort publishBtoA = client.Publish(TOPIC_result, Encoding.UTF8.GetBytes(PAYLOAD_move21));
        }

        private void button_OK(object sender, EventArgs e)
        {
            ushort publishOK = client.Publish(TOPIC_result, Encoding.UTF8.GetBytes(PAYLOAD_respOK));
        }

        private void button_NG(object sender, EventArgs e)
        {
            ushort publishNG = client.Publish(TOPIC_result, Encoding.UTF8.GetBytes(PAYLOAD_respNG));
        }
    }
}
