using System;
using System.Text.Json;
using System.Xml.Serialization;

namespace OpenTap.Authentication
{
    /// <summary> Represents stored information about a token. </summary>
    public class TokenInfo
    {
        /// <summary>Raw token string. This value can be used as a Bearer token if <see cref="Type"/> is <see cref="TokenType.AccessToken"/></summary>
        public string TokenData { get; set; }

        static DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary> Expiration date of the token. </summary>
        [XmlIgnore]
        public DateTime Expiration => unixEpoch.AddSeconds(long.Parse(GetClaim("exp")));

        /// <summary> The site this token belongs to. </summary>
        public string Domain { get; set; }
        /// <summary> The type of token. </summary>
        public TokenType Type { get; set; }

        JsonDocument payload;

        JsonDocument GetPayload()
        {
            if (payload != null) return payload;
            var payloadData = TokenData.Split('.')[1];
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
        /// Get a claim from the JWT payload
        /// </summary>
        /// <param name="claim">Name of the claim, e.g. 'sub'</param>
        /// <returns>Claim value or null if claim does not exist</returns>
        public string GetClaim(string claim)
        {
            if (GetPayload().RootElement.TryGetProperty(claim, out var id))
                return id.GetRawText();
            return null;
        }

        /// <summary>
        /// Serializable constructur
        /// </summary>
        public TokenInfo()
        {

        }

        /// <summary>
        /// Default constructor from user code
        /// </summary>
        /// <param name="jwtString">Token Data</param>
        /// <param name="type">Access, Refresh or ID type</param>
        /// <param name="domain">Domain name for which this token is valid</param>
        public TokenInfo(string jwtString, TokenType type, string domain)
        {
            TokenData = jwtString;
            Type = type;
            Domain = domain;
        }

    }
}