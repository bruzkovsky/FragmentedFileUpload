using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Mvc;
using FragmentedFileUpload.Server;

namespace MVCApp.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult ServerMerge()
        {
            return View();
        }

        public ActionResult ClientUpload()
        {
            return View();
        }


        // generic file post method - use in MVC or WebAPI
        [HttpPost]
        public HttpResponseMessage UploadFile(HttpPostedFileBase file, string hash, string partHash)
        {
            var uploadPath = Server.MapPath($"~/App_Data/uploads/{hash}");
            var outputPath = Server.MapPath("~/App_Data/imported/");
            if (file == null || file.ContentLength <= 0)
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("The file is empty or missing.")
                };

            var fileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrEmpty(fileName))
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("The filename was not set.")
                };

            // take the input stream, and save it to a temp folder using the original file.part name posted
            using (var stream = file.InputStream)
            {
                var receiver = Receiver.Create(uploadPath, outputPath, hash);
                try
                {
                    receiver.Receive(stream, fileName, partHash);
                }
                catch (InvalidOperationException e)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.BadRequest,
                        Content = new StringContent(e.Message)
                    };
                }
            }

            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("File uploaded.")
            };
        }
    }
}