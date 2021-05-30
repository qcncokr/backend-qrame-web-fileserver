using LiteDB;
using Newtonsoft.Json;
using System;

namespace Qrame.Web.FileServer.Entities
{
	public partial class Repository
    {
        [BsonId]
        [JsonProperty("REPOSITORYID")]
        public string RepositoryID { get; set; }
        [JsonProperty("REPOSITORYNAME")]
        public string RepositoryName { get; set; }
        [JsonProperty("APPLICATIONID")]
        public string ApplicationID { get; set; }
        [JsonProperty("PROJECTID")]
        public string ProjectID { get; set; }
        [JsonProperty("STORAGETYPE")]
        public string StorageType { get; set; }
        [JsonProperty("PHYSICALPATH")]
        public string PhysicalPath { get; set; }
        [JsonProperty("AZUREBLOBCONTAINERID")]
        public string AzureBlobContainerID { get; set; }
        [JsonProperty("AZUREBLOBCONNECTIONSTRING")]
        public string AzureBlobConnectionString { get; set; }
        [JsonProperty("AZUREBLOBITEMURL")]
        public string AzureBlobItemUrl { get; set; }
        [JsonProperty("ISVIRTUALPATH")]
        public string IsVirtualPath { get; set; }
        [JsonProperty("ISMULTIUPLOAD")]
        public string IsMultiUpload { get; set; }
        [JsonProperty("ISFILEOVERWRITE")]
        public string IsFileOverWrite { get; set; }
        [JsonProperty("ISFILENAMEENCRYPT")]
        public string IsFileNameEncrypt { get; set; }
        [JsonProperty("ISAUTOPATH")]
        public string IsAutoPath { get; set; }
        [JsonProperty("POLICYPATHID")]
        public string PolicyPathID { get; set; }
        [JsonProperty("UPLOADTYPEID")]
        public string UploadTypeID { get; set; }
        [JsonProperty("UPLOADTYPE")]
        public string UploadType { get; set; }
        [JsonProperty("UPLOADEXTENSIONS")]
        public string UploadExtensions { get; set; }
        [JsonProperty("UPLOADCOUNT")]
        public int UploadCount { get; set; }
        [JsonProperty("UPLOADSIZELIMIT")]
        public int UploadSizeLimit { get; set; }
        [JsonProperty("POLICYEXCEPTIONID")]
        public int PolicyExceptionID { get; set; }
        [JsonProperty("REDIRECTURL")]
        public string RedirectUrl { get; set; }
        [JsonProperty("TRANSACTIONGETITEM")]
        public string TransactionGetItem { get; set; }
        [JsonProperty("TRANSACTIONGETITEMS")]
        public string TransactionGetItems { get; set; }
        [JsonProperty("TRANSACTIONDELETEITEM")]
        public string TransactionDeleteItem { get; set; }
        [JsonProperty("TRANSACTIONUPSERTITEM")]
        public string TransactionUpsertItem { get; set; }
        [JsonProperty("TRANSACTIONUPDATEDENDENCYID")]
        public string TransactionUpdateDendencyID { get; set; }

        [JsonProperty("TRANSACTIONUPDATEFILENAME")]
        public string TransactionUpdateFileName { get; set; }
        [JsonProperty("USEYN")]
        public string UseYN { get; set; }
        [JsonProperty("DESCRIPTION")]
        public string Description { get; set; }
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
