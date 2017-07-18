using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web.Mvc;
using FragmentedFileUpload.Extensions;
using FragmentedFileUpload;

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
        public HttpResponseMessage UploadFile()
        {
            var hash = Request.Form["hash"];
            var uploadPath = Server.MapPath($"~/App_Data/uploads/{hash}");
            foreach (string file in Request.Files)
            {
                var fileDataContent = Request.Files[file];
                if (fileDataContent == null || fileDataContent.ContentLength <= 0)
                    continue;

                // take the input stream, and save it to a temp folder using the original file.part name posted
                using (var stream = fileDataContent.InputStream)
                {
                    var fileName = Path.GetFileName(fileDataContent.FileName) ?? "upload.file";
                    Directory.CreateDirectory(uploadPath);
                    var path = Path.Combine(uploadPath, fileName);
                    try
                    {
                        if (System.IO.File.Exists(path))
                            System.IO.File.Delete(path);
                        using (var fileStream = System.IO.File.Create(path))
                        {
                            stream.CopyTo(fileStream);
                        }
                    }
                    catch (IOException ex)
                    {
                        // handle
                    }

                    var outputPath = Server.MapPath("~/App_Data/imported/");
                    var merger = FileMerger.Create(path, outputPath);
                    string outputFilePath = null;
                    try
                    {
                        outputFilePath = merger.MergeFile();
                    }
                    catch (InvalidOperationException e)
                    {
                        Debug.WriteLine($"Exception: {e.Message}");
                    }

                    if (outputFilePath == null || !System.IO.File.Exists(outputFilePath))
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            Content = new StringContent("The parts could not be merged.")
                        };

                    using (var resultStream = System.IO.File.OpenRead(outputFilePath))
                    {
                        var resultHash = resultStream.ComputeSha256Hash();
                        if (!string.Equals(resultHash, hash))
                            return new HttpResponseMessage
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                                Content = new StringContent("The hash of the merged file does not match.")
                            };
                    }
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