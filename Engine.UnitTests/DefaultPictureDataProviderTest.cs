using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class DefaultPictureDataProviderTest
    {
        public class TestPictureProvider : IPictureDataProvider
        {
            public const string Format = "Test Format";
            public const string Source = "NonFileNameSource";
            
            // Go after DefaultPictureDataProvider
            public double Order => 999;

            Task<Stream> IPictureDataProvider.GetStream(IPicture picture)
            {
                if (picture.Source != Source) return null;
                var stream = new MemoryStream();
                stream.Write(new byte[] {1,2,3,4,5,6,7,8,9,10}, 0, 10);
                return Task.FromResult<Stream>(stream);
            }

            public Task<string> GetFormat(IPicture picture)
            {
                if (picture.Source != Source) return null;
                return Task.FromResult(Format);
            }
        }


        [Test]
        public async Task TestFilePicture()
        {
            var dir = Path.GetDirectoryName(typeof(DefaultPictureDataProviderTest).Assembly.Location);
            var source = Path.Combine(dir, "Resources/TestImg.png");

            var pic = new Picture() {Source = source, Description = "Test Picture"};

            {
                // Test file name and file type
                Assert.AreEqual("png", await pic.GetFormat());
            }

            var expectedBytes = File.ReadAllBytes(source);

            using (var picStream = await pic.GetStream())
            {
                using (var memoryStream = new MemoryStream())
                {
                    picStream.CopyTo(memoryStream);
                    CollectionAssert.AreEqual(expectedBytes, memoryStream.ToArray());
                }
            }
        }

        [Test]
        public async Task TestNoFileExtension()
        {
            var source = "abc";
            var content = "def";
            if (File.Exists(source) == false)
                File.WriteAllText(source, content);

            var pic = new Picture() {Source = source, Description = "No File Extensions"};
            
            Assert.IsNull(await pic.GetFormat());
            Assert.IsNull(await pic.GetStream());
        }

        [Test]
        public async Task TestWebPicture()
        {
            var dir = Path.GetDirectoryName(typeof(DefaultPictureDataProviderTest).Assembly.Location);
            var filename = Path.Combine(dir, "Resources/TestImg.png");
            var fileContent = File.ReadAllBytes(filename);
            
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var source = $"http://{IPAddress.Loopback}:{((IPEndPoint)listener.LocalEndpoint).Port}/file.png";
            TapThread.Start(() =>
            {
                for (int i = 0; i < 2; i++)
                {
                    var client = listener.AcceptTcpClient();
                    var stream = client.GetStream();
                    var header = $"HTTP/1.0 200 OK\r\nContent-Length: {fileContent.Length}\r\n\r\n";
                    stream.Write(Encoding.UTF8.GetBytes(header), 0, header.Length);
                    stream.Write(fileContent, 0, fileContent.Length);
                }
            });



            var pic = new Picture() {Source = source, Description = "Test Picture"};

            // Ensure filenames and types are correct
            Assert.AreEqual("png", await pic.GetFormat());

            byte[] expectedBytes;
            {
                // Read the actual data
                var client = new HttpClient();
                var req = new HttpRequestMessage(HttpMethod.Get, source);
                var data = client.SendAsync(req).Result;
                expectedBytes = data.Content.ReadAsByteArrayAsync().Result;
                CollectionAssert.AreEqual(fileContent, expectedBytes, "Files are not equal");
            }

            using (var picStream = await pic.GetStream())
            {
                using (var memoryStream = new MemoryStream())
                {
                    picStream.CopyTo(memoryStream);
                    CollectionAssert.AreEqual(expectedBytes, memoryStream.ToArray());
                }
            }
            
            listener.Stop();
        }

        [Test]
        public async Task TestOrder()
        {
            var source = TestPictureProvider.Source;
            var pic = new Picture() {Source = source, Description = "Non-existent"};
            
            Assert.AreEqual(TestPictureProvider.Format, await pic.GetFormat());

            var bytes = await pic.GetStream() as MemoryStream;
            CollectionAssert.AreEqual(new byte[]{1,2,3,4,5,6,7,8,9,10}, bytes.ToArray());

        }
    }
}
