using Microsoft.AspNetCore.Mvc;
using ONNXPackage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Net;

namespace Server.Controllers
{
    public class DetectedObject
    {
        public double XMin { get; set; }
        public double YMin { get; set; }
        public double XMax { get; set; }
        public double YMax { get; set; }
        public double Confidence { get; set; }
        public string Class { get; set; }
    }

    [ApiController]
    [Route("[controller]")]
    public class ImageController : ControllerBase
    {
        private static Network network = new();

       [HttpPost("process")]
        public async Task<ActionResult<List<DetectedObject>>> ProcessImageAsync([FromBody] string base64Image)
        {
            try
            {
                var imageBytes = Convert.FromBase64String(base64Image);
                var image = Image.Load<Rgb24>(imageBytes);

                if (network.session == null)
                {
                    network.CreateSession();
                }
                var ct = new CancellationToken();
                var objects = await network.GetDetectedObjectsAsync(image, ct);

                List<DetectedObject> detectedObjects = new();
                foreach (var obj in objects)
                {
                    detectedObjects.Add(new DetectedObject
                    {
                        XMin = obj.Object.XMin,
                        XMax = obj.Object.XMax,
                        YMin = obj.Object.YMin,
                        YMax = obj.Object.YMax,
                        Confidence = obj.Object.Confidence,
                        Class = obj.Object.Class,
                    });
                }
                return Ok(detectedObjects);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, ex.Message);
            }
        }
    }
}
