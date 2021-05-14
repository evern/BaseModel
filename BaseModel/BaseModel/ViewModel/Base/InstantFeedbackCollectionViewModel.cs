using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using DevExpress.Mvvm.DataAnnotations;
using System.Collections.ObjectModel;
using DevExpress.Data.Linq;
using System.Collections;
using BaseModel.DataModel;
using BaseModel.Misc;

namespace BaseModel.ViewModel.Base
{
    public partial class InstantFeedbackCollectionViewModel<TEntity, TPrimaryKey, TUnitOfWork> : InstantFeedbackCollectionViewModelBase<TEntity, TEntity, TPrimaryKey, TUnitOfWork>
        where TEntity : class, ICanUpdate, new()
        where TUnitOfWork : IUnitOfWork
    {

        public static InstantFeedbackCollectionViewModel<TEntity, TPrimaryKey, TUnitOfWork> CreateInstantFeedbackCollectionViewModel(
            IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory,
            Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc,
            Func<IRepositoryQuery<TEntity>, IQueryable<TEntity>> projection = null,
            Func<bool> canCreateNewEntity = null)
        {
            return ViewModelSource.Create(() => new InstantFeedbackCollectionViewModel<TEntity, TPrimaryKey, TUnitOfWork>(unitOfWorkFactory, getRepositoryFunc, projection, canCreateNewEntity));
        }

        protected InstantFeedbackCollectionViewModel(
            IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory,
            Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc,
            Func<IRepositoryQuery<TEntity>, IQueryable<TEntity>> projection = null,
            Func<bool> canCreateNewEntity = null)
            : base(unitOfWorkFactory, getRepositoryFunc, projection, canCreateNewEntity)
        {
        }
    }

    public partial class InstantFeedbackCollectionViewModel<TEntity, TProjection, TPrimaryKey, TUnitOfWork> : InstantFeedbackCollectionViewModelBase<TEntity, TProjection, TPrimaryKey, TUnitOfWork>
        where TEntity : class, new()
        where TProjection : class, ICanUpdate, new()
        where TUnitOfWork : IUnitOfWork
    {

        public static InstantFeedbackCollectionViewModel<TEntity, TProjection, TPrimaryKey, TUnitOfWork> CreateInstantFeedbackCollectionViewModel(
            IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory,
            Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc,
            Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> projection,
            Func<bool> canCreateNewEntity = null)
        {
            return ViewModelSource.Create(() => new InstantFeedbackCollectionViewModel<TEntity, TProjection, TPrimaryKey, TUnitOfWork>(unitOfWorkFactory, getRepositoryFunc, projection, canCreateNewEntity));
        }

        protected InstantFeedbackCollectionViewModel(
            IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory,
            Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc,
            Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> projection,
            Func<bool> canCreateNewEntity = null)
            : base(unitOfWorkFactory, getRepositoryFunc, projection, canCreateNewEntity)
        {
        }
    }
}