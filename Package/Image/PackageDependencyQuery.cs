using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace OpenTap.Package
{
    internal class PackageQueryException : Exception
    {
        public PackageQueryException(string message) : base(message)
        {

        }
    }

    /// <summary>
    /// This class takes care of making the right package query queries to the repository server.
    /// </summary>
    static class PackageDependencyQuery
    {
        static readonly TraceSource log = Log.CreateSource("GraphQL");

        /// <summary>
        /// Only used by unit tests
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="compressed"></param>
        /// <returns></returns>
        public static PackageDependencyGraph LoadGraph(Stream stream, bool compressed)
        {
            if(compressed)
                stream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true); 
            
            var graph = new PackageDependencyGraph();
            var doc = JsonDocument.Parse(stream);
            var packages = doc.RootElement.GetProperty("packages");
            graph.LoadFromJson(packages);
            if (compressed)
                stream.Dispose();
            return graph;
        }
    }
}
