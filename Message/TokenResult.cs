using System.Runtime.Serialization;

using MessagePack;

namespace Qrame.Web.FileServer.Message
{
    [MessagePackObject]
    [DataContract]
    public class TokenResult
    {
        [Key(nameof(Token))]
        [DataMember]
        public string Token = "";

        [Key(nameof(Message))]
        [DataMember]
        public string Message = "";

        [Key(nameof(Result))]
        [DataMember]
        public bool Result = true;
    }
}
