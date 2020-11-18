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
using System.Threading.Tasks;
using System.Reflection;
using DevExpress.Xpf.Editors.Settings;
using DevExpress.Xpf.Grid.LookUp;
using System.Windows.Forms;
using DevExpress.Xpf.Grid.TreeList;

namespace BaseModel.ViewModel.Loader
{
    public abstract partial class CollectionViewModelsWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey,
        TMainEntityUnitOfWork> : ViewModelBase, ICollectionViewModelsWrapper<TMainProjectionEntity>, IDocumentContent, ISupportParameter
        where TMainEntity : class, new()
        where TMainProjectionEntity : class, ICanUpdate, new()
        where TMainEntityUnitOfWork : IUnitOfWork
        
    {
        protected EntitiesLoaderDescriptionCollection loaderCollection = null;
        protected EntitiesLoaderDescription<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork> mainEntityLoaderDescription;

        /// <summary>
        /// when grid rows loaded will be handled
        /// </summary>
        protected bool isHandleLoadedGridRows;
        public bool IsUsedAsPersistentViewModel;
        public bool IsReadOnly { get; set; }
        protected virtual string readOnlyMessage => string.Empty;
        public CollectionViewModel<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork> MainViewModel { get; set; }
        public bool SuppressNotification { get; set; }
        public bool AlwaysSkipMessage { get; set; }
        public string CurrentHWID { get; set; }
        protected bool stopSubsequentEntitiesLoading;
        //allows view state to interact with OnMainViewModelRefreshed
        protected object onMessageSender;
        protected Dispatcher mainThreadDispatcher = System.Windows.Application.Current.Dispatcher;
        //so that bulk refresh don't get called multiple times within a short duration
        private DispatcherTimer viewRefreshDispatcherTimer;
        private BackgroundWorker viewRefreshBackgroundWorker;
        private DispatcherTimer viewRaisePropertyChangeDispatcherTimer;
        private System.Timers.Timer entitiesLoadedTimer;
        public CollectionViewModelsWrapper()
        {
            viewRefreshBackgroundWorker = new BackgroundWorker();
            viewRefreshBackgroundWorker.DoWork += refreshBackgroundWorker_DoWork;
            viewRefreshBackgroundWorker.WorkerSupportsCancellation = true;

            CurrentHWID = string.Empty;
        }

        public void SetCurrentHWID(string hwid)
        {
            CurrentHWID = hwid;
        }

        public virtual void OnParameterChange(object parameter)
        {
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
            if (stopSubsequentEntitiesLoading)
                return;

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
                return new List<TProjection>();

            Func<IEnumerable<TProjection>> getCollectionFunc = loaderCollection.GetCollectionFunc<TProjection>();
            return getCollectionFunc();
        }

        protected abstract void resolveParameters(object parameter);

        protected void initializeAndLoad()
        {
            stopSubsequentEntitiesLoading = false;
            MainViewModel = null;
            //CleanUpEntitiesLoader();
            loaderCollection = new EntitiesLoaderDescriptionCollection(this);
            loaderCollection.AlwaysSkipMessage = this.AlwaysSkipMessage;
            addEntitiesLoader();
            loadEntitiesCollection();
        }

        protected abstract void addEntitiesLoader();

        protected abstract void onAuxiliaryEntitiesCollectionLoaded();

        protected void CreateMainViewModel(IUnitOfWorkFactory<TMainEntityUnitOfWork> unitOfWorkFactory, Func<TMainEntityUnitOfWork, IRepository<TMainEntity, TMainEntityPrimaryKey>> getRepositoryFunc)
        {
            mainEntityLoaderDescription = new EntitiesLoaderDescription<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork>(this, 0, unitOfWorkFactory, getRepositoryFunc, OnMainViewModelLoaded, OnBeforeEntitiesChanged, OnAfterAuxiliaryEntitiesChanged, specifyMainViewModelProjection, null, this.AlwaysSkipMessage);
            MainViewModel = mainEntityLoaderDescription.CreateMainCollectionViewModel();
            MainViewModel.OnAfterSavedSendMessageCallBack = this.OnAfterSavedSendMessage;
            MainViewModel.OnAfterDeletedSendMessageCallBack = this.OnAfterDeletedSendMessage;
            MainViewModel.SelectedEntities = this.SelectedEntities;
            MainViewModel.UnifiedValueChangingCallback = this.UnifiedCellValueChanging;
            MainViewModel.UnifiedValueChangedCallback = this.UnifiedCellValueChanged;
            MainViewModel.UnifiedNewRowInitialisationFromView = this.UnifiedNewRowInitializationFromView;
            MainViewModel.UnifiedValueValidationCallback = this.UnifiedValueValidation;
            MainViewModel.UnifiedValidateRow = this.UnifiedRowValidation;
            MainViewModel.FullRefreshWithoutClearingUndoRedoCallBack = this.FullRefreshWithoutClearingUndoRedo;
            MainViewModel.OnBeforeApplyingProjectionPropertiesToEntityIsContinueCallBack = OnBeforeApplyingProjectionPropertiesToEntityIsContinue;
            MainViewModel.FormatErrorMessagesCallBack = FormatErrorMessages;
            MainViewModel.OnSelectedEntitiesChangedCallBack = OnSelectedEntitiesChanged;

            //database behaviours
            MainViewModel.OnBeforeProjectionSaveIsContinueCallBack = OnBeforeProjectionSaveIsContinue;
            MainViewModel.OnBeforeProjectionDeleteIsContinueCallBack = OnBeforeProjectionDeleteIsContinue;
            MainViewModel.OnAfterProjectionSavedCallBack = OnAfterProjectionSave;
            MainViewModel.OnAfterProjectionsSavedCallBack = OnAfterProjectionsSave;
            MainViewModel.OnBeforeProjectionsDeleteCallBack = OnBeforeProjectionsDelete;
            MainViewModel.OnAfterProjectionsDeletedCallBack = OnAfterProjectionsDeleted;
            mainEntityLoaderDescription.LoadMainCollectionViewModel();
        }

        protected abstract Func<IRepositoryQuery<TMainEntity>, IQueryable<TMainProjectionEntity>> specifyMainViewModelProjection();
        public Action<IEnumerable<TMainProjectionEntity>, object> OnEntitiesLoadedCallBack { get; set; }
        public bool OnEntitiesLoadedCallBackManualDispose { get; set; }
        public Func<object> OnEntitiesLoadedCallBackRelateParam { get; set; }
        protected virtual bool OnMainViewModelLoaded(IEnumerable<TMainProjectionEntity> entities)
        {
            //if it was disposed before fully loaded
            if (mainEntityLoaderDescription == null)
                return false;

            if (MainViewModel == null)
                return false;

            viewRefreshDispatcherTimer = new DispatcherTimer();
            viewRefreshDispatcherTimer.Interval = new TimeSpan(0, 0, 0, 3);
            viewRaisePropertyChangeDispatcherTimer = new DispatcherTimer();
            viewRaisePropertyChangeDispatcherTimer.Interval = new TimeSpan(0, 0, 0, 3);

            AssignCallBacksAndRaisePropertyChange(entities);
            return true;
        }

        protected virtual void AssignCallBacksAndRaisePropertyChange(IEnumerable<TMainProjectionEntity> entities)
        {
            isFirstLoaded = true;
            MainViewModel.SetParentViewModel(this);
            if (!OnEntitiesLoadedCallBackManualDispose && OnEntitiesLoadedCallBack != null)
            {
                OnEntitiesLoadedCallBack?.Invoke(entities, OnEntitiesLoadedCallBackRelateParam == null ? null : OnEntitiesLoadedCallBackRelateParam());
                OnEntitiesLoadedCallBack = null;
                OnEntitiesLoadedCallBackRelateParam = null;

                if(!IsUsedAsPersistentViewModel)
                {
                    //Self destruct after entities has been returned
                    CleanUpEntitiesLoader();
                    return;
                }
            }

            if(!IsUsedAsPersistentViewModel)
            {
                BackgroundRefresh();
                if (!isHandleLoadedGridRows)
                {
                    onGridRowsLoaded();
                }
            }
        }

        protected void onGridRowsLoaded()
        {
            entitiesLoadedTimer = new System.Timers.Timer();
            entitiesLoadedTimer.Interval = 1000;
            entitiesLoadedTimer.Elapsed += entitiesLoadedTimer_Elapsed;
            entitiesLoadedTimer.Start();
        }

        protected void entitiesLoadedTimer_Elapsed(object sender, EventArgs e)
        {
            entitiesLoadedTimer.Stop();
            if(OnEntitiesLoadedCallBackManualDispose && OnEntitiesLoadedCallBack != null)
            {
                OnEntitiesLoadedCallBack?.Invoke(MainViewModel.Entities, OnEntitiesLoadedCallBackRelateParam == null ? null : OnEntitiesLoadedCallBackRelateParam());
                OnEntitiesLoadedCallBack = null;
                OnEntitiesLoadedCallBackRelateParam = null;
                return;
            }

            mainThreadDispatcher.BeginInvoke(new Action(() => OnAfterAssignedCallbackAndRaisePropertyChanged()));
        }

        protected bool forceApplyBestFit = false;
        protected bool doNotApplyBestFit = false;
        bool isLayoutLoaded = false;
        protected virtual void OnAfterAssignedCallbackAndRaisePropertyChanged()
        {
            if (GridControlService != null)
            {
                GridControlService.SetExcelFilterPopUpMode();
                //GridControlService.SetGridColumnSortMode();
                GridControlService.CombineMasterDetailSearch();
            }

            if (!PersistentLayoutHelper.TryDeserializeLayout(LayoutSerializationService, ViewName))
            {
                isLayoutLoaded = false;
                if (TableViewService != null)
                {
                    //TableViewService.ApplyDefaultF2Behavior();

                    if (!doNotApplyBestFit)
                        //Do not apply best fit if entities aren't loaded within timeframe
                        if (forceApplyBestFit || (Entities != null && Entities.Count > 0))
                            TableViewService.ApplyBestFit();
                }
            }
            else
                isLayoutLoaded = true;

            if (IsReadOnly)
                MessageBoxService.ShowMessage(readOnlyMessage, "Read Only", MessageButton.OK);
        }

        protected virtual bool OnBeforeApplyingProjectionPropertiesToEntityIsContinue(TMainProjectionEntity projectionEntity, TMainEntity entity)
        {
            return true;
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
            set { OnParameterChange(value); }
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
            UpdateGridSummaryAsync();

            OnPersistentAfterAuxiliaryEntitiesChanges(key, changedType, messageType, sender, isBulkRefresh);

            if (sender != null && sender == MainViewModel)
                return;

            if (!IsSingleMainEntityRefreshIdentified(key, changedType, messageType, sender, isBulkRefresh))
            {
                if (isBulkRefresh)
                {
                    viewRefreshDispatcherTimer.Tick -= refreshTimer_Tick;
                    viewRefreshDispatcherTimer.Tick += refreshTimer_Tick;
                    viewRefreshDispatcherTimer.Start();
                }
                else
                {
                    viewRaisePropertyChangeDispatcherTimer.Tick -= RaisePropertyChangeDispatcherTimer_Tick;
                    viewRaisePropertyChangeDispatcherTimer.Tick += RaisePropertyChangeDispatcherTimer_Tick;
                    viewRaisePropertyChangeDispatcherTimer.Start();
                }
            }
        }

        private void RaisePropertyChangeDispatcherTimer_Tick(object sender, EventArgs e)
        {
            this.RaisePropertiesChanged();
        }

        protected virtual void OnPersistentAfterAuxiliaryEntitiesChanges(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh)
        {

        }

        private void UpdateGridSummaryAsync()
        {
            //Always refresh summary after any changes happens
            if (GridControlService != null)
                Task.Run(() => mainThreadDispatcher.BeginInvoke(new Action(() => GridControlService.RefreshSummary())));
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
                    return;
                }
                else if (messageType == EntityMessageType.Added && compulsoryLoaders.All(x => x.GetEntitiesCount() > 0))
                {
                    mainThreadDispatcher.BeginInvoke(new Action(() => ReloadEntitiesCollection()));
                    return;
                }
            }
        }

        private void refreshTimer_Tick(object sender, EventArgs e)
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
        public virtual void RefreshSelectedEntity()
        {
            this.RaisePropertyChanged(x => x.SelectedEntity);
        }
        #endregion

        #region Refresh
        public virtual void FullRefreshWithoutClearingUndoRedo()
        {
            if (MainViewModel == null)
                return;

            //need to force load or else addition/deletion won't be refreshed
            MainViewModel.LoadEntities(true, BackgroundRefresh);
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
            if (viewRefreshBackgroundWorker != null && !viewRefreshBackgroundWorker.IsBusy)
                viewRefreshBackgroundWorker.RunWorkerAsync();
        }

        private void refreshBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            System.Threading.Thread.Sleep(viewRefreshDelay);
            if (viewRefreshBackgroundWorker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }

            mainThreadDispatcher.BeginInvoke(new Action(() => this.ViewRefresh()));
        }

        protected virtual void onAfterRefresh()
        {

        }

        public void PreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                mainThreadDispatcher.BeginInvoke(new Action(() =>
                {
                    TableViewService.CommitEditing();
                }));
            }
        }

        public virtual void ViewRefresh()
        {
            IPOCOViewModel viewModel = this as IPOCOViewModel;
            if (viewModel != null)
            {
                ObservableCollection<Misc.GroupInfo> groupExpansionState = new ObservableCollection<Misc.GroupInfo>();
                CriteriaOperator filterCriteria = null; 
                if (GridControlService != null)
                {
                    groupExpansionState = GridControlService.GetExpansionState();
                    filterCriteria = GridControlService.FilterCriteria;
                }

                viewModel.RaisePropertiesChanged();
                if (GridControlService != null)
                {
                    GridControlService.RefreshSummary();
                    GridControlService.SetExpansionState(groupExpansionState);
                    GridControlService.FilterCriteria = filterCriteria;
                }

                onAfterRefresh();
            }

            this.RaisePropertiesChanged();
        }
        #endregion

        #region Grid Proxy
        protected virtual void OnSelectedEntitiesChanged()
        {
            this.RaisePropertyChanged(x => x.SelectedEntities);
            this.RaisePropertyChanged(x => x.SelectedEntity);
        }

        /// <summary>
        /// Fired before cell validation, influence column(s) when changes happens in other column
        /// </summary>
        public virtual void CellValueChanging(CellValueChangedEventArgs e)
        {
            MainViewModel?.CellValueChanging(e);
        }

        /// <summary>
        /// Fired after cell validation, influence column(s) when changes happens in other column
        /// </summary>
        public virtual void CellValueChanged(CellValueChangedEventArgs e)
        {
            MainViewModel?.CellValueChanged(e);
        }

        protected ObservableCollection<TMainProjectionEntity> selectedEntities;
        public ObservableCollection<TMainProjectionEntity> SelectedEntities
        {
            get => MainViewModel == null ? null : MainViewModel.SelectedEntities;
            set
            {
                if (MainViewModel != null)
                    MainViewModel.SelectedEntities = value;
            }
        }

        public TMainProjectionEntity SelectedEntity
        {
            get => MainViewModel == null ? null : MainViewModel.SelectedEntity;
            set
            {
                if (MainViewModel != null)
                    MainViewModel.SelectedEntity = value;
            }
        }

        public virtual ObservableCollection<TMainProjectionEntity> Entities => MainViewModel == null ? null : MainViewModel.Entities;

        public ScaleTransform Zoom => MainViewModel == null ? null : MainViewModel.Zoom;

        public void ShowPopUp(object sender)
        {
            MainViewModel?.ShowPopUp(sender);
        }

        public void Grid_MouseWheel(MouseWheelEventArgs e)
        {
            MainViewModel?.Grid_MouseWheel(e);
        }

        public string CellValueChangingFieldName => MainViewModel == null ? null : MainViewModel.CellValueChangingFieldName;

        public bool IsPasteCellLevel
        {
            get => MainViewModel == null ? false : MainViewModel.IsPasteCellLevel;
            set
            {
                if (MainViewModel != null)
                {
                    MainViewModel.IsPasteCellLevel = value;
                    this.RaisePropertyChanged(x => x.SelectMode);
                }
            }
        }

        public MultiSelectMode SelectMode => MainViewModel == null ? MultiSelectMode.Row : MainViewModel.SelectMode;

        public virtual bool IsValidEntity(TMainProjectionEntity entity, IEnumerable<TMainProjectionEntity> preCommittedProjections, ref string errorMessage, out List<KeyValuePair<string, string>> constraintIssues)
        {
            constraintIssues = new List<KeyValuePair<string, string>>();
            if(MainViewModel != null)
                if(!MainViewModel.IsValidEntity(entity, preCommittedProjections, ref errorMessage, out constraintIssues))
                    return false;

            return true;
        }

        public virtual void ValidateCell(GridCellValidationEventArgs e)
        {
            MainViewModel?.ValidateCell(e);
        }

        public virtual void ValidateRow(GridRowValidationEventArgs e)
        {
            MainViewModel?.ValidateRow(e);
        }

        public virtual void InitNewRow(InitNewRowEventArgs e)
        {
            try
            {
                TMainProjectionEntity projection = (TMainProjectionEntity)GridControlService.GetRow(e.RowHandle);
                UnifiedNewRowInitializationFromView(projection);
            }
            catch
            {

            }
        }

        public void NewRowSave(RowEventArgs e)
        {
            MainViewModel?.NewRowSave(e);
        }

        public void OnAfterNewProjectionsAdded(IEnumerable<TMainProjectionEntity> newItems)
        {
            MainViewModel?.OnAfterNewProjectionsAdded(newItems);
        }

        public virtual void PastingFromClipboard(PastingFromClipboardEventArgs e)
        {
            MainViewModel?.PastingFromClipboard(e);
        }

        public void ShownEditor(EditorEventArgs e)
        {
            MainViewModel?.ShownEditor(e);
        }

        public virtual void TreelistExistingRowSave(TreeListCellValueChangedEventArgs e)
        {
            MainViewModel?.TreelistExistingRowSave(e);
        }

        public virtual void PastingFromClipboardTreeList(PastingFromClipboardEventArgs e)
        {
            MainViewModel?.PastingFromClipboardTreeList(e);
        }

        public bool IsChangingValueFromBackgroundEvents
        {
            get => MainViewModel == null ? false : MainViewModel.IsChangingValueFromBackgroundEvents;
            set
            {
                if (MainViewModel != null)
                    MainViewModel.IsChangingValueFromBackgroundEvents = value;
            }
        }

        public void ShowErrorMessage(string dialogTitle, List<ErrorMessage> errorMessages)
        {
            if (MainViewModel == null)
                return;

            MainViewModel.ShowErrorMessage(dialogTitle, errorMessages);
        }
        #endregion

        #region Button Commands
        public virtual bool CanFullRefresh()
        {
            return !IsLoading && MainViewModel != null;
        }

        public virtual void FullRefresh()
        {
            //don't have to call this because it's not going to be called from the background by user interaction, i.e. Undo/Redo
            //if (!CanFullRefresh())
            //    return;

            if (!stopSubsequentEntitiesLoading && MainViewModel == null)
                return;

            ReloadEntitiesCollection();
            BackgroundRefresh();
        }

        protected virtual void onBeforeReloadingEntitiesCollection()
        {

        }

        public virtual bool CanSaveLayout()
        {
            return !IsLoading;
        }

        public void SaveLayout()
        {
            isLayoutLoaded = true;
            PersistentLayoutHelper.TrySerializeLayout(LayoutSerializationService, ViewName);
            PersistentLayoutHelper.SaveLayout();
        }

        public virtual bool CanResetLayout()
        {
            return !IsLoading && isLayoutLoaded;
        }

        public void ResetLayout()
        {
            if (MessageBoxService.ShowMessage(CommonResources.Confirmation_ResetLayout, CommonResources.Confirmation_Caption, MessageButton.YesNo) != MessageResult.Yes)
                return;

            isLayoutLoaded = false;
            PersistentLayoutHelper.ResetLayout(ViewName);
        }

        public virtual bool CanCopyWithHeader()
        {
            return !IsLoading && GridControlService != null;
        }

        public virtual void CopyWithHeader()
        {
            GridControlService.CopyWithHeader();
        }

        public bool CanExpandAllGroups()
        {
            return !IsLoading && GridControlService != null && GridControlService.GridControl.IsGrouped;
        }

        public virtual void ExpandAllGroups()
        {
            GridControlService.ExpandAllGroups();
        }

        public bool CanCollapseAllGroups()
        {
            return !IsLoading && GridControlService != null && GridControlService.GridControl.IsGrouped;
        }

        public virtual void CollapseAllGroups()
        {
            GridControlService.CollapseAllGroups();
        }

        public virtual bool CanBulkDelete()
        {
            return !IsLoading && MainViewModel != null;
        }

        public virtual void BulkDelete()
        {
            MainViewModel?.BulkDelete();
        }

        public virtual bool CanUndo()
        {
            return !IsLoading && MainViewModel != null && MainViewModel.CanUndo();
        }

        public virtual void Undo()
        {
            if (!CanUndo())
                return;

            MainViewModel?.Undo();
        }

        public virtual bool CanRedo()
        {
            return !IsLoading && MainViewModel != null && MainViewModel.CanRedo();
        }

        public virtual void Redo()
        {
            if (!CanRedo())
                return;

            MainViewModel?.Redo();
        }

        public virtual bool CanKeyboardCopy()
        {
            return !IsLoading;
        }

        public virtual void KeyboardCopy()
        {
            if (!CanKeyboardCopy())
                return;

            SendKeys.SendWait("^c");
        }

        public virtual bool CanKeyboardPaste()
        {
            return !IsLoading;
        }

        public virtual void KeyboardPaste()
        {
            if (!CanKeyboardPaste())
                return;

            SendKeys.SendWait("^v");
        }

        public virtual bool CanDeleteCellContent(GridControl gridControl)
        {
            return !IsLoading;
        }

        public virtual void DeleteCellContent(GridControl gridControl)
        {
            MainViewModel?.DeleteCellContent(gridControl);
        }

        public virtual bool CanBulkColumnEdit(object button)
        {
            return !IsLoading && MainViewModel != null && MainViewModel.CanBulkColumnEdit(button);
        }

        public virtual void BulkColumnEdit(object button)
        {
            MainViewModel?.BulkColumnEdit(button);
        }

        public virtual bool CanFillUp(object button)
        {
            return !IsLoading && MainViewModel != null && MainViewModel.CanFillUp(button);
        }

        public virtual void FillUp(object button)
        {
            MainViewModel?.FillUp(button);
        }

        public virtual bool CanFillDown(object button)
        {
            return !IsLoading && MainViewModel != null && MainViewModel.CanFillDown(button);
        }

        public virtual void FillDown(object button)
        {
            MainViewModel?.FillDown(button);
        }
        #endregion

        #region IDocumentContent

        protected IDocumentOwner DocumentOwner { get; private set; }

        object IDocumentContent.Title
        {
            get { return null; }
        }

        public abstract string ViewName { get; }

        //because onloaded will be called repeatedly during tab switching, only allow it to be invoked once
        protected bool isFirstLoaded;
        public virtual void OnLoaded()
        {
            //PersistentLayoutHelper.TryDeserializeLayout(LayoutSerializationService, ViewName);
        }

        private bool? isLoading;
        public virtual bool IsLoading
        {
            get
            {
                if (this.IsInDesignMode())
                    return true;

                if (isLoading != null && (bool)isLoading)
                    return true;

                if (MainViewModel == null)
                    return true;

                //assuming RaisePropertyChanged will be always be called upon on MainViewModel entities loaded
                return MainViewModel.IsLoading;
            }
            set
            {
                isLoading = value;
            }
        }

        protected virtual void OnClose(CancelEventArgs e)
        {
            viewRefreshBackgroundWorker.CancelAsync();
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


        protected virtual string ExportFilename()
        {
            return "grid_export";
        }

        public virtual bool CanExportToExcel()
        {
            return !IsLoading;
        }

        protected bool isExcelExportDataAware = true;
        public virtual void ExportToExcel()
        {
            string ResultPath = string.Empty;
            if (FolderBrowserDialogService.ShowDialog())
            {
                ResultPath = FolderBrowserDialogService.ResultPath;
                bool result = TableViewService.ExportToXls(ResultPath + "\\" + ExportFilename() + ".xlsx", isExcelExportDataAware);

                if (!result)
                    MessageBoxService.ShowMessage("Export failed because the file is in use", "Warning", MessageButton.OK, MessageIcon.Warning);
            }
        }

        public virtual bool CanExportToPDF()
        {
            return !IsLoading;
        }

        public virtual void ExportToPDF()
        {
            string ResultPath = string.Empty;
            if (FolderBrowserDialogService.ShowDialog())
            {
                ResultPath = FolderBrowserDialogService.ResultPath;
                bool result = TableViewService.ExportToPDF(ResultPath + "\\" + ExportFilename() + ".pdf");

                if (!result)
                    MessageBoxService.ShowMessage("Export failed because the file is in use");
            }
        }
        #endregion

        #region Services
        [ServiceProperty(Key = "DefaultGridControlService")]
        public virtual IGridControlService GridControlService { get { return null; } }
        //protected virtual IGridControlService GridControlService { get { return this.GetService<IGridControlService>("DefaultGridControlService"); } }

        [ServiceProperty(Key = "DefaultTableViewService")]
        protected virtual ITableViewService TableViewService { get { return null; } }
        //protected virtual ITableViewService TableViewService { get { return this.GetService<ITableViewService>("DefaultTableViewService"); } }

        //because this is specified in DefaultControlResourceDictionary.CollectionView.BaseRootContainer, it is save to assume we have it in xaml
        protected IDialogService BulkColumnEditDialogService
        {
            get { return this.GetRequiredService<IDialogService>("BulkColumnEditService"); }
        }

        //because this is specified in DefaultControlResourceDictionary.CollectionView.BaseRootContainer, it is save to assume we have it in xaml
        protected IDialogService ErrorMessagesDialogService
        {
            get { return this.GetRequiredService<IDialogService>("ErrorMessagesDialogService"); }
        }

        protected virtual ITreeViewService TreeViewService { get { return this.GetService<ITreeViewService>(); } }
        protected virtual ITreeListControlService TreeListControlService { get { return this.GetService<ITreeListControlService>(); } }
        protected virtual IFolderBrowserDialogService FolderBrowserDialogService { get { return this.GetService<IFolderBrowserDialogService>(); } }
        protected virtual IOpenFileDialogService FileBrowserDialogService { get { return this.GetService<IOpenFileDialogService>(); } }

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

        #region Messages
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

        #region Database behaviour
        protected virtual OperationInterceptMode OnBeforeProjectionSaveIsContinue(TMainProjectionEntity projection, out bool isNew)
        {
            isNew = false;
            return OperationInterceptMode.Continue;
        }

        protected virtual OperationInterceptMode OnBeforeProjectionDeleteIsContinue(TMainProjectionEntity projection, out List<ErrorMessage> errorMessages)
        {
            errorMessages = new List<ErrorMessage>();
            return OperationInterceptMode.Continue;
        }

        protected virtual void OnBeforeProjectionsDelete(IEnumerable<TMainProjectionEntity> projections)
        {

        }

        protected virtual void OnAfterProjectionsDeleted(IEnumerable<TMainProjectionEntity> projections)
        {

        }

        protected virtual void OnAfterProjectionSave(TMainProjectionEntity projection, TMainEntity entity, bool isNew)
        {

        }

        protected virtual void OnAfterProjectionsSave(IEnumerable<TMainProjectionEntity> projections)
        {

        }
        #endregion

        #region View Behavior
        public bool CanCustomColumnGroup(CustomColumnSortEventArgs e)
        {
            return !IsLoading;
        }

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

        /// <summary>
        /// Used to populate cell lookup properties in projection so when value is changed from a cell another combobox type cell values can be filtered
        /// </summary>
        /// <param name="projection">New projection</param>
        public virtual void UnifiedNewRowInitializationFromView(TMainProjectionEntity projection)
        {

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
        /// Routine used by copy paste, fill, new and existing row cell value changed to determine which other cells to affect or commit to database different from MainViewModel context
        /// </summary>
        /// <param name="field_name">Field name changed</param>
        /// <param name="old_value">Old value currently in projection</param>
        /// <param name="new_value">New value that projection is going to use</param>
        /// <param name="projection">Changed projection</param>
        /// <param name="isNew">Is new row</param>
        public virtual void UnifiedCellValueChanged(string field_name, object old_value, object new_value, TMainProjectionEntity projection, bool isNew)
        {

        }

        /// <summary>
        /// Routine used by copy paste, fill, new and existing row cell value changing to determine whether value is valid
        /// </summary>
        /// <param name="projection">Changed projection</param>
        /// <param name="field_name">Field name changed</param>
        /// <param name="new_value">New value that projection is going to use</param>
        /// <param name="error_message">Default is empty string, set value to indicate error</param>
        public abstract string UnifiedValueValidation(TMainProjectionEntity projection, string field_name, object new_value, bool isPaste);

        public abstract string UnifiedRowValidation(TMainProjectionEntity projection);

        protected virtual void FormatErrorMessages(IEnumerable<ErrorMessage> errorMessages)
        {
            if(GridControlService != null)
            {
                GridColumnCollection gridColumns = GridControlService.GridColumns();
                foreach (ErrorMessage errorMessage in errorMessages)
                {
                    if (errorMessage.CONSTRAINT_ISSUES.Count > 0)
                    {
                        string newErrorMessage = string.Empty;
                        IEnumerable<KeyValuePair<string, string>> validIssues = errorMessage.CONSTRAINT_ISSUES.Where(x => x.Value != string.Empty);
                        foreach (KeyValuePair<string, string> constraintIssue in validIssues)
                        {
                            GridColumn gridColumn = gridColumns.FirstOrDefault(x => x.FieldName == constraintIssue.Key);
                            if (gridColumn != null)
                            {
                                if (constraintIssue.Key == validIssues.Last().Key && constraintIssue.Key != validIssues.First().Key)
                                {
                                    newErrorMessage = newErrorMessage.Substring(0, newErrorMessage.Length - 2);
                                    newErrorMessage += " and ";
                                }

                                object cellTemplate = gridColumn.CellTemplate;
                                DataTemplate dataTemplate = cellTemplate as DataTemplate;
                                Type editSettingsType = null;
                                //try to retrieve data template type for RowData.Row items source binding
                                editSettingsType = gridColumn.ActualEditSettings.GetType();

                                string displayValue = constraintIssue.Value;
                                object editSettings = null;
                                if (editSettingsType == typeof(ComboBoxEditSettings))
                                    editSettings = gridColumn.ActualEditSettings as ComboBoxEditSettings;
                                else if (editSettingsType == typeof(LookUpEditSettings))
                                    editSettings = gridColumn.ActualEditSettings as LookUpEditSettingsBase;
                                if (editSettings != null)
                                {
                                    displayValue = DataUtils.GetEditSettingsDisplayMemberValue(editSettings, constraintIssue.Value);
                                }

                                newErrorMessage += "[" + gridColumn.Header.ToString() + "] = " + displayValue + ", ";
                            }
                        }

                        if(newErrorMessage != string.Empty)
                        {
                            newErrorMessage = newErrorMessage.Substring(0, newErrorMessage.Length - 2);
                            errorMessage.ERROR = "Entry already exist for " + newErrorMessage;
                        }
                    }
                }
            }
        }

        protected virtual void CellValueChangedImmediatePost(CellValueChangedEventArgs e)
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
        where TMainProjectionEntity : class, new()
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

        bool AlwaysSkipMessage { get; set; }
    }
}