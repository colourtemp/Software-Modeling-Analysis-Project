/////////////////////////////////////////////////////////////////////
// Repository.cs - holds test code for TestHarness                 //
//                                                                 //
// Author:      Xin Du                                             //
// Source:      Jim Fawcett,                                       //
//              CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * -------------------
 * save files from client
 * sends files to TestHarness.
 * save log from TestHarness
 * send log file list to client
 * send log to client according to query
 * 
 * Required Files:
 * -------------------
 * - Client.cs, ITest.cs, Logger.cs, Messages.cs, Serializaiton.cs
 * 
 * Maintenance History:
 * --------------------
 * ver 1.0 : 20 Nov 2016
 * - first release
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Utilities;
using System.Xml.Linq;

namespace TestHarness
{

    public class Repository : IRepository
  {
    string repoStoragePath = "..\\..\\ReceivedFiles\\";
    public Comm<Repository> comm { get; set; } = new Comm<Repository>();
    public string endPoint { get; } = Comm<Repository>.makeEndPoint("http://localhost", 8082);
    private Thread rcvThread = null;

    public Repository()
    {
      Console.Title = "Repository: " + endPoint;
      Console.Write("\n  Creating instance of Repository");
      repoStoragePath = System.IO.Path.GetFullPath(repoStoragePath);
      comm.rcvr.CreateRecvChannel(endPoint);
      rcvThread = comm.rcvr.start(rcvThreadProc);
    }

    public void wait()
    {
      rcvThread.Join();
    }

    //----< retrieve test code from testRequest >--------------------

    List<string> extractCode(List<ITestInfo> testInfos)
    {
      Console.Write("\n  retrieving code files from testInfo data structure");
      List<string> codes = new List<string>();
      foreach (ITestInfo testInfo in testInfos)
        codes.AddRange(testInfo.files);
      return codes;
    }

    /// <summary>
    /// put message in sender's queue
    /// </summary>
    public Message sendMessage(Message msg)
    {
        //Console.Write("\n  {0} send message:", comm.name);
        //msg.showMsg();
        comm.sndr.PostMessage(msg);
      return msg;
    }

    /// <summary>
    /// make a file to notify that file transfer has finished
    /// </summary>
    void makePulseFile(string name)
    {
        string fileSpec = repoStoragePath + name;
        FileStream fs = File.Open(fileSpec, FileMode.Create, FileAccess.Write);
        fs.Close();
    }

    void rcvThreadProc()
    {
      while (true)
      {
        Message msg = comm.rcvr.GetMessage();
        msg.time = DateTime.Now;
        //Console.Write("\n  REPOSITORY RECEIVE---------------------");
        //Console.Write("\n  {0} received message:", comm.name);
        //msg.showMsg();
        switch(msg.type)
        {
            case "FilesRequest":
            {
                Console.Write("\n  REPOSITORY RECEIVE FilesRequest----------------");
                Console.Write("\n  {0} received message:", comm.name);
                msg.showMsg();
                string body = msg.body;
                List<string> files = body.FromXml<List<string>>();
                //send messages to deliver files required
                for(int i=0;i<files.Count()-1;i++)
                {
                    string file = files[i];
                    string fqSrcFile = repoStoragePath + file;
                    Message fpMsg = makeFileReplyMessage(msg, fqSrcFile);
                    sendMessage(fpMsg);
                }
                //send message to deliver a flag file
                makePulseFile(files.Last());
                string pulseFile = repoStoragePath + files.Last();
                Message Msg = makeFileReplyMessage(msg, pulseFile);
                Msg.author = "RepPulse";
                sendMessage(Msg);
                break;
            }
            case "SendLog":
            {
                Console.Write("\n  REPOSITORY RECEIVE Log--------------------------- - Req #7");
                Console.Write("\n  {0} received message:", comm.name);
                msg.showMsg();
                saveLog(msg);               
                break;
            }
            case "ResultsQuery":
            {
                Console.Write("\n  REPOSITORY RECEIVE Test Results/Log Query--------- - Req #7");
                Console.Write("\n  {0} received message:", comm.name);
                msg.showMsg();
                List<string> queryFiles = queryLogs(msg.body);
                List<Message> ResultReplyMessages = makeResultReplyMessages(msg, queryFiles);
                foreach(Message replyMsg in ResultReplyMessages)
                {
                    sendMessage(replyMsg);
                }
                break;
            }
            case "ContentQuery":
            {
                Console.Write("\n  REPOSITORY RECEIVE Log List Query------------------");
                Console.Write("\n  {0} received message:", comm.name);
                msg.showMsg();
                Message qfMsg = queryFileNames(msg);
                sendMessage(qfMsg);
                break;
            }
            default:
                break;
        }
        if (msg.body == "quit")
          break;
      }
    }

