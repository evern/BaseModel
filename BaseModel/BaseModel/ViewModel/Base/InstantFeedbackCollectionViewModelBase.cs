using BaseModel.Data.Helpers;
using BaseModel.DataModel;
using BaseModel.Misc;
using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using DevExpress.Xpf.Grid;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace BaseModel.ViewModel.Base
{
    public abstract class InstantFeedbackCollectionViewModelBase<TEntity, TProjection, TPrimaryKey, TUnitOfWork> : IDocumentContent, ISupportLogicalLayout
        where TEntity : class, new()
        where TProjection : class
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
        public virtual object SelectedEntity { get; set; }
        protected ILayoutSerializationService LayoutSerializationService { get { return this.GetService<ILayoutSerializationService>(); } }
        string ViewName { get { return typeof(TEntity).Name + "InstantFeedbackCollectionView"; } }
        public CollectionViewModelBase<TEntity, TEntity, TPrimaryKey, TUnitOfWork>.EntitySaveDelegate OnBeforeProjectionSaveIsContinueCallBack;

        public virtual void Refresh()
        {
            //this.helperRepository = CreateRepository();

            //RepositoryExtensions.VerifyProjection(helperRepository, Projection);

            this.source = unitOfWorkFactory.CreateInstantFeedbackSource(getRepositoryFunc, Projection);
            this.Entities = InstantFeedbackSourceViewModel.Create(() => helperRepository.Count(), source);
            //Entities.Refresh();
        }

        public virtual void SaveChanges()
        {
            helperRepository.UnitOfWork.SaveChanges();
        }

        public virtual void Save(object threadSafeProxy, string fieldName, object new_value)
        {
            if (!source.IsLoadedProxy(threadSafeProxy))
                return;
            TPrimaryKey primaryKey = GetProxyPrimaryKey(threadSafeProxy);
            TEntity entity = helperRepository.Find(primaryKey);
            if (entity == null)
                return;

            if (DataUtils.TrySetNestedValue(fieldName, entity, new_value))
            {
                if(OnBeforeProjectionSaveIsContinueCallBack != null)
                {
                    bool isNewEntity = false;
                    OperationInterceptMode operationInterceptMode = OnBeforeProjectionSaveIsContinueCallBack.Invoke(entity, out isNewEntity);
                    if(operationInterceptMode == OperationInterceptMode.Continue)
                        SaveChanges();
                }
                else
                    SaveChanges();
            }
        }

        public TPrimaryKey GetProxyPrimaryKey(object threadSafeProxy)
        {
            var expression = RepositoryExtensions.GetProjectionPrimaryKeyExpression<TEntity, TProjection, TPrimaryKey>(helperRepository);
            return GetProxyPropertyValue(threadSafeProxy, expression);
        }

        protected TProperty GetProxyPropertyValue<TProperty>(object threadSafeProxy, Expression<Func<TProjection, TProperty>> propertyExpression)
        {
            return source.GetPropertyValue(threadSafeProxy, propertyExpression);
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

        protected TUnitOfWork CreateUnitOfWork()
        {
            return unitOfWorkFactory.CreateUnitOfWork();
        }

        protected virtual void OnInitializeInRuntime()
        {
            Messenger.Default.Register<EntityMessage<TEntity, TPrimaryKey>>(this, x => OnMessage(x));
        }

        protected virtual void OnDestroy()
        {
            Messenger.Default.Unregister(this);
        }

        void OnMessage(EntityMessage<TEntity, TPrimaryKey> message)
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
        #endregion
    }
}
