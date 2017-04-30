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
        where TEntity : class
        where TProjection : class
        where TUnitOfWork : IUnitOfWork
    {
        #region Call Backs
        /// <summary>
        /// Fires when selected entities is changed
        /// Used by dashboard to generate chart
        /// </summary>
        public Action OnSelectedEntitiesChangedCallBack;

        /// <summary>
        /// Used when multiple existing rows are affected by a single save operation
        /// Cannot be substituted by OnAfterEntitySavedCallBack in CollectionViewModelBase because edited fieldname is required
        /// </summary>
        public Action<TreeListCellValueChangedEventArgs> OnAfterTreelistExistingRowAddUndoAndSaveCallBack;

        /// <summary>
        /// Additional initialization parameter apart from SetParentAssociationCallBack from CollectionViewModelBase when RowEventArgs is needed
        /// e.g. Retrieving master row from child to set parent association
        /// </summary>
        public Func<RowEventArgs, TProjection, bool> IsContinueNewRowFromViewCallBack;

        /// <summary>
        /// Additional validation from view
        /// </summary>
        public Func<GridCellValidationEventArgs, bool> IsValidFromViewCallBack;

        /// <summary>
        /// Allows only certain fields to be edited in existing row
        /// Allows only associated entity to be saved in existing row
        /// Allows children entities to be saved and undo/redo action to be added
        /// </summary>
        public Func<TProjection, CellValueChangedEventArgs, bool> ExistingRowAddUndoAndSaveCallBack;

        /// <summary>
        /// Allows only specific rows be to deleted
        /// </summary>
        public Func<IEnumerable<TProjection>, bool> CanBulkDeleteCallBack; 
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
                    DataUtils.SetNestedValue(entityProperty.PropertyName, entityProperty.ChangedEntity,
                        entityProperty.NewValue);

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

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                textEditor.SelectionStart = textEditor.Text.Length;
                textEditor.SelectionLength = 0;
            }), DispatcherPriority.Background);
        }

        public void ShowPopUp(object sender)
        {
            var editor = sender as GridControl;
            var comboBoxEditor = editor.View.ActiveEditor as ComboBoxEdit;
            if (comboBoxEditor == null)
                return;
            if (comboBoxEditor.IsPopupOpen)
                return;
            else
                comboBoxEditor.ShowPopup();
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
                if (ExistingRowAddUndoAndSaveCallBack != null)
                    if (!ExistingRowAddUndoAndSaveCallBack(projection, e))
                    {
                        EntitiesUndoRedoManager.UnpauseActionId();
                        return;
                    }

                EntitiesUndoRedoManager.AddUndo(projection, e.Column.FieldName, e.OldValue, e.Value,
                    EntityMessageType.Changed);
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
            EntitiesUndoRedoManager.AddUndo(projection, e.Column.FieldName, e.OldValue, e.Value,
                EntityMessageType.Changed);
            EntitiesUndoRedoManager.UnpauseActionId();

            Save(projection);

            OnAfterTreelistExistingRowAddUndoAndSaveCallBack?.Invoke(e);
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

                if (IsContinueNewRowFromViewCallBack != null)
                    if (!IsContinueNewRowFromViewCallBack(e, projection))
                        return;

                EntitiesUndoRedoManager.AddUndo(projection, null, null, null, EntityMessageType.Added);
                Save(projection);
                EntitiesUndoRedoManager.UnpauseActionId();
            }
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

            IsValidFromViewCallBack?.Invoke(e);
        }

        public Func<GridRowValidationEventArgs, bool> AdditionalValidateRowCallBack { get; set; }

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

        #region Fill Down Convention

        public Func<IEnumerable<TProjection>, GridMenuInfo, bool> CanFillDownCallBack;

        public bool CanFillDown(object button)
        {
            var info = GridPopupMenuBase.GetGridMenuInfo((DependencyObject)button) as GridMenuInfo;
            return Entities != null && Entities.Count > 1 && !IsLoading && info != null && info.Column != null && !info.Column.ReadOnly && (CanFillDownCallBack == null || CanFillDownCallBack(SelectedEntities, info));
        }

        public bool CanFillUp(object button)
        {
            var info = GridPopupMenuBase.GetGridMenuInfo((DependencyObject)button) as GridMenuInfo;
            return Entities != null && Entities.Count > 1 && !IsLoading && info != null && info.Column != null && !info.Column.ReadOnly && (CanFillDownCallBack == null || CanFillDownCallBack(SelectedEntities, info));
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
            var info = GridPopupMenuBase.GetGridMenuInfo((DependencyObject)button) as GridMenuInfo;
            object valueToFill;

            if (isUp)
                valueToFill = DataUtils.GetNestedValue(info.Column.FieldName,
                    SelectedEntities[selectedentities.Count - 1]);
            else
                valueToFill = DataUtils.GetNestedValue(info.Column.FieldName, SelectedEntities[0]);

            EntitiesUndoRedoManager.PauseActionId();
            var bulkSaveEntities = new List<TProjection>();
            long? enumerator = null;
            int? numericIndex = null;
            int numericFieldLength = 0;
            if (valueToFill != null && valueToFill.GetType() == typeof(string))
            {
                string stringValueToFill = valueToFill.ToString();
                numericIndex = StringFormatUtils.GetNumericIndex(stringValueToFill, out numericFieldLength);
                if (numericIndex != null)
                {
                    enumerator = Int64.Parse(stringValueToFill.Substring(numericIndex.Value, stringValueToFill.Length - numericIndex.Value));
                }
            }

            if(!isUp)
            {
                for (int i = 1; i < SelectedEntities.Count; i++)
                {
                    if (enumerator != null)
                        enumerator++;

                    TProjection seletedEntity = SelectedEntities[i];
                    setEntityProperty(seletedEntity, info, valueToFill, numericIndex, enumerator, numericFieldLength);
                    bulkSaveEntities.Add(seletedEntity);
                }
            }
            else
            {
                for (int i = SelectedEntities.Count - 2; i >= 0; i--)
                {
                    if (enumerator != null && enumerator > 0)
                        enumerator--;

                    TProjection seletedEntity = SelectedEntities[i];
                    setEntityProperty(seletedEntity, info, valueToFill, numericIndex, enumerator, numericFieldLength);
                    bulkSaveEntities.Add(seletedEntity);
                }
            }

            BulkSave(bulkSaveEntities);
            EntitiesUndoRedoManager.UnpauseActionId();

            OnFillDownCompletedCallBack?.Invoke();
        }

        void setEntityProperty(TProjection editEntity, GridMenuInfo info, object valueToFill, int? numericIndex, long? enumerator, int numericFieldLength)
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
            EntitiesUndoRedoManager.AddUndo(editEntity, info.Column.FieldName, OldValue, valueToFill,
                EntityMessageType.Changed);
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
            return Entities != null && Entities.Count > 0 && !IsLoading &&
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

        public bool IsInBulkOperation { get; set; }
        /// <summary>
        /// Deletes a given entity from the repository and saves changes if confirmed by the user.
        /// Since CollectionViewModelBase is a POCO view model, an the instance of this class will also expose the DeleteCommand property that can be used as a binding source in views.
        /// </summary>
        /// <param name="projectionEntity">An entity to edit.</param>
        public void BulkSave(IEnumerable<TProjection> entities)
        {
            EntitiesUndoRedoManager.PauseActionId();
            BaseBulkSave(entities);
            EntitiesUndoRedoManager.UnpauseActionId();
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
                columnPropertyInfo.PropertyType == typeof(string))
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

        public Func<TProjection, string, object, bool> ValidateBulkEditCallBack;
        public Action<TProjection, string> OnBeforeBulkEditSaveCallBack;

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
                TProjection firstSelectedEntity = SelectedEntities.First();
                var columnPropertyInfo = DataUtils.GetNestedPropertyInfo(info.Column.FieldName, firstSelectedEntity);
                if (columnPropertyInfo.PropertyType == typeof(Guid) || columnPropertyInfo.PropertyType == typeof(Guid?) ||
                    columnPropertyInfo.PropertyType.BaseType == typeof(Enum))
                {
                    var copyColumnEditSettings = info.Column.ActualEditSettings as ComboBoxEditSettings;
                    if (copyColumnEditSettings != null)
                    {
                        var bulkEditEnumsViewModel =
                            BulkEditEnumsViewModel.Create((IEnumerable<object>)copyColumnEditSettings.ItemsSource,
                                copyColumnEditSettings.DisplayMember);
                        if (BulkColumnEditDialogService.ShowDialog(MessageButton.OKCancel, "Select Item to assign",
                                "BulkEditEnums", bulkEditEnumsViewModel) == MessageResult.OK)
                        {
                            if (bulkEditEnumsViewModel.SelectedItem != null)
                            {
                                if (columnPropertyInfo.PropertyType.BaseType == typeof(Enum))
                                {
                                    var selectedEnum = (EnumMemberInfo)bulkEditEnumsViewModel.SelectedItem;
                                    newValue = Enum.Parse(columnPropertyInfo.PropertyType, selectedEnum.Id.ToString());
                                }
                                else
                                {
                                    IHaveGUID entityWithGuid = bulkEditEnumsViewModel.SelectedItem as IHaveGUID;
                                    if(entityWithGuid != null)
                                    {
                                        newValue = entityWithGuid.Guid;
                                    }
                                }
                            }
                        }

                        bulkEditEnumsViewModel = null;
                    }
                }
                else if (columnPropertyInfo.PropertyType == typeof(decimal) ||
                         columnPropertyInfo.PropertyType == typeof(decimal?))
                {
                    var selectedEntityValue =
                        (decimal)DataUtils.GetNestedValue(info.Column.FieldName, SelectedEntities.First());
                    var bulkEditNumbersViewModel = BulkEditNumbersViewModel.Create(selectedEntityValue);
                    if (
                        BulkColumnEditDialogService.ShowDialog(MessageButton.OKCancel,
                            "Choose number and operation to assign", "BulkEditNumbers", bulkEditNumbersViewModel) ==
                        MessageResult.OK)
                    {
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
                        if (bulkEditStringsViewModel.EditValue != null)
                            newValue = bulkEditStringsViewModel.EditValue;
                }

                if (newValue != null)
                    foreach (var selectedProjection in SelectedEntities)
                    {
                        if (ValidateBulkEditCallBack != null &&
                            !ValidateBulkEditCallBack(selectedProjection, info.Column.FieldName, newValue))
                            continue;

                        if (newValue.GetType() == typeof(decimal) && operation != Arithmetic.None)
                        {
                            var currentValue =
                                (decimal)DataUtils.GetNestedValue(info.Column.FieldName, selectedProjection);
                            var currentOldValue = currentValue;

                            if (operation == Arithmetic.Add)
                                currentValue = currentValue + (decimal)newValue;
                            else if (operation == Arithmetic.Subtract)
                                currentValue = currentValue - (decimal)newValue;
                            else if (operation == Arithmetic.Multiply)
                                currentValue = currentValue * (decimal)newValue;
                            else if (operation == Arithmetic.Divide && (decimal)newValue > 0)
                                currentValue = currentValue / (decimal)newValue;

                            DataUtils.SetNestedValue(info.Column.FieldName, selectedProjection, currentValue);
                            EntitiesUndoRedoManager.AddUndo(selectedProjection, info.Column.FieldName, currentOldValue,
                                currentValue, EntityMessageType.Changed);
                        }
                        else
                        {
                            oldValue = DataUtils.GetNestedValue(info.Column.FieldName, selectedProjection);
                            DataUtils.SetNestedValue(info.Column.FieldName, selectedProjection, newValue);
                            EntitiesUndoRedoManager.AddUndo(selectedProjection, info.Column.FieldName, oldValue,
                                newValue, EntityMessageType.Changed);
                        }

                        OnBeforeBulkEditSaveCallBack?.Invoke(selectedProjection, info.Column.FieldName);
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

        /// <summary>
        /// Converts clipboard text into entity values and saves to database
        /// </summary>
        /// <param name="e"></param>
        public virtual void PastingFromClipboard(PastingFromClipboardEventArgs e)
        {
            PasteListener?.Invoke(PasteStatus.Start);

            var PasteString = Clipboard.GetText();
            var RowData = PasteString.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var sourceGridControl = (GridControl)e.Source;
            var entityProperties = typeof(TEntity).GetProperties();
            var gridView = sourceGridControl.View;

            if (gridView.ActiveEditor == null && gridView.GetType() == typeof(TableViewEx))
            {
                var gridTableView = gridView as TableViewEx;
                foreach (var Row in RowData)
                {
                    var newEntity = CreateEntity();
                    //have to do a callback here because TProjection is not new() constrained yet
                    TProjection projection;
                    if (newEntity is TProjection)
                        projection = newEntity as TProjection;
                    else
                    {
                        if (CreateNewProjectionFromNewEntityCallBack != null)
                            projection = CreateNewProjectionFromNewEntityCallBack();
                        else
                            return;
                    }

                    var ColumnStrings = Row.Split('\t');
                    for (var i = 0; i < ColumnStrings.Count(); i++)
                        try
                        {
                            var copyColumn = gridTableView.VisibleColumns[i];

                            if (copyColumn.ReadOnly)
                                continue;

                            var columnName = copyColumn.FieldName;
                            var columnPropertyInfo = DataUtils.GetNestedPropertyInfo(columnName, projection);
                            if (columnPropertyInfo != null)
                                if (columnPropertyInfo.PropertyType == typeof(Guid?) ||
                                    columnPropertyInfo.PropertyType == typeof(Guid))
                                {
                                    var copyColumnEditSettings =
                                        copyColumn.ActualEditSettings as ComboBoxEditSettings;
                                    if (copyColumnEditSettings != null)
                                    {
                                        var copyColumnValueMember = copyColumnEditSettings.ValueMember;
                                        var copyColumnDisplayMember = copyColumnEditSettings.DisplayMember;
                                        var copyColumnItemsSource =
                                            copyColumnEditSettings.ItemsSource as IEnumerable<object>;
                                        Guid? itemValue = null;
                                        foreach (var copyColumnItem in copyColumnItemsSource)
                                        {
                                            var itemDisplayMemberPropertyInfo =
                                                copyColumnItem.GetType().GetProperty(copyColumnDisplayMember);
                                            var itemValueMemberPropertyInfo =
                                                copyColumnItem.GetType().GetProperty(copyColumnValueMember);
                                            if (itemDisplayMemberPropertyInfo.GetValue(copyColumnItem).ToString() ==
                                                ColumnStrings[i])
                                                itemValue = (Guid)itemValueMemberPropertyInfo.GetValue(copyColumnItem);
                                        }

                                        if (itemValue != null)
                                            DataUtils.SetNestedValue(columnName, projection, itemValue);
                                        else
                                            continue;
                                    }
                                    else if (ColumnStrings[i] != Guid.Empty.ToString())
                                    {
                                        var newGuid = new Guid(ColumnStrings[i]);
                                        DataUtils.SetNestedValue(columnName, projection, newGuid);
                                    }
                                }
                                else if (columnPropertyInfo.PropertyType == typeof(string))
                                    DataUtils.SetNestedValue(columnName, projection, ColumnStrings[i]);
                                else if (columnPropertyInfo.PropertyType.BaseType == typeof(Enum))
                                {
                                    var enumValues = Enum.GetValues(columnPropertyInfo.PropertyType);
                                    foreach (var enumValue in enumValues)
                                    {
                                        var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
                                        if (fieldInfo == null)
                                            return;

                                        var descriptionAttributes =
                                            fieldInfo.GetCustomAttributes(typeof(DisplayAttribute), false) as
                                                DisplayAttribute[];
                                        if (descriptionAttributes == null || descriptionAttributes.Count() == 0)
                                            return;

                                        var descriptionAttribute = descriptionAttributes.First();
                                        if (ColumnStrings[i] == descriptionAttribute.Name)
                                        {
                                            DataUtils.SetNestedValue(columnName, projection, enumValue);
                                            continue;
                                        }
                                    }
                                }
                                else if (columnPropertyInfo.PropertyType == typeof(decimal) ||
                                         columnPropertyInfo.PropertyType == typeof(decimal?)
                                         || columnPropertyInfo.PropertyType == typeof(int) ||
                                         columnPropertyInfo.PropertyType == typeof(int?)
                                         || columnPropertyInfo.PropertyType == typeof(double) ||
                                         columnPropertyInfo.PropertyType == typeof(double?))
                                {
                                    var rgx = new Regex("[^0-9a-z\\.]");
                                    var cleanColumnString = rgx.Replace(ColumnStrings[i], string.Empty);

                                    if (columnPropertyInfo.PropertyType == typeof(decimal) ||
                                        columnPropertyInfo.PropertyType == typeof(decimal?))
                                    {
                                        decimal getDecimal;
                                        if (decimal.TryParse(cleanColumnString, out getDecimal))
                                        {
                                            if (columnName.Contains('%') || columnName.ToUpper().Contains("PERCENT"))
                                                getDecimal /= 100;

                                            DataUtils.SetNestedValue(columnName, projection, getDecimal);
                                        }
                                        else
                                            return;
                                    }
                                    else if (columnPropertyInfo.PropertyType == typeof(int) ||
                                             columnPropertyInfo.PropertyType == typeof(int?))
                                    {
                                        int getInt;
                                        if (int.TryParse(cleanColumnString, out getInt))
                                            DataUtils.SetNestedValue(columnName, projection, getInt);
                                        else
                                            return;
                                    }
                                    else if (columnPropertyInfo.PropertyType == typeof(double) ||
                                             columnPropertyInfo.PropertyType == typeof(double?))
                                    {
                                        double getDouble;
                                        if (double.TryParse(cleanColumnString, out getDouble))
                                        {
                                            if (columnName.Contains('%') || columnName.ToUpper().Contains("PERCENT"))
                                                getDouble /= 100;

                                            DataUtils.SetNestedValue(columnName, projection, getDouble);
                                        }
                                        else
                                            return;
                                    }
                                    else
                                        return;
                                }
                                else if (columnPropertyInfo.PropertyType == typeof(DateTime?) ||
                                         columnPropertyInfo.PropertyType == typeof(DateTime))
                                {
                                    DateTime getDateTime;
                                    if (DateTime.TryParse(ColumnStrings[i], out getDateTime))
                                        DataUtils.SetNestedValue(columnName, projection, getDateTime);
                                    else
                                        continue;
                                }
                                else
                                    continue;
                            else
                                continue;
                        }
                        catch
                        {
                            return;
                        }

                    var errorMessage = "Duplicate exists on constraint field named: ";
                    if (IsValidEntity(projection, ref errorMessage))
                        if (OnBeforePasteWithValidation != null)
                        {
                            if (OnBeforePasteWithValidation(projection))
                                Save(projection);
                        }
                        else
                            Save(projection);
                    else
                    {
                        errorMessage += " , paste operation will be terminated";
                        MessageBoxService.ShowMessage(errorMessage, CommonResource.Exception_UpdateErrorCaption,
                            MessageButton.OK);
                        break;
                    }
                }

                PasteListener?.Invoke(PasteStatus.Stop);
                e.Handled = true;
            }
        }

        #endregion

        public virtual void CleanUpCallBacks()
        {
            this.AdditionalValidateRowCallBack = null;
            this.ApplyEntityPropertiesToProjectionCallBack = null;
            this.ApplyProjectionPropertiesToEntityCallBack = null;
            this.CanBulkDeleteCallBack = null;
            this.CanFillDownCallBack = null;
            this.CreateNewProjectionFromNewEntityCallBack = null;
            this.ExistingRowAddUndoAndSaveCallBack = null;
            this.IsContinueNewRowFromViewCallBack = null;
            this.IsContinueSaveCallBack = null;
            this.IsValidFromViewCallBack = null;
            this.OnAfterEntitiesDeletedCallBack = null;
            this.OnAfterEntitySavedCallBack = null;
            this.OnAfterTreelistExistingRowAddUndoAndSaveCallBack = null;
            this.OnBeforeBulkEditSaveCallBack = null;
            this.OnBeforeEntitiesDeleteCallBack = null;
            this.OnBeforeEntityDeleteCallBack = null;
            this.OnFillDownCompletedCallBack = null;
            this.OnSelectedEntitiesChangedCallBack = null;
            this.SetParentAssociationCallBack = null;
            this.ValidateBulkEditCallBack = null;
            this.ValidateFillDownCallBack = null;

            //Entities view model call backs
            this.OnEntitiesLoadedCallBack = null;
            this.OnBeforeEntitiesChangedCallBack = null;
            this.OnAfterEntitiesChangedCallBack = null;
        }
    }
}