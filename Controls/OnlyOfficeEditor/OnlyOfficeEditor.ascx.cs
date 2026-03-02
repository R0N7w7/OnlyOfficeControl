using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace OnlyOfficeControl.Controls.OnlyOfficeEditorBundle
{
    public partial class OnlyOfficeEditor : UserControl
    {
        public string DocumentUrl
        {
            get => (string)ViewState["DocumentUrl"];
            set => ViewState["DocumentUrl"] = value;
        }

        public string DocumentName
        {
            get => (string)ViewState["DocumentName"];
            set => ViewState["DocumentName"] = value;
        }

        public string DocumentKey
        {
            get => (string)ViewState["DocumentKey"];
            set => ViewState["DocumentKey"] = value;
        }

        public string CallbackUrl
        {
            get => (string)ViewState["CallbackUrl"];
            set => ViewState["CallbackUrl"] = value;
        }

        // ==================================================================================
        // Configuraci�n de rutas y secretos - ajustar seg�n el entorno y necesidades

        // URL del API de OnlyOffice (Servidor de documentos)
        public string OnlyOfficeApiUrl { get; set; } = "https://doclinea.pjhidalgo.gob.mx:4443/web-apps/apps/api/documents/api.js";

        // Clave secreta para JWT - debe coincidir con la configurada en el servidor de OnlyOffice
        // public string JwtSecret { get; set; } = "secreto_personalizado";
        public string JwtSecret { get; set; } = "JGbxwFgrXgMcMrknjdxI";

        public string PublicBaseUrl { get; set; } = "https://192.168.10.34:44311";

        // ==================================================================================

        public string Mode { get; set; } = "edit";

        public string Lang { get; set; } = "es";

        public string EditorHeight { get; set; } = "520px";

        public string UserId { get; set; } = "1";

        public string UserDisplayName { get; set; } = "Usuario";

        public string CaptureTriggerId { get; set; }

        public string EditorContainerId => ClientID + "_editor";

        public string ConfigJson { get; private set; } = "null";

        public bool HasDocument =>
            !string.IsNullOrWhiteSpace(DocumentUrl)
            && !string.IsNullOrWhiteSpace(DocumentName)
            && !string.IsNullOrWhiteSpace(DocumentKey);

        public string HiddenFieldClientId => hfEditedDocumentBase64.ClientID;

        public bool HasEditedDocument =>
            !string.IsNullOrWhiteSpace(hfEditedDocumentBase64.Value);

        public byte[] GetEditedDocumentBytes()
        {
            var b64 = hfEditedDocumentBase64.Value;
            if (string.IsNullOrWhiteSpace(b64)) return null;
            try { return Convert.FromBase64String(b64); }
            catch { return null; }
        }

        public void ClearEditedDocument()
        {
            hfEditedDocumentBase64.Value = string.Empty;
        }

        public void SetDocumentFromBytes(byte[] data, string fileName)
        {
            if (data == null || data.Length == 0 || string.IsNullOrWhiteSpace(fileName))
                return;

            var fileId = Guid.NewGuid().ToString("N");
            var ext = Path.GetExtension(fileName);
            var storedName = fileId + ext;
            var uploadsDir = HttpContext.Current.Server.MapPath("~/App_Data/uploads");
            Directory.CreateDirectory(uploadsDir);
            File.WriteAllBytes(Path.Combine(uploadsDir, storedName), data);

            DocumentName = Path.GetFileName(fileName);
            DocumentKey = GenerateDocumentKey(fileId);
            DocumentUrl = BuildAbsoluteUrl(
                "~/Controls/OnlyOfficeEditor/OnlyOfficeHandler.ashx?action=download&fileId=" + HttpUtility.UrlEncode(fileId));
            CallbackUrl = BuildAbsoluteUrl(
                "~/Controls/OnlyOfficeEditor/OnlyOfficeHandler.ashx?action=callback&fileId=" + HttpUtility.UrlEncode(fileId));
        }

        public void SetDocumentFromFile(string serverFilePath, string displayName = null)
        {
            if (!File.Exists(serverFilePath)) return;
            SetDocumentFromBytes(
                File.ReadAllBytes(serverFilePath),
                displayName ?? Path.GetFileName(serverFilePath));
        }

        public void SetDocumentFromUpload(string fileId, string originalName)
        {
            if (string.IsNullOrWhiteSpace(fileId)) return;

            DocumentName = originalName ?? fileId;
            DocumentKey = GenerateDocumentKey(fileId);
            DocumentUrl = BuildAbsoluteUrl(
                "~/Controls/OnlyOfficeEditor/OnlyOfficeHandler.ashx?action=download&fileId=" + HttpUtility.UrlEncode(fileId));
            CallbackUrl = BuildAbsoluteUrl(
                "~/Controls/OnlyOfficeEditor/OnlyOfficeHandler.ashx?action=callback&fileId=" + HttpUtility.UrlEncode(fileId));
        }

        public string ConvertCurrentDocumentToPdfUrl(int maxAttempts = 15, int delayMs = 1000)
        {
            if (!HasDocument)
                throw new InvalidOperationException("No hay un documento cargado para convertir.");

            if (string.IsNullOrWhiteSpace(DocumentUrl) || !Uri.TryCreate(DocumentUrl, UriKind.Absolute, out _))
                throw new InvalidOperationException("La URL del documento no es válida para la conversión.");

            var sourceExt = Path.GetExtension(DocumentName)?.TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(sourceExt))
                throw new InvalidOperationException("No fue posible determinar el tipo del documento.");

            var convertServiceUrl = ResolveConvertServiceUrl();
            var serializer = new JavaScriptSerializer();
            var attempts = Math.Max(1, maxAttempts);
            var wait = Math.Max(0, delayMs);

            for (var i = 0; i < attempts; i++)
            {
                var requestPayload = new Dictionary<string, object>
                {
                    ["async"] = false,
                    ["filetype"] = sourceExt,
                    ["outputtype"] = "pdf",
                    ["url"] = DocumentUrl,
                    ["title"] = Path.GetFileName(DocumentName),
                    ["key"] = DocumentKey
                };

                var payloadJson = serializer.Serialize(requestPayload);
                var token = OnlyOfficeJwt.Create(payloadJson, JwtSecret);

                requestPayload["token"] = token;
                var body = serializer.Serialize(requestPayload);

                var result = CallConvertService(convertServiceUrl, body, token);
                if (result.EndConvert && !string.IsNullOrWhiteSpace(result.FileUrl))
                    return result.FileUrl;

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                    throw new InvalidOperationException(result.ErrorMessage);

                if (i < attempts - 1 && wait > 0)
                    Thread.Sleep(wait);
            }

            throw new TimeoutException("La conversión a PDF no terminó dentro del tiempo esperado.");
        }

        public byte[] ConvertCurrentDocumentToPdfBytes(int maxAttempts = 15, int delayMs = 1000)
        {
            var pdfUrl = ConvertCurrentDocumentToPdfUrl(maxAttempts, delayMs);
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            var req = (HttpWebRequest)WebRequest.Create(pdfUrl);
            req.Method = "GET";

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var stream = resp.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected void Page_PreRender(object sender, EventArgs e)
        {
            ConfigJson = HasDocument ? BuildConfigJson() : "null";
            RegisterTriggerScripts();
        }

        private string BuildConfigJson()
        {
            var ext = Path.GetExtension(DocumentName);
            var fileType = string.IsNullOrWhiteSpace(ext) ? "" : ext.TrimStart('.');

            var config = new
            {
                document = new
                {
                    fileType,
                    key = DocumentKey,
                    title = DocumentName,
                    url = DocumentUrl
                },
                documentType = ResolveDocumentType(fileType),
                editorConfig = new
                {
                    callbackUrl = CallbackUrl ?? "",
                    mode = Mode ?? "edit",
                    lang = Lang ?? "es",
                    user = new { id = UserId ?? "1", name = UserDisplayName ?? "Usuario" }
                }
            };

            var serializer = new JavaScriptSerializer();
            var json = serializer.Serialize(config);
            var token = OnlyOfficeJwt.Create(json, JwtSecret);

            return "{\"token\":" + serializer.Serialize(token)
                + ",\"document\":" + serializer.Serialize(config.document)
                + ",\"documentType\":" + serializer.Serialize(config.documentType)
                + ",\"editorConfig\":" + serializer.Serialize(config.editorConfig)
                + "}";
        }

        private string ResolveConvertServiceUrl()
        {
            if (string.IsNullOrWhiteSpace(OnlyOfficeApiUrl) || !Uri.TryCreate(OnlyOfficeApiUrl, UriKind.Absolute, out var apiUri))
                throw new InvalidOperationException("OnlyOfficeApiUrl no está configurada correctamente.");

            var root = apiUri.GetLeftPart(UriPartial.Authority);
            return root.TrimEnd('/') + "/ConvertService.ashx";
        }

        private static ConvertServiceResult CallConvertService(string convertServiceUrl, string body, string token)
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            var req = (HttpWebRequest)WebRequest.Create(convertServiceUrl);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Accept = "application/json";
            req.Headers["Authorization"] = "Bearer " + token;

            var bytes = Encoding.UTF8.GetBytes(body);
            using (var reqStream = req.GetRequestStream())
            {
                reqStream.Write(bytes, 0, bytes.Length);
            }

            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var stream = resp.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    var json = reader.ReadToEnd();
                    return ParseConvertServiceResult(json);
                }
            }
            catch (WebException ex)
            {
                var message = "Error al invocar ConvertService.ashx";
                if (ex.Response != null)
                {
                    using (var stream = ex.Response.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var responseBody = reader.ReadToEnd();
                        if (!string.IsNullOrWhiteSpace(responseBody))
                            message += ": " + responseBody;
                    }
                }
                return new ConvertServiceResult { ErrorMessage = message };
            }
        }

        private static ConvertServiceResult ParseConvertServiceResult(string json)
        {
            var result = new ConvertServiceResult();
            if (string.IsNullOrWhiteSpace(json))
            {
                result.ErrorMessage = "La respuesta del servicio de conversión llegó vacía.";
                return result;
            }

            var serializer = new JavaScriptSerializer();
            var payload = serializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();

            if (payload.TryGetValue("fileUrl", out var fileUrlObj))
                result.FileUrl = fileUrlObj as string;

            if (payload.TryGetValue("endConvert", out var endConvertObj))
            {
                try { result.EndConvert = Convert.ToBoolean(endConvertObj); }
                catch { result.EndConvert = false; }
            }

            if (payload.TryGetValue("error", out var errorObj) && errorObj != null)
            {
                var errorRaw = Convert.ToString(errorObj);
                if (!string.IsNullOrWhiteSpace(errorRaw) && errorRaw != "0")
                    result.ErrorMessage = "ConvertService devolvió error=" + errorRaw + ".";
            }

            if (string.IsNullOrWhiteSpace(result.ErrorMessage)
                && !result.EndConvert
                && payload.TryGetValue("percent", out var percentObj)
                && percentObj != null)
            {
                result.ErrorMessage = null;
            }

            return result;
        }

        private string BuildAbsoluteUrl(string virtualPath)
        {
            var resolved = ResolveUrl(virtualPath);
            if (!string.IsNullOrWhiteSpace(PublicBaseUrl))
            {
                var baseUri = new Uri(PublicBaseUrl.TrimEnd('/') + "/");
                var rel = resolved.StartsWith("~") ? resolved.Substring(1) : resolved;
                return new Uri(baseUri, rel.TrimStart('/')).ToString();
            }
            return new Uri(Page.Request.Url, resolved).ToString();
        }

        private static string GenerateDocumentKey(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId))
                return Guid.NewGuid().ToString("N");
            var c = fileId.Replace("-", "");
            if (c.Length < 24) c = c.PadRight(24, '0');
            return c.Substring(0, 20) + "_" + c.Substring(c.Length - 4);
        }

        private static string ResolveDocumentType(string fileType)
        {
            switch ((fileType ?? "").ToLowerInvariant())
            {
                case "xls":
                case "xlsx":
                case "ods":
                case "csv":
                    return "cell";
                case "ppt":
                case "pptx":
                case "odp":
                    return "slide";
                default:
                    return "word";
            }
        }

        private void RegisterTriggerScripts()
        {
            if (!HasDocument) return;

            var cs = Page.ClientScript;
            var type = GetType();
            var uid = ClientID;

            var apiKey = "oo_api_script_" + uid;
            if (!cs.IsClientScriptIncludeRegistered(type, apiKey))
                cs.RegisterClientScriptInclude(type, apiKey, OnlyOfficeApiUrl);

            var moduleKey = "oo_module_script_" + uid;
            if (!cs.IsClientScriptIncludeRegistered(type, moduleKey))
                cs.RegisterClientScriptInclude(type, moduleKey, ResolveUrl("~/Controls/OnlyOfficeEditor/OnlyOfficeEditor.js"));

            var proxyKey = "oo_proxy_url_" + uid;
            if (!cs.IsStartupScriptRegistered(type, proxyKey))
            {
                var proxyUrl = ResolveUrl("~/Controls/OnlyOfficeEditor/OnlyOfficeHandler.ashx?action=proxy&url=");
                var proxyScript = string.Format("window.__onlyOfficeProxyUrl='{0}';", proxyUrl);
                cs.RegisterStartupScript(type, proxyKey, proxyScript, true);
            }

            var initKey = "oo_init_" + uid;
            if (!cs.IsStartupScriptRegistered(type, initKey))
            {
                var initScript = string.Format(
                    @"(function(){{ var cfg={0}; if(cfg) OnlyOfficeEditorModule.init('{1}',cfg); }})();",
                    ConfigJson, EditorContainerId);
                cs.RegisterStartupScript(type, initKey, initScript, true);
            }

            if (string.IsNullOrWhiteSpace(CaptureTriggerId)) return;

            var ids = CaptureTriggerId.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawId in ids)
            {
                var id = rawId.Trim();
                if (string.IsNullOrEmpty(id)) continue;

                var capBtn = FindControlRecursive(Page, id);
                if (capBtn == null) continue;

                var capJs = string.Format(
                    "if(typeof OnlyOfficeEditorModule!=='undefined'){{OnlyOfficeEditorModule.captureToHiddenField('{0}','{1}',{{autoPostBack:true,postBackTarget:'{2}'}}).catch(function(e){{console.error(e)}});}};return false;",
                    EditorContainerId,
                    hfEditedDocumentBase64.ClientID,
                    capBtn.UniqueID);

                if (capBtn is IAttributeAccessor acc)
                    acc.SetAttribute("onclick", capJs);
                else if (capBtn is WebControl wc)
                    wc.Attributes["onclick"] = capJs;
            }
        }

        private static Control FindControlRecursive(Control root, string id)
        {
            if (root == null || string.IsNullOrWhiteSpace(id)) return null;
            if (root.ID == id) return root;
            foreach (Control c in root.Controls)
            {
                var found = FindControlRecursive(c, id);
                if (found != null) return found;
            }
            return null;
        }

        private class ConvertServiceResult
        {
            public bool EndConvert { get; set; }
            public string FileUrl { get; set; }
            public string ErrorMessage { get; set; }
        }
    }
}
