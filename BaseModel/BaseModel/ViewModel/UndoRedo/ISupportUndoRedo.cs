using DevExpress.Xpf.Grid;

namespace BaseModel.ViewModel.UndoRedo
{
    public interface ISupportUndoRedo<TEntity>
        where TEntity : class
    {
        /// <summary>
        /// The EntitiesUndoRedoManager
        /// </summary>
        EntitiesUndoRedoManager<TEntity> EntitiesUndoRedoManager { get; }

        /// <summary>
        /// Callback for undoing entity changes
        /// </summary>
        /// <param name="entityProperty">Undoing action, entity and/or specific property</param>
        void PropertyUndo(UndoRedoEntityInfo<TEntity> entityProperty);

        /// <summary>
        /// Callback for undoing entity changes
        /// </summary>
        /// <param name="entityProperty">Undoing action, entity and/or specific property</param>
        void PropertyRedo(UndoRedoEntityInfo<TEntity> entityProperty);

        /// <summary>
        /// Add existing row property changes to EntitiesUndoRedoManager and save changes to database
        /// </summary>
        /// <param name="e">CellValueChanged event</param>
        void ExistingRowAddUndoAndSave(CellValueChangedEventArgs e);

        /// <summary>
        /// Add new row property changes to EntitiesUndoRedoManager and save changes to database
        /// </summary>
        /// <param name="e">CellValueChRowUpdatedanged event</param>
        void NewRowAddUndoAndSave(RowEventArgs e);

        /// <summary>
        /// Used with POCO view model to expose method as UndoCommand
        /// </summary>
        void Undo();

        /// <summary>
        /// Used with POCO view model to expose method as RedoCommand
        /// </summary>
        void Redo();

        /// <summary>
        /// Used with POCO view model to expose can execute for UndoCommand
        /// </summary>
        bool CanUndo();

        /// <summary>
        /// Used with POCO view model to expose can execute for RedoCommand
        /// </summary>
        bool CanRedo();
    }
}