using NUnit.Framework;
using OpenTap.Cli;
using OpenTap.Package;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Image.Tests
{
    /// <summary>
    /// Deploys images from a local HTTP repository, where packages are staged while they are downloaded.
    /// </summary>
    public class StreamingDownload
    {
        /// <summary>
        /// Serves a single package the way the repository's object download endpoint does. The response body is sent
        /// in small, delayed chunks, so staging catches up with the download.
        /// </summary>
        sealed class PackageServer : IDisposable
        {
            readonly HttpListener listener = new();
            readonly byte[] package;
            readonly Task serving;
            public string Url { get; }
            /// <summary> If set, the response is cut off after this many bytes, and later requests fail. </summary>
            public int? FailAfter { get; set; }
            public int Requests;

            public PackageServer(byte[] package)
            {
                this.package = package;
                Url = $"http://localhost:{FreePort()}";
                listener.Prefixes.Add(Url + "/");
                listener.Start();
                serving = Task.Run(Serve);
            }

            static int FreePort()
            {
                var l = new TcpListener(IPAddress.Loopback, 0);
                l.Start();
                int port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return port;
            }

            async Task Serve()
            {
                while (listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = await listener.GetContextAsync(); }
                    catch { return; }
                    Interlocked.Increment(ref Requests);
                    try
                    {
                        if (FailAfter != null && Requests > 1)
                        {
                            ctx.Response.StatusCode = 500;
                            ctx.Response.Close();
                            continue;
                        }
                        ctx.Response.StatusCode = 200;
                        ctx.Response.AddHeader("Content-Disposition", "attachment; filename=package.TapPackage");
                        ctx.Response.ContentLength64 = package.Length;
                        int limit = FailAfter ?? package.Length;
                        for (int pos = 0; pos < limit; pos += 997)
                        {
                            await ctx.Response.OutputStream.WriteAsync(package, pos, Math.Min(997, limit - pos));
                            await ctx.Response.OutputStream.FlushAsync();
                            await Task.Delay(1);
                        }
                        if (FailAfter != null)
                            ctx.Response.Abort();
                        else
                            ctx.Response.Close();
                    }
                    catch
                    {
                        /* The client disconnected. */
                    }
                }
            }

            public void Dispose()
            {
                listener.Stop();
                listener.Close();
                try { serving.Wait(TimeSpan.FromSeconds(5)); } catch { }
            }
        }

        /// <param name="name">The package name.</param>
        /// <param name="streamable">If false, the package uses data descriptors (sizes stored after the file data),
        /// which <see cref="OpenTap.Package.Zip.StreamingZipReader"/> cannot stream, so staging falls back to ZipArchive.</param>
        static (PackageDef def, byte[] bytes) CreatePackage(string name, bool streamable = true)
        {
            var def = new PackageDef
            {
                Name = name,
                // A unique version ensures the package is never found in the package cache.
                Version = new SemanticVersion(1, 0, 0, null, Guid.NewGuid().ToString("N")),
                Architecture = CpuArchitecture.AnyCPU,
                OS = "Windows,Linux,MacOS",
            };
            var files = Enumerable.Range(0, 50).Select(i => $"{name}/file{i}.txt").ToList();
            foreach (var f in files)
                def.Files.Add(new PackageFile { RelativeDestinationPath = f });

            var ms = new MemoryStream();
            /* ZipArchive writes data descriptors when the output stream cannot seek. */
            using (var zip = new ZipArchive(streamable ? ms : new WriteOnlyStream(ms), ZipArchiveMode.Create, true))
            {
                using (var s = zip.CreateEntry($"Packages/{name}/package.xml").Open())
                    def.SaveTo(s);
                for (int i = 0; i < files.Count; i++)
                {
                    using var w = new StreamWriter(zip.CreateEntry(files[i]).Open());
                    w.Write(new string((char)('a' + i % 26), 500 + i * 100));
                }
            }
            return (def, ms.ToArray());
        }

        /// <summary> Temporary download files are named after the cache file, with a '.{guid}' suffix. </summary>
        static string[] TemporaryDownloads(string cachePath) =>
            Directory.GetFiles(Path.GetDirectoryName(cachePath), Path.GetFileName(cachePath) + ".*")
                .Where(f => Guid.TryParseExact(Path.GetExtension(f).TrimStart('.'), "N", out _))
                .ToArray();

        static ImageIdentifier CreateImage(PackageDef def, string url)
        {
            def.PackageSource = new HttpRepositoryPackageDefSource { RepositoryUrl = url };
            return new ImageIdentifier([def], [url]);
        }

        [Test]
        public void DeploysWhileDownloading()
        {
            var (def, bytes) = CreatePackage("StreamedPackage");
            using var server = new PackageServer(bytes);
            using var tempInstall = new TempInstall();
            var cachePath = PackageCacheHelper.GetCacheFilePath(def);
            try
            {
                CreateImage(def, server.Url).Deploy(tempInstall.Directory, CancellationToken.None);

                for (int i = 0; i < 50; i++)
                {
                    var file = Path.Combine(tempInstall.Directory, def.Name, $"file{i}.txt");
                    Assert.AreEqual(new string((char)('a' + i % 26), 500 + i * 100), File.ReadAllText(file));
                }
                Assert.IsTrue(tempInstall.Installation.GetPackages().Any(p => p.Name == def.Name));
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(cachePath), "The download should be in the package cache.");
                Assert.IsEmpty(TemporaryDownloads(cachePath), "Temporary download files should be removed.");
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        /// <summary>
        /// A package that cannot be streamed is read again with ZipArchive from the package cache. Reading the
        /// download stream to the end must wait until the download has been moved into the cache.
        /// The download is slow, so the fallback starts long before it completes.
        /// </summary>
        [Test]
        public void UnstreamablePackageFallsBackToCachedFile()
        {
            var (def, bytes) = CreatePackage("UnstreamablePackage", streamable: false);
            using var server = new PackageServer(bytes);
            using var tempInstall = new TempInstall();
            var cachePath = PackageCacheHelper.GetCacheFilePath(def);
            try
            {
                CreateImage(def, server.Url).Deploy(tempInstall.Directory, CancellationToken.None);

                for (int i = 0; i < 50; i++)
                {
                    var file = Path.Combine(tempInstall.Directory, def.Name, $"file{i}.txt");
                    Assert.AreEqual(new string((char)('a' + i % 26), 500 + i * 100), File.ReadAllText(file));
                }
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(cachePath));
                Assert.IsEmpty(TemporaryDownloads(cachePath), "Temporary download files should be removed.");
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        sealed class WriteOnlyStream(Stream inner) : Stream
        {
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        }

        [Test]
        public void FailedDownloadGivesNetworkError()
        {
            var (def, bytes) = CreatePackage("FailedDownload");
            using var server = new PackageServer(bytes) { FailAfter = bytes.Length / 2 };
            using var tempInstall = new TempInstall();
            var cachePath = PackageCacheHelper.GetCacheFilePath(def);

            var ex = Assert.Throws<ExitCodeException>(() => CreateImage(def, server.Url).Deploy(tempInstall.Directory, CancellationToken.None));

            Assert.AreEqual((int)ExitCodes.NetworkError, ex.ExitCode);
            StringAssert.Contains(def.Name, ex.Message);
            StringAssert.Contains(server.Url, ex.Message);
            Assert.IsFalse(ex.Message.TrimEnd().EndsWith(":"), "The message should include the cause.");
            Assert.AreEqual(2, server.Requests, "The interrupted download should be resumed once.");
            Assert.IsFalse(File.Exists(cachePath), "A failed download must not be added to the package cache.");
            Assert.IsEmpty(TemporaryDownloads(cachePath), "Temporary download files should be removed.");
        }
    }
}
