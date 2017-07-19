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
    public interface ITreeViewService
    {
        TreeListView GetTreeListView();
    }

    public class TreeViewService : ServiceBase, ITreeViewService
    {
        public TreeListView TreeView
        {
            get { return (TreeListView)GetValue(TreeViewProperty); }
            set { SetValue(TreeViewProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Camera.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TreeViewProperty =
            DependencyProperty.Register("TreeView", typeof(TreeListView), typeof(TreeViewService), new PropertyMetadata(null));

        public TreeListView GetTreeListView()
        {
            return TreeView;
        }
    }
}
