using BaseModel.Misc;
using DevExpress.Mvvm;
using System;

namespace BaseModel.ViewModel.Document
{
    /// <summary>
    /// Provides the extension methods that are used to implement the IDocumentManagerService interface.
    /// </summary>
    public static class DocumentManagerServiceExtensions
    {
        /// <summary>
        /// Creates and shows a document based upon custom parameter selection from CollectionViewModelWrapper
        /// </summary>
        /// <param name="documentManagerService">An instance of the IDocumentManager interface used to create and show the document.</param>
        /// <param name="customDocumentInfo">A custom document info to search and show document</param>
        /// <param name="parentViewModel">An object that is passed to the view model of the created view.</param>
        public static IDocument ShowExistingEntityDocument(
            this IDocumentManagerService documentManagerService, DocumentInfo customDocumentInfo, object parentViewModel)
        {
            var document = FindCustomDocument(documentManagerService, customDocumentInfo.Title) ??
                                 CreateCustomDocument(documentManagerService, customDocumentInfo, parentViewModel);
            if (document != null)
                document.Show();

            return document;
        }

        /// <summary>
        /// Creates and shows a document containing a single object view model for new entity.
        /// </summary>
        /// <param name="documentManagerService">An instance of the IDocumentManager interface used to create and show the document.</param>
        /// <param name="parentViewModel">An object that is passed to the view model of the created view.</param>
        /// <param name="newEntityInitializer">An optional parameter that provides a function that initializes a new entity.</param>
        public static void ShowNewEntityDocument<TEntity>(this IDocumentManagerService documentManagerService,
            object parentViewModel, Action<TEntity> newEntityInitializer = null)
        {
            var document = CreateDocument<TEntity>(documentManagerService,
                newEntityInitializer ?? (x => DefaultEntityInitializer(x)), parentViewModel);
            if (document != null)
                document.Show();
        }

        /// <summary>
        /// Searches for a custom document that contains the title.
        /// </summary>
        /// <param name="documentManagerService">An instance of the IDocumentManager interface used to find a document.</param>
        /// <param name="title">A document title.</param>
        public static IDocument FindCustomDocument(
            this IDocumentManagerService documentManagerService, string title = "")
        {
            if (documentManagerService == null)
                return null;
            foreach (var document in documentManagerService.Documents)
                if (title != string.Empty)
                {
                    //BaseModel Customization Start
                    if (document.Title != null && document.Title.ToString() == title)
                        return document;
                    //BaseModel Customization End
                }

            return null;
        }

        private static void DefaultEntityInitializer<TEntity>(TEntity entity)
        {
        }

        private static IDocument CreateCustomDocument(IDocumentManagerService documentManagerService, DocumentInfo customDocumentInfo, object parentViewModel)
        {
            if (documentManagerService == null)
                return null;

            IDocument document;
            document = documentManagerService.CreateDocument(customDocumentInfo.DocumentType, customDocumentInfo.Parameter, parentViewModel);
            document.Title = customDocumentInfo.Title;
            document.DestroyOnClose = true;

            return document;
        }

        private static IDocument CreateDocument<TEntity>(IDocumentManagerService documentManagerService, object parameter,
            object parentViewModel, string customDocumentName = "", string uniqueIdentifier = "")
        {
            if (documentManagerService == null)
                return null;

            var document = documentManagerService.CreateDocument(GetDocumentTypeName<TEntity>(), parameter, parentViewModel);
            document.DestroyOnClose = true;

            return document;
        }


        /// <summary>
        /// Creates and shows a document containing a single object view model for the existing entity.
        /// </summary>
        /// <param name="documentManagerService">An instance of the IDocumentManager interface used to create and show the document.</param>
        /// <param name="parentViewModel">An object that is passed to the view model of the created view.</param>
        /// <param name="primaryKey">An entity primary key.</param>
        public static IDocument ShowExistingEntityDocument<TEntity, TPrimaryKey>(
            this IDocumentManagerService documentManagerService, object parentViewModel, TPrimaryKey primaryKey)
        {
            var document = FindEntityDocument<TEntity, TPrimaryKey>(documentManagerService, primaryKey) ??
                                 CreateDocument<TEntity>(documentManagerService, primaryKey, parentViewModel);
            if (document != null)
                document.Show();
            return document;
        }

        /// <summary>
        /// Searches for a document that contains a single object view model editing entity with a specified primary key.
        /// </summary>
        /// <param name="documentManagerService">An instance of the IDocumentManager interface used to find a document.</param>
        /// <param name="primaryKey">An entity primary key.</param>
        public static IDocument FindEntityDocument<TEntity, TPrimaryKey>(
            this IDocumentManagerService documentManagerService, TPrimaryKey primaryKey)
        {
            if (documentManagerService == null)
                return null;
            foreach (var document in documentManagerService.Documents)
            {
                var entityViewModel =
                    document.Content as ISingleObjectViewModel<TEntity, TPrimaryKey>;
                if (entityViewModel != null && Equals(entityViewModel.PrimaryKey, primaryKey))
                    return document;
            }

            return null;
        }

        public static string GetDocumentTypeName<TEntity>()
        {
            return typeof(TEntity).Name + "View";
        }
    }
}