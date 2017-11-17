using BaseModel.DataModel;
using BaseModel.Misc;
using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace BaseModel.ViewModel.Document
{
    /// <summary>
    /// The base class for POCO view models that operate the collection of documents.
    /// </summary>
    /// <typeparam name="TModule">A navigation list entry type.</typeparam>
    /// <typeparam name="TUnitOfWork">A unit of work type.</typeparam>
    public abstract class DocumentsViewModel<TModule, TUnitOfWork> : ISupportLogicalLayout
        where TModule : ModuleDescription<TModule>
        where TUnitOfWork : IUnitOfWork
    {
        private const string ViewLayoutName = "DocumentViewModel";

        protected readonly IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory;

        /// <summary>
        /// Initializes a new instance of the DocumentsViewModel class.
        /// </summary>
        /// <param name="unitOfWorkFactory">A factory used to create a unit of work instance.</param>
        protected DocumentsViewModel(IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
            Modules = new RangeObservableCollection<TModule>();
            Modules.AddRange(CreateModules());

            //Modules.AddRange(CreateModules());
            //foreach (var module in Modules)
            //    Messenger.Default.Register<NavigateMessage<TModule>>(this, module, x => Show(x.Token));
            //Messenger.Default.Register<DestroyOrphanedDocumentsMessage>(this, x => DestroyOrphanedDocuments());
        }

        private void DestroyOrphanedDocuments()
        {
            var orphans = this.GetOrphanedDocuments().Except(this.GetImmediateChildren());
            foreach (var orphan in orphans)
            {
                orphan.DestroyOnClose = true;
                orphan.Close();
            }
        }

        public void AddModules(IEnumerable<TModule> modules)
        {
            Modules.AddRange(modules);
        }

        /// <summary>
        /// Navigation list that represents a collection of module descriptions.
        /// </summary>
        public RangeObservableCollection<TModule> Modules { get; set; }

        /// <summary>
        /// A currently selected navigation list entry. This property is writable. When this property is assigned a new value, it triggers the navigating to the corresponding document.
        /// Since DocumentsViewModel is a POCO view model, this property will raise INotifyPropertyChanged.PropertyEvent when modified so it can be used as a binding source in views.
        /// </summary>
        public virtual TModule SelectedModule { get; set; }

        /// <summary>
        /// A navigation list entry that corresponds to the currently active document. If the active document does not have the corresponding entry in the navigation list, the property value is null. This property is read-only.
        /// Since DocumentsViewModel is a POCO view model, this property will raise INotifyPropertyChanged.PropertyEvent when modified so it can be used as a binding source in views.
        /// </summary>
        public virtual TModule ActiveModule { get; protected set; }

        /// <summary>
        /// Saves changes in all opened documents.
        /// Since DocumentsViewModel is a POCO view model, an instance of this class will also expose the SaveAllCommand property that can be used as a binding source in views.
        /// </summary>
        public void SaveAll()
        {
            Messenger.Default.Send(new SaveAllMessage());
        }

        /// <summary>
        /// Used to close all opened documents and allows you to save unsaved results and to cancel closing.
        /// Since DocumentsViewModel is a POCO view model, an instance of this class will also expose the OnClosingCommand property that can be used as a binding source in views.
        /// </summary>
        /// <param name="cancelEventArgs">An argument of the System.ComponentModel.CancelEventArgs type which is used to cancel closing if needed.</param>
        public virtual void OnClosing(CancelEventArgs cancelEventArgs)
        {
            if (GroupedDocumentManagerService != null && GroupedDocumentManagerService.Groups.Count() > 1)
            {
                var activeGroup = GroupedDocumentManagerService.ActiveGroup;
                var message = new CloseAllMessage(cancelEventArgs, vm =>
                {
                    var activeVMs = activeGroup.Documents.Select(d => d.Content);
                    return activeVMs.Contains(vm);
                });
                Messenger.Default.Send(message);
                return;
            }
            //BaseModel Customization
            //SaveLogicalLayout();
            if (LayoutSerializationService != null)
                PersistentLayoutHelper.PersistentViewsLayout[ViewLayoutName] = LayoutSerializationService.Serialize();

            Messenger.Default.Send(new CloseAllMessage(cancelEventArgs, vm => true));
            PersistentLayoutHelper.SaveLayout();
        }

        private NavigationPaneVisibility navigationPaneVisibility { get; set; }

        /// <summary>
        /// Contains a current state of the navigation pane.
        /// </summary>
        /// Since DocumentsViewModel is a POCO view model, this property will raise INotifyPropertyChanged.PropertyEvent when modified so it can be used as a binding source in views.
        public virtual NavigationPaneVisibility NavigationPaneVisibility
        {
            get { return navigationPaneVisibility; }
            set { navigationPaneVisibility = value; }
        }

        /// <summary>
        /// Finalizes the DocumentsViewModel initialization and opens the default document.
        /// Since DocumentsViewModel is a POCO view model, an instance of this class will also expose the OnLoadedCommand property that can be used as a binding source in views.
        /// </summary>
        public abstract void OnLoaded();

        protected IGroupedDocumentManagerService GroupedDocumentManagerService
        {
            get { return this.GetService<IGroupedDocumentManagerService>(); }
        }

        protected IDocumentManagerService DocumentManagerService
        {
            get { return this.GetService<IDocumentManagerService>(); }
        }

        protected ILayoutSerializationService LayoutSerializationService
        {
            get { return this.GetService<ILayoutSerializationService>("RootLayoutSerializationService"); }
        }

        protected IDocumentManagerService WorkspaceDocumentManagerService
        {
            get { return this.GetService<IDocumentManagerService>("WorkspaceDocumentManagerService"); }
        }

        public virtual TModule DefaultModule
        {
            get { return null; }
        }

        protected bool IsLoaded { get; set; }

        /// <summary>
        /// Navigates to a document.
        /// Since DocumentsViewModel is a POCO view model, an instance of this class will also expose the NavigateCommand property that can be used as a binding source in views.
        /// </summary>
        /// <param name="module">A navigation list entry specifying a document what to be opened.</param>
        public void Navigate()
        {
            if (IsLoaded && SelectedModule != null && SelectedModule.CanNavigate)
                NavigateCore(SelectedModule);
        }

        public virtual IDocument NavigateCore(TModule module)
        {
            if (module == null || DocumentManagerService == null)
                return null;

            DocumentInfo documentInfo = new DocumentInfo(module.Id, module.DocumentParameter, module.DocumentType, module.ModuleTitle);
            var document = DocumentManagerService.ShowExistingEntityDocument(documentInfo, this);
            //var document = DocumentManagerService.FindDocumentByIdOrCreate(module.ModuleTitle,
            //    x => NavigateToDocument(module));
            //document.Show();
            return document;
        }

        IDocument preloadDocument;
        protected void startPreloading(TModule module)
        {
            if (module == null || DocumentManagerService == null)
                return;

            DocumentInfo documentInfo = new DocumentInfo(module.Id, module.DocumentParameter, module.DocumentType, module.ModuleTitle);
            var document = DocumentManagerService.ShowExistingEntityDocument(documentInfo, this);
            //var document = DocumentManagerService.FindDocumentByIdOrCreate(module.ModuleTitle,
            //    x => NavigateToDocument(module));
            //document.Show();
            document.Hide();
            preloadDocument = document;
        }

        protected void ClosePreloadDocument()
        {
            if (preloadDocument != null)
            {
                preloadDocument.Close();
                preloadDocument = null;
            }
        }

        private IDocument NavigateToDocument(TModule module)
        {
            var document = DocumentManagerService.CreateDocument(module.DocumentType, module.DocumentParameter, this);
            document.Title = module.ModuleTitle;
            document.Id = module.ModuleTitle;
            document.DestroyOnClose = true;
            return document;
        }

        protected virtual void OnActiveModuleChanged(TModule oldModule)
        {
            SelectedModule = ActiveModule;
        }


        private IDocument CreateDocument(TModule module)
        {
            var document = DocumentManagerService.CreateDocument(module.DocumentType, null, this);
            document.Title = module.ModuleTitle;
            document.DestroyOnClose = true;
            return document;
        }

        protected abstract TModule[] CreateModules();

        protected TUnitOfWork CreateUnitOfWork()
        {
            return unitOfWorkFactory.CreateUnitOfWork();
        }

        bool ISupportLogicalLayout.CanSerialize
        {
            get { return true; }
        }

        IDocumentManagerService ISupportLogicalLayout.DocumentManagerService
        {
            get { return DocumentManagerService; }
        }

        IEnumerable<object> ISupportLogicalLayout.LookupViewModels
        {
            get { return null; }
        }
    }

    /// <summary>
    /// Represents a navigation pane state.
    /// </summary>
    public enum NavigationPaneVisibility
    {
        /// <summary>
        /// Navigation pane is visible and minimized.
        /// </summary>
        Minimized,

        /// <summary>
        /// Navigation pane is visible and not minimized.
        /// </summary>
        Normal,

        /// <summary>
        /// Navigation pane is invisible.
        /// </summary>
        Off
    }
}