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
        /// <param name="title">A navigation list entry display text.</param>
        /// <param name="documentType">A string value that specifies the view type of corresponding document.</param>
        /// <param name="treeViewProperty">A property containing tree view specific properties for view binding</param>
        /// <param name="documentParameter">A document parameter to specify SingleObjectView to display</param>
        protected ModuleDescription(object documentId, object parentId, string title, string documentType, object documentParameter = null, ImageSource image = null, bool treeViewIsExpanded = false)
        {
            ModuleTitle = title;
            DocumentType = documentType;
            DocumentParameter = documentParameter;
            DocumentId = documentId;
            ParentId = parentId;
            Image = Image;
            TreeViewIsExpanded = treeViewIsExpanded;
        }


        /// <summary>
        /// The navigation list entry display text.
        /// </summary>
        public string ModuleTitle { get; private set; }

        /// <summary>
        /// Contains the corresponding document view type.
        /// </summary>
        public string DocumentType { get; private set; }

        /// <summary>
        ///     The navigation parameter for SingleObjectViewModel.
        /// </summary>
        public object DocumentParameter { get; private set; }

        /// <summary>
        ///     Specifies the SingleObjectView document id
        /// </summary>
        public object DocumentId { get; private set; }

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
