using DevExpress.Mvvm.UI.Interactivity;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace BaseModel.View.Behaviors
{
    public class DetailGridSearchingBehavior : Behavior<GridControl>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            this.AssociatedObject.Loaded += AssociatedObject_Loaded;
            this.AssociatedObject.Unloaded += AssociatedObject_Unloaded;
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            this.AssociatedObject.MasterRowExpanded += this.MasterRowExpanded;
            this.AssociatedObject.View.SearchPanelAllowFilter = false;
        }

        private void MasterRowExpanded(object sender, RowEventArgs e)
        {
            var detailView = this.GetDetailView(e.RowHandle);
            if ((detailView == null))
            {
                return;
            }

            detailView.ShowSearchPanelMode = ShowSearchPanelMode.Never;
            BindingOperations.SetBinding(detailView, DataViewBase.SearchStringProperty, new Binding("SearchString") { Source = AssociatedObject.View });
        }

        public TableView GetDetailView(int rowHandle)
        {
            var detail = this.AssociatedObject.GetDetail(rowHandle) as GridControl;
            return detail != null ? (TableView)detail.View : null;
        }

        private void AssociatedObject_Unloaded(object sender, RoutedEventArgs e)
        {
            this.AssociatedObject.MasterRowExpanded -= this.MasterRowExpanded;
        }
        protected override void OnDetaching()
        {
            this.AssociatedObject.Loaded -= AssociatedObject_Loaded;
            this.AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
            base.OnDetaching();
        }
    }
}
