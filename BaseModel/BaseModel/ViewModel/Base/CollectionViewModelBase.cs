using BaseModel.Data.Helpers;
using BaseModel.DataModel;
using BaseModel.Misc;
using BaseModel.View;
using BaseModel.ViewModel.Dialogs;
using BaseModel.ViewModel.Document;
using BaseModel.ViewModel.UndoRedo;
using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Windows.Threading;

namespace BaseModel.ViewModel.Base
{
    /// <summary>
    /// The base class for POCO view models exposing a collection of entities of a given type and CRUD operations against these entities.
    /// It is not recommended to inherit directly from this class. Use the CollectionViewModel class instead.
    /// </summary>
    /// <typeparam name="TEntity">A repository entity type.</typeparam>
    /// <typeparam name="TProjection">A projection entity type.</typeparam>
    /// <typeparam name="TPrimaryKey">A primary key value type.</typeparam>
    /// <typeparam name="TUnitOfWork">A unit of work type.</typeparam>
    public abstract class CollectionViewModelBase<TEntity, TProjection, TPrimaryKey, TUnitOfWork> :
        ReadOnlyCollectionViewModel<TEntity, TProjection, TUnitOfWork>, ISupportLogicalLayout
        where TEntity : class, new()
        where TProjection : class
        where TUnitOfWork : IUnitOfWork
    {
        #region Call Backs
        /// <summary>
        /// Fires when selected entities is changed
        /// Used by dashboard to generate chart
        /// </summary>
        public Action OnSelectedEntitiesChangedCallBack;

        /// <summary>
        /// Map projection entity properties to main entity properties
        /// </summary>
        public Func<TProjection, TEntity, bool> OnBeforeApplyingProjectionPropertiesToEntityIsContinueCallBack;

        /// <summary>
        /// Used to populate lookup cell values, that is required immediately for new item row comboboxes to work when it's collection is binded to rowdata
        /// </summary>
        public Action<TProjection> UnifiedNewRowInitialisationFromView;

        /// <summary>
        /// Additional initialization parameter apart from SetParentAssociationCallBack from CollectionViewModelBase when RowEventArgs is needed
        /// e.g. Retrieving master row from child to set parent association
        /// </summary>
        public Func<RowEventArgs, TProjection, bool> OnBeforeNewRowSavedIsContinueFromViewCallBack;

        /// <summary>
        /// Apply additional entity property before saving, or intercept entire save operation, when it's intercepted, undo and focus new rows need to be manually handled
        /// </summary>
        public delegate OperationInterceptMode EntitySaveDelegate(TProjection projection, out bool isNew);
        public EntitySaveDelegate OnBeforeProjectionSaveIsContinueCallBack;

        /// <summary>
        /// Save projection associated entity, e.g. save user address to another table when user is saved
        /// Undo/Redo manager should be tied to main entity CollectionViewModel and this will be used to handle associated entity save
        /// Only called when main entity is successfully saved
        /// Any associating changes to the collection must be placed here so undo/redo will be in effect
        /// </summary>
        public Action<TProjection, TEntity, bool> OnAfterProjectionSavedCallBack;

        /// <summary>
        /// Allow unit of work to bulk save on db saves skip operation
        /// </summary>
        public Action<IEnumerable<TProjection>> OnAfterProjectionsSavedCallBack;

        /// <summary>
        /// For detail validation on whether deletion can continue
        /// </summary>
        public delegate OperationInterceptMode EntityDeleteDelegate(TProjection projection, out List<ErrorMessage> errorMessages);
        public EntityDeleteDelegate OnBeforeProjectionDeleteIsContinueCallBack;

        /// <summary>
        /// Process the collection before projections are deleted, e.g. renumbering/renaming remaining entities
        /// </summary>
        public Action<IEnumerable<TProjection>> OnBeforeProjectionsDeleteCallBack;

        /// <summary>
        /// Process the collection after projections are deleted, e.g. renumbering/renaming remaining entities
        /// </summary>
        public Action<IEnumerable<TProjection>> OnAfterProjectionsDeletedCallBack;

        /// <summary>
        /// Used for sending SignalR messages after deletion
        /// </summary>
        public Action<string, string, string, string> OnAfterDeletedSendMessageCallBack;

        /// <summary>
        /// Used for sending SignalR messages after saving
        /// </summary>
        public Action<string, string, string, string> OnAfterSavedSendMessageCallBack;

        /// <summary>
        /// For refreshing without clearing undo/redo
        /// </summary>
        public Action FullRefreshWithoutClearingUndoRedoCallBack;

        /// <summary>
        /// External call back used to format error messages
        /// </summary>
        public Action<IEnumerable<ErrorMessage>> FormatErrorMessagesCallBack;

        protected IDialogService ErrorMessagesDialogService
        {
            get { return this.GetRequiredService<IDialogService>("ErrorMessagesDialogService"); }
        }

        #region Selected Entities
        /// <summary>
        /// The selected entities.
        /// Since CollectionViewModel is a POCO view model, this property will raise INotifyPropertyChanged.PropertyEvent when modified so it can be used as a binding source in views.
        /// </summary>
        protected ObservableCollection<TProjection> selectedentities { get; set; }
        public ObservableCollection<TProjection> SelectedEntities
        {
            get { return selectedentities; }
            set { selectedentities = value; }
        }

        //so that bulk refresh don't get called multiple times within a short duration
        private DispatcherTimer selectedEntitiesChangedDispatchTimer;
        protected System.Timers.Timer postLoadedTimer;
        DispatcherTimer focusNewlyAddedProjectionTimer = new DispatcherTimer();
        //indicate that pasting is in effect, so add newly committed entities manually
        public bool IsPasting;
        #endregion
        #endregion

        /// <summary>
        /// Initializes a new instance of the CollectionViewModelBase class.
        /// </summary>
        /// <param name="unitOfWorkFactory">A factory used to create a unit of work instance.</param>
        /// <param name="getRepositoryFunc">A function that returns a repository representing entities of the given type.</param>
        /// <param name="projection">A LINQ function used to customize a query for entities. The parameter, for example, can be used for sorting data and/or for projecting data to a custom type that does not match the repository entity type.</param>
        protected CollectionViewModelBase(IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory, Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc, Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> projection)
            : base(unitOfWorkFactory, getRepositoryFunc, projection)
        {
            SelectedEntities = new ObservableCollection<TProjection>();
            selectedentities.CollectionChanged += DelayedOnSelectedEntitiesChanged;
            selectedEntitiesChangedDispatchTimer = new DispatcherTimer();
            selectedEntitiesChangedDispatchTimer.Interval = new TimeSpan(0, 0, 0, 0, 10);
            focusNewlyAddedProjectionTimer = new DispatcherTimer();
            focusNewlyAddedProjectionTimer.Interval = new TimeSpan(0, 0, 0, 0, 1);
        }

        #region View methods
        private void DelayedOnSelectedEntitiesChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //multiple selection will call this multiple times. so this is used to remove unecessary calls
            selectedEntitiesChangedDispatchTimer.Tick -= selectedEntitiesChangedDispatcherTimer_Tick;
            selectedEntitiesChangedDispatchTimer.Tick += selectedEntitiesChangedDispatcherTimer_Tick;
            selectedEntitiesChangedDispatchTimer.Start();
        }