    /// <summary>
    /// generate a list of ResultReply message with a list of files stored in string
    /// </summary>
    List<Message> makeResultReplyMessages(Message origin, List<string> queryFiles)
    {
        List<Message> resultsMessages = new List<Message>();

        foreach (string file in queryFiles)
        {
            Message rpMsg = new Message();
            rpMsg.author = "Repository";
            rpMsg.to = origin.from;
            rpMsg.from = origin.to;
            rpMsg.type = "ResultReply";
            rpMsg.body = file;
            resultsMessages.Add(rpMsg);
        }
        return resultsMessages;
    }

    /// <summary>
    /// generate a FileReply message(which will send out file) with full qualified file name
    /// </summary>
    Message makeFileReplyMessage(Message origin, string file)
    {
        Message fpMsg = new Message();
        fpMsg.author = "Repository";
        fpMsg.to = origin.from;
        fpMsg.from = origin.to;
        fpMsg.type = "FileReply";
        fpMsg.body = file;
        return fpMsg;
    }
    //----< search for text in log files >---------------------------
    /// <summary>
    /// search key words in log files, store files wanted in the format of strings
    /// </summary>
    public List<string> queryLogs(string queryText)
    {
      List<string> queryResults = new List<string>();
      string path = System.IO.Path.GetFullPath(repoStoragePath);
      string[] files = System.IO.Directory.GetFiles(repoStoragePath, "*.txt");
      foreach(string file in files)
      {
        string contents = File.ReadAllText(file);
        if (contents.Contains(queryText))
        {
          string name = System.IO.Path.GetFileName(file);
          queryResults.Add(contents);
        }
      }
      queryResults.Sort();
      queryResults.Reverse();
      return queryResults;
    }
    /// <summary>
    /// search key words in file names, store wanted file names in a ContentReply Msg
    /// </summary>
    public Message queryFileNames(Message origin)
    {
        string path = System.IO.Path.GetFullPath(repoStoragePath);
        string[] files = System.IO.Directory.GetFiles(repoStoragePath, origin.body);
        List<string> fileNames = new List<string>();
        foreach(string file in files)
        {
            string fileName = Path.GetFileName(file);
            fileNames.Add(fileName);
        }

        string namelist = fileNames.ToXml();
        Message rpMsg = new Message();
        rpMsg.author = "Repository";
        rpMsg.to = origin.from;
        rpMsg.from = origin.to;
        rpMsg.type = "ContentReply";
        rpMsg.body = namelist;
        return rpMsg;
    }
    //----< send files with names on fileList >----------------------

    public bool getFiles(string path, string fileList)
    {
      string[] files = fileList.Split(new char[] { ',' });
      //string repoStoragePath = "..\\..\\RepositoryStorage\\";

      foreach (string file in files)
      {
        string fqSrcFile = repoStoragePath + file;
        string fqDstFile = "";
        try
        {
          fqDstFile = path + "\\" + file;
          File.Copy(fqSrcFile, fqDstFile);
        }
        catch
        {
          Console.Write("\n  could not copy \"" + fqSrcFile + "\" to \"" + fqDstFile);
          return false;
        }
      }
      return true;
    }
    //----< intended for Project #4 >--------------------------------
    /// <summary>
    /// store a log message in rep folder
    /// </summary>
    public bool saveLog(Message msg)
    {
        XDocument doc = XDocument.Parse(msg.body);
        IEnumerable<XElement> allElements = doc.Descendants("testKey");
        string logName = allElements.First().Value + ".txt";
        System.IO.StreamWriter sr = null;
        try
        {
        sr = new System.IO.StreamWriter(System.IO.Path.Combine(repoStoragePath, logName));
        sr.WriteLine(msg.body);
        }
        catch
        {
        sr.Close();
        return false;
        }
        sr.Close();
        Console.Write("\n Saved Log file: {0} - Req #8", logName);
        return true;
    }

    public void sendLog(string msg)
    {
    }
#if (TEST_REPOSITORY)
    static void Main(string[] args)
    {
            /*
             * ToDo: Rep, testharness, and client shoud be tested as a whole
             */
            Repository rep = new Repository();
            rep.wait();
    }
#endif
  }
}
