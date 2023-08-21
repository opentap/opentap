using System.IO;
namespace OpenTap.Plugins
{
    public interface IArtifactListener{
        void OnArtifactPublished(TestRun run, Stream s, string filename);
        void OnArtifactPublished(TestRun run, string filepath);
    }
}
