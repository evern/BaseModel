using BaseModel.Data.Helpers;
using DevExpress.Mvvm.POCO;
using DevExpress.Xpf.Grid;
using System.Collections.Generic;

namespace BaseModel.ViewModel.Dialogs
{
    public class DialogCollectionViewModel<TEntity>
        where TEntity : class
    {
        public static DialogCollectionViewModel<TEntity> Create(IEnumerable<TEntity> enumerableObjects, string message = "")
        {
            return ViewModelSource.Create(() => new DialogCollectionViewModel<TEntity>(enumerableObjects, message));
        }

        public IEnumerable<TEntity> SourceObjects { get; set; }
        public string Message { get; set; }

        protected DialogCollectionViewModel(IEnumerable<TEntity> enumerableObjects, string message = "")
        {
            SourceObjects = enumerableObjects;
            Message = message;
        }
    }
}