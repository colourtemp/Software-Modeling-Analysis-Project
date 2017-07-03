/////////////////////////////////////////////////////////////////////
// Client.cs - sends Files, TestRequests, Query Log list, Log      //
//             displays results                                    //
//                                                                 //
// Author:      Xin Du                                             //
// Source:      Jim Fawcett,                                       //
//              CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * -------------------
 * Implement a Client with communication channel 
 * talk to default repository and testharnes server
 * Send local files into repository
 * Send test request to testharness and display result 
 * Query into Repository for name of Logs
 * Queries into Repository for Logs and display 
 *
 * Public Interface:
 * -------------------
 * public Client(string url, int port)
 * public Message buildUplodMessage(string dir)
 * public Message buildTestMessage(testRequest tr)
 * public Message buildContentQueryMessage(string pattern="*.*")
 * public Message buildResultQueryMessage(string text)
 * public Message makeRepQuitMessage()
 * public Message makeThQuitMessage()
 * public Message makeSelfQuitMessage()
 * public void wait()
 *
 * Required Files:
 * -------------------
 * - Client.cs, Logger.cs, Communication.cs, CS-BlockingQueue
 *   ITest.cs, Message.cs, Serialization.cs
 * 
 * Maintenance History:
 * --------------------
 * ver 1.0 : 19 Nov 2016
 * - first release
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace TestHarness
{
  public class Client : IClient
  {
    public Comm<Client> comm { get; set; } = new Comm<Client>();//make sure blocking queue not shared
    public string endPoint; 
    public string repUrl { get; } = Comm<Client>.makeEndPoint("http://localhost", 8082);
    public string thUrl { get; } = Comm<Client>.makeEndPoint("http://localhost", 8080);
    private Thread rcvThread = null;

    //----< initialize receiver >------------------------------------

    public Client(string url, int port)
    {
      endPoint = Comm<Client>.makeEndPoint(url, port);
      Console.Write("\n  Creating instance of Client");
      comm.rcvr.CreateRecvChannel(endPoint);
      //Console.Title = "Client: " + endPoint;
      rcvThread = comm.rcvr.start(rcvThreadProc);
    }
    //----< join receive thread >------------------------------------

    public void wait()
    {
      rcvThread.Join();
    }
    //----< construct a basic message >------------------------------

    public Message makeMessage(string author, string fromEndPoint, string toEndPoint)
    {
      Message msg = new Message();
      msg.author = author;
      msg.from = fromEndPoint;
      msg.to = toEndPoint;
      return msg;
    }
    //----< use private service method to receive a message >--------
     
    void rcvThreadProc()
    {
      while (true)
      {
        Message msg = comm.rcvr.GetMessage();
        //Console.Write("\n  RECEIVE---------------------");
        //Console.Write("\n  {0} received message:", comm.name);
        //msg.showMsg();
                switch (msg.type)
                {
                    case "TestResults":
                        {
                            Console.Write("\n  CLIENT RECEIVE TestResults---------------------- - Req #7");
                            Console.Write("\n  {0} received message:", comm.name);
                            msg.showMsg();
                            break;
                        }
                    case "ResultReply":
                        {
                            Console.Write("\n  CLIENT RECEIVE ResultReply----------------------- - Req #9");
                            Console.Write("\n  {0} received message:", comm.name);
                            msg.showMsg();
                            break;
                        }
                    default:
                        {
                            Console.Write("\n  CLIENT RECEIVE-----------------------------------");
                            Console.Write("\n  {0} received message:", comm.name);
                            msg.showMsg();
                            break;
                        }
                }
                if (msg.body == "quit")
          break;
      }
    }

    /// <summary>
    /// build a demo test request for demo 
    /// </summary>
    public testRequest buildDemoRequest()
    {
        testRequest tr = new testRequest();
        tr.author = "XIN";
        testElement te1 = new testElement("test1");
        te1.addDriver("testdriver.dll");
        te1.addCode("testedcode.dll");
        testElement te2 = new testElement("test2");
        te2.addDriver("testdriver.dll");
        te2.addCode("testedcode.dll");
        testElement te3 = new testElement("test3");
        te3.addDriver("anothertestdriver.dll");
        te3.addCode("anothertestedcode.dll");
        tr.tests.Add(te1);
        tr.tests.Add(te2);
        tr.tests.Add(te3);
        return tr;
    }

    /// <summary>
    /// build a demo test request for demo 
    /// </summary>
    public testRequest buildDemoRequest2()
    {
        testRequest tr = new testRequest();
        tr.author = "XIN";
        testElement te1 = new testElement("ghostTest");
        te1.addDriver("td1.dll");
        te1.addCode("tc1.dll");
        tr.tests.Add(te1);
        return tr;
    }

        /// <summary>
        /// build a TestRequest message with a given request
        /// </summary>
        public Message buildTestMessage(string name,testRequest tr)
    {
      Message msg = new Message();
      msg.to = thUrl;
      msg.from = endPoint;
      msg.author = name;
      msg.type = "TestRequest";
      msg.body = tr.ToString();
      return msg;
    }

    /// <summary>
    /// build a FileUpload message with a fully qualified file dir
    /// </summary>
    public Message buildUplodMessage(string dir)
    {
      Message msg = new Message();
      msg.to = repUrl;
      msg.from = endPoint;
      msg.author = "Xin";
      msg.type = "FileUpload";
      msg.body = dir;
      return msg;
    }    

    /// <summary>
    /// build a ContentQuery message(get list of DLL\log file name) with a given pattern
    /// </summary>
    public Message buildContentQueryMessage(string pattern="*.*")
    {
      Message msg = new Message();
      msg.to = repUrl;
      msg.from = endPoint;
      msg.author = "Xin";
      msg.type = "ContentQuery";
      msg.body = pattern;
      return msg;
    }  

    /// <summary>
    /// build a ResultsQuery message with a key word
    /// </summary>
    public Message buildResultQueryMessage(string text)
    {
      Message msg = new Message();
      msg.to = repUrl;
      msg.from = endPoint;
      msg.author = "Xin";
      msg.type = "ResultsQuery";
      msg.body = text;
      return msg;
    }  

    /// <summary>
    /// build a quit message for rep
    /// </summary>
    public Message makeRepQuitMessage()
    {
        Message quitMessage = new Message();
        quitMessage.to = repUrl;
        quitMessage.from = endPoint;
        quitMessage.author = "Client";
        quitMessage.type = "quit";
        quitMessage.body = "quit";
        return quitMessage;
    }

    /// <summary>
    /// build a quit message for test harness
    /// </summary>
    public Message makeThQuitMessage()
    {
        Message quitMessage = new Message();
        quitMessage.to = thUrl;
        quitMessage.from = endPoint;
        quitMessage.author = "Client";
        quitMessage.type = "quit";
        quitMessage.body = "quit";
        return quitMessage;
    }

    /// <summary>
    /// build a quit message for test harness
    /// </summary>
    public Message makeSelfQuitMessage()
    {
        Message quitMessage = new Message();
        quitMessage.to = endPoint;
        quitMessage.from = endPoint;
        quitMessage.author = "Client";
        quitMessage.type = "quit";
        quitMessage.body = "quit";
        return quitMessage;
    }

    //from here below for local test
    private ITestHarness th_ = null;
    private IRepository repo_ = null;
    public Client(ITestHarness th)
    {
      Console.Write("\n  Creating instance of Client");
      th_ = th;
    }
    public void setRepository(IRepository repo)
    {
      repo_ = repo;
    }
    /// <summary>
    /// call local test harness to send test request
    /// </summary>
    public void sendTestRequest(Message testRequest)
    {
      th_.sendTestRequest(testRequest);
    }
    /// <summary>
    /// print result received as argument
    /// </summary>
    public void sendResults(Message results)
    {
      Console.Write("\n  Client received results message:");
      Console.Write("\n  " + results.ToString());
      Console.WriteLine();
    }
    /// <summary>
    /// send query to loacl rep, print results
    /// </summary>
    public void makeQuery(string queryText)
    {
      Console.Write("\n  Results of client query for \"" + queryText + "\"");
      if (repo_ == null)
        return;
      List<string> files = repo_.queryLogs(queryText);
      Console.Write("\n  first 10 reponses to query \"" + queryText + "\"");
      for (int i = 0; i < 10; ++i)
      {
        if (i == files.Count())
          break;
        Console.Write("\n  " + files[i]);
      }
    }

#if (TEST_CLIENT)
    static void Main(string[] args)
    {
        Client client = new Client("http://localhost", 8081);
        //demo file upload
        string repoStoragePath = "..\\..\\..\\Client\\toSend\\";
        string file1 = repoStoragePath+ "TestDriver.dll";
        file1 = System.IO.Path.GetFullPath(file1);
        string file2 = repoStoragePath + "TestedCode.dll";
        file2 = System.IO.Path.GetFullPath(file2);
        string file3 = repoStoragePath + "AnotherTestDriver.dll";
        file3 = System.IO.Path.GetFullPath(file3);
        string file4 = repoStoragePath + "AnotherTestedCode.dll";
        file4 = System.IO.Path.GetFullPath(file4);

        Message send1 = client.buildUplodMessage(file1);
        Message send2 = client.buildUplodMessage(file2);
        Message send3 = client.buildUplodMessage(file3);
        Message send4 = client.buildUplodMessage(file4);

        client.comm.sndr.PostMessage(send1);
        client.comm.sndr.PostMessage(send2);
        client.comm.sndr.PostMessage(send3);
        client.comm.sndr.PostMessage(send4);

        //demo test request
        Message msg = client.buildTestMessage("Xin", client.buildDemoRequest());
        client.comm.sndr.PostMessage(msg);
        client.comm.sndr.PostMessage(msg);

        //demo content query

        Message CTmsg = client.buildContentQueryMessage("*.txt");
        client.comm.sndr.PostMessage(CTmsg);

        Thread.Sleep(10000);

        //demo result query
        Message Qmsg = client.buildResultQueryMessage("Xin");
        client.comm.sndr.PostMessage(Qmsg);

        //wait to stop
        Console.Write("\n  press key to exit: ");
        Console.ReadKey();
        //Message quit0 = client.makeRepQuitMessage();
        //client.comm.sndr.PostMessage(quit0);

        //Message quit1 = client.makeThQuitMessage();
        //client.comm.sndr.PostMessage(quit1);

        Message quit2 = client.makeSelfQuitMessage();
        client.comm.sndr.PostMessage(quit2);

        client.wait();
    }
#endif
  }
}
