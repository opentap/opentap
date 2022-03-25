using System;

namespace OpenTap.Login
{
    public class TokenInfo
    {
        public string TokenData { get; set; }
        public DateTime Expiration { get; set; }
        public string Site { get; set; }
        public string Type { get; set; }
        public bool Expired => Expiration < DateTime.Now;

        private System.Text.Json.JsonDocument payload;

        System.Text.Json.JsonDocument GetPayload()
        {
            if (payload != null) return payload;
            var payloadData = TokenData.Split('.')[1];
            payload = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(payloadData));
            return payload;
        }
        public string GetClientId()
        {
            if (GetPayload().RootElement.TryGetProperty("azp", out var id))
                return id.GetString();
            return null;
        }

        public string GetAuthUrl()
        {
            if (GetPayload().RootElement.TryGetProperty("iss", out var id))
                return id.GetString();
            return null;
        }
    }
}