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
        public DocumentInfo(object id, object parameter, string documentType, string title)
        {
            this.Parameter = parameter;
            this.DocumentType = documentType;
            this.Title = title;
            this.Id = id;
        }

        public object Parameter { get; set; }
        public string DocumentType { get; set; }
        public string Title { get; set; }
        //Id used for searching document, should be unique
        public object Id { get; set; }
    }
}
