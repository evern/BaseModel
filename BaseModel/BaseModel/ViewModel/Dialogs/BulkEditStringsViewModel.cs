using DevExpress.Mvvm.POCO;

namespace BaseModel.ViewModel.Dialogs
{
    public class BulkEditStringsViewModel
    {
        public static BulkEditStringsViewModel Create(string editString)
        {
            return ViewModelSource.Create(() => new BulkEditStringsViewModel(editString));
        }

        public string EditValue { get; set; }

        protected BulkEditStringsViewModel(string editString)
        {
            EditValue = editString;
        }
    }
}