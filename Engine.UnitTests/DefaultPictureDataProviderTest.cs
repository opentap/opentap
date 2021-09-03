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
            public const string Name = "Test Format";
            // Go after DefaultPictureDataProvider
            public double Order =>
                ((IPictureDataProvider) TypeData.FromType(typeof(DefaultPictureDataProvider)).CreateInstance()).Order +
                1;
            public Task<Stream> GetStream(IPicture picture)
            {
                var stream = new MemoryStream();
                stream.Write(new byte[] {1,2,3,4,5,6,7,8,9,10}, 0, 10);
                return Task.FromResult<Stream>(stream);
            }

            public Task<string> GetPictureFormat(IPicture picture)
            {
                return Task.FromResult(Format);
            }

            public Task<string> GetPictureName(IPicture picture)
            {
                return Task.FromResult(Name);
            }
        }


        [Test]
        public async Task TestFilePicture()
        {
            var source = Path.Combine(ExecutorClient.ExeDir, "Resources/TestImg.png");

            var pic = new Picture() {Source = source, Description = "Test Picture"};

            {
                // Test file name and file type
                Assert.AreEqual("TestImg", await PictureDataProvider.GetPictureName(pic));
                Assert.AreEqual("png", await PictureDataProvider.GetPictureFormat(pic));
            }

            var expectedBytes = File.ReadAllBytes(source);

            using (var picStream = await PictureDataProvider.GetStream(pic))
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
            Assert.AreEqual("package.a7440fd6", await PictureDataProvider.GetPictureName(pic));
            Assert.AreEqual("png", await PictureDataProvider.GetPictureFormat(pic));

            byte[] expectedBytes;
            {
                // Read the actual data
                var client = new HttpClient();
                var req = new HttpRequestMessage(HttpMethod.Get, source);
                var data = client.SendAsync(req).Result;
                expectedBytes = data.Content.ReadAsByteArrayAsync().Result;
            }

            using (var picStream = await PictureDataProvider.GetStream(pic))
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
            
            Assert.AreEqual(TestPictureProvider.Format, await PictureDataProvider.GetPictureFormat(pic));
            Assert.AreEqual(TestPictureProvider.Name, await PictureDataProvider.GetPictureName(pic));

            var bytes = await PictureDataProvider.GetStream(pic) as MemoryStream;
            CollectionAssert.AreEqual(new byte[]{1,2,3,4,5,6,7,8,9,10}, bytes.ToArray());

        }
    }
}
