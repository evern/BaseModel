using BaseModel.Data.Helpers;
using BaseModel.Misc;
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
    public interface IGridControlService
    {
        void SetRowExpandedByColumnValue(string field_name, IHaveExpandState row);
        void RefreshSummary();
    }

    public class GridControlService : ServiceBase, IGridControlService
    {
        public GridControl GridControl
        {
            get { return (GridControl)GetValue(GridControlProperty); }
            set { SetValue(GridControlProperty, value); }
        }
        
        public static readonly DependencyProperty GridControlProperty =
            DependencyProperty.Register("GridControl", typeof(GridControl), typeof(GridControlService), new PropertyMetadata(null));

        public void SetRowExpandedByColumnValue(string field_name, IHaveExpandState row)
        {
            if (field_name == string.Empty || row == null)
                return;

            object find_value = DataUtils.GetNestedValue(field_name, row);
            if (find_value == null)
                return;

            if (GridControl == null)
                return;

            var rowHandle = GridControl.DataController.FindRowByValue(field_name, find_value);
            if (rowHandle >= 0)
                GridControl.SetMasterRowExpanded(rowHandle, row.IsExpanded);
        }

        public void RefreshSummary()
        {
            if (GridControl == null)
                return;

            GridControl.RefreshData();
        }
    }
}