        private void selectedEntitiesChangedDispatcherTimer_Tick(object sender, EventArgs e)
        {
            selectedEntitiesChangedDispatchTimer.Stop();
            OnSelectedEntitiesChanged();
        }

        protected virtual void OnSelectedEntitiesChanged()
        {
            RefreshSelectedEntities();
        }

        public virtual void RefreshSelectedEntities()
        {
            this.RaisePropertyChanged(x => x.SelectedEntities);
            this.RaisePropertyChanged(x => x.SelectedEntity);
            OnSelectedEntitiesChangedCallBack?.Invoke();
        }

        List<TProjection> newlyAddedProjections;
        public virtual void OnAfterNewProjectionsAdded(IEnumerable<TProjection> newItems)
        {
            if (newItems.Count() > 0)
            {
                if (newlyAddedProjections == null)
                    newlyAddedProjections = new List<TProjection>();

                newlyAddedProjections.AddRange(newItems);
                //Uncomment this to allow grid to focus on new row
                focusNewlyAddedProjectionTimer.Tick -= FocusNewlyAddedProjectionTimer_Tick;
                focusNewlyAddedProjectionTimer.Tick += FocusNewlyAddedProjectionTimer_Tick;
                focusNewlyAddedProjectionTimer.Start();
            }
        }

