using BaseModel.DataModel;
using BaseModel.ViewModel.Base;
using BaseModel.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Mvvm;

namespace BaseModel.ViewModel.Loader
{
    public abstract partial class CollectionViewModelsWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork> : ViewModelBase, ICollectionViewModelsWrapper<TMainProjectionEntity>, IDocumentContent, ISupportParameter, IDisposable
        where TMainEntity : class, IGuidEntityKey, new()
        where TMainProjectionEntity : class, IGuidEntityKey, ICanUpdate, new()
        where TMainEntityUnitOfWork : IUnitOfWork
    {
        public void Dispose()
        {
            CleanUpEntitiesLoader();
        }

        public class EntitiesLoaderDescriptionCollection : List<IEntitiesLoaderDescription>
        {
            private readonly ICollectionViewModelsWrapper owner;

            public EntitiesLoaderDescriptionCollection(ICollectionViewModelsWrapper owner)
            {
                this.owner = owner;
            }

            public void AddLoaderDescription<TEntity, TProjection, TPrimaryKey, TUnitOfWork>(
            IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory,
            Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc,
            Func<Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>>> projectionFunc = null,
            Action<TProjection> compulsoryEntityAssignmentFunc = null, bool suppressNotification = false)
            where TEntity : class, new()
            where TProjection : class, new()
            where TUnitOfWork : IUnitOfWork
            {
                Action<object, Type, EntityMessageType, object, bool> onAfterEntitiesChanged = null;
                Func<object, Type, EntityMessageType, object, bool, bool> onBeforeEntitiesChanged = null;
                int loadOrder = this.Count() + 1;

                //Entities either affect MainEntities before it is loaded or after it is loaded
                //CompulsoryEntityAssignment is used to determine whether MainEntity should be loaded and assign variable back for projection usage
                //Because it doesn't affect MainEntities after it is loaded OnAfterAffectingEntities is not assigned
                if (compulsoryEntityAssignmentFunc != null)
                    onAfterEntitiesChanged = owner.OnAfterCompulsoryEntitiesChanged;
                //Some entities are used as auxiliary data for certain functions and doesn't not affect MainEntities at all
                else
                    onAfterEntitiesChanged = owner.OnAfterAuxiliaryEntitiesChanged;

                onBeforeEntitiesChanged = owner.OnBeforeEntitiesChanged;
                owner.SuppressNotification = suppressNotification;

                Add(new EntitiesLoaderDescription<TEntity, TProjection, TPrimaryKey, TUnitOfWork>(
                    owner,
                    loadOrder,
                    unitOfWorkFactory,
                    getRepositoryFunc,
                    null,
                    onBeforeEntitiesChanged,
                    onAfterEntitiesChanged,
                    projectionFunc,
                    compulsoryEntityAssignmentFunc));
            }

            public IEntitiesLoaderDescription GetLoader(Type dependencyType)
            {
                return this.FirstOrDefault(x => x.GetProjectionEntityType() == dependencyType);
            }

            public IEntitiesViewModel<TProjection> GetViewModel<TProjection>()
                where TProjection : class
            {
                var entitiesLoader =
                    (IEntitiesLoaderDescription<TProjection>)GetLoader(typeof(TProjection));
                if (entitiesLoader == null)
                    throw new InvalidOperationException("Entities loader not added");

                return entitiesLoader.GetViewModel();
            }

            public IReadOnlyRepository<TProjection> GetRepository<TProjection>()
                where TProjection : class
            {
                var entitiesLoader = (IEntitiesLoaderDescription<TProjection>)GetLoader(typeof(TProjection));
                if (entitiesLoader == null)
                    throw new InvalidOperationException("Entities loader not added");

                return entitiesLoader.GetRepository();
            }

            public Func<TProjection> GetObjectFunc<TProjection>()
                where TProjection : class
            {
                var entitiesLoader =
                    (IEntitiesLoaderDescription<TProjection>)GetLoader(typeof(TProjection));
                if (entitiesLoader == null)
                    throw new InvalidOperationException("Entities loader not added");

                return entitiesLoader.GetSingleObject;
            }

            public Func<IEnumerable<TProjection>> GetCollectionFunc<TProjection>()
                where TProjection : class
            {
                var entitiesLoader =
                    (IEntitiesLoaderDescription<TProjection>)GetLoader(typeof(TProjection));
                if (entitiesLoader == null)
                    throw new InvalidOperationException("Entities loader not added");

                return entitiesLoader.GetCollection;
            }

            public IEnumerable<TProjection> GetCollection<TProjection>()
                where TProjection : class
            {
                var GetCollectionFunc = GetCollectionFunc<TProjection>();
                return GetCollectionFunc();
            }

            public TProjection GetObject<TProjection>()
                where TProjection : class
            {
                var GetSingleObjectFunc = GetObjectFunc<TProjection>();
                return GetSingleObjectFunc();
            }

            public bool IsEntitiesLoaderExists(Type type)
            {
                return this.Any(x => x.GetProjectionEntityType() == type);
            }

            bool isDestroying { get; set; }
            public void OnDestroy()
            {
                if (isDestroying)
                    return;

                isDestroying = true;
                for (int i = this.Count() - 1; i >= 0; i--)
                {
                    IEntitiesLoaderDescription entitiesLoaderDescription = this[i];
                    entitiesLoaderDescription.DisposeViewModel();
                    this.Remove(entitiesLoaderDescription);
                    entitiesLoaderDescription = null;
                }
                isDestroying = false;
            }
        }
    }
}