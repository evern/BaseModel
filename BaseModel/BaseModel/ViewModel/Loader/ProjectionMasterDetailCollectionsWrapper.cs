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
        where TMainEntity : class, IGuidEntityKey, IHaveCreatedDate, IGuidParentEntityKey, new()
        where TMainProjectionEntity : class, IProjectionMasterDetail<TMainEntity, TMainProjectionEntity>, ICanUpdate, new()
        where TMainEntityUnitOfWork : IUnitOfWork
    {
        protected virtual bool OnBeforeParentAssigned(TMainProjectionEntity masterEntity, TMainProjectionEntity childEntity)
        {
            return true;
        }

        protected override void AssignCallBacksAndRaisePropertyChange(IEnumerable<TMainProjectionEntity> entities)
        {
            MainViewModel.OnBeforeNewRowSavedIsContinueFromViewCallBack = NewRowAddUndoAndSave;

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
                {
                    IOriginalGuidEntityKey masterEntityOriginalKey = masterEntity as IOriginalGuidEntityKey;
                    if (masterEntityOriginalKey == null)
                        projectionEntity.Entity.ParentEntityKey = masterEntity.GUID;
                    else
                        projectionEntity.Entity.ParentEntityKey = masterEntityOriginalKey.OriginalEntityKey;
                }

            }

            return true;
        }

        protected override void OnBeforeProjectionsDelete(IEnumerable<TMainProjectionEntity> projections)
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
                    if (!projections.Any(x => x.GUID == childrenEntityInTotal.GUID))
                        childrenEntitiesNotInDeletionCollection.Add(childrenEntityInTotal);

                TMainProjectionEntity parentEntity = null;
                if (projection.Entity.ParentEntityKey != Guid.Empty)
                {
                    parentEntity = MainViewModel.Entities.FirstOrDefault(x => x.GUID == projection.Entity.ParentEntityKey);
                    if (parentEntity != null)
                        if (!projections.Any(x => x.GUID == parentEntity.GUID))
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
        #endregion

        private List<Guid> RestoreExpandedGuids = new List<Guid>();
        protected ObservableCollection<TMainProjectionEntity> entities;
        public override ObservableCollection<TMainProjectionEntity> Entities
        {
            get
            {
                if (MainViewModel == null)
                    return null;

                if (entities == null)
                {
                    entities = new ObservableCollection<TMainProjectionEntity>();
                    var parentEntities = MainViewModel.Entities.Where(x => x.Entity.ParentEntityKey == null).AsEnumerable();
                    var childEntities = MainViewModel.Entities.Where(x => x.Entity.ParentEntityKey != null).AsEnumerable();

                    IEnumerable<TMainProjectionEntity> filteredParentEntities = parentEntities.Where(x => parentEntitiesFilter(x));

                    foreach (var parentEntity in filteredParentEntities.OrderBy(x => parentEntitiesOrder(x)))
                    {
                        var parentEntityPOCO = new TMainProjectionEntity();
                        DataUtils.ShallowCopy(parentEntityPOCO, parentEntity);
                        DataUtils.ShallowCopy(parentEntityPOCO.Entity, parentEntity.Entity);

                        parentEntityPOCO.IsExpanded = RestoreExpandedGuids.Any(x => x == parentEntity.GUID);
                        entities.Add(parentEntityPOCO);
                    }

                    //foreach added parent
                    foreach (var entity in entities)
                    {
                        IOriginalGuidEntityKey displayEntityWithOriginalKey = entity as IOriginalGuidEntityKey;

                        IEnumerable<TMainProjectionEntity> currentChildEntities;
                        if (displayEntityWithOriginalKey == null)
                            currentChildEntities = childEntities.Where(y => y.Entity.ParentEntityKey == entity.GUID);
                        else
                            currentChildEntities = childEntities.Where(y => y.Entity.ParentEntityKey == displayEntityWithOriginalKey.OriginalEntityKey);

                        IEnumerable<TMainProjectionEntity> filteredChildEntities = currentChildEntities.Where(x => childEntitiesFilter(x));

                        foreach (var currentChildEntity in filteredChildEntities.OrderBy(x => childEntitiesOrder(x)))
                        {
                            var currentChildEntityPOCO = new TMainProjectionEntity();
                            //childCOMMODITY_GROUP_DIRECTPOCO.EntityKey = childCOMMODITY_GROUP_DIRECT.EntityKey;
                            DataUtils.ShallowCopy(currentChildEntityPOCO, currentChildEntity);
                            DataUtils.ShallowCopy(currentChildEntityPOCO.Entity, currentChildEntity.Entity);
                            entity.DetailEntities.Add(currentChildEntityPOCO);
                        }
                    }
                }

                return entities;
            }
        }

        protected virtual object parentEntitiesOrder(TMainProjectionEntity x)
        {
            return x.Entity.EntityCreatedDate;
        }

        protected virtual object childEntitiesOrder(TMainProjectionEntity x)
        {
            return x.Entity.EntityCreatedDate;
        }

        protected virtual bool parentEntitiesFilter(TMainProjectionEntity x)
        {
            return true;
        }

        protected virtual bool childEntitiesFilter(TMainProjectionEntity x)
        {
            return true;
        }

        #region View Refresh
        public override void OnAfterAuxiliaryEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh)
        {
            mainThreadDispatcher.BeginInvoke(new Action(() => RefreshEntities()));
        }

        public override void FullRefresh()
        {
            if (!CanFullRefresh())
                return;

            base.FullRefresh();
            RefreshEntities();
        }

        protected void RefreshEntities()
        {
            entities = null;
            this.RaisePropertyChanged(x => x.Entities);
            restoreRowExpansionState();
        }

        protected abstract string expand_key_field_name { get; }

        //public Action<TMainProjectionEntity> SetIsRowExpanded;
        protected override void onAfterRefresh()
        {
            entities = null;
            this.RaisePropertyChanged(x => x.Entities);
            restoreRowExpansionState();
        }

        private void restoreRowExpansionState()
        {
            if (Entities != null)
                foreach (var entity in Entities)
                {
                    GridControlService.SetRowExpandedByColumnValue(expand_key_field_name, entity);
                }
        }

        public void MasterRowExpanded(RowEventArgs e)
        {
            Guid expandedGuid = ((TMainProjectionEntity)e.Row).GUID;
            if (!RestoreExpandedGuids.Any(x => x == expandedGuid))
                RestoreExpandedGuids.Add(((TMainProjectionEntity)e.Row).GUID);
        }

        public void MasterRowCollapsed(RowEventArgs e)
        {
            RestoreExpandedGuids.RemoveAll(x => x == ((TMainProjectionEntity)e.Row).GUID);
        }
        #endregion
    }
}
