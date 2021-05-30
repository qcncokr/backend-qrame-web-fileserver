using System;
using System.Runtime.Serialization;

using MessagePack;

namespace Qrame.Web.FileServer.Message
{
    [MessagePackObject]
    [DataContract]
    public class DownloadResult
    {
        [Key(nameof(Result))]
        [DataMember]
        public bool Result = true;

        [Key(nameof(Message))]
        [DataMember]
        public string Message;

        [Key(nameof(FileName))]
        [DataMember]
        public string FileName;

        [Key(nameof(MimeType))]
        [DataMember]
        public string MimeType;

        [Key(nameof(MD5))]
        [DataMember]
        public string MD5;

        [Key(nameof(Length))]
        [DataMember]
        public long Length = 0;

        [Key(nameof(CreationTime))]
        [DataMember]
        public DateTime CreationTime;

        [Key(nameof(LastWriteTime))]
        [DataMember]
        public DateTime LastWriteTime;
    }
}
