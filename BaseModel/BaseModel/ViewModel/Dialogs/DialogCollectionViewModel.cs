using DevExpress.Mvvm.POCO;
using System.Collections.Generic;

namespace BaseModel.ViewModel.Dialogs
{
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