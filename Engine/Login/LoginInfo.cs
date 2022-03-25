using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Login
{
    /// <summary>  This class stores information about the logged in client. </summary>
    [Browsable(false)]
    public class LoginInfo : ComponentSettings<LoginInfo>
    {
        class AuthClientHandler : HttpClientHandler
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
        public static HttpClientHandler GetClientHandler() => new AuthClientHandler();

        /// <summary> Registers a refresh token.  </summary>
        /// <param name="site"></param>
        /// <param name="refreshToken"></param>
        /// <param name="expiration"></param>
        public void RegisterRefreshToken(string site, string refreshToken, DateTime expiration)
        {
            UnregisterRefreshToken(site);
            RefreshTokens.Add(new TokenInfo{Site = site, Expiration = expiration, Type = "Refresh", TokenData = refreshToken});
        }

        /// <summary> Registers an access token.  </summary>
        public void RegisterAccessToken(string site, string accessToken, DateTime expiration)
        {
            UnregisterAccessToken(site);
            AccessTokens.Add(new TokenInfo{Site = site, Expiration = expiration, Type = "Access", TokenData = accessToken});
        }

        /// <summary> Unregisters an access token.</summary>
        public void UnregisterAccessToken(string site)
        {
            AccessTokens.RemoveIf(x => x.Site == site);
        }
        /// <summary> Unregisters a refresh token.</summary>
        public void UnregisterRefreshToken(string site)
        {
            RefreshTokens.RemoveIf(x => x.Site == site);
        }

        /// <summary> Gets a valid access token matching the site. If the current token has expired, the refresh action will be used to refresh it. </summary>
        public TokenInfo GetValidAccessToken(string site, CancellationToken cancel)
        {
            if (AccessTokens.FirstOrDefault(x => x.Site == site) is TokenInfo access){
                // if the access token is about to expire, try refreshing it - if there is a refresh token available.
                if (access.Expiration < DateTime.Now.AddSeconds(5) &&
                    RefreshTokens.FirstOrDefault(x => x.Site == site) is TokenInfo refresh)
                {
                    var refresh2 = new RefreshTokenAction
                    {
                        Site = site,
                        Token = refresh
                    };
                    refresh2.Execute(cancel);
                }
            }

            return AccessTokens.FirstOrDefault(x => x.Site == site);
        }
    }
}