using DevExpress.Mvvm.POCO;

namespace BaseModel.ViewModel.Dialogs
{
    public class BulkEditStringsViewModel
    {
        public static BulkEditStringsViewModel Create(string editString, string labelTitle = "Value:")
        {
            return ViewModelSource.Create(() => new BulkEditStringsViewModel(editString, labelTitle));
        }

        public string EditValue { get; set; }
        public string LabelTitle { get; set; }
        protected BulkEditStringsViewModel(string editString, string labelTitle)
        {
            EditValue = editString;
            LabelTitle = labelTitle;
        }
    }
}