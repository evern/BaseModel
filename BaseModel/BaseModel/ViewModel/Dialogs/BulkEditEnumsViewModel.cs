using DevExpress.Mvvm.POCO;
using System.Collections.Generic;

namespace BaseModel.ViewModel.Dialogs
{
    public class BulkEditEnumsViewModel
    {
        public static BulkEditEnumsViewModel Create(IEnumerable<object> enumerableObjects, string comboBoxDisplayMember)
        {
            return ViewModelSource.Create(() => new BulkEditEnumsViewModel(enumerableObjects, comboBoxDisplayMember));
        }

        public IEnumerable<object> SourceObjects { get; set; }
        public string ComboBoxDisplayMember { get; set; }

        protected BulkEditEnumsViewModel(IEnumerable<object> enumerableObjects, string comboBoxDisplayMember)
        {
            SourceObjects = enumerableObjects;
            ComboBoxDisplayMember = comboBoxDisplayMember;
        }

        private object selectedItem { get; set; }

        public object SelectedItem
        {
            get { return selectedItem; }
            set
            {
                if (value != null)
                {
                    selectedItem = value;
                    this.RaisePropertiesChanged();
                }
            }
        }
    }
}