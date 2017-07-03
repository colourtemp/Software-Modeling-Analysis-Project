/////////////////////////////////////////////////////////////////////
// TestHarness.cs - TestHarness Engine: creates child domains      //
// Author:      Xin Du                                             //
// Source:      Jim Fawcett,                                       //
//              CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * -------------------
 * TestHarness package provides integration testing services.  It:
 * - receives structured test requests
 * - retrieves cited files from a repository
 * - executes tests on all code that implements an ITest interface,
 *   e.g., test drivers.
 * - reports pass or fail status for each test in a test request
 * - stores test logs in the repository
 * It contains classes:
 * - TestHarness that runs all tests in child AppDomains
 * - Callback to support sending messages from a child AppDomain to
 *   the TestHarness primary AppDomain.
 * - Test and RequestInfo to support transferring test information
 *   from TestHarness to child AppDomain
 * 
 * Required Files:
 * ---------------
 * - TestHarness.cs, CS-BlockingQueue.cs
 * - ITest.cs, Communication.cs, Serialization
 * - LoadAndTest.cs, Logger.cs, Messages.cs
 *
 * Maintanence History:
 * --------------------
 * ver 3.0 : 20 Nov 2016
 * - added communiatino channel and the logic to process received message
 * ver 2.0 : 13 Nov 2016
 * - added creation of threads to run tests in ProcessMessages
 * - removed logger statements as they were confusing with multiple threads
 * - added locking in a few places
 * - added more error handling
 * - No longer save temp directory name in member data of TestHarness class.
 *   It's now captured in TestResults data structure.
 * ver 1.1 : 11 Nov 2016
 * - added ability for test harness to pass a load path to
 *   LoadAndTest instance in child AppDomain
 * ver 1.0 : 16 Oct 2016
 * - first release
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Security.Policy;    // defines evidence needed for AppDomain construction
using System.Runtime.Remoting;   // provides remote communication between AppDomains
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using Utilities;

namespace TestHarness
{
  ///////////////////////////////////////////////////////////////////
  // Callback class is used to receive messages from child AppDomain
  //
  public class Callback : MarshalByRefObject, ICallback
  {
    public void sendMessage(Message message)
    {
      Console.Write("\n  Test result from childDomain: \"" + message.body + "\"");
    }
  }
  ///////////////////////////////////////////////////////////////////
  // Test and RequestInfo are used to pass test request information
  // to child AppDomain
  //
  [Serializable]
  class Test : ITestInfo
  {
    public string testName { get; set; }
    public List<string> files { get; set; } = new List<string>();
  }
  [Serializable]
  public class RequestInfo : IRequestInfo
  {
    public string tempDirName { get; set; }//author+daytime+threadID
    public List<ITestInfo> requestInfo { get; set; } = new List<ITestInfo>();
  }
  ///////////////////////////////////////////////////////////////////
  // class TestHarness

  public class TestHarness : ITestHarness
  {
    public SWTools.BlockingQueue<Message> inQ_ { get; set; } = new SWTools.BlockingQueue<Message>();
    public Comm<TestHarness> comm { get; set; } = new Comm<TestHarness>();
    public string endPoint { get; } = Comm<TestHarness>.makeEndPoint("http://localhost", 8080);
    public string repUrl { get; } = Comm<TestHarness>.makeEndPoint("http://localhost", 8082);
    private Thread rcvThread = null;
    private string receivePath_ = "../../ReceivedFiles";// receive dir to hold files received
    private string filePath_;// full name of temporary dir to hold test files
    object sync_ = new object();
    List<Thread> threads_ = new List<Thread>();
    HRTimer.HiResTimer hrt = null;

    private ICallback cb_;
    private IRepository repo_;
    private IClient client_;

