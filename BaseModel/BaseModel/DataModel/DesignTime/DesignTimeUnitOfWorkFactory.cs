using System;
using System.Linq;

namespace BaseModel.DataModel.DesignTime
{
    internal class DesignTimeUnitOfWorkFactory<TUnitOfWork> : IUnitOfWorkFactory<TUnitOfWork> where TUnitOfWork : IUnitOfWork
    {
        private Func<TUnitOfWork> getUnitOfWork;

        public DesignTimeUnitOfWorkFactory(Func<TUnitOfWork> getUnitOfWork)
        {
            this.getUnitOfWork = getUnitOfWork;
        }

        TUnitOfWork IUnitOfWorkFactory<TUnitOfWork>.CreateUnitOfWork()
        {
            return getUnitOfWork();
        }

        IInstantFeedbackSource<TProjection> IUnitOfWorkFactory<TUnitOfWork>.CreateInstantFeedbackSource
            <TEntity, TProjection, TPrimaryKey>(
                Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc,
                Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> projection)
        {
            return new DesignTimeInstantFeedbackSource<TProjection, TPrimaryKey>();
        }
    }
}