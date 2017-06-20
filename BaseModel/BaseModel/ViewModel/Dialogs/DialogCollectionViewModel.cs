using BaseModel.Data.Helpers;
using DevExpress.Mvvm.POCO;
using DevExpress.Xpf.Grid;
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