    public TestHarness()
    {
      Console.Title = "TestHarness: " + endPoint;
      Console.Write("\n  creating instance of TestHarness");
      receivePath_ = System.IO.Path.GetFullPath(receivePath_);
      cb_ = new Callback();
      hrt = new HRTimer.HiResTimer();
      comm.rcvr.CreateRecvChannel(endPoint);
      rcvThread = comm.rcvr.start(rcvThreadProc);
    }

    //----< called by TestExecutive >--------------------------------

    public void setClient(IClient client)
    {
      client_ = client;
    }
    //----< called by clients >--------------------------------------
    /// <summary>
    /// enque message to local queue
    /// </summary>
    public void sendTestRequest(Message testRequest)
    {
      Console.Write("\n  TestHarness received a testRequest - Req #2");
      inQ_.enQ(testRequest);
    }

    public TestHarness(IRepository repo)
    {
      Console.Write("\n  creating instance of TestHarness");
      repo_ = repo;
      receivePath_ = System.IO.Path.GetFullPath(receivePath_);
      cb_ = new Callback();

      comm.rcvr.CreateRecvChannel(endPoint);
      rcvThread = comm.rcvr.start(rcvThreadProc);
    }

    /// <summary>
    /// Method for server's receive thread to run to process messages
    /// </summary>
    void rcvThreadProc()
    {
      while (true)
      {
        Message msg = comm.rcvr.GetMessage();
        msg.time = DateTime.Now;
        switch(msg.type)
        {
            case "TestRequest":
                {
                    Console.Write("\n  TEST HARNESS RECEIVE TestRequest------------------- - Req #2");
                    Console.Write("\n  {0} received message:", comm.name);
                    msg.showMsg();
                    inQ_.enQ(msg);
                    break;
                }
            default:
                {
                    Console.Write("\n  TEST HARNESS RECEIVE--------------------------------");
                    Console.Write("\n  {0} received message:", comm.name);
                    msg.showMsg();
                    break;
                }
 
        }
        if (msg.body == "quit")
          break;
      }
    }


    //----< not used for Project #2 >--------------------------------
    /// <summary>
    /// put the message into sender's queue
    /// </summary>
    public Message sendMessage(Message msg)
    {
        //Console.Write("\n  {0} send message:", comm.name);
        //msg.showMsg();
        comm.sndr.PostMessage(msg);
      return msg;
    }
    //----< make path name from author and time >--------------------

    string makeKey(string author)
    {
      DateTime now = DateTime.Now;
      string nowDateStr = now.Date.ToString("d");
      string[] dateParts = nowDateStr.Split('/');
      string key = "";
      foreach (string part in dateParts)
        key += part.Trim() + '_';
      string nowTimeStr = now.TimeOfDay.ToString();
      string[] timeParts = nowTimeStr.Split(':');
      for (int i = 0; i < timeParts.Count() - 1; ++i)
        key += timeParts[i].Trim() + '_';
      key += timeParts[timeParts.Count() - 1];
      key = author + "_" + key + "_" + "ThreadID" + Thread.CurrentThread.ManagedThreadId;
      return key;
    }
    //----< retrieve test information from testRequest >-------------

    List<ITestInfo> extractTests(Message testRequest)
    {
      Console.Write("\n  parsing test request");
      List<ITestInfo> tests = new List<ITestInfo>();
      XDocument doc = XDocument.Parse(testRequest.body);
      foreach (XElement testElem in doc.Descendants("test"))
      {
        Test test = new Test();
        string testDriverName = testElem.Element("testDriver").Value;
        test.testName = testElem.Attribute("name").Value;
        test.files.Add(testDriverName);
        foreach (XElement lib in testElem.Elements("library"))
        {
          test.files.Add(lib.Value);
        }
        tests.Add(test);
      }
      return tests;
    }
    //----< retrieve test code from testRequest >--------------------

    List<string> extractCode(List<ITestInfo> testInfos)
    {
      //Console.Write("\n  retrieving code files from testInfo data structure");
      List<string> codes = new List<string>();
      foreach (ITestInfo testInfo in testInfos)
        codes.AddRange(testInfo.files);
      return codes;
    }

