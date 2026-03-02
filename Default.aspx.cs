using System;
using System.IO;
using System.Web.UI;

namespace OnlyOfficeControl
{
    public partial class _Default : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            //    if (!IsPostBack)
            //    {
            //        string sampleFilePath = Server.MapPath("~/doc.docx");
            //        if (File.Exists(sampleFilePath))
            //        {
            //            byte[] fileBytes = File.ReadAllBytes(sampleFilePath);
            //            docEditor.SetDocumentFromBytes(fileBytes, "doc.docx");
            //        }
            //}
        }

        protected void btnUpload_Click(object sender, EventArgs e)
        {
            if (!fuFile.HasFile)
            {
                litStatus.Text = string.Empty;
                return;
            }
            docEditor.SetDocumentFromBytes(fuFile.FileBytes, fuFile.FileName);
            litStatus.Text = string.Empty;

            docEditor.Mode = "view";
        }

        protected void btnDescargar_Click(object sender, EventArgs e)
        {
            byte[] documentBytes = docEditor.ConvertCurrentDocumentToPdfBytes();
            //byte[] documentBytes = docEditor.GetEditedDocumentBytes();

            if (documentBytes == null || documentBytes.Length == 0)
            {
                litStatus.Text = "<span class='text-warning'>No hay documento editado para descargar.</span>";
                return;
            }

            docEditor.ClearEditedDocument();

            var ext = Path.GetExtension(docEditor.DocumentName ?? ".docx");
            var fileName = docEditor.DocumentName ?? ("documento" + ext);
            Response.Clear();
            Response.ContentType = "application/octet-stream";
            Response.AddHeader("Content-Disposition", "attachment; filename=" + fileName + ".pdf");
            Response.AddHeader("Content-Length", documentBytes.Length.ToString());
            Response.BinaryWrite(documentBytes);
            Response.End();
        }
    }
}
