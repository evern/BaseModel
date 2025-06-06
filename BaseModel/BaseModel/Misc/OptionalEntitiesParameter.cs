﻿using BaseModel.Misc;

namespace BaseModel.Misc
{
    /// <summary>
    /// Supports generating unique id for document id, TEntity must have GUID field
    /// </summary>
    public class EntitiesParameter<TEntity>
        where TEntity : class
    {
        private TEntity entity;

        public EntitiesParameter(TEntity entity)
        {
            this.entity = entity;
        }

        public TEntity GetEntity()
        {
            return entity;
        }

        public override string ToString()
        {
            if (entity != null)
            {
                IGuidEntityKey entityWithGuid = entity as IGuidEntityKey;
                if (entityWithGuid != null)
                    return entityWithGuid.GUID.ToString();
                else
                    return string.Empty;
            }

            else
                return string.Empty;
        }
    }

    public class TripleEntitiesParameter<TEntity, TSecondEntity, TThirdEntity>
        : DualEntitiesParameter<TEntity, TSecondEntity>
        where TEntity : class
        where TSecondEntity : class
        where TThirdEntity : class
    {
        private TThirdEntity thirdEntity;

        public TripleEntitiesParameter(TEntity entity, TSecondEntity secondEntity, TThirdEntity thirdEntity)
            : base(entity, secondEntity)
        {
            this.thirdEntity = thirdEntity;
        }

        public TThirdEntity GetThirdEntity()
        {
            return this.thirdEntity;
        }
    }


    public class DualEntitiesParameter<TEntity, TSecondEntity>
        where TEntity : class
        where TSecondEntity : class
    {
        private TEntity entity;
        private TSecondEntity secondEntity;

        public DualEntitiesParameter(TEntity entity, TSecondEntity secondEntity)
        {
            this.entity = entity;
            this.secondEntity = secondEntity;
        }

        public TEntity GetFirstEntity()
        {
            return entity;
        }

        public TSecondEntity GetSecondEntity()
        {
            return secondEntity;
        }

        public override string ToString()
        {
            if (entity != null)
            {
                IGuidEntityKey entityWithGuid = entity as IGuidEntityKey;

                if (entityWithGuid != null)
                    return entityWithGuid.GUID.ToString();
                else
                    return string.Empty;
            }
            else if (secondEntity != null)
            {
                IGuidEntityKey entityWithGuid = secondEntity as IGuidEntityKey;
                if (entityWithGuid != null)
                    return entityWithGuid.GUID.ToString();
                else
                    return string.Empty;
            }
            else
                return string.Empty;
        }
    }
}