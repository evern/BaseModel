using BaseModel.View;
using BaseModel.ViewModel.UndoRedo;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseModel.Misc
{
    public static class GridControlHelpers
    {
        public static void PasteCellData(GridControl gridControl, TableView gridTableView, string[] RowData, Func<DataRow, ColumnBase, string, bool, bool> basePasteDataAction, bool showLoadingScreen = false)
        {
            var selected_cells = gridTableView.GetSelectedCells();
            if (selected_cells.Count == 0)
            {
                selected_cells = Enumerable.Range(0, gridControl.VisibleRowCount)
                .Select(x => (GridControl)gridControl.GetDetail(x))
                .Where(x => x != null).
                SelectMany(x => ((TableView)(x).View).GetSelectedCells()).ToList();

                if (selected_cells.Count == 0)
                    return;
                else
                {
                    gridTableView = (TableView)selected_cells.First().Column.View;
                    gridControl = gridTableView.Grid;
                }
            }

            List<List<string>> row_data = new List<List<string>>();
            foreach (var row in RowData)
            {
                string formatRow = row;
                //remove tab in front
                if (row.Substring(0, 1) == "\t")
                {
                    formatRow = row.Substring(1, row.Length - 1);
                }

                List<string> column_data = formatRow.Split('\t').ToList();
                row_data.Add(column_data);
            }

            var grouped_results = row_data
            .SelectMany(inner => inner.Select((item, index) => new { item, index }))
            .GroupBy(i => i.index, i => i.item)
            .Select(g => g.ToList())
            .ToList();

            var selected_cells_groupby_columns = selected_cells.GroupBy(x => x.Column.FieldName).Select(group => new { FieldName = group.Key, Cells = group.ToList() });
            if (grouped_results.Count == 0)
            {
                if (showLoadingScreen)
                    LoadingScreenManager.ShowLoadingScreen(selected_cells.Count);

                foreach (var selected_cell in selected_cells)
                {
                    int row_handle = selected_cell.RowHandle;
                    DataRowView editing_row_view = (DataRowView)gridControl.GetRow(row_handle);
                    DataRow editing_row = editing_row_view.Row;
                    basePasteDataAction?.Invoke(editing_row, selected_cell.Column, string.Empty);

                    if (showLoadingScreen)
                        LoadingScreenManager.Progress();
                }

                if (showLoadingScreen)
                    LoadingScreenManager.CloseLoadingScreen();
            }
            else
            {
                GridCell first_selected_cell = selected_cells.First();
                GridCell last_selected_cell = selected_cells.Last();

                int first_row_handle = selected_cells.Min(x => x.RowHandle);
                int last_row_handle = selected_cells.Max(x => x.RowHandle);
                int first_row_visible_index = gridControl.GetRowVisibleIndexByHandle(first_row_handle);
                int last_row_visible_index = gridControl.GetRowVisibleIndexByHandle(last_row_handle);
                int numberOfSelectedRows = (last_row_visible_index - first_row_visible_index) + 1;
                int numberOfCopiedRows = grouped_results.First().Count;

                List<GridColumn> visible_columns = gridTableView.VisibleColumns.ToList();
                int first_column_visible_index = visible_columns.First(x => x.FieldName == first_selected_cell.Column.FieldName).VisibleIndex;
                int last_column_visible_index = visible_columns.First(x => x.FieldName == last_selected_cell.Column.FieldName).VisibleIndex;

                int numberOfSelectedColumns = (last_column_visible_index - first_column_visible_index) + 1;
                int numberOfCopiedColumns = grouped_results.Count;

                //commented out because not accurate during banded view
                //int first_column_visible_index = first_selected_cell.Column.VisibleIndex;

                int rowOffsetSelection = numberOfSelectedRows > numberOfCopiedRows ? numberOfSelectedRows : numberOfCopiedRows;
                int columnOffsetSelection = numberOfSelectedColumns > numberOfCopiedColumns ? numberOfSelectedColumns : numberOfCopiedColumns;

                int pasteValueRowOffset = 0;
                if (showLoadingScreen)
                    LoadingScreenManager.ShowLoadingScreen(rowOffsetSelection);

                for (int rowOffset = 0; rowOffset < rowOffsetSelection; rowOffset++)
                {
                    int pasteValueColumnOffset = 0;
                    for (int columnOffset = 0; columnOffset < columnOffsetSelection; columnOffset++)
                    {
                        if (!visible_columns.Any(x => x.VisibleIndex == (first_column_visible_index + columnOffset)))
                            continue;

                        GridColumn current_column = visible_columns.First(x => x.VisibleIndex == (first_column_visible_index + columnOffset));
                        string columnValue = grouped_results[pasteValueColumnOffset][pasteValueRowOffset];

                        int current_row_visible_index = first_row_visible_index + rowOffset;
                        int current_row_handle = gridControl.GetRowHandleByVisibleIndex(current_row_visible_index);

                        object rowObject = gridControl.GetRow(current_row_handle);
                        if (rowObject == null)
                            continue;

                        DataRowView editing_row_view = (DataRowView)rowObject;
                        DataRow editing_row = editing_row_view.Row;

                        pasteValueColumnOffset += 1;
                        if (pasteValueColumnOffset >= grouped_results.Count)
                            pasteValueColumnOffset = 0;

                        basePasteDataAction?.Invoke(editing_row, current_column, columnValue, columnOffset == columnOffsetSelection - 1);

                        if (showLoadingScreen)
                            LoadingScreenManager.Progress();
                    }

                    if (showLoadingScreen)
                        LoadingScreenManager.CloseLoadingScreen();

                    pasteValueRowOffset += 1;
                    if (pasteValueRowOffset >= grouped_results[pasteValueColumnOffset].Count)
                        pasteValueRowOffset = 0;
                }
            }
        }
    }
}
