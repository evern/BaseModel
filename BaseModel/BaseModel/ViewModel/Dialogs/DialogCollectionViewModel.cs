using DevExpress.Mvvm.POCO;
using System.Collections.Generic;

namespace BaseModel.ViewModel.Dialogs
{
    public class MissingP6Activities
    {
        public string INTERNAL_NUM { get; set; }
        public string P6_ACTIVITY { get; set; }
        public decimal UNITS { get; set; }
    }

    public class DialogCollectionViewModel<TEntity>
        where TEntity : class
    {
        public static DialogCollectionViewModel<TEntity> Create(IEnumerable<TEntity> enumerableObjects)
        {
            return ViewModelSource.Create(() => new DialogCollectionViewModel<TEntity>(enumerableObjects));
        }

        public IEnumerable<TEntity> SourceObjects { get; set; }

        protected DialogCollectionViewModel(IEnumerable<TEntity> enumerableObjects)
        {
            SourceObjects = enumerableObjects;
        }
    }
}