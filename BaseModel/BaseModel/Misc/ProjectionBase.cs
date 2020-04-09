using BaseModel.ViewModel;
using DevExpress.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseModel.Misc
{
    public abstract class ProjectionBase<TEntity> : BindableBase, IProjection<TEntity>, IHaveEntity<TEntity>, ICanUpdate, IHaveCreatedDate
        where TEntity : class, IGuidEntityKey, IHaveCreatedDate, new()
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

        public bool NewEntityFromView { get; set; }
        public DateTime EntityCreatedDate { get => Entity.EntityCreatedDate; set => Entity.EntityCreatedDate = value; }

        public virtual void Update()
        {
            RaisePropertiesChanged();
        }
    }

    public interface IHaveEntity<TEntity>
    {
        TEntity Entity { get; set; }
    }

    public abstract class ProjectionMasterDetailBase<TEntity, TProjection> : ProjectionBase<TEntity>, IProjectionMasterDetail<TEntity, TProjection>
        where TEntity : class, IGuidEntityKey, IHaveCreatedDate, new()
        where TProjection : class, IGuidEntityKey, new()
    {
        protected virtual ObservableCollection<TProjection> detailEntities { get; set; }
        public virtual ObservableCollection<TProjection> DetailEntities
        {
            get { return GetProperty(() => detailEntities); }
            set { SetProperty(() => detailEntities, value); }
        }

        public virtual bool IsExpanded
        {
            get { return GetProperty(() => IsExpanded); }
            set { SetProperty(() => IsExpanded, value); }
        }

        public ProjectionMasterDetailBase()
            : base()
        {
            DetailEntities = new ObservableCollection<TProjection>();
        }
    }

    public abstract class ProjectionMasterOtherDetailBase<TEntity, TChild, TProjection> : ProjectionBase<TEntity>, IProjectionMasterOtherDetail<TEntity, TChild>
    where TEntity : class, IGuidEntityKey, IHaveCreatedDate, new()
    where TChild : class, IGuidEntityKey, IGuidParentEntityKey, new()
    where TProjection : class, IGuidEntityKey, new()
    {
        [NotMapped]
        protected virtual ObservableCollection<TChild> detailEntities { get; set; }

        [NotMapped]
        public virtual ObservableCollection<TChild> DetailEntities
        {
            get { return GetProperty(() => detailEntities); }
            set { SetProperty(() => detailEntities, value); }
        }

        [NotMapped]
        public virtual bool IsExpanded
        {
            get { return GetProperty(() => IsExpanded); }
            set { SetProperty(() => IsExpanded, value); }
        }

        public ProjectionMasterOtherDetailBase()
            : base()
        {
            DetailEntities = new ObservableCollection<TChild>();
        }
    }
}
