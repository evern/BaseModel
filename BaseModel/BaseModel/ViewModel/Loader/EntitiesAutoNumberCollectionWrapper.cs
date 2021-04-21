using BaseModel.Data.Helpers;
using BaseModel.DataModel;
using BaseModel.Helpers;
using BaseModel.Misc;
using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseModel.ViewModel.Loader
{
    public abstract class EntitiesAutoNumberCollectionWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey,
            TMainEntityUnitOfWork> : CollectionViewModelsWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey,
            TMainEntityUnitOfWork>
            where TMainEntity : class, IGuidEntityKey, IEntityNumber, new()
            where TMainProjectionEntity : class, IGuidEntityKey, IEntityNumber, ICanUpdate, new()
            where TMainEntityUnitOfWork : IUnitOfWork
    {
        /// <summary>
        /// Get the project specific numeric field length
        /// </summary>
        protected abstract int? DefaultNumericFieldLength();
        protected abstract bool AllowReorderingOnDeletion();

        #region Call Backs
        protected override void AssignCallBacksAndRaisePropertyChange(IEnumerable<TMainProjectionEntity> entities)
        {
            //MainViewModel.IsContinueNewRowFromViewCallBack += IsContinueNewRowFromViewCallBack;
            MainViewModel.OnAfterProjectionsDeletedCallBack = OnAfterProjectionDeletedCallBack;
            MainViewModel.SetParentViewModel(this);
            base.AssignCallBacksAndRaisePropertyChange(entities);
        }
        
        protected bool IsContinueNewRowFromViewCallBack(RowEventArgs e, TMainProjectionEntity projection)
        {
            IEnumerable<TMainProjectionEntity> entitiesInOrder = MainViewModel.Entities.Where(x => x.EntityGroup == projection.EntityGroup).OrderBy(x => x.EntitySortNumber);
            if(entitiesInOrder.Count() == 0)
            {
                projection.EntityNumber = StringFormatUtils.AppendStringWithEnumerator(string.Empty, 0, DefaultNumericFieldLength());
                return true;
            }

            TMainProjectionEntity largestNumberEntity = entitiesInOrder.Last();
            string largestNumberString = largestNumberEntity.EntityNumber;
            int numericFieldLength = 0;
            long largestNumberValueOnly = 0;
            string largestNumberStringOnly = StringFormatUtils.ParseStringIntoComponents(largestNumberString, out numericFieldLength, out largestNumberValueOnly);
            long newRowNumber = largestNumberValueOnly + 1;
            projection.EntityNumber = StringFormatUtils.AppendStringWithEnumerator(string.Empty, newRowNumber, DefaultNumericFieldLength());

            return true;
        }

        public bool CanDuplicate()
        {
            if (MainViewModel == null || SelectedEntities == null)
                return false;

            return true;
        }

        public void DuplicateMultiple(BarEditItem barEdit)
        {
            MainViewModel.EntitiesUndoRedoManager.PauseActionId();
            _isProcessingMultiple = true;
            var timesToDuplicate = 0;
            List<TMainProjectionEntity> newEntities = new List<TMainProjectionEntity>();
            if (int.TryParse(barEdit.EditValue.ToString(), out timesToDuplicate))
            {
                List<TMainProjectionEntity> currentEnumerationSaveEntities = getNewDuplicateEntities(timesToDuplicate, false, MainViewModel.Entities, MainViewModel.SelectedEntities);
                newEntities.AddRange(currentEnumerationSaveEntities);
            }

            MainViewModel.BaseBulkSave(newEntities);
            _isProcessingMultiple = false;
            MainViewModel.EntitiesUndoRedoManager.UnpauseActionId();
        }

        public void Duplicate()
        {
            //Handled in bulk save
            //if (!_isProcessingMultiple)
            //    MainViewModel.EntitiesUndoRedoManager.PauseActionId();

            List<TMainProjectionEntity> newEntities = getNewDuplicateEntities(1, false, MainViewModel.Entities, MainViewModel.SelectedEntities);
            MainViewModel.BaseBulkSave(newEntities);

            //Handled in bulk save
            //foreach (TMainProjectionEntity newEntity in newEntities)
            //    MainViewModel.EntitiesUndoRedoManager.AddUndo(newEntity, null, null, null, EntityMessageType.Added);

            //Handled in bulk save
            //if (!_isProcessingMultiple)
            //    MainViewModel.EntitiesUndoRedoManager.UnpauseActionId();
        }


        private List<TMainProjectionEntity> getNewDuplicateEntities(int timesToDuplicate, bool isInsert, IEnumerable<TMainProjectionEntity> all_entities, IEnumerable<TMainProjectionEntity> selected_entities)
        {
            List<TMainProjectionEntity> unsavedEntities = new List<TMainProjectionEntity>();

            for (int i = 0; i < timesToDuplicate; i++)
            {
                foreach (var selectedEntity in selected_entities)
                {
                    var newProjection = new TMainProjectionEntity();
                    DataUtils.ShallowCopy(newProjection, selectedEntity);
                    newProjection.GUID = Guid.Empty;
                    newProjection.EntityNumber = StringFormatUtils.GetNewRegisterNumber(MainViewModel.Entities, unsavedEntities, MainViewModel.SelectedEntities, selectedEntity.EntityGroup);
                    newProjectionPropertyOverride(newProjection);

                    //handled in bulk save
                    //MainViewModel.EntitiesUndoRedoManager.AddUndo(newProjection, null, null, null, EntityMessageType.Added);
                    unsavedEntities.Add(newProjection);
                }
            }

            return unsavedEntities;
        }

        /// <summary>
        /// set default property after shallow copy from selected property
        /// </summary>
        protected virtual void newProjectionPropertyOverride(TMainProjectionEntity projection)
        {

        }

        /// <summary>
        /// Reorder number when auto number group is deleted
        /// </summary>
        private void OnAfterProjectionDeletedCallBack(IEnumerable<TMainProjectionEntity> entities)
        {
            if (AllowReorderingOnDeletion())
            {
                reorderEntities(entities);
            }
        }

        public bool CanAutoOrder()
        {
            return SelectedEntities != null && SelectedEntities.Count > 0;
        }

        public void AutoOrder()
        {
            reorderEntities(SelectedEntities);
        }

        private void reorderEntities(IEnumerable<TMainProjectionEntity> entities)
        {
            List<TMainProjectionEntity> changedEntities = new List<TMainProjectionEntity>();
            var groupedEntities = MainViewModel.Entities.GroupBy(x => x.EntityGroup).Select(group => new { Group = group.Key, GroupedEntities = group.ToList() });
            foreach (var groupedEntity in groupedEntities)
            {
                long enumerateNumber = 1;
                foreach (TMainProjectionEntity entityInOrder in groupedEntity.GroupedEntities.OrderBy(x => x.EntitySortNumber))
                {
                    int numericFieldLength = 0;
                    long largestNumberValueOnly = 0;
                    string largestNumberStringOnly = StringFormatUtils.ParseStringIntoComponents(entityInOrder.EntityNumber, out numericFieldLength, out largestNumberValueOnly);
                    if (enumerateNumber != largestNumberValueOnly)
                    {
                        string oldNumberString = entityInOrder.EntityNumber;
                        string newNumberString = StringFormatUtils.AppendStringWithEnumerator(string.Empty, enumerateNumber, DefaultNumericFieldLength());
                        entityInOrder.EntityNumber = newNumberString;
                        MainViewModel.EntitiesUndoRedoManager.AddUndo(entityInOrder, BindableBase.GetPropertyName(() => new TMainProjectionEntity().EntityNumber), oldNumberString, newNumberString, EntityMessageType.Changed);
                        changedEntities.Add(entityInOrder);
                    }

                    enumerateNumber += 1;
                }
            }

            MainViewModel.BaseBulkSave(changedEntities);
        }

        private bool _isProcessingMultiple;
        public void Insert()
        {
            if (!_isProcessingMultiple)
                MainViewModel.EntitiesUndoRedoManager.PauseActionId();

            List<TMainProjectionEntity> newEntities = getNewInsertEntities(1);
            newEntities = concatenateNewEntitiesWithExistingRenameEntities(newEntities);
            MainViewModel.BaseBulkSave(newEntities);
            if (!_isProcessingMultiple)
                MainViewModel.EntitiesUndoRedoManager.UnpauseActionId();
        }

        public void InsertMultiple(BarEditItem barEdit)
        {
            //Handled in bulk save
            //MainViewModel.EntitiesUndoRedoManager.PauseActionId();
            _isProcessingMultiple = true;
            var timesToInsert = 0;
            List<TMainProjectionEntity> newEntities = new List<TMainProjectionEntity>();
            if (int.TryParse(barEdit.EditValue.ToString(), out timesToInsert))
            {
                List<TMainProjectionEntity> currentEnumerationSaveEntities = getNewInsertEntities(timesToInsert);
                newEntities.AddRange(currentEnumerationSaveEntities);
            }

            newEntities = concatenateNewEntitiesWithExistingRenameEntities(newEntities);

            MainViewModel.BaseBulkSave(newEntities);
            _isProcessingMultiple = false;

            //Handled in Bulk Save
            //MainViewModel.EntitiesUndoRedoManager.UnpauseActionId();
        }

        List<TMainProjectionEntity> getNewInsertEntities(int timestoInsert)
        {
            List<TMainProjectionEntity> unsavedEntities = new List<TMainProjectionEntity>();
            for (int i = 0; i < timestoInsert; i++)
            {
                foreach (var selectedEntity in MainViewModel.SelectedEntities)
                {
                    var newProjection = new TMainProjectionEntity();
                    DataUtils.ShallowCopy(newProjection, selectedEntity);
                    newProjection.GUID = Guid.Empty;
                    newProjection.EntityNumber = StringFormatUtils.GetNewRegisterNumber(MainViewModel.Entities, unsavedEntities, MainViewModel.SelectedEntities, selectedEntity.EntityGroup, selectedEntity.EntityNumber);
                    newProjectionPropertyOverride(newProjection);

                    //Handled in Bulk Save
                    //MainViewModel.EntitiesUndoRedoManager.AddUndo(newProjection, null, null, null, EntityMessageType.Added);
                    unsavedEntities.Add(newProjection);
                }
            }

            return unsavedEntities;
        }

        /// <summary>
        /// Concatenate entities to be saved and entities to be renamed.
        /// </summary>
        /// <param name="newEntities">Entities to be saved.</param>
        /// <returns></returns>
        private List<TMainProjectionEntity> concatenateNewEntitiesWithExistingRenameEntities(List<TMainProjectionEntity> newEntities)
        {
            List<TMainProjectionEntity> concatenatedEntities = new List<TMainProjectionEntity>();
            concatenatedEntities.AddRange(newEntities);

            List<string> processedValueToFillStringOnly = new List<string>();
            foreach (TMainProjectionEntity entity in newEntities.OrderBy(x => x.EntitySortNumber))
            {
                long lowestUnsavedNumericValue = 0;
                long highestUnsavedNumericValue = 0;

                int numericFieldLength = 0;
                long arbitraryNumericValue = 0;
                string valueToFill = entity.EntityNumber;
                if (valueToFill == string.Empty)
                    return concatenatedEntities;

                string valueToFillStringOnly = StringFormatUtils.ParseStringIntoComponents(valueToFill, out numericFieldLength, out arbitraryNumericValue);

                List<TMainProjectionEntity> relatedNewEntities = newEntities.ToList();
                TMainProjectionEntity smallestNumberEntity = relatedNewEntities.First();
                TMainProjectionEntity largestNumberEntity = relatedNewEntities.Last();

                string smallestInternalNum = smallestNumberEntity.EntityNumber;
                string largestInternalNum = largestNumberEntity.EntityNumber;

                valueToFillStringOnly = StringFormatUtils.ParseStringIntoComponents(smallestInternalNum, out numericFieldLength, out lowestUnsavedNumericValue);
                valueToFillStringOnly = StringFormatUtils.ParseStringIntoComponents(largestInternalNum, out numericFieldLength, out highestUnsavedNumericValue);
                if (!processedValueToFillStringOnly.Contains(valueToFillStringOnly))
                {
                    processedValueToFillStringOnly.Add(valueToFillStringOnly);
                    List<TMainProjectionEntity> renameEntities = getRenameExistingEntities(valueToFillStringOnly, lowestUnsavedNumericValue, highestUnsavedNumericValue, entity.EntityGroup);
                    concatenatedEntities.AddRange(renameEntities);
                }
            }

            return concatenatedEntities;
        }

        /// <summary>
        /// Identify entities which internal number require to be named.
        /// </summary>
        /// <param name="renameStringOnly">Rename internal number string component only.</param>
        /// <param name="startNumber">Start of internal number to be named</param>
        /// <param name="endNumber">End if internal number to be named</param>
        /// <returns></returns>
        private List<TMainProjectionEntity> getRenameExistingEntities(string renameStringOnly, long startNumber, long endNumber, string entityGroup)
        {
            long valueToAdd = (endNumber - startNumber) + 1;
            List<TMainProjectionEntity> renameEntities = new List<TMainProjectionEntity>();

            long loopNumber = startNumber;
            foreach (TMainProjectionEntity entity in MainViewModel.Entities.Where(x => x.EntityGroup == entityGroup).OrderBy(x => x.EntitySortNumber))
            {
                string stringValueToFill = entity.EntityNumber;
                if (stringValueToFill == null)
                    continue;

                int numericFieldLength = 0;
                long valueToFillNumberOnly = 0;
                string valueToFillStringOnly = StringFormatUtils.ParseStringIntoComponents(stringValueToFill, out numericFieldLength, out valueToFillNumberOnly);

                if (valueToFillNumberOnly == loopNumber)
                {
                    long increasedNumber = valueToFillNumberOnly + valueToAdd;
                    string oldInternalNum = entity.EntityNumber;
                    entity.EntityNumber = StringFormatUtils.AppendStringWithEnumerator(valueToFillStringOnly, increasedNumber, numericFieldLength);
                    MainViewModel.EntitiesUndoRedoManager.AddUndo(entity, BindableBase.GetPropertyName(() => new TMainProjectionEntity().EntityNumber), oldInternalNum, entity.EntityNumber, EntityMessageType.Changed);
                    renameEntities.Add(entity);
                    loopNumber += 1;
                }
            }

            return renameEntities;
        }
        #endregion
    }
}