        private void FocusNewlyAddedProjectionTimer_Tick(object sender, EventArgs e)
        {
            focusNewlyAddedProjectionTimer.Stop();
            if (Entities == null || newlyAddedProjections.Count() == 0)
                return;

            List<TProjection> selectedProjections = new List<TProjection>();
            var keyPropertyInfo = DataUtils.GetKeyPropertyInfo(typeof(TProjection));

            if (keyPropertyInfo != null)
            {
                object findKeyValue = null;
                if (keyPropertyInfo != null)
                {
                    foreach (TProjection newlyAddedProjection in newlyAddedProjections)
                    {
                        findKeyValue = keyPropertyInfo.GetValue(newlyAddedProjection);
                        if(findKeyValue != null)
                        {
                            try
                            {
                                TProjection actualNewlyAddedProjection = Entities.Where(x => x != null).FirstOrDefault(x => keyPropertyInfo.GetValue(x) != null && keyPropertyInfo.GetValue(x).ToString() == findKeyValue.ToString());
                                if (actualNewlyAddedProjection != null)
                                    selectedProjections.Add(actualNewlyAddedProjection);
                            }
                            catch
                            {

                            }
                        }
                    }
                }
            }
            else
            {
                foreach (TProjection newlyAddedProjection in newlyAddedProjections)
                {
                    selectedProjections.Add(newlyAddedProjection);
                }
            }

            newlyAddedProjections.Clear();
            SelectedEntities?.Clear();
            foreach (TProjection selectedProjection in selectedProjections)
            {
                SelectedEntities?.Add(selectedProjection);
            }

            if (selectedProjections.Count > 0)
            {
                SelectedEntity = selectedProjections.Last();
                RefreshSelectedEntities();
            }
        }
        #endregion

        private void UpdateCommands()
        {
            TProjection projectionEntity = null;
            this.RaiseCanExecuteChanged(x => x.Edit(projectionEntity));
            this.RaiseCanExecuteChanged(x => x.Delete(projectionEntity));
            this.RaiseCanExecuteChanged(x => x.Save(projectionEntity));
        }

        private EntitiesChangeTracker<TPrimaryKey> ChangeTrackerWithKey
        {
            get { return (EntitiesChangeTracker<TPrimaryKey>)ChangeTracker; }
        }

        private IRepository<TEntity, TPrimaryKey> Repository
        {
            get { return (IRepository<TEntity, TPrimaryKey>)ReadOnlyRepository; }
        }

        protected virtual void ApplyProjectionPropertiesToEntity(TProjection projectionEntity, TEntity entity)
        {
            if(OnBeforeApplyingProjectionPropertiesToEntityIsContinueCallBack != null)
            {
                if (!OnBeforeApplyingProjectionPropertiesToEntityIsContinueCallBack.Invoke(projectionEntity, entity))
                    return;
            }

            //when it's the same type but one is EF object and the other is POCO initialised from view
            TEntity projectionObject = projectionEntity as TEntity;
            if(projectionObject != null)
            {
                if (entity.GetHashCode() != projectionObject.GetHashCode())
                {
                    //need to set created date because POCO have incompatible min date with Db
                    IHaveCreatedDate iHaveCreatedDateObject = projectionObject as IHaveCreatedDate;
                    if(iHaveCreatedDateObject != null)
                        iHaveCreatedDateObject.EntityCreatedDate = DateTime.Now;

                    DataUtils.ShallowCopy(entity, projectionObject);
                }
            }
            //when it's a projection type
            else
            {
                IProjection<TEntity> projection = projectionEntity as IProjection<TEntity>;
                if (projection != null)
                    DataUtils.ShallowCopy(entity, projection.Entity);
            }
        }

        protected override IEntitiesChangeTracker CreateEntitiesChangeTracker()
        {
            return new EntitiesChangeTracker<TPrimaryKey>(this);
        }

