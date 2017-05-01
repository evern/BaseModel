using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace BaseModel.ViewModel.Document
{
    public abstract partial class ModuleDescription<TModule> where TModule : ModuleDescription<TModule>
    {
        /// <summary>
        ///     Initializes a new instance of the ModuleDescription class.
        /// </summary>
        protected ModuleDescription(object id, object parentId, string title, string documentType = "", object documentParameter = null, ImageSource image = null, bool treeViewIsExpanded = false)
        {
            ModuleTitle = title;
            DocumentType = documentType;
            DocumentParameter = documentParameter;
            Id = id;
            ParentId = parentId;
            Image = Image;
            TreeViewIsExpanded = treeViewIsExpanded;
        }

        /// <summary>
        /// The navigation list entry display text.
        /// </summary>
        public string ModuleTitle { get; private set; }

        /// <summary>
        /// Specify whether the document is navigatable
        /// </summary>
        public bool CanNavigate
        {
            get { return DocumentType != string.Empty; }
        }

        /// <summary>
        /// Contains the corresponding document view type.
        /// </summary>
        public string DocumentType { get; private set; }

        /// <summary>
        ///     The navigation parameter for SingleObjectViewModel.
        /// </summary>
        public object DocumentParameter { get; private set; }

        /// <summary>
        ///     Specifies the parentId for treeview binding, cannot be nested since dxTreeView doesn't support nested for Ids
        /// </summary>
        public object ParentId { get; private set; }

        /// <summary>
        ///     Specifies the Id for treeview binding, cannot be nested since dxTreeView doesn't support nested for Ids
        /// </summary>
        public object Id { get; private set; }
        
        /// <summary>
        ///     Specify the treeview image property when binded to TreeViewControl
        /// </summary>
        public ImageSource Image { get; set; }

        /// <summary>
        ///     Describe whether the treelist item is expanded
        /// </summary>
        public bool TreeViewIsExpanded { get; set; }
    }
}
