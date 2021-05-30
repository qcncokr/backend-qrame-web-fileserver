using LiteDB;
using Newtonsoft.Json;
using System;

namespace Qrame.Web.FileServer.Entities
{
	public partial class RepositoryItems
    {
        [BsonId]
        [JsonProperty("ITEMID")]
        public string ItemID { get; set; }
        [JsonProperty("REPOSITORYID")]
        public string RepositoryID { get; set; }
        [JsonProperty("DEPENDENCYID")]
        public string DependencyID { get; set; }
        [JsonProperty("FILENAME")]
        public string FileName { get; set; }
        [JsonProperty("ORDERBY")]
        public int OrderBy { get; set; }
        [JsonProperty("ITEMSUMMARY")]
        public string ItemSummary { get; set; }
        [JsonProperty("PHYSICALPATH")]
        public string PhysicalPath { get; set; }
        [JsonProperty("ABSOLUTEPATH")]
        public string AbsolutePath { get; set; }
        [JsonProperty("RELATIVEPATH")]
        public string RelativePath { get; set; }
        [JsonProperty("EXTENSION")]
        public string Extension { get; set; }
        [JsonProperty("FILELENGTH")]
        public long FileLength { get; set; }
        [JsonProperty("MD5")]
        public string MD5 { get; set; }
        [JsonProperty("MIMETYPE")]
        public string MimeType { get; set; }
        [JsonProperty("CREATIONTIME")]
        public DateTime CreationTime { get; set; }
        [JsonProperty("LASTWRITETIME")]
        public DateTime LastWriteTime { get; set; }
        [JsonProperty("CUSTOMPATH1")]
        public string CustomPath1 { get; set; }
        [JsonProperty("CUSTOMPATH2")]
        public string CustomPath2 { get; set; }
        [JsonProperty("CUSTOMPATH3")]
        public string CustomPath3 { get; set; }
        [JsonProperty("POLICYPATH")]
        public string PolicyPath { get; set; }
        [JsonProperty("CREATEUSERID")]
        public string CreateUserID { get; set; }
        [JsonProperty("CREATEDATETIME")]
        public DateTime CreateDateTime { get; set; }
        [JsonProperty("UPDATEUSERID")]
        public string UpdateUserID { get; set; }
        [JsonProperty("UPDATEDATETIME")]
        public DateTime UpdateDateTime { get; set; }
    }

}
