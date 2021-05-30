using System.Runtime.Serialization;

using MessagePack;

namespace Qrame.Web.FileServer.Message
{
	[MessagePackObject]
	[DataContract]
	public class DownloadRequest
	{
		[Key(nameof(ItemID))]
		[DataMember]
		public string ItemID;

		[Key(nameof(FileMD5))]
		[DataMember]
		public string FileMD5;

		[Key(nameof(TokenID))]
		[DataMember]
		public string TokenID;

		[Key(nameof(RepositoryID))]
		[DataMember]
		public string RepositoryID;
	}
}
