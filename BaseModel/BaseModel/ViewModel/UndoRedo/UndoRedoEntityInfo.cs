using BaseModel.Misc;
using System;
using System.Collections.Generic;

namespace BaseModel.ViewModel.UndoRedo
{
    public class UndoRedoEntityInfo<TEntity>
    {
        /// <summary>
        /// Stores undo/redo property information
        /// </summary>
        /// <param name="entityHashCode">Hash code of the property instance</param>
        /// <param name="propertyName">Name of the property</param>
        /// <param name="oldValue">Old value of the property</param>
        /// <param name="newValue">New value of the property</param>
        /// <param name="actionId">Undo/Redo action id</param>
        /// <param name="messageType">Action to take when undoing/redoing</param>
        public UndoRedoEntityInfo(TEntity changedEntity, string propertyName, object oldValue, object newValue,
            int actionId, EntityMessageType messageType)
        {
            ChangedEntity = changedEntity;
            PropertyName = propertyName;
            OldValue = oldValue;
            NewValue = newValue;
            ActionId = actionId;
            MessageType = messageType;
        }

        //Hash code of the property's entity
        public TEntity ChangedEntity { get; set; }
        //Reflection property name
        public string PropertyName { get; set; }
        //Old value of the property
        public object OldValue { get; set; }
        //New value of the property
        public object NewValue { get; set; }
        //Action Id tagged by undo manager
        public int ActionId { get; set; }
        //Action to take when undoing/redoing
        public EntityMessageType MessageType { get; set; }
    }
}