    //----< make TestResults Message >-------------------------------
    /// <summary>
    /// generate FilesRequest Message from RequestInfo
    /// </summary>
    Message makeFilesRequestMessage(RequestInfo rqi)
    {
      Message trMsg = new Message();
      trMsg.author = "TestHarness";
      trMsg.to = repUrl;
      trMsg.from = endPoint;
      trMsg.type = "FilesRequest";
        List<string> files = extractCode(rqi.requestInfo);
        files.Add(rqi.tempDirName);
        trMsg.body = files.ToXml();
      return trMsg;
    }

    //----< create local directory and load from Repository >--------
    /// <summary>
    /// parse TestRequest, create Temp Directory and load files from Repository, return Test info
    /// </summary>
    RequestInfo processRequestAndLoadFiles(Message testRequest)
    {
      string localDir_ = "";
      RequestInfo rqi = new RequestInfo();
      rqi.requestInfo = extractTests(testRequest);
      List<string> files = extractCode(rqi.requestInfo);

      localDir_ = makeKey(testRequest.author);            // name of temporary dir to hold test files
      rqi.tempDirName = localDir_;
      filePath_ = System.IO.Path.GetFullPath(localDir_);  // LoadAndTest will use this path
      Console.Write("\n  creating local test directory \"" + localDir_ + "\"");
      System.IO.Directory.CreateDirectory(localDir_);

      //if have file unavaliable in local, retrieve from rep
      if(inStack(files)==false)
      {
         DownloadFiles(rqi);
      }

      //move files from receive/stack folder to temp folder
      foreach (string file in files)
      {
        string name = System.IO.Path.GetFileName(file);
        string src = System.IO.Path.Combine(receivePath_, file);
        if (System.IO.File.Exists(src))
        {
          string dst = System.IO.Path.Combine(localDir_, name);
          try
          {
                System.IO.File.Copy(src, dst, true);
                //System.IO.File.Move(src, dst);
          }
          catch
          {
            /* do nothing because file was already copied and is being used */
          }
          Console.Write("\n    TID" + Thread.CurrentThread.ManagedThreadId + ": retrieved file \"" + name + "\"");
        }
        else
        {
          Console.Write("\n    TID" + Thread.CurrentThread.ManagedThreadId + ": could not retrieve file \"" + name + "\"");
        }
      }
      Console.WriteLine();
      return rqi;
    }