        protected IRepository<TEntity, TPrimaryKey> CreateRepository()
        {
            return (IRepository<TEntity, TPrimaryKey>)CreateReadOnlyRepository();
        }

        protected void DestroyDocument(IDocument document)
        {
            if (document != null)
                document.Close();
        }

        protected TProjection FindLocalProjectionByKey(TPrimaryKey projectionKey)
        {
            return ChangeTrackerWithKey.FindLocalProjectionByKey(projectionKey);
        }

        protected virtual void OnEntityDeleted(TPrimaryKey primaryKey, TEntity entity, bool willPerformBulkRefresh = false)
        {
            Messenger.Default.Send(new EntityMessage<TEntity, TPrimaryKey>(primaryKey, this.Key, EntityMessageType.Deleted, this, CurrentHWID, willPerformBulkRefresh));
            OnAfterDeletedSendMessageCallBack?.Invoke(typeof(TEntity).ToString(), primaryKey.ToString(), EntityMessageType.Deleted.ToString(), ToString());
        }

        protected virtual void SendMessage(TPrimaryKey primaryKey, TProjection projectionEntity, TEntity entity,
            bool isNewEntity, bool willPerformBulkRefresh = false)
        {
            //ApplyEntityPropertiesToProjectionCallBack?.Invoke(primaryKey, projectionEntity, entity, isNewEntity);

            try
            {
                Messenger.Default.Send(new EntityMessage<TEntity, TPrimaryKey>(primaryKey, this.Key, isNewEntity ? EntityMessageType.Added : EntityMessageType.Changed, this, CurrentHWID, willPerformBulkRefresh));
                OnAfterSavedSendMessageCallBack?.Invoke(typeof(TEntity).ToString(), primaryKey.ToString(),
                isNewEntity ? EntityMessageType.Added.ToString() : EntityMessageType.Changed.ToString(), ToString());
            }
            catch(Exception e)
            {
                string s = e.ToString();
            }
        }

        protected override void OnSelectedEntityChanged()
        {
            base.OnSelectedEntityChanged();
            UpdateCommands();
        }

        protected override void RestoreSelectedEntity(TProjection existingProjectionEntity,
            TProjection newProjectionEntity)
        {
            base.RestoreSelectedEntity(existingProjectionEntity, newProjectionEntity);
            if (ReferenceEquals(SelectedEntity, existingProjectionEntity))
                SelectedEntity = newProjectionEntity;
        }

        protected IEnumerable<TPrimaryKey> RetrieveLocalProjectionsEntitiesKey(
            IEnumerable<TProjection> projectionEntities)
        {
            var returningEntitiesKey = new List<TPrimaryKey>();
            foreach (var projectionEntity in projectionEntities)
            {
                var primaryKeyAvailable = projectionEntity != null &&
                                           Repository.ProjectionHasPrimaryKey(projectionEntity);
                if (primaryKeyAvailable)
                    returningEntitiesKey.Add(Repository.GetProjectionPrimaryKey(projectionEntity));
            }
            return returningEntitiesKey.AsEnumerable();
        }

        protected IDocumentManagerService DocumentManagerService
        {
            get { return this.GetService<IDocumentManagerService>(); }
        }

        protected IMessageBoxService MessageBoxService
        {
            get { return this.GetRequiredService<IMessageBoxService>(); }
        }

        public override string ViewName
        {
            get { return typeof(TEntity).Name + "CollectionView"; }
        }

