using BaseModel.DataModel;
using BaseModel.Misc;
using BaseModel.View;
using BaseModel.ViewModel.Document;
using BaseModel.ViewModel.UndoRedo;
using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;

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
        where TEntity : class
        where TProjection : class
        where TUnitOfWork : IUnitOfWork
    {
        private readonly Func<bool> canCreateNewEntity;
        private readonly Action<TEntity> newEntityInitializer;

        #region Call Backs
        /// <summary>
        /// Map projection entity properties to main entity properties
        /// </summary>
        public Action<TProjection, TEntity> ApplyProjectionPropertiesToEntityCallBack;

        /// <summary>
        /// Apply additional entity property before saving, e.g. Set project guid for an area
        /// </summary>
        public Func<TProjection, bool> OnBeforeEntitySavedIsContinueCallBack;

        /// <summary>
        /// Validate if entity should be saved
        /// </summary>
        public Func<TProjection, bool, bool> IsContinueSaveCallBack;


        public Func<TProjection, bool> OnBeforeEntityDeletedIsContinueCallBack;

        /// <summary>
        /// Save projection associated entity, e.g. save user address to another table when user is saved
        /// Undo/Redo manager should be tied to main entity CollectionViewModel and this will be used to handle associated entity save
        /// Only called when main entity is successfully saved
        /// Any associating changes to the collection must be placed here so undo/redo will be in effect
        /// </summary>
        public Action<TProjection, TEntity, bool> OnAfterEntitySavedCallBack;

        /// <summary>
        /// Process the collection before entities deletion, e.g. deletion of children entities
        /// </summary>
        public Action<IEnumerable<TProjection>> OnBeforeEntitiesDeleteCallBack;

        /// <summary>
        /// Delete projection associated entity, e.g. user address in another table should be deleted when user is deleted
        /// Undo/Redo manager should be tied to main entity CollectionViewModel and this will be used to handle associated entity deletion
        /// </summary>
        public Action<TProjection> OnBeforeEntityDeleteCallBack;

        /// <summary>
        /// Process the collection after entities are deleted, e.g. renumbering/renaming remaining entities
        /// </summary>
        public Action<IEnumerable<TEntity>> OnAfterEntitiesDeletedCallBack;

        /// <summary>
        /// Process the collection after projections are deleted, e.g. renumbering/renaming remaining entities
        /// </summary>
        public Action<IEnumerable<TProjection>> OnAfterProjectionsDeletedCallBack;

        /// <summary>
        /// Used for sending SignalR messages after deletion
        /// </summary>
        public Action<string, string, string, string> OnAfterDeletedSendMessage;

        /// <summary>
        /// Used for sending SignalR messages after saving
        /// </summary>
        public Action<string, string, string, string> OnAfterSavedSendMessage;
        #endregion

        /// <summary>
        /// Initializes a new instance of the CollectionViewModelBase class.
        /// </summary>
        /// <param name="unitOfWorkFactory">A factory used to create a unit of work instance.</param>
        /// <param name="getRepositoryFunc">A function that returns a repository representing entities of the given type.</param>
        /// <param name="projection">A LINQ function used to customize a query for entities. The parameter, for example, can be used for sorting data and/or for projecting data to a custom type that does not match the repository entity type.</param>
        /// <param name="newEntityInitializer">A function to initialize a new entity. This parameter is used in the detail collection view models when creating a single object view model for a new entity.</param>
        /// <param name="canCreateNewEntity">A function that is called before an attempt to create a new entity is made. This parameter is used together with the newEntityInitializer parameter.</param>
        /// <param name="ignoreSelectEntityMessage">A parameter used to specify whether the selected entity should be managed by PeekCollectionViewModel.</param>
        protected CollectionViewModelBase(
            IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory,
            Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc,
            Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> projection,
            Action<TEntity> newEntityInitializer,
            Func<bool> canCreateNewEntity,
            bool ignoreSelectEntityMessage
        )
            : base(unitOfWorkFactory, getRepositoryFunc, projection)
        {
            RepositoryExtensions.VerifyProjection(CreateRepository(), projection);
            this.newEntityInitializer = newEntityInitializer;
            this.canCreateNewEntity = canCreateNewEntity;
            this.ignoreSelectEntityMessage = ignoreSelectEntityMessage;
            if (!this.IsInDesignMode())
                RegisterSelectEntityMessage();
        }

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
            ApplyProjectionPropertiesToEntityCallBack?.Invoke(projectionEntity, entity);
            //else
            //    throw new NotImplementedException(
            //        "Override this method in the collection view model class and apply projection properties to the entity so that it can be correctly saved by unit of work.");
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

        protected virtual void OnBeforeEntityDeleted(TPrimaryKey primaryKey, TEntity entity)
        {
        }

        protected virtual void OnBeforeEntitySaved(TEntity entity)
        {

        }

        protected virtual void OnEntityDeleted(TPrimaryKey primaryKey, TEntity entity, bool willPerformBulkRefresh = false)
        {
            Messenger.Default.Send(new EntityMessage<TEntity, TPrimaryKey>(primaryKey, EntityMessageType.Deleted, this, CurrentHWID, willPerformBulkRefresh));
            OnAfterDeletedSendMessage?.Invoke(typeof(TEntity).ToString(), primaryKey.ToString(), EntityMessageType.Deleted.ToString(), ToString());
        }

        protected virtual void SendMessage(TPrimaryKey primaryKey, TProjection projectionEntity, TEntity entity,
            bool isNewEntity, bool willPerformBulkRefresh = false)
        {
            //ApplyEntityPropertiesToProjectionCallBack?.Invoke(primaryKey, projectionEntity, entity, isNewEntity);

            try
            {
                Messenger.Default.Send(new EntityMessage<TEntity, TPrimaryKey>(primaryKey, isNewEntity ? EntityMessageType.Added : EntityMessageType.Changed, this, CurrentHWID, willPerformBulkRefresh));
                OnAfterSavedSendMessage?.Invoke(typeof(TEntity).ToString(), primaryKey.ToString(),
                isNewEntity ? EntityMessageType.Added.ToString() : EntityMessageType.Changed.ToString(), ToString());
            }
            catch(Exception e)
            {

            }
        }

        protected override void OnIsLoadingChanged()
        {
            base.OnIsLoadingChanged();
            UpdateCommands();
            if (!IsLoading)
                RequestSelectedEntity();
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

        protected override string ViewName
        {
            get { return typeof(TEntity).Name + "CollectionView"; }
        }

        public virtual void BaseBulkDelete(IEnumerable<TProjection> projectionEntities, bool ignoreRefresh = false)
        {
            var projectionEntitiesWithTag = new List<KeyValuePair<int, TProjection>>();
            var entitiesWithTag = new List<KeyValuePair<int, TEntity>>();
            var primaryKeysWithTag = new List<KeyValuePair<int, TPrimaryKey>>();

            var findOrAddNewEntities = new List<TEntity>();
            var projectionEntitiesList = projectionEntities.ToList();
            OnBeforeEntitiesDeleteCallBack?.Invoke(projectionEntities);

            for (var i = 0; i < projectionEntitiesList.Count; i++)
            {
                AddUndoBeforeEntityDeleted(projectionEntitiesList[i]);

                if (OnBeforeEntityDeletedIsContinueCallBack != null)
                    if (!OnBeforeEntityDeletedIsContinueCallBack(projectionEntitiesList[i]))
                        continue;

                OnBeforeEntityDeleteCallBack?.Invoke(projectionEntitiesList[i]);

                Entities.Remove(projectionEntitiesList[i]);
                projectionEntitiesWithTag.Add(new KeyValuePair<int, TProjection>(i, projectionEntitiesList[i]));
            }

            try
            {
                LoadingScreenManager.ShowLoadingScreen(projectionEntitiesWithTag.Count);
                LoadingScreenManager.SetMessage("Deleting...");
                foreach (var projectionEntityWithTag in projectionEntitiesWithTag)
                {
                    var primaryKey = Repository.GetProjectionPrimaryKey(projectionEntityWithTag.Value);
                    var entity = Repository.Find(primaryKey);
                    if (entity != null)
                    {
                        entitiesWithTag.Add(new KeyValuePair<int, TEntity>(projectionEntityWithTag.Key, entity));
                        primaryKeysWithTag.Add(new KeyValuePair<int, TPrimaryKey>(projectionEntityWithTag.Key,
                            primaryKey));
                        OnBeforeEntityDeleted(primaryKey, entity);
                        Repository.Remove(entity);
                    }

                    LoadingScreenManager.Progress();
                }

                Repository.UnitOfWork.SaveChanges();

                foreach (var entityWithTag in entitiesWithTag)
                {
                    var findPrimaryKey = primaryKeysWithTag.First(x => x.Key == entityWithTag.Key).Value;
                    OnEntityDeleted(findPrimaryKey, entityWithTag.Value);
                }

                var entitiesDeleted = entitiesWithTag.Select(x => x.Value).ToList();
                OnAfterEntitiesDeletedCallBack?.Invoke(entitiesDeleted);

                var projectionsDeleted = projectionEntitiesWithTag.Select(x => x.Value).ToList();
                OnAfterProjectionsDeletedCallBack?.Invoke(projectionsDeleted);
            }
            catch (DbException e)
            {
                MessageBoxService.ShowMessage(e.ErrorMessage, e.ErrorCaption, MessageButton.OK, MessageIcon.Error);
            }

            if(!ignoreRefresh)
                AfterBulkOperationRefreshCallBack?.Invoke();
        }

        public Action AfterBulkOperationRefreshCallBack;

        protected virtual void BaseBulkSave(IEnumerable<TProjection> projectionEntities, bool doNotRefresh = false)
        {
            var projectionEntitiesWithTag = new List<KeyValuePair<int, TProjection>>();
            var entitiesWithTag = new List<KeyValuePair<int, TEntity>>();
            var isNewEntityWithTag = new List<KeyValuePair<int, bool>>();

            var findOrAddNewEntities = new List<TEntity>();
            var projectionEntitiesList = projectionEntities.ToList();
            for (var i = 0; i < projectionEntitiesList.Count; i++)
                projectionEntitiesWithTag.Add(new KeyValuePair<int, TProjection>(i, projectionEntitiesList[i]));

            LoadingScreenManager.ShowLoadingScreen(projectionEntitiesWithTag.Count);
            LoadingScreenManager.SetMessage("Saving...");
            bool isContinueSave = true;
            bool haveNewEntity = false;
            foreach (var projectionEntityWithTag in projectionEntitiesWithTag)
            {
                bool isNewEntity;
                if (OnBeforeEntitySavedIsContinueCallBack != null)
                    isContinueSave = OnBeforeEntitySavedIsContinueCallBack(projectionEntityWithTag.Value);

                if (!isContinueSave)
                {
                    LoadingScreenManager.Progress();
                    continue;
                }

                var findOrAddNewEntity = Repository.FindExistingOrAddNewEntity(projectionEntityWithTag.Value,
                    (p, e) => { ApplyProjectionPropertiesToEntity(p, e); }, out isNewEntity);

                if (IsContinueSaveCallBack != null)
                    if (!IsContinueSaveCallBack(projectionEntityWithTag.Value, isNewEntity))
                        continue;

                if (isNewEntity)
                    haveNewEntity = true;

                ApplyCreatedDateToEntity(findOrAddNewEntity);
                entitiesWithTag.Add(new KeyValuePair<int, TEntity>(projectionEntityWithTag.Key, findOrAddNewEntity));
                isNewEntityWithTag.Add(new KeyValuePair<int, bool>(projectionEntityWithTag.Key, isNewEntity));
                OnBeforeEntitySaved(findOrAddNewEntity);
                LoadingScreenManager.Progress();
            }

            if (!isContinueSave)
                return;

            try
            {
                Repository.UnitOfWork.SaveChanges();
                foreach (var entityWithTag in entitiesWithTag)
                {
                    var primaryKey = Repository.GetPrimaryKey(entityWithTag.Value);
                    var projectionEntity = projectionEntitiesWithTag.First(x => x.Key == entityWithTag.Key).Value;
                    var isNewEntity = isNewEntityWithTag.First(x => x.Key == entityWithTag.Key).Value;

                    if(isNewEntity)
                        Repository.SetProjectionPrimaryKey(projectionEntity, primaryKey);

                    //Need to put here because any updates associated with the entity need to be committed before sending message
                    OnAfterEntitySavedCallBack?.Invoke(projectionEntity, entityWithTag.Value, isNewEntity);

                    if(AfterBulkOperationRefreshCallBack == null)
                        SendMessage(primaryKey, projectionEntity, entityWithTag.Value, isNewEntity);

                    if(!haveNewEntity && doNotRefresh)
                    {
                        ICanUpdate updatableEntity = projectionEntity as ICanUpdate;
                        if (updatableEntity != null)
                            updatableEntity.Update();
                    }
                }
            }
            catch (DbException e)
            {
                MessageBoxService.ShowMessage(e.ErrorMessage, e.ErrorCaption, MessageButton.OK, MessageIcon.Error);
            }

            if((haveNewEntity || !doNotRefresh) && AfterBulkOperationRefreshCallBack != null)
                AfterBulkOperationRefreshCallBack.Invoke();
        }

        protected void ApplyCreatedDateToEntity(TEntity entity)
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

        public void SimpleSaveAll()
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
        /// <param name="projectionEntity">An entity to edit.</param>
        public virtual void Delete(TProjection projectionEntity)
        {
            //BaseModel Customization Start
            //if (MessageBoxService.ShowMessage(string.Format(CommonResources.Confirmation_Delete, EntityDisplayName), CommonResources.Confirmation_Caption, MessageButton.YesNo) != MessageResult.Yes)
            //    return;
            //BaseModel Customization End
            try
            {
                //BaseModel Customization Start
                AddUndoBeforeEntityDeleted(projectionEntity);

                if (OnBeforeEntityDeletedIsContinueCallBack != null)
                    if (!OnBeforeEntityDeletedIsContinueCallBack(projectionEntity))
                        return;

                OnBeforeEntityDeleteCallBack?.Invoke(projectionEntity);
                if (!IsPersistentView)
                    //BaseModel Customization End
                    Entities.Remove(projectionEntity);

                var primaryKey = Repository.GetProjectionPrimaryKey(projectionEntity);
                var entity = Repository.Find(primaryKey);
                if (entity != null)
                {
                    OnBeforeEntityDeleted(primaryKey, entity);
                    Repository.Remove(entity);
                    Repository.UnitOfWork.SaveChanges();
                    OnEntityDeleted(primaryKey, entity);
                }
            }
            catch (DbException e)
            {
                Refresh();
                MessageBoxService.ShowMessage(e.ErrorMessage, e.ErrorCaption, MessageButton.OK, MessageIcon.Error);
            }
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

        public virtual TEntity InstantiateEntity(TEntity entity)
        {
            newEntityInitializer?.Invoke(entity);
            return entity;
        }

        /// <summary>
        /// Creates and shows a document that contains a single object view model for new entity.
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the NewCommand property that can be used as a binding source in views.
        /// </summary>
        public virtual void New()
        {
            if (canCreateNewEntity != null && !canCreateNewEntity())
                return;
            DocumentManagerService.ShowNewEntityDocument(this, newEntityInitializer);
        }

        /// <summary>
        /// Saves the given entity.
        /// Since CollectionViewModelBase is a POCO view model, the instance of this class will also expose the SaveCommand property that can be used as a binding source in views.
        /// </summary>
        /// <param name="projectionEntity">An entity to save.</param>
        [Display(AutoGenerateField = false)]
        public virtual void Save(TProjection projectionEntity)
        {
            bool isNewEntity;
            if (OnBeforeEntitySavedIsContinueCallBack != null)
                if (!OnBeforeEntitySavedIsContinueCallBack(projectionEntity))
                    return;

            var entity = Repository.FindExistingOrAddNewEntity(projectionEntity,
                (p, e) => { ApplyProjectionPropertiesToEntity(p, e); }, out isNewEntity);

            if (IsContinueSaveCallBack != null)
                if (!IsContinueSaveCallBack(projectionEntity, isNewEntity))
                    return;

            try
            {
                OnBeforeEntitySaved(entity);
                ApplyCreatedDateToEntity(entity);
                Repository.UnitOfWork.SaveChanges();
                var primaryKey = Repository.GetPrimaryKey(entity);
                Repository.SetProjectionPrimaryKey(projectionEntity, primaryKey);
                //Need to put here because any updates associated with the entity need to be committed before sending message
                OnAfterEntitySavedCallBack?.Invoke(projectionEntity, entity, isNewEntity);
                //ICanUpdate canUpdateEntity = projectionEntity as ICanUpdate;
                //if (canUpdateEntity != null)
                //    canUpdateEntity.Update();
                SendMessage(primaryKey, projectionEntity, entity, isNewEntity);
            }
            catch (DbException e)
            {
                MessageBoxService.ShowMessage(e.ErrorMessage, e.ErrorCaption, MessageButton.OK, MessageIcon.Error);
            }
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

        private readonly bool ignoreSelectEntityMessage;

        private void RegisterSelectEntityMessage()
        {
            if (!ignoreSelectEntityMessage)
                Messenger.Default.Register<SelectEntityMessage>(this, x => OnSelectEntityMessage(x));
        }

        private void RequestSelectedEntity()
        {
            if (!ignoreSelectEntityMessage)
                Messenger.Default.Send(new SelectedEntityRequest());
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
        protected virtual void AddUndoBeforeEntityDeleted(TProjection projectionEntity)
        {
        }

        /// <summary>
        /// Custom method deviating from devexpress scaffolding to expose repository create function.
        /// </summary>
        /// <returns>A new entity</returns>
        protected virtual TEntity CreateEntity()
        {
            var entity = Repository.Create();
            entity = InstantiateEntity(entity);

            return entity;
        }

        protected virtual TEntity CreateNewEntity(TProjection projectionEntity)
        {
            var entity = Repository.Create();
            //ApplyProjectionPropertiesToEntity(projectionEntity, entity);
            return entity;
        }

        #endregion
    }
}
