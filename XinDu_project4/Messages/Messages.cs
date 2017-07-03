/////////////////////////////////////////////////////////////////////
// Messages.cs - defines communication messages                    //
//                                                                 //
// Author:      Xin Du                                             //
// Source:      Jim Fawcett,                                       //
//              CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * -------------------
 * Messages provides helper code for building and parsing XML messages.
 *
 * Required files:
 * ---------------
 * - Messages.cs
 * 
 * Maintanence History:
 * --------------------
 * ver 1.0 : 20 Nov 2016
 * - first release
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Utilities;

namespace TestHarness
{
    [Serializable]
    public class Message
    {
        public string type { get; set; } = "default";
        public string to { get; set; }
        public string from { get; set; }
        public string author { get; set; } = "";
        public DateTime time { get; set; } = DateTime.Now;
        public string body { get; set; } = "none";

        public List<string> messageTypes { get; set; } = new List<string>();

        public Message()
        {
            messageTypes.Add("TestRequest");
            body = "";
        }
        public Message(string bodyStr)
        {
            messageTypes.Add("TestRequest");
            body = bodyStr;
        }
        public Message fromString(string msgStr)
        {
            Message msg = new Message();
            try
            {
                string[] parts = msgStr.Split(',');
                for (int i = 0; i < parts.Count(); ++i)
                    parts[i] = parts[i].Trim();

                msg.type = parts[0].Substring(6);
                msg.to = parts[1].Substring(4);
                msg.from = parts[2].Substring(6);
                msg.author = parts[3].Substring(8);
                msg.time = DateTime.Parse(parts[4].Substring(6));
                msg.body = parts[5].Substring(6);
            }
            catch
            {
                Console.Write("\n  string parsing failed in Message.fromString(string)");
                return null;
            }
            //XDocument doc = XDocument.Parse(body);
            return msg;
        }
        public override string ToString()
        {
            string temp = "type: " + type;
            temp += ", to: " + to;
            temp += ", from: " + from;
            if (author != "")
                temp += ", author: " + author;
            temp += ", time: " + time;
            temp += ", body:\n" + body;
            return temp;
        }
        public Message copy()
        {
            Message temp = new Message();
            temp.type = type;
            temp.to = to;
            temp.from = from;
            temp.author = author;
            temp.time = DateTime.Now;
            temp.body = body;
            return temp;
        }
    }

    public static class extMethods
    {
        /// <summary>
        /// print out formatted message
        /// </summary>
        public static void showMsg(this Message msg)
        {
            Console.Write("\n  formatted message:");
            string[] lines = msg.ToString().Split(new char[] { ',' });
            for(int i=0;i<lines.Count()-1;i++)
            {
                string line = lines[i];
                Console.Write("\n    {0}", line.Trim());
            }
            Console.Write("\n    body:\n{0}", msg.body.shift());
            Console.WriteLine();
        }
        /// <summary>
        /// turn message into formated string
        /// </summary>
        public static string showThis(this object msg)
        {
            string showStr = "\n  formatted message:";
            string[] lines = msg.ToString().Split('\n');
            foreach (string line in lines)
                showStr += "\n    " + line.Trim();
            showStr += "\n";
            return showStr;
        }
        /// <summary>
        /// shift a formatted string message to right
        /// </summary>
        public static string shift(this string str, int n = 4)
        {
            string insertString = new string(' ', n);
            string[] lines = str.Split('\n');
            for (int i = 0; i < lines.Count(); ++i)
            {
                lines[i] = insertString + lines[i];
            }
            string temp = "";
            foreach (string line in lines)
                temp += line + "\n";
            return temp;
        }
        /// <summary>
        /// turn a xml string into a formatted shifted string
        /// </summary>
        public static string formatXml(this string xml, int n = 2)
        {
            XDocument doc = XDocument.Parse(xml);
            return doc.ToString().shift(n);
        }
    }

    public class testElement
  {
    public string testName { get; set; }
    public string testDriver { get; set; }
    public List<string> testCodes { get; set; } = new List<string>();

    public testElement() { }
    public testElement(string name)
    {
      testName = name;
    }
    public void addDriver(string name)
    {
      testDriver = name;
    }
    public void addCode(string name)
    {
      testCodes.Add(name);
    }
    public override string ToString()
    {
      string temp = "<test name=\"" + testName + "\">";
      temp += "<testDriver>" + testDriver + "</testDriver>";
      foreach (string code in testCodes)
        temp += "<library>" + code + "</library>";
      temp += "</test>";
      return temp;
    }
  }

    public class testRequest
  {
    public testRequest() { }
    public string author { get; set; }
    public List<testElement> tests { get; set; } = new List<testElement>();
    public override string ToString()
    {
      string temp = "<testRequest>";
      foreach (testElement te in tests)
        temp += te.ToString();
      temp += "</testRequest>";
      temp = temp.formatXml(0);
      return temp;
    }
  }

  class TestMessages
  {
#if (TEST_MESSAGES)
    static void Main(string[] args)
    {
      Console.Write("\n  Testing Message with Serialized TestRequest");
      Console.Write("\n ---------------------------------------------\n");

      Message msg = new Message();
      msg.to = "http://localhost:8080/ICommunicator";
      msg.from = "http://localhost:8081/ICommunicator";
      msg.author = "Fawcett";
      msg.type = "TestRequest";
      Console.Write("\n\n");

      Console.Write("\n  Testing testRequest");
      Console.Write("\n ---------------------");
      testElement te1 = new testElement("test1");
      te1.addDriver("td1.dll");
      te1.addCode("tc1.dll");
      te1.addCode("tc2.dll");
      testElement te2 = new testElement("test2");
      te2.addDriver("td2.dll");
      te2.addCode("tc3.dll");
      te2.addCode("tc4.dll");
      testRequest tr = new testRequest();
      tr.author = "Jim Fawcett";
      tr.tests.Add(te1);
      tr.tests.Add(te2);
      msg.body = tr.ToXml();

      Console.Write("\n  Serialized TestRequest:");
      Console.Write("\n -------------------------\n");
      Console.Write(msg.body.shift());

      Console.Write("\n  TestRequest Message:");
      Console.Write("\n ----------------------");
      msg.showMsg();

      Console.Write("\n  Testing Deserialized TestRequest");
      Console.Write("\n ----------------------------------\n");
      testRequest trDS = msg.body.FromXml<testRequest>();
      Console.Write(trDS.showThis());
    }
#endif
  }
}
