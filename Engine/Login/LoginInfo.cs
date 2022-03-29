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
    public class LoginInfo : ComponentSettings<LoginInfo>
    {
        class AuthenticationClientHandler : HttpClientHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Current.PrepareRequest(request, cancellationToken);
                return base.SendAsync(request, cancellationToken);
            }
        }

        /// <summary> Access tokens. These expires within a few minutes, but can be refreshed using the refresh action. </summary>
        public IList<TokenInfo> AccessTokens { get; set; } = new List<TokenInfo>();
        /// <summary> Refresh tokens. These expires within a few hours, but can be refreshed using the refresh action. </summary>
        public IList<TokenInfo> RefreshTokens { get; set; } = new List<TokenInfo>();

        void PrepareRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var host = request.RequestUri.Host;
            var token = GetValidAccessToken(host, cancellationToken);
            if(token != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.TokenData);
        }
        
        /// <summary> Constructs a HttpClientHandler that can be used with HttpClient. </summary>
        public static HttpClientHandler GetClientHandler() => new AuthenticationClientHandler();

        /// <summary> Registers a refresh token.  </summary>
        /// <param name="site"></param>
        /// <param name="refreshToken"></param>
        /// <param name="expiration"></param>
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

        /// <summary> Unregisters an access token.</summary>
        public void UnregisterAccessToken(string site)
        {
            AccessTokens.RemoveIf(x => x.Domain == site);
        }
        /// <summary> Unregisters a refresh token.</summary>
        public void UnregisterRefreshToken(string site)
        {
            RefreshTokens.RemoveIf(x => x.Domain == site);
        }

        /// <summary> Gets a valid access token matching the site. If the current token has expired, the refresh action will be used to refresh it. </summary>
        public TokenInfo GetValidAccessToken(string site, CancellationToken cancel)
        {
            if (AccessTokens.FirstOrDefault(x => x.Domain == site) is TokenInfo access){
                // if the access token is about to expire, try refreshing it - if there is a refresh token available.
                if (access.Expiration < DateTime.Now &&
                    RefreshTokens.FirstOrDefault(x => x.Domain == site) is TokenInfo refresh)
                {
                    var refresh2 = new RefreshTokenAction
                    {
                        Domain = site,
                        Token = refresh
                    };
                    refresh2.Execute(cancel);
                }
            }

            return AccessTokens.FirstOrDefault(x => x.Domain == site);
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
                        throw new NotSupportedException("Identity token is not yet supported.");
                    default:
                        throw new ArgumentOutOfRangeException();
                }   
            }
        }
    }
}