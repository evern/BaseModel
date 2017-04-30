using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseModel.ViewModel.Document
{
    /// <summary>
    /// Parameter for IDocumentManagerServiceExtensions to search and show document
    /// </summary>
    public class DocumentInfo
    {
        public DocumentInfo(object parameter, string documentType, string title)
        {
            this.Parameter = parameter;
            this.DocumentType = documentType;
            this.Title = title;
        }

        public object Parameter { get; set; }
        public string DocumentType { get; set; }
        //Title used to display on tab and searching for document, should be unique
        public string Title { get; set; }
    }
}
