using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BaseModel.ViewModel.Services
{
    public interface ITreeListControlService
    {
        void RefreshData();
    }

    public class TreeListControlService : ServiceBase, ITreeListControlService
    {
        public TreeListControl TreeListControl
        {
            get { return (TreeListControl)GetValue(TreeListControlProperty); }
            set { SetValue(TreeListControlProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Camera.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TreeListControlProperty =
            DependencyProperty.Register("TreeListControl", typeof(TreeListControl), typeof(TreeListControlService), new PropertyMetadata(null));

        public void RefreshData()
        {
            if (this.TreeListControl != null)
            {
                TreeListControl.RefreshData();
            }
        }
    }
}
