using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace BaseModel.Misc
{
    public class GroupChildSelector : DependencyObject
    {
        static readonly DependencyProperty ModeProperty = DependencyProperty.RegisterAttached("Mode", typeof(ChildSelectionMode), typeof(GroupChildSelector), new PropertyMetadata(ChildSelectionMode.None, new PropertyChangedCallback(OnModeChanged)));

        public static ChildSelectionMode GetMode(DependencyObject obj)
        {
            return (ChildSelectionMode)obj.GetValue(ModeProperty);
        }
        public static void SetMode(DependencyObject obj, ChildSelectionMode value)
        {
            obj.SetValue(ModeProperty, value);
        }

        static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is TableView)) return;
            TableView view = (d as TableView);
            view.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            view.Grid.GroupRowExpanding += OnGroupRowExpanding;
        }
        static void OnGroupRowExpanding(object sender, RowAllowEventArgs e)
        {
            var grid = (GridControl)sender;
            grid.BeginSelection();
            SelectChild(grid, e.RowHandle);
            grid.EndSelection();
        }
        static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TableView view = (e.Source as TableView);
            if (view != null)
            {
                TableViewHitInfo hitInfo = view.CalcHitInfo(e.OriginalSource as DependencyObject);
                if (hitInfo.InRow && view.Grid.IsGroupRowHandle(hitInfo.RowHandle))
                {
                    view.Grid.BeginSelection();
                    SelectChild(view.Grid, hitInfo.RowHandle);
                    view.Grid.EndSelection();
                }
            }
        }
        static void SelectChild(GridControl grid, int groupRowHandle)
        {
            int childRowCount = grid.GetChildRowCount(groupRowHandle);
            grid.BeginSelection();
            for (int i = 0; i < childRowCount; i++)
            {
                int childRowHandle = grid.GetChildRowHandle(groupRowHandle, i);
                if (grid.IsGroupRowHandle(childRowHandle)
                    && grid.IsGroupRowExpanded(childRowHandle))
                    //if (GetMode(grid.View) == ChildSelectionMode.Hierarchical
                    //&& grid.IsGroupRowHandle(childRowHandle)
                    //&& grid.IsGroupRowExpanded(childRowHandle))
                {
                    SelectChild(grid, childRowHandle);
                }
                grid.SelectItem(childRowHandle);
            }
            grid.EndSelection();
        }
    }

    public enum ChildSelectionMode
    {
        None,
        Child,
        Hierarchical,
    }
}