        public virtual void BaseBulkDelete(IEnumerable<TProjection> projections)
        {
            List<BulkProcessModel<TProjection, TEntity>> bulkProcessModels = new List<BulkProcessModel<TProjection, TEntity>>();
            bulkProcessModels.AddRange(projections.Select(x => new BulkProcessModel<TProjection, TEntity>() { Projection = x }));

            try
            {
                if (projections.Count() > Int32.Parse(CommonResources.BulkOperationLoadingScreenMinCount))
                {
                    LoadingScreenManager.ShowLoadingScreen(projections.Count());
                    LoadingScreenManager.SetMessage("Deleting...");
                }

                PauseEntitiesUndoRedoManager();
                OnBeforeProjectionsDeleteCallBack?.Invoke(projections);
                List<ErrorMessage> errorMessages = new List<ErrorMessage>();
                string errorMessageDialogTitle = "The following data cannot be deleted";
                bool skipDbSave = false;
                foreach (BulkProcessModel<TProjection, TEntity> bulkProcessModel in bulkProcessModels)
                {
                    if (OnBeforeProjectionDeleteIsContinueCallBack != null)
                    {
                        AddUndoBeforeEntityDeleted(bulkProcessModel.Projection);
                        List<ErrorMessage> currentProjectionErrorMessages;
                        OperationInterceptMode interceptMode = OnBeforeProjectionDeleteIsContinueCallBack(bulkProcessModel.Projection, out currentProjectionErrorMessages);
                        if (currentProjectionErrorMessages.Count > 0)
                        {
                            errorMessages.AddRange(currentProjectionErrorMessages);
                            continue;
                        }

                        if (interceptMode == OperationInterceptMode.SkipOne)
                            continue;
                        else if (interceptMode == OperationInterceptMode.SkipOneAndAllDbSaves)
                        {
                            skipDbSave = true;
                            if (!IsPersistentView)
                                Entities.Remove(bulkProcessModel.Projection);
                            continue;
                        }
                        else if (interceptMode == OperationInterceptMode.SkipAll)
                        {
                            UnpauseEntitiesUndoRedoManager();
                            LoadingScreenManager.CloseLoadingScreen();
                            ShowErrorMessage(errorMessageDialogTitle, errorMessages);
                            return;
                        }

                        if (!IsPersistentView)
                            Entities.Remove(bulkProcessModel.Projection);
                    }

                    if(bulkProcessModels.Count > 0 && !skipDbSave)
                    {
                        var primaryKey = Repository.GetProjectionPrimaryKey(bulkProcessModel.Projection);
                        var entity = Repository.Find(primaryKey);
                        if (entity != null)
                        {
                            bulkProcessModel.RepositoryEntity = entity;
                            Repository.Remove(entity);

                            OnEntityDeleted(primaryKey, entity);
                        }
                    }

                    LoadingScreenManager.Progress();
                }

                UnpauseEntitiesUndoRedoManager();

                if (!skipDbSave)
                    Repository.UnitOfWork.SaveChanges();

                OnAfterProjectionsDeletedCallBack?.Invoke(bulkProcessModels.Select(x => x.Projection));
                LoadingScreenManager.CloseLoadingScreen();
                ShowErrorMessage(errorMessageDialogTitle, errorMessages);
            }
            catch (DbException e)
            {
                MessageBoxService.ShowMessage(e.ErrorMessage, e.ErrorCaption, MessageButton.OK, MessageIcon.Error);
            }
        }

        public bool ShowErrorMessage(string dialogTitle, IEnumerable<ErrorMessage> errorMessages)
        {
            if (errorMessages.Count() > 0)
            {
                FormatErrorMessagesCallBack?.Invoke(errorMessages);

                if (ErrorMessagesDialogService != null)
                {
                    DialogCollectionViewModel<ErrorMessage> viewModel = DialogCollectionViewModel<ErrorMessage>.Create(errorMessages, dialogTitle);
                    if (ErrorMessagesDialogService.ShowDialog(MessageButton.OKCancel, string.Empty, "ListErrorMessages", viewModel) == MessageResult.OK)
                        return true;
                }
            }

            return false;
        }

        protected void ApplyCreatedDateToEntity(object entity)
        {
            if (entity != null)
            {
                IHaveCreatedDate iHaveCreatedDateProjectionEntity = entity as IHaveCreatedDate;
                if (iHaveCreatedDateProjectionEntity != null)
                {
                    //workaround for created because Save() only sets the projection primary key, this is used for property redo where the interceptor only tampers with UPDATED and CREATED is left as null
                    if (iHaveCreatedDateProjectionEntity.EntityCreatedDate.Date.Year == 1)
                        iHaveCreatedDateProjectionEntity.EntityCreatedDate = DateTime.Now;
                }
            }
        }

        public void SaveChangesDirectly()
        {
            Repository.UnitOfWork.SaveChanges();
        }

