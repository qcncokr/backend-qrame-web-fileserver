using Microsoft.AspNetCore.Http;

namespace Qrame.Web.FileServer.Models.Home
{
    public class FileInputModel
    {
        public IFormFile FileToUpload { get; set; }
    }
}
