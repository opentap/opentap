using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    class HttpStringContent : HttpContent
    {
        readonly string content;
        public HttpStringContent(string content)
        {
            this.content = content;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            return stream.WriteAsync(bytes, 0, bytes.Length);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = Encoding.UTF8.GetByteCount(content);
            return true;
        }
    }
}