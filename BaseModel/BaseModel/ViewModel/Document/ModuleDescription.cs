﻿using BaseModel.Data.Helpers;
using BaseModel.Misc;
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
        protected ModuleDescription(string securityKey, string uniqueNavigationKey, string parentId, string title, string documentType = null, object documentParameter = null, ImageSource image = null, string navigationTitle = null, bool treeViewIsExpanded = false, bool showInCollapseMode = false, Func<string> preferredDocumentType = null, bool isTextSearchEnabled = false)
        {
            ModuleTitle = title;
            DocumentType = documentType == null ? string.Empty : documentType;
            DocumentParameter = documentParameter;

            //to allow Document.Id to comply to XamlName Grammar
            string uniqueNavKeyFormat = DataUtils.FormatNavigationKey(uniqueNavigationKey);
            NavigationId = securityKey + uniqueNavKeyFormat;
            SecurityKey = securityKey;
            ParentId = parentId;
            Image = Image;
            TreeViewIsExpanded = treeViewIsExpanded;
            NavigationTitle = (navigationTitle == string.Empty || navigationTitle == null) ? ModuleTitle : navigationTitle;
            ShowInCollapseMode = showInCollapseMode;
            PreferredDocumentType = preferredDocumentType;
            IsTextSearchEnabled = isTextSearchEnabled;
        }

        /// <summary>
        /// The navigation list entry display text, also used for document searching
        /// </summary>
        public string ModuleTitle { get; private set; }

        /// <summary>
        /// The tab display title, usually follows ModuleTitle
        /// </summary>
        public string NavigationTitle { get; private set; }

        /// <summary>
        /// Specify whether the document is navigatable
        /// </summary>
        public bool CanNavigate
        {
            get { return (DocumentType != null && DocumentType != string.Empty) || PreferredDocumentType != null; }
        }

        /// <summary>
        /// Contains the corresponding document view type.
        /// </summary>
        public string DocumentType { get; private set; }

        /// <summary>
        /// Contains the method to retrieve preferred view type.
        /// </summary>
        public Func<string> PreferredDocumentType { get; private set; }

        /// <summary>
        ///     The navigation parameter for SingleObjectViewModel.
        /// </summary>
        public object DocumentParameter { get; private set; }

        /// <summary>
        ///     Specifies the parentId for treeview binding, cannot be nested since dxTreeView doesn't support nested for Ids
        /// </summary>
        public string ParentId { get; private set; }

        /// <summary>
        ///     Specifies the Id for treeview binding, cannot be nested since dxTreeView doesn't support nested for Ids
        /// </summary>
        public string NavigationId { get; private set; }

        /// <summary>
        ///     Specifies the Id used for security profiles
        /// </summary>
        public string SecurityKey { get; private set; }

        /// <summary>
        ///     Specify the treeview image property when binded to TreeViewControl
        /// </summary>
        public ImageSource Image { get; set; }

        /// <summary>
        ///     Specifies the tool tip to display on menu item
        /// </summary>
        public string ToolTip { get; set; }

        /// <summary>
        ///     Describe whether the treelist item is expanded
        /// </summary>
        public bool TreeViewIsExpanded { get; set; }

        /// <summary>
        ///     Describe whether the treelist item can be searched
        /// </summary>
        public bool IsTextSearchEnabled { get; set; }

        /// <summary>
        ///     Describe whether the treelist item show in expanded
        /// </summary>
        public bool ShowInCollapseMode { get; set; }

        /// <summary>
        ///     Child modules that forms the navigation tree
        /// </summary>
        public RangeObservableCollection<TModule> ChildModules { get; set; }
    }
}
