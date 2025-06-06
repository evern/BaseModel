﻿using BaseModel.Data.Helpers;
using BaseModel.DataModel;
using BaseModel.Misc;
using BaseModel.ViewModel.Services;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.POCO;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Grid.DragDrop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseModel.ViewModel.Loader
{
    public abstract class EntitiesTreeCollectionWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey,
            TMainEntityUnitOfWork> : CollectionViewModelsWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey,
            TMainEntityUnitOfWork>
            where TMainEntity : class, IGuidEntityKey, new()
            where TMainProjectionEntity : class, IGuidEntityKey, IHaveSortOrder, INewEntityName, IHaveExpandState, IGuidParentEntityKey, ICanUpdate, new()
            where TMainEntityUnitOfWork : IUnitOfWork
    {
        private Guid? Parent_GuidOldValue; //stores old parent guid for undo redo
        private List<Guid?> uniqueParent_Guids; //stores dropping entity parent guid before it gets reassigned

        /// <summary>
        /// Get the actual parent entity key field name for undo redo
        /// </summary>
        protected virtual string GetParentEntityKeyFieldName()
        {
            return BindableBase.GetPropertyName(() => new TMainProjectionEntity().ParentEntityKey);
        }

        /// <summary>
        /// Get the actual sort order field name for undo redo
        /// </summary>
        protected virtual string GetSortOrderFieldName()
        {
            return BindableBase.GetPropertyName(() => new TMainProjectionEntity().SortOrder);
        }

        /// <summary>
        /// Required properties of projection must be populated
        /// </summary>
        protected virtual void PopulateNewProjection(TMainProjectionEntity projection)
        {
            projection.NewEntityName = "(new)";
        }

        #region Call Backs
        protected override void AssignCallBacksAndRaisePropertyChange(IEnumerable<TMainProjectionEntity> entities)
        {
            MainViewModel.SetParentViewModel(this);
            base.AssignCallBacksAndRaisePropertyChange(entities);
        }

        //Remove children before parent deletion
        protected override void OnBeforeProjectionsDelete(IEnumerable<TMainProjectionEntity> projections)
        {
            //Undo manager is paused in bulk deletion and will be unpaused in bulk deletion too
            var childrenProjections = new List<TMainProjectionEntity>();
            foreach (var projection in projections)
            {
                var childrenEntitiesInTotal = RecurseFindChildren(projection, MainViewModel.Entities);
                var childrenEntitiesNotInDeletionCollection = new List<TMainProjectionEntity>();
                foreach (var childrenEntityInTotal in childrenEntitiesInTotal)
                    if (!projections.Any(x => x.GUID == childrenEntityInTotal.GUID))
                        childrenEntitiesNotInDeletionCollection.Add(childrenEntityInTotal);

                childrenProjections = childrenProjections.Concat(childrenEntitiesNotInDeletionCollection).ToList();
            }

            uniqueParent_Guids = new List<Guid?>();
            //can't use bulk delete here due to stack overflow
            foreach (var childrenProjection in childrenProjections)
            {
                if (!uniqueParent_Guids.Any(x => x == childrenProjection.ParentEntityKey))
                    uniqueParent_Guids.Add(childrenProjection.ParentEntityKey);

                MainViewModel.EntitiesUndoRedoManager.AddUndo(childrenProjection, null, null, null,
                    EntityMessageType.Deleted);
                MainViewModel.Delete(childrenProjection);
            }
        }

        protected override void OnAfterProjectionsDeleted(IEnumerable<TMainProjectionEntity> entities)
        {
            //Undo manager is paused in bulk deletion and will be unpaused in bulk deletion too
            //uniqueParent_Guids is initialized in EntitiesBeforeDeletion
            foreach (var entity in entities)
                if (!uniqueParent_Guids.Any(x => x == entity.ParentEntityKey))
                    uniqueParent_Guids.Add(entity.ParentEntityKey);

            MainViewModel.EntitiesUndoRedoManager.PauseActionId(); //save will unpause this
            ReorderAndSave(uniqueParent_Guids);
        }

        protected override void OnPersistentAfterAuxiliaryEntitiesChanges(object key, Type changedType, EntityMessageType messageType, object sender, Guid senderKey, bool isBulkRefresh)
        {
            TreeGridControlService?.RefreshData();
            base.OnPersistentAfterAuxiliaryEntitiesChanges(key, changedType, messageType, sender, senderKey, isBulkRefresh);
        }
        #endregion

        #region Reordering
        [ServiceProperty(Key = "TreeGridControlService")]
        public virtual IGridControlService TreeGridControlService { get { return null; } }
        //protected virtual IGridControlService TreeGridControlService { get { return this.GetService<IGridControlService>("TreeGridControlService"); } }

        private void ReorderAndSave(IEnumerable<Guid?> guid_parents)
        {
            var childEntities = new List<TMainProjectionEntity>();
            foreach (var guid_parent in guid_parents)
                childEntities = childEntities.Concat(ReorderAndSave(guid_parent, true)).ToList();

            MainViewModel.BaseBulkSave(childEntities);
            GridControlService?.RefreshData();
            TreeGridControlService?.RefreshData();
        }

        protected virtual void onReorderingPopulateOrderSpecificProperties(TMainProjectionEntity orderingProjection)
        {

        }

        protected virtual void onReorderingPopulateParentSpecificProperties(TMainProjectionEntity parentProjection)
        {

        }

        protected virtual void onReorderingPopulateChildSpecificProperties(TMainProjectionEntity childProjection)
        {

        }

        protected virtual TMainProjectionEntity onAfterReorderingParentProperties(Guid? guid_parent)
        {
            return null;
        }

        private IEnumerable<TMainProjectionEntity> ReorderAndSave(Guid? guid_parent, bool dontSave = false)
        {
            var childProjections = new List<TMainProjectionEntity>(MainViewModel.Entities.Where(x => x.ParentEntityKey == guid_parent).OrderBy(x => x.SortOrder).ToList());

            var alignedSortOrder = 10;
            foreach (var childProjection in childProjections)
            {
                if (childProjection.SortOrder != alignedSortOrder)
                {
                    MainViewModel.EntitiesUndoRedoManager.AddUndo(childProjection,
                        GetSortOrderFieldName(), childProjection.OldSortOrder == null ? childProjection.SortOrder : childProjection.OldSortOrder,
                        alignedSortOrder, EntityMessageType.Changed);

                    childProjection.OldSortOrder = null; //Prepare for next possible drag-drop operation
                    childProjection.SortOrder = alignedSortOrder;
                    childProjection.IsExpanded = true;

                    onReorderingPopulateOrderSpecificProperties(childProjection);
                }

                //when current projection is a parent of any other entity
                if (MainViewModel.Entities.Any(x => x.ParentEntityKey == childProjection.GUID))
                    onReorderingPopulateParentSpecificProperties(childProjection);
                else
                    onReorderingPopulateChildSpecificProperties(childProjection);

                alignedSortOrder += 10;
            }

            TMainProjectionEntity parentEntity = onAfterReorderingParentProperties(guid_parent);
            if (parentEntity != null)
                childProjections.Add(parentEntity);

            if (!dontSave)
            {
                MainViewModel.BaseBulkSave(childProjections);
                TreeGridControlService?.RefreshData();
            }


            return childProjections;
        }

        public static IEnumerable<TMainProjectionEntity> RecurseFindChildren(TMainProjectionEntity parentEntity,
            IEnumerable<TMainProjectionEntity> entities)
        {
            foreach (var entity in entities)
                if (entity.ParentEntityKey == parentEntity.GUID)
                {
                    yield return entity;

                    foreach (var entityChild in RecurseFindChildren(entity, entities))
                        yield return entityChild;
                }
        }
        #endregion

        #region View Commands
        public void DragDropManager_Drop(TreeListDropEventArgs e)
        {
            uniqueParent_Guids = new List<Guid?>();
            Parent_GuidOldValue = null;

            if (e.TargetNode != null)
            {
                MainViewModel.EntitiesUndoRedoManager.PauseActionId(); //save will unpause this
                foreach (var obj in e.DraggedRows)
                {
                    var editROLE = e.SourceManager.GetObject(obj) as TMainProjectionEntity;

                    Parent_GuidOldValue = editROLE.ParentEntityKey;
                    if (!uniqueParent_Guids.Any(x => x == Parent_GuidOldValue))
                        uniqueParent_Guids.Add(Parent_GuidOldValue);
                }
            }
        }

        public void DragDropManager_Dropped(TreeListDroppedEventArgs e)
        {
            Guid? newParentGuid = null;
            if (e.TargetNode != null)
            {
                foreach (TreeListNode obj in e.DraggedRows)
                {
                    var droppedProjection = obj.Content as TMainProjectionEntity;
                    var targetProjection = e.TargetNode.Content as TMainProjectionEntity;

                    droppedProjection.OldSortOrder = droppedProjection.SortOrder;
                    MainViewModel.EntitiesUndoRedoManager.AddUndo(droppedProjection, GetParentEntityKeyFieldName(), Parent_GuidOldValue,
                    droppedProjection.ParentEntityKey, EntityMessageType.Changed);

                    if (e.DropTargetType == DropTargetType.InsertRowsAfter)
                    {
                        droppedProjection.SortOrder = targetProjection.SortOrder + 1;
                    }
                    else if (e.DropTargetType == DropTargetType.InsertRowsBefore)
                    {
                        droppedProjection.SortOrder = targetProjection.SortOrder - 1;
                    }
                    else
                    {
                        var targetParentChilds =
                            MainViewModel.Entities.Where(x => x.ParentEntityKey == targetProjection.GUID);

                        var maxTargetChildrenOrder = 0;
                        if (targetParentChilds.Count() > 0)
                            maxTargetChildrenOrder = targetParentChilds.Max(x => x.SortOrder);

                        maxTargetChildrenOrder += 1;

                        droppedProjection.SortOrder = maxTargetChildrenOrder;

                        MainViewModel.EntitiesUndoRedoManager.AddUndo(droppedProjection, GetParentEntityKeyFieldName(), Parent_GuidOldValue, droppedProjection.ParentEntityKey, EntityMessageType.Changed);
                    }

                    newParentGuid = droppedProjection.ParentEntityKey;
                }

                if (!uniqueParent_Guids.Any(x => x == newParentGuid))
                    uniqueParent_Guids.Add(newParentGuid);

                ReorderAndSave(uniqueParent_Guids);
            }
        }

        public bool CanAddRowBefore()
        {
            return !IsLoading;
        }

        public void AddRowBefore()
        {
            AddRow(false);
        }

        public bool CanAddRowAfter()
        {
            return !IsLoading;
        }

        public void AddRowAfter()
        {
            AddRow(true);
        }

        protected virtual bool OnBeforeAddRow(bool isAfter)
        {
            return true;
        }

        private void AddRow(bool isAfter)
        {
            if (!OnBeforeAddRow(isAfter))
                return;

            var unalignedSortOrder = 0;
            Guid? guid_parent = null;

            if (SelectedEntity != null)
            {
                if (isAfter)
                    unalignedSortOrder = SelectedEntity.SortOrder + 1;
                else
                    unalignedSortOrder = SelectedEntity.SortOrder - 1;

                guid_parent = SelectedEntity.ParentEntityKey;
            }

            var newRow = new TMainProjectionEntity();
            PopulateNewProjection(newRow);
            newRow.SortOrder = unalignedSortOrder;
            newRow.ParentEntityKey = guid_parent;
            newRow.IsExpanded = true;

            //Handled in Bulk Save
            //MainViewModel.EntitiesUndoRedoManager.PauseActionId(); //Save will unpause this
            //MainViewModel.EntitiesUndoRedoManager.AddUndo(newRow, null, null, null, EntityMessageType.Added);
            MainViewModel.Save(newRow);
            ReorderAndSave(guid_parent);
        }
        #endregion
    }
}
