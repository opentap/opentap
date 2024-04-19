using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace OpenTap
{
    /// <summary>
    /// A mechanism for retrieving <see cref="IPicture"/> data with <see cref="PictureDataExtensions"/>
    /// </summary>
    internal interface IPictureDataProvider
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
        Task<string> GetFormat(IPicture picture);
    }
    
    /// <summary>
    /// Picture data provider for URI sources.
    /// </summary>
    class DefaultPictureDataProvider : IPictureDataProvider
    {
        public double Order => 10;

        /// <summary>
        /// Relative URIs are poorly supported in dotnet core. Ensure we only use absolute URIs by normalizing path strings to absolute paths.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private string normalizeSource(string source)
        {
            try
            {
                if (File.Exists(source))
                    return Path.GetFullPath(source);
            }
            catch
            {
                // this is fine -- source is not a file
            }

            return source;
        }

        public async Task<Stream> GetStream(IPicture picture)
        {
            if (await GetFormat(picture) == null) return null;
            var source = normalizeSource(picture.Source);
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
            {
#pragma warning disable SYSLIB0014
                var req = WebRequest.Create(uri);
#pragma warning restore SYSLIB0014
                var response = await req.GetResponseAsync();
                return response.GetResponseStream();
            }

            return null;
        }

        public Task<string> GetFormat(IPicture picture)
        {
            var source = normalizeSource(picture.Source);
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
            {
                var name = uri.Segments.LastOrDefault();
                if (string.IsNullOrWhiteSpace(name) == false)
                {
                    var ext = Path.GetExtension(name);
                    if (ext.StartsWith(".") && ext.Length > 1) return Task.FromResult(ext.Substring(1));
                }
            }

            return Task.FromResult<string>(null);
        }
    }
    

    /// <summary>
    /// Provide <see cref="IPicture"/> data from <see cref="IPictureDataProvider"/> implementations.
    /// </summary>
    public static class PictureDataExtensions
    {
        private static IPictureDataProvider[] cache = Array.Empty<IPictureDataProvider>();
        private static ISet<ITypeData> cacheKey;

        private static IEnumerable<IPictureDataProvider> GetProviders()
        {
            var types = TypeData.GetDerivedTypes<IPictureDataProvider>()
                .Where(td => td.CanCreateInstance).ToImmutableHashSet();
            
            if (cacheKey == null || types.SetEquals(cacheKey) == false)
            {
                cache = types.TrySelect(td => td.CreateInstance(), ex => log.Debug("Unable to load IPictureDataProvider: {0}", ex.Message))
                    .OfType<IPictureDataProvider>().OrderBy(p => p.Order)
                    .ToArray();
                cacheKey = types;
            }

            return cache;
        }

        private static TraceSource log = Log.CreateSource(nameof(PictureDataExtensions));
        private static async Task<T> GetFirst<T>(IPicture picture, Func<IPicture, IPictureDataProvider, Task<T>> func) where T : class
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

            return null;
        }


        /// <summary>
        /// Get a stream of the picture from the first <see cref="IPictureDataProvider"/> which returns a non-null.
        /// </summary>
        /// <param name="picture"></param>
        public static Task<Stream> GetStream(this IPicture picture) =>
            GetFirst(picture, (pic, provider) => provider.GetStream(pic)); 

        /// <summary>
        /// Get the format of the picture from the first <see cref="IPictureDataProvider"/> which returns a non-null.
        /// </summary>
        /// <param name="picture"></param>
        public static Task<string> GetFormat(this IPicture picture) => GetFirst(picture, (pic, provider) => provider.GetFormat(pic));
    }
}
