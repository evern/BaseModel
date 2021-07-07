using BaseModel.Data.Helpers;
using BaseModel.DataModel;
using BaseModel.Misc;
using BaseModel.ViewModel.Dialogs;
using BaseModel.ViewModel.Loader;
using BaseModel.ViewModel.Services;
using BaseModel.ViewModel.UndoRedo;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.POCO;
using DevExpress.Xpf.Grid;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace BaseModel.ViewModel.Base
{
    public abstract class InstantFeedbackCollectionViewModelBase<TEntity, TProjection, TPrimaryKey, TUnitOfWork> : IDocumentContent, ISupportLogicalLayout, ISupportUndoRedo<TEntity>, IDisposable
        where TEntity : class, new()
        where TProjection : class, ICanUpdate, new()
        where TUnitOfWork : IUnitOfWork
    {
        #region inner classes
        public class InstantFeedbackSourceViewModel : IListSource
        {
            public static InstantFeedbackSourceViewModel Create(Func<int> getCount, IInstantFeedbackSource<TProjection> source)
            {
                return ViewModelSource.Create(() => new InstantFeedbackSourceViewModel(getCount, source));
            }

            readonly Func<int> getCount;
            readonly IInstantFeedbackSource<TProjection> source;

            protected InstantFeedbackSourceViewModel(Func<int> getCount, IInstantFeedbackSource<TProjection> source)
            {
                this.getCount = getCount;
                this.source = source;
            }

            public int Count { get { return getCount(); } }

            public void Refresh()
            {
                source.Refresh();
                this.RaisePropertyChanged(x => x.Count);
            }

            bool IListSource.ContainsListCollection { get { return source.ContainsListCollection; } }

            IList IListSource.GetList()
            {
                return source.GetList();
            }
        }
        #endregion

        protected readonly IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory;
        protected readonly Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc;
        protected Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> Projection { get; private set; }
        Func<bool> canCreateNewEntity;
        IRepository<TEntity, TPrimaryKey> helperRepository;
        IInstantFeedbackSource<TProjection> source;
        protected InstantFeedbackCollectionViewModelBase(
            IUnitOfWorkFactory<TUnitOfWork> unitOfWorkFactory,
            Func<TUnitOfWork, IRepository<TEntity, TPrimaryKey>> getRepositoryFunc,
            Func<IRepositoryQuery<TEntity>, IQueryable<TProjection>> projection,
            Func<bool> canCreateNewEntity = null)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.canCreateNewEntity = canCreateNewEntity;
            this.getRepositoryFunc = getRepositoryFunc;
            this.Projection = projection;
            this.helperRepository = CreateRepository();

            RepositoryExtensions.VerifyProjection(helperRepository, projection);

            this.source = unitOfWorkFactory.CreateInstantFeedbackSource(getRepositoryFunc, Projection);
            this.Entities = InstantFeedbackSourceViewModel.Create(() => helperRepository.Count(), source);

            if (!this.IsInDesignMode())
                OnInitializeInRuntime();
        }

        public InstantFeedbackSourceViewModel Entities { get; private set; }
        public virtual object CurrentEntity { get; set; }

        protected ILayoutSerializationService LayoutSerializationService { get { return this.GetService<ILayoutSerializationService>(); } }
        string ViewName { get { return typeof(TEntity).Name + "InstantFeedbackCollectionView"; } }
        public Action<TEntity> ApplyInstantFeedbackEntityPropertiesToOtherUnitOfWorkEntityCallBack;
        public Action OtherUnitOfWorkSaveChangesCallBack;
        [ServiceProperty(Key = "DefaultGridControlService")]
        public virtual IGridControlService GridControlService { get { return null; } }

        private TPrimaryKey GetPrimaryKey(object threadSafeProxy)
        {
            return GetProxyPrimaryKey(threadSafeProxy);
        }

        public virtual void Refresh()
        {
            this.source = unitOfWorkFactory.CreateInstantFeedbackSource(getRepositoryFunc, Projection);
            this.Entities = InstantFeedbackSourceViewModel.Create(() => helperRepository.Count(), source);
            GridControlService.SaveExpansionStates();
            this.GetParentViewModel<CollectionViewModelsWrapper<TEntity, TProjection, TPrimaryKey, TUnitOfWork>>().RaisePropertyChanged(x => x.InstantFeedbackEntities);
        }

        public virtual void SaveChanges(bool sendMessage)
        {
            if (OtherUnitOfWorkSaveChangesCallBack != null)
                OtherUnitOfWorkSaveChangesCallBack();
            else
                helperRepository.UnitOfWork.SaveChanges();

            if(sendMessage)
                Messenger.Default.Send(new EntityMessage<TEntity>(EntityMessageType.Changed));
        }

        public virtual void EditSelectedEntity(string fieldName, object newValue)
        {
            Save(CurrentEntity, fieldName, newValue);
        }

        public virtual void SaveEntity(TEntity entity, string fieldName, object newValue)
        {
            if (DataUtils.TrySetNestedValue(fieldName, entity, newValue))
            {
                ApplyInstantFeedbackEntityPropertiesToOtherUnitOfWorkEntityCallBack?.Invoke(entity);
                SaveChanges(true);
            }
        }

        public virtual void Save(object threadSafeProxy, string fieldName, object newValue)
        {
            TEntity entity = GetEntityFromThreadSafeProxy(threadSafeProxy);
            if (entity == null)
                return;

            object oldValue = DataUtils.GetNestedValue(fieldName, entity);
            if (DataUtils.TrySetNestedValue(fieldName, entity, newValue))
            {
                EntitiesUndoRedoManager.AddUndo(entity, fieldName, oldValue, newValue, EntityMessageType.Changed);
                ApplyInstantFeedbackEntityPropertiesToOtherUnitOfWorkEntityCallBack?.Invoke(entity);
                SaveChanges(true);
            }
        }

        public TEntity GetEntityFromThreadSafeProxy(object threadSafeProxy)
        {
            if (!source.IsLoadedProxy(threadSafeProxy))
                return null;

            TPrimaryKey primaryKey = GetProxyPrimaryKey(threadSafeProxy);
            TEntity entity = helperRepository.Find(primaryKey);
            if (entity == null)
                return null;

            return entity;
        }

        public TPrimaryKey GetProxyPrimaryKey(object threadSafeProxy)
        {
            var expression = RepositoryExtensions.GetProjectionPrimaryKeyExpression<TEntity, TProjection, TPrimaryKey>(helperRepository);
            return GetProxyPropertyValue(threadSafeProxy, expression);
        }

        public TProperty GetProxyPropertyValue<TProperty>(object threadSafeProxy, Expression<Func<TProjection, TProperty>> propertyExpression)
        {
            return source.GetPropertyValue(threadSafeProxy, propertyExpression);
        }

        public TProperty GetProxyPropertyValueTest<TProperty>(object threadSafeProxy, string propertyName)
        {
            var expression = RepositoryExtensions.GetProjectionValueExpression<TProjection, TProperty>(propertyName);
            return source.GetPropertyValue(threadSafeProxy, expression);
        }

        protected IMessageBoxService MessageBoxService { get { return this.GetRequiredService<IMessageBoxService>(); } }
        protected IDocumentManagerService DocumentManagerService { get { return this.GetService<IDocumentManagerService>(); } }

        protected virtual void OnBeforeEntityDeleted(TPrimaryKey primaryKey, TEntity entity) { }

        protected void DestroyDocument(IDocument document)
        {
            if (document != null)
                document.Close();
        }

        protected IRepository<TEntity, TPrimaryKey> CreateRepository()
        {
            return getRepositoryFunc(CreateUnitOfWork());
        }

        TUnitOfWork unitOfWork;
        protected TUnitOfWork CreateUnitOfWork()
        {
            unitOfWork = unitOfWorkFactory.CreateUnitOfWork();
            return unitOfWork;
        }

        protected virtual void OnInitializeInRuntime()
        {
            Messenger.Default.Register<EntityMessage<TEntity>>(this, x => OnMessage(x));
        }

        protected virtual void OnDestroy()
        {
            Messenger.Default.Unregister(this);
        }

        void OnMessage(EntityMessage<TEntity> message)
        {
            Refresh();
        }

        protected IDocumentOwner DocumentOwner { get; private set; }

        #region IDocumentContent
        object IDocumentContent.Title { get { return null; } }

        void IDocumentContent.OnDestroy()
        {
            OnDestroy();
        }

        public void OnClose(CancelEventArgs e)
        {
        }

        IDocumentOwner IDocumentContent.DocumentOwner
        {
            get { return DocumentOwner; }
            set { DocumentOwner = value; }
        }
        #endregion

        #region ISupportLogicalLayout
        bool ISupportLogicalLayout.CanSerialize
        {
            get { return true; }
        }

        IDocumentManagerService ISupportLogicalLayout.DocumentManagerService
        {
            get { return DocumentManagerService; }
        }

        IEnumerable<object> ISupportLogicalLayout.LookupViewModels
        {
            get { return null; }
        }
        #endregion

        #region View Events
        public void MouseDoubleClick(MouseButtonEventArgs e)
        {
            
        }

        #region ISupportUndoRedo
        /// <summary>
        /// Manages all undo and redo operation
        /// </summary>
        private EntitiesUndoRedoManager<TEntity> entitiesundoredomanager { get; set; }

        public EntitiesUndoRedoManager<TEntity> EntitiesUndoRedoManager
        {
            get
            {
                if (entitiesundoredomanager == null)
                    entitiesundoredomanager = new EntitiesUndoRedoManager<TEntity>(BulkPropertUndo, BulkPropertyRedo);

                return entitiesundoredomanager;
            }
        }

        public void BulkPropertUndo(IEnumerable<UndoRedoEntityInfo<TEntity>> entityProperties)
        {
            IEnumerable<UndoRedoEntityInfo<TEntity>> bulkSaveProperties = entityProperties.Where(x => x.MessageType == EntityMessageType.Changed);
            var bulkSavePropertiesGroupByEntity = bulkSaveProperties.GroupBy(x => x.ChangedEntity).Select(group => new { Entity = group.Key, UndoRedoEntityInfos = group.ToList() });

            foreach(var bulkSavePropertyGroupByEntity in bulkSavePropertiesGroupByEntity)
            {
                foreach (UndoRedoEntityInfo<TEntity> entityProperty in bulkSavePropertyGroupByEntity.UndoRedoEntityInfos)
                {
                    DataUtils.SetNestedValue(entityProperty.PropertyName, entityProperty.ChangedEntity, entityProperty.OldValue);
                }

                ApplyInstantFeedbackEntityPropertiesToOtherUnitOfWorkEntityCallBack?.Invoke(bulkSavePropertyGroupByEntity.Entity);
            }

            SaveChanges(true);
        }

        public virtual void BulkPropertyRedo(IEnumerable<UndoRedoEntityInfo<TEntity>> entityProperties)
        {
            IEnumerable<UndoRedoEntityInfo<TEntity>> bulkSaveProperties = entityProperties.Where(x => x.MessageType == EntityMessageType.Changed);
            var bulkSavePropertiesGroupByEntity = bulkSaveProperties.GroupBy(x => x.ChangedEntity).Select(group => new { Entity = group.Key, UndoRedoEntityInfos = group.ToList() });

            foreach (var bulkSavePropertyGroupByEntity in bulkSavePropertiesGroupByEntity)
            {
                foreach (UndoRedoEntityInfo<TEntity> entityProperty in bulkSavePropertyGroupByEntity.UndoRedoEntityInfos)
                {
                    DataUtils.SetNestedValue(entityProperty.PropertyName, entityProperty.ChangedEntity, entityProperty.NewValue);
                }

                ApplyInstantFeedbackEntityPropertiesToOtherUnitOfWorkEntityCallBack?.Invoke(bulkSavePropertyGroupByEntity.Entity);
            }

            SaveChanges(true);
        }

        public void PropertyUndo(UndoRedoEntityInfo<TEntity> entityProperty)
        {
            if (entityProperty.MessageType == EntityMessageType.Changed)
                SaveEntity(entityProperty.ChangedEntity, entityProperty.PropertyName, entityProperty.OldValue);
        }

        public void PropertyRedo(UndoRedoEntityInfo<TEntity> entityProperty)
        {
            if (entityProperty.MessageType == EntityMessageType.Changed)
                SaveEntity(entityProperty.ChangedEntity, entityProperty.PropertyName, entityProperty.NewValue);
        }

        public void Undo()
        {
            EntitiesUndoRedoManager.Undo();
        }

        public void Redo()
        {
            EntitiesUndoRedoManager.Redo();
        }

        public bool CanUndo()
        {
            return EntitiesUndoRedoManager.CanUndo();
        }

        public bool CanRedo()
        {
            return EntitiesUndoRedoManager.CanRedo();
        }

        public void Dispose()
        {
            OnDestroy();
        }
        #endregion

        #region Copy Paste
        //Indicate that paste data will not have carriage return in cells, to improve paste data accuracy
        public bool UseRegularSplitting;
        public MultiSelectMode SelectMode => IsPasteCellLevel ? MultiSelectMode.Cell : MultiSelectMode.Row;
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

        protected IDialogService ErrorMessagesDialogService
        {
            get { return this.GetRequiredService<IDialogService>("ErrorMessagesDialogService"); }
        }

        /// <summary>
        /// Converts clipboard text into entity values and saves to database
        /// </summary>
        /// <param name="e"></param>
        public virtual void PastingFromClipboard(PastingFromClipboardEventArgs e)
        {
            bool shouldSkip = false;
            var gridControl = (GridControl)e.Source;
            TableView tableView = gridControl.View as TableView;
            //when cell is in editing mode, user might want to paste clipboard data into cell
            if (tableView.ActiveEditor != null)
                return;

            if (tableView != null && tableView.FocusedRowHandle == GridControl.AutoFilterRowHandle)
            {
                shouldSkip = true;
            }

            List<int> selectedRowHandles = gridControl.GetSelectedRowHandles().ToList();
            if (!shouldSkip)
            {
                CopyPasteHelper<TEntity> copyPasteHelper = new CopyPasteHelper<TEntity>(IsValidEntity, null, ErrorMessagesDialogService, null, null, null, null, null, null, null, GetEntityFromThreadSafeProxy);

                bool dontSplit = false;
                if ((Keyboard.Modifiers | ModifierKeys.Shift) == Keyboard.Modifiers)
                    dontSplit = true;

                List<TEntity> pasteEntities;
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

                List<ErrorMessage> errorMessages = new List<ErrorMessage>();
                if (IsPasteCellLevel)
                    pasteEntities = copyPasteHelper.PastingFromClipboardCellLevel<InstantFeedbackTableView>(gridControl, RowData, EntitiesUndoRedoManager, out errorMessages);
                else
                    pasteEntities = copyPasteHelper.PastingFromClipboard<InstantFeedbackTableView>(gridControl, RowData, out errorMessages);

                if (pasteEntities.Count > 0)
                {
                    if(ApplyInstantFeedbackEntityPropertiesToOtherUnitOfWorkEntityCallBack != null)
                    {
                        foreach (TEntity pasteEntity in pasteEntities)
                        {
                            ApplyInstantFeedbackEntityPropertiesToOtherUnitOfWorkEntityCallBack(pasteEntity);
                        }
                    }

                    SaveChanges(true);
                }

                if (errorMessages.Count > 0)
                {
                    if (ErrorMessagesDialogService != null)
                    {
                        DialogCollectionViewModel<ErrorMessage> viewModel = DialogCollectionViewModel<ErrorMessage>.Create(errorMessages, "The following data cannot be pasted");
                        ErrorMessagesDialogService.ShowDialog(MessageButton.OKCancel, string.Empty, "ListErrorMessages", viewModel);
                    }
                }

                gridControl.UpdateGroupSummary();
                gridControl.UpdateTotalSummary();
                e.Handled = true;
            }
        }
        #endregion

        #region Data Operation
        /// <summary>
        /// Determine whether other entities in the collection shares any common combination of unique constraints
        /// </summary>
        /// <param name="entity">The entity to be validated</param>
        /// <param name="errorMessage">Error message to notify the user of conflicting constraints</param>
        /// <returns>Returns true if no other entity contains similar constraint member values</returns>
        public virtual bool IsValidEntity(TEntity entity, IEnumerable<TEntity> preCommittedProjections, ref string errorMessage, out List<KeyValuePair<string, string>> constraintIssues)
        {
            //if (OnBeforeEntitySavedIsContinueCallBack != null && !OnBeforeEntitySavedIsContinueCallBack(entity))
            //    return false;

            constraintIssues = new List<KeyValuePair<string, string>>();
            if (!DataUtils.IsRequiredAttributesHasValue<TEntity, TEntity>(entity, ref errorMessage))
                return false;

            if (errorMessage != null && errorMessage != string.Empty)
                return false;

            return true;
        }

        #endregion
        #endregion
    }
}
