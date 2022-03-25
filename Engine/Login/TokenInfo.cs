using System;

namespace OpenTap.Login
{
    /// <summary> Represents stored information about a token. </summary>
    public class TokenInfo
    {
        /// <summary> Raw token string. </summary>
        public string TokenData { get; set; }
        /// <summary> Expiration date of the token. </summary>
        public DateTime Expiration { get; set; }
        /// <summary> The site this token activates. </summary>
        public string Site { get; set; }
        /// <summary> The type of token. </summary>
        public string Type { get; set; }
        
        /// <summary> Gets if the token is expired.</summary>
        public bool Expired => Expiration < DateTime.Now;

        System.Text.Json.JsonDocument payload;

        System.Text.Json.JsonDocument GetPayload()
        {
            if (payload != null) return payload;
            var payloadData = TokenData.Split('.')[1];
            payload = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(payloadData));
            return payload;
        }
        /// <summary> Gets the client id. </summary>
        public string GetClientId()
        {
            if (GetPayload().RootElement.TryGetProperty("azp", out var id))
                return id.GetString();
            return null;
        }

        /// <summary> Gets the auth URL. </summary>
        public string GetAuthUrl()
        {
            if (GetPayload().RootElement.TryGetProperty("iss", out var id))
                return id.GetString();
            return null;
        }
    }
}