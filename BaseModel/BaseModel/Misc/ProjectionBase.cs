using BaseModel.ViewModel;
using DevExpress.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace BaseModel.Misc
{
    public abstract class ProjectionBase<TEntity> : BindableBase, IProjection<TEntity>
        where TEntity : class, IHaveGUID, new()
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
        public Guid GUID
        {
            get { return Entity.GUID; }
            set { Entity.GUID = value; }
        }
    }

    public abstract class ProjectionMasterDetailBase<TEntity, TProjection> : ProjectionBase<TEntity>, IProjectionMasterDetail<TEntity, TProjection>
        where TEntity : class, IHaveGUID, new()
        where TProjection : class, IHaveGUID, new()
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
