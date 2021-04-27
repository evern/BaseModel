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
using BaseModel.ViewModel.Base;
using System.Windows.Data;

namespace BaseModel.ViewModel.Loader
{
    public abstract class ProjectionMasterOtherDetailCollectionsWrapper<TMainEntity, TChildEntity, TMainProjectionEntity, TMainEntityPrimaryKey,
        TMainEntityUnitOfWork> : CollectionViewModelsWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey,
        TMainEntityUnitOfWork>
        where TMainEntity : class, IGuidEntityKey, new()
        where TChildEntity : class, IGuidEntityKey, IGuidParentEntityKey, new()
        where TMainProjectionEntity : class, IProjectionMasterOtherDetail<TMainEntity, TChildEntity>, ICanUpdate, new()
        where TMainEntityUnitOfWork : IUnitOfWork
    {
        protected virtual bool OnBeforeParentAssigned(TMainProjectionEntity masterEntity, TChildEntity childEntity)
        {
            return true;
        }

        protected override void AssignCallBacksAndRaisePropertyChange(IEnumerable<TMainProjectionEntity> entities)
        {
            ChildEntitiesViewModel.OnBeforeNewRowSavedIsContinueFromViewCallBack = onBeforeViewNewRowSavedIsContinue;
            MainViewModel.SetParentViewModel(this);
            base.AssignCallBacksAndRaisePropertyChange(entities);
        }

        #region Call Backs
        private bool onBeforeViewNewRowSavedIsContinue(RowEventArgs e, TChildEntity childEntity)
        {
            var gridView = (GridViewBase)e.Source;
            var grid = gridView.Grid;
            var masterGrid = grid.GetMasterGrid();

            if (masterGrid != null)
            {
                var masterRowHandle = grid.GetMasterRowHandle();
                var masterEntity = (TMainProjectionEntity)masterGrid.GetRow(masterRowHandle);
                if (OnBeforeParentAssigned(masterEntity, childEntity))
                {
                    IHaveCreatedDate childEntityCreatedDate = childEntity as IHaveCreatedDate;
                    if (childEntityCreatedDate != null)
                    {
                        if (childEntityCreatedDate.EntityCreatedDate.Year == 1)
                            childEntityCreatedDate.EntityCreatedDate = DateTime.Now;
                    }

                    IOriginalGuidEntityKey masterEntityOriginalKey = masterEntity as IOriginalGuidEntityKey;
                    if (masterEntityOriginalKey == null)
                        childEntity.ParentEntityKey = masterEntity.GUID;
                    else
                        childEntity.ParentEntityKey = masterEntityOriginalKey.OriginalEntityKey;
                }

            }

            return true;
        }
        #endregion

        private List<Guid> RestoreExpandedGuids = new List<Guid>();
        ObservableCollection<TMainProjectionEntity> entities;
        public override ObservableCollection<TMainProjectionEntity> Entities
        {
            get
            {
                if (MainViewModel == null)
                    return null;

                if (entities == null)
                {
                    entities = new ObservableCollection<TMainProjectionEntity>();
                    var parentEntities = MainViewModel.Entities;
                    var childEntities = child_entities;
                    foreach (var parentEntity in parentEntities)
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

                        IEnumerable<TChildEntity> currentChildEntities;
                        if (displayEntityWithOriginalKey == null)
                            currentChildEntities = childEntities.Where(y => y.ParentEntityKey == entity.GUID);
                        else
                            currentChildEntities = childEntities.Where(y => y.ParentEntityKey == displayEntityWithOriginalKey.OriginalEntityKey);

                        foreach (var currentChildEntity in currentChildEntities)
                        {
                            entity.DetailEntities.Add(currentChildEntity);
                        }
                    }
                }

                return entities;
            }
        }

        protected abstract IEnumerable<TChildEntity> child_entities { get; }

        public abstract CollectionViewModel<TChildEntity, TChildEntity, Guid, TMainEntityUnitOfWork> ChildEntitiesViewModel { get;}

        #region View Refresh
        public override void OnAfterAuxiliaryEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, Guid senderKey, bool isBulkRefresh)
        {
            mainThreadDispatcher.BeginInvoke(new Action(() => refreshEntities()));
        }

        private void refreshEntities()
        {
            entities = null;
            this.RaisePropertyChanged(x => x.Entities);
            loadDataPointsTable();
        }

        protected abstract string expand_key_field_name { get; }

        //public Action<TMainProjectionEntity> SetIsRowExpanded;
        protected override bool loadDataPointsTable()
        {
            if(Entities != null)
                foreach (var entity in Entities)
                {
                    GridControlService.SetRowExpandedByColumnValue(expand_key_field_name, entity);
                }

            return true;
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
