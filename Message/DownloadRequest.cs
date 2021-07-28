using System.Runtime.Serialization;

using MessagePack;

namespace Qrame.Web.FileServer.Message
{
	[MessagePackObject]
	[DataContract]
	public class DownloadRequest
	{
		[Key(nameof(RepositoryID))]
		[DataMember]
		public string RepositoryID;

		[Key(nameof(ItemID))]
		[DataMember]
		public string ItemID;

		[Key(nameof(BusinessID))]
		[DataMember]
		public string BusinessID;

		[DataMember]
		public string FileMD5;

		[DataMember]
		public string TokenID;
	}
}
