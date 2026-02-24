using System;
using System.Security.Cryptography;
using System.Text;

namespace OnlyOfficeControl.Helpers
{
    public static class OnlyOfficeJwt
    {
        public static string Create(string payloadJson, string secret)
        {
            if (payloadJson == null) payloadJson = "{}";
            if (string.IsNullOrWhiteSpace(secret)) secret = "secret";

            var headerJson = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
            var header = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
            var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
            var unsignedToken = header + "." + payload;

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedToken));
                var signature = Base64UrlEncode(signatureBytes);
                return unsignedToken + "." + signature;
            }
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            var s = Convert.ToBase64String(bytes);
            s = s.Split('=')[0];
            s = s.Replace('+', '-').Replace('/', '_');
            return s;
        }
    }
}
