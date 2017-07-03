/////////////////////////////////////////////////////////////////////
// CommService.cs - Communicator Service                           //
//                                                                 //
// Author:      Xin Du                                             //
// Source:      Jim Fawcett,                                       //
//              CSE681 - Software Modeling & Analysis, Summer 2011 //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * -------------------
 * This package defindes a Sender class and Receiver class that
 * manage all of the details to set up a WCF channel.
 * Comm class is just a wrapper of Sender and Receiver
 *
 * Public Interface:
 * -------------------
 * public Comm()
 * public static string makeEndPoint(string url, int port)
 * public void Close()
 * 
 * public Sender()
 * public void CreateSendChannel(string address)
 * public void PostMessage(Message msg)
 * public void Close()
 * 
 * public Receiver()
 * public void CreateRecvChannel(string address)
 * public Message GetMessage()
 * public Thread start(ThreadStart rcvThreadProc)
 * public void Close()
 * 
 * 
 * Required Files:
 * ---------------
 * CommService.cs, ICommunicator, CS-BlockingQueue.cs, Messages.cs
 * Serialization.cs
 *   
 * Maintenance History:
 * --------------------
 * ver 1.0 : 19 Nov 2016
 * - first release
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using SWTools;
using System.IO;

namespace TestHarness
{
    ///////////////////////////////////////////////////////////////////
    // Receiver hosts Communication service used by other Peers
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
    public class Receiver<T> : ICommunicator
    {
        static BlockingQueue<Message> rcvBlockingQ = null;
        ServiceHost service = null;
        public string name { get; set; }

        string filePath = "..\\..\\ReceivedFiles\\";
        string fileSpec = "";
        FileStream fs = null;

        public void SetServerFilePath(string path)
        {
            filePath = path;
        }
        public bool OpenFileForWrite(string name)
        {
            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            fileSpec = filePath + "\\" + name;
            try
            {
                fs = File.Open(fileSpec, FileMode.Create, FileAccess.Write);
                Console.Write("\n  {0} start to receive - Req #2", fileSpec);
                return true;
            }
            catch
            {
                Console.Write("\n  {0} failed to open", fileSpec);
                return false;
            }
        }
        public bool WriteFileBlock(byte[] block)
        {
            try
            {
                Console.Write("\n  writing block with {0} bytes- Req #6", block.Length);
                fs.Write(block, 0, block.Length);
                fs.Flush();
                return true;
            }
            catch { return false; }
        }
        public bool CloseFile()
        {
            try
            {
                fs.Close();
                Console.Write("\n  {0} finish to receive - Req #2", fileSpec);
                return true;
            }
            catch { return false; }
        }

        public Receiver()
        {
            if (rcvBlockingQ == null)
                rcvBlockingQ = new BlockingQueue<Message>();
        }

        public Thread start(ThreadStart rcvThreadProc)
        {
            Thread rcvThread = new Thread(rcvThreadProc);
            rcvThread.Start();
            return rcvThread;
        }

        public void Close()
        {
            service.Close();
        }

        //  Create ServiceHost for Communication service

        public void CreateRecvChannel(string address)
        {
            WSHttpBinding binding = new WSHttpBinding();//guranteen in order
            Uri baseAddress = new Uri(address);
            service = new ServiceHost(typeof(Receiver<T>), baseAddress);
            service.AddServiceEndpoint(typeof(ICommunicator), binding, baseAddress);
            service.Open();
            Console.Write("\n  Service is open listening on {0} - Req #10", address);
        }

        // Implement service method to receive messages from other Peers

        public void PostMessage(Message msg)
        {
            //Console.Write("\n  service enQing message: \"{0}\"", msg.body);
            rcvBlockingQ.enQ(msg);
        }

        // Implement service method to extract messages from other Peers.
        // This will often block on empty queue, so user should provide
        // read thread.

        public Message GetMessage()
        {
            Message msg = rcvBlockingQ.deQ();
            //Console.Write("\n  {0} dequeuing message from {1}", name, msg.from);
            return msg;
        }
    }
    ///////////////////////////////////////////////////////////////////
    // Sender is client of another Peer's Communication service

    public class Sender
    {
        public string name { get; set; }

        ICommunicator channel;
        string lastError = "";
        BlockingQueue<Message> sndBlockingQ = null;
        Thread sndThrd = null;
        int tryCount = 0, MaxCount = 10;
        string currEndpoint = "";

        static bool SendFile(ICommunicator service, string file)
        {
            long blockSize = 1024;//can be bigger
            try
            {
                FileStream fs = File.Open(file, FileMode.Open, FileAccess.Read);//open local
                string filename = Path.GetFileName(file);
                service.OpenFileForWrite(filename);//open remote 
                int bytesRead = 0;
                while (true)
                {
                    long remainder = (int)(fs.Length - fs.Position);
                    if (remainder == 0)
                        break;
                    long size = Math.Min(blockSize, remainder);
                    byte[] block = new byte[size];
                    bytesRead = fs.Read(block, 0, block.Length);
                    service.WriteFileBlock(block);//write remote
                }
                fs.Close();
                service.CloseFile();//close remote
                return true;
            }
            catch (Exception ex)
            {
                Console.Write("\n  can't open {0} for writing - {1}", file, ex.Message);
                return false;
            }
        }