        public override void Refresh()
        {
            ISupportUndoRedo<TProjection> ISupportUndoRedoViewModel = this as ISupportUndoRedo<TProjection>;
            if (ISupportUndoRedoViewModel != null)
                ISupportUndoRedoViewModel.EntitiesUndoRedoManager.Clear();

            base.Refresh();
        }

        public virtual void RefreshWithoutClearingUndoManager()
        {
            base.Refresh();
        }

        /// <summary>
        /// Determines whether an entity can be deleted.
        /// Since CollectionViewModelBase is a POCO view model, this method will be used as a CanExecute callback for DeleteCommand.
        /// </summary>
        /// <param name="projectionEntity">An entity to edit.</param>
        public virtual bool CanDelete(TProjection projectionEntity)
        {
            return projectionEntity != null && !IsLoading && SelectedEntity != null;
        }

        /// <summary>
        /// Determines whether an entity can be edited.
        /// Since CollectionViewModelBase is a POCO view model, this method will be used as a CanExecute callback for EditCommand.
        /// </summary>
        /// <param name="projectionEntity">An entity to edit.</param>
        public virtual bool CanEdit(TProjection projectionEntity)
        {
            return projectionEntity != null && !IsLoading;
        }

        /// <summary>
        /// Determines whether entity local changes can be saved.
        /// Since CollectionViewModelBase is a POCO view model, this method will be used as a CanExecute callback for SaveCommand.
        /// </summary>
        /// <param name="projectionEntity">An entity to save.</param>
        public virtual bool CanSave(TProjection projectionEntity)
        {
            return projectionEntity != null && !IsLoading;
        }

        /// <summary>
        /// Closes the corresponding view.
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the CloseCommand property that can be used as a binding source in views.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public void Close()
        {
            if (DocumentOwner != null)
                DocumentOwner.Close(this);
        }

        /// <summary>
        /// Deletes a given entity from the repository and saves changes if confirmed by the user.
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the DeleteCommand property that can be used as a binding source in views.
        /// </summary>
        /// <param name="projection">An entity to edit.</param>
        public virtual void Delete(TProjection projection)
        {
            List<TProjection> projections = new List<TProjection>();
            projections.Add(projection);
            BaseBulkDelete(projections);
        }

        /// <summary>
        /// Creates and shows a document that contains a single object view model for the existing entity.
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the EditCommand property that can be used as a binding source in views.
        /// </summary>
        /// <param name="projectionEntity">Entity to edit.</param>
        public virtual void Edit(TProjection projectionEntity)
        {
            if (Repository.IsDetached(projectionEntity))
                return;
            var primaryKey = Repository.GetProjectionPrimaryKey(projectionEntity);
            var index = Entities.IndexOf(projectionEntity);
            projectionEntity = ChangeTrackerWithKey.FindActualProjectionByKey(primaryKey);
            if (index >= 0)
                if (projectionEntity == null)
                    Entities.RemoveAt(index);
                else
                    Entities[index] = projectionEntity;

            if (projectionEntity == null)
            {
                DestroyDocument(DocumentManagerService.FindEntityDocument<TProjection, TPrimaryKey>(primaryKey));
                return;
            }

            DocumentManagerService.ShowExistingEntityDocument<TProjection, TPrimaryKey>(this, primaryKey);
        }

        public virtual TProjection FindActualProjectionByExpression(Expression<Func<TEntity, bool>> predicate)
        {
            return ChangeTrackerWithKey.FindActualProjectionByExpression(predicate);
        }

        /// <summary>
        /// Saves the given entity.
        /// Since CollectionViewModelBase is a POCO view model, the instance of this class will also expose the SaveCommand property that can be used as a binding source in views.
        /// </summary>
        /// <param name="projection">An entity to save.</param>
        [Display(AutoGenerateField = false)]
        public virtual void Save(TProjection projection)
        {
            List<TProjection> projections = new List<TProjection>();
            projections.Add(projection);
            BaseBulkSave(projections);
        }

