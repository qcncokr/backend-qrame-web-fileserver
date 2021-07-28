using System;
using System.Runtime.Serialization;

using MessagePack;

namespace Qrame.Web.FileServer.Message
{
    [MessagePackObject]
    [DataContract]
    public class DeleteResult
    {
        [Key(nameof(Result))]
        [DataMember]
        public bool Result = true;

        [Key(nameof(Message))]
        [DataMember]
        public string Message;
    }
}
