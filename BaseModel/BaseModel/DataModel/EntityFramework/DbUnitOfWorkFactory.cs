using BaseModel.Utils;
using DevExpress.Data.Linq;
using DevExpress.Data.Linq.Helpers;
using System;
using System.ComponentModel;
using System.Data;
using System.Linq;

namespace BaseModel.DataModel.EntityFramework
{
    public class DbUnitOfWorkFactory<TUnitOfWork> : IUnitOfWorkFactory<TUnitOfWork> where TUnitOfWork : IUnitOfWork
    {
        private Func<TUnitOfWork> createUnitOfWork;

        public DbUnitOfWorkFactory(Func<TUnitOfWork> createUnitOfWork)
        {
            this.createUnitOfWork = createUnitOfWork;
        }

        TUnitOfWork IUnitOfWorkFactory<TUnitOfWork>.CreateUnitOfWork()
        {
            return createUnitOfWork();
        }

        IInstantFeedbackSource<TProjection> IUnitOfWorkFactory<TUnitOfWork>.CreateInstantFeedbackSource
            <TEntity, TProjection, TPrimaryKey>(
                Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc,
                Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> projection)
        {
            var threadSafeProperties =
                new TypeInfoProxied(TypeDescriptor.GetProperties(typeof(TProjection)), null).UIDescriptors;
            if (projection == null)
                projection = x => x as IQueryable<TProjection>;
            var keyProperties =
                ExpressionHelper.GetKeyProperties(getRepositoryFunc(createUnitOfWork()).GetPrimaryKeyExpression);
            var keyExpression = keyProperties.Select(p => p.Name).Aggregate((l, r) => l + ";" + r);
            var source =
                new EntityInstantFeedbackSource(
                    (DevExpress.Data.Linq.GetQueryableEventArgs e) =>
                        e.QueryableSource = projection(getRepositoryFunc(createUnitOfWork())))
                {
                    KeyExpression = keyExpression
                };
            return new InstantFeedbackSource<TProjection>(source, threadSafeProperties);
        }
    }
}