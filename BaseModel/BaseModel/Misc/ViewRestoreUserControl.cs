using BaseModel;
using BaseModel.ViewModel;
using BaseModel.Misc;
using DevExpress.Xpf.Grid;
using System.Windows.Controls;
using System.Windows.Input;
using System;

namespace BaseModel.Misc
{
    public class ViewStateRestoreUserControl : UserControl
    {
        int focusedRowHandle;
        ColumnBase currentColumn;
        bool onBeforeRefreshIsActive;

        GridControl gridControl;
        TableViewEx tableView;

        public void InitializeViewControl(GridControl gridControl, TableViewEx tableView)
        {
            this.gridControl = gridControl;
            this.tableView = tableView;

            this.gridControl.PreviewKeyDown += gridControl_PreviewKeyDown;

            ISupportViewRestoration viewRestoration = DataContext as ISupportViewRestoration;
            if(viewRestoration != null)
            {
                viewRestoration.StoreActiveCell = this.StoreFocusedCell;
                viewRestoration.RestoreActiveCell = this.RestoreFocusedCell;
                viewRestoration.ForceGridRefresh = this.ForceGridRefresh;
                viewRestoration.PostEditor = this.PostEditor;
            }

            foreach(GridColumn gridColumn in gridControl.Columns)
            {
                gridColumn.FilterPopupMode = FilterPopupMode.Excel;
            }
        }

        private void gridControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    tableView.CommitEditing();
                    tableView.MoveNextRow();
                    //this can't be used in filtered grid
                    //gridControl.SelectedItem = gridControl.GetRow(tableView.FocusedRowHandle);
                }));
            }
        }

        protected virtual void PostEditor()
        {
            tableView.PostEditor();
        }

        protected virtual void ForceGridRefresh()
        {
            gridControl.RefreshData();
        }

        protected virtual void StoreFocusedCell()
        {
            this.focusedRowHandle = tableView.FocusedRowHandle;
            this.currentColumn = gridControl.CurrentColumn;
            this.onBeforeRefreshIsActive = tableView.IsEditing;
        }

        protected virtual void RestoreFocusedCell()
        {
            gridControl.CurrentColumn = this.currentColumn;
            tableView.FocusedRowHandle = focusedRowHandle;
            gridControl.Focus();
            //Allows for previous value to be restored 
            //Because active editor have latest value but cannot revert to old value when esc is pressed
            //GridColumn setValueColumn = gridControl.Columns[gridControl.CurrentColumn.FieldName];
            //gridControl.SetFocusedRowCellValue(setValueColumn, currentValue);

            if (this.onBeforeRefreshIsActive && !tableView.IsEditing)
                tableView.ShowEditor();
        }
    }
}
