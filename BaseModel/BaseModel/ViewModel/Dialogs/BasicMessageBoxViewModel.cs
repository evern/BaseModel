using DevExpress.Mvvm.POCO;
using System.Collections.Generic;
using System.Windows;

namespace BaseModel.ViewModel.Dialogs
{
    public class BasicMessageBoxViewModel
    {
        public static BasicMessageBoxViewModel Create(string message)
        {
            return ViewModelSource.Create(() => new BasicMessageBoxViewModel(message));
        }

        public string Message { get; set; }
        public bool IsChecked { get; set; }
        public Visibility CheckboxVisibility { get; set; }

        protected BasicMessageBoxViewModel(string message)
        {
            Message = message;
            CheckboxVisibility = Visibility.Hidden;
        }
    }
}