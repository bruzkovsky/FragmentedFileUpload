﻿using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using FragmentedFileUpload.Extensions;
using FragmentedFileUpload.Server;

namespace SampleServer.Controllers
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
        public string UploadFile(HttpPostedFileBase file, string hash, string partHash)
        {
	        var fileName = Path.GetFileName(file.FileName);
	        if (string.IsNullOrEmpty(fileName))
		        throw new InvalidOperationException("The filename was not set.");

	        var uploadPath = Server.MapPath($"~/App_Data/uploads");
	        var outputPath = Path.Combine(Server.MapPath("~/App_Data/imported/"), fileName.GetBaseName());

	        if (file == null || file.ContentLength <= 0)
                throw new InvalidOperationException("The file is empty or missing.");
	        // take the input stream, and save it to a temp folder using the original file.part name posted
            using (var stream = file.InputStream)
            {
                var receiver = Receiver.Create(uploadPath, s =>
                {
	                var directory = Path.GetDirectoryName(outputPath);
	                if (directory != null && !Directory.Exists(directory))
		                Directory.CreateDirectory(directory);
	                using (var writeStream = System.IO.File.OpenWrite(outputPath))
						s.CopyTo(writeStream);
                }, hash);
                try
                {
                    receiver.Receive(stream, fileName, partHash);
                }
                catch (InvalidOperationException e)
                {
                    throw new InvalidOperationException(e.Message);
                }
            }

            return "{\"fileEntryIds\": [\"b4c1cd59-e842-45d4-b0e0-13b9ea2247f5\"]}";
        }
    }
}