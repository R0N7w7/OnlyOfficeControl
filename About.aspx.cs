using System;
using System.Web.UI;

namespace OnlyOfficeControl
{
    public partial class About : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // cargar un documento desde la raiz del servidor
            if (!IsPostBack)
            {
                string filePath = Server.MapPath("~/doc.docx");
                if (System.IO.File.Exists(filePath))
                {
                    docEditor.SetDocumentFromBytes(System.IO.File.ReadAllBytes(filePath), "doc.docx");
                }
                else
                {
                    // Manejar el caso donde el archivo no existe
                    Response.Write("El archivo no se encontró.");
                }
            }
        }
    }
}
