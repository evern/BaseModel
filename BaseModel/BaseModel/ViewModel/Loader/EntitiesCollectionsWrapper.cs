using BaseModel.DataModel;
using BaseModel.Helpers;
using BaseModel.Data.Helpers;
using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using BaseModel.Misc;
using BaseModel.ViewModel.Base;
using BaseModel.ViewModel.Document;
using BaseModel.ViewModel.Services;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Editors;
using System.Windows.Input;
using System.Windows.Media;

namespace BaseModel.ViewModel.Loader
{
    public abstract class CollectionViewModelsWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey,
        TMainEntityUnitOfWork> : ICollectionViewModelsWrapper, IDocumentContent, ISupportParameter, ISupportViewRestoration
        where TMainEntity : class, IGuidEntityKey, new()
        where TMainProjectionEntity : class, IGuidEntityKey, new()
        where TMainEntityUnitOfWork : IUnitOfWork
        
    {
        protected bool isSubEntitiesAdded;
        protected EntitiesLoaderDescriptionCollection loaderCollection = null;

        protected EntitiesLoaderDescription<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork> mainEntityLoaderDescription;

        public CollectionViewModel<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork> MainViewModel { get; set; }
        public bool SuppressNotification { get; set; }

        //allows view state to interact with OnMainViewModelRefreshed
        //due to StoreViewState being called OnBeforeEntitiesChanged and OnMainViewModelRefreshed called OnEntitiesLoaded
        protected object onMessageSender;
        protected Dispatcher mainThreadDispatcher = Application.Current.Dispatcher;

        public void CollectionViewModelWrapper()
        {
            bulk_refresh_dispatcher_timer = new DispatcherTimer();
            bulk_refresh_dispatcher_timer.Interval = new TimeSpan(0, 0, 0, 3);
        }

        public virtual void InvokeEntitiesLoaderDescriptionLoading()
        {
            if (MainViewModel != null)
                return;
            else if (isAllEntitiesLoaded())
                mainThreadDispatcher.BeginInvoke(new Action(() => OnAllEntitiesCollectionLoaded()));
            else
                mainThreadDispatcher.BeginInvoke(new Action(() => loadEntitiesCollectionOnMainThread()));
        }

        /// <summary>
        /// Begins loading the collection of entities loader
        /// </summary>
        private void loadEntitiesCollectionOnMainThread()
        {
            var entitiesLoader = loaderCollection.Where(x => !x.IsLoaded);
            if (entitiesLoader == null || entitiesLoader.Count() == 0)
                return;

            var currentLoadOrder = entitiesLoader.Min(x => x.LoadOrder);
            var entitiesLoaderDescription =
                loaderCollection.First(x => x.LoadOrder == currentLoadOrder);

            entitiesLoaderDescription.CreateCollectionViewModel();
        }

        private bool isAllEntitiesLoaded()
        {
            if (loaderCollection == null)
                return false;

            return loaderCollection.Where(x => !x.IsLoaded).Count() == 0 ? true : false;
        }

        protected IEnumerable<TProjection> GetEntities<TProjection>()
            where TProjection : class
        {
            if (loaderCollection == null)
                return null;

            Func<IEnumerable<TProjection>> getCollectionFunc = loaderCollection.GetCollectionFunc<TProjection>();
            return getCollectionFunc();
        }

        protected virtual void OnParameterChanged(object parameter)
        {
            InitializePresentationProperties();
            InitializeParameters(parameter);

            InitializeAndLoadEntitiesLoaderDescription();
        }

        protected virtual void InitializeParameters(object parameter)
        {
            throw new NotImplementedException("Override this method to initialize primary parameter attributes in inherited member.");
        }

        public virtual void InitializeAndLoadEntitiesLoaderDescription()
        {
            throw new NotImplementedException("Override this method to initialize EntitiesLoaderDescriptionCollection.");
        }

        protected virtual void OnAllEntitiesCollectionLoaded()
        {
            throw new NotImplementedException("Override this method to initialize main entity loader.");
        }

        protected void CreateMainViewModel(
            IUnitOfWorkFactory<TMainEntityUnitOfWork> unitOfWorkFactory,
            Func<TMainEntityUnitOfWork, IRepository<TMainEntity, TMainEntityPrimaryKey>> getRepositoryFunc)
        {
            mainEntityLoaderDescription =
                new EntitiesLoaderDescription
                    <TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork>(this, 0,
                        unitOfWorkFactory, getRepositoryFunc, OnMainViewModelLoaded, OnBeforeAffectingOrCompulsoryEntitiesChanged, OnAfterAffectingEntitiesChanged, 
                        ConstructMainViewModelProjection);
        }

        protected virtual Func<IRepositoryQuery<TMainEntity>, IQueryable<TMainProjectionEntity>>
            ConstructMainViewModelProjection()
        {
            throw new NotImplementedException("Override this method to define how main view model should be constructed.");
        }

        protected virtual bool OnMainViewModelLoaded(IEnumerable<TMainProjectionEntity> entities)
        {
            MainViewModel = (CollectionViewModel<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork>)mainEntityLoaderDescription.GetViewModel();
            MainViewModel.OnAfterSavedSendMessage = this.OnAfterSavedSendMessage;
            MainViewModel.OnAfterDeletedSendMessage = this.OnAfterDeletedSendMessage;
            AssignCallBacksAndRaisePropertyChange(entities);
            return true;
        }

        protected virtual void AssignCallBacksAndRaisePropertyChange(IEnumerable<TMainProjectionEntity> entities)
        {
            MainViewModel.SelectedEntities = this.DisplaySelectedEntities;
            //MainViewModel.AfterBulkOperationRefreshCallBack = this.FullRefresh;
            MainViewModel.ApplyProjectionPropertiesToEntityCallBack = ApplyProjectionPropertiesToEntity;
            RefreshView();
        }

        protected void ApplyProjectionPropertiesToEntity(TMainProjectionEntity projectionEntity, TMainEntity entity)
        {
            OnBeforeApplyProjectionPropertiesToEntity(projectionEntity, entity);
            IProjection<TMainEntity> projection = projectionEntity as IProjection<TMainEntity>;
            if(projection != null)
            {
                IHaveCreatedDate iHaveCreatedDateProjectionEntity = projection.Entity as IHaveCreatedDate;
                if (iHaveCreatedDateProjectionEntity != null)
                {
                    //workaround for created because Save() only sets the projection primary key, this is used for property redo where the interceptor only tampers with UPDATED and CREATED is left as null
                    if (iHaveCreatedDateProjectionEntity.EntityCreatedDate.Date.Year == 1)
                        iHaveCreatedDateProjectionEntity.EntityCreatedDate = DateTime.Now;
                }

                DataUtils.ShallowCopy(entity, projection.Entity);
            }
            else
            {
                IHaveCreatedDate iHaveCreatedDateEntity = entity as IHaveCreatedDate;
                if (iHaveCreatedDateEntity != null)
                {
                    //workaround for created because Save() only sets the projection primary key, this is used for property redo where the interceptor only tampers with UPDATED and CREATED is left as null
                    if (iHaveCreatedDateEntity.EntityCreatedDate.Date.Year == 1)
                        iHaveCreatedDateEntity.EntityCreatedDate = DateTime.Now;
                }
            }
        }

        protected virtual void OnBeforeApplyProjectionPropertiesToEntity(TMainProjectionEntity projectionEntity, TMainEntity entity)
        {

        }

        IEnumerable<IEntitiesLoaderDescription> compulsoryLoaders { get; set; }
        IEnumerable<IEntitiesLoaderDescription> CompulsoryLoaders
        {
            get
            {
                if (compulsoryLoaders == null)
                    compulsoryLoaders = loaderCollection.Where(x => x.IsCompulsory);

                return compulsoryLoaders;
            }
        }

        public virtual bool OnBeforeAffectingOrCompulsoryEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh)
        {
            onMessageSender = sender;
            if (sender != null && sender == MainViewModel)
                return true;

                //mainThreadDispatcher.BeginInvoke(new Action(() => StoreViewState()));
            return true;
        }

