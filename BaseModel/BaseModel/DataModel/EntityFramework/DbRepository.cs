﻿using BaseModel.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace BaseModel.DataModel.EntityFramework
{
    /// <summary>
    /// A DbRepository is a IRepository interface implementation representing the collection of all entities in the unit of work, or that can be queried from the database, of a given type. 
    /// DbRepository objects are created from a DbUnitOfWork using the GetRepository method. 
    /// DbRepository provides only write operations against entities of a given type in addition to the read-only operation provided DbReadOnlyRepository base class.
    /// </summary>
    /// <typeparam name="TEntity">Repository entity type.</typeparam>
    /// <typeparam name="TPrimaryKey">Entity primary key type.</typeparam>
    /// <typeparam name="TDbContext">DbContext type.</typeparam>
    public class DbRepository<TEntity, TPrimaryKey, TDbContext> : DbReadOnlyRepository<TEntity, TDbContext>,
        IRepository<TEntity, TPrimaryKey>
        where TEntity : class
        where TDbContext : DbContext
    {
        private readonly Expression<Func<TEntity, TPrimaryKey>> getPrimaryKeyExpression;
        private readonly EntityTraits<TEntity, TPrimaryKey> entityTraits;

        /// <summary>
        /// Initializes a new instance of DbRepository class.
        /// </summary>
        /// <param name="unitOfWork">Owner unit of work that provides context for repository entities.</param>
        /// <param name="dbSetAccessor">Function that returns DbSet entities from Entity Framework DbContext.</param>
        /// <param name="getPrimaryKeyExpression">Lambda-expression that returns entity primary key.</param>
        public DbRepository(DbUnitOfWork<TDbContext> unitOfWork, Func<TDbContext, DbSet<TEntity>> dbSetAccessor,
            Expression<Func<TEntity, TPrimaryKey>> getPrimaryKeyExpression)
            : base(unitOfWork, dbSetAccessor)
        {
            this.getPrimaryKeyExpression = getPrimaryKeyExpression;
            entityTraits = ExpressionHelper.GetEntityTraits(this, getPrimaryKeyExpression);
        }

        protected virtual TEntity CreateCore(bool add = true)
        {
            var newEntity = DbSet.Create();
            if (add)
                DbSet.Add(newEntity);
            return newEntity;
        }

        protected virtual void UpdateCore(TEntity entity)
        {
            Context.Entry(entity).Reload();
        }

        protected virtual EntityState GetStateCore(TEntity entity)
        {
            return GetEntityState(Context.Entry(entity).State);
        }

        private static EntityState GetEntityState(System.Data.Entity.EntityState entityStates)
        {
            switch (entityStates)
            {
                case System.Data.Entity.EntityState.Added:
                    return EntityState.Added;
                case System.Data.Entity.EntityState.Deleted:
                    return EntityState.Deleted;
                case System.Data.Entity.EntityState.Detached:
                    return EntityState.Detached;
                case System.Data.Entity.EntityState.Modified:
                    return EntityState.Modified;
                case System.Data.Entity.EntityState.Unchanged:
                    return EntityState.Unchanged;
                default:
                    throw new NotImplementedException();
            }
        }


        protected virtual TEntity FindCore(TPrimaryKey primaryKey)
        {
            object[] values;
            if (ExpressionHelper.IsTuple<TPrimaryKey>())
                values = ExpressionHelper.GetKeyPropertyValues(primaryKey);
            else
                values = new object[] {primaryKey};

            return DbSet.Find(values);
        }

        protected virtual void RemoveCore(TEntity entity)
        {
            try
            {
                //DbSet.Remove will clear nullable Guid value. So remember it and restore it later
                var sourceProperties = typeof(TEntity)
                    .GetProperties()
                    .Where(p => p.CanRead && p.CanWrite &&
                                p.GetCustomAttributes(
                                        typeof(System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute), true)
                                    .Length == 0);
                var notVirtualProperties = sourceProperties.Where(p => !p.GetGetMethod().IsVirtual);

                var recordKeys = new List<KeyValuePair<PropertyInfo, Guid?>>();
                foreach (var nullableGUIDProperty in notVirtualProperties)
                    if (nullableGUIDProperty.PropertyType == typeof(Guid?))
                        recordKeys.Add(new KeyValuePair<PropertyInfo, Guid?>(nullableGUIDProperty,
                            (Guid?) nullableGUIDProperty.GetValue(entity)));

                DbSet.Remove(entity);

                foreach (var recordKey in recordKeys)
                    recordKey.Key.SetValue(entity, recordKey.Value);
            }
            catch (DbEntityValidationException ex)
            {
                throw DbExceptionsConverter.Convert(ex);
            }
            catch (DbUpdateException ex)
            {
                throw DbExceptionsConverter.Convert(ex);
            }
        }

        ObjectContext ObjectContext
        {
            get
            {
                var objectContext = (Context as IObjectContextAdapter);
                if (objectContext != null)
                    return (Context as IObjectContextAdapter).ObjectContext;
                else
                    return null;
            }
        }

        protected virtual TEntity ReloadCore(TEntity entity)
        {
            ObjectContext.Refresh(RefreshMode.StoreWins, entity);

            //Context.Entry(entity).Reload();
            return FindCore(GetPrimaryKeyCore(entity));
        }

        protected virtual TPrimaryKey GetPrimaryKeyCore(TEntity entity)
        {
            return entityTraits.GetPrimaryKey(entity);
        }

        protected virtual void SetPrimaryKeyCore(TEntity entity, TPrimaryKey primaryKey)
        {
            var setPrimaryKeyAction = entityTraits.SetPrimaryKey;
            setPrimaryKeyAction(entity, primaryKey);
        }

        #region IRepository

        TEntity IRepository<TEntity, TPrimaryKey>.Find(TPrimaryKey primaryKey)
        {
            return FindCore(primaryKey);
        }

        void IRepository<TEntity, TPrimaryKey>.Add(TEntity entity)
        {
            DbSet.Add(entity);
        }

        public void AddRange(IEnumerable<TEntity> entities)
        {
            DbSet.AddRange(entities);
        }

        void IRepository<TEntity, TPrimaryKey>.Remove(TEntity entity)
        {
            RemoveCore(entity);
        }

        public void RemoveRange(IEnumerable<TEntity> entities)
        {
            DbSet.RemoveRange(entities);
        }

        TEntity IRepository<TEntity, TPrimaryKey>.Create(bool add)
        {
            return CreateCore(add);
        }

        void IRepository<TEntity, TPrimaryKey>.Update(TEntity entity)
        {
            UpdateCore(entity);
        }

        EntityState IRepository<TEntity, TPrimaryKey>.GetState(TEntity entity)
        {
            return GetStateCore(entity);
        }

        TEntity IRepository<TEntity, TPrimaryKey>.Reload(TEntity entity)
        {
            return ReloadCore(entity);
        }

        Expression<Func<TEntity, TPrimaryKey>> IRepository<TEntity, TPrimaryKey>.GetPrimaryKeyExpression
        {
            get { return getPrimaryKeyExpression; }
        }

        void IRepository<TEntity, TPrimaryKey>.SetPrimaryKey(TEntity entity, TPrimaryKey primaryKey)
        {
            SetPrimaryKeyCore(entity, primaryKey);
        }

        TPrimaryKey IRepository<TEntity, TPrimaryKey>.GetPrimaryKey(TEntity entity)
        {
            return GetPrimaryKeyCore(entity);
        }

        bool IRepository<TEntity, TPrimaryKey>.HasPrimaryKey(TEntity entity)
        {
            return entityTraits.HasPrimaryKey(entity);
        }

        public void ReloadAll()
        {
            // Get all objects in statemanager with entityKey 
            // (context.Refresh will throw an exception otherwise) 
            var refreshableObjects = from entry in ObjectContext.ObjectStateManager.GetObjectStateEntries(
                                                        System.Data.Entity.EntityState.Deleted
                                                      | System.Data.Entity.EntityState.Modified
                                                      | System.Data.Entity.EntityState.Unchanged)
                                      where entry.EntityKey != null
                                      select entry.Entity;

            ObjectContext.Refresh(RefreshMode.StoreWins, refreshableObjects);
        }
        #endregion
    }
}