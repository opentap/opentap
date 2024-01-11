using System.Net.Http;
namespace OpenTap.Engine.UnitTests
{
    [Display("HTTP Artifact Step", Group: "Test")]
    public class HttpArtifactStep : TestStep
    {
        public string Url { get; set; }
        public string FileName { get; set; } = "test.html";

        public override void Run()
        {
            var response = new HttpClient().GetAsync(Url, HttpCompletionOption.ResponseHeadersRead).Result;
            response.EnsureSuccessStatusCode();

            StepRun.PublishArtifact(response.Content.ReadAsStreamAsync().Result, FileName);
            response.Dispose();
        }
    }
}
