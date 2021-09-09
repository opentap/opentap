using System.IO;
using System.Net.Http;
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
            // Go after DefaultPictureDataProvider
            public double Order => 999;

            Task<Stream> IPictureDataProvider.GetStream(IPicture picture)
            {
                var stream = new MemoryStream();
                stream.Write(new byte[] {1,2,3,4,5,6,7,8,9,10}, 0, 10);
                return Task.FromResult<Stream>(stream);
            }

            public Task<string> GetFormat(IPicture picture)
            {
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
        public async Task TestWebPicture()
        {
            var source = @"http://packages.opentap.io/img/package.a7440fd6.png";

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
            }

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
        public async Task TestOrder()
        {
            var source = "Source which does not exist";
            var pic = new Picture() {Source = source, Description = "Non-existent"};
            
            Assert.AreEqual(TestPictureProvider.Format, await pic.GetFormat());

            var bytes = await pic.GetStream() as MemoryStream;
            CollectionAssert.AreEqual(new byte[]{1,2,3,4,5,6,7,8,9,10}, bytes.ToArray());

        }
    }
}
