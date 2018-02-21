using BaseModel.DataModel;
using BaseModel.Utils;
using BaseModel.Misc;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.POCO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace BaseModel.ViewModel.Base
{
    /// <summary>
    /// The base class for POCO view models exposing a collection of entities of the given type.
    /// This is a partial class that provides an extension point to add custom properties, commands and override methods without modifying the auto-generated code.
    /// </summary>
    /// <typeparam name="TEntity">A repository entity type.</typeparam>
    /// <typeparam name="TProjection">A projection entity type.</typeparam>
    /// <typeparam name="TUnitOfWork">A unit of work type.</typeparam>
    public abstract partial class EntitiesViewModel<TEntity, TProjection, TUnitOfWork> :
        EntitiesViewModelBase<TEntity, TProjection, TUnitOfWork>
        where TEntity : class
        where TProjection : class
        where TUnitOfWork : IUnitOfWork
    {
        /// <summary>
        /// Initializes a new instance of the EntitiesViewModel class.
        /// </summary>
        /// <param name="unitOfWorkFactory">A factory used to create a unit of work instance.</param>
        /// <param name="getRepositoryFunc">A function that returns a repository representing entities of the given type.</param>
        /// <param name="projection">A LINQ function used to customize a query for entities. The parameter, for example, can be used for sorting data and/or for projecting data to a custom type that does not match the repository entity type.</param>
        protected EntitiesViewModel(
            IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory,
            Func<TUnitOfWork, IReadOnlyRepository<TEntity>> getRepositoryFunc,
            Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> projection)
            : base(unitOfWorkFactory, getRepositoryFunc, projection)
        {
        }
    }

    /// <summary>
    /// The base class for a POCO view models exposing a collection of entities of the given type.
    /// It is not recommended to inherit directly from this class. Use the EntitiesViewModel class instead.
    /// </summary>
    /// <typeparam name="TEntity">A repository entity type.</typeparam>
    /// <typeparam name="TProjection">A projection entity type.</typeparam>
    /// <typeparam name="TUnitOfWork">A unit of work type.</typeparam>
    [POCOViewModel]
    public abstract class EntitiesViewModelBase<TEntity, TProjection, TUnitOfWork> : IEntitiesViewModel<TProjection>
        where TEntity : class
        where TProjection : class
        where TUnitOfWork : IUnitOfWork
    {
        #region inner classes

        protected interface IEntitiesChangeTracker
        {
            void RegisterMessageHandler();
            void UnregisterMessageHandler();
        }

        protected class EntitiesChangeTracker<TPrimaryKey> : IEntitiesChangeTracker
        {
            private readonly EntitiesViewModelBase<TEntity, TProjection, TUnitOfWork> owner;

            private ObservableCollection<TProjection> Entities
            {
                get { return owner.Entities; }
            }

            private IRepository<TEntity, TPrimaryKey> Repository
            {
                get { return (IRepository<TEntity, TPrimaryKey>) owner.ReadOnlyRepository; }
            }

            public EntitiesChangeTracker(EntitiesViewModelBase<TEntity, TProjection, TUnitOfWork> owner)
            {
                this.owner = owner;
            }

            void IEntitiesChangeTracker.RegisterMessageHandler()
            {
                Messenger.Default.Register<EntityMessage<TEntity, TPrimaryKey>>(this, x => OnMessage(x));
            }

            void IEntitiesChangeTracker.UnregisterMessageHandler()
            {
                try
                {
                    Messenger.Default.Unregister(this);
                }
                catch
                {

                }
            }

            public TProjection FindLocalProjectionByKey(TPrimaryKey primaryKey)
            {
                var primaryKeyEqualsExpression =
                    RepositoryExtensions.GetProjectionPrimaryKeyEqualsExpression<TEntity, TProjection, TPrimaryKey>(
                        Repository, primaryKey);
                return Entities.AsQueryable().FirstOrDefault(primaryKeyEqualsExpression);
            }

            public TProjection FindActualProjectionByKey(TPrimaryKey primaryKey)
            {
                var projectionEntity = Repository.FindActualProjectionByKey(owner.Projection, primaryKey);
                if (projectionEntity != null &&
                    ExpressionHelper.IsFitEntity(Repository.Find(primaryKey), owner.GetFilterExpression()))
                {
                    owner.OnEntitiesLoaded(GetUnitOfWork(Repository), new TProjection[] {projectionEntity});
                    return projectionEntity;
                }
                return null;
            }

            public TProjection FindActualProjectionByExpression(Expression<Func<TEntity, bool>> predicate)
            {
                return Repository.FindActualProjectionByExpression(owner.Projection, predicate);
            }

            private void OnMessage(EntityMessage<TEntity, TPrimaryKey> message)
            {
                if (!owner.IsLoaded)
                    return;

                if (owner.OnBeforeEntitiesChangedCallBack != null && !owner.OnBeforeEntitiesChangedCallBack(message.PrimaryKey, typeof(TEntity),
                        message.MessageType, message.Sender, message.WillPerformBulkRefresh))
                    return;

                bool skipOnMessage = message.Sender.ToString() == owner.ToString() && message.HWID == owner.CurrentHWID;

                switch (message.MessageType)
                {
                    case EntityMessageType.Added:
                        OnEntityAdded(message.PrimaryKey);
                        break;
                    case EntityMessageType.Changed:
                        OnEntityChanged(message.PrimaryKey, skipOnMessage);
                        break;
                    case EntityMessageType.Deleted:
                        OnEntityDeleted(message.PrimaryKey);
                        break;
                }

                owner.OnAfterEntitiesChangedCallBack?.Invoke(message.PrimaryKey, typeof(TEntity), message.MessageType, message.Sender, message.WillPerformBulkRefresh);
            }

            private void OnEntityAdded(TPrimaryKey primaryKey)
            {
                var projectionEntity = FindActualProjectionByKey(primaryKey);
                var entity = FindLocalProjectionByKey(primaryKey);
                if (projectionEntity != null && entity == null && !owner.IsPersistentView)
                    Entities.Add(projectionEntity);
            }

            private void OnEntityChanged(TPrimaryKey primaryKey, bool skipOnMessage)
            {
                var existingProjectionEntity = FindLocalProjectionByKey(primaryKey);
                ICanUpdate can_update_entity = existingProjectionEntity as ICanUpdate;

                if (skipOnMessage)
                {
                    if (can_update_entity != null)
                    {
                        if(!can_update_entity.NewEntityFromView)
                        {
                            can_update_entity.Update();
                            return;
                        }
                        else
                        {
                            can_update_entity.NewEntityFromView = false;
                        }
                    }
                }

                var projectionEntity = FindActualProjectionByKey(primaryKey);

                if (projectionEntity == null)
                {
                    if (!owner.IsPersistentView)
                        Entities.Remove(existingProjectionEntity);
                    return;
                }
                if (existingProjectionEntity != null)
                {
                    owner.OnBeforeAssignRepositoryToExistingProjection?.Invoke(existingProjectionEntity, projectionEntity);
                    Entities[Entities.IndexOf(existingProjectionEntity)] = projectionEntity;
                    owner.RestoreSelectedEntity(existingProjectionEntity, projectionEntity);
                    return;
                }

                OnEntityAdded(primaryKey);
            }

            private void OnEntityDeleted(TPrimaryKey primaryKey)
            {
                if (!owner.IsPersistentView)
                    Entities.Remove(FindLocalProjectionByKey(primaryKey));
            }
        }

        #endregion

        private ObservableCollection<TProjection> entities = new ObservableCollection<TProjection>();
        private CancellationTokenSource loadCancellationTokenSource;
        protected readonly IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory;
        protected readonly Func<TUnitOfWork, IReadOnlyRepository<TEntity>> getRepositoryFunc;
        protected Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> Projection { get; private set; }

        /// <summary>
        /// Initializes a new instance of the EntitiesViewModelBase class.
        /// </summary>
        /// <param name="unitOfWorkFactory">A factory used to create a unit of work instance.</param>
        /// <param name="getRepositoryFunc">A function that returns a repository representing entities of the given type.</param>
        /// <param name="projection">A LINQ function used to customize a query for entities. The parameter, for example, can be used for sorting data and/or for projecting data to a custom type that does not match the repository entity type.</param>
        protected EntitiesViewModelBase(
            IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory,
            Func<TUnitOfWork, IReadOnlyRepository<TEntity>> getRepositoryFunc,
            Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> projection
        )
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.getRepositoryFunc = getRepositoryFunc;
            Projection = projection;
            ChangeTracker = CreateEntitiesChangeTracker();
            if (!this.IsInDesignMode())
                OnInitializeInRuntime();
        }

        /// <summary>
        /// Used to check whether entities are currently being loaded in the background. The property can be used to show the progress indicator.
        /// </summary>
        public virtual bool IsLoading { get; protected set; }


        public IReadOnlyRepository<TProjection> RepositoryForProjectionQuery
        {
            get
            {
                if (typeof(TEntity) == typeof(TProjection))
                    return (IReadOnlyRepository<TProjection>)ReadOnlyRepository;
                else
                    return null;
            }
        }

        /// <summary>
        /// The collection of entities loaded from the unit of work.
        /// </summary>
        public ObservableCollection<TProjection> Entities
        {
            get
            {
                if (!IsLoaded)
                    LoadEntities(false);
                return entities;
            }
        }

        protected IEntitiesChangeTracker ChangeTracker { get; private set; }

        protected IReadOnlyRepository<TEntity> ReadOnlyRepository { get; private set; }

        protected bool IsLoaded
        {
            get { return ReadOnlyRepository != null; }
        }

        protected void LoadEntities(bool forceLoad)
        {
            if (forceLoad)
            {
                if (loadCancellationTokenSource != null)
                    loadCancellationTokenSource.Cancel();
            }
            else if (IsLoading)
            {
                return;
            }
            loadCancellationTokenSource = LoadCore();
        }

        private void CancelLoading()
        {
            if (loadCancellationTokenSource != null)
                loadCancellationTokenSource.Cancel();
            IsLoading = false;
        }

        private CancellationTokenSource LoadCore()
        {
            IsLoading = true;
            var cancellationTokenSource = new CancellationTokenSource();
            //BaseModel Customization
            var selectedEntitiesCallBack = GetSelectedEntityCallback();
            StoreSelectedEntitiesKey();
            //BaseModel Customization
            Task.Factory.StartNew(() =>
            {
                var repository = CreateReadOnlyRepository();
                var entities =
                    new ObservableCollection<TProjection>(repository.GetFilteredEntities(GetFilterExpression(),
                        Projection));
                OnEntitiesLoaded(GetUnitOfWork(repository), entities);
                return new Tuple<IReadOnlyRepository<TEntity>, ObservableCollection<TProjection>>(repository, entities);
            }).ContinueWith(x =>
                {
                    if (!x.IsFaulted)
                    {
                        ReadOnlyRepository = x.Result.Item1;
                        entities = x.Result.Item2;
                        this.RaisePropertyChanged(y => y.Entities);
                        OnEntitiesAssigned(selectedEntitiesCallBack);
                    }
                    IsLoading = false;
                }, cancellationTokenSource.Token, TaskContinuationOptions.None,
                TaskScheduler.FromCurrentSynchronizationContext());
            return cancellationTokenSource;
        }

        private static TUnitOfWork GetUnitOfWork(IReadOnlyRepository<TEntity> repository)
        {
            return (TUnitOfWork) repository.UnitOfWork;
        }

        protected virtual void OnEntitiesLoaded(TUnitOfWork unitOfWork, IEnumerable<TProjection> entities)
        {
            OnEntitiesLoadedCallBack?.Invoke(entities);
        }

        protected virtual void OnEntitiesAssigned(Action restoreSelectedEntitiesCallBack)
        {
            restoreSelectedEntitiesCallBack?.Invoke();
        }

        protected virtual Action GetSelectedEntityCallback()
        {
            return null;
        }

        protected virtual void StoreSelectedEntitiesKey()
        {
            return;
        }

        protected virtual void RestoreSelectionEntitiesByKey()
        {
        }

        protected virtual void RestoreSelectedEntity(TProjection existingProjectionEntity, TProjection projectionEntity)
        {
            OnMappingAdditionalChangedEntitiesProperties?.Invoke(existingProjectionEntity, projectionEntity);
        }

        protected virtual Expression<Func<TEntity, bool>> GetFilterExpression()
        {
            return null;
        }

        protected virtual void OnInitializeInRuntime()
        {
            if (ChangeTracker != null)
                ChangeTracker.RegisterMessageHandler();
        }

        public virtual void OnDestroy()
        {
            CancelLoading();
            if (ChangeTracker != null)
                ChangeTracker.UnregisterMessageHandler();
        }

        public void ManualUnregisterMessageHandler()
        {
            if (ChangeTracker != null)
                ChangeTracker.UnregisterMessageHandler();
        }

        protected virtual void OnIsLoadingChanged()
        {
        }

        protected IReadOnlyRepository<TEntity> CreateReadOnlyRepository()
        {
            return getRepositoryFunc(CreateUnitOfWork());
        }

        protected TUnitOfWork CreateUnitOfWork()
        {
            return unitOfWorkFactory.CreateUnitOfWork();
        }

        protected virtual IEntitiesChangeTracker CreateEntitiesChangeTracker()
        {
            return null;
        }

        protected IDocumentOwner DocumentOwner { get; private set; }

        #region IDocumentContent

        object IDocumentContent.Title
        {
            get { return null; }
        }

        protected virtual void OnClose(CancelEventArgs e)
        {
        }

        void IDocumentContent.OnClose(CancelEventArgs e)
        {
            OnClose(e);
        }

        void IDocumentContent.OnDestroy()
        {
            OnEntitiesLoadedCallBack = null;
            OnBeforeEntitiesChangedCallBack = null;
            OnAfterEntitiesChangedCallBack = null;
            OnDestroy();
        }

        IDocumentOwner IDocumentContent.DocumentOwner
        {
            get { return DocumentOwner; }
            set { DocumentOwner = value; }
        }

        #endregion

        #region IEntitiesViewModel

        ObservableCollection<TProjection> IEntitiesViewModel<TProjection>.Entities
        {
            get { return Entities; }
        }

        bool IEntitiesViewModel<TProjection>.IsLoading
        {
            get { return IsLoading; }
        }

        #endregion


        public Action<IEnumerable<TProjection>> OnEntitiesLoadedCallBack { get; set; }
        public Func<object, Type, EntityMessageType, object, bool, bool> OnBeforeEntitiesChangedCallBack { get; set; }
        public Action<object, Type, EntityMessageType, object, bool> OnAfterEntitiesChangedCallBack { get; set; }
        public Action<TProjection, TProjection> OnBeforeAssignRepositoryToExistingProjection { get; set; }
        public Action<TProjection, TProjection> OnMappingAdditionalChangedEntitiesProperties { get; set; }
        public string CurrentHWID { get; set; }
        public bool IsPersistentView { get; set; }
    }

    /// <summary>
    /// The base interface for view models exposing a collection of entities of the given type.
    /// </summary>
    /// <typeparam name="TProjection">An entity type.</typeparam>
    public interface IEntitiesViewModel<TProjection> : IDocumentContent
        where TProjection : class
    {
        /// <summary>
        /// The loaded collection of entities.
        /// </summary>
        ObservableCollection<TProjection> Entities { get; }

        IReadOnlyRepository<TProjection> RepositoryForProjectionQuery { get; }

        /// <summary>
        /// Used to check whether entities are currently being loaded in the background. The property can be used to show the progress indicator.
        /// </summary>
        bool IsLoading { get; }
        string CurrentHWID { get; set; }
        bool IsPersistentView { get; set; }

        //BaseModel Customization Start
        Action<IEnumerable<TProjection>> OnEntitiesLoadedCallBack { get; set; }
        Func<object, Type, EntityMessageType, object, bool, bool> OnBeforeEntitiesChangedCallBack { get; set; }
        Action<object, Type, EntityMessageType, object, bool> OnAfterEntitiesChangedCallBack { get; set; }
        Action<TProjection, TProjection> OnBeforeAssignRepositoryToExistingProjection { get; set; }
        //BaseModel Customization End
    }
}