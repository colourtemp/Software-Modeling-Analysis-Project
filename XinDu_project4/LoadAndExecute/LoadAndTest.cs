﻿/////////////////////////////////////////////////////////////////////
// LoadAndTest.cs - loads and executes tests using reflection      //
// ver 2.0                                                         //
// Jim Fawcett, CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * -------------------
 * LoadAndTest package operates in child AppDomain.  It loads and
 * executes test code defined by a TestRequest message.
 *
 * Required files:
 * ---------------
 * - LoadAndTest.cs
 * - ITest.cs
 * - Logger, Messages
 * 
 * Maintanence History:
 * --------------------
 * ver 2.0 : 13 Nov 2016
 * - remove logger statements
 * - added thread id markers on displays
 * - modified some of the error handling
 * ver 1.1 : 11 Oct 2016
 * - now loading files using absolute path evaluated
 *   for the machine on which this application runs
 * ver 1.0 : 16 Oct 2016
 * - first release
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Reflection;
using System.Threading;
using System.Security.Policy;
using System.Runtime.Remoting;

namespace TestHarness
{
  public class LoadAndTest : MarshalByRefObject, ILoadAndTest
  {
    private ICallback cb_ = null;
    private string loadPath_ = "";
    object sync_ = new object();

    ///////////////////////////////////////////////////////
    // Data Structures used to store test information
    //
    [Serializable]
    private class TestResult : ITestResult
    {
      public string testName { get; set; }
      public string testResult { get; set; }
      public string testLog { get; set; }
    }
    [Serializable]
    private class TestResults : ITestResults
    {
      public string testKey { get; set; }
      public DateTime dateTime { get; set; }
      public List<ITestResult> testResults { get; set; } = new List<ITestResult>();
    }
    TestResults testResults_ = new TestResults();

    //----< initialize loggers >-------------------------------------

    public LoadAndTest()
    {
    }
    public void loadPath(string path)
    {
      loadPath_ = path;
      //Console.Write("\n  loadpath = {0}", loadPath_);
    }
    //----< load libraries into child AppDomain and test >-----------
    /*
     * ToDo:
     * - refactor this function to make it more testable.
     */
    public ITestResults test(IRequestInfo testRequest)
    {
      TestResults testResults = new TestResults();
      foreach(ITestInfo test in testRequest.requestInfo)
      {
        TestResult testResult = new TestResult();
        testResult.testName = test.testName;
        try
        {
          Console.Write("\n  TID" + Thread.CurrentThread.ManagedThreadId + ": -- \"" + test.testName + "\" --");

          ITest tdr = null;
          string testDriverName = "";
          string fileName = "";

          foreach (string file in test.files)
          {
            fileName = file;
            Assembly assem = null;
            //load Files from Temp Dir into AppDomain
            try
            {
              if (loadPath_.Count() > 0)
              {
                assem = Assembly.LoadFrom(loadPath_ + "/" + file);
              }
              else
                assem = Assembly.Load(file);
            }
            catch
            {
              testResult.testResult = "failed";
              testResult.testLog = "file not loaded";
              Console.Write("\n    TID" + Thread.CurrentThread.ManagedThreadId + ": can't load\"" + file + "\"");
              continue;
            }
            Console.Write("\n    TID" + Thread.CurrentThread.ManagedThreadId + ": loaded \"" + file + "\"");
            Type[] types = assem.GetExportedTypes();
            //find out test driver and make a instance
            foreach (Type t in types)
            {
              if (t.IsClass && typeof(ITest).IsAssignableFrom(t))  // does this type derive from ITest ?
              {
                try
                {
                  testDriverName = file;
                  tdr = (ITest)Activator.CreateInstance(t);    // create instance of test driver
                  Console.Write(
                    "\n    TID" + Thread.CurrentThread.ManagedThreadId + ": " + testDriverName + " implements ITest interface - Req #5"
                  );
                }
                catch
                {
                  //Console.Write("\n----" + file + " - exception thrown when created");
                  continue;
                }
              }
            }
          }
          Console.Write("\n    TID" + Thread.CurrentThread.ManagedThreadId + ": testing " + testDriverName);
          bool testReturn;
          //run current test
          try
          {
            testReturn = tdr.test();
          }
          catch
          {
            //Console.Write("\n----exception thrown in " + fileName);
            testReturn = false;
          }
          //write current test result, send simple result through callback 
          if (tdr != null && testReturn == true)
          {
            testResult.testResult = "passed";
            testResult.testLog = tdr.getLog();
            Console.Write("\n    TID" + Thread.CurrentThread.ManagedThreadId + ": test passed");
            if (cb_ != null)
            {
              Message msg = new Message(testDriverName + " passed");
              msg.type = "TestResult";
              cb_.sendMessage(msg);
            }
          }
          else
          {
            testResult.testResult = "failed";
            if (tdr != null)
              testResult.testLog = tdr.getLog();
            else
              testResult.testLog = "file not loaded - Req #3";
            Console.Write("\n    TID" + Thread.CurrentThread.ManagedThreadId + ": test failed");
            if (cb_ != null)
            {
              Message msg = new Message(testDriverName + " failed");
              msg.type = "TestResult";
              cb_.sendMessage(msg);
            }
          }
        }
        catch(Exception ex)
        {
          testResult.testResult = "failed";
          testResult.testLog = "exception thrown";
          Console.Write("\n  TID" + Thread.CurrentThread.ManagedThreadId + ": " + ex.Message);
        }
        testResults_.testResults.Add(testResult);//add current result to results
      }

      testResults_.dateTime = DateTime.Now;
      testResults_.testKey = System.IO.Path.GetFileName(loadPath_);
      return testResults_;//resturn Test Results wo Primary AppDomain
    }
    //----< TestHarness calls to pass ref to Callback function >-----
     
    public void setCallback(ICallback cb)
    {
      cb_ = cb;
    }

#if (TEST_LOADANDTEST)
    static void Main(string[] args)
    {
            ILoadAndTest ldandtst = null;
            //RequestInfo rqi = null;
            AppDomainSetup domaininfo = new AppDomainSetup();
            domaininfo.ApplicationBase
              = "file:///" + System.Environment.CurrentDirectory;  // defines search path for LoadAndTest library

            Evidence adevidence = AppDomain.CurrentDomain.Evidence;
            AppDomain ad
              = AppDomain.CreateDomain("ChildDomain", adevidence, domaininfo);
            ad.Load("LoadAndTest");
            ObjectHandle oh = ad.CreateInstance("LoadAndTest", "TestHarness.LoadAndTest");
            object ob = oh.Unwrap();    // unwrap creates proxy to ChildDomain
            ldandtst = (ILoadAndTest)ob;
            //ITestResults tr = ldandtst.test(rqi);
        }
#endif
    }
}
