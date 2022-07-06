using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Serialization;

namespace OpenTap.Authentication
{
    /// <summary> 
    /// Represents a set of Oauth2/OpenID Connect jwt tokens (access and possibly refresh token) that grants access to a given domain.
    /// </summary>
    public class TokenInfo
    {
        static DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Raw access token string. This value can be used as a Bearer token. The HttpClient 
        /// returned from <see cref="AuthenticationSettings.GetClient"/> will automatically do 
        /// this for requests that go to domains that match <see cref="Domain"/>.
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Raw refresh token string. May be null if no refresh token is available.
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary> 
        /// The site this token is intended for. Used by the HttpClient 
        /// returned from <see cref="AuthenticationSettings.GetClient"/> to determine which TokenInfo
        /// in the <see cref="AuthenticationSettings.Tokens"/> list to use for a given request.
        /// </summary>
        public string Domain { get; set; }

        private Dictionary<string, string> _Claims;
        /// <summary>
        /// Claims contained in the <see cref="AccessToken"/>.
        /// </summary>
        public IReadOnlyDictionary<string, string> Claims
        {
            get
            {
                if(_Claims == null)
                    _Claims = GetPayload().RootElement.EnumerateObject().ToDictionary(c => c.Name, c => c.Value.ToString());
                return _Claims;
            }
        }

        /// <summary> Expiration date of the <see cref="AccessToken"/>. </summary>
        [XmlIgnore]
        public DateTime Expiration => unixEpoch.AddSeconds(long.Parse(Claims["exp"]));

        JsonDocument payload;

        JsonDocument GetPayload()
        {
            if (payload != null) return payload;
            var payloadData = AccessToken.Split('.')[1];
            payload = JsonDocument.Parse(Base64UrlDecode(payloadData));
            return payload;
        }


        byte[] Base64UrlDecode(string encoded)
        {
            string substituded = encoded;
            substituded = substituded.Replace('-', '+');
            substituded = substituded.Replace('_', '/');
            while (substituded.Length % 4 != 0)
            {
                substituded += '=';
            }
            return Convert.FromBase64String(substituded);
        }

        /// <summary>
        /// Constructor used by serializer, please use constructor with arguments from user code.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public TokenInfo()
        {

        }

        /// <summary>
        /// Default constructor from user code
        /// </summary>
        /// <param name="access_token">The raw jwt token string for the access token</param>
        /// <param name="refresh_token">Access, Refresh or ID type</param>
        /// <param name="domain">Domain name for which this token is valid</param>
        public TokenInfo(string access_token, string refresh_token, string domain)
        {
            AccessToken = access_token;
            RefreshToken = refresh_token;
            Domain = domain;
        }

        /// <summary> Creates a TokenInfo object based on the given OAuth response (json format). </summary>
        public static TokenInfo FromResponse(string response, string domain)
        {
            var ti = new TokenInfo();
            ti.Domain = domain;
            var json = JsonDocument.Parse(response);

            if (json.RootElement.TryGetProperty("access_token", out var accessTokenData))
                ti.AccessToken = accessTokenData.GetString();

            if (json.RootElement.TryGetProperty("refresh_token", out var refreshTokenData))
                ti.RefreshToken = refreshTokenData.GetString();
            return ti;
        }

        public void Refresh(string client_id, string client_secret)
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, Claims["iss"] + "/.well-known/openid-configuration");
                HttpResponseMessage response = client.SendAsync(request).Result;
                var doc = JsonDocument.Parse(response.Content.ReadAsStreamAsync().Result);
                string tokenEndoint = doc.RootElement.GetProperty("token_endpoint").GetString();
                request = new HttpRequestMessage(HttpMethod.Post, tokenEndoint);
                var nvc = new List<KeyValuePair<string, string>>();
                nvc.Add(new KeyValuePair<string, string>("grant_type", "refresh_token"));
                nvc.Add(new KeyValuePair<string, string>("client_id", client_id));
                nvc.Add(new KeyValuePair<string, string>("client_secret", client_secret));
                nvc.Add(new KeyValuePair<string, string>("refresh_token", RefreshToken));
                request.Content = new FormUrlEncodedContent(nvc);
                response = client.SendAsync(request).Result;
                var rt= FromResponse(response.Content.ReadAsStringAsync().Result,Domain);
                this.AccessToken = rt.AccessToken;
                this.RefreshToken = rt.RefreshToken;
            }
        }

        public TokenInfo GetRequestingPartyToken(string audience)
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, Claims["iss"] + "/.well-known/openid-configuration");
                HttpResponseMessage response = client.SendAsync(request).Result;
                var doc = JsonDocument.Parse(response.Content.ReadAsStreamAsync().Result);
                string tokenEndoint = doc.RootElement.GetProperty("token_endpoint").GetString();
                request = new HttpRequestMessage(HttpMethod.Post, tokenEndoint);
                request.Headers.Add("Authorization", "Bearer " + AccessToken);
                var nvc = new List<KeyValuePair<string, string>>();
                nvc.Add(new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:uma-ticket"));
                nvc.Add(new KeyValuePair<string, string>("audience", audience));
                request.Content = new FormUrlEncodedContent(nvc);
                response = client.SendAsync(request).Result;
                return FromResponse(response.Content.ReadAsStringAsync().Result, Domain);
            }
        }
    }
}