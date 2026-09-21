<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="DocumentUploader.ascx.cs" Inherits="DocumentProcessor.WebForms.Controls.DocumentUploader" %>

<h1 class="upload-title">Upload a document and I'll summarize it.</h1>
<p class="upload-subtitle">
    <asp:Literal ID="FileTypeSummaryText" runat="server" />
    · up to <asp:Literal ID="MaxFileSizeText" runat="server" /> MB
</p>

<div class="dropzone">
    <%-- The file input is stretched over the whole zone by .dropzone-input, which is what
         makes dropping files onto it work without any script. --%>
    <asp:FileUpload ID="FilePicker" runat="server" AllowMultiple="true"
        CssClass="dropzone-input" ToolTip="Drag files here or click to browse" />
    <div class="dropzone-icon"><i class="bi bi-cloud-arrow-up"></i></div>
    <p class="dropzone-hint">Drag files here</p>
    <span class="btn-brand">Browse files</span>
</div>

<%-- Filled in by script once the user picks files: the server does not know what is in
     the file input until the form is posted. --%>
<ul id="selectedList" class="selected-list"></ul>

<div id="uploadActions" class="d-flex gap-2 mt-3" style="display: none;">
    <asp:Button ID="UploadButton" runat="server" CssClass="btn-brand"
        Text="Upload" OnClick="UploadButton_Click" />
    <button type="button" id="clearButton" class="btn-quiet">Clear</button>
</div>

<asp:Repeater ID="NoticeRepeater" runat="server">
    <ItemTemplate>
        <div class="notice notice-<%# Eval("CssSuffix") %>">
            <i class="bi <%# Eval("IconClass") %>"></i>
            <span><%# System.Web.HttpUtility.HtmlEncode(Eval("Text") as string) %></span>
        </div>
    </ItemTemplate>
</asp:Repeater>

<script type="text/javascript">
    (function () {
        var picker = document.getElementById('<%= FilePicker.ClientID %>');
        var uploadButton = document.getElementById('<%= UploadButton.ClientID %>');
        var list = document.getElementById('selectedList');
        var actions = document.getElementById('uploadActions');
        var clearButton = document.getElementById('clearButton');

        function formatSize(bytes) {
            var units = ['B', 'KB', 'MB', 'GB'];
            var size = bytes;
            var unit = 0;
            while (size >= 1024 && unit < units.length - 1) {
                size = size / 1024;
                unit++;
            }
            return (Math.round(size * 100) / 100) + ' ' + units[unit];
        }

        function render() {
            var files = picker.files;
            list.innerHTML = '';

            for (var i = 0; i < files.length; i++) {
                var item = document.createElement('li');
                item.className = 'selected-item';
                var label = document.createElement('small');
                label.appendChild(document.createTextNode(files[i].name + ' (' + formatSize(files[i].size) + ')'));
                item.appendChild(label);
                list.appendChild(item);
            }

            actions.style.display = files.length > 0 ? '' : 'none';
            uploadButton.value = files.length === 1
                ? 'Upload 1 file'
                : 'Upload ' + files.length + ' files';
        }

        function reset() {
            picker.value = '';
            render();
        }

        picker.onchange = render;
        clearButton.onclick = reset;
    })();
</script>
