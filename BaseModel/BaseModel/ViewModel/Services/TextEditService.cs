using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BaseModel.ViewModel.Services
{
    public interface ITextEditService
    {
        string GetSelectedText();
    }

    public class TextEditService : ServiceBase, ITextEditService
    {
        public TextEdit TextEdit
        {
            get { return (TextEdit)GetValue(TextEditProperty); }
            set { SetValue(TextEditProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Camera.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextEditProperty =
            DependencyProperty.Register("TextEdit", typeof(TextEdit), typeof(TextEditService), new PropertyMetadata(null));

        public string GetSelectedText()
        {
            if (this.TextEdit != null)
            {
                return TextEdit.SelectedText.Trim();
            }

            return string.Empty;
        }
    }
}
