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
                    var parentEntities = MainViewModel.Entities;
                    var childEntities = child_entities;
                    foreach (var parentEntity in parentEntities)
                    {
                        var parentEntityPOCO = new TMainProjectionEntity();
                        DataUtils.ShallowCopy(parentEntityPOCO, parentEntity);
                        DataUtils.ShallowCopy(parentEntityPOCO.Entity, parentEntity.Entity);

                        parentEntityPOCO.IsExpanded = RestoreExpandedGuids.Any(x => x == parentEntity.GUID);
                        displayEntities.Add(parentEntityPOCO);
                    }

                    //foreach added parent
                    foreach (var displayEntity in displayEntities)
                    {
                        IOriginalGuidEntityKey displayEntityWithOriginalKey = displayEntity as IOriginalGuidEntityKey;

                        IEnumerable<TChildEntity> currentChildEntities;
                        if (displayEntityWithOriginalKey == null)
                            currentChildEntities = childEntities.Where(y => y.ParentEntityKey == displayEntity.GUID);
                        else
                            currentChildEntities = childEntities.Where(y => y.ParentEntityKey == displayEntityWithOriginalKey.OriginalEntityKey);

                        foreach (var currentChildEntity in currentChildEntities)
                        {
                            displayEntity.DetailEntities.Add(currentChildEntity);
                        }
                    }
                }

                return displayEntities;
            }
        }

        protected abstract IEnumerable<TChildEntity> child_entities { get; }

        public abstract CollectionViewModel<TChildEntity, TChildEntity, Guid, TMainEntityUnitOfWork> ChildEntitiesViewModel { get;}

        #region View Refresh
        public override void OnAfterAuxiliaryEntitiesChanged(object key, Type changedType, EntityMessageType messageType, object sender, bool isBulkRefresh)
        {
            mainThreadDispatcher.BeginInvoke(new Action(() => refreshDisplayEntities()));
        }

        private void refreshDisplayEntities()
        {
            displayEntities = null;
            this.RaisePropertyChanged(x => x.DisplayEntities);
            onAfterRefresh();
        }

        protected abstract string expand_key_field_name { get; }

        //public Action<TMainProjectionEntity> SetIsRowExpanded;
        protected override void onAfterRefresh()
        {
            if(DisplayEntities != null)
                foreach (var entity in DisplayEntities)
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
