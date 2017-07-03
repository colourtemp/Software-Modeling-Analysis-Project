/////////////////////////////////////////////////////////////////////
// ICommunicator.cs - Peer-To-Peer Communicator Service Contract   //
// Author:      Xin Du                                             //
// Source:      Jim Fawcett,                                       //
//              CSE681 - Software Modeling & Analysis, Summer 2011 //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * ====================
 * Provide wcf contract supportin message and file transfer
 * 
 * Required Files:
 * ====================
 * - ICommunicator.cs, Message.cs
 *
 * Maintenance History:
 * ====================
 * ver 1.0 : 19 Nov 16
 * - first release
 */

using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace TestHarness
{
    [ServiceContract]
    public interface ICommunicator
    {
        [OperationContract(IsOneWay = true)]
        void PostMessage(Message msg);

        // used only locally so not exposed as service method
        Message GetMessage();

        [OperationContract]
        bool OpenFileForWrite(string name);

        [OperationContract]
        bool WriteFileBlock(byte[] block);

        [OperationContract]
        bool CloseFile();
    }

    // The class Message is defined in CommChannelDemo.Messages as [Serializable]
    // and that appears to be equivalent to defining a similar [DataContract]

}
