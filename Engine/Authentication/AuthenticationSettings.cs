using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
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

        /// <summary> Access tokens.</summary>
        public IList<TokenInfo> AccessTokens { get; set; } = new List<TokenInfo>();
        /// <summary> Refresh tokens.</summary>
        public IList<TokenInfo> RefreshTokens { get; set; } = new List<TokenInfo>();
        /// <summary> Identity tokens.</summary>
        public IList<TokenInfo> IdentityTokens { get; set; } = new List<TokenInfo>();

        private string baseAddress { get; set; } = "http://localhost";
        /// <summary> Configuration which is used as BaseAddress in the GetClient() returned HttpClient.</summary>
        public string BaseAddress
        {
            get { return baseAddress; }
            set
            {
                baseAddress = value;
                List<WeakReference> garbageCollected = null;
                foreach (var client in clientReferences)
                {
                    if (client.Target is AuthenticationClient authClient && !authClient.IsDisposed)
                    {
                        try
                        {
                            authClient.BaseAddress = new Uri(value);
                        }
                        catch (ObjectDisposedException)
                        {
                            // Catch disposed exception in case of a race condition between the `IsAlive` check and actually fetching the Target in the try clause. Next call will return !IsAlive and list will be updated accordingly from en else clause.
                        }
                    }
                    else
                    {
                        if (garbageCollected == null)
                            garbageCollected = new List<WeakReference>();
                        garbageCollected.Add(client);
                    }
                }

                if (garbageCollected is object)
                    foreach (var client in garbageCollected)
                        clientReferences.Remove(client);
            }
        }


        void PrepareRequest(HttpRequestMessage request, string domain, CancellationToken cancellationToken)
        {
            TokenInfo token = null;
            if (domain != null)
                token = GetValidAccessToken(domain, cancellationToken);
            if (token == null)
                token = GetValidAccessToken(request.RequestUri.Host, cancellationToken);
            if (token != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.TokenData);
        }

        /// <summary> Constructs a HttpClientHandler that can be used with HttpClient. </summary>
        public static HttpClientHandler GetClientHandler(string domain = null) => new AuthenticationClientHandler(domain);

        /// <summary> Constructs a HttpClientHandler that can be used with HttpClient. </summary>
        public static HttpClientHandler GetClientHandleWithRetryPolicy(string domain = null) => new AuthenticationClientHandler(domain, withRetryPolicy: true);

        /// <summary> Registers a refresh token.  </summary>
        void RegisterRefreshToken(TokenInfo token)
        {
            UnregisterRefreshToken(token.Domain);
            RefreshTokens.Add(token);
        }

        /// <summary> Registers an access token.  </summary>
        void RegisterAccessToken(TokenInfo token)
        {
            UnregisterAccessToken(token.Domain);
            AccessTokens.Add(token);
        }

        void RegisterIdentityToken(TokenInfo token)
        {
            UnregisterIdentityToken(token.Domain);
            IdentityTokens.Add(token);
        }

        private void UnregisterIdentityToken(string domain)
        {
            IdentityTokens.RemoveIf(x => x.Domain == domain);
        }

        /// <summary> Unregisters an access token.</summary>
        public void UnregisterAccessToken(string domain)
        {
            AccessTokens.RemoveIf(x => x.Domain == domain);
        }
        /// <summary> Unregisters a refresh token.</summary>
        public void UnregisterRefreshToken(string domain)
        {
            RefreshTokens.RemoveIf(x => x.Domain == domain);
        }

        /// <summary> Gets a valid access token matching the site. If the current token has expired, the refresh action will be used to refresh it. </summary>
        public TokenInfo GetValidAccessToken(string domain, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            // Try refresh

            return AccessTokens.FirstOrDefault(x => x.Domain == domain);
        }

        /// <summary> Registers a set of tokens.</summary>
        /// <param name="tokens"></param>
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void RegisterTokens(params TokenInfo[] tokens)
        {
            foreach (var token in tokens)
            {
                switch (token.Type)
                {
                    case TokenType.AccessToken:
                        RegisterAccessToken(token);
                        break;
                    case TokenType.RefreshToken:
                        RegisterRefreshToken(token);
                        break;
                    case TokenType.IdentityToken:
                        RegisterIdentityToken(token);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        internal List<WeakReference> clientReferences = new List<WeakReference>();

        /// <summary>
        /// Get preconfigured HttpClient with BaseAddress and AuthenticationClientHandler.
        /// It is up to the caller of this method to control the lifetime of the HttpClient
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="withRetryPolicy"></param>
        /// <returns>HttpClient object</returns>
        public HttpClient GetClient(string domain = null, bool withRetryPolicy = false)
        {
            HttpClient client = new AuthenticationClient(new AuthenticationClientHandler(domain, withRetryPolicy)) { BaseAddress = new Uri(BaseAddress) };
            clientReferences.Add(new WeakReference(client));
            return client;
        }

        class AuthenticationClient : HttpClient
        {
            public AuthenticationClient(HttpMessageHandler handler) : base(handler)
            {
            }

            public bool IsDisposed { get; set; } = false;
            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }
    }
}