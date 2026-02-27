<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="OnlyOfficeEditor.ascx.cs" Inherits="OnlyOfficeControl.Controls.OnlyOfficeEditorBundle.OnlyOfficeEditor" %>
<link rel="stylesheet" href="<%= ResolveUrl("~/Controls/OnlyOfficeEditor/OnlyOfficeEditor.css") %>" />

<div id="<%= EditorContainerId %>_wrapper" class="ooe-wrapper">
    <div id="<%= EditorContainerId %>_busy" class="ooe-busyOverlay" style="display:none;" aria-live="polite" aria-busy="true">
        <div class="ooe-busyOverlay__card" role="status">
            <span class="ooe-spinner" aria-hidden="true"></span>
            <div class="ooe-busyOverlay__text">Procesando&#8230;</div>
        </div>
    </div>
    <div id="<%= EditorContainerId %>" class="ooe-editor" style="min-height:<%= EditorHeight %>;"></div>
</div>

<asp:HiddenField ID="hfEditedDocumentBase64" runat="server" />
