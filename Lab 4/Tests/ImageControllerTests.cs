using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Tests
{
    public class ImageControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        readonly HttpClient _client;
        readonly string url = "http://localhost:5053/image/process";

        public ImageControllerTests(WebApplicationFactory<Program> application)
        {
            _client = application.CreateClient();
        }
        
        [Fact]
        public async Task StatusOKTest()
        {
            byte[] imageBytes = File.ReadAllBytes("..\\..\\..\\..\\Tests\\test_image_bird.jpg");
            string base64Image = Convert.ToBase64String(imageBytes);
            
            var response = await _client.PostAsJsonAsync(url, base64Image);
            Assert.Equal((int)HttpStatusCode.OK, (int)response.StatusCode);
        }

        [Fact]
        public async Task StatusErrorTest()
        {
            string notBase64 = "x";

            var response = await _client.PostAsJsonAsync(url, notBase64);
            Assert.Equal((int)HttpStatusCode.InternalServerError, (int)response.StatusCode);
            Assert.Equal((int)HttpStatusCode.InternalServerError, (int)response.StatusCode);
        }

        [Fact]
        public async Task ResponseContentTest()
        {
            string base64Image = Convert.ToBase64String(File.ReadAllBytes("..\\..\\..\\..\\Tests\\test_image_bird.jpg"));

            var response = await _client.PostAsJsonAsync(url, base64Image);

            var result = await response.Content.ReadAsStringAsync();

            var objects = JsonConvert.DeserializeObject<List<DetectedObject>>(result);
            Assert.Equal(1, objects.Count);
            Assert.Equal("bird", objects[0].Class);
        }
    }

    public class DetectedObject
    {
        public double XMin { get; set; }
        public double YMin { get; set; }
        public double XMax { get; set; }
        public double YMax { get; set; }
        public double Confidence { get; set; }
        public string Class { get; set; }
    }
}