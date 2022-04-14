using DevExpress.Mvvm.POCO;
using System;

namespace BaseModel.ViewModel.Dialogs
{
    public class BulkEditDateTimeViewModel
    {
        public static BulkEditDateTimeViewModel Create(DateTime editDate, string labelTitle = "Value:")
        {
            return ViewModelSource.Create(() => new BulkEditDateTimeViewModel(editDate, labelTitle));
        }

        public DateTime EditValue { get; set; }
        public string LabelTitle { get; set; }
        protected BulkEditDateTimeViewModel(DateTime editString, string labelTitle)
        {
            EditValue = editString;
            LabelTitle = labelTitle;
        }
    }
}