using System;
using System.Runtime.Serialization;

using MessagePack;

namespace Qrame.Web.FileServer.Message
{
    [MessagePackObject]
    [DataContract]
    public class FileUploadRequest
    {
        [Key(nameof(IsFIle))]
        [DataMember]
        public bool IsFIle;

        [Key(nameof(FileName))]
        [DataMember]
        public string FileName;

        [Key(nameof(Length))]
        [DataMember]
        public long Length = 0;

        [Key(nameof(LastWriteTime))]
        [DataMember]
        public DateTime LastWriteTime;
    }
}
