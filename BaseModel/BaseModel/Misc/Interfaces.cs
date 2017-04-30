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

    public interface IHaveGUID
    {
        Guid GUID { get; set; }
    }

    public interface IProjection<TEntity> : IHaveGUID
        where TEntity : class, new()
    {
        TEntity Entity { get; set; }
    }

    public interface IProjectionMasterDetail<TEntity, TProjection> : IProjection<TEntity>
        where TEntity : class, IHaveGUID, new()
        where TProjection : class, IHaveGUID, new()
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
