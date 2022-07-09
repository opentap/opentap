using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Authentication
{
    /// <summary>  This class stores information about the logged in client. </summary>
    [Browsable(false)]
    public class AuthenticationSettings : ComponentSettings<AuthenticationSettings>
    {
        private string baseAddress = "";

        class AuthenticationClientHandler : HttpClientHandler
        {
            private readonly string domain;
            readonly bool withRetryPolicy;

            static readonly TimeSpan[] waits =
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
            };

            async Task<HttpResponseMessage> SendWithRetry(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                foreach (var wait in waits)
                {
                    await Task.Delay(wait, cancellationToken);
                    var result = await base.SendAsync(request, cancellationToken);
                    if (result.IsSuccessStatusCode == false && HttpUtils.TransientStatusCode(result.StatusCode) && wait != waits.Last())
                        continue;
                    return result;
                }

                // There is no chance of getting down here. result from SendAsync is never null.
                throw new InvalidOperationException();
            }




            public AuthenticationClientHandler(string domain = null, bool withRetryPolicy = false)
            {
                this.domain = domain;
                this.withRetryPolicy = withRetryPolicy;
            }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Current.PrepareRequest(request, domain, cancellationToken);
                if (withRetryPolicy)
                    return SendWithRetry(request, cancellationToken);
                return base.SendAsync(request, cancellationToken);
            }
        }

        /// <summary>
        /// Token store containing access and refresh tokens.
        /// These tokens are used in the HttpClients returned by <see cref="GetClient"/> to authenticate requests.
        /// </summary>
        public IList<TokenInfo> Tokens { get; set; } = new List<TokenInfo>();

        /// <summary> Configuration used as BaseAddress in HttpClients returned by <see cref="GetClient"/>.
        /// This string will be prepended to all relative urls, e.g. '/api/packages' will become '{BaseAddress}/api/packages'
        /// </summary>
        public string BaseAddress
        {
            get => baseAddress; set
            {
                if (String.IsNullOrEmpty(value) || Uri.IsWellFormedUriString(value, UriKind.Absolute))
                    baseAddress = value;
                else
                    throw new FormatException("BaseAddress must be a well formed absolute URI.");
            }
        }

        void PrepareRequest(HttpRequestMessage request, string domain, CancellationToken cancellationToken)
        {
            TokenInfo token = null;
            if (domain != null)
                token = Tokens.FirstOrDefault(t => t.Domain == domain);
            if (token == null)
                token = Tokens.FirstOrDefault(t => t.Domain == request.RequestUri.Host);
            if (token != null)
            {
                if (token.Expiration < DateTime.Now.AddSeconds(10))
                {
                    if (token.RefreshToken != null)
                    {
                        // TODO: refresh
                    }
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            }
        }

        private static string userAgent = null;

        /// <summary>
        /// Get preconfigured HttpClient with BaseAddress and AuthenticationClientHandler.
        /// It is up to the caller of this method to control the lifetime of the HttpClient
        /// </summary>
        /// <param name="domain">An access token will attempt to be included which are valid against this domain.</param>
        /// <param name="withRetryPolicy">If the request should be retried in case of transient errors.</param>
        /// <returns>HttpClient object</returns>
        public HttpClient GetClient(string domain = null, bool withRetryPolicy = false)
        {
            var client = new HttpClient(new AuthenticationClientHandler(domain, withRetryPolicy));
            if (!String.IsNullOrEmpty(BaseAddress))
                client.BaseAddress = new Uri(BaseAddress, UriKind.Absolute);
            if(userAgent == null)
            {
                //var sw = System.Diagnostics.Stopwatch.StartNew();
                userAgent = $"OpenTAP/{PluginManager.GetOpenTapAssembly().SemanticVersion}";
                if(Cli.CliActionExecutor.SelectedAction is ITypeData td)
                {
                    // We are running a CLI Action. Add it's name and version to the User-Agent header
                    var source = TypeData.GetTypeDataSource(td);
                    userAgent += $" {td.Name}/{source.Version}";
                }
                else
                {
                    var asm = Assembly.GetEntryAssembly();
                    if (asm != null)
                    {
                        var assemblyData = PluginManager.GetSearcher().Assemblies.FirstOrDefault(ad => ad.Location == asm.Location);
                        if (assemblyData?.SemanticVersion is SemanticVersion ver)
                        {
                            // The process was started from an assembly that we know ablout and that has a semantic version number. Add it's name and version to the User-Agent header
                            userAgent += $" {assemblyData.Name}/{ver}";
                        }
                    }
                }
                //var log = Log.CreateSource("AuthSettings");
                //log.Debug(sw, "Setting User-Agent to '{0}'", userAgent);
            }
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            return client;
        }
    }
}