using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace OpenTap
{
    /// <summary>
    /// A mechanism for retrieving <see cref="IPicture"/> data with <see cref="PictureDataProvider"/>
    /// </summary>
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
        public double Order => 10;

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
    

    /// <summary>
    /// Provide <see cref="IPicture"/> data from <see cref="IPictureDataProvider"/> implementations.
    /// </summary>
    public static class PictureDataProvider
    {
        private static IEnumerable<IPictureDataProvider> GetProviders()
        {
            return TypeData.GetDerivedTypes<IPictureDataProvider>().Where(td => td.CanCreateInstance)
                .Select(td => td.CreateInstance()).OfType<IPictureDataProvider>().OrderBy(p => p.Order);
        }
        

        private static TraceSource log = Log.CreateSource(nameof(PictureDataProvider));
        private static async Task<T> GetFirst<T>(IPicture picture, Func<IPicture, IPictureDataProvider, Task<T>> func)
        {
            foreach (var provider in GetProviders())
            {
                try
                {
                    var res = await func(picture, provider);
                    if (res != null)
                        return res;
                }
                catch (Exception ex)
                {
                    log.Debug($"Unexpected error in {nameof(IPictureDataProvider)} '{TypeData.GetTypeData(provider).AsTypeData().GetBestName()}'.");
                    log.Debug(ex);
                }
            }

            return default;
        }


        /// <summary>
        /// Get a stream of the picture from the first <see cref="IPictureDataProvider"/> which returns a non-null.
        /// </summary>
        /// <param name="picture"></param>
        public static Task<Stream> GetStream(IPicture picture) =>
            GetFirst(picture, (pic, provider) => provider.GetStream(pic)); 

        /// <summary>
        /// Get the format of the picture from the first <see cref="IPictureDataProvider"/> which returns a non-null.
        /// </summary>
        /// <param name="picture"></param>
        public static Task<string> GetPictureFormat(IPicture picture) => GetFirst(picture, (pic, provider) => provider.GetPictureFormat(pic));

        /// <summary>
        /// Get the name of the picture from the first <see cref="IPictureDataProvider"/> which returns a non-null.
        /// </summary>
        /// <param name="picture"></param>
        /// <returns></returns>

        public static Task<string> GetPictureName(IPicture picture) => GetFirst(picture, (pic, provider) => provider.GetPictureName(pic));
    }
}
