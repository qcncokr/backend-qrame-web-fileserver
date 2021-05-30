using System.Runtime.Serialization;

using MessagePack;

namespace Qrame.Web.FileServer.Message
{
    public class JsonContentResult
    {
        public dynamic Message = "";

        public bool Result;
    }
}
