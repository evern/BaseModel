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
using System.Timers;
using System.Collections;

namespace BaseModel.ViewModel.Loader
{
    public abstract partial class CollectionViewModelsWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey,
        TMainEntityUnitOfWork> : ICollectionViewModelsWrapper<TMainProjectionEntity>, IDocumentContent, ISupportParameter
        where TMainEntity : class, IGuidEntityKey, new()
        where TMainProjectionEntity : class, IGuidEntityKey, ICanUpdate, new()
        where TMainEntityUnitOfWork : IUnitOfWork
        
    {
        protected EntitiesLoaderDescriptionCollection loaderCollection = null;
        protected EntitiesLoaderDescription<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork> mainEntityLoaderDescription;

        public CollectionViewModel<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork> MainViewModel { get; set; }
        public bool SuppressNotification { get; set; }
        public string CurrentHWID { get; set; }
        //allows view state to interact with OnMainViewModelRefreshed
        protected object onMessageSender;
        protected Dispatcher mainThreadDispatcher = Application.Current.Dispatcher;

        DispatcherTimer bulk_refresh_dispatcher_timer;
        Timer post_loaded_dispatcher_timer;
        public void CollectionViewModelWrapper()
        {
            CurrentHWID = string.Empty;
        }

        public void SetCurrentHWID(string hwid)
        {
            CurrentHWID = hwid;
        }

        public virtual void OnParameterChanged(object parameter)
        {
            initializePresentationProperties();
            resolveParameters(parameter);
            initializeEntitiesLoadersDescription();
            loadEntitiesCollection();
        }

        public void ReloadEntitiesCollection()
        {
            MainViewModel = null;
            this.RaisePropertyChanged(x => x.IsLoading);
            cleanUpEntitiesLoader();
            initializeEntitiesLoadersDescription();
            loadEntitiesCollection();
        }

        /// <summary>
        /// start loading the entities collection as per entities collection description specification in specifyEntitiesLoadersDescription()
        /// </summary>
        public void loadEntitiesCollection()
        {
            if (MainViewModel != null)
                return;
            else if (isAuxiliaryEntitiesLoaded())
                mainThreadDispatcher.BeginInvoke(new Action(() => onAuxiliaryEntitiesCollectionLoaded()));
            else
                mainThreadDispatcher.BeginInvoke(new Action(() => loadSubsequentEntitiesCollection()));
        }

        /// <summary>
        /// begin loading the collection of entities loader
        /// </summary>
        private void loadSubsequentEntitiesCollection()
        {
            var entitiesLoader = loaderCollection.Where(x => !x.IsLoaded);
            if (entitiesLoader == null || entitiesLoader.Count() == 0)
                return;

            var currentLoadOrder = entitiesLoader.Min(x => x.LoadOrder);
            var entitiesLoaderDescription = loaderCollection.First(x => x.LoadOrder == currentLoadOrder);

            entitiesLoaderDescription.CreateCollectionViewModel();
        }

        /// <summary>
        /// check if auxiliary entities are all loaded
        /// </summary>
        private bool isAuxiliaryEntitiesLoaded()
        {
            if (loaderCollection == null)
                return false;

            return loaderCollection.Where(x => !x.IsLoaded).Count() == 0 ? true : false;
        }

        /// <summary>
        /// get entities
        /// </summary>
        /// <typeparam name="TProjection">type of entities to retrieve</typeparam>
        /// <returns>entities as per TProjection specification</returns>
        protected IEnumerable<TProjection> GetEntities<TProjection>()
            where TProjection : class
        {
            if (loaderCollection == null)
                return null;

            Func<IEnumerable<TProjection>> getCollectionFunc = loaderCollection.GetCollectionFunc<TProjection>();
            return getCollectionFunc();
        }


        protected abstract void resolveParameters(object parameter);

        protected abstract void initializeEntitiesLoadersDescription();

        protected abstract void onAuxiliaryEntitiesCollectionLoaded();

        protected void CreateMainViewModel(
            IUnitOfWorkFactory<TMainEntityUnitOfWork> unitOfWorkFactory,
            Func<TMainEntityUnitOfWork, IRepository<TMainEntity, TMainEntityPrimaryKey>> getRepositoryFunc)
        {
            mainEntityLoaderDescription =
                new EntitiesLoaderDescription
                    <TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork>(this, 0,
                        unitOfWorkFactory, getRepositoryFunc, OnMainViewModelLoaded, OnBeforeEntitiesChanged, OnAfterAuxiliaryEntitiesChanged, 
                        specifyMainViewModelProjection);
        }

        protected abstract Func<IRepositoryQuery<TMainEntity>, IQueryable<TMainProjectionEntity>> specifyMainViewModelProjection();
        public Action<IEnumerable<TMainProjectionEntity>, object> OnEntitiesLoadedCallBack { get; set; }
        public Func<object> OnEntitiesLoadedCallBackRelateParam { get; set; }
        protected virtual bool OnMainViewModelLoaded(IEnumerable<TMainProjectionEntity> entities)
        {
            MainViewModel = (CollectionViewModel<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork>)mainEntityLoaderDescription.GetViewModel();
            MainViewModel.OnAfterSavedSendMessage = this.OnAfterSavedSendMessage;
            MainViewModel.OnAfterDeletedSendMessage = this.OnAfterDeletedSendMessage;
            bulk_refresh_dispatcher_timer = new DispatcherTimer();
            bulk_refresh_dispatcher_timer.Interval = new TimeSpan(0, 0, 0, 3);

            AssignCallBacksAndRaisePropertyChange(entities);
            return true;
        }

        protected virtual void AssignCallBacksAndRaisePropertyChange(IEnumerable<TMainProjectionEntity> entities)
        {
            if (OnEntitiesLoadedCallBack != null)
            {
                OnEntitiesLoadedCallBack?.Invoke(entities, OnEntitiesLoadedCallBackRelateParam == null ? null : OnEntitiesLoadedCallBackRelateParam());
                OnEntitiesLoadedCallBack = null;
                OnEntitiesLoadedCallBackRelateParam = null;
                //Self destruct after entities has been returned
                cleanUpEntitiesLoader();
                return;
            }
            
            MainViewModel.SelectedEntities = this.DisplaySelectedEntities;
            //MainViewModel.AfterBulkOperationRefreshCallBack = this.FullRefresh;
            MainViewModel.ApplyProjectionPropertiesToEntityCallBack = ApplyProjectionPropertiesToEntity;
            BackgroundRefresh();

            post_loaded_dispatcher_timer = new Timer();
            post_loaded_dispatcher_timer.Interval = 500;
            post_loaded_dispatcher_timer.Elapsed += post_loaded_dispatcher_timer_tick;
            post_loaded_dispatcher_timer.Start();
        }

        private void post_loaded_dispatcher_timer_tick(object sender, EventArgs e)
        {
            post_loaded_dispatcher_timer.Stop();
            mainThreadDispatcher.BeginInvoke(new Action(() => OnAfterAssignedCallbackAndRaisePropertyChanged()));
        }

        protected virtual void OnAfterAssignedCallbackAndRaisePropertyChanged()
        {
            if (GridControlService != null)
            {
                GridControlService.SetCheckedListFilterPopUpMode();
                GridControlService.SetGridColumnSortMode();
            }

            PersistentLayoutHelper.TryDeserializeLayout(LayoutSerializationService, ViewName);
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
        
        public Type MainEntityType
        {
            get { return typeof(TMainProjectionEntity); }
        }

        #region ISupportParameter
        object ISupportParameter.Parameter
        {
            get { return null; }
            set { OnParameterChanged(value); }
        }
        #endregion

        #region Messaging
        public virtual bool OnBeforeEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh)
        {
            onMessageSender = sender;
            if (sender != null && sender == MainViewModel)
                return true;
            return true;
        }

        public virtual void OnAfterAuxiliaryEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh)
        {
            UpdateGridSummary();

            OnPersistentAfterAuxiliaryEntitiesChanges(key, changedType, messageType, sender, isBulkRefresh);

            if (sender != null && sender == MainViewModel)
                return;

            if (!IsSingleMainEntityRefreshIdentified(key, changedType, messageType, sender, isBulkRefresh))
            {
                if (isBulkRefresh)
                {
                    bulk_refresh_dispatcher_timer.Tick -= bulk_refresh_dispatcher_timer_tick;
                    bulk_refresh_dispatcher_timer.Tick += bulk_refresh_dispatcher_timer_tick;
                    bulk_refresh_dispatcher_timer.Start();
                }
                else
                {
                    this.RaisePropertiesChanged();
                }
            }
        }