        //----< processing for send thread >-----------------------------

        void ThreadProc()
        {
            tryCount = 0;
            while (true)
            {
                Message msg = sndBlockingQ.deQ();
                if (msg.to != currEndpoint)//check
                {
                    currEndpoint = msg.to;
                    CreateSendChannel(currEndpoint);//infact creating a proxy
                }
                while (true)
                {
                    try
                    {
                        if(msg.type== "FileReply"|| msg.type == "FileUpload")
                        {                           
                            string filename = Path.GetFileName(msg.body);
                            Console.Write("\n  SEND==============================================");
                            Console.Write("\n  sending file {0}", filename);
                            SendFile(channel, msg.body);
                            if (msg.author== "RepPulse")
                            {
                                //Console.Write("\n  deleting file {0}", msg.body);
                                System.IO.File.Delete(msg.body);
                            }
                        }
                        else
                        {
                            channel.PostMessage(msg);//put in sender's queue
                            Console.Write("\n  SEND==============================================");
                            Console.Write("\n  posted message from {0} to {1}", name, msg.to);
                            msg.showMsg();
                        }
                        tryCount = 0;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.Write("\n  connection failed");
                        Console.Write("\n  {0}", ex);
                        if (++tryCount < MaxCount)
                            Thread.Sleep(100);
                        else
                        {
                            Console.Write("\n  {0}", "can't connect\n");
                            currEndpoint = "";
                            tryCount = 0;
                            break;
                        }
                    }
                }
                if (msg.body == "quit")
                    break;
            }
        }

        //----< initialize Sender >--------------------------------------

        public Sender()
        {
            sndBlockingQ = new BlockingQueue<Message>();
            sndThrd = new Thread(ThreadProc);
            sndThrd.IsBackground = true;
            sndThrd.Start();
        }

        //----< Create proxy to another Peer's Communicator >------------

        public void CreateSendChannel(string address)
        {
            EndpointAddress baseAddress = new EndpointAddress(address);
            WSHttpBinding binding = new WSHttpBinding();
            ChannelFactory<ICommunicator> factory
              = new ChannelFactory<ICommunicator>(binding, address);
            channel = factory.CreateChannel();
            Console.Write("\n  service proxy created for {0} - Req #10", address);
        }

        //----< posts message to another Peer's queue >------------------
        /*
         *  This is a non-service method that passes message to
         *  send thread for posting to service.
         */
        public void PostMessage(Message msg)//put in cash queue
        {
            sndBlockingQ.enQ(msg);
        }

        public string GetLastError()
        {
            string temp = lastError;
            lastError = "";
            return temp;
        }

        //----< closes the send channel >--------------------------------

        public void Close()
        {
            ChannelFactory<ICommunicator> temp = (ChannelFactory<ICommunicator>)channel;
            temp.Close();
        }
    }
    ///////////////////////////////////////////////////////////////////
    // Comm class simply aggregates a Sender and a Receiver
    //
    public class Comm<T>
    {
        public string name { get; set; } = typeof(T).Name;

        public Receiver<T> rcvr { get; set; } = new Receiver<T>();

        public Sender sndr { get; set; } = new Sender();

        public Comm()
        {
            rcvr.name = name;
            sndr.name = name;
        }

        public static string makeEndPoint(string url, int port)
        {
            string endPoint = url + ":" + port.ToString() + "/ICommunicator";
            return endPoint;
        }
        //----< this thrdProc() used only for testing, below >-----------

        public void thrdProc()
        {
            while (true)
            {
                Message msg = rcvr.GetMessage();
                msg.showMsg();
                if (msg.body == "quit")
                {
                    break;
                }
            }
        }

        public void Close()
        {
            sndr.Close();
            rcvr.Close();
        }
    }
#if (TEST_COMMSERVICE)

  class Cat { }
  class TestComm
  {
    [STAThread]
    static void Main(string[] args)
    {
      Comm<Cat> comm = new Comm<Cat>();
      string endPoint = Comm<Cat>.makeEndPoint("http://localhost", 8080);
      comm.rcvr.CreateRecvChannel(endPoint);
      comm.rcvr.start(comm.thrdProc);
      comm.sndr = new Sender();
      comm.sndr.CreateSendChannel(endPoint);
      Message msg1 = new Message();
      msg1.body = "Message #1";
      comm.sndr.PostMessage(msg1);
      Message msg2 = new Message();
      msg2.body = "quit";
      comm.sndr.PostMessage(msg2);
      Console.Write("\n  Comm Service Running:");
      Console.Write("\n  Press key to quit");
      Console.ReadKey();
      Console.Write("\n\n");
    }
#endif
  }
}