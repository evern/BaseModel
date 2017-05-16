using BaseModel.DataModel;
using BaseModel.ViewModel;
using BaseModel.ViewModel.Base;
using BaseModel.Misc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BaseModel.ViewModel.Loader
{

    public class EntitiesLoaderDescription<TEntity, TProjection, TPrimaryKey, TUnitOfWork> :
        IEntitiesLoaderDescription<TProjection>
        where TEntity : class, new()
        where TProjection : class, new()
        where TUnitOfWork : IUnitOfWork
    {
        private readonly ICollectionViewModelsWrapper owner;
        public int LoadOrder { get; set; }
        public bool IsLoaded { get; set; }
        readonly Action<TProjection> compulsoryEntityAssignmentFunc;
        readonly IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory;
        readonly Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc;

        readonly Func<Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>>> constructProjectionCallBackFunc;

        private IEntitiesViewModel<TProjection> collectionViewModel;
        readonly Func<IEnumerable<TProjection>, bool> isContinueLoadingCallBack;
        readonly Action<object, Type, EntityMessageType, object> collectionViewModelChangedCallBack;
        readonly Func<object, Type, EntityMessageType, object, bool> collectionViewModelBeforeChangedCallBack;

        /// <summary>
        /// Describe how should entities be handled within EntitiesCollectionWrapper
        /// </summary>
        /// <param name="loadOrder"></param>
        /// <param name="isRequired"></param>
        /// <param name="entitiesLoader"></param>
        /// <param name="dependencyType"></param>
        public EntitiesLoaderDescription(
            ICollectionViewModelsWrapper owner,
            int loadOrder,
            IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory,
            Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc,
            Func<IEnumerable<TProjection>, bool> isContinueLoadingCallBack = null,
            Func<object, Type, EntityMessageType, object, bool> collectionViewModelBeforeChangedCallBack = null,
            Action<object, Type, EntityMessageType, object> collectionViewModelChangedCallBack = null,
            Func<Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>>> constructProjectionCallBackFunc = null,
            Action<TProjection> compulsoryEntityAssignmentFunc = null)
        {
            this.owner = owner;
            this.LoadOrder = loadOrder;
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.getRepositoryFunc = getRepositoryFunc;
            this.constructProjectionCallBackFunc = constructProjectionCallBackFunc;
            this.isContinueLoadingCallBack = isContinueLoadingCallBack;
            this.collectionViewModelChangedCallBack = collectionViewModelChangedCallBack;
            this.collectionViewModelBeforeChangedCallBack = collectionViewModelBeforeChangedCallBack;
            this.compulsoryEntityAssignmentFunc = compulsoryEntityAssignmentFunc;
        }

        public void CreateCollectionViewModel()
        {
            Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> projection = null;
            if (constructProjectionCallBackFunc != null)
                projection = constructProjectionCallBackFunc();

            collectionViewModel =
                CollectionViewModel<TEntity, TProjection, TPrimaryKey, TUnitOfWork>.CreateCollectionViewModel(
                    unitOfWorkFactory, getRepositoryFunc, projection);
            collectionViewModel.OnEntitiesLoadedCallBack = OnEntitiesFirstLoaded;
            collectionViewModel.OnAfterEntitiesChangedCallBack = collectionViewModelChangedCallBack;
            collectionViewModel.OnBeforeEntitiesChangedCallBack = collectionViewModelBeforeChangedCallBack;
            collectionViewModel.Entities.ToList();
        }

        public int GetEntitiesCount()
        {
            if (collectionViewModel == null || collectionViewModel.IsLoading)
                return 0;

            return collectionViewModel.Entities.Count();
        }

        public Type GetEntitiesProjectionType()
        {
            return typeof(TProjection);
        }

        private void OnEntitiesFirstLoaded(IEnumerable<TProjection> loadedEntities)
        {
            IsLoaded = true;

            if (isContinueLoadingCallBack != null && !isContinueLoadingCallBack(loadedEntities))
            {
                collectionViewModel.OnEntitiesLoadedCallBack = null;
                return;
            }

            if (compulsoryEntityAssignmentFunc != null)
            {
                if (loadedEntities.Count() == 0)
                    return;

                TProjection compulsoryEntity = loadedEntities.First();
                compulsoryEntityAssignmentFunc(compulsoryEntity);
            }

            if (collectionViewModel != null)
            {
                collectionViewModel.OnEntitiesLoadedCallBack = null;
                owner.InvokeEntitiesLoaderDescriptionLoading();
            }
        }

        public bool IsCompulsory
        {
            get { return compulsoryEntityAssignmentFunc != null; }
        }

        public Type GetProjectionEntityType()
        {
            return typeof(TProjection);
        }

        public IEntitiesViewModel<TProjection> GetViewModel()
        {
            return collectionViewModel;
        }

        public IReadOnlyRepository<TProjection> GetRepository()
        {
            return collectionViewModel.RepositoryForProjectionQuery;
        }

        /// <summary>
        /// Call this only after entities has been loaded as notified by OnEntitiesLoadedCallBackFunc
        /// </summary>
        public IEnumerable<TProjection> GetCollection()
        {
            //this.collectionViewModel.OnEntitiesLoadedCallBack = null;
            if (collectionViewModel == null || collectionViewModel.Entities == null)
                return new List<TProjection>().AsQueryable();
            else
                return collectionViewModel.Entities.AsQueryable();
        }

        /// <summary>
        /// Call this only after entities has been loaded as notified by OnEntitiesLoadedCallBackFunc
        /// </summary>
        public TProjection GetSingleObject()
        {
            //this.collectionViewModel.OnEntitiesLoadedCallBack = null;
            if (collectionViewModel == null || collectionViewModel.Entities == null)
                return null;
            else
            {
                if (collectionViewModel.Entities.Count == 0)
                    return null;

                return collectionViewModel.Entities.First();
            }
        }

        public void DisposeViewModel()
        {
            if (collectionViewModel != null)
            {
                ICollectionViewModel<TProjection> viewModel = collectionViewModel as ICollectionViewModel<TProjection>;
                if (viewModel != null)
                    viewModel.CleanUpCallBacks();

                collectionViewModel.OnDestroy();
                collectionViewModel = null;
            }
        }
    }

    public interface IEntitiesLoaderDescription<TProjection> : IEntitiesLoaderDescription
        where TProjection : class
    {
        TProjection GetSingleObject();
        IEnumerable<TProjection> GetCollection();
        IEntitiesViewModel<TProjection> GetViewModel();
        IReadOnlyRepository<TProjection> GetRepository();
    }

    public interface IEntitiesLoaderDescription
    {
        void DisposeViewModel();
        Type GetProjectionEntityType();
        void CreateCollectionViewModel();
        bool IsLoaded { get; set; }
        int GetEntitiesCount();
        Type GetEntitiesProjectionType();
        int LoadOrder { get; set; }
        bool IsCompulsory { get; }
    }
}
