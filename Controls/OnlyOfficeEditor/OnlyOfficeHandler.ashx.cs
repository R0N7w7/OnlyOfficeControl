using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;

namespace OnlyOfficeControl.Controls.OnlyOfficeEditorBundle
{
    public class OnlyOfficeHandler : IHttpHandler
    {
        public bool IsReusable => false;

        public void ProcessRequest(HttpContext context)
        {
            var action = (context.Request["action"] ?? string.Empty).ToLowerInvariant();
            switch (action)
            {
                case "download":
                    Download(context);
                    break;
                case "callback":
                    Callback(context);
                    break;
                case "proxy":
                    Proxy(context);
                    break;
                default:
                    context.Response.StatusCode = 400;
                    context.Response.ContentType = "text/plain";
                    context.Response.Write("Invalid action");
                    break;
            }
        }

        private static string UploadsPath(HttpContext ctx)
        {
            var path = ctx.Server.MapPath("~/App_Data/uploads");
            Directory.CreateDirectory(path);
            return path;
        }

        private static string ResolveStoredFile(string uploadsDir, string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId)) return null;
            var matches = Directory.GetFiles(uploadsDir, fileId + ".*");
            if (matches.Length > 0) return matches[0];
            return null;
        }

        private static void Download(HttpContext context)
        {
            var fileId = context.Request["fileId"];
            var uploads = UploadsPath(context);
            var path = ResolveStoredFile(uploads, fileId);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                context.Response.StatusCode = 404;
                context.Response.Write("File not found");
                return;
            }

            var fileName = Path.GetFileName(path);
            context.Response.Clear();
            context.Response.ContentType = "application/octet-stream";
            context.Response.AddHeader("Content-Disposition", "attachment; filename=" + fileName);
            context.Response.WriteFile(path);
            context.Response.End();
        }

        private static void Callback(HttpContext context)
        {
            string body;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }

            var serializer = new JavaScriptSerializer();
            var responseObj = new { error = 0 };

            try
            {
                var payload = serializer.Deserialize<OnlyOfficeCallbackPayload>(body);
                var fileId = context.Request["fileId"];
                var uploads = UploadsPath(context);
                var currentPath = ResolveStoredFile(uploads, fileId);

                if (payload != null && (payload.status == 2 || payload.status == 6) && !string.IsNullOrWhiteSpace(payload.url) && !string.IsNullOrWhiteSpace(currentPath))
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                    var req = (HttpWebRequest)WebRequest.Create(payload.url);
                    req.Method = "GET";
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var stream = resp.GetResponseStream())
                    using (var fs = File.Create(currentPath))
                    {
                        stream.CopyTo(fs);
                    }
                }
            }
            catch
            {
                responseObj = new { error = 1 };
            }

            context.Response.ContentType = "application/json";
            context.Response.Write(serializer.Serialize(responseObj));
        }

        private static void Proxy(HttpContext context)
        {
            var url = context.Request["url"];
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                context.Response.StatusCode = 400;
                context.Response.Write("Invalid URL");
                return;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                context.Response.StatusCode = 400;
                context.Response.Write("Unsupported URL scheme");
                return;
            }

            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            var req = (HttpWebRequest)WebRequest.Create(uri);
            req.Method = "GET";

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var stream = resp.GetResponseStream())
            {
                context.Response.Clear();
                context.Response.ContentType = string.IsNullOrWhiteSpace(resp.ContentType)
                    ? "application/octet-stream"
                    : resp.ContentType;

                var contentDisposition = resp.Headers["Content-Disposition"];
                if (!string.IsNullOrWhiteSpace(contentDisposition))
                    context.Response.AddHeader("Content-Disposition", contentDisposition);

                stream.CopyTo(context.Response.OutputStream);
                context.Response.Flush();
            }
        }

        private class OnlyOfficeCallbackPayload
        {
            public int status { get; set; }
            public string url { get; set; }
        }
    }
}