    /// <summary>
    /// check if files needed are avaliable in stack folder
    /// </summary>
    bool inStack(List<string> files)
    {
        foreach (string file in files)
        {
            string name = System.IO.Path.GetFileName(file);
            string src = System.IO.Path.Combine(receivePath_, file);
            if (System.IO.File.Exists(src) == false)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// load all files required from rep to local stack folder
    /// </summary>
    void DownloadFiles(RequestInfo rqi)
    {
        Console.Write("\n  loading code from Repository");

        //send a files request msg to rep, files will be received in receive folder
        Message reqFiles = makeFilesRequestMessage(rqi);
        sendMessage(reqFiles);
        //waiting for pulse file arrive(means file transfer complete)
        string pulseDir = System.IO.Path.Combine(receivePath_, rqi.tempDirName);
        while (true)
        {
            if (System.IO.File.Exists(pulseDir))
            {
                try//deleting pulse file in reveice folder
                {
                    System.IO.File.Delete(pulseDir);
                    break;
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
        }
    }

    //----< save results and logs in Repository >--------------------
    /// <summary>
    /// save results and logs in local Repository
    /// </summary>
    bool saveResultsAndLogs(ITestResults testResults)
    {
      string logName = testResults.testKey + ".txt";
      System.IO.StreamWriter sr = null;
      try
      {
        sr = new System.IO.StreamWriter(System.IO.Path.Combine(receivePath_, logName));
        sr.WriteLine(logName);
        foreach (ITestResult test in testResults.testResults)
        {
          sr.WriteLine("-----------------------------");
          sr.WriteLine(test.testName);
          sr.WriteLine(test.testResult);
          sr.WriteLine(test.testLog);
        }
        sr.WriteLine("-----------------------------");
      }
      catch
      {
        sr.Close();
        return false;
      }
      sr.Close();
      return true;
    }
    //----< run tests >----------------------------------------------
     /// <summary>
     /// parse test request, get files, create child domain, send result to local rep, unload child domain
     /// </summary>
    ITestResults runTests(Message testRequest)
    {
      AppDomain ad = null;
      ILoadAndTest ldandtst = null;
      RequestInfo rqi = null;
      ITestResults tr = null;

      try
      {
        lock (sync_)
        {
          rqi = processRequestAndLoadFiles(testRequest);
          ad = createChildAppDomain();
          ldandtst = installLoader(ad);
        }
        if (ldandtst != null)
        {
          tr = ldandtst.test(rqi);//call child domain to start test
        }
        // unloading ChildDomain, and so unloading the library

        //saveResultsAndLogs(tr);

        lock (sync_)
        {
          Console.Write("\n  TID" + Thread.CurrentThread.ManagedThreadId + ": unloading: \"" + ad.FriendlyName + "\" - Req #7\n");
          AppDomain.Unload(ad);
          try
          {
            System.IO.Directory.Delete(rqi.tempDirName, true);
            Console.Write("\n  TID" + Thread.CurrentThread.ManagedThreadId + ": removed directory " + rqi.tempDirName);
          }
          catch (Exception ex)
          {
            Console.Write("\n  TID" + Thread.CurrentThread.ManagedThreadId + ": could not remove directory " + rqi.tempDirName);
            Console.Write("\n  TID" + Thread.CurrentThread.ManagedThreadId + ": " + ex.Message);
          }
        }
        return tr;
      }
      catch(Exception ex)
      {
        Console.Write("\n\n---- {0}\n\n", ex.Message);
        return tr;
      }
    }
    //----< make TestResults Message >-------------------------------
    /// <summary>
    /// generate TestResults Message from received message and results returned
    /// </summary>
    Message makeTestResultsMessage(Message origin, ITestResults tr)
    {
      Message trMsg = new Message();
      trMsg.author = "TestHarness";
      trMsg.to = origin.from;
      trMsg.from = origin.to;
      trMsg.type = "TestResults";
      XDocument doc = new XDocument();
      XElement root = new XElement("testResultsMsg");
      doc.Add(root);
      XElement testKey = new XElement("testKey");
      testKey.Value = tr.testKey;
      root.Add(testKey);
      XElement timeStamp = new XElement("timeStamp");
      timeStamp.Value = tr.dateTime.ToString();
      root.Add(timeStamp);
      XElement testResults = new XElement("testResults");
      root.Add(testResults);
      foreach(ITestResult test in tr.testResults)
      {
        XElement testResult = new XElement("testResult");
        testResults.Add(testResult);
        XElement testName = new XElement("testName");
        testName.Value = test.testName;
        testResult.Add(testName);
        XElement result = new XElement("result");
        result.Value = test.testResult;
        testResult.Add(result);
        XElement log = new XElement("log");
        log.Value = test.testLog;
        testResult.Add(log);
      }
      trMsg.body = doc.ToString();
      return trMsg;
    }
    //----< make TestResults Message >-------------------------------
    /// <summary>
    /// generate TestResults Message from received message and results returned
    /// </summary>
    Message makeLogMessage(Message origin)
    {
      Message trMsg = origin.copy();
      trMsg.to = repUrl;
      trMsg.type = "SendLog";
      return trMsg;
    }
    //----< wait for all threads to finish >-------------------------
    /// <summary>
    /// wait for all threads to finish
    /// </summary>
    public void wait()
    {
      rcvThread.Join();
      foreach (Thread t in threads_)
        t.Join();
    }
    //----< main activity of TestHarness >---------------------------
    /// <summary>
    /// start threads to dequeue local queue, run test, send result to local client
    /// </summary>
    public void processMessages()
    {
      AppDomain main = AppDomain.CurrentDomain;
      Console.Write("\n  Starting in AppDomain " + main.FriendlyName + "\n");

      ThreadStart doTests = () => {
        while(true)
          {
            Message testRequest = inQ_.deQ();
            hrt.Start();
            if (testRequest.body == "quit")
            {
                inQ_.enQ(testRequest);//so all the threads quit
                return;
            }
            ITestResults testResults = runTests(testRequest);//finish test in child domain
            hrt.Stop();
            lock (sync_)
            {
                  Console.Write("\n  It takes {0} microsec to run this test- Req #12", hrt.ElapsedMicroseconds);
                  Message ResultsMessage = makeTestResultsMessage(testRequest, testResults);
                  sendMessage(ResultsMessage);
                  Message LogMessage = makeLogMessage(ResultsMessage);
                  sendMessage(LogMessage);
                //client_.sendResults(makeTestResultsMessage(testRequest, testResults));
            }
          }
      };

      int numThreads = 8;

      for(int i = 0; i < numThreads; ++i)//control the num of thread
      {
        Console.Write("\n  Creating AppDomain thread");
        Thread t = new Thread(doTests);
        threads_.Add(t);
        t.Start();
      }
    }
    //----< was used for debugging >---------------------------------

    void showAssemblies(AppDomain ad)
    {
      Assembly[] arrayOfAssems = ad.GetAssemblies();
      foreach (Assembly assem in arrayOfAssems)
        Console.Write("\n  " + assem.ToString());
    }



    //----< create child AppDomain >---------------------------------
    /// <summary>
    /// create, return child AppDomain
    /// </summary>
    public AppDomain createChildAppDomain()
    {
      try
      {
        //Console.Write("\n  creating child AppDomain - Req #4");

        AppDomainSetup domaininfo = new AppDomainSetup();
        domaininfo.ApplicationBase
          = "file:///" + System.Environment.CurrentDirectory;  // defines search path for LoadAndTest library

        //Create evidence for the new AppDomain from evidence of current

        Evidence adevidence = AppDomain.CurrentDomain.Evidence;

        // Create Child AppDomain

        AppDomain ad
          = AppDomain.CreateDomain("ChildDomain", adevidence, domaininfo);

        Console.Write("\n  created Child AppDomain \"" + ad.FriendlyName + "\" - Req #4");
        return ad;
      }
      catch (Exception except)
      {
        Console.Write("\n  " + except.Message + "\n\n");
      }
      return null;
    }
    //----< Load and Test is responsible for testing >---------------
    /// <summary>
    /// Configure child domain with call back, temp folder path, Return reference to LoadAndTest object in child
    /// </summary>
    ILoadAndTest installLoader(AppDomain ad)
    {
      ad.Load("LoadAndTest");
      //showAssemblies(ad);
      //Console.WriteLine();

      // create proxy for LoadAndTest object in child AppDomain

      ObjectHandle oh
        = ad.CreateInstance("LoadAndTest", "TestHarness.LoadAndTest");
      object ob = oh.Unwrap();    // unwrap creates proxy to ChildDomain
                                  // Console.Write("\n  {0}", ob);

      // set reference to LoadAndTest object in child

      ILoadAndTest landt = (ILoadAndTest)ob;

      // create Callback object in parent domain and pass reference
      // to LoadAndTest object in child

      landt.setCallback(cb_);
      landt.loadPath(filePath_);  // send file path to LoadAndTest
      return landt;
    }
#if (TEST_TESTHARNESS)
    static void Main(string[] args)
    {
        //Repository repository = new Repository();
        //TestHarness testHarness = new TestHarness(repository);
        TestHarness testHarness = new TestHarness();
        testHarness.processMessages();
        testHarness.wait();
    }
#endif
  }
}