        /// <summary>
        /// Deletes a given entity from the repository and saves changes if confirmed by the user.
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the DeleteCommand property that can be used as a binding source in views.
        /// </summary>
        /// <param name="projectionEntity">An entity to edit.</param>
        public void BaseBulkSave(IEnumerable<TProjection> projections, bool doNotRefresh = true)
        {
            List<BulkProcessModel<TProjection, TEntity>> bulkProcessModels = new List<BulkProcessModel<TProjection, TEntity>>();
            bulkProcessModels.AddRange(projections.Select(x => new BulkProcessModel<TProjection, TEntity>() { Projection = x }));
            bool showLoadingScreen = false;
            if (bulkProcessModels.Count > Int32.Parse(CommonResources.BulkOperationLoadingScreenMinCount))
                showLoadingScreen = true;

            if(showLoadingScreen)
            {
                LoadingScreenManager.ShowLoadingScreen(projections.Count());
                LoadingScreenManager.SetMessage("Preparing Data...");
            }

            bool skipDbSave = false;
            bool doBulkRefresh = false;

            //when the total count of refreshes exceeds a certain threshold, it's faster to perform bulk refresh, but this will cause grid entries order to be rearranged
            if (projections.Count() > Int32.Parse(CommonResources.BulkOperationBulkRefreshMinCount))
                doBulkRefresh = true;

            PauseEntitiesUndoRedoManager();
            List<TProjection> newlyAddedProjections = new List<TProjection>();
            foreach (var bulkProcessModel in bulkProcessModels)
            {
                bool isNewEntity = false;
                if (OnBeforeProjectionSaveIsContinueCallBack != null)
                {
                    OperationInterceptMode operationInterceptMode = OnBeforeProjectionSaveIsContinueCallBack.Invoke(bulkProcessModel.Projection, out isNewEntity);
                    bulkProcessModel.IsNewEntity = isNewEntity;

                    if (operationInterceptMode == OperationInterceptMode.SkipOne)
                    {
                        if (isNewEntity)
                        {
                            AddUndoBeforeEntityAdded(bulkProcessModel.Projection);
                            newlyAddedProjections.Add(bulkProcessModel.Projection);
                            if (IsInUndoRedoOperation() || IsPasting)
                            {
                                Entities.Add(bulkProcessModel.Projection);
                            }
                        }

                        continue;
                    }
                    else if(operationInterceptMode == OperationInterceptMode.SkipOneAndAllDbSaves)
                    {
                        if (isNewEntity)
                        {
                            AddUndoBeforeEntityAdded(bulkProcessModel.Projection);
                            newlyAddedProjections.Add(bulkProcessModel.Projection);

                            //because this doesn't go through normal messaging mechanism to add new entities, add it manually here
                            if (IsInUndoRedoOperation() || IsPasting)
                            {
                                Entities.Add(bulkProcessModel.Projection);
                            }
                        }

                        skipDbSave = true;
                        continue;
                    }
                    else if(operationInterceptMode == OperationInterceptMode.SkipAll)
                    {
                        UnpauseEntitiesUndoRedoManager();
                        LoadingScreenManager.CloseLoadingScreen();
                        return;
                    }
                }

                if(!skipDbSave)
                {
                    var findOrAddNewEntity = Repository.FindExistingOrAddNewEntity(bulkProcessModel.Projection,
                        (p, e) => { ApplyProjectionPropertiesToEntity(p, e); }, out isNewEntity);

                    bulkProcessModel.RepositoryEntity = findOrAddNewEntity;
                    bulkProcessModel.IsNewEntity = isNewEntity;
                }
                //we don't have to manually add it to the view here because DB save method will use messages to add new entity into repository

                if (isNewEntity)
                {
                    AddUndoBeforeEntityAdded(bulkProcessModel.Projection);
                    newlyAddedProjections.Add(bulkProcessModel.Projection);
                }

                if (showLoadingScreen)
                    LoadingScreenManager.Progress();
            }

            UnpauseEntitiesUndoRedoManager();

            if (showLoadingScreen)
            {
                LoadingScreenManager.CloseLoadingScreen();
                LoadingScreenManager.ShowLoadingScreen(1);
                LoadingScreenManager.SetMessage("Saving...");
            }

            bool isError = false;
            //perform after save operation to map primary key back to TEntity
            if(bulkProcessModels.Count > 0)
            {
                try
                {
                    if(!skipDbSave)
                        Repository.UnitOfWork.SaveChanges();

                    foreach (BulkProcessModel<TProjection, TEntity> bulkProcessModel in bulkProcessModels)
                    {
                        TPrimaryKey primaryKey = default(TPrimaryKey);
                        if (!skipDbSave)
                        {
                            primaryKey = Repository.GetPrimaryKey(bulkProcessModel.RepositoryEntity);
                            if (bulkProcessModel.IsNewEntity)
                                Repository.SetProjectionPrimaryKey(bulkProcessModel.Projection, primaryKey);
                        }

                        //Need to put here because any updates associated with the entity need to be committed before sending message
                        OnAfterProjectionSavedCallBack?.Invoke(bulkProcessModel.Projection, bulkProcessModel.RepositoryEntity, bulkProcessModel.IsNewEntity);

                        if (!skipDbSave)
                            //if (!doBulkRefresh && !AlwaysSkipMessage)
                            if (!doBulkRefresh)
                                SendMessage(primaryKey, bulkProcessModel.Projection, bulkProcessModel.RepositoryEntity, bulkProcessModel.IsNewEntity, doBulkRefresh);
                    }
                }
                catch (DbException e)
                {
                    isError = true;
                    MessageBoxService.ShowMessage(e.ErrorMessage, e.ErrorCaption, MessageButton.OK, MessageIcon.Error);
                }
            }

            OnAfterProjectionsSavedCallBack?.Invoke(bulkProcessModels.Select(x => x.Projection));
            if (!isError)
            {
                OnAfterNewProjectionsAdded(newlyAddedProjections);
                if (doNotRefresh)
                {
                    if (typeof(TEntity).GetInterfaces().Contains(typeof(ICanUpdate)))
                        bulkProcessModels.ForEach(x => ((ICanUpdate)x.Projection).Update());
                }

                if ((doBulkRefresh && !doNotRefresh) && FullRefreshWithoutClearingUndoRedoCallBack != null)
                    FullRefreshWithoutClearingUndoRedoCallBack.Invoke();
            }

            if (showLoadingScreen)
                LoadingScreenManager.CloseLoadingScreen();
        }

