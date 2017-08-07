using BaseModel.Data.Helpers;
using BaseModel.DataModel;
using BaseModel.Misc;
using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseModel.ViewModel.Loader
{
    public abstract class ProjectionTreeCollectionWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey,
        TMainEntityUnitOfWork> : EntitiesTreeCollectionWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey,
        TMainEntityUnitOfWork>
        where TMainEntity : class, IGuidEntityKey, new()
        where TMainProjectionEntity : class, IProjection<TMainEntity>, IHaveSortOrder, IHaveExpandState, IGuidParentEntityKey, ICanUpdate, new()
        where TMainEntityUnitOfWork : IUnitOfWork
    {
        #region Call Backs
        //protected override void AssignCallBacksAndRaisePropertyChange(IEnumerable<TMainProjectionEntity> entities)
        //{
        //    MainViewModel.ApplyProjectionPropertiesToEntityCallBack = ApplyProjectionPropertiesToEntity;
        //    base.AssignCallBacksAndRaisePropertyChange(entities);
        //}

        //private void ApplyProjectionPropertiesToEntity(TMainProjectionEntity projectionEntity, TMainEntity entity)
        //{
        //    DataUtils.ShallowCopy(entity, projectionEntity.Entity);

        //    IHaveCreatedDate iHaveCreatedDateProjectionEntity = projectionEntity.Entity as IHaveCreatedDate;
        //    if (iHaveCreatedDateProjectionEntity != null)
        //    {
        //        //workaround for created because Save() only sets the projection primary key, this is used for property redo where the interceptor only tampers with UPDATED and CREATED is left as null
        //        if (iHaveCreatedDateProjectionEntity.EntityCreatedDate.Date.Year == 1)
        //            iHaveCreatedDateProjectionEntity.EntityCreatedDate = DateTime.Now;
        //    }
        //}
        #endregion
    }
}
