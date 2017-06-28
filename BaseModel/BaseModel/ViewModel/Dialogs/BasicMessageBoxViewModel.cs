using DevExpress.Mvvm.POCO;
using System.Collections.Generic;

namespace BaseModel.ViewModel.Dialogs
{
    public class BasicMessageBoxViewModel
    {
        public static BasicMessageBoxViewModel Create(string message)
        {
            return ViewModelSource.Create(() => new BasicMessageBoxViewModel(message));
        }

        public string Message { get; set; }

        protected BasicMessageBoxViewModel(string message)
        {
            Message = message;
        }
    }
}