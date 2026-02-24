<%@ Page Title="Acerca de" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="About.aspx.cs" Inherits="OnlyOfficeControl.About" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <main aria-labelledby="title">
        <h2 id="title"><%: Title %>.</h2>
        <h3>Descripción de la aplicación.</h3>
        <p>Aplicación WebForms con integración de OnlyOffice Document Server.</p>
    </main>
</asp:Content>
