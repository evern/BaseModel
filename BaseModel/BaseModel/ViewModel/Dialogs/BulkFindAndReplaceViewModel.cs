using BaseModel.ViewModel.Services;
using DevExpress.Mvvm.POCO;
using System;

namespace BaseModel.ViewModel.Dialogs
{
    public class BulkFindAndReplaceViewModel
    {
        public static BulkFindAndReplaceViewModel Create(string editString)
        {
            return ViewModelSource.Create(() => new BulkFindAndReplaceViewModel(editString));
        }

        protected virtual ITextEditService TextEditService { get { return this.GetService<ITextEditService>(); } }

        public string FindValue { get; set; }

        public string ReplaceValue { get; set; }

        protected BulkFindAndReplaceViewModel(string editString)
        {
            FindValue = editString;
        }

        public void TrimToSelection()
        {
            if (TextEditService == null)
                return;

            string selectedText = TextEditService.GetSelectedText();
            FindValue = selectedText;
            this.RaisePropertyChanged(x => x.FindValue);
        }
    }
}