        /// <summary>
        /// Notifies that SelectedEntity has been changed by raising the PropertyChanged event.
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the UpdateSelectedEntityCommand property that can be used as a binding source in views.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public virtual void UpdateSelectedEntity()
        {
            this.RaisePropertyChanged(x => x.SelectedEntity);
        }

        /// <summary>
        /// The display name of TEntity to be used when presenting messages to the user.
        /// </summary>
        public virtual string EntityDisplayName
        {
            get { return typeof(TEntity).Name; }
        }

        public Func<IDocumentManagerService> OverrideGetDocumentManagerService { get; set; }

        #region SelectEntityMessage

        protected class SelectEntityMessage
        {
            public SelectEntityMessage(TPrimaryKey primaryKey)
            {
                PrimaryKey = primaryKey;
            }

            public TPrimaryKey PrimaryKey { get; private set; }
        }

        protected class SelectedEntityRequest
        {
        }

        private void OnSelectEntityMessage(SelectEntityMessage message)
        {
            if (!IsLoaded)
                return;
            var projectionEntity = ChangeTrackerWithKey.FindActualProjectionByKey(message.PrimaryKey);
            if (projectionEntity == null)
            {
                FilterExpression = null;
                projectionEntity = ChangeTrackerWithKey.FindActualProjectionByKey(message.PrimaryKey);
            }
            SelectedEntity = projectionEntity;
        }
        #endregion

        #region ISupportLogicalLayout

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

        #endregion

        #region BaseModel Customization
        protected virtual void AddUndoBeforeEntityAdded(TProjection projectionEntity)
        {

        }

        protected virtual void AddUndoBeforeEntityDeleted(TProjection projectionEntity)
        {
        }

        protected virtual void PauseEntitiesUndoRedoManager()
        {

        }

        protected virtual void UnpauseEntitiesUndoRedoManager()
        {

        }

        protected virtual bool IsInUndoRedoOperation()
        {
            return false;
        }
        #endregion
    }
}
