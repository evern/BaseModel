using BaseModel.ViewModel;
using DevExpress.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace BaseModel.Misc
{
    public abstract class ProjectionBase<TEntity> : BindableBase, IProjection<TEntity>
        where TEntity : class, IGuidEntityKey, new()
    {
        public TEntity Entity { get; set; }

        public ProjectionBase()
        {
            Entity = new TEntity();
        }

        public ProjectionBase(TEntity entity)
        {
            Entity = entity;
        }

        [Key]
        public Guid EntityKey
        {
            get { return Entity.EntityKey; }
            set { Entity.EntityKey = value; }
        }
    }

    public abstract class ProjectionMasterDetailBase<TEntity, TProjection> : ProjectionBase<TEntity>, IProjectionMasterDetail<TEntity, TProjection>
        where TEntity : class, IGuidEntityKey, new()
        where TProjection : class, IGuidEntityKey, new()
    {
        protected virtual ObservableCollection<TProjection> detailEntities { get; set; }
        public virtual ObservableCollection<TProjection> DetailEntities
        {
            get { return detailEntities; }
            set { detailEntities = value; }
        }

        public ProjectionMasterDetailBase()
            : base()
        {
            DetailEntities = new ObservableCollection<TProjection>();
        }
    }
}