        DispatcherTimer bulk_refresh_dispatcher_timer;
        private void bulk_refresh_dispatcher_timer_tick(object sender, EventArgs e)
        {
            this.RaisePropertiesChanged();
        }

        public virtual void OnAfterAffectingEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh)
        {
            if (sender != null && sender == MainViewModel)
                return;

            if (!IsSingleMainEntityRefreshIdentified(key, changedType, messageType, sender, isBulkRefresh))
            {
                if(isBulkRefresh)
                {
                    bulk_refresh_dispatcher_timer.Tick -= bulk_refresh_dispatcher_timer_tick;
                    bulk_refresh_dispatcher_timer.Tick += bulk_refresh_dispatcher_timer_tick;
                    bulk_refresh_dispatcher_timer.Start();
                }
                else
                    this.RaisePropertiesChanged();
            }

            //mainThreadDispatcher.BeginInvoke(new Action(() => FullRefreshWithoutClearingUndoRedo()));
            //this.RaisePropertiesChanged();
            //IsSingleMainEntityRefreshIdentified(key, changedType, messageType, sender);
        }

        protected virtual bool IsSingleMainEntityRefreshIdentified(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh)
        {
            return DoNotAutoRefresh;
            //Override this method to check if a single main entity can be refreshed by sending a message
            //return false;
        }

        public virtual void OnAfterCompulsoryEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh)
        {
            if (SuppressNotification)
                return;

            IEntitiesLoaderDescription currentCompulsoryEntitiesLoader = CompulsoryLoaders.FirstOrDefault(x => x.GetEntitiesProjectionType() == changedType);

            if (currentCompulsoryEntitiesLoader != null)
            {
                if (messageType == EntityMessageType.Deleted && currentCompulsoryEntitiesLoader.GetEntitiesCount() == 0)
                {
                    //MessageBoxService.ShowMessage(string.Format(CommonResources.Notify_View_Removed,
                    //    StringFormatUtils.GetEntityNameByType(changedType)));
                    mainThreadDispatcher.BeginInvoke(new Action(() => FullRefresh()));
                    return;
                }
                else if (messageType == EntityMessageType.Added && compulsoryLoaders.All(x => x.GetEntitiesCount() > 0))
                {
                    //MessageBoxService.ShowMessage(string.Format(CommonResources.Notify_View_Restored,
                    //    StringFormatUtils.GetEntityNameByType(changedType)));

                    mainThreadDispatcher.BeginInvoke(new Action(() => InitializeAndLoadEntitiesLoaderDescription()));
                    return;
                }
            }
        }

        public Type MainEntityType
        {
            get { return typeof(TMainProjectionEntity); }
        }

        protected virtual void OnMainViewModelRefreshed(IEnumerable<TMainProjectionEntity> refreshedEntities)
        {
            if (onMessageSender != null && (onMessageSender == MainViewModel || onMessageSender == this))
                return;

            //entities are confirmed to be loaded here for refresh to work properly on MainViewModel
            RefreshView();
        }

        #region ISupportParameter
        object ISupportParameter.Parameter
        {
            get { return null; }
            set { OnParameterChanged(value); }
        }
        #endregion

        #region Presentation
        public Action StoreActiveCell { get; set; }
        public Action RestoreActiveCell { get; set; }
        public Action ForceGridRefresh { get; set; }

        private Guid RestoreSelectedEntityGuid;
        private List<Guid> RestoreSelectedEntitiesGuids = new List<Guid>();
        TMainProjectionEntity displaySelectedEntity;
        public TMainProjectionEntity DisplaySelectedEntity
        {
            get { return displaySelectedEntity;  }
            set
            {
                displaySelectedEntity = value;
                OnDisplaySelectedEntityChanged(value);
            }
        }

        public ObservableCollection<TMainProjectionEntity> DisplaySelectedEntities { get; set; }
        public Action OnSelectedEntitiesChangedCallBack;
        private BackgroundWorker refreshBackgroundWorker;
        private BackgroundWorker storeViewStateBackgroundWorker;

        private void InitializePresentationProperties()
        {
            refreshBackgroundWorker = new BackgroundWorker();
            refreshBackgroundWorker.DoWork += refreshBackgroundWorker_DoWork;
            refreshBackgroundWorker.WorkerSupportsCancellation = true;

            storeViewStateBackgroundWorker = new BackgroundWorker();
            storeViewStateBackgroundWorker.DoWork += storeViewStateBackgroundWorker_DoWork;
            storeViewStateBackgroundWorker.WorkerSupportsCancellation = true;

            DisplaySelectedEntities = new ObservableCollection<TMainProjectionEntity>();
            DisplaySelectedEntities.CollectionChanged += DisplaySelectedEntities_CollectionChanged;
        }

        private void DisplaySelectedEntities_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnSelectedEntitiesChangedCallBack?.Invoke();
        }

        public virtual void RefreshSelectedEntity()
        {
            this.RaisePropertyChanged(x => x.DisplaySelectedEntity);
        }

        public virtual void OnDisplaySelectedEntityChanged(TMainProjectionEntity entity)
        {
            
        }

        public virtual bool CanFullRefresh()
        {
            return !IsLoading;
        }

        public virtual void FullRefresh()
        {
            if (MainViewModel == null)
                return;

            mainThreadDispatcher.BeginInvoke(new Action(() => StoreViewState()));
            MainViewModel.Refresh();
            RefreshView();
        }

        public virtual void FullRefreshWithoutClearingUndoRedo()
        {
            if (MainViewModel == null)
                return;

            mainThreadDispatcher.BeginInvoke(new Action(() => StoreViewState()));
            MainViewModel.RefreshWithoutClearingUndoManager();
            RefreshView();
        }

        private bool doNotAutoRefresh { get; set; }

        public bool DoNotAutoRefresh
        {
            get { return doNotAutoRefresh; }
            set { doNotAutoRefresh = value; }
        }

        //Delay to make sure entities are fully loaded before refreshing the view
        int viewRefreshDelay = 500;
        protected virtual void RefreshView(bool forceGridRefresh = false)
        {
            if(!refreshBackgroundWorker.IsBusy)
                refreshBackgroundWorker.RunWorkerAsync(forceGridRefresh);
        }

        private void refreshBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            bool forceGridRefresh = (bool)e.Argument;
            System.Threading.Thread.Sleep(viewRefreshDelay);
            if (refreshBackgroundWorker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }

            mainThreadDispatcher.BeginInvoke(new Action(() => this.refreshView(forceGridRefresh)));
        }

        public virtual ObservableCollection<TMainProjectionEntity> DisplayEntities
        {
            get
            {
                if (MainViewModel == null)
                    return null;

                return MainViewModel.Entities;
            }
        }

        protected void StoreViewState()
        {
            if (!storeViewStateBackgroundWorker.IsBusy)
                storeViewStateBackgroundWorker.RunWorkerAsync();
        }

        private void storeViewStateBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (storeViewStateBackgroundWorker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }

            mainThreadDispatcher.BeginInvoke(new Action(() => this.storeViewState()));
            System.Threading.Thread.Sleep(viewRefreshDelay);
        }

        protected virtual void storeViewState()
        {
            IPOCOViewModel viewModel = this as IPOCOViewModel;
            if (viewModel == null)
                return;

            if (DisplayEntities == null)
                return;

            StoreActiveCell?.Invoke();

            RestoreSelectedEntityGuid = Guid.Empty;
            RestoreSelectedEntitiesGuids.Clear();

            foreach (var selectedEntity in DisplaySelectedEntities)
                RestoreSelectedEntitiesGuids.Add(new Guid(selectedEntity.EntityKey.ToString()));

            if (DisplaySelectedEntity != null)
                RestoreSelectedEntityGuid = DisplaySelectedEntity.EntityKey;
        }

        protected virtual void restoreViewState()
        {
            if (DisplayEntities == null)
                return;

            var restoreSelectedEntities =
                DisplayEntities.Where(x => RestoreSelectedEntitiesGuids.Any(y => y == x.EntityKey));
            DisplaySelectedEntities.Clear();
            if (restoreSelectedEntities.Count() > 0)
                foreach (var restoreSelectedEntity in restoreSelectedEntities)
                    DisplaySelectedEntities.Add(restoreSelectedEntity);

            if (RestoreSelectedEntityGuid != Guid.Empty)
            {
                var restoreSelectedEntity =
                    DisplayEntities.FirstOrDefault(x => x.EntityKey == RestoreSelectedEntityGuid);
                if (restoreSelectedEntity != null)
                    DisplaySelectedEntity = restoreSelectedEntity;
            }

            RestoreActiveCell?.Invoke();
        }

        private void refreshView(bool isForceGridRefresh)
        {
            IPOCOViewModel viewModel = this as IPOCOViewModel;
            if(viewModel != null)
            {
                viewModel.RaisePropertiesChanged();
                if (isForceGridRefresh && ForceGridRefresh != null)
                    ForceGridRefresh();
                restoreViewState();
            }
        }
        #endregion

        #region IDocumentContent

        protected IDocumentOwner DocumentOwner { get; private set; }

        object IDocumentContent.Title
        {
            get { return null; }
        }

        protected virtual string ViewName
        {
            get { throw new NotImplementedException("Override this method to specify the view name."); }
        }

        public virtual void OnLoaded()
        {
            PersistentLayoutHelper.TryDeserializeLayout(LayoutSerializationService, ViewName);
        }

        public bool IsLoading
        {
            get
            {
                if (this.IsInDesignMode())
                    return true;
                if (MainViewModel == null)
                    return true;

                //assuming RaisePropertyChanged will be always be called upon on MainViewModel entities loaded
                return false;
            }
        }

        protected virtual void OnClose(CancelEventArgs e)
        {
            refreshBackgroundWorker.CancelAsync();
        }

        void IDocumentContent.OnClose(CancelEventArgs e)
        {
            OnClose(e);
        }

        /// <summary>
        /// Unregister any messaging listener
        /// </summary>
        public virtual void CleanUpEntitiesLoader()
        {
            compulsoryLoaders = null;
            if (mainEntityLoaderDescription != null)
            {
                mainEntityLoaderDescription.DisposeViewModel();
                mainEntityLoaderDescription = null;
                MainViewModel = null;
            }

            if (loaderCollection == null)
                return;

            loaderCollection.OnDestroy();
            loaderCollection = null;
        }

        void IDocumentContent.OnDestroy()
        {
            CleanUpEntitiesLoader();
        }

        IDocumentOwner IDocumentContent.DocumentOwner
        {
            get { return DocumentOwner; }
            set { DocumentOwner = value; }
        }

        #endregion

        #region View Interactions
        public void SetMainNestedValueWithUndoAndRefresh(TMainProjectionEntity entity, string propertyName, object newValue)
        {
            MainViewModel.SetNestedValueWithUndo(entity, propertyName, newValue);
            this.RaisePropertyChanged(x => x.DisplaySelectedEntity);
        }

        protected virtual string ExportExcelFilename()
        {
            return "grid_export.xls";
        }

        public void ExportToExcel()
        {
            string ResultPath = string.Empty;
            if (FolderBrowserDialogService.ShowDialog())
            {
                ResultPath = FolderBrowserDialogService.ResultPath;
                TableViewService.ExportToXls(ResultPath + "\\" + ExportExcelFilename());
            }
        }
        #endregion

        #region Services
        protected virtual IGridControlService GridControlService { get { return this.GetService<IGridControlService>(); } }
        protected virtual ITableViewService TableViewService { get { return this.GetService<ITableViewService>(); } }
        protected virtual ITreeViewService TreeViewService { get { return this.GetService<ITreeViewService>(); } }
        protected virtual IFolderBrowserDialogService FolderBrowserDialogService { get { return this.GetService<IFolderBrowserDialogService>(); } }

        protected IMessageBoxService MessageBoxService
        {
            get { return this.GetRequiredService<IMessageBoxService>(); }
        }

        protected ILayoutSerializationService LayoutSerializationService
        {
            get { return this.GetService<ILayoutSerializationService>(); }
        }
        #endregion

        #region Layout
        public void SaveLayout()
        {
            PersistentLayoutHelper.TrySerializeLayout(LayoutSerializationService, ViewName);
            PersistentLayoutHelper.SaveLayout();
        }

        public void ResetLayout()
        {
            if (MessageBoxService.ShowMessage(CommonResources.Confirmation_ResetLayout, CommonResources.Confirmation_Caption, MessageButton.YesNo) != MessageResult.Yes)
                return;

            PersistentLayoutHelper.ResetLayout(ViewName);
        }

        public virtual void OnAfterDeletedSendMessage(string entityName, string key, string messageType, string sender)
        {
            
        }

        public virtual void OnAfterSavedSendMessage(string entityName, string key, string messageType, string sender)
        {
            
        }
        #endregion

        #region View Behavior

        /// <summary>
        /// Influence column(s) when changes happens in other column
        /// </summary>
        public void CellValueChanging(CellValueChangedEventArgs e)
        {
            if (e.RowHandle == GridControl.AutoFilterRowHandle)
                return;

            CellValueAnyRowChanging(e);
            if(!e.Handled)
            {
                if (e.RowHandle == DataControlBase.NewItemRowHandle)
                    CellValueNewRowChanging(e);
                else
                    CellValueExistingRowChanging(e);
            }

            CellValueChangingImmediatePost(e);
        }

        protected virtual void CellValueAnyRowChanging(CellValueChangedEventArgs e)
        {
        }

        protected virtual void CellValueNewRowChanging(CellValueChangedEventArgs e)
        {

        }

        protected virtual void CellValueExistingRowChanging(CellValueChangedEventArgs e)
        {

        }

        private void CellValueChangingImmediatePost(CellValueChangedEventArgs e)
        {
            TableView tableView = e.Source as TableView;
            //only post editor if editing row is not new row or else new row will be committed immediately
            if (tableView != null && e.RowHandle != GridControl.NewItemRowHandle)
            {
                if(tableView.ActiveEditor != null)
                {
                    Type activeEditorType = tableView.ActiveEditor.GetType();
                    if (activeEditorType == typeof(ComboBoxEdit) || activeEditorType == typeof(CheckEdit))
                        tableView.PostEditor();
                }
            }
        }
        #endregion
    }

    public interface ICollectionViewModelsWrapper
    {
        void InvokeEntitiesLoaderDescriptionLoading();

        void InitializeAndLoadEntitiesLoaderDescription();

        bool OnBeforeAffectingOrCompulsoryEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh);

        void OnAfterAffectingEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh);

        void OnAfterCompulsoryEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh);

        void OnAfterDeletedSendMessage(string entityName, string key, string messageType, string sender);

        void OnAfterSavedSendMessage(string entityName, string key, string messageType, string sender);

        Type MainEntityType { get; }

        bool SuppressNotification { get; set; }
    }
}