        protected virtual void OnPersistentAfterAuxiliaryEntitiesChanges(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh)
        {

        }

        private async void UpdateGridSummary()
        {
            //Always refresh summary after any changes happens
            if (GridControlService != null)
                GridControlService.RefreshSummary();
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
                    //potential to set a global where compulsory entity doesn't exists message
                    //MessageBoxService.ShowMessage(string.Format(CommonResources.Notify_View_Removed,
                    //    StringFormatUtils.GetEntityNameByType(changedType)));

                    //take this out for now, to make program leaner
                    //mainThreadDispatcher.BeginInvoke(new Action(() => FullRefresh()));
                    return;
                }
                else if (messageType == EntityMessageType.Added && compulsoryLoaders.All(x => x.GetEntitiesCount() > 0))
                {
                    //potential to set a global where compulsory entity restored mechanism
                    MessageBoxService.ShowMessage(changedType.ToString() + " restored");
                    mainThreadDispatcher.BeginInvoke(new Action(() => ReloadEntitiesCollection()));
                    return;
                }
            }
        }

        private void bulk_refresh_dispatcher_timer_tick(object sender, EventArgs e)
        {
            this.RaisePropertiesChanged();
        }

        /// <summary>
        /// override this method if entity refresh can be handled manually
        /// </summary>
        protected virtual bool IsSingleMainEntityRefreshIdentified(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh)
        {
            return DoNotAutoRefresh;
        }

        #endregion

        #region Presentation
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
        public Action<object, System.Collections.Specialized.NotifyCollectionChangedEventArgs> OnSelectedEntitiesChangedCallBack;
        private BackgroundWorker refreshBackgroundWorker;

        private void initializePresentationProperties()
        {
            refreshBackgroundWorker = new BackgroundWorker();
            refreshBackgroundWorker.DoWork += refreshBackgroundWorker_DoWork;
            refreshBackgroundWorker.WorkerSupportsCancellation = true;

            DisplaySelectedEntities = new ObservableCollection<TMainProjectionEntity>();
            DisplaySelectedEntities.CollectionChanged += DisplaySelectedEntities_CollectionChanged;
        }

        private void DisplaySelectedEntities_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnSelectedEntitiesChangedCallBack?.Invoke(sender, e);
        }

        public virtual void RefreshSelectedEntity()
        {
            this.RaisePropertyChanged(x => x.DisplaySelectedEntity);
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

        public Action<TMainProjectionEntity> OnDisplaySelectedEntityChangedCallBack;
        public virtual void OnDisplaySelectedEntityChanged(TMainProjectionEntity entity)
        {
            OnDisplaySelectedEntityChangedCallBack?.Invoke(entity);
        }
        #endregion

        #region Refresh
        public virtual bool CanFullRefresh()
        {
            return !IsLoading;
        }

        public virtual void FullRefresh()
        {
            if (MainViewModel == null)
                return;

            MainViewModel.Refresh();
            BackgroundRefresh();
        }

        public virtual void FullRefreshWithoutClearingUndoRedo()
        {
            if (MainViewModel == null)
                return;

            MainViewModel.RefreshWithoutClearingUndoManager();
            BackgroundRefresh();
        }

        private bool doNotAutoRefresh { get; set; }
        public bool DoNotAutoRefresh
        {
            get { return doNotAutoRefresh; }
            set { doNotAutoRefresh = value; }
        }

        //Delay to make sure entities are fully loaded before refreshing the view
        int viewRefreshDelay = 500;
        protected virtual void BackgroundRefresh(bool forceGridRefresh = false)
        {
            if (refreshBackgroundWorker != null && !refreshBackgroundWorker.IsBusy)
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

            mainThreadDispatcher.BeginInvoke(new Action(() => this.Refresh()));
        }

        protected virtual void onAfterRefresh()
        {

        }

        public void Refresh()
        {
            IPOCOViewModel viewModel = this as IPOCOViewModel;
            if (viewModel != null)
            {
                viewModel.RaisePropertiesChanged();
                if (GridControlService != null)
                    GridControlService.RefreshSummary();

                onAfterRefresh();
            }
        }
        #endregion

        #region IDocumentContent

        protected IDocumentOwner DocumentOwner { get; private set; }

        object IDocumentContent.Title
        {
            get { return null; }
        }

        protected abstract string ViewName { get; }

        public virtual void OnLoaded()
        {
            //PersistentLayoutHelper.TryDeserializeLayout(LayoutSerializationService, ViewName);
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
        public virtual void cleanUpEntitiesLoader()
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
            cleanUpEntitiesLoader();
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
            entity.Update();
        }

        protected virtual string ExportExcelFilename()
        {
            return "grid_export.xlsx";
        }

        public virtual void ExportToExcel()
        {
            string ResultPath = string.Empty;
            if (FolderBrowserDialogService.ShowDialog())
            {
                ResultPath = FolderBrowserDialogService.ResultPath;
                bool result = TableViewService.ExportToXls(ResultPath + "\\" + ExportExcelFilename());

                if (!result)
                    MessageBoxService.ShowMessage("Export failed because the file is in use");
            }
        }
        #endregion

        #region Services
        protected virtual IGridControlService GridControlService { get { return this.GetService<IGridControlService>("DefaultGridControlService"); } }
        protected virtual ITableViewService TableViewService { get { return this.GetService<ITableViewService>(); } }
        protected virtual ITreeViewService TreeViewService { get { return this.GetService<ITreeViewService>(); } }
        protected virtual ITreeListControlService TreeListControlService { get { return this.GetService<ITreeListControlService>(); } }
        protected virtual IFolderBrowserDialogService FolderBrowserDialogService { get { return this.GetService<IFolderBrowserDialogService>(); } }

        protected IMessageBoxService MessageBoxService
        {
            get { return this.GetRequiredService<IMessageBoxService>(); }
        }
        
        protected virtual INotificationService AppNotificationService
        {
            get { return this.GetRequiredService<INotificationService>(); }
        }

        protected ILayoutSerializationService LayoutSerializationService
        {
            get { return this.GetService<ILayoutSerializationService>(); }
        }

        public bool SupressCompulsoryEntityNotFoundMessage { get; set; }
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

        /// <summary>
        /// for sending signalR deleted message
        /// </summary>
        public virtual void OnAfterDeletedSendMessage(string entityName, string key, string messageType, string sender)
        {
            
        }

        /// <summary>
        /// for sending signalR saved message
        /// </summary>
        public virtual void OnAfterSavedSendMessage(string entityName, string key, string messageType, string sender)
        {

        }
        #endregion

        #region View Behavior
        /// <summary>
        /// Resolve problem in the view group value repeats itself
        /// </summary>
        public void CustomColumnGroup(CustomColumnSortEventArgs e)
        {
            if (e.Value1 != null && e.Value2 != null)
            {
                string first_department_string = e.Value1.ToString();
                string second_department_string = e.Value2.ToString();
                int res = Comparer.Default.Compare(first_department_string, second_department_string);
                e.Result = res;
                e.Handled = true;
            }
        }

        protected bool disable_immediate_post;
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

            if(!disable_immediate_post)
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
                    {
                        tableView.PostEditor();
                    }
                }
            }
        }
        #endregion
    }

    public interface ICollectionViewModelsWrapper<TMainProjectionEntity> : ICollectionViewModelsWrapper
        where TMainProjectionEntity : class, IGuidEntityKey, new()
    {
        //allow loaded entities to relate back to it's context
        Func<object> OnEntitiesLoadedCallBackRelateParam { get; set; }
        Action<IEnumerable<TMainProjectionEntity>, object> OnEntitiesLoadedCallBack { get; set; }
    }

    public interface ICollectionViewModelsWrapper
    {
        void loadEntitiesCollection();

        void OnAfterAuxiliaryEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh);

        void OnAfterCompulsoryEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh);

        bool OnBeforeEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh);

        void OnAfterDeletedSendMessage(string entityName, string key, string messageType, string sender);

        void OnAfterSavedSendMessage(string entityName, string key, string messageType, string sender);

        Type MainEntityType { get; }

        bool SuppressNotification { get; set; }

        bool SupressCompulsoryEntityNotFoundMessage { get; set; }

        string CurrentHWID { get; set; }
    }
}