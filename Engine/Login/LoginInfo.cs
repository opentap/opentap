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

        public IList<TokenInfo> AccessTokens { get; set; } = new List<TokenInfo>();
        public IList<TokenInfo> RefreshTokens { get; set; } = new List<TokenInfo>();

        void PrepareRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var host = request.RequestUri.Host;
            var token = GetValidAccessToken(host, cancellationToken);
            if(token != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.TokenData);
        }
        public static HttpClientHandler GetClientHandler() => new AuthClientHandler();

        public void RegisterRefreshToken(string site, string refreshToken, DateTime expiration)
        {
            UnregisterRefreshToken(site);
            RefreshTokens.Add(new TokenInfo{Site = site, Expiration = expiration, Type = "Refresh", TokenData = refreshToken});
        }

        public void RegisterAccessToken(string site, string accessToken, DateTime expiration)
        {
            UnregisterAccessToken(site);
            AccessTokens.Add(new TokenInfo{Site = site, Expiration = expiration, Type = "Access", TokenData = accessToken});
        }

        public void UnregisterAccessToken(string site)
        {
            AccessTokens.RemoveIf(x => x.Site == site);
        }
        
        public void UnregisterRefreshToken(string site)
        {
            RefreshTokens.RemoveIf(x => x.Site == site);
        }

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