using BaseModel.DataModel;
using BaseModel.Misc;
using BaseModel.ViewModel.Loader;
using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseModel.ViewModel.Composition
{
    public class TreeViewModel<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork> : EntitiesTreeCollectionWrapper<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork>
        where TMainEntity : class, IGuidEntityKey, new()
        where TMainProjectionEntity : class, IGuidEntityKey, IHaveSortOrder, IHaveExpandState, IGuidParentEntityKey, INewEntityName, ICanUpdate, new()
        where TMainEntityUnitOfWork : IUnitOfWork
    {
        public override string ViewName => string.Empty;

        private Func<TMainEntityUnitOfWork, IRepository<TMainEntity, TMainEntityPrimaryKey>> _getRepositoryFunc;
        private IUnitOfWorkFactory<TMainEntityUnitOfWork> _unitOfWorkFactory;
        private Func<IRepositoryQuery<TMainEntity>, IQueryable<TMainProjectionEntity>> _projectionFunc;
        public delegate string UnifiedRowValidationDelegate(TMainProjectionEntity projection);
        public event UnifiedRowValidationDelegate OnRowValidationEvent;
        public delegate string UnifiedValueValidationDelegate(TMainProjectionEntity projection, string field_name, object new_value);
        public event UnifiedValueValidationDelegate OnValueValidationEvent;
        public delegate bool OnBeforeEntitySavedDelegate(TMainProjectionEntity projection);
        public event OnBeforeEntitySavedDelegate OnBeforeEntitySavedEvent;
        /// <summary>
        /// Creates a new instance of TreeViewModel as a POCO view model.
        /// </summary>
        /// <param name="unitOfWorkFactory">unit of work factory</param>
        /// <param name="getRepositoryFunc">function to get repository context</param>
        /// <param name="projectionFunc">linq query function</param>
        /// <returns></returns>
        public static TreeViewModel<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork> Create(IUnitOfWorkFactory<TMainEntityUnitOfWork> unitOfWorkFactory, Func<TMainEntityUnitOfWork, IRepository<TMainEntity, TMainEntityPrimaryKey>> getRepositoryFunc, Func<IRepositoryQuery<TMainEntity>, IQueryable<TMainProjectionEntity>> projectionFunc)
        {
            return ViewModelSource.Create(() => new TreeViewModel<TMainEntity, TMainProjectionEntity, TMainEntityPrimaryKey, TMainEntityUnitOfWork>(unitOfWorkFactory, getRepositoryFunc, projectionFunc));
        }

        /// <summary>
        /// Initializes a new instance of the TreeViewModel class.
        /// This constructor is declared protected to avoid undesired instantiation of the TreeViewModel type without the POCO proxy factory.
        /// </summary>
        protected TreeViewModel(IUnitOfWorkFactory<TMainEntityUnitOfWork> unitOfWorkFactory, Func<TMainEntityUnitOfWork, IRepository<TMainEntity, TMainEntityPrimaryKey>> getRepositoryFunc, Func<IRepositoryQuery<TMainEntity>, IQueryable<TMainProjectionEntity>> projectionFunc)
            : base()
        {
            _getRepositoryFunc = getRepositoryFunc;
            _unitOfWorkFactory = unitOfWorkFactory;
            _projectionFunc = projectionFunc;
            //start loading, entities view model starts loading on parameter changes
            OnParameterChange(null);
        }

        protected override void resolveParameters(object parameter)
        {
        }

        protected override bool onBeforeEntitySavedIsContinue(TMainProjectionEntity entity)
        {
            OnBeforeEntitySavedEvent?.Invoke(entity);
            return base.onBeforeEntitySavedIsContinue(entity);
        }

        protected override Func<IRepositoryQuery<TMainEntity>, IQueryable<TMainProjectionEntity>> specifyMainViewModelProjection()
        {
            return _projectionFunc;
        }

        public override string UnifiedRowValidation(TMainProjectionEntity projection)
        {
            return OnRowValidationEvent?.Invoke(projection);
        }

        public override string UnifiedValueValidation(TMainProjectionEntity projection, string field_name, object new_value)
        {
            return OnValueValidationEvent?.Invoke(projection, field_name, new_value);
        }

        protected override void addEntitiesLoader()
        {
        }

        protected override void onAuxiliaryEntitiesCollectionLoaded()
        {
            CreateMainViewModel(_unitOfWorkFactory, _getRepositoryFunc);
            mainThreadDispatcher.BeginInvoke(new Action(() => mainEntityLoaderDescription.CreateCollectionViewModel()));
        }
    }
}
