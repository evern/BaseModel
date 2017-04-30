using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;

namespace BaseModel.ViewModel.Dialogs
{
    public class BulkEditNumbersViewModel
    {
        public static BulkEditNumbersViewModel Create(decimal editNumber)
        {
            return ViewModelSource.Create(() => new BulkEditNumbersViewModel(editNumber));
        }

        public decimal EditValue { get; set; }
        public EnumMemberInfo SelectedOperation { get; set; }

        protected BulkEditNumbersViewModel(decimal editValue)
        {
            EditValue = editValue;
        }
    }
}