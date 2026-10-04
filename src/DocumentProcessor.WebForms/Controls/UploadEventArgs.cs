using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components.Forms;

namespace DocumentProcessor.WebForms.Controls
{
    public class UploadEventArgs : EventArgs
    {
        public UploadEventArgs(IList<IBrowserFile> files)
        {
            Files = files;
        }

        public IList<IBrowserFile> Files { get; private set; }
    }
}
