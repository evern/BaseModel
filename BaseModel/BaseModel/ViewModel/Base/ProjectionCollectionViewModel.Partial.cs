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
        /// <summary>
        /// Fires when selected entities is changed
        /// Used by dashboard to generate chart
        /// </summary>
        public Action OnSelectedEntitiesChangedCallBack;

        /// <summary>
        /// Additional initialization parameter apart from SetParentAssociationCallBack from CollectionViewModelBase when RowEventArgs is needed
        /// e.g. Retrieving master row from child to set parent association
        /// </summary>
        public Func<RowEventArgs, TProjection, bool> OnBeforeViewNewRowSavedIsContinueCallBack;

        ///// <summary>
        ///// Additional validation for cell
        ///// </summary>
        //public Action<GridCellValidationEventArgs> AdditionalValidateCellCallBack;

        /// <summary>
        /// Additional validation for row
        /// </summary>
        public Action<GridRowValidationEventArgs> AdditionalValidateRowCallBack { get; set; }

        /// <summary>
        /// Allows only specific rows be to deleted
        /// </summary>
        public Func<IEnumerable<TProjection>, bool> CanBulkDeleteCallBack;

        /// <summary>
        /// External call back used by copy paste, fill, new and existing row cell value changing to determine which other cells to affect
        /// </summary>
        public Action<string, object, object, TProjection, bool> UnifiedValueChangingCallback;

        /// <summary>
        /// External call back used by copy paste, fill, new and existing row cell value changing to determine whether value is valid
        /// </summary>
        public Func<TProjection, string, object, string> UnifiedValueValidationCallback;
        #endregion

        /// <summary>
        /// Initializes a new instance of the CollectionViewModel class.
        /// This constructor is declared protected to avoid an undesired instantiation of the CollectionViewModel type without the POCO proxy factory.
        /// </summary>
        /// <param name="unitOfWorkFactory">A factory used to create a unit of work instance.</param>
        /// <param name="getRepositoryFunc">A function that returns a repository representing entities of the given type.</param>
        /// <param name="projection">A LINQ function used to customize a query for entities. The parameter, for example, can be used for sorting data and/or for projecting data to a custom type that does not match the repository entity type.</param>
        /// <param name="newEntityInitializer">An optional parameter that provides a function to initialize a new entity. This parameter is used in the detail collection view models when creating a single object view model for a new entity.</param>
        /// <param name="canCreateNewEntity">A function that is called before an attempt to create a new entity is made. This parameter is used together with the newEntityInitializer parameter.</param>
        /// <param name="ignoreSelectEntityMessage">An optional parameter that used to specify that the selected entity should not be managed by PeekCollectionViewModel.</param>
        protected CollectionViewModel(
            IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory,
            Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc,
            Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> projection,
            Action<TEntity> newEntityInitializer = null,
            Func<bool> canCreateNewEntity = null,
            bool ignoreSelectEntityMessage = false
        )
            : base(
                unitOfWorkFactory, getRepositoryFunc, projection, newEntityInitializer, canCreateNewEntity,
                ignoreSelectEntityMessage)
        {
            InitZoom();
            SelectedEntities = new ObservableCollection<TProjection>();
            SelectedEntities.CollectionChanged += SelectedEntities_CollectionChanged;
        }

        #region Interceptors

        protected override void AddUndoBeforeEntityDeleted(TProjection projection)
        {
            if (!EntitiesUndoRedoManager.IsInUndoRedoOperation())
                EntitiesUndoRedoManager.AddUndo(projection, null, null, null, EntityMessageType.Deleted);
            base.AddUndoBeforeEntityDeleted(projection);
        }

        #endregion

        protected virtual void SelectedEntities_CollectionChanged(object sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnSelectedEntitiesChangedCallBack?.Invoke();
        }

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
                ViewModelSource.Create(
                    () =>
                        new CollectionViewModel<TEntity, TProjection, TPrimaryKey, TUnitOfWork>(unitOfWorkFactory,
                            getRepositoryFunc, projection, null, null, false));
        }

        #region Selected Entities
        /// <summary>
        /// The selected entities.
        /// Since CollectionViewModel is a POCO view model, this property will raise INotifyPropertyChanged.PropertyEvent when modified so it can be used as a binding source in views.
        /// </summary>
        private ObservableCollection<TProjection> selectedentities { get; set; }

        public ObservableCollection<TProjection> SelectedEntities
        {
            get { return selectedentities; }
            set { selectedentities = value; }
        }
        #endregion

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
                    entitiesundoredomanager = new EntitiesUndoRedoManager<TProjection>(PropertyUndo, PropertyRedo);

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

        /// <summary>
        /// Specify whether any elements remains in the undo list
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the CanUndoCommand property that can be used as a binding source in views.
        /// </summary>
        public bool CanUndo()
        {
            return EntitiesUndoRedoManager.CanUndo();
        }

        /// <summary>
        /// Specify whether any elements remains in the undo list
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the CanRedoCommand property that can be used as a binding source in views.
        /// </summary>
        public bool CanRedo()
        {
            return EntitiesUndoRedoManager.CanRedo();
        }

        /// <summary>
        /// Undo last operation
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the UndoCommand property that can be used as a binding source in views.
        /// </summary>
        public void Undo()
        {
            EntitiesUndoRedoManager.Undo();
        }

        /// <summary>
        /// Redo last operation
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the RedoCommand property that can be used as a binding source in views.
        /// </summary>
        public void Redo()
        {
            EntitiesUndoRedoManager.Redo();
        }

        #endregion

        #region Data Operations
        /// <summary>
        /// Determine whether other entities in the collection shares any common combination of unique constraints
        /// </summary>
        /// <param name="entity">The entity to be validated</param>
        /// <param name="errorMessage">Error message to notify the user of conflicting constraints</param>
        /// <returns>Returns true if no other entity contains similar constraint member values</returns>
        public bool IsValidEntity(TProjection entity, ref string errorMessage)
        {
            if (!isRequiredAttributesHasValue(entity, ref errorMessage))
                return false;

            return IsUniqueEntityConstraintValues(entity, ref errorMessage);
        }

        /// <summary>
        /// Determine whether other entities in the collection shares any common combination of unique constraints
        /// And since this is designed to be called from cell value changing the entity would not have been updated with the new value
        /// Hence fieldName and newValue is used for validation
        /// </summary>
        /// <param name="entity">The entity to be validated</param>
        /// <param name="fieldName">Fieldname of the current changing cell</param>
        /// <param name="newValue">New value of the current changing cell</param>
        /// <param name="errorMessage">Error message to notify the user of conflicting constraints</param>
        /// <returns>Returns true if no other entity contains similar constraint member values</returns>
        public bool IsValidEntityCellValue(TProjection entity, string fieldName, object newValue,
            ref string errorMessage)
        {
            return IsUniqueEntityConstraintValues(entity, ref errorMessage,
                new KeyValuePair<string, object>(fieldName, newValue));
            //return IsUniqueEntityConstraintValues(entity, ref errorMessage);
        }

        /// <summary>
        /// Gets the concatenated string value for constraint field name for an entity
        /// </summary>
        /// <param name="entity">Entity to retrieve the field name</param>
        /// <param name="errorMessage">Error message to be populated with entity member constraint field names</param>
        /// <param name="keyValuePairNewFieldValue">In some instance the new value isn't yet updated on the entity, so this provides other ways pass in the new value</param>
        /// <returns>Concatenated constraint value string</returns>
        private bool IsUniqueEntityConstraintValues(TProjection entity, ref string errorMessage,
            KeyValuePair<string, object>? keyValuePairNewFieldValue = null)
        {
            var currentEntityConcatenatedConstraints = string.Empty;

            var constraintMemberPropertyStrings =
                DataUtils.GetConstraintPropertyStrings(typeof(TProjection));
            if (constraintMemberPropertyStrings == null)
                return true;
            else if (keyValuePairNewFieldValue != null &&
                     !constraintMemberPropertyStrings.Contains(((KeyValuePair<string, object>)keyValuePairNewFieldValue).Key))
                return true;

            foreach (var constraintMemberPropertyString in constraintMemberPropertyStrings)
            {
                object constraintMemberPropertyValue = null;
                if (keyValuePairNewFieldValue == null)
                    constraintMemberPropertyValue = DataUtils.GetNestedValue(constraintMemberPropertyString, entity);
                else
                {
                    var keyValuePairForNewFieldValue =
                        (KeyValuePair<string, object>)keyValuePairNewFieldValue;
                    if (constraintMemberPropertyString == keyValuePairForNewFieldValue.Key)
                        constraintMemberPropertyValue = keyValuePairForNewFieldValue.Value;
                    else
                        constraintMemberPropertyValue = DataUtils.GetNestedValue(constraintMemberPropertyString, entity);
                }

                if (constraintMemberPropertyValue != null)
                {
                    var immediatePropertyString = constraintMemberPropertyString.Split('.').Last();
                    errorMessage += immediatePropertyString + " and ";
                    string constraintMemberPropertyStringFormat;
                    if (constraintMemberPropertyValue.GetType() == typeof(decimal))
                        constraintMemberPropertyStringFormat = ((decimal)constraintMemberPropertyValue).ToString("0.00");
                    else
                        constraintMemberPropertyStringFormat = constraintMemberPropertyValue.ToString();
                    currentEntityConcatenatedConstraints += constraintMemberPropertyStringFormat;
                }
            }

            return IsConstraintExistsInOtherEntities(entity, currentEntityConcatenatedConstraints,
                constraintMemberPropertyStrings, ref errorMessage);
        }


        /// <summary>
        /// Determine whether current entity constraint exists in other entity
        /// </summary>
        /// <param name="entity">The entity to be validated</param>
        /// <param name="entityConstraint">Constraint string of the current entity</param>
        /// <param name="constraintMemberPropertyInfos">Constraint property infos</param>
        /// <param name="constraintErrorMessage">Error message to notify the user of conflicting constraints</param>
        /// <returns>Returns true if no other entity contains similar constraint member values</returns>
        private bool IsConstraintExistsInOtherEntities(TProjection entity, string entityConstraint,
            IEnumerable<string> constraintMemberPropertyStrings, ref string constraintErrorMessage)
        {
            if (entityConstraint == string.Empty)
                return true;

            var keyPropertyInfo = DataUtils.GetKeyPropertyInfo(typeof(TProjection));
            object exclusionKeyValue = null;
            if (keyPropertyInfo != null)
                exclusionKeyValue = keyPropertyInfo.GetValue(entity);

            foreach (var otherEntity in Entities)
            {
                if (keyPropertyInfo != null)
                {
                    var otherKey = keyPropertyInfo.GetValue(otherEntity);
                    if (otherKey.Equals(exclusionKeyValue))
                        continue;
                }

                var otherEntityConcatenatedConstraints = string.Empty;
                foreach (var constraintMemberPropertyString in constraintMemberPropertyStrings)
                {
                    var constraintMemberPropertyValue = DataUtils.GetNestedValue(constraintMemberPropertyString,
                        otherEntity);
                    if (constraintMemberPropertyValue != null)
                    {
                        string constraintMemberPropertyStringFormat;
                        if (constraintMemberPropertyValue.GetType() == typeof(decimal))
                            constraintMemberPropertyStringFormat =
                                ((decimal)constraintMemberPropertyValue).ToString("0.00");
                        else
                            constraintMemberPropertyStringFormat = constraintMemberPropertyValue.ToString();

                        otherEntityConcatenatedConstraints += constraintMemberPropertyStringFormat;
                    }
                }

                if (otherEntityConcatenatedConstraints != string.Empty &&
                    otherEntityConcatenatedConstraints == entityConstraint)
                {
                    constraintErrorMessage = constraintErrorMessage.Substring(0, constraintErrorMessage.Length - 5);
                    constraintErrorMessage = constraintErrorMessage.Replace("GUID_", string.Empty);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Check if entity have key value
        /// </summary>
        /// <param name="entity">The entity to be validated</param>
        /// <param name="errorMessage">Error mesasage formatted with key property info</param>
        /// <returns></returns>
        private bool isRequiredAttributesHasValue(TProjection entity, ref string errorMessage)
        {
            IEnumerable<string> requiredPropertyStrings;
            if (typeof(TProjection) == typeof(TEntity))
                requiredPropertyStrings = DataUtils.GetRequiredPropertyStrings(typeof(TProjection));
            else
                requiredPropertyStrings = DataUtils.GetRequiredPropertyStringsForProjection(typeof(TProjection));

            var requiredPropertyNames = string.Empty;
            if (requiredPropertyStrings == null || requiredPropertyStrings.Count() == 0)
                return true;
            else
            {
                foreach (var requiredPropertyString in requiredPropertyStrings)
                {
                    var requiredPropertyValue = DataUtils.GetNestedValue(requiredPropertyString, entity);
                    if (requiredPropertyValue == null || requiredPropertyValue.ToString() == Guid.Empty.ToString())
                        requiredPropertyNames += requiredPropertyString.Replace("GUID_", string.Empty).Split('.').Last() + ", ";
                }

                if (requiredPropertyNames != string.Empty)
                {
                    errorMessage = string.Format("{0} value missing",
                        requiredPropertyNames.Substring(0, requiredPropertyNames.Length - 2));
                    return false;
                }
                else
                    return true;
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
        public virtual void NewRowAddUndoAndSave(RowEventArgs e)
        {
            if (e.RowHandle == DataControlBase.NewItemRowHandle)
            {
                EntitiesUndoRedoManager.PauseActionId();

                var projection = (TProjection)e.Row;
                ICanUpdate updateProjection = projection as ICanUpdate;
                if (updateProjection != null)
                    updateProjection.NewEntityFromView = true;

                if (OnBeforeViewNewRowSavedIsContinueCallBack != null)
                    if (!OnBeforeViewNewRowSavedIsContinueCallBack(e, projection))
                        return;

                Save(projection);
                //add undo must be after so that Guid is populated
                EntitiesUndoRedoManager.AddUndo(projection, null, null, null, EntityMessageType.Added);
                EntitiesUndoRedoManager.UnpauseActionId();
            }
        }

        /// <summary>
        /// Remembers an entity property old value for undoing
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the AddUndoCommand property that can be used as a binding source in views.
        /// </summary>
        public virtual void ExistingRowAddUndoAndSave(CellValueChangedEventArgs e)
        {
            if (e.RowHandle != DataControlBase.NewItemRowHandle)
            {
                var projection = (TProjection)e.Row;

                EntitiesUndoRedoManager.PauseActionId();
                EntitiesUndoRedoManager.AddUndo(projection, e.Column.FieldName, e.OldValue, e.Value, EntityMessageType.Changed);
                EntitiesUndoRedoManager.UnpauseActionId();

                Save(projection);
            }
        }


        /// <summary>
        /// Remembers an entity property old value for undoing
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the AddUndoCommand property that can be used as a binding source in views.
        /// </summary>
        public virtual void TreelistExistingRowAddUndoAndSave(TreeListCellValueChangedEventArgs e)
        {
            var projection = (TProjection)e.Row;

            EntitiesUndoRedoManager.PauseActionId();
            EntitiesUndoRedoManager.AddUndo(projection, e.Column.FieldName, e.OldValue, e.Value, EntityMessageType.Changed);
            EntitiesUndoRedoManager.UnpauseActionId();

            Save(projection);
        }

        protected override void OnBeforeEntitySaved(TEntity entity)
        {
            InstantiateEntity(entity);
            base.OnBeforeEntitySaved(entity);
        }


        /// <summary>
        /// Validate any row within the binded datagrid
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the ValidateRowCommand property that can be used as a binding source in views.
        /// </summary>
        /// <param name="e"></param>
        public virtual void ValidateCell(GridCellValidationEventArgs e)
        {
            var constraintName = string.Empty;
            if (!IsValidEntityCellValue((TProjection)e.Row, e.Column.FieldName, e.Value, ref constraintName))
            {
                e.IsValid = false;
                e.ErrorType = DevExpress.XtraEditors.DXErrorProvider.ErrorType.Critical;
                e.ErrorContent = constraintName + " is not unique";
            }

            if(UnifiedValueValidationCallback != null)
            {
                string error_message = UnifiedValueValidationCallback((TProjection)e.Row, e.Column.FieldName, e.Value);
                if (error_message != string.Empty)
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
            if (!IsValidEntity((TProjection)e.Row, ref errorMessage))
            {
                e.IsValid = false;
                e.ErrorType = DevExpress.XtraEditors.DXErrorProvider.ErrorType.Critical;
                e.ErrorContent = errorMessage;
            }

            AdditionalValidateRowCallBack?.Invoke(e);
        }

        #endregion

        #region Cell Content Deletion
        public virtual void DeleteCellContent(GridControl gridControl)
        {
            string[] RowData = new string[] { string.Empty };
            CopyPasteHelper<TProjection> copyPasteHelper = new CopyPasteHelper<TProjection>(IsValidEntity, OnBeforePasteWithValidation, MessageBoxService, UnifiedValueValidationCallback);
            List<TProjection> pasteProjections;
            if(gridControl.View.GetType() == typeof(TableView))
                pasteProjections = copyPasteHelper.PastingFromClipboardCellLevel<TableView>(gridControl, RowData, EntitiesUndoRedoManager);
            else
                pasteProjections = copyPasteHelper.PastingFromClipboardTreeListCellLevel<TreeListView>(gridControl, RowData, EntitiesUndoRedoManager);

            if (pasteProjections.Count > 0)
            {
                BulkSave(pasteProjections);
            }
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

            EntitiesUndoRedoManager.PauseActionId();
            var bulkSaveEntities = new List<TProjection>();

            long? enumerationDifferences = null;
            long? enumerator = null;
            int? numericIndex = null;
            int numericFieldLength = 0;
            EnumerationType enumerationType;
            if (valueToFill != null && valueToFill.GetType() == typeof(string) && nextValueInSequence != null)
                enumerationType = getEnumerateType(valueToFill.ToString(), nextValueInSequence.ToString(), out enumerationDifferences, out enumerator, out numericIndex, out numericFieldLength);
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
                        string error_message = UnifiedValueValidationCallback.Invoke(seletedEntity, info.Column.FieldName, valueToFill);
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
                        string error_message = UnifiedValueValidationCallback.Invoke(seletedEntity, info.Column.FieldName, valueToFill);
                        if (error_message == string.Empty)
                        {
                            setEntityProperty(seletedEntity, info, valueToFill, numericIndex, enumerator, numericFieldLength);
                            bulkSaveEntities.Add(seletedEntity);
                        }
                    }
                }
            }

            BulkSave(bulkSaveEntities);
            EntitiesUndoRedoManager.UnpauseActionId();

            OnFillDownCompletedCallBack?.Invoke();
        }

        private EnumerationType getEnumerateType(string value, string nextvalue, out long? differences, out long? startEnumeration, out int? numericIndex, out int numericFieldLength)
        {
            long? nextEnumerator = null;
            int? nextNumericIndex = null;
            int nextNumericFieldLength = 0;

            differences = null;
            numericIndex = StringFormatUtils.GetNumericIndex(value, out numericFieldLength);
            if (numericIndex != null)
                startEnumeration = Int64.Parse(value.Substring(numericIndex.Value, value.Length - numericIndex.Value));
            else
            {
                startEnumeration = null;
                return EnumerationType.None;
            }

            nextNumericIndex = StringFormatUtils.GetNumericIndex(nextvalue, out nextNumericFieldLength);
            if (nextNumericIndex != null)
            {
                if (numericIndex == nextNumericIndex)
                    nextEnumerator = Int64.Parse(nextvalue.Substring(nextNumericIndex.Value, nextvalue.Length - nextNumericIndex.Value));
                else
                    return EnumerationType.None;
            }

            if (startEnumeration < nextEnumerator)
            {
                if (startEnumeration != null && nextEnumerator != null)
                {
                    differences = (long)nextEnumerator - (long)startEnumeration;
                    return EnumerationType.Increase;
                }
                else
                    return EnumerationType.None;

            }
            else
            {
                if (startEnumeration != null && nextEnumerator != null)
                {
                    differences = (long)startEnumeration - (long)nextEnumerator;
                    return EnumerationType.Decrease;
                }
                else
                    return EnumerationType.None;
            }
        }

        private void setEntityProperty(TProjection editEntity, GridMenuInfo info, object valueToFill, int? numericIndex, long? enumerator, int numericFieldLength)
        {
            if (numericIndex != null && enumerator != null)
            {
                string valueToFillStringOnly = valueToFill.ToString().Substring(0, valueToFill.ToString().Length - numericFieldLength);

                valueToFill = StringFormatUtils.AppendStringWithEnumerator(valueToFillStringOnly, (long)enumerator, numericFieldLength);
                //int actualReplacementPos;
                //if (enumeratorString.Length <= numericFieldLength)
                //    actualReplacementPos = valueToFillStringOnly.Length - enumeratorString.Length;
                //else
                //    actualReplacementPos = numericIndex.Value;

                //if (actualReplacementPos > 0)
                //{
                //    valueToFillString = valueToFillString.Substring(0, actualReplacementPos);
                //    valueToFillString = valueToFillString + enumerator.Value.ToString();
                //    valueToFill = valueToFillString;
                //}
            }

            if (ValidateFillDownCallBack != null &&
                !ValidateFillDownCallBack(editEntity, info.Column.FieldName, valueToFill))
                return;

            var OldValue = DataUtils.GetNestedValue(info.Column.FieldName, editEntity);
            UnifiedValueChangingCallback?.Invoke(info.Column.FieldName, OldValue, valueToFill, editEntity, false);
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

        #region Bulk Operation
        /// <summary>
        /// Determines whether an entities can be deleted
        /// Since CollectionViewModelBase is a POCO view model, this method will be used as a CanExecute callback for BulkDeleteCommand.
        /// </summary>
        /// <param name="projectionEntity">Entities to delete.</param>
        public virtual bool CanBulkDelete()
        {
            return Entities != null && Entities.Count > 0 && !IsLoading && SelectedEntities != null && SelectedEntities.Count > 0 && 
                   (CanBulkDeleteCallBack == null || CanBulkDeleteCallBack(selectedentities));
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
            EntitiesUndoRedoManager.PauseActionId();
            BaseBulkDelete(selectedentities);
            EntitiesUndoRedoManager.UnpauseActionId();
        }

        public void KeyboardCopy()
        {
            SendKeys.SendWait("^c");
        }

        public void KeyboardPaste()
        {
            SendKeys.SendWait("^v");
        }

        public bool IsInBulkOperation { get; set; }
        /// <summary>
        /// Deletes a given entity from the repository and saves changes if confirmed by the user.
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the DeleteCommand property that can be used as a binding source in views.
        /// </summary>
        /// <param name="projectionEntity">An entity to edit.</param>
        public void BulkSave(IEnumerable<TProjection> entities)
        {
            BaseBulkSave(entities);
        }
        #endregion

        #region Bulk Edit

        private IDialogService BulkColumnEditDialogService
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

        public Action<List<KeyValuePair<ColumnBase, string>>, TProjection> ManualPasteAction;
        public void BulkColumnEdit(object button)
        {
            var info = GridPopupMenuBase.GetGridMenuInfo((DependencyObject)button) as GridMenuInfo;

            object oldValue = null;
            object newValue = null;
            var SaveEntities = new List<TProjection>();
            var operation = Arithmetic.None;

            EntitiesUndoRedoManager.PauseActionId();
            try
            {
                bool commence_bulk_edit = false;
                TProjection firstSelectedEntity = SelectedEntities.First();
                var columnPropertyInfo = DataUtils.GetNestedPropertyInfo(info.Column.FieldName, firstSelectedEntity);
                if (columnPropertyInfo.PropertyType == typeof(Guid) || columnPropertyInfo.PropertyType == typeof(Guid?) ||
                    columnPropertyInfo.PropertyType.BaseType == typeof(Enum))
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
                                        newValue = entityWithGuid.EntityKey;
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
                    if (
                        BulkColumnEditDialogService.ShowDialog(MessageButton.OKCancel, "Type in text for bulk edit",
                            "BulkEditStrings", bulkEditStringsViewModel) == MessageResult.OK)
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
                                string error_message = UnifiedValueValidationCallback.Invoke(selectedProjection, info.Column.FieldName, currentValue);
                                if (error_message == string.Empty)
                                {
                                    DataUtils.SetNestedValue(info.Column.FieldName, selectedProjection, currentValue);
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
                                string error_message = UnifiedValueValidationCallback.Invoke(selectedProjection, info.Column.FieldName, newValue);
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

                BulkSave(SaveEntities);
            }
            catch
            {
            }

            EntitiesUndoRedoManager.UnpauseActionId();
        }
        #endregion

        #region Data Operations
        public Func<TProjection, bool> OnBeforePasteWithValidation;
        public Action<PasteStatus> PasteListener;
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

        /// <summary>
        /// Converts clipboard text into entity values and saves to database
        /// </summary>
        /// <param name="e"></param>
        public virtual void PastingFromClipboard(PastingFromClipboardEventArgs e)
        {
            if (DisablePasting)
                return;
            
            PasteListener?.Invoke(PasteStatus.Start);
            CopyPasteHelper<TProjection> copyPasteHelper = new CopyPasteHelper<TProjection>(IsValidEntity, OnBeforePasteWithValidation, MessageBoxService, UnifiedValueValidationCallback, ManualPasteAction, UnifiedValueChangingCallback);

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
                RowData = excelSplit(PasteString).ToArray();

            var gridControl = (GridControl)e.Source;

            if (IsPasteCellLevel)
                pasteProjections = copyPasteHelper.PastingFromClipboardCellLevel<TableView>(gridControl, RowData, EntitiesUndoRedoManager);
            else if (!DisablePasteRowLevel)
                pasteProjections = copyPasteHelper.PastingFromClipboard<TableView>(gridControl, RowData);
            else
                pasteProjections = new List<TProjection>();

            if (pasteProjections.Count > 0)
            {
                if (!IsPasteCellLevel)
                {
                    EntitiesUndoRedoManager.PauseActionId();
                    pasteProjections.ForEach(x => EntitiesUndoRedoManager.AddUndo(x, null, null, null, EntityMessageType.Added));
                    EntitiesUndoRedoManager.UnpauseActionId();
                }

                BulkSave(pasteProjections);
                e.Handled = true;
            }

            PasteListener?.Invoke(PasteStatus.Stop);
        }

        /// <summary>
        /// Account for "" in excel splitting which signifies line breaks within "" isn't new row
        /// </summary>
        private List<string> excelSplit(string pasteString)
        {
            List<string> rowData = new List<string>();
            char[] charSplits = pasteString.ToCharArray();

            string rowCache = string.Empty;
            bool isQuoteOpen = false;
            for(int i = 0; i < charSplits.Count(); i++)
            {
                char? previousChar = i == 0 ? (char?)null : charSplits[i - 1];
                char currentChar = charSplits[i];

                if (currentChar == '"')
                    isQuoteOpen = !isQuoteOpen;
                else if (currentChar == '\n' && previousChar != null && ((char)previousChar) == '\r')
                {
                    if (!isQuoteOpen)
                    {
                        if(rowCache.Length > 1)
                            //remove the previous /r from row cache
                            rowCache = rowCache.Substring(0, rowCache.Length - 1);

                        rowData.Add(rowCache);
                        rowCache = string.Empty;
                    }
                    else
                        rowCache += currentChar;
                }
                else
                    rowCache += currentChar;
            }

            //when only a single row is pasted
            if (rowCache != string.Empty)
                rowData.Add(rowCache);

            return rowData;
        }

        /// <summary>
        /// Converts clipboard text into entity values and saves to database
        /// </summary>
        /// <param name="e"></param>
        public virtual void PastingFromClipboardTreeList(PastingFromClipboardEventArgs e)
        {
            if (DisablePasting)
                return;

            PasteListener?.Invoke(PasteStatus.Start);
            CopyPasteHelper<TProjection> copyPasteHelper = new CopyPasteHelper<TProjection>(IsValidEntity, OnBeforePasteWithValidation, MessageBoxService, UnifiedValueValidationCallback);
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
            List<TProjection> pasteProjections;
            if (IsPasteCellLevel)
                pasteProjections = copyPasteHelper.PastingFromClipboardTreeListCellLevel<TreeListView>(gridControl, RowData, EntitiesUndoRedoManager);
            else
                pasteProjections = copyPasteHelper.PastingFromClipboard<TreeListView>(gridControl, RowData);

            if (pasteProjections.Count > 0)
            {
                EntitiesUndoRedoManager.PauseActionId();
                pasteProjections.ForEach(x => EntitiesUndoRedoManager.AddUndo(x, null, null, null, EntityMessageType.Added));
                EntitiesUndoRedoManager.UnpauseActionId();

                BulkSave(pasteProjections);
                e.Handled = true;
            }

            PasteListener?.Invoke(PasteStatus.Stop);
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