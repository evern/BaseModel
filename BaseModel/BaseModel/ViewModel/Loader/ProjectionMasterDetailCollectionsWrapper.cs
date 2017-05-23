using BaseModel.Data.Helpers;
using BaseModel.DataModel;
using BaseModel.Misc;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using DevExpress.Mvvm.POCO;

namespace BaseModel.ViewModel.Loader
{
    public abstract class ProjectionMasterDetailCollectionsWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey,
        TMainEntityUnitOfWork> : CollectionViewModelsWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey,
        TMainEntityUnitOfWork>
        where TMainEntity : class, IGuidEntityKey, IGuidParentEntityKey, new()
        where TMainProjectionEntity : class, IProjectionMasterDetail<TMainEntity, TMainProjectionEntity>, new()
        where TMainEntityUnitOfWork : IUnitOfWork
    {
        protected virtual bool OnBeforeParentAssigned(TMainProjectionEntity masterEntity, TMainProjectionEntity childEntity)
        {
            return true;
        }

        protected override void AssignCallBacksAndRaisePropertyChange(IEnumerable<TMainProjectionEntity> entities)
        {
            MainViewModel.OnBeforeEntitiesDeleteCallBack = EntitiesBeforeDeletion;
            MainViewModel.IsContinueNewRowFromViewCallBack = NewRowAddUndoAndSave;
            MainViewModel.ApplyProjectionPropertiesToEntityCallBack = ApplyProjectionPropertiesToEntity;

            MainViewModel.SetParentViewModel(this);
            base.AssignCallBacksAndRaisePropertyChange(entities);
        }
        #region Call Backs
        private bool NewRowAddUndoAndSave(RowEventArgs e, TMainProjectionEntity projectionEntity)
        {
            var gridView = (GridViewBase)e.Source;
            var grid = gridView.Grid;
            var masterGrid = grid.GetMasterGrid();

            if (masterGrid != null)
            {
                var masterRowHandle = grid.GetMasterRowHandle();
                var masterEntity = (TMainProjectionEntity)masterGrid.GetRow(masterRowHandle);
                if (OnBeforeParentAssigned(masterEntity, projectionEntity))
                    projectionEntity.Entity.ParentEntityKey = masterEntity.EntityKey;
            }

            return true;
        }

        private void EntitiesBeforeDeletion(IEnumerable<IProjectionMasterDetail<TMainEntity, TMainProjectionEntity>> projections)
        {
            //Undo manager is paused in bulk deletion and will be unpaused in bulk deletion too
            var childrenEntities = new List<TMainProjectionEntity>();
            var parentEntitiesNotInList =
                new List<TMainProjectionEntity>();

            foreach (var projection in projections)
            {
                var childrenEntitiesInTotal = projection.DetailEntities;
                var childrenEntitiesNotInDeletionCollection =
                    new List<TMainProjectionEntity>();
                foreach (var childrenEntityInTotal in childrenEntitiesInTotal)
                    if (!projections.Any(x => x.EntityKey == childrenEntityInTotal.EntityKey))
                        childrenEntitiesNotInDeletionCollection.Add(childrenEntityInTotal);

                TMainProjectionEntity parentEntity = null;
                if (projection.Entity.ParentEntityKey != Guid.Empty)
                {
                    parentEntity = MainViewModel.Entities.FirstOrDefault(x => x.EntityKey == projection.Entity.ParentEntityKey);
                    if (parentEntity != null)
                        if (!projections.Any(x => x.EntityKey == parentEntity.EntityKey))
                            parentEntitiesNotInList.Add(parentEntity);
                }

                childrenEntities = childrenEntities.Concat(childrenEntitiesNotInDeletionCollection).ToList();
            }

            //can't use bulk delete here due to stack overflow
            foreach (var childrenEntity in childrenEntities)
            {
                MainViewModel.EntitiesUndoRedoManager.AddUndo(childrenEntity, null, null, null,
                    EntityMessageType.Deleted);
                MainViewModel.Delete(childrenEntity);
            }
        }

        private void ApplyProjectionPropertiesToEntity(TMainProjectionEntity projectionEntity, TMainEntity entity)
        {
            OnBeforeApplyProjectionPropertiesToEntity(projectionEntity, entity);
            DataUtils.ShallowCopy(entity, projectionEntity.Entity);

            IHaveCreatedDate iHaveCreatedDateEntity = entity as IHaveCreatedDate;
            IHaveCreatedDate iHaveCreatedDateProjectionEntity = projectionEntity.Entity as IHaveCreatedDate;
            if (iHaveCreatedDateEntity != null && iHaveCreatedDateProjectionEntity != null)
            {
                //workaround for created because Save() only sets the projection primary key, this is used for property redo where the interceptor only tampers with UPDATED and CREATED is left as null
                if (iHaveCreatedDateEntity.EntityCreatedDate.Date.Year == 1)
                    iHaveCreatedDateProjectionEntity.EntityCreatedDate = DateTime.Now;

                iHaveCreatedDateEntity.EntityCreatedDate = iHaveCreatedDateProjectionEntity.EntityCreatedDate;
            }
        }

        protected virtual void OnBeforeApplyProjectionPropertiesToEntity(TMainProjectionEntity projectionEntity, TMainEntity entity)
        {

        }
        #endregion

        private List<Guid> RestoreExpandedGuids = new List<Guid>();
        ObservableCollection<TMainProjectionEntity> displayEntities;
        public override ObservableCollection<TMainProjectionEntity> DisplayEntities
        {
            get
            {
                if (MainViewModel == null)
                    return null;

                if (displayEntities == null)
                {
                    displayEntities = new ObservableCollection<TMainProjectionEntity>();
                    var parentEntities = MainViewModel.Entities.Where(x => x.Entity.ParentEntityKey == null).AsEnumerable();
                    var childEntities = MainViewModel.Entities.Where(x => x.Entity.ParentEntityKey != null).AsEnumerable();
                    foreach (var parentEntity in parentEntities)
                    {
                        var parentEntityPOCO = new TMainProjectionEntity();
                        DataUtils.ShallowCopy(parentEntityPOCO, parentEntity);
                        DataUtils.ShallowCopy(parentEntityPOCO.Entity, parentEntity.Entity);

                        parentEntityPOCO.IsExpanded = RestoreExpandedGuids.Any(x => x == parentEntity.EntityKey);
                        displayEntities.Add(parentEntityPOCO);
                    }

                    foreach (var displayEntity in displayEntities)
                    {
                        var currentChildEntities = childEntities.Where(y => y.Entity.ParentEntityKey == displayEntity.EntityKey);
                        foreach (var childCOMMODITY_GROUP_DIRECT in currentChildEntities)
                        {
                            var childCOMMODITY_GROUP_DIRECTPOCO = new TMainProjectionEntity();
                            childCOMMODITY_GROUP_DIRECTPOCO.EntityKey = childCOMMODITY_GROUP_DIRECT.EntityKey;
                            DataUtils.ShallowCopy(childCOMMODITY_GROUP_DIRECTPOCO.Entity, childCOMMODITY_GROUP_DIRECT.Entity);
                            displayEntity.DetailEntities.Add(childCOMMODITY_GROUP_DIRECTPOCO);
                        }
                    }
                }

                return displayEntities;
            }
        }

        #region View Refresh
        public override void OnAfterAffectingEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender)
        {
            mainThreadDispatcher.BeginInvoke(new Action(() => refreshDisplayEntities()));
        }

        private void refreshDisplayEntities()
        {
            displayEntities = null;
            this.RaisePropertyChanged(x => x.DisplayEntities);
            restoreViewState();
        }

        public Action<TMainProjectionEntity> SetIsRowExpanded;
        protected override void restoreViewState()
        {
            base.restoreViewState();
            foreach (var entity in DisplayEntities)
            {
                SetIsRowExpanded?.Invoke(entity);
            }
        }

        public void MasterRowExpanded(RowEventArgs e)
        {
            Guid expandedGuid = ((TMainProjectionEntity)e.Row).EntityKey;
            if (!RestoreExpandedGuids.Any(x => x == expandedGuid))
                RestoreExpandedGuids.Add(((TMainProjectionEntity)e.Row).EntityKey);
        }

        public void MasterRowCollapsed(RowEventArgs e)
        {
            RestoreExpandedGuids.RemoveAll(x => x == ((TMainProjectionEntity)e.Row).EntityKey);
        }
        #endregion
    }
}
