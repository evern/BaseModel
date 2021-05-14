using BaseModel.DataModel;
using BaseModel.Helpers;
using BaseModel.Utils;
using BaseModel.ViewModel.UndoRedo;
using BaseModel.Data.Helpers;
using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Editors.Settings;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Grid.TreeList;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using BaseModel.Misc;
using BaseModel.ViewModel.Dialogs;
using BaseModel.ViewModel.Services;
using DevExpress.Xpf.Grid.LookUp;
using System.Windows.Media;
using System.Windows.Input;
using DevExpress.Xpf.Editors.Filtering;
using System.Windows.Forms;
using System.ComponentModel;
using DevExpress.Xpf.Utils;

namespace BaseModel.ViewModel.Base
{
    /// <summary>
    /// The base class for a POCO view models exposing a colection of entities of a given type and CRUD operations against these entities.
    /// This is a partial class that provides extension point to add custom properties, commands and override methods without modifying the auto-generated code.
    /// All extensions from DevExpress will be implemented here
    /// </summary>
    /// <typeparam name="TEntity">An entity type.</typeparam>
    /// <typeparam name="TProjection">A projection entity type.</typeparam>
    /// <typeparam name="TPrimaryKey">A primary key value type.</typeparam>
    /// <typeparam name="TUnitOfWork">A unit of work type.</typeparam>
    public partial class CollectionViewModel<TEntity, TProjection, TPrimaryKey, TUnitOfWork> :
        CollectionViewModelBase<TEntity, TProjection, TPrimaryKey, TUnitOfWork>, ISupportUndoRedo<TProjection>,
        ICollectionViewModel<TProjection>
        where TEntity : class, new()
        where TProjection : class, new()
        where TUnitOfWork : IUnitOfWork
    {
        #region Call Backs
        public bool DisableEntitiesPauseUnpause;

        /// <summary>
        /// Disable immediate posting for checkboxes
        /// </summary>
        public bool DisableImmediatePosting;

        /// <summary>
        /// Before invoking save method from event
        /// </summary>
        public Func<CellValueChangedEventArgs, TProjection, bool> OnBeforeRowSaveIsContinue { get; set; }

        /// <summary>
        /// Additional validation for row
        /// </summary>
        public Func<TProjection, string> UnifiedValidateRow { get; set; }

        /// <summary>
        /// External call back used by copy paste, fill, new and existing row cell value changing to determine which other cells to affect
        /// </summary>
        public Action<string, object, object, TProjection, bool> UnifiedValueChangingCallback;

        /// <summary>
        /// External call back used by copy paste, fill, new and existing row cell value changed to determine which other cells to affect, or to commit to database different from MainViewModel's datacontext
        /// </summary>
        public Action<string, object, object, TProjection, bool> UnifiedValueChangedCallback;

        /// <summary>
        /// External call back used by copy paste, fill, new and existing row cell value changing to determine whether value is valid
        /// </summary>
        public Func<TProjection, string, object, bool, string> UnifiedValueValidationCallback;

        /// <summary>
        /// used to indicate whether cell value is changing to perform validation, as validation needs to be differentiated between cell value changes and general grid control validation
        /// </summary>
        public string CellValueChangingFieldName { get; private set; }

        /// <summary>
        /// Indicate whether value is changing from events i.e. paste and fill
        /// </summary>
        public bool IsChangingValueFromBackgroundEvents { get; set; }
        #endregion
        /// <summary>
        /// Initializes a new instance of the CollectionViewModel class.
        /// This constructor is declared protected to avoid an undesired instantiation of the CollectionViewModel type without the POCO proxy factory.
        /// </summary>
        /// <param name="unitOfWorkFactory">A factory used to create a unit of work instance.</param>
        /// <param name="getRepositoryFunc">A function that returns a repository representing entities of the given type.</param>
        protected CollectionViewModel(IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory, Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc, Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> projection)
            : base(unitOfWorkFactory, getRepositoryFunc, projection)
        {
            InitZoom();
        }

        #region Interceptors
        protected override void AddUndoBeforeEntityAdded(TProjection projection)
        {
            if(!IsPersistentView)
                EntitiesUndoRedoManager.AddUndo(projection, null, null, null, EntityMessageType.Added);

            base.AddUndoBeforeEntityAdded(projection);
        }

        protected override void AddUndoBeforeEntityDeleted(TProjection projection)
        {
            EntitiesUndoRedoManager.AddUndo(projection, null, null, null, EntityMessageType.Deleted);
            base.AddUndoBeforeEntityDeleted(projection);
        }

        protected override void PauseEntitiesUndoRedoManager()
        {
            if(!DisableEntitiesPauseUnpause)
            {
                EntitiesUndoRedoManager.PauseActionId();
                base.PauseEntitiesUndoRedoManager();
            }
        }

        protected override void UnpauseEntitiesUndoRedoManager()
        {
            if (!DisableEntitiesPauseUnpause)
            {
                EntitiesUndoRedoManager.UnpauseActionId();
                base.UnpauseEntitiesUndoRedoManager();
            }
        }

        protected override bool IsInUndoRedoOperation()
        {
            return EntitiesUndoRedoManager.IsInUndoRedoOperation;
        }
        #endregion

        /// <summary>
        /// Creates a new instance of CollectionViewModel as a POCO view model.
        /// </summary>
        /// <param name="unitOfWorkFactory">A factory used to create a unit of work instance.</param>
        /// <param name="getRepositoryFunc">A function that returns a repository representing entities of the given type.</param>
        /// <param name="projection">An optional parameter that provides a LINQ function used to customize a query for entities. The parameter, for example, can be used for sorting data.</param>
        /// <param name="newEntityInitializer">An optional parameter that provides a function to initialize a new entity. This parameter is used in the detail collection view models when creating a single object view model for a new entity.</param>
        /// <param name="canCreateNewEntity">A function that is called before an attempt to create a new entity is made. This parameter is used together with the newEntityInitializer parameter.</param>
        /// <param name="ignoreSelectEntityMessage">An optional parameter that used to specify that the selected entity should not be managed by PeekCollectionViewModel.</param>
        public static CollectionViewModel<TEntity, TProjection, TPrimaryKey, TUnitOfWork> CreateCollectionViewModel(
            IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory,
            Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc,
            Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> projection)
        {
            return
                ViewModelSource.Create(() => new CollectionViewModel<TEntity, TProjection, TPrimaryKey, TUnitOfWork>(unitOfWorkFactory, getRepositoryFunc, projection));
        }

        #region ISupportUndoRedo

        /// <summary>
        /// Manages all undo and redo operation
        /// </summary>
        private EntitiesUndoRedoManager<TProjection> entitiesundoredomanager { get; set; }

        public EntitiesUndoRedoManager<TProjection> EntitiesUndoRedoManager
        {
            get
            {
                if (entitiesundoredomanager == null)
                    entitiesundoredomanager = new EntitiesUndoRedoManager<TProjection>(BulkPropertyUndo, BulkPropertyRedo);

                return entitiesundoredomanager;
            }
        }

        /// <summary>
        /// Function to undo the entity changes
        /// Must be used in conjunction of EntitiesUndoManager
        /// </summary>
        /// <param name="entityProperty">Entity passed over from EntitiesUndoRedo</param>
        public virtual void PropertyUndo(UndoRedoEntityInfo<TProjection> entityProperty)
        {
            if (entityProperty.MessageType == EntityMessageType.Added)
                Delete(entityProperty.ChangedEntity);
            else
            {
                if (entityProperty.MessageType == EntityMessageType.Changed)
                    DataUtils.SetNestedValue(entityProperty.PropertyName, entityProperty.ChangedEntity,
                        entityProperty.OldValue);

                Save(entityProperty.ChangedEntity);
            }
        }

        /// <summary>
        /// Function to undo the entity changes
        /// Must be used in conjunction of EntitiesUndoManager
        /// </summary>
        /// <param name="entityProperty">Entity passed over from EntitiesUndoRedo</param>
        public virtual void BulkPropertyUndo(IEnumerable<UndoRedoEntityInfo<TProjection>> entityProperties)
        {
            IsChangingValueFromBackgroundEvents = true;
            IEnumerable<UndoRedoEntityInfo<TProjection>> bulkSaveProperties = entityProperties.Where(x => x.MessageType == EntityMessageType.Changed);
            IEnumerable<UndoRedoEntityInfo<TProjection>> bulkDeleteProperties = entityProperties.Where(x => x.MessageType == EntityMessageType.Added);
            IEnumerable<UndoRedoEntityInfo<TProjection>> bulkAddProperties = entityProperties.Where(x => x.MessageType == EntityMessageType.Deleted);

            //change property before deleting, because if property is saved after it's deleted the entity will be restored
            foreach (UndoRedoEntityInfo<TProjection> entityProperty in bulkSaveProperties)
            {
                UnifiedValueChangingCallback?.Invoke(entityProperty.PropertyName, entityProperty.NewValue, entityProperty.OldValue, entityProperty.ChangedEntity, false);
                DataUtils.SetNestedValue(entityProperty.PropertyName, entityProperty.ChangedEntity, entityProperty.OldValue);
                UnifiedValueChangedCallback?.Invoke(entityProperty.PropertyName, entityProperty.NewValue, entityProperty.OldValue, entityProperty.ChangedEntity, false);

                ICanUpdate canUpdateEntity = entityProperty as ICanUpdate;
                canUpdateEntity?.Update();
            }

            List<UndoRedoEntityInfo<TProjection>> bulkSaveOperationEntities = new List<UndoRedoEntityInfo<TProjection>>();
            bulkSaveOperationEntities.AddRange(bulkSaveProperties);
            bulkSaveOperationEntities.AddRange(bulkAddProperties);
            if (bulkSaveOperationEntities.Count > 0)
                BaseBulkSave(bulkSaveOperationEntities.Select(x => x.ChangedEntity));

            //use ignore refresh here because it'll be refreshed in basebulksave
            if (bulkDeleteProperties.Count() > 0)
                BaseBulkDelete(bulkDeleteProperties.Select(x => x.ChangedEntity));

            IsChangingValueFromBackgroundEvents = false;
        }

        /// <summary>
        /// Function to redo the entity changes
        /// Must be used in conjunction of EntitiesUndoManager
        /// </summary>
        /// <param name="entityProperty">Entity passed over from EntitiesUndoRedo</param>
        public virtual void BulkPropertyRedo(IEnumerable<UndoRedoEntityInfo<TProjection>> entityProperties)
        {
            IsChangingValueFromBackgroundEvents = true;
            IEnumerable<UndoRedoEntityInfo<TProjection>> bulkSaveProperties = entityProperties.Where(x => x.MessageType == EntityMessageType.Changed);
            IEnumerable<UndoRedoEntityInfo<TProjection>> bulkAddProperties = entityProperties.Where(x => x.MessageType == EntityMessageType.Added);
            IEnumerable<UndoRedoEntityInfo<TProjection>> bulkDeleteProperties = entityProperties.Where(x => x.MessageType == EntityMessageType.Deleted);

            //change property before deleting, because if property is saved after it's deleted the entity will be restored
            foreach (UndoRedoEntityInfo<TProjection> entityProperty in bulkSaveProperties)
            {
                UnifiedValueChangingCallback?.Invoke(entityProperty.PropertyName, entityProperty.OldValue, entityProperty.NewValue, entityProperty.ChangedEntity, false);
                DataUtils.SetNestedValue(entityProperty.PropertyName, entityProperty.ChangedEntity, entityProperty.NewValue);
                UnifiedValueChangedCallback?.Invoke(entityProperty.PropertyName, entityProperty.OldValue, entityProperty.NewValue, entityProperty.ChangedEntity, false);

                ICanUpdate canUpdateEntity = entityProperty as ICanUpdate;
                canUpdateEntity?.Update();
            }

            List<UndoRedoEntityInfo<TProjection>> bulkSaveOperationEntities = new List<UndoRedoEntityInfo<TProjection>>();
            bulkSaveOperationEntities.AddRange(bulkSaveProperties);
            bulkSaveOperationEntities.AddRange(bulkAddProperties);

            if(bulkSaveOperationEntities.Count > 0)
                BaseBulkSave(bulkSaveOperationEntities.Select(x => x.ChangedEntity));

            if (bulkDeleteProperties.Count() > 0)
                //use ignore refresh here because it'll be refreshed in basebulksave
                BaseBulkDelete(bulkDeleteProperties.Select(x => x.ChangedEntity));

            IsChangingValueFromBackgroundEvents = false;
        }

        /// <summary>
        /// Function to redo the entity changes
        /// Must be used in conjunction of EntitiesUndoManager
        /// </summary>
        /// <param name="entityProperty">Entity passed over from EntitiesUndoRedo</param>
        public virtual void PropertyRedo(UndoRedoEntityInfo<TProjection> entityProperty)
        {
            if (entityProperty.MessageType == EntityMessageType.Deleted)
                Delete(entityProperty.ChangedEntity);
            else
            {
                if (entityProperty.MessageType == EntityMessageType.Changed)
                    DataUtils.SetNestedValue(entityProperty.PropertyName, entityProperty.ChangedEntity, entityProperty.NewValue);

                Save(entityProperty.ChangedEntity);
            }
        }

        public bool CanFullRefresh()
        {
            return !IsLoading;
        }

        public void FullRefresh()
        {
            if (!CanFullRefresh())
                return;

            this.Refresh();
        }

        /// <summary>
        /// Specify whether any elements remains in the undo list
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the CanUndoCommand property that can be used as a binding source in views.
        /// </summary>
        public bool CanUndo()
        {
            return !IsLoading && EntitiesUndoRedoManager.CanUndo();
        }

        /// <summary>
        /// Specify whether any elements remains in the undo list
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the CanRedoCommand property that can be used as a binding source in views.
        /// </summary>
        public bool CanRedo()
        {
            return !IsLoading && EntitiesUndoRedoManager.CanRedo();
        }

        /// <summary>
        /// Undo last operation
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the UndoCommand property that can be used as a binding source in views.
        /// </summary>
        public void Undo()
        {
            if (!CanUndo())
                return;

            EntitiesUndoRedoManager.Undo();
        }

        /// <summary>
        /// Redo last operation
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the RedoCommand property that can be used as a binding source in views.
        /// </summary>
        public void Redo()
        {
            if (!CanRedo())
                return;

            EntitiesUndoRedoManager.Redo();
        }

        public void PreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                TableView tableView = e.Source as TableView;
                if(tableView != null)
                {
                    if (tableView.FocusedRowHandle == GridControl.NewItemRowHandle)
                    {
                        TextEdit textEdit = tableView.ActiveEditor as TextEdit;
                        if(textEdit == null || textEdit.AcceptsReturn == false)
                            tableView.CommitEditing();
                    }
                    else
                        tableView.PostEditor();
                }
            }
        }
        #endregion

        #region Views
        public Func<EditorEventArgs, bool> BeforeShownEditor;

        #region Grid Zooming
        private double zoom_step => Double.Parse(CommonResources.Default_ZoomStep);

        public ScaleTransform Zoom { get; private set; }
        private void InitZoom()
        {
            Zoom = new ScaleTransform();
        }

        public void Grid_MouseWheel(MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers | ModifierKeys.Control) == Keyboard.Modifiers)
            {
                if (e.Delta > 0 && Zoom.ScaleX < 3)
                {
                    Zoom.ScaleX += zoom_step;
                    Zoom.ScaleY += zoom_step;
                }
                else if (e.Delta < 0 && Zoom.ScaleX > 0.7)
                {
                    Zoom.ScaleX -= zoom_step;
                    Zoom.ScaleY -= zoom_step;
                }
            }
        }
        #endregion

        public virtual void ShownEditor(EditorEventArgs e)
        {
            if (BeforeShownEditor != null)
                if (!BeforeShownEditor(e))
                    return;

            var view = e.Source as TableView;
            if (view == null)
                return;

            var textEditor = view.ActiveEditor as TextEdit;
            if (textEditor == null)
                return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                textEditor.SelectionStart = textEditor.Text.Length;
                textEditor.SelectionLength = 0;
            }), DispatcherPriority.Background);
        }

        public void ShowPopUp(object sender)
        {
            var editor = sender as GridControl;
            if (editor == null)
                return;

            var comboBoxEditor = editor.View.ActiveEditor as ComboBoxEdit;
            if (comboBoxEditor == null)
                return;
            if (comboBoxEditor.IsPopupOpen)
                return;
            else
                comboBoxEditor.ShowPopup();
        }

        /// <summary>
        /// Remembers an entity added for undoing
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the AddUndoCommand property that can be used as a binding source in views.
        /// </summary>
        public virtual void NewRowSave(RowEventArgs e)
        {
            if (e.RowHandle == DataControlBase.NewItemRowHandle)
            {
                var projection = (TProjection)e.Row;

                ICanUpdate updateProjection = projection as ICanUpdate;
                if (updateProjection != null)
                    updateProjection.NewEntityFromView = true;

                if (OnBeforeNewRowSavedIsContinueFromViewCallBack != null)
                    if (!OnBeforeNewRowSavedIsContinueFromViewCallBack(e, projection))
                        return;

                List<TProjection> newlyAddedProjections = new List<TProjection>();
                newlyAddedProjections.Add(projection);

                Save(projection);

                //handled in save operation
                //EntitiesUndoRedoManager.AddUndo(projection, null, null, null, EntityMessageType.Added);
                //EntitiesUndoRedoManager.UnpauseActionId();
            }
        }

        /// <summary>
        /// Influence column(s) when changes happens in other column
        /// </summary>
        public virtual void CellValueChanging(CellValueChangedEventArgs e)
        {
            if (e.RowHandle == GridControl.AutoFilterRowHandle)
                return;

            if (!e.Handled)
            {
                CellValueChangingFieldName = e.Column.FieldName;
                var projection = (TProjection)e.Row;
                UnifiedValueChangingCallback?.Invoke(e.Column.FieldName, e.OldValue, e.Value, projection, e.RowHandle == GridControl.NewItemRowHandle);

                if (!DisableImmediatePosting && e.RowHandle != GridControl.NewItemRowHandle)
                    CellValueChangedImmediatePost(e);
            }
        }

        protected virtual void CellValueChangedImmediatePost(CellValueChangedEventArgs e)
        {
            TableView tableView = e.Source as TableView;
            //only post editor if editing row is not new row or else new row will be committed immediately
            if (tableView != null && e.RowHandle != GridControl.NewItemRowHandle)
            {
                if (tableView.ActiveEditor != null)
                {
                    Type activeEditorType = tableView.ActiveEditor.GetType();
                    if (activeEditorType == typeof(ComboBoxEdit) || activeEditorType == typeof(CheckEdit))
                    {
                        previousImmediatePostFieldname = string.Empty;
                        previousImmediatePostOldValue = null;
                        previousImmediatePostNewValue = null;
                        //will be unpaused in CellValueChanged
                        PauseEntitiesUndoRedoManager();
                        tableView.PostEditor();
                    }
                }
            }
        }

        private string previousImmediatePostFieldname = string.Empty;
        private object previousImmediatePostOldValue = null;
        private object previousImmediatePostNewValue = null;

        /// <summary>
        /// Influence column(s) when changes happens in other column or to commit to database different from MainViewModel's datacontext
        /// </summary>
        public virtual void CellValueChanged(CellValueChangedEventArgs e)
        {
            if (e.RowHandle == GridControl.AutoFilterRowHandle)
                return;

            TableView tableView = e.Source as TableView;
            Type activeEditorType = tableView.ActiveEditor.GetType();
            if (activeEditorType == typeof(ComboBoxEdit) || activeEditorType == typeof(CheckEdit))
            {
                if (e.Column.FieldName == previousImmediatePostFieldname)
                {
                    //because immediate post already committed the changes, and this gets called when user exits the editor
                    if (e.Value == null && e.OldValue == null)
                        return;

                    if ((e.Value != null && e.OldValue != null) && e.Value.ToString() == e.OldValue.ToString())
                        return;

                    if (previousImmediatePostOldValue != null)
                    {
                        if (e.Value != null && e.Value.ToString() == previousImmediatePostNewValue.ToString())
                        {
                            if (e.OldValue != null && e.OldValue.ToString() == previousImmediatePostOldValue.ToString())
                            {
                                previousImmediatePostOldValue = null;
                                previousImmediatePostNewValue = null;
                                return;
                            }
                        }
                    }
                    else
                    {
                        if (e.Value == null || e.OldValue == null)
                        {
                            previousImmediatePostNewValue = null;
                            return;
                        }

                        if (previousImmediatePostNewValue != null && e.Value.ToString() == previousImmediatePostNewValue.ToString())
                        {
                            previousImmediatePostNewValue = null;
                            return;
                        }
                    }
                }

                previousImmediatePostFieldname = e.Column.FieldName;
                previousImmediatePostOldValue = e.OldValue;
                previousImmediatePostNewValue = e.Value;
            }

            if (!e.Handled)
            {
                CellValueChangingFieldName = null;
                var projection = (TProjection)e.Row;
                PauseEntitiesUndoRedoManager();
                if (e.RowHandle != DataControlBase.NewItemRowHandle)
                {
                    if (OnBeforeRowSaveIsContinue != null)
                        if (!OnBeforeRowSaveIsContinue(e, projection))
                        {
                            return;
                        }

                    EntitiesUndoRedoManager.AddUndo(projection, e.Column.FieldName, e.OldValue, e.Value, EntityMessageType.Changed);
                    UnifiedValueChangedCallback?.Invoke(e.Column.FieldName, e.OldValue, e.Value, projection, e.RowHandle == GridControl.NewItemRowHandle);
                    Save(projection); //undoredomanager will be unpaused within
                }
                else
                {
                    UnifiedValueChangedCallback?.Invoke(e.Column.FieldName, e.OldValue, e.Value, projection, e.RowHandle == GridControl.NewItemRowHandle);
                    UnpauseEntitiesUndoRedoManager();
                }
            }
        }

        /// <summary>
        /// Remembers an entity property old value for undoing
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the AddUndoCommand property that can be used as a binding source in views.
        /// </summary>
        public virtual void TreelistExistingRowSave(TreeListCellValueChangedEventArgs e)
        {
            var projection = (TProjection)e.Row;
            Save(projection);
        }

        /// <summary>
        /// Validate any row within the binded datagrid
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the ValidateRowCommand property that can be used as a binding source in views.
        /// </summary>
        /// <param name="e"></param>
        public virtual void ValidateCell(GridCellValidationEventArgs e)
        {
            string constraintName = string.Empty;
            string errorMessage = string.Empty;
            if (!DataUtils.IsValidEntityCellValue(Entities, (TProjection)e.Row, e.Column.FieldName, e.Value, ref errorMessage, out constraintName))
            {
                e.IsValid = false;
                e.ErrorType = DevExpress.XtraEditors.DXErrorProvider.ErrorType.Critical;
                e.ErrorContent = errorMessage;
            }

            if(!IsLoading && UnifiedValueValidationCallback != null)
            {
                string error_message = UnifiedValueValidationCallback((TProjection)e.Row, e.Column.FieldName, e.Value, false);
                if (error_message != null && error_message != string.Empty)
                {
                    e.IsValid = false;
                    e.ErrorType = DevExpress.XtraEditors.DXErrorProvider.ErrorType.Critical;
                    e.ErrorContent = error_message;
                }
            }
        }

        public virtual void ValidateRow(GridRowValidationEventArgs e)
        {
            var errorMessage = string.Empty;
            List<KeyValuePair<string, string>> constraintIssues;
            if (!IsValidEntity((TProjection)e.Row, null, ref errorMessage, out constraintIssues))
            {
                e.IsValid = false;
                e.ErrorType = DevExpress.XtraEditors.DXErrorProvider.ErrorType.Critical;
                e.ErrorContent = errorMessage;
            }

            if(UnifiedValidateRow != null)
            {
                string error = UnifiedValidateRow.Invoke((TProjection)e.Row);
                if(error != null && error != string.Empty)
                {
                    e.IsValid = false;
                    e.ErrorType = DevExpress.XtraEditors.DXErrorProvider.ErrorType.Critical;
                    e.ErrorContent = error;
                }
            }
        }

        #endregion

        #region Cell Content Deletion
        public virtual void DeleteCellContent(GridControl gridControl)
        {
            IsChangingValueFromBackgroundEvents = true;
            string[] RowData = new string[] { string.Empty };
            CopyPasteHelper<TProjection> copyPasteHelper = new CopyPasteHelper<TProjection>(IsValidEntity, OnBeforePasteWithValidation, ErrorMessagesDialogService, UnifiedValueValidationCallback, FuncManualCellPastingIsContinue, FuncManualRowPastingIsContinue, UnifiedValueChangingCallback, UnifiedValueChangedCallback, UnifiedNewRowInitialisationFromView, FormatErrorMessagesCallBack);
            List<TProjection> pasteProjections;

            List<ErrorMessage> errorMessages = new List<ErrorMessage>();
            if(gridControl != null && gridControl.View != null)
            {
                if (gridControl.View.GetType() == typeof(TableView))
                    pasteProjections = copyPasteHelper.PastingFromClipboardCellLevel<TableView>(gridControl, RowData, EntitiesUndoRedoManager, out errorMessages);
                else
                    pasteProjections = copyPasteHelper.PastingFromClipboardTreeListCellLevel<TreeListView>(gridControl, RowData, EntitiesUndoRedoManager, out errorMessages);

                if (pasteProjections.Count > 0)
                {
                    //For copy paste don't have to refresh the entire list, just call ICanUpdate.Update() on entity
                    BaseBulkSave(pasteProjections, true);
                }

                if (errorMessages.Count > 0)
                {
                    FormatErrorMessagesCallBack?.Invoke(errorMessages);
                    if (ErrorMessagesDialogService != null)
                    {
                        DialogCollectionViewModel<ErrorMessage> viewModel = DialogCollectionViewModel<ErrorMessage>.Create(errorMessages, "The following data cannot be deleted");
                        ErrorMessagesDialogService.ShowDialog(MessageButton.OKCancel, string.Empty, "ListErrorMessages", viewModel);
                    }
                }
            }
            IsChangingValueFromBackgroundEvents = false;
        }
        #endregion

        #region Fill Down Convention

        public Func<IEnumerable<TProjection>, GridMenuInfo, bool> CanFillDownCallBack;

        public bool CanFillDown(object button)
        {
            var info = GridPopupMenuBase.GetGridMenuInfo((DependencyObject)button) as GridMenuInfo;
            return SelectedEntities != null && SelectedEntities.Count > 1 && Entities != null && Entities.Count > 1 && !IsLoading && info != null && info.Column != null && !info.Column.ReadOnly && (CanFillDownCallBack == null || CanFillDownCallBack(SelectedEntities, info));
        }

        public bool CanFillUp(object button)
        {
            var info = GridPopupMenuBase.GetGridMenuInfo((DependencyObject)button) as GridMenuInfo;
            return SelectedEntities != null && SelectedEntities.Count > 1 && Entities != null && Entities.Count > 1 && !IsLoading && info != null && info.Column != null && !info.Column.ReadOnly && (CanFillDownCallBack == null || CanFillDownCallBack(SelectedEntities, info));
        }

        public Func<TProjection, string, object, bool> ValidateFillDownCallBack;
        public Action OnFillDownCompletedCallBack;

        public void FillDown(object button)
        {
            Fill(button, false);
        }

        public void FillUp(object button)
        {
            Fill(button, true);
        }
        
        public void Fill(object button, bool isUp)
        {
            IsChangingValueFromBackgroundEvents = true;
            GridMenuInfo info = GridPopupMenuBase.GetGridMenuInfo((DependencyObject)button) as GridMenuInfo;
            object valueToFill;
            object nextValueInSequence;

            if (isUp)
            {
                valueToFill = DataUtils.GetNestedValue(info.Column.FieldName, SelectedEntities[selectedentities.Count - 1]);
                nextValueInSequence = DataUtils.GetNestedValue(info.Column.FieldName, SelectedEntities[selectedentities.Count - 2]);
            }
            else
            {
                valueToFill = DataUtils.GetNestedValue(info.Column.FieldName, SelectedEntities[0]);
                nextValueInSequence = DataUtils.GetNestedValue(info.Column.FieldName, SelectedEntities[1]);
            }

            PauseEntitiesUndoRedoManager();
            var bulkSaveEntities = new List<TProjection>();

            long? enumerationDifferences = null;
            long? enumerator = null;
            int? numericIndex = null;
            int numericFieldLength = 0;
            EnumerationType enumerationType;
            if (valueToFill != null && valueToFill.GetType() == typeof(string) && nextValueInSequence != null)
                enumerationType = DataUtils.GetEnumerateType(valueToFill.ToString(), nextValueInSequence.ToString(), out enumerationDifferences, out enumerator, out numericIndex, out numericFieldLength);
            else
                enumerationType = EnumerationType.None;

            if (!isUp)
            {
                for (int i = 1; i < SelectedEntities.Count; i++)
                {
                    if(enumerationType == EnumerationType.Increase)
                        enumerator += enumerationDifferences;
                    else
                    {
                        enumerator -= enumerationDifferences;
                        if (enumerator < 0)
                            enumerator = 0;
                    }


                    TProjection seletedEntity = SelectedEntities[i];
                    if (UnifiedValueValidationCallback != null)
                    {
                        string error_message = UnifiedValueValidationCallback.Invoke(seletedEntity, info.Column.FieldName, valueToFill, false);
                        if (error_message == string.Empty)
                        {
                            setEntityProperty(seletedEntity, info, valueToFill, numericIndex, enumerator, numericFieldLength);
                            bulkSaveEntities.Add(seletedEntity);
                        }
                        else if(MessageBoxService != null)
                            MessageBoxService.ShowMessage(error_message);
                    }
                }
            }
            else
            {
                for (int i = SelectedEntities.Count - 2; i >= 0; i--)
                {
                    if (enumerationType == EnumerationType.Increase)
                        enumerator += enumerationDifferences;
                    else
                    {
                        enumerator -= enumerationDifferences;
                        if (enumerator < 0)
                            enumerator = 0;
                    }

                    TProjection seletedEntity = SelectedEntities[i];
                    if (UnifiedValueValidationCallback != null)
                    {
                        string error_message = UnifiedValueValidationCallback.Invoke(seletedEntity, info.Column.FieldName, valueToFill, false);
                        if (error_message == string.Empty)
                        {
                            setEntityProperty(seletedEntity, info, valueToFill, numericIndex, enumerator, numericFieldLength);
                            bulkSaveEntities.Add(seletedEntity);
                        }
                    }
                }
            }

            BaseBulkSave(bulkSaveEntities, true);
            OnFillDownCompletedCallBack?.Invoke();
            UnpauseEntitiesUndoRedoManager();
            IsChangingValueFromBackgroundEvents = false;
        }
        
        private void setEntityProperty(TProjection editEntity, GridMenuInfo info, object valueToFill, int? numericIndex, long? enumerator, int numericFieldLength)
        {
            if (numericIndex != null && enumerator != null)
            {
                string valueToFillStringOnly = valueToFill.ToString().Substring(0, valueToFill.ToString().Length - numericFieldLength);

                valueToFill = StringFormatUtils.AppendStringWithEnumerator(valueToFillStringOnly, (long)enumerator, numericFieldLength);
            }

            if (ValidateFillDownCallBack != null &&
                !ValidateFillDownCallBack(editEntity, info.Column.FieldName, valueToFill))
                return;

            var OldValue = DataUtils.GetNestedValue(info.Column.FieldName, editEntity);
            UnifiedValueChangingCallback?.Invoke(info.Column.FieldName, OldValue, valueToFill, editEntity, false);
            UnifiedValueChangedCallback?.Invoke(info.Column.FieldName, OldValue, valueToFill, editEntity, false);
            EntitiesUndoRedoManager.AddUndo(editEntity, info.Column.FieldName, OldValue, valueToFill, EntityMessageType.Changed);
            DataUtils.SetNestedValue(info.Column.FieldName, editEntity, valueToFill);
        }

        public void SetNestedValueWithUndo(TProjection entity, string propertyName, object newValue)
        {
            var oldValue = DataUtils.GetNestedValue(propertyName, entity);
            DataUtils.SetNestedValue(propertyName, entity, newValue);
            EntitiesUndoRedoManager.AddUndo(entity, propertyName, oldValue, newValue, EntityMessageType.Changed);
        }

        public bool AllowEdit = true;
        public override void Edit(TProjection projectionEntity)
        {
            if (AllowEdit)
                base.Edit(projectionEntity);
            //Do not allow edit in projection view
        }

        public void Destroy()
        {
            OnDestroy();
        }

        #endregion

        #region Data Operation
        /// <summary>
        /// Determine whether other entities in the collection shares any common combination of unique constraints
        /// </summary>
        /// <param name="entity">The entity to be validated</param>
        /// <param name="errorMessage">Error message to notify the user of conflicting constraints</param>
        /// <returns>Returns true if no other entity contains similar constraint member values</returns>
        public virtual bool IsValidEntity(TProjection entity, IEnumerable<TProjection> preCommittedProjections, ref string errorMessage, out List<KeyValuePair<string, string>> constraintIssues)
        {
            //if (OnBeforeEntitySavedIsContinueCallBack != null && !OnBeforeEntitySavedIsContinueCallBack(entity))
            //    return false;

            constraintIssues = new List<KeyValuePair<string, string>>();
            if (!DataUtils.IsRequiredAttributesHasValue<TEntity, TProjection>(entity, ref errorMessage))
                return false;

            errorMessage = UnifiedValidateRow?.Invoke(entity);
            if (errorMessage != null && errorMessage != string.Empty)
                return false;

            if (DataUtils.IsUniqueEntityConstraintValues(Entities, entity, preCommittedProjections, ref errorMessage, out constraintIssues))
            {
                errorMessage = string.Empty;
                return true;
            }
            else
                return false;
        }

        #endregion
        #region Bulk Operation
        /// <summary>
        /// Determines whether an entities can be deleted
        /// Since CollectionViewModelBase is a POCO view model, this method will be used as a CanExecute callback for BulkDeleteCommand.
        /// </summary>
        /// <param name="projectionEntity">Entities to delete.</param>
        public virtual bool CanBulkDelete()
        {
            return Entities != null && Entities.Count > 0 && !IsLoading && SelectedEntities != null && SelectedEntities.Count > 0;
        }

        /// <summary>
        /// Determines whether an entities can be saved
        /// Since CollectionViewModelBase is a POCO view model, this method will be used as a CanExecute callback for BulkSaveCommand.
        /// </summary>
        /// <param name="projectionEntity">Entities to save.</param>
        public virtual bool CanBulkSave(IEnumerable<TProjection> entities)
        {
            return Entities != null && Entities.Count > 0 && !IsLoading;
        }
        /// <summary>
        /// Deletes a collection of entities from the repository.
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the DeleteCommand property that can be used as a binding source in views.
        /// </summary>
        /// <param name="projectionEntity">An entity to edit.</param>
        public virtual void BulkDelete()
        {
            if (MessageBoxService.ShowMessage("Are you sure you want to delete " + selectedentities.Count + " selected entries?", "Confirmation", MessageButton.OKCancel) == MessageResult.Cancel)
                return;

            PauseEntitiesUndoRedoManager();
            BaseBulkDelete(selectedentities);
            UnpauseEntitiesUndoRedoManager();
        }
        #endregion

        #region Bulk Edit
        protected IDialogService BulkColumnEditDialogService
        {
            get { return this.GetRequiredService<IDialogService>("BulkColumnEditService"); }
        }

        public bool CanBulkColumnEdit(object button)
        {
            var info = GridPopupMenuBase.GetGridMenuInfo((DependencyObject)button) as GridMenuInfo;
            if (info == null)
                return false;

            if (info.Column == null)
                return false;

            if (info.Column.ReadOnly)
                return false;

            if (SelectedEntities == null || SelectedEntities.Count < 2)
                return false;

            //Use first selected entity instead of SelectedEntity in ReadOnlyCollectionViewModel because it cannot be referenced from EntitiesCollectionsWrapper
            TProjection firstSelectedEntity = SelectedEntities.First();
            if (info.Column.FieldName == string.Empty)
                return false;

            var columnPropertyInfo = DataUtils.GetNestedPropertyInfo(info.Column.FieldName, firstSelectedEntity);
            if (columnPropertyInfo.PropertyType == typeof(Guid) ||
                columnPropertyInfo.PropertyType == typeof(Guid?) ||
                columnPropertyInfo.PropertyType.BaseType == typeof(Enum) ||
                columnPropertyInfo.PropertyType == typeof(decimal) ||
                columnPropertyInfo.PropertyType == typeof(decimal?) ||
                columnPropertyInfo.PropertyType == typeof(string) ||
                columnPropertyInfo.PropertyType == typeof(int) ||
                columnPropertyInfo.PropertyType == typeof(int?))
            {
                var constraintString = DataUtils.GetConstraintPropertyStrings(firstSelectedEntity.GetType());
                if (constraintString == null)
                    constraintString = DataUtils.GetConstraintPropertyStrings(firstSelectedEntity.GetType().BaseType);

                var bulkEditDisabledString =
                    DataUtils.GetBulkEditDisabledPropertyStrings(firstSelectedEntity.GetType());
                if (bulkEditDisabledString == null)
                    bulkEditDisabledString =
                        DataUtils.GetBulkEditDisabledPropertyStrings(firstSelectedEntity.GetType().BaseType);

                if (constraintString != null && constraintString.Any(x => x == columnPropertyInfo.Name) ||
                    bulkEditDisabledString != null && bulkEditDisabledString.Any(x => x == columnPropertyInfo.Name))
                    return false;
                else
                    return true;
            }

            return false;
        }

        //Denotes that edit operation comes from the background so onBeforeEntitySaved will not perform default actions
        public Func<List<KeyValuePair<ColumnBase, string>>, TProjection, bool, bool> FuncManualRowPastingIsContinue;
        public Func<TProjection, ColumnBase, string, List<UndoRedoArg<TProjection>>, bool> FuncManualCellPastingIsContinue;
        public Action<IEnumerable<string>> RawPasteOverride;
        public void BulkColumnEdit(object button)
        {
            var info = GridPopupMenuBase.GetGridMenuInfo((DependencyObject)button) as GridMenuInfo;

            object oldValue = null;
            object newValue = null;
            var SaveEntities = new List<TProjection>();
            var operation = Arithmetic.None;
            IsChangingValueFromBackgroundEvents = true;
            PauseEntitiesUndoRedoManager();
            try
            {
                bool commence_bulk_edit = false;
                TProjection firstSelectedEntity = SelectedEntities.First();
                var columnPropertyInfo = DataUtils.GetNestedPropertyInfo(info.Column.FieldName, firstSelectedEntity);
                if (columnPropertyInfo.PropertyType == typeof(Guid) || columnPropertyInfo.PropertyType == typeof(Guid?) || columnPropertyInfo.PropertyType.BaseType == typeof(Enum))
                {
                    Type editSettingsType = info.Column.ActualEditSettings.GetType();
                    object editSettings = null;
                    if (editSettingsType == typeof(ComboBoxEditSettings))
                        editSettings = info.Column.ActualEditSettings as ComboBoxEditSettings;
                    else if (editSettingsType == typeof(LookUpEditSettings))
                        editSettings = info.Column.ActualEditSettings as LookUpEditSettingsBase;

                    if (editSettings != null)
                    {
                        var copyColumnDisplayMember = (string)editSettings.GetType().GetProperty("DisplayMember").GetValue(editSettings);
                        var copyColumnItemsSource = (IEnumerable<object>)editSettings.GetType().GetProperty("ItemsSource").GetValue(editSettings);

                        var bulkEditEnumsViewModel =
                            BulkEditEnumsViewModel.Create(copyColumnItemsSource, copyColumnDisplayMember);
                        if (BulkColumnEditDialogService.ShowDialog(MessageButton.OKCancel, "Select Item to assign",
                                "BulkEditEnums", bulkEditEnumsViewModel) == MessageResult.OK)
                        {
                            commence_bulk_edit = true;
                            if (bulkEditEnumsViewModel.SelectedItem != null)
                            {
                                if (columnPropertyInfo.PropertyType.BaseType == typeof(Enum))
                                {
                                    var selectedEnum = (EnumMemberInfo)bulkEditEnumsViewModel.SelectedItem;
                                    newValue = Enum.Parse(columnPropertyInfo.PropertyType, selectedEnum.Id.ToString());
                                }
                                else
                                {
                                    IGuidEntityKey entityWithGuid = bulkEditEnumsViewModel.SelectedItem as IGuidEntityKey;
                                    if (entityWithGuid != null)
                                    {
                                        newValue = entityWithGuid.GUID;
                                    }
                                }
                            }
                        }

                        bulkEditEnumsViewModel = null;
                    }
                }
                else if (columnPropertyInfo.PropertyType == typeof(decimal) ||
                         columnPropertyInfo.PropertyType == typeof(decimal?) || columnPropertyInfo.PropertyType == typeof(int) || columnPropertyInfo.PropertyType == typeof(int?))
                {
                    var selectedEntityValue =
                        Decimal.Parse(DataUtils.GetNestedValue(info.Column.FieldName, SelectedEntities.First()).ToString());
                    var bulkEditNumbersViewModel = BulkEditNumbersViewModel.Create(selectedEntityValue);
                    if (
                        BulkColumnEditDialogService.ShowDialog(MessageButton.OKCancel,
                            "Choose number and operation to assign", "BulkEditNumbers", bulkEditNumbersViewModel) ==
                        MessageResult.OK)
                    {
                        commence_bulk_edit = true;
                        newValue = bulkEditNumbersViewModel.EditValue;
                        if (bulkEditNumbersViewModel.SelectedOperation != null)
                        {
                            var selectedArithmeticEnum = bulkEditNumbersViewModel.SelectedOperation;
                            operation = (Arithmetic)Enum.Parse(typeof(Arithmetic), selectedArithmeticEnum.Name);
                        }
                    }
                }
                else if (columnPropertyInfo.PropertyType == typeof(string))
                {
                    var selectedEntityValue =
                        (string)DataUtils.GetNestedValue(info.Column.FieldName, SelectedEntities.First());
                    var bulkEditStringsViewModel = BulkEditStringsViewModel.Create(selectedEntityValue);
                    if (BulkColumnEditDialogService.ShowDialog(MessageButton.OKCancel, "Type in text for bulk edit", "BulkEditStrings", bulkEditStringsViewModel) == MessageResult.OK)
                    {
                        commence_bulk_edit = true;
                        newValue = bulkEditStringsViewModel.EditValue;
                    }
                }

                bool isError = false;
                if(commence_bulk_edit)
                    foreach (var selectedProjection in SelectedEntities)
                    {
                        if (newValue != null && (newValue.GetType() == typeof(decimal) || newValue.GetType() == typeof(int)) && operation != Arithmetic.None)
                        {
                            var currentValue = decimal.Parse(DataUtils.GetNestedValue(info.Column.FieldName, selectedProjection).ToString());
                            var currentOldValue = currentValue;

                            if (operation == Arithmetic.Add)
                                currentValue = currentValue + (decimal)newValue;
                            else if (operation == Arithmetic.Subtract)
                                currentValue = currentValue - (decimal)newValue;
                            else if (operation == Arithmetic.Multiply)
                                currentValue = currentValue * (decimal)newValue;
                            else if (operation == Arithmetic.Divide && (decimal)newValue > 0)
                                currentValue = currentValue / (decimal)newValue;

                            if (UnifiedValueValidationCallback != null)
                            {
                                string error_message = UnifiedValueValidationCallback.Invoke(selectedProjection, info.Column.FieldName, currentValue, false);
                                if (error_message == string.Empty)
                                {
                                    DataUtils.SetNestedValue(info.Column.FieldName, selectedProjection, currentValue);

                                    UnifiedValueChangingCallback?.Invoke(info.Column.FieldName, currentValue, newValue, selectedProjection, false);
                                    UnifiedValueChangedCallback?.Invoke(info.Column.FieldName, currentValue, newValue, selectedProjection, false);
                                    EntitiesUndoRedoManager.AddUndo(selectedProjection, info.Column.FieldName, currentOldValue, currentValue, EntityMessageType.Changed);
                                }
                                else
                                    isError = true;
                            }
                            else
                                isError = true;
                        }
                        else
                        {
                            if (UnifiedValueValidationCallback != null)
                            {
                                string error_message = UnifiedValueValidationCallback.Invoke(selectedProjection, info.Column.FieldName, newValue, false);
                                if (error_message == string.Empty)
                                {
                                    oldValue = DataUtils.GetNestedValue(info.Column.FieldName, selectedProjection);
                                    if (columnPropertyInfo.PropertyType == typeof(decimal) || columnPropertyInfo.PropertyType == typeof(decimal?))
                                        newValue = decimal.Parse(newValue.ToString());
                                    else if (columnPropertyInfo.PropertyType == typeof(int) || columnPropertyInfo.PropertyType == typeof(int?))
                                        newValue = Int32.Parse(newValue.ToString());

                                    if (newValue != null)
                                    {
                                        DataUtils.SetNestedValue(info.Column.FieldName, selectedProjection, newValue);

                                        UnifiedValueChangingCallback?.Invoke(info.Column.FieldName, oldValue, newValue, selectedProjection, false);
                                        UnifiedValueChangedCallback?.Invoke(info.Column.FieldName, oldValue, newValue, selectedProjection, false);
                                        EntitiesUndoRedoManager.AddUndo(selectedProjection, info.Column.FieldName, oldValue, newValue, EntityMessageType.Changed);
                                    }
                                }
                                else
                                    isError = true;
                            }
                            else
                                isError = true;
                        }

                        if(!isError)
                            SaveEntities.Add(selectedProjection);
                    }

                BaseBulkSave(SaveEntities);
            }
            catch
            {
            }

            UnpauseEntitiesUndoRedoManager();
            IsChangingValueFromBackgroundEvents = false;
        }
        #endregion

        #region Data Operations
        public Func<TProjection, bool> OnBeforePasteWithValidation;
        public bool DisablePasting { get; set; }
        public bool DisablePasteRowLevel { get; set; }

        bool isPasteCellLevel;
        public bool IsPasteCellLevel
        {
            get => isPasteCellLevel;
            set
            {
                isPasteCellLevel = value;
                this.RaisePropertyChanged(x => x.SelectMode);
            }
        }

        public MultiSelectMode SelectMode => IsPasteCellLevel ? MultiSelectMode.Cell : MultiSelectMode.Row;

        IEnumerable<TProjection> ICollectionViewModel<TProjection>.Entities => base.Entities;

        //Indicate that paste data will not have carriage return in cells, to improve paste data accuracy
        public bool UseRegularSplitting;

        /// <summary>
        /// Converts clipboard text into entity values and saves to database
        /// </summary>
        /// <param name="e"></param>
        public virtual void PastingFromClipboard(PastingFromClipboardEventArgs e)
        {
            IsChangingValueFromBackgroundEvents = true;
            bool shouldSkip = false;
            var gridControl = (GridControl)e.Source;
            TableView tableView = gridControl.View as TableView;
            //when cell is in editing mode, user might want to paste clipboard data into cell
            if (tableView.ActiveEditor != null)
                return;

            IsPasting = true;

            if (tableView != null && tableView.FocusedRowHandle == GridControl.AutoFilterRowHandle)
            {
                shouldSkip = true;
            }

            List<int> selectedRowHandles = gridControl.GetSelectedRowHandles().ToList();
            if(!shouldSkip)
            {
                CopyPasteHelper<TProjection> copyPasteHelper = new CopyPasteHelper<TProjection>(IsValidEntity, OnBeforePasteWithValidation, ErrorMessagesDialogService, UnifiedValueValidationCallback, FuncManualCellPastingIsContinue, FuncManualRowPastingIsContinue, UnifiedValueChangingCallback, UnifiedValueChangedCallback, UnifiedNewRowInitialisationFromView, FormatErrorMessagesCallBack);

                bool dontSplit = false;
                if ((Keyboard.Modifiers | ModifierKeys.Shift) == Keyboard.Modifiers)
                    dontSplit = true;

                List<TProjection> pasteProjections;
                var PasteString = System.Windows.Clipboard.GetText();
                string[] RowData;
                if (dontSplit)
                {
                    string format_string = PasteString.Replace(@"""", "");
                    RowData = new string[] { format_string };
                }
                else
                {
                    if (UseRegularSplitting)
                    {
                        RowData = PasteString.Split('\n').ToArray();
                        RowData = RowData.Where(x => x != string.Empty).ToArray();
                    }
                    else
                        RowData = DataUtils.ExcelSplit(PasteString).ToArray();
                }

                if (RawPasteOverride != null)
                    RawPasteOverride.Invoke(RowData);
                else
                {
                    List<ErrorMessage> errorMessages = new List<ErrorMessage>();
                    if (IsPasteCellLevel)
                        pasteProjections = copyPasteHelper.PastingFromClipboardCellLevel<TableView>(gridControl, RowData, EntitiesUndoRedoManager, out errorMessages);
                    else if (!DisablePasteRowLevel)
                        pasteProjections = copyPasteHelper.PastingFromClipboard<TableView>(gridControl, RowData, out errorMessages);
                    else
                        pasteProjections = new List<TProjection>();

                    if (pasteProjections.Count > 0)
                    {
                        //handled in BulkSave
                        //if (!IsPasteCellLevel)
                        //{
                        //    EntitiesUndoRedoManager.PauseActionId();
                        //    pasteProjections.ForEach(x => EntitiesUndoRedoManager.AddUndo(x, null, null, null, EntityMessageType.Added));
                        //    EntitiesUndoRedoManager.UnpauseActionId();
                        //}

                        //For copy paste don't have to refresh the entire list, just call ICanUpdate.Update() on entity
                        BaseBulkSave(pasteProjections, IsPasteCellLevel);

                        if (!IsPasteCellLevel && !DisablePasteRowLevel)
                            OnAfterNewProjectionsAdded(pasteProjections);
                    }

                    if (errorMessages.Count > 0)
                    {
                        FormatErrorMessagesCallBack?.Invoke(errorMessages);
                        if (ErrorMessagesDialogService != null)
                        {
                            DialogCollectionViewModel<ErrorMessage> viewModel = DialogCollectionViewModel<ErrorMessage>.Create(errorMessages, "The following data cannot be pasted");
                            ErrorMessagesDialogService.ShowDialog(MessageButton.OKCancel, string.Empty, "ListErrorMessages", viewModel);
                        }
                    }
                }

                e.Handled = true;
            }

            IsChangingValueFromBackgroundEvents = false;

            IsPasting = false;
        }

        /// <summary>
        /// Converts clipboard text into entity values and saves to database
        /// </summary>
        /// <param name="e"></param>
        public virtual void PastingFromClipboardTreeList(PastingFromClipboardEventArgs e)
        {
            IsChangingValueFromBackgroundEvents = true;
            CopyPasteHelper<TProjection> copyPasteHelper = new CopyPasteHelper<TProjection>(IsValidEntity, OnBeforePasteWithValidation, ErrorMessagesDialogService, UnifiedValueValidationCallback, null, null, null, null, null, FormatErrorMessagesCallBack);
            bool dontSplit = false;
            if ((Keyboard.Modifiers | ModifierKeys.Shift) == Keyboard.Modifiers)
            {
                dontSplit = true;
            }

            var PasteString = System.Windows.Clipboard.GetText();
            string[] RowData;
            if (dontSplit)
            {
                string format_string = PasteString.Substring(1, PasteString.Length - 2);
                RowData = new string[] { format_string };
            }
            else
                RowData = PasteString.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            GridControl gridControl = e.Source as GridControl;
            List<ErrorMessage> errorMessages = new List<ErrorMessage>();
            List<TProjection> pasteProjections;
            if (IsPasteCellLevel)
                pasteProjections = copyPasteHelper.PastingFromClipboardTreeListCellLevel<TreeListView>(gridControl, RowData, EntitiesUndoRedoManager, out errorMessages);
            else
                pasteProjections = copyPasteHelper.PastingFromClipboard<TreeListView>(gridControl, RowData, out errorMessages);

            if (pasteProjections.Count > 0)
            {
                //Handled in BulkSave
                //EntitiesUndoRedoManager.PauseActionId();
                //pasteProjections.ForEach(x => EntitiesUndoRedoManager.AddUndo(x, null, null, null, EntityMessageType.Added));
                //EntitiesUndoRedoManager.UnpauseActionId();

                BaseBulkSave(pasteProjections);
                e.Handled = true;
            }

            if (errorMessages.Count > 0)
            {
                FormatErrorMessagesCallBack?.Invoke(errorMessages);

                if(ErrorMessagesDialogService != null)
                {
                    DialogCollectionViewModel<ErrorMessage> viewModel = DialogCollectionViewModel<ErrorMessage>.Create(errorMessages, "The following data cannot be pasted");
                    ErrorMessagesDialogService.ShowDialog(MessageButton.OKCancel, string.Empty, "ListErrorMessages", viewModel);
                }
            }

            IsChangingValueFromBackgroundEvents = false;
        }
        #endregion

        public virtual void CleanUpCallBacks()
        {
            //Sometimes save happens when view is closing on ExistingRowAddUndoAndSave
            //this.AdditionalValidateRowCallBack = null;
            //this.ApplyEntityPropertiesToProjectionCallBack = null;
            //this.ApplyProjectionPropertiesToEntityCallBack = null;
            //this.CanBulkDeleteCallBack = null;
            //this.CanFillDownCallBack = null;
            //this.CreateNewProjectionFromNewEntityCallBack = null;
            //this.IsContinueNewRowFromViewCallBack = null;
            //this.IsContinueSaveCallBack = null;
            //this.IsValidFromViewCallBack = null;
            //this.OnAfterEntitiesDeletedCallBack = null;
            //this.OnAfterEntitySavedCallBack = null;
            //this.OnBeforeBulkEditSaveCallBack = null;
            //this.OnBeforeEntitiesDeleteCallBack = null;
            //this.OnBeforeEntityDeleteCallBack = null;
            //this.OnFillDownCompletedCallBack = null;
            //this.OnSelectedEntitiesChangedCallBack = null;
            //this.SetParentAssociationCallBack = null;
            //this.ValidateBulkEditCallBack = null;
            //this.ValidateFillDownCallBack = null;

            ////Entities view model call backs
            //this.OnEntitiesLoadedCallBack = null;
            //this.OnBeforeEntitiesChangedCallBack = null;
            //this.OnAfterEntitiesChangedCallBack = null;
        }
    }
}
