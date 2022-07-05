using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Authentication
{
    /// <summary>  This class stores information about the logged in client. </summary>
    [Browsable(false)]
    public class AuthenticationSettings : ComponentSettings<AuthenticationSettings>
    {
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
        /// Token store containing access, refresh and identity tokens.
        /// These tokens are used in the HttpClients returned by GetClient() to authenticate requests.
        /// </summary>
        public IList<TokenInfo> Tokens { get; set; } = new List<TokenInfo>();

        /// <summary> Parses tokens from OAuth response string (json format) and adds them to current Tokens list. </summary>
        public void AddTokensFromResponse(string response, string domain)
        {

            var json = JsonDocument.Parse(response);

            if (json.RootElement.TryGetProperty("access_token", out var accessTokenData))
                Tokens.Add(new TokenInfo(accessTokenData.GetString(), TokenType.AccessToken, domain));

            if (json.RootElement.TryGetProperty("refresh_token", out var refreshTokenData))
                Tokens.Add(new TokenInfo(refreshTokenData.GetString(), TokenType.RefreshToken, domain));

            if (json.RootElement.TryGetProperty("id_token", out var idTokenData))
                Tokens.Add(new TokenInfo(idTokenData.GetString(), TokenType.IdentityToken, domain));

        }

        /// <summary> Configuration used as BaseAddress in returned HttpClients.
        /// This string will be prepended to all relative urls, e.g. '/api/packages' will become '{BaseAddress}/api/packages'
        /// </summary>
        public string BaseAddress { get; set; } = "http://localhost";

        void PrepareRequest(HttpRequestMessage request, string domain, CancellationToken cancellationToken)
        {
            TokenInfo token = null;
            if (domain != null)
                token = Tokens.FirstOrDefault(t => t.Domain == domain && t.Type == TokenType.AccessToken);
            if (token == null)
                token = Tokens.FirstOrDefault(t => t.Domain == request.RequestUri.Host && t.Type == TokenType.AccessToken);
            if (token != null)
            {
                if (token.Expiration < DateTime.Now.AddSeconds(10))
                {
                    var rToken = Tokens.FirstOrDefault(t => t.Domain == domain && t.Type == TokenType.RefreshToken);
                    if (rToken != null)
                    {
                        // refresh
                    }
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.TokenData);
            }
        }

        /// <summary>
        /// Get preconfigured HttpClient with BaseAddress and AuthenticationClientHandler.
        /// It is up to the caller of this method to control the lifetime of the HttpClient
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="withRetryPolicy"></param>
        /// <returns>HttpClient object</returns>
        public HttpClient GetClient(string domain = null, bool withRetryPolicy = false)
        {
            return new HttpClient(new AuthenticationClientHandler(domain, withRetryPolicy)) { BaseAddress = new Uri(BaseAddress) };
        }
    }
}