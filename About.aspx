<%@ Page Title="Acerca de" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="About.aspx.cs" Inherits="OnlyOfficeControl.About" %>

<%@ Register Src="~/Controls/OnlyOfficeEditor/OnlyOfficeEditor.ascx" TagPrefix="Editor" TagName="Editor" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <main aria-labelledby="title">
        <h2 id="title"><%: Title %>.</h2>
        <h3>Descripción de la aplicación.</h3>
        <p>Aplicación WebForms con integración de OnlyOffice Document Server.</p>
        <Editor:Editor
            ID="docEditor"
            runat="server"
            CaptureTriggerId="btnDescargar" />
    </main>
</asp:Content>
