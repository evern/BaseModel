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

namespace BaseModel.ViewModel.Loader
{
    public abstract class ProjectionStaticMasterOtherDetailCollectionsWrapper<TStaticEntity, TMainEntity, TChildEntity, TAllEntityPrimaryKey,
        TMainEntityUnitOfWork> : CollectionViewModelsWrapper<TStaticEntity, TStaticEntity, TAllEntityPrimaryKey,
        TMainEntityUnitOfWork>
        where TStaticEntity : class, IHaveDetail<TMainEntity>, IGuidEntityKey, ICanUpdate, IHaveSortOrder, new()
        where TMainEntity : class, IHaveDetail<TChildEntity>, IGuidEntityKey, IGuidParentEntityKey, ICanUpdate, new()
        where TChildEntity : class, IGuidEntityKey, IGuidParentEntityKey, new()
        where TMainEntityUnitOfWork : IUnitOfWork
    {
        protected virtual bool MainOnBeforeParentAssigned(TStaticEntity staticEntity, TMainEntity mainEntity)
        {
            return true;
        }

        protected virtual bool ChildOnBeforeParentAssigned(TMainEntity mainEntity, TChildEntity childEntity)
        {
            return true;
        }

        protected override void AssignCallBacksAndRaisePropertyChange(IEnumerable<TStaticEntity> entities)
        {
            MainEntitiesViewModel.OnBeforeNewRowSavedIsContinueFromViewCallBack = newMainRowAddUndoAndSave;
            ChildEntitiesViewModel.OnBeforeNewRowSavedIsContinueFromViewCallBack = newChildrenRowAddUndoAndSave;
            MainViewModel.SetParentViewModel(this);
            base.AssignCallBacksAndRaisePropertyChange(entities);
        }

        #region Call Backs
        private bool newMainRowAddUndoAndSave(RowEventArgs e, TMainEntity mainEntity)
        {
            var gridView = (GridViewBase)e.Source;
            var grid = gridView.Grid;
            var masterGrid = grid.GetMasterGrid();

            if (masterGrid != null)
            {
                var masterRowHandle = grid.GetMasterRowHandle();
                var masterEntity = (TStaticEntity)masterGrid.GetRow(masterRowHandle);
                if (MainOnBeforeParentAssigned(masterEntity, mainEntity))
                {
                    IHaveCreatedDate mainEntityCreatedDate = mainEntity as IHaveCreatedDate;
                    if (mainEntityCreatedDate != null)
                    {
                        if (mainEntityCreatedDate.EntityCreatedDate.Year == 1)
                            mainEntityCreatedDate.EntityCreatedDate = DateTime.Now;
                    }

                    IOriginalGuidEntityKey masterEntityOriginalKey = masterEntity as IOriginalGuidEntityKey;
                    if (masterEntityOriginalKey == null)
                        mainEntity.ParentEntityKey = masterEntity.GUID;
                    else
                        mainEntity.ParentEntityKey = masterEntityOriginalKey.OriginalEntityKey;
                }
            }

            return true;
        }

        private bool newChildrenRowAddUndoAndSave(RowEventArgs e, TChildEntity childEntity)
        {
            var gridView = (GridViewBase)e.Source;
            var grid = gridView.Grid;
            var masterGrid = grid.GetMasterGrid();

            if (masterGrid != null)
            {
                var masterRowHandle = grid.GetMasterRowHandle();
                var masterEntity = (TMainEntity)masterGrid.GetRow(masterRowHandle);
                if (ChildOnBeforeParentAssigned(masterEntity, childEntity))
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

        private List<Guid> restoreMainExpandedGuids = new List<Guid>();
        ObservableCollection<TStaticEntity> entities;
        public override ObservableCollection<TStaticEntity> Entities
        {
            get
            {
                if (MainViewModel == null)
                    return null;

                if (entities == null)
                {
                    entities = new ObservableCollection<TStaticEntity>();
                    var staticEntities = MainViewModel.Entities.Where(x => x.IsLast).OrderBy(x => x.DisplayNumber);
                    var mainEntities = main_entities;
                    var childEntities = child_entities;
                    foreach(var staticEntity in staticEntities)
                    {
                        entities.Add(staticEntity);
                    }

                    foreach(var entity in entities)
                    {
                        IEnumerable<TMainEntity> currentMainProjectionEntities = mainEntities.Where(y => y.ParentEntityKey == entity.GUID);
                        foreach(var currentMainProjectionEntity in currentMainProjectionEntities)
                        {
                            entity.DetailEntities.Add(currentMainProjectionEntity);
                        }
                    }

                    IEnumerable<TMainEntity> mainProjectionEntities = entities.SelectMany(x => x.DetailEntities);
                    foreach (var parentEntity in mainProjectionEntities)
                    {
                        IEnumerable<TChildEntity> currentChildEntities = childEntities.Where(y => y.ParentEntityKey == parentEntity.GUID);
                        foreach (var currentChildEntity in currentChildEntities)
                        {
                            parentEntity.DetailEntities.Add(currentChildEntity);
                        }
                    }
                }

                return entities;
            }
        }

        protected abstract IEnumerable<TChildEntity> child_entities { get; }

        protected abstract IEnumerable<TMainEntity> main_entities { get; }


        public abstract CollectionViewModel<TChildEntity, TChildEntity, TAllEntityPrimaryKey, TMainEntityUnitOfWork> ChildEntitiesViewModel { get;}

        public abstract CollectionViewModel<TMainEntity, TMainEntity, TAllEntityPrimaryKey, TMainEntityUnitOfWork> MainEntitiesViewModel { get; }

        #region View Refresh
        public override void OnAfterAuxiliaryEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, Guid senderKey, bool isBulkRefresh)
        {
            mainThreadDispatcher.BeginInvoke(new Action(() => refreshEntities()));
        }

        private void refreshEntities()
        {
            entities = null;
            this.RaisePropertyChanged(x => x.Entities);
            onAfterRefresh();
        }

        protected abstract string expand_key_field_name { get; }

        //public Action<TMainProjectionEntity> SetIsRowExpanded;
        protected override void onAfterRefresh()
        {
            if(Entities != null)
                foreach (var entity in Entities)
                {
                    GridControlService.SetRowExpandedByColumnValue(expand_key_field_name, entity);
                }
        }

        public void MasterRowExpanded(RowEventArgs e)
        {
            Guid expandedGuid = ((TStaticEntity)e.Row).GUID;
            if (!restoreMainExpandedGuids.Any(x => x == expandedGuid))
                restoreMainExpandedGuids.Add(((TStaticEntity)e.Row).GUID);
        }

        public void MasterRowCollapsed(RowEventArgs e)
        {
            restoreMainExpandedGuids.RemoveAll(x => x == ((TStaticEntity)e.Row).GUID);
        }
        #endregion
    }
}
