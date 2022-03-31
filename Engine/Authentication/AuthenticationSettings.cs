using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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

            public AuthenticationClientHandler(string domain = null)
            {
                this.domain = domain;
            }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Current.PrepareRequest(request, domain, cancellationToken);
                return base.SendAsync(request, cancellationToken);
            }
        }

        /// <summary> Access tokens.</summary>
        public IList<TokenInfo> AccessTokens { get; set; } = new List<TokenInfo>();
        /// <summary> Refresh tokens.</summary>
        public IList<TokenInfo> RefreshTokens { get; set; } = new List<TokenInfo>();
        /// <summary> Identity tokens.</summary>
        public IList<TokenInfo> IdentityTokens { get; set; } = new List<TokenInfo>();

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
            //if (AccessTokens.FirstOrDefault(x => x.Domain == site) is TokenInfo access){
            //    // if the access token is about to expire, try refreshing it - if there is a refresh token available.
            //    if (access.Expiration < DateTime.Now &&
            //        RefreshTokens.FirstOrDefault(x => x.Domain == site) is TokenInfo refresh)
            //    {
            //        var refresh2 = new RefreshTokenAction
            //        {
            //            Domain = site,
            //            Token = refresh
            //        };
            //        refresh2.Execute(cancel);
            //    }
            //}
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
    }
}