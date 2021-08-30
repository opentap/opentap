using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace OpenTap
{
    public interface IPictureDataProvider
    {
        /// <summary>
        /// The order in which IPictureDataProviders will be tested. Lowers numbers go first
        /// </summary>
        double Order { get; } 
        /// <summary>
        /// Get a stream of the picture data
        /// </summary>
        /// <param name="picture"></param>
        /// <returns></returns>
        Task<Stream> GetStream(IPicture picture); 
        /// <summary>
        /// Get a string specifying the picture format
        /// </summary>
        /// <param name="picture"></param>
        /// <returns></returns>
        Task<string> GetPictureFormat(IPicture picture);
        /// <summary>
        /// Get the name of the picture
        /// </summary>
        /// <param name="picture"></param>
        /// <returns></returns>
        Task<string> GetPictureName(IPicture picture);
    }
    
    /// <summary>
    /// Picture data provider for URI sources.
    /// </summary>
    class DefaultPictureDataProvider : IPictureDataProvider
    {
        public double Order => 9;

        public Task<Stream> GetStream(IPicture picture)
        {
            if (Uri.TryCreate(picture.Source, UriKind.RelativeOrAbsolute, out var uri))
            {
                var req = WebRequest.Create(uri);
                return req.GetResponseAsync().ContinueWith(t => t.Result.GetResponseStream());
            }

            return null;
        }

        public Task<string> GetPictureFormat(IPicture picture)
        {
            if (Uri.TryCreate(picture.Source, UriKind.RelativeOrAbsolute, out var uri))
            {
                var name = uri.Segments.LastOrDefault();
                if (string.IsNullOrWhiteSpace(name) == false)
                    return Task.FromResult(Path.GetExtension(name).Substring(1));
            }

            return null;
        }

        public Task<string> GetPictureName(IPicture picture)
        {
            if (Uri.TryCreate(picture.Source, UriKind.RelativeOrAbsolute, out var uri))
            {
                var name = uri.Segments.LastOrDefault();
                if (string.IsNullOrWhiteSpace(name) == false)
                    return Task.FromResult(Path.GetFileNameWithoutExtension(name));
            }

            return null;
        }
    }

    public static class PictureDataProvider
    {
        private static IEnumerable<IPictureDataProvider> GetProviders()
        {
            return TypeData.GetDerivedTypes<IPictureDataProvider>().Where(td => td.CanCreateInstance)
                .Select(td => td.CreateInstance()).OfType<IPictureDataProvider>().OrderBy(p => p.Order);
        }

        private static async Task<T> GetFirstNonDefault<T>(string name, IPicture picture)
        {
            var method = typeof(IPictureDataProvider).GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            if (method == null)
                throw new Exception();

            foreach (var provider in GetProviders())
            {
                try
                {
                    var result = await (Task<T>)method.Invoke(provider, new object[] {picture});

                    if (result != null)
                        return result;
                }
                catch
                {
                    // Continue
                }
            }

            return default;
        }

        public static Task<Stream> GetStream(IPicture picture) => GetFirstNonDefault<Stream>(nameof(IPictureDataProvider.GetStream), picture);

        public static Task<string> GetPictureFormat(IPicture picture) => GetFirstNonDefault<string>(nameof(IPictureDataProvider.GetPictureFormat), picture);

        public static Task<string> GetPictureName(IPicture picture) => GetFirstNonDefault<string>(nameof(IPictureDataProvider.GetPictureName), picture);
    }
}
