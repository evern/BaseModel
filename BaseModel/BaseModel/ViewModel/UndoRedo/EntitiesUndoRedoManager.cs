using BaseModel.Data.Helpers;
using BaseModel.Misc;
using BaseModel.View;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BaseModel.ViewModel.UndoRedo
{
    public class EntitiesUndoRedoManager<TEntity>
        where TEntity : class
    {
        readonly Action<UndoRedoEntityInfo<TEntity>> FuncUndo;
        readonly Action<UndoRedoEntityInfo<TEntity>> FuncRedo;
        readonly Action<IEnumerable<UndoRedoEntityInfo<TEntity>>> BulkFuncUndo;
        readonly Action<IEnumerable<UndoRedoEntityInfo<TEntity>>> BulkFuncRedo;
        public List<string> ExceptionFieldNames = new List<string>();
        public EntitiesUndoRedoManager(Action<UndoRedoEntityInfo<TEntity>> funcUndo,
            Action<UndoRedoEntityInfo<TEntity>> funcRedo)
        {
            FuncUndo = funcUndo;
            FuncRedo = funcRedo;
        }

        public EntitiesUndoRedoManager(Action<IEnumerable<UndoRedoEntityInfo<TEntity>>> bulkFuncUndo, Action<IEnumerable<UndoRedoEntityInfo<TEntity>>> bulkFuncRedo)
        {
            BulkFuncUndo = bulkFuncUndo;
            BulkFuncRedo = bulkFuncRedo;
        }

        #region Public Methods

        public void Clear()
        {
            UndoList.Clear();
            RedoList.Clear();
        }

        /// <summary>
        /// Specify whether any undo operation can be done
        /// </summary>
        /// <returns></returns>
        public bool CanUndo()
        {
            return UndoList.Count > 0;
        }

        /// <summary>
        /// Specify whether any redo operation can be done
        /// </summary>
        /// <returns></returns>
        public bool CanRedo()
        {
            return RedoList.Count > 0;
        }

        /// <summary>
        /// Adds a property from the view to the undo list
        /// </summary>
        /// <param name="entityHashCode">Hash code of the property instance</param>
        /// <param name="propertyName">Name of the property</param>
        /// <param name="oldValue">Old value of the property</param>
        /// <param name="newValue">New value of the property</param>
        /// <param name="actionId">Undo/Redo action id</param>
        /// <param name="messageType">Action to take when undoing/redoing</param>
        public void AddUndo(TEntity changedEntity, string propertyName, object oldValue, object newValue,
            EntityMessageType messageType, bool force = false)
        {
            //view will invoke add undo, put a check to make sure that it's not redoing before adding
            if(!_isUndoing && !_isRedoing)
            {
                if (propertyName == null)
                    return;

                if(!ExceptionFieldNames.Any(x => x == propertyName))
                {
                    //when type is enumerable (e.g. tokens), add the entire object
                    if (force || oldValue != null && DataUtils.GetEnumerableType(oldValue.GetType()) != null)
                    {
                        UndoList.Add(new UndoRedoEntityInfo<TEntity>(changedEntity, propertyName, oldValue, newValue, ActionId, messageType));
                    }
                    else
                    { 
                        if (oldValue == null && newValue == null && messageType != EntityMessageType.Added)
                            return;

                        if ((oldValue != null && newValue != null) && oldValue.ToString() == newValue.ToString())
                            return;

                        //sometimes undo gets invoked multiple times by view events, check that it doesn't exists already before adding
                        IEnumerable<UndoRedoEntityInfo<TEntity>> similarUndoRedoProperties = UndoList.Where(x => (x.ActionId == QueryActionId - 1 || x.ActionId == QueryActionId) && x.ChangedEntity == changedEntity && x.PropertyName == propertyName && x.MessageType == messageType);
                        UndoRedoEntityInfo<TEntity> similarUndoRedoProperty = null;
                        if (newValue == null)
                            similarUndoRedoProperty = similarUndoRedoProperties.Where(x => x.OldValue != null).FirstOrDefault(x => x.OldValue.ToString() == oldValue.ToString() && x.NewValue == null);
                        else if (oldValue != null)
                            similarUndoRedoProperty = similarUndoRedoProperties.Where(x => x.OldValue != null && x.NewValue != null).FirstOrDefault(x => x.OldValue.ToString() == oldValue.ToString() && x.NewValue.ToString() == newValue.ToString());

                        if (similarUndoRedoProperty != null)
                            return;

                        UndoList.Add(new UndoRedoEntityInfo<TEntity>(changedEntity, propertyName, oldValue, newValue, ActionId, messageType));
                    }

                    RedoList.Clear();
                }
            }
        }

        public void AddRedo(TEntity changedEntity, string propertyName, object oldValue, object newValue,
            EntityMessageType messageType)
        {
            RedoList.Add(new UndoRedoEntityInfo<TEntity>(changedEntity, propertyName, oldValue, newValue, ActionId, messageType));
        }

        /// <summary>
        /// Undo the last entity change
        /// </summary>
        public void Undo()
        {
            _isUndoing = true;
            List<UndoRedoEntityInfo<TEntity>> bulkUndoList = new List<UndoRedoEntityInfo<TEntity>>();
            if (UndoList.Count > 0)
            {
                // Extract the item from the undo list.
                var undoActionId = UndoList.Last().ActionId;
                UndoRedoEntityInfo<TEntity> item;

                int undoActionCount = UndoList.Where(x => x.ActionId == undoActionId).Count();
                for (var i = UndoList.Count - 1; i >= 0; i--)
                {
                    item = UndoList.Last();
                    if (item.ActionId == undoActionId)
                    {
                        UndoList.RemoveAt(UndoList.Count - 1);
                        var copyRedoList = RedoList.ToList();
                        copyRedoList.Add(item);
                        // We need to copy the undo list here.
                        var copyUndoList = UndoList.ToList();
                        if (BulkFuncUndo == null)
                            FuncUndo(item);
                        else
                            bulkUndoList.Add(item);
                        // Now repopulate the undo and redo lists.
                        UpdateRedoList(copyRedoList);
                        UndoList.Clear();
                        UndoList.AddRange(copyUndoList);
                    }
                }

                SetActionId(_ActionId - 1);
            }

            BulkFuncUndo?.Invoke(bulkUndoList);
            _isUndoing = false;
        }

        /// <summary>
        /// Redo the last undone entity change
        /// </summary>
        /// <remarks>
        /// Unlike the undo operation, we don't need to copy the undo list out
        /// because we want the item we're redoing being added back to the redo
        /// list.
        /// </remarks>
        public void Redo()
        {
            _isRedoing = true;

            List<UndoRedoEntityInfo<TEntity>> bulkRedoList = new List<UndoRedoEntityInfo<TEntity>>();
            if (RedoList.Count > 0)
            {
                // Extract the item from the redo list.
                var redoActionId = RedoList.Last().ActionId;
                UndoRedoEntityInfo<TEntity> item;

                int redoActionCount = UndoList.Where(x => x.ActionId == redoActionId).Count();
                for (var i = RedoList.Count - 1; i >= 0; i--)
                {
                    item = RedoList.Last();
                    if (item.ActionId == redoActionId)
                    {
                        // Now, remove it from the list.
                        RedoList.RemoveAt(RedoList.Count - 1);
                        // Here we need to copy the redo list out because
                        // we will clear the list when the Add is called and
                        // the Redo is cleared there.
                        var redoList = RedoList.ToList();
                        //Redo actionId should be the same as undo action id
                        SetActionId(item.ActionId);

                        if(BulkFuncRedo == null)
                            // Redo the last operation.
                            FuncRedo(item);
                        else
                        {
                            bulkRedoList.Add(item);
                        }

                        // Add the last redo item into undo list, add it directly because AddUndo checks for whether it is redoing
                        UndoList.Add(new UndoRedoEntityInfo<TEntity>(item.ChangedEntity, item.PropertyName, item.OldValue, item.NewValue, ActionId, item.MessageType));

                        // Now reset the redo list.
                        UpdateRedoList(redoList);
                    }
                }

                BulkFuncRedo?.Invoke(bulkRedoList);
            }

            _isRedoing = false;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Refreshes the redo list with new entries
        /// </summary>
        /// <param name="redoList"></param>
        private void UpdateRedoList(List<UndoRedoEntityInfo<TEntity>> redoList)
        {
            RedoList.Clear();
            RedoList.AddRange(redoList);
        }

        private List<UndoRedoEntityInfo<TEntity>> _undoList;

        /// <summary>
        /// Get the undo list.
        /// </summary>
        private List<UndoRedoEntityInfo<TEntity>> UndoList
        {
            get
            {
                if (_undoList == null)
                    _undoList = new List<UndoRedoEntityInfo<TEntity>>();
                return _undoList;
            }
            set { _undoList = value; }
        }

        private List<UndoRedoEntityInfo<TEntity>> _redoList;

        /// <summary>
        /// Get the redo list.
        /// </summary>
        private List<UndoRedoEntityInfo<TEntity>> RedoList
        {
            get
            {
                if (_redoList == null)
                    _redoList = new List<UndoRedoEntityInfo<TEntity>>();
                return _redoList;
            }
            set { _redoList = value; }
        }

        #endregion

        #region Action Id

        //allows undo/redo operation to be tagged with incremental action id
        private int _ActionId = 0;
        //allows action id to be paused from increment for bulk operation
        private bool _PauseActionId;
        //describes undo redo manager undo state
        private bool _isUndoing;
        //describes undo redo manager redo state
        private bool _isRedoing;

        /// <summary>
        /// Allows action id to be incremented everytime is is retrieved
        /// </summary>
        private int ActionId
        {
            get
            {
                if (IsInUndoRedoOperation)
                {
                    return _ActionId;
                }
                else
                {
                    if (!_PauseActionId)
                        _ActionId += 1;

                    return _ActionId;
                }
            }
        }

        private int QueryActionId => _ActionId;

        /// <summary>
        /// Set action id manually by undo/redo operation 
        /// </summary>
        /// <param name="actionId">Action Id to set</param>
        private void SetActionId(int actionId)
        {
            _ActionId = actionId;
        }

        /// <summary>
        /// Pause action id to record multiple property changes in one action
        /// </summary>
        public void PauseActionId()
        {
            if (IsInUndoRedoOperation)
                return;

            if (!_PauseActionId) //sometimes pause can be called multiple times
                _ActionId += 1;

            _PauseActionId = true;
        }

        public void RewindActionId(int rewindValue)
        {
            _ActionId -= rewindValue;
        }

        /// <summary>
        /// Unpause action id to allow increment per property changes
        /// </summary>
        public void UnpauseActionId()
        {
            _PauseActionId = false;
        }

        public bool IsPaused()
        {
            return _PauseActionId;
        }

        public bool IsInUndoRedoOperation =>  _isUndoing || _isRedoing;
        #endregion
    }
}