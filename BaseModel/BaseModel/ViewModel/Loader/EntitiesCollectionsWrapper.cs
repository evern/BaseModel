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
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Data.Filtering;

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
        private DispatcherTimer selectedEntitiesChangedDispatchTimer;
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
            selectedEntitiesChangedDispatchTimer = new DispatcherTimer();
            selectedEntitiesChangedDispatchTimer.Interval = new TimeSpan(0, 0, 0, 0, 1);
            initializePresentationProperties();
            resolveParameters(parameter);
            initializeAndLoad();
        }

        public void ReloadEntitiesCollection()
        {
            MainViewModel = null;
            this.RaisePropertyChanged(x => x.IsLoading);
            CleanUpEntitiesLoader();
            initializeAndLoad();
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
            if (loaderCollection == null)
                return;

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

        protected void initializeAndLoad()
        {
            MainViewModel = null;
            //CleanUpEntitiesLoader();
            loaderCollection = new EntitiesLoaderDescriptionCollection(this);
            addEntitiesLoader();
            loadEntitiesCollection();
        }

        protected abstract void addEntitiesLoader();

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
        public bool OnEntitiesLoadedCallBackManualDispose { get; set; }
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
            if (!OnEntitiesLoadedCallBackManualDispose && OnEntitiesLoadedCallBack != null)
            {
                OnEntitiesLoadedCallBack?.Invoke(entities, OnEntitiesLoadedCallBackRelateParam == null ? null : OnEntitiesLoadedCallBackRelateParam());
                OnEntitiesLoadedCallBack = null;
                OnEntitiesLoadedCallBackRelateParam = null;
                //Self destruct after entities has been returned

                CleanUpEntitiesLoader();
                return;
            }
            
            MainViewModel.SelectedEntities = this.DisplaySelectedEntities;
            MainViewModel.UnifiedValueChangingCallback = this.UnifiedCellValueChanging;
            MainViewModel.UnifiedValueValidationCallback = this.UnifiedValueValidation;
            MainViewModel.UnifiedValidateRow = this.UnifiedRowValidation;
            MainViewModel.AfterBulkOperationRefreshCallBack = this.FullRefreshWithoutClearingUndoRedo;
            MainViewModel.ApplyProjectionPropertiesToEntityCallBack = ApplyProjectionPropertiesToEntity;
            BackgroundRefresh();

            post_loaded_dispatcher_timer = new Timer();
            post_loaded_dispatcher_timer.Interval = 1500;
            post_loaded_dispatcher_timer.Elapsed += post_loaded_dispatcher_timer_tick;
            post_loaded_dispatcher_timer.Start();
        }

        private void post_loaded_dispatcher_timer_tick(object sender, EventArgs e)
        {
            post_loaded_dispatcher_timer.Stop();
            if(OnEntitiesLoadedCallBackManualDispose && OnEntitiesLoadedCallBack != null)
            {
                OnEntitiesLoadedCallBack?.Invoke(MainViewModel.Entities, OnEntitiesLoadedCallBackRelateParam == null ? null : OnEntitiesLoadedCallBackRelateParam());
                OnEntitiesLoadedCallBack = null;
                OnEntitiesLoadedCallBackRelateParam = null;
                return;
            }

            mainThreadDispatcher.BeginInvoke(new Action(() => OnAfterAssignedCallbackAndRaisePropertyChanged()));
        }

        protected virtual void OnAfterAssignedCallbackAndRaisePropertyChanged()
        {
            if (GridControlService != null)
            {
                GridControlService.SetCheckedListFilterPopUpMode();
                //GridControlService.SetGridColumnSortMode();
                GridControlService.CombineMasterDetailSearch();
            }

            if(!PersistentLayoutHelper.TryDeserializeLayout(LayoutSerializationService, ViewName))
            {
                if (TableViewService != null)
                {
                    TableViewService.ApplyDefaultF2Behavior();

                    //Do not apply best fit if entities aren't loaded within timeframe
                    if(DisplayEntities != null && DisplayEntities.Count > 0)
                        TableViewService.ApplyBestFit();
                }
            }
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
                if (compulsoryLoaders == null && loaderCollection != null)
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
            //if (sender != null && sender == MainViewModel)
            //    return true;
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
        protected virtual TMainProjectionEntity returnDisplaySelectedEntity()
        {
            return displaySelectedEntity;
        }

        TMainProjectionEntity displaySelectedEntity;
        public TMainProjectionEntity DisplaySelectedEntity
        {
            get
            {
                return returnDisplaySelectedEntity();
            }
            set
            {
                displaySelectedEntity = value;
                OnDisplaySelectedEntityChanged(value);
            }
        }


        protected ObservableCollection<TMainProjectionEntity> displaySelectedEntities;
        public ObservableCollection<TMainProjectionEntity> DisplaySelectedEntities
        {
            get
            {
                mainThreadDispatcher.BeginInvoke(new Action(() => onBeforeDisplaySelectedEntitiesGet()));
                return displaySelectedEntities;
            }

            set { displaySelectedEntities = value; }
        }

        protected virtual void onBeforeDisplaySelectedEntitiesGet()
        {

        }

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
            //multiple selection will call this multiple times. so this is used to remove unecessary calls
            selectedEntitiesChangedDispatchTimer.Tick -= dispatchTimer_Tick;
            selectedEntitiesChangedDispatchTimer.Tick += dispatchTimer_Tick;
            selectedEntitiesChangedDispatchTimer.Start();
        }

        private void dispatchTimer_Tick(object sender, EventArgs e)
        {
            selectedEntitiesChangedDispatchTimer.Stop();
            OnSelectedEntitiesChanged();
        }

        protected virtual void OnSelectedEntitiesChanged()
        {

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

            //Since notification is turned off full refresh demands reloading entities
            //MainViewModel.Refresh();
            ReloadEntitiesCollection();
            BackgroundRefresh();
        }

        public virtual void FullRefreshWithoutClearingUndoRedo()
        {
            if (MainViewModel == null)
                return;


            //need to force load or else addition/deletion won't be refreshed
            MainViewModel.LoadEntities(true, BackgroundRefresh);
            //MainViewModel.RefreshWithoutClearingUndoManager();
            //BackgroundRefresh();
            //GridControlService.SetExpansionState(groupExpansionState);
        }

        private void onAfterBulkChangeRefresh()
        {
            ObservableCollection<Misc.GroupInfo> groupExpansionState = GridControlService.GetExpansionState();
            IPOCOViewModel viewModel = this as IPOCOViewModel;
            if (viewModel != null)
            {
                viewModel.RaisePropertiesChanged();
                if (GridControlService != null)
                    GridControlService.RefreshSummary();

                onAfterRefresh();
            }
            GridControlService.SetExpansionState(groupExpansionState);
        }

        private bool doNotAutoRefresh { get; set; }
        public bool DoNotAutoRefresh
        {
            get { return doNotAutoRefresh; }
            set { doNotAutoRefresh = value; }
        }

        //Delay to make sure entities are fully loaded before refreshing the view
        int viewRefreshDelay = 1000;
        protected virtual void BackgroundRefresh()
        {
            if (refreshBackgroundWorker != null && !refreshBackgroundWorker.IsBusy)
                refreshBackgroundWorker.RunWorkerAsync();
        }

        private void refreshBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
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

        public void PreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                mainThreadDispatcher.BeginInvoke(new Action(() =>
                {
                    TableViewService.CommitEditing();
                }));
            }
        }

        public virtual void Refresh()
        {
            IPOCOViewModel viewModel = this as IPOCOViewModel;
            if (viewModel != null)
            {
                ObservableCollection<Misc.GroupInfo> groupExpansionState = new ObservableCollection<Misc.GroupInfo>();
                CriteriaOperator filterCriteria = null; 
                if (GridControlService != null)
                {
                    groupExpansionState = GridControlService.GetExpansionState();
                    filterCriteria = GridControlService.GetFilterCriteria();
                }

                viewModel.RaisePropertiesChanged();
                if (GridControlService != null)
                {
                    GridControlService.RefreshSummary();
                    GridControlService.SetExpansionState(groupExpansionState);
                    GridControlService.SetFilterCriteria(filterCriteria);
                }

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

        protected virtual void onBeforeDestroy()
        {

        }

        void IDocumentContent.OnDestroy()
        {
            onBeforeDestroy();
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

        public virtual void CopyWithHeader()
        {
            GridControlService.CopyWithHeader();
        }
        #endregion

        #region Services
        [ServiceProperty(Key = "DefaultGridControlService")]
        public virtual IGridControlService GridControlService { get { return null; } }

        //protected virtual IGridControlService GridControlService { get { return this.GetService<IGridControlService>("DefaultGridControlService"); } }
        [ServiceProperty(Key = "DefaultTableViewService")]
        protected virtual ITableViewService TableViewService { get { return null; } }
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
        public bool InViewModelOnlyMode { get; set; }
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
        public virtual void CellValueChanging(CellValueChangedEventArgs e)
        {
            if (e.RowHandle == GridControl.AutoFilterRowHandle)
                return;

            if(!e.Handled)
            {
                MainViewModel.EntitiesUndoRedoManager.PauseActionId();
                UnifiedCellValueChanging(e.Column.FieldName, e.OldValue, e.Value, (TMainProjectionEntity)e.Row, e.RowHandle == DataControlBase.NewItemRowHandle);
                //will be unpaused in existingrow or newrow save
            }

            if (!disable_immediate_post)
                CellValueChangingImmediatePost(e);
        }

        /// <summary>
        /// Routine used by copy paste, fill, new and existing row cell value changing to determine which other cells to affect
        /// </summary>
        /// <param name="field_name">Field name changed</param>
        /// <param name="old_value">Old value currently in projection</param>
        /// <param name="new_value">New value that projection is going to use</param>
        /// <param name="projection">Changed projection</param>
        /// <param name="isNew">Is new row</param>
        public virtual void UnifiedCellValueChanging(string field_name, object old_value, object new_value, TMainProjectionEntity projection, bool isNew)
        {

        }

        /// <summary>
        /// Routine used by copy paste, fill, new and existing row cell value changing to determine whether value is valid
        /// </summary>
        /// <param name="projection">Changed projection</param>
        /// <param name="field_name">Field name changed</param>
        /// <param name="new_value">New value that projection is going to use</param>
        /// <param name="error_message">Default is empty string, set value to indicate error</param>
        public abstract string UnifiedValueValidation(TMainProjectionEntity projection, string field_name, object new_value);

        public abstract string UnifiedRowValidation(TMainProjectionEntity projection);

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

        bool InViewModelOnlyMode { get; set; }

        string CurrentHWID { get; set; }
    }
}