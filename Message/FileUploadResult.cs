using System.Collections.Generic;
using System.Runtime.Serialization;

using MessagePack;

namespace Qrame.Web.FileServer.Message
{
    [MessagePackObject]
    [DataContract]
    public class FileUploadResult
    {
        [Key(nameof(Result))]
        [DataMember]
        public bool Result = true;

        [Key(nameof(Message))]
        [DataMember]
        public string Message = "";

        [Key(nameof(ItemID))]
        [DataMember]
        public string ItemID = "";

        [Key(nameof(RemainingCount))]
        [DataMember]
        public int RemainingCount = 0;
    }

    [MessagePackObject]
    [DataContract]
    public class MultiFileUploadResult
    {
        [Key(nameof(Result))]
        [DataMember]
        public bool Result = true;

        [Key(nameof(Message))]
        [DataMember]
        public string Message = "";

        [Key(nameof(FileUploadResults))]
        [DataMember]
        public List<FileUploadResult> FileUploadResults = new List<FileUploadResult>();

        [Key(nameof(RemainingCount))]
        [DataMember]
        public int RemainingCount = 0;
    }
}
