using System;
using System.Collections.ObjectModel;

namespace BaseModel.Misc
{
    public interface ICollectionViewModel<TProjection>
        where TProjection : class
    {
        void Save(TProjection entity);
        void Delete(TProjection entity);
        void CleanUpCallBacks();
    }

    public interface IGuidEntityKey
    {
        Guid EntityKey { get; set; }
    }

    public interface IHaveCreatedDate
    {
        DateTime EntityCreatedDate { get; set; }
    }

    public interface IHaveExpandState
    {
        bool IsExpanded { get; set; }
    }

    public interface IHaveSortOrder
    {
        int SortOrder { get; set; }
        int? OldSortOrder { get; set; }
    }

    public interface ICanUpdate
    {
        void Update();
    }

    public interface IGuidParentEntityKey
    {
        Guid? ParentEntityKey { get; set; }
    }

    public interface IOriginalGuidEntityKey
    {
        Guid OriginalEntityKey { get; }
        void SetOriginalEntityKey(Guid newGuid);
    }

    public interface IEntityNumber
    {
        string EntityNumber { get; set; }
    }

    public interface IProjection<TEntity> : IGuidEntityKey
        where TEntity : class, new()
    {
        TEntity Entity { get; set; }
    }

    public interface IProjectionMasterDetail<TEntity, TProjection> : IProjection<TEntity>, IHaveExpandState
        where TEntity : class, IGuidEntityKey, new()
        where TProjection : class, IGuidEntityKey, new()
    {
        ObservableCollection<TProjection> DetailEntities { get; set; }
    }

    public interface ISupportViewRestoration
    {
        Action StoreActiveCell { get; set; }
        Action RestoreActiveCell { get; set; }
        //Raise Properties changed doesn't refresh column data, call this method instead
        Action ForceGridRefresh { get; set; }
    }

    /// <summary>
    /// The interface for supporting children document other than using TEntity type name.
    /// </summary>
    public interface ISupportCustomDocument
    {
        string CustomDocumentType();
        object CustomDocumentParameter();
        string CustomDocumentTitle();
    }

    /// <summary>
    /// The base interface for view models representing a single entity.
    /// </summary>
    /// <typeparam name="TEntity">An entity type.</typeparam>
    /// <typeparam name="TPrimaryKey">An entity primary key type.</typeparam>
    public interface ISingleObjectViewModel<TEntity, TPrimaryKey>
    {
        /// <summary>
        /// The entity represented by a view model.
        /// </summary>
        TEntity Entity { get; }

        /// <summary>
        /// The entity primary key value.
        /// </summary>
        TPrimaryKey PrimaryKey { get; }
    }
}
