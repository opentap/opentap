using System.Net;

namespace OpenTap{
    class HttpUtils
    {
        public static bool TransientStatusCode(HttpStatusCode resultStatusCode)
        {
            //429: too may requests
            //5xx: server error
            int statusCode = (int)resultStatusCode;
            if (statusCode == 429 || (statusCode >= 500 && statusCode < 600))
                return true;
            return false;
        }
    }
}