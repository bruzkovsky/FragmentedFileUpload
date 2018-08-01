using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FragmentedFileUpload.Extensions;
using FragmentedFileUpload.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace SampleServer_NetCore.Controllers
{
    [Route("[controller]")]
    public class HomeController : Controller
    {
	    private readonly IHostingEnvironment _environment;

	    public HomeController(IHostingEnvironment environment)
	    {
		    _environment = environment;
	    }

        // GET api/values
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new[] { "value1", "value2" };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
		
	    [HttpPost("UploadFile")]
	    public string UploadFile(IFormFile file, string hash, string partHash)
	    {
			var fileName = Path.GetFileName(file.FileName);
			if (string.IsNullOrEmpty(fileName))
				throw new InvalidOperationException("The filename was not set.");

			var uploadPath = Path.Combine(_environment.ContentRootPath, "App_Data", "uploads");
			var outputPath = Path.Combine(_environment.ContentRootPath, "App_Data", "imported", fileName.GetBaseName());

			if (file == null || file.Length <= 0)
				throw new InvalidOperationException("The file is empty or missing.");
			// take the input stream, and save it to a temp folder using the original file.part name posted
			using (var stream = file.OpenReadStream())
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
