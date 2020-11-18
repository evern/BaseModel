using BaseModel.Data.Helpers;
using BaseModel.ViewModel.Services;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.POCO;
using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Grid;
using System.Collections.Generic;

namespace BaseModel.ViewModel.Dialogs
{
    public class DialogCollectionViewModel<TEntity>
        where TEntity : class
    {
        public static DialogCollectionViewModel<TEntity> Create(IEnumerable<TEntity> enumerableObjects, string message = "", string excelExportFileName = "")
        {
            return ViewModelSource.Create(() => new DialogCollectionViewModel<TEntity>(enumerableObjects, message, excelExportFileName));
        }

        [ServiceProperty(Key = "DefaultTableViewService")]
        protected virtual ITableViewService TableViewService { get { return null; } }

        public IEnumerable<TEntity> SourceObjects { get; set; }
        public string Message { get; set; }
        private string excelExportFileName;
        protected IMessageBoxService MessageBoxService
        {
            get { return this.GetRequiredService<IMessageBoxService>(); }
        }

        protected virtual IFolderBrowserDialogService FolderBrowserDialogService { get { return this.GetService<IFolderBrowserDialogService>(); } }

        protected DialogCollectionViewModel(IEnumerable<TEntity> enumerableObjects, string message = "", string excelExportFileName = "")
        {
            SourceObjects = enumerableObjects;
            Message = message;
            if (excelExportFileName == string.Empty)
                this.excelExportFileName = "ErrorMessages";
            else
                this.excelExportFileName = excelExportFileName;
        }

        public virtual void ExportToExcel()
        {
            string ResultPath = string.Empty;
            if (FolderBrowserDialogService.ShowDialog())
            {
                ResultPath = FolderBrowserDialogService.ResultPath;
                bool result = TableViewService.ExportToXls(ResultPath + "\\" + excelExportFileName + ".xlsx", false);

                if (!result)
                    MessageBoxService.ShowMessage("Export failed because the file is in use", "Warning", MessageButton.OK, MessageIcon.Warning);
            }
        }
    }
}