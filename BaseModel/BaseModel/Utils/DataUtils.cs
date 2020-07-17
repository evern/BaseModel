using BaseModel.Attributes;
using BaseModel.Helpers;
using BaseModel.Misc;
using BaseModel.View;
using BaseModel.ViewModel.UndoRedo;
using DevExpress.Mvvm;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Editors.Settings;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Grid.LookUp;
using DevExpress.Xpf.Grid.TreeList;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace BaseModel.Data.Helpers
{
    public class UndoRedoArg<TProjection>
    {
        public TProjection Projection { get; set; }
        public string FieldName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
    }

    public class CopyPasteHelper<TProjection>
        where TProjection : class, new()
    {
        public delegate bool IsValidProjectionFunc(TProjection projection, IEnumerable<TProjection> preCommittedProjections, ref string errorMessage, out List<KeyValuePair<string, string>> constraintIssues);
        readonly IsValidProjectionFunc isValidProjectionFunc;
        readonly Func<TProjection, bool> onBeforePasteWithValidationFunc;
        readonly IDialogService errorMessagesDialogService;
        readonly Func<TProjection, string, object, bool, string> unifiedValueValidationCallback;
        readonly Func<List<KeyValuePair<ColumnBase, string>>, TProjection, bool, bool> funcManualRowPastingIsContinue;
        readonly Func<TProjection, ColumnBase, string, List<UndoRedoArg<TProjection>>, bool> funcManualCellPastingIsContinue;
        readonly Action<IEnumerable<ErrorMessage>> formatErrorMessages;
        public Action<string, object, object, TProjection, bool> cellValueChanging;
        public Action<string, object, object, TProjection, bool> cellValueChanged;
        public Action<TProjection> newRowInitialization;
        public CopyPasteHelper(IsValidProjectionFunc isValidProjectionFunc = null, Func<TProjection, bool> onBeforePasteWithValidationFunc = null, IDialogService errorMessagesDialogService = null, Func<TProjection, string, object, bool, string> unifiedValueValidationCallback = null, Func<TProjection, ColumnBase, string, List<UndoRedoArg<TProjection>>, bool> funcManualCellPastingIsContinue = null, Func<List<KeyValuePair<ColumnBase, string>>, TProjection, bool, bool> funcManualRowPastingIsContinue = null, Action<string, object, object, TProjection, bool> cellValueChanging = null, Action<string, object, object, TProjection, bool> cellValueChanged = null, Action<TProjection> newRowInitialization = null, Action<IEnumerable<ErrorMessage>> formatErrorMessages = null)
        {
            this.isValidProjectionFunc = isValidProjectionFunc;
            this.onBeforePasteWithValidationFunc = onBeforePasteWithValidationFunc;
            this.errorMessagesDialogService = errorMessagesDialogService;
            this.unifiedValueValidationCallback = unifiedValueValidationCallback;
            this.funcManualRowPastingIsContinue = funcManualRowPastingIsContinue;
            this.funcManualCellPastingIsContinue = funcManualCellPastingIsContinue;
            this.cellValueChanging = cellValueChanging;
            this.cellValueChanged = cellValueChanged;
            this.newRowInitialization = newRowInitialization;
            this.formatErrorMessages = formatErrorMessages;
        }

        public enum PasteResult
        {
            Success, 
            Skip, 
            Failed, 
            FailOnRequired
        }

        public List<TProjection> PastingFromClipboardCellLevel<TView>(GridControl gridControl, string[] RowData, EntitiesUndoRedoManager<TProjection> undo_redo_manager, out List<ErrorMessage> errorMessages)
            where TView : DataViewBase
        {
            var gridView = gridControl.View;

            errorMessages = new List<ErrorMessage>();
            HashSet<TProjection> preValidatedProjections = new HashSet<TProjection>();
            List<TProjection> validatedProjections = new List<TProjection>();
            List<UndoRedoArg<TProjection>> undoRedoArguments = new List<UndoRedoArg<TProjection>>();
            if (gridView.ActiveEditor == null && (gridView.GetType() == typeof(TView)))
            {
                var gridTView = gridView as TView;
                TableView gridTableView = gridTView as TableView;
                TreeListView gridTreeListView = gridTView as TreeListView;

                List<List<string>> row_data = new List<List<string>>();
                foreach (var row in RowData)
                {
                    List<string> column_data = row.Split('\t').ToList();
                    row_data.Add(column_data);
                }

                var grouped_results = row_data
                    .SelectMany(inner => inner.Select((item, index) => new { item, index }))
                    .GroupBy(i => i.index, i => i.item)
                    .Select(g => g.ToList())
                    .ToList();

                var selected_cells = gridTableView.GetSelectedCells();
                if (selected_cells.Count == 0)
                    return validatedProjections;

                var selected_cells_groupby_columns = selected_cells.GroupBy(x => x.Column.FieldName).Select(group => new { FieldName = group.Key, Cells = group.ToList() });
                if (grouped_results.Count == 0)
                {
                    foreach(var selected_cell in selected_cells)
                    {
                        int row_handle = selected_cell.RowHandle;
                        TProjection editing_row = (TProjection)gridControl.GetRow(row_handle);
                        string errorMessage = string.Empty;
                        PasteResult result = pasteDataInProjectionColumn(editing_row, selected_cell.Column, string.Empty, out errorMessage, undoRedoArguments);
                        if (result == PasteResult.FailOnRequired || errorMessage != string.Empty)
                        {
                            string errorString = errorMessage == string.Empty ? "Cannot set null in required cell, operation has been terminated" : errorMessage;
                            errorMessages.Add(new ErrorMessage(selected_cell.Column.Header.ToString(), errorString));
                            break;
                        }
                        if (result != PasteResult.Success)
                            continue;

                        if (!preValidatedProjections.Any(x => x.GetHashCode() == editing_row.GetHashCode()))
                            preValidatedProjections.Add(editing_row);
                    }
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
                    //commented out because not accurate during banded view
                    //int first_column_visible_index = first_selected_cell.Column.VisibleIndex;
                    int first_column_visible_index = visible_columns.IndexOf(visible_columns.First(x => x.FieldName == first_selected_cell.Column.FieldName));
                    int last_column_visible_index = visible_columns.IndexOf(visible_columns.First(x => x.FieldName == last_selected_cell.Column.FieldName));

                    int numberOfSelectedColumns = (last_column_visible_index - first_column_visible_index) + 1;
                    int numberOfCopiedColumns = grouped_results.Count;

                    //commented out because not accurate during banded view
                    //int first_column_visible_index = first_selected_cell.Column.VisibleIndex;

                    int rowOffsetSelection = numberOfSelectedRows > numberOfCopiedRows ? numberOfSelectedRows : numberOfCopiedRows;
                    int columnOffsetSelection = numberOfSelectedColumns > numberOfCopiedColumns ? numberOfSelectedColumns : numberOfCopiedColumns;

                    int pasteValueRowOffset = 0;
                    TProjection validate_row = null;
                    for (int rowOffset = 0; rowOffset < rowOffsetSelection; rowOffset++)
                    {
                        int pasteValueColumnOffset = 0;
                        for (int columnOffset = 0; columnOffset < columnOffsetSelection; columnOffset++)
                        {
                            int findVisibleIndex = first_column_visible_index + columnOffset;
                            if (findVisibleIndex >= visible_columns.Count)
                                continue;

                            GridColumn current_column = visible_columns[findVisibleIndex];
                            string columnValue = grouped_results[pasteValueColumnOffset][pasteValueRowOffset];

                            pasteValueColumnOffset += 1;
                            if (pasteValueColumnOffset >= grouped_results.Count)
                                pasteValueColumnOffset = 0;

                            int current_row_visible_index = first_row_visible_index + rowOffset;
                            int current_row_handle = gridControl.GetRowHandleByVisibleIndex(current_row_visible_index);

                            object rowObject = gridControl.GetRow(current_row_handle);
                            if (rowObject == null)
                                continue;

                            TProjection editing_row = (TProjection)gridControl.GetRow(current_row_handle);
                            validate_row = editing_row;
                            if (editing_row == null)
                            {
                                errorMessages.Add(new ErrorMessage(current_column.Header.ToString(), "Please remove all line break from paste data or double click into cell to paste your data with line breaks"));
                                break;
                            }

                            string errorMessage = string.Empty;
                            PasteResult result = pasteDataInProjectionColumn(editing_row, current_column, columnValue, out errorMessage, undoRedoArguments);
                            if (result == PasteResult.FailOnRequired || errorMessage != string.Empty)
                            {
                                string errorString = errorMessage == string.Empty ? "Cannot set null in required cell, operation has been terminated" : errorMessage;
                                errorMessages.Add(new ErrorMessage(current_column.Header.ToString(), errorString));
                                break;
                            }
                            if (result != PasteResult.Success)
                                continue;

                        }

                        if(validate_row != null)
                            preValidatedProjections.Add(validate_row);

                        pasteValueRowOffset += 1;
                        if (pasteValueRowOffset >= grouped_results[pasteValueColumnOffset].Count)
                            pasteValueRowOffset = 0;
                    }
                }
                
                undo_redo_manager.PauseActionId();
                foreach (TProjection preValidatedProjection in preValidatedProjections)
                {
                    string errorMessage = string.Empty;
                    List<KeyValuePair<string, string>> constraintIssues;
                    if (isValidProjectionFunc(preValidatedProjection, validatedProjections, ref errorMessage, out constraintIssues))
                        if (onBeforePasteWithValidationFunc != null)
                        {
                            if (onBeforePasteWithValidationFunc(preValidatedProjection))
                            {
                                IEnumerable<UndoRedoArg<TProjection>> projection_undo_redos = undoRedoArguments.Where(x => x.Projection == preValidatedProjection);
                                foreach (UndoRedoArg<TProjection> projection_undo_redo in projection_undo_redos)
                                {
                                    cellValueChanging?.Invoke(projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, projection_undo_redo.Projection, false);
                                    cellValueChanged?.Invoke(projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, projection_undo_redo.Projection, false);

                                    undo_redo_manager.AddUndo(projection_undo_redo.Projection, projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, EntityMessageType.Changed);
                                }

                                validatedProjections.Add(preValidatedProjection);
                            }
                        }
                        else
                        {
                            IEnumerable<UndoRedoArg<TProjection>> projection_undo_redos = undoRedoArguments.Where(x => x.Projection == preValidatedProjection);
                            foreach (UndoRedoArg<TProjection> projection_undo_redo in projection_undo_redos)
                            {
                                cellValueChanging?.Invoke(projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, projection_undo_redo.Projection, false);
                                cellValueChanged?.Invoke(projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, projection_undo_redo.Projection, false);

                                undo_redo_manager.AddUndo(projection_undo_redo.Projection, projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, EntityMessageType.Changed);
                            }

                            validatedProjections.Add(preValidatedProjection);
                        }
                    else
                    {
                        formatErrorMessages?.Invoke(errorMessages);
                        errorMessages.Add(new ErrorMessage("Cell edit error", errorMessage, constraintIssues));
                    }
                }
            }

            undo_redo_manager.UnpauseActionId();
            return validatedProjections;
        }

        public List<TProjection> PastingFromClipboardTreeListCellLevel<TView>(GridControl gridControl, string[] RowData, EntitiesUndoRedoManager<TProjection> undo_redo_manager, out List<ErrorMessage> errorMessages)
            where TView : DataViewBase
        {
            var gridView = gridControl.View;
            errorMessages = new List<ErrorMessage>();
            HashSet<TProjection> preValidatedProjections = new HashSet<TProjection>();
            List<TProjection> validatedProjections = new List<TProjection>();
            List<UndoRedoArg<TProjection>> undoRedoArguments = new List<UndoRedoArg<TProjection>>();
            if (gridView.ActiveEditor == null && (gridView.GetType() == typeof(TView)))
            {
                var gridTView = gridView as TView;
                TreeListView gridTreeListView = gridTView as TreeListView;

                List<List<string>> row_data = new List<List<string>>();
                foreach (var row in RowData)
                {
                    List<string> column_data = row.Split('\t').ToList();
                    row_data.Add(column_data);
                }

                var grouped_results = row_data
                    .SelectMany(inner => inner.Select((item, index) => new { item, index }))
                    .GroupBy(i => i.index, i => i.item)
                    .Select(g => g.ToList())
                    .ToList();

                var selected_cells = gridTreeListView.GetSelectedCells();
                if (selected_cells.Count == 0)
                    return validatedProjections;

                var selected_cells_groupby_columns = selected_cells.GroupBy(x => x.Column.FieldName).Select(group => new { FieldName = group.Key, Cells = group.ToList() });
                if (grouped_results.Count == 0)
                {
                    foreach (var selected_cell in selected_cells)
                    {
                        int row_handle = selected_cell.RowHandle;
                        TProjection editing_row = (TProjection)gridControl.GetRow(row_handle);
                        string errorMessage = string.Empty;
                        PasteResult result = pasteDataInProjectionColumn(editing_row, selected_cell.Column, string.Empty, out errorMessage, undoRedoArguments);
                        
                        if(errorMessage != string.Empty)
                            errorMessages.Add(new ErrorMessage(selected_cell.Column.Header.ToString(), errorMessage));

                        if (!preValidatedProjections.Any(x => x.GetHashCode() == editing_row.GetHashCode()))
                            preValidatedProjections.Add(editing_row);
                    }
                }
                //if copied only a row
                else if (grouped_results.All(x => x.Count == 1) && (grouped_results.Count == 1 || (selected_cells_groupby_columns.Count() == grouped_results.Count)))
                {
                    int column_offset = 0;
                    foreach (var selected_column in selected_cells_groupby_columns)
                    {
                        int validated_column_offset = column_offset > (grouped_results.Count - 1) ? grouped_results.Count - 1 : column_offset;
                        var paste_value = grouped_results[validated_column_offset];
                        column_offset += 1;
                        //since we've already verified that each column group only has a row
                        string paste_data = paste_value.First();
                        List<ColumnBase> visible_columns = gridTreeListView.VisibleColumns.ToList();

                        foreach (var selected_cell in selected_column.Cells)
                        {
                            int column_visible_index = selected_cell.Column.VisibleIndex;
                            int row_handle = selected_cell.RowHandle;
                            ColumnBase current_column = visible_columns[column_visible_index];
                            TProjection editing_row = (TProjection)gridControl.GetRow(row_handle);

                            string errorMessage = string.Empty;
                            PasteResult result = pasteDataInProjectionColumn(editing_row, current_column, paste_data, out errorMessage, undoRedoArguments);

                            if (result == PasteResult.FailOnRequired || errorMessage != string.Empty)
                            {
                                string errorString = errorMessage == string.Empty ? "Cannot set null in required cell, operation has been terminated" : errorMessage;
                                errorMessages.Add(new ErrorMessage(current_column.Header.ToString(), errorString));
                                break;
                            }
                            if (result != PasteResult.Success)
                                continue;

                            if (!preValidatedProjections.Any(x => x.GetHashCode() == editing_row.GetHashCode()))
                                preValidatedProjections.Add(editing_row);
                        }
                    }
                }
                else
                {
                    TreeListCell first_selected_cell = selected_cells.First();
                    int first_row_handle = first_selected_cell.RowHandle;
                    int first_row_visible_index = gridControl.GetRowVisibleIndexByHandle(first_row_handle);
                    int first_column_visible_index = first_selected_cell.Column.VisibleIndex;
                    List<ColumnBase> visible_columns = gridTreeListView.VisibleColumns.ToList();

                    for (int i = 0; i < grouped_results.Count; i++)
                    {
                        ColumnBase current_column = visible_columns[first_column_visible_index + i];
                        string column_name = current_column.FieldName;
                        int row_visible_index_offset = 0;

                        foreach (string rowValue in grouped_results[i])
                        {
                            int current_row_visible_index = first_row_visible_index + row_visible_index_offset;
                            int current_row_handle = gridControl.GetRowHandleByVisibleIndex(current_row_visible_index);
                            TProjection editing_row = (TProjection)gridControl.GetRow(current_row_handle);
                            row_visible_index_offset += 1;

                            string errorMessage = string.Empty;
                            PasteResult result = pasteDataInProjectionColumn(editing_row, current_column, rowValue, out errorMessage, undoRedoArguments);
                            if (result == PasteResult.FailOnRequired || errorMessage != string.Empty)
                            {
                                string errorString = errorMessage == string.Empty ? "Cannot set null in required cell, operation has been terminated" : errorMessage;
                                errorMessages.Add(new ErrorMessage(current_column.Header.ToString(), errorString));
                                break;
                            }
                            if (result != PasteResult.Success)
                                continue;

                            //only add once
                            if (i == 0)
                                preValidatedProjections.Add(editing_row);
                        }
                    }
                }

                undo_redo_manager.PauseActionId();
                foreach (TProjection preValidatedProjection in preValidatedProjections)
                {
                    string errorMessage = string.Empty;
                    List<KeyValuePair<string, string>> constraintIssues;
                    if (isValidProjectionFunc(preValidatedProjection, validatedProjections, ref errorMessage, out constraintIssues))
                        if (onBeforePasteWithValidationFunc != null)
                        {
                            if (onBeforePasteWithValidationFunc(preValidatedProjection))
                            {
                                IEnumerable<UndoRedoArg<TProjection>> projection_undo_redos = undoRedoArguments.Where(x => x.Projection == preValidatedProjection);
                                foreach (UndoRedoArg<TProjection> projection_undo_redo in projection_undo_redos)
                                    undo_redo_manager.AddUndo(projection_undo_redo.Projection, projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, EntityMessageType.Changed);

                                validatedProjections.Add(preValidatedProjection);
                            }
                        }
                        else
                        {
                            IEnumerable<UndoRedoArg<TProjection>> projection_undo_redos = undoRedoArguments.Where(x => x.Projection == preValidatedProjection);
                            foreach (UndoRedoArg<TProjection> projection_undo_redo in projection_undo_redos)
                                undo_redo_manager.AddUndo(projection_undo_redo.Projection, projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, EntityMessageType.Changed);

                            validatedProjections.Add(preValidatedProjection);
                        }
                    else
                    {
                        formatErrorMessages?.Invoke(errorMessages);
                        errorMessages.Add(new ErrorMessage("Row add error", errorMessage, constraintIssues));
                    }
                }

            }

            undo_redo_manager.UnpauseActionId();
            return validatedProjections;
        }

        public List<TProjection> PastingFromClipboard<TView>(GridControl gridControl, string[] RowData, out List<ErrorMessage> errorMessages)
            where TView : DataViewBase
        {
            var gridView = gridControl.View;
            errorMessages = new List<ErrorMessage>();
            List<TProjection> pasteProjections = new List<TProjection>();
            List<TProjection> validatedProjections = new List<TProjection>();
            if (gridView.ActiveEditor == null && (gridView.GetType() == typeof(TView)) && !ShouldSkipPasting(gridControl))
            {
                var gridTView = gridView as TView;
                TableView gridTableView = gridTView as TableView;
                TreeListView gridTreeListView = gridTView as TreeListView;

                PasteResult result = PasteResult.Success;
                foreach (var Row in RowData)
                {
                    TProjection projection = new TProjection();
                    newRowInitialization?.Invoke(projection);
                    List<KeyValuePair<ColumnBase, string>> columnData = new List<KeyValuePair<ColumnBase, string>>();
                    var ColumnStrings = Row.Split('\t');
                    for (var i = 0; i < ColumnStrings.Count(); i++)
                    {
                        if (i > gridTableView.VisibleColumns.Count - 1)
                            continue;

                        ColumnBase copyColumn = gridTableView != null ? gridTableView.VisibleColumns[i] : gridTreeListView.VisibleColumns[i];
                        string errorMessage = string.Empty;
                        result = pasteDataInProjectionColumn(projection, copyColumn, ColumnStrings[i], out errorMessage);

                        if(errorMessage != string.Empty)
                            errorMessages.Add(new ErrorMessage(gridTableView.VisibleColumns[i].Header.ToString(), errorMessage));

                        //When column has gone through unifiedCellValidation and have error
                        if (result == PasteResult.Failed)
                            break;

                        columnData.Add(new KeyValuePair<ColumnBase, string>(copyColumn, ColumnStrings[i]));
                    }

                    if (funcManualRowPastingIsContinue != null)
                    {
                        if (!funcManualRowPastingIsContinue.Invoke(columnData, projection, Row == RowData.Last()))
                            continue;
                    }

                    if (result != PasteResult.Failed)
                    {
                        pasteProjections.Add(projection);
                    }
                }

                foreach (TProjection projection in pasteProjections)
                {
                    string errorMessage = string.Empty;
                    List<KeyValuePair<string, string>> constraintIssues;

                    if (isValidProjectionFunc(projection, validatedProjections, ref errorMessage, out constraintIssues))
                        if (onBeforePasteWithValidationFunc != null)
                        {
                            if (onBeforePasteWithValidationFunc(projection))
                                validatedProjections.Add(projection);
                        }
                        else
                            validatedProjections.Add(projection);
                    else
                    {
                        formatErrorMessages?.Invoke(errorMessages);
                        errorMessages.Add(new ErrorMessage("Row error", errorMessage, constraintIssues));
                    }
                }
            }

            return validatedProjections;
        }

        private bool ShouldSkipPasting(GridControl gridControl)
        {
            //DataControlDetailDescriptor detailDescriptor = gridControl.DetailDescriptor as DataControlDetailDescriptor;
            //if (detailDescriptor != null)
            //{
            //    return true;
            //    //GridControl grid_control = detailDescriptor.DataControl as GridControl;
            //    //if (grid_control != null)
            //    //{
            //    //    TableView tableView = grid_control.View as TableView;
            //    //    if (tableView != null)
            //    //    {
            //    //        if (tableView.ActiveEditor != null)
            //    //            return true;
            //    //        else
            //    //            return ShouldSkipPasting(grid_control);
            //    //    }
            //    //    else
            //    //        //always skip pasting of detail descriptor exists
            //    //        return true;
            //    //}
            //    //else
            //    //    return false;
            //}
            //else
                return false;
        }

        public PasteResult pasteDataInProjectionColumn(TProjection projection, ColumnBase column, string pasteData, out string errorMessage, List<UndoRedoArg<TProjection>> undoRedoArguments = null)
        {
            errorMessage = string.Empty;
            if (column.ReadOnly)
                return PasteResult.Skip;

            string column_name = column.FieldName;
            pasteData = pasteData.Trim();

            try
            {
                PropertyInfo columnPropertyInfo = DataUtils.GetNestedPropertyInfo(column_name, projection);
                if (columnPropertyInfo != null)
                {
                    if(funcManualCellPastingIsContinue == null || (funcManualCellPastingIsContinue != null && funcManualCellPastingIsContinue.Invoke(projection, column, pasteData, undoRedoArguments)))
                    {
                        if (pasteData == string.Empty && Nullable.GetUnderlyingType(columnPropertyInfo.PropertyType) != null)
                        {
                            if (Attribute.IsDefined(columnPropertyInfo, typeof(RequiredAttribute)))
                                return PasteResult.FailOnRequired;

                            return trySetValueInProjection(projection, column_name, null, out errorMessage, undoRedoArguments);
                        }
                        else if (columnPropertyInfo.PropertyType == typeof(Guid?) || columnPropertyInfo.PropertyType == typeof(Guid))
                        {
                            object cellTemplate = column.CellTemplate;
                            DataTemplate dataTemplate = cellTemplate as DataTemplate;
                            Type editSettingsType = null;
                            //try to retrieve data template type for RowData.Row items source binding
                            editSettingsType = column.ActualEditSettings.GetType();

                            object editSettings = null;
                            //if (dataTemplate.HasContent)
                            //{
                            //    editSettings = dataTemplate.LoadContent() as ComboBoxEdit;
                            //    editSettings = FindVisualChild<ComboBoxEdit>(dataTemplate.LoadContent());
                            //}
                            if (editSettingsType == typeof(ComboBoxEditSettings))
                                editSettings = column.ActualEditSettings as ComboBoxEditSettings;
                            else if (editSettingsType == typeof(LookUpEditSettings))
                                editSettings = column.ActualEditSettings as LookUpEditSettingsBase;

                            if (editSettings != null)
                            {
                                Guid? guid_value = getEditSettingsValueMemberValue<Guid?>(editSettings, pasteData);
                                if (guid_value != null)
                                    return trySetValueInProjection(projection, column_name, guid_value, out errorMessage, undoRedoArguments);
                                else
                                    return PasteResult.Skip;
                            }
                            //lookupedit under datatemplate are detected is texteditsettings
                            else if ((editSettings != null || column.ActualEditSettings.GetType() == typeof(TextEditSettings)) && pasteData != Guid.Empty.ToString())
                            {
                                Guid new_guid = new Guid(pasteData);
                                return trySetValueInProjection(projection, column_name, new_guid, out errorMessage, undoRedoArguments);
                            }
                        }
                        else if (columnPropertyInfo.PropertyType == typeof(string))
                        {
                            Type editSettingsType = null;
                            //try to retrieve data template type for RowData.Row items source binding
                            editSettingsType = column.ActualEditSettings.GetType();
                            object editSettings = null;
                            if (editSettingsType == typeof(ComboBoxEditSettings))
                                editSettings = column.ActualEditSettings as ComboBoxEditSettings;
                            else if (editSettingsType == typeof(LookUpEditSettings))
                                editSettings = column.ActualEditSettings as LookUpEditSettingsBase;

                            string new_string = pasteData.ToString();
                            if (editSettings != null)
                            {
                                if ((string)editSettings.GetType().GetProperty("ValueMember").GetValue(editSettings) != string.Empty)
                                    new_string = getEditSettingsValueMemberValue<string>(editSettings, pasteData);
                            }
                            else if (Attribute.IsDefined(columnPropertyInfo, typeof(PasteSkipAttribute)) && pasteData.ToUpper() == DataUtils.GetPasteSkipAttributeString(typeof(TProjection)))
                                return PasteResult.Skip;

                            if (new_string == string.Empty)
                            {
                                if (Attribute.IsDefined(columnPropertyInfo, typeof(RequiredAttribute)))
                                    return PasteResult.FailOnRequired;
                            }

                            return trySetValueInProjection(projection, column_name, new_string, out errorMessage, undoRedoArguments);
                        }
                        else if (columnPropertyInfo.PropertyType.BaseType == typeof(Enum))
                        {
                            var enumValues = Enum.GetValues(columnPropertyInfo.PropertyType);
                            foreach (var enum_value in enumValues)
                            {
                                var fieldInfo = enum_value.GetType().GetField(enum_value.ToString());
                                if (fieldInfo == null)
                                    return PasteResult.Skip;

                                var descriptionAttributes = fieldInfo.GetCustomAttributes(typeof(DisplayAttribute), false) as DisplayAttribute[];
                                if (descriptionAttributes == null || descriptionAttributes.Count() == 0)
                                    return PasteResult.Skip;

                                var descriptionAttribute = descriptionAttributes.First();
                                if (pasteData == descriptionAttribute.Name)
                                    return trySetValueInProjection(projection, column_name, enum_value, out errorMessage, undoRedoArguments);
                            }
                        }
                        else if (columnPropertyInfo.PropertyType == typeof(decimal) || columnPropertyInfo.PropertyType == typeof(decimal?) || columnPropertyInfo.PropertyType == typeof(int) || columnPropertyInfo.PropertyType == typeof(int?) || columnPropertyInfo.PropertyType == typeof(double) || columnPropertyInfo.PropertyType == typeof(double?))
                        {
                            var rgx = new Regex("[A-Za-z\\.\\-]");
                            var cleanColumnString = rgx.Replace(pasteData, string.Empty);

                            if (columnPropertyInfo.PropertyType == typeof(decimal) ||
                                columnPropertyInfo.PropertyType == typeof(decimal?))
                            {
                                decimal decimal_value;
                                if (decimal.TryParse(cleanColumnString, out decimal_value))
                                {
                                    if (column_name.Contains('%') || column_name.ToUpper().Contains("PERCENT"))
                                    {
                                        if (decimal_value > 1)
                                            decimal_value /= 100;
                                        //else when user copy from grid and paste it will be the actual value
                                    }

                                    return trySetValueInProjection(projection, column_name, decimal_value, out errorMessage, undoRedoArguments);
                                }
                                else
                                    return PasteResult.Skip;
                            }
                            else if (columnPropertyInfo.PropertyType == typeof(int) || columnPropertyInfo.PropertyType == typeof(int?))
                            {
                                ComboBoxEditSettings editSettings = column.ActualEditSettings as ComboBoxEditSettings;
                                if (editSettings != null)
                                {
                                    int? int_value = getEditSettingsValueMemberValue<int?>(editSettings, pasteData);
                                    if (int_value != null)
                                        return trySetValueInProjection(projection, column_name, int_value, out errorMessage, undoRedoArguments);
                                    else
                                        return PasteResult.Skip;
                                }
                                else
                                {
                                    int int_value;
                                    if (int.TryParse(cleanColumnString, out int_value))
                                        return trySetValueInProjection(projection, column_name, int_value, out errorMessage, undoRedoArguments);
                                    else
                                        return PasteResult.Skip;
                                }
                            }
                            else if (columnPropertyInfo.PropertyType == typeof(double) ||
                                     columnPropertyInfo.PropertyType == typeof(double?))
                            {
                                double double_value;
                                if (double.TryParse(cleanColumnString, out double_value))
                                {
                                    if (column_name.Contains('%') || column_name.ToUpper().Contains("PERCENT"))
                                        double_value /= 100;

                                    return trySetValueInProjection(projection, column_name, double_value, out errorMessage, undoRedoArguments);
                                }
                                else
                                    return PasteResult.Skip;
                            }
                            else
                                return PasteResult.Skip;
                        }
                        else if (columnPropertyInfo.PropertyType == typeof(DateTime?) || columnPropertyInfo.PropertyType == typeof(DateTime))
                        {
                            DateTime datetime_value;
                            if (DateTime.TryParse(pasteData, out datetime_value))
                                return trySetValueInProjection(projection, column_name, datetime_value, out errorMessage, undoRedoArguments);
                            else
                                return PasteResult.Skip;
                        }
                        else if (column.ActualEditSettings is ComboBoxEditSettings)
                        {
                            ComboBoxEditSettings editSettings = column.ActualEditSettings as ComboBoxEditSettings;
                            CheckedComboBoxStyleSettings checkedComboBoxStyleSettings = editSettings.StyleSettings as CheckedComboBoxStyleSettings;

                            if (checkedComboBoxStyleSettings != null)
                            {
                                var copyColumnDisplayMember = (string)editSettings.GetType().GetProperty("DisplayMember").GetValue(editSettings);
                                var copyColumnItemsSource = (IEnumerable<object>)editSettings.GetType().GetProperty("ItemsSource").GetValue(editSettings);
                                var copyColumnTag = (IEnumerable<object>)editSettings.GetType().GetProperty("Tag").GetValue(editSettings);
                                if (copyColumnTag != null && copyColumnTag.ToString().ToUpper() == "COPYPASTESKIP")
                                    return PasteResult.Skip;

                                string[] pasteStringArray = pasteData.Split(';');
                                List<object> setValues = new List<object>();
                                foreach (string pasteString in pasteStringArray)
                                {
                                    foreach (var copyColumnItem in copyColumnItemsSource)
                                    {
                                        var itemDisplayMemberPropertyInfo = copyColumnItem.GetType().GetProperty(copyColumnDisplayMember);
                                        if (itemDisplayMemberPropertyInfo.GetValue(copyColumnItem).ToString().ToUpper() == pasteString.ToUpper())
                                        {
                                            setValues.Add(copyColumnItem);
                                        }
                                    }
                                }

                                if (setValues.Count > 0)
                                    return trySetValueInProjection(projection, column_name, setValues, out errorMessage, undoRedoArguments);
                                else
                                    return PasteResult.Skip;
                            }
                            else
                                return PasteResult.Skip;
                        }
                        else if (column.ActualEditSettings is CheckEditSettings)
                        {
                            CheckEditSettings editSettings = column.ActualEditSettings as CheckEditSettings;
                            if (editSettings != null)
                            {
                                bool? booleanValue = pasteData.ToString().ToUpper().Contains("UNCHECKED") ? false : pasteData.ToString().ToUpper().Contains("CHECKED") ? true : (bool?)null;
                                if (booleanValue == null)
                                    return PasteResult.Skip;
                                else
                                    return trySetValueInProjection(projection, column_name, (bool)booleanValue, out errorMessage, undoRedoArguments);
                            }
                        }
                    }
                    else
                        return PasteResult.Skip;
                }
                else
                    return PasteResult.Skip;
            }
            catch
            {
                return PasteResult.Skip;
            }

            return PasteResult.Success;
        }

        private T getEditSettingsValueMemberValue<T>(object editSettings, string searchData)
        {
            var copyColumnValueMember = (string)editSettings.GetType().GetProperty("ValueMember").GetValue(editSettings);
            var copyColumnDisplayMember = (string)editSettings.GetType().GetProperty("DisplayMember").GetValue(editSettings);
            var copyColumnItemsSource = (IEnumerable<object>)editSettings.GetType().GetProperty("ItemsSource").GetValue(editSettings);

            T editValue = default(T);

            if (copyColumnItemsSource == null || (copyColumnValueMember == null || copyColumnValueMember == string.Empty) || (copyColumnDisplayMember == null || copyColumnDisplayMember == string.Empty))
            {
                return editValue;
            }

            foreach (var copyColumnItem in copyColumnItemsSource)
            {
                var itemDisplayMemberPropertyInfo =
                    copyColumnItem.GetType().GetProperty(copyColumnDisplayMember);
                var itemValueMemberPropertyInfo =
                    copyColumnItem.GetType().GetProperty(copyColumnValueMember);
                if (itemDisplayMemberPropertyInfo.GetValue(copyColumnItem) != null && itemDisplayMemberPropertyInfo.GetValue(copyColumnItem).ToString().ToUpper() == searchData.ToUpper())
                {
                    editValue = (T)itemValueMemberPropertyInfo.GetValue(copyColumnItem);
                    break;
                }
            }

            return editValue;
        }

        private PasteResult trySetValueInProjection(TProjection projection, string column_name, object new_value, out string error_message, List<UndoRedoArg<TProjection>> undoRedoArguments = null)
        {
            error_message = unifiedValueValidationCallback == null ? string.Empty : unifiedValueValidationCallback(projection, column_name, new_value, true);
            if (error_message == string.Empty)
            {
                object old_value = DataUtils.GetNestedValue(column_name, projection);
                if (!DataUtils.TrySetNestedValue(column_name, projection, new_value))
                    return PasteResult.Skip;
                else if (undoRedoArguments != null)
                    undoRedoArguments.Add(new UndoRedoArg<TProjection>() { FieldName = column_name, Projection = projection, OldValue = old_value, NewValue = new_value });

                return PasteResult.Success;
            }
            else
                return PasteResult.Failed;
        }
    }

    public static class MorphUtils<TFromEntity, TToEntity>
    {
        public static TToEntity ShallowCopy(TToEntity copyObject, TFromEntity objectToCopy,
            bool copyVirtualProperties = false)
        {
            var objectToCopyProperties =
                objectToCopy.GetType()
                    .GetProperties()
                    .Where(
                        p =>
                            (copyVirtualProperties == true || !p.GetGetMethod().IsVirtual) &&
                            !p.GetCustomAttributes().Any(attr => attr.GetType() == typeof(ProjectionPropertyAttribute)));
            foreach (var objectToCopyProperty in objectToCopyProperties)
            {
                if (!objectToCopyProperty.CanWrite || !objectToCopyProperty.CanRead)
                    continue;

                var objectToCopyValue = objectToCopyProperty.GetValue(objectToCopy);
                var copyObjectProperty = copyObject.GetType().GetProperty(objectToCopyProperty.Name);

                copyObjectProperty.SetValue(copyObject, objectToCopyValue);
            }

            return copyObject;
        }
    }

    public static class DataUtils
    {
        /// <summary>
        /// Account for "" in excel splitting which signifies line breaks within "" isn't new row
        /// </summary>
        public static List<string> ExcelSplit(string pasteString)
        {
            List<string> rowData = new List<string>();
            char[] charSplits = pasteString.ToCharArray();

            string rowCache = string.Empty;
            bool isQuoteOpen = false;
            for (int i = 0; i < charSplits.Count(); i++)
            {
                char? previousChar = i == 0 ? (char?)null : charSplits[i - 1];
                char currentChar = charSplits[i];

                if (currentChar == '"')
                    isQuoteOpen = !isQuoteOpen;
                else if (currentChar == '\n' && previousChar != null && ((char)previousChar) == '\r')
                {
                    if (!isQuoteOpen)
                    {
                        if (rowCache.Length > 1)
                            //remove the previous /r from row cache
                            rowCache = rowCache.Substring(0, rowCache.Length - 1);

                        rowData.Add(rowCache);
                        rowCache = string.Empty;
                    }
                    else
                        rowCache += currentChar;
                }
                else
                    rowCache += currentChar;
            }

            //when only a single row is pasted
            if (rowCache != string.Empty)
                rowData.Add(rowCache);

            return rowData;
        }

        public static bool? IsNewEntity<TEntity>(TEntity entity)
            where TEntity : IHaveCreatedDate
        {
            IHaveCreatedDate iHaveCreatedDateProjectionEntity = entity as IHaveCreatedDate;
            if (iHaveCreatedDateProjectionEntity != null)
            {
                //workaround for created because Save() only sets the projection primary key, this is used for property redo where the interceptor only tampers with UPDATED and CREATED is left as null
                if (iHaveCreatedDateProjectionEntity.EntityCreatedDate.Date.Year == 1)
                    return true;
                else
                    return false;
            }

            return null;
        }

        public static string ShallowCopyDiffTracking(object copyObject, object objectToCopy, bool copyVirtualProperties = false)
        {
            string propertyDiff = string.Empty;
            if (copyObject == null || objectToCopy == null)
                return null;

            PropertyInfo keyProperty = objectToCopy.GetType().GetProperties().FirstOrDefault(x => x.GetCustomAttributes().Any(y => y.GetType() == typeof(KeyAttribute)));
            IEnumerable<PropertyInfo> objectToCopyProperties = objectToCopy.GetType().GetProperties().Where(p => !p.GetCustomAttributes().Any(attr => attr.GetType() == typeof(ProjectionPropertyAttribute)));

            if (!copyVirtualProperties)
                objectToCopyProperties = objectToCopyProperties.Where(p => p.GetGetMethod().IsFinal || !p.GetGetMethod().IsVirtual);

            if (keyProperty != null)
            {
                PropertyInfo copyObjectKeyProperty = copyObject.GetType().GetProperties().FirstOrDefault(x => x.Name == keyProperty.Name);
                if (copyObjectKeyProperty != null)
                {
                    var keyValue = keyProperty.GetValue(objectToCopy);
                    copyObjectKeyProperty.SetValue(copyObject, keyValue);
                }
            }

            foreach (var objectToCopyProperty in objectToCopyProperties)
            {
                if (!objectToCopyProperty.CanWrite || !objectToCopyProperty.CanRead)
                    continue;

                var objectToCopyValue = objectToCopyProperty.GetValue(objectToCopy);

                PropertyInfo copyObjectProperty = copyObject.GetType().GetProperty(objectToCopyProperty.Name);
                var objectValue = copyObjectProperty.GetValue(copyObject);

                if (objectValue != null && objectToCopyValue != null && objectValue.ToString() != objectToCopyValue.ToString())
                    propertyDiff += "From " + objectValue + " to " + objectToCopyValue + ";";
                else if (objectValue == null && objectToCopyValue != null)
                    propertyDiff += "From null to " + objectToCopyValue + ";";
                else if (objectValue != null && objectToCopyValue == null)
                    propertyDiff += "From " + objectValue + " to null;";

                copyObjectProperty.SetValue(copyObject, objectToCopyValue);
            }

            return propertyDiff;
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey> (IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }


        public static void ShallowCopy(object copyObject, object objectToCopy, bool copyVirtualProperties = false)
        {
            string propertyDiff = string.Empty;
            if (copyObject == null || objectToCopy == null)
                return;

            PropertyInfo keyProperty = objectToCopy.GetType().GetProperties().FirstOrDefault(x => x.GetCustomAttributes().Any(y => y.GetType() == typeof(KeyAttribute)));
            IEnumerable<PropertyInfo> objectToCopyProperties = objectToCopy.GetType().GetProperties().Where(p => !p.GetCustomAttributes().Any(attr => attr.GetType() == typeof(ProjectionPropertyAttribute)));

            if (!copyVirtualProperties)
                objectToCopyProperties = objectToCopyProperties.Where(p => p.GetGetMethod().IsFinal || !p.GetGetMethod().IsVirtual);
            
            if(keyProperty != null)
            {
                PropertyInfo copyObjectKeyProperty = copyObject.GetType().GetProperties().FirstOrDefault(x => x.Name == keyProperty.Name);
                if(copyObjectKeyProperty != null)
                {
                    var keyValue = keyProperty.GetValue(objectToCopy);
                    copyObjectKeyProperty.SetValue(copyObject, keyValue);
                }
            }

            foreach (var objectToCopyProperty in objectToCopyProperties)
            {
                if (!objectToCopyProperty.CanWrite || !objectToCopyProperty.CanRead)
                    continue;

                var objectToCopyValue = objectToCopyProperty.GetValue(objectToCopy);

                PropertyInfo copyObjectProperty = copyObject.GetType().GetProperty(objectToCopyProperty.Name);
                //var objectValue = copyObjectProperty.GetValue(copyObject);

                //when projection has a property that entity doesn't
                if(copyObjectProperty != null)
                    copyObjectProperty.SetValue(copyObject, objectToCopyValue);
            }
        }

        public static string FormatColumnFieldname(string columnFieldName)
        {
            return columnFieldName.Replace("Entity.", string.Empty);
        }

        public static PropertyInfo GetKeyPropertyInfo(Type type)
        {
            try
            {
                var keyPropertyInfo =
                    type.GetProperties()
                        .Single(
                            property =>
                                property.GetCustomAttributes().Any(attr => attr.GetType() == typeof(KeyAttribute)));
                return keyPropertyInfo;
            }
            catch
            {
                return null;
            }
        }

        public static IEnumerable<PropertyInfo> GetProjectionPropertyInfos(Type type)
        {
            try
            {
                var projectionPropertyInfos =
                    type.GetProperties()
                        .Where(
                            property =>
                                property.GetCustomAttributes()
                                    .Any(attr => attr.GetType() == typeof(ProjectionPropertyAttribute)))
                        .ToList();
                return projectionPropertyInfos;
            }
            catch
            {
                return null;
            }
        }

        public static PropertyInfo GetFilterNamePropertyInfo(Type type)
        {
            try
            {
                var filterPropertyInfo =
                    type.GetProperties()
                        .Single(
                            property =>
                                property.GetCustomAttributes()
                                    .Any(attr => attr.GetType() == typeof(FilterNameAttribute)));
                return filterPropertyInfo;
            }
            catch
            {
                return null;
            }
        }

        public static PropertyInfo GetFilterValuePropertyInfo(Type type)
        {
            try
            {
                var filterPropertyInfo =
                    type.GetProperties()
                        .Single(
                            property =>
                                property.GetCustomAttributes()
                                    .Any(attr => attr.GetType() == typeof(FilterValueAttribute)));
                return filterPropertyInfo;
            }
            catch
            {
                return null;
            }
        }


        public static string GetPasteSkipAttributeString(Type type)
        {
            var TypeSpecificSkipAttribute =
                (PasteSkipAttribute)Attribute.GetCustomAttribute(type, typeof(PasteSkipAttribute), false);
            if (TypeSpecificSkipAttribute != null)
                return TypeSpecificSkipAttribute.SkipString;

            return null;
        }

        public static IEnumerable<string> GetConstraintPropertyStrings(Type type)
        {
            var TypeSpecificConstraintAttribute =
                (ConstraintAttributes) Attribute.GetCustomAttribute(type, typeof(ConstraintAttributes), false);
            if (TypeSpecificConstraintAttribute != null)
                return TypeSpecificConstraintAttribute.ColumnNames;

            return null;
        }

        public static IEnumerable<string> GetBulkEditDisabledPropertyStrings(Type type)
        {
            var TypeSpecificConstraintAttribute =
                (BulkEditDisabledAttributes)
                Attribute.GetCustomAttribute(type, typeof(BulkEditDisabledAttributes), false);
            if (TypeSpecificConstraintAttribute != null)
                return TypeSpecificConstraintAttribute.ColumnNames;

            return null;
        }

        public static IEnumerable<string> GetRequiredPropertyStringsForProjection(Type type)
        {
            var TypeSpecificRequiredAttribute =
                (RequiredAttributes) Attribute.GetCustomAttribute(type, typeof(RequiredAttributes), false);
            if (TypeSpecificRequiredAttribute != null)
                return TypeSpecificRequiredAttribute.ColumnNames;

            return null;
        }

        public static IEnumerable<string> GetRequiredPropertyStrings(Type type)
        {
            var requiredPropertyStrings = new List<string>();
            var props = type.GetProperties();
            foreach (var prop in props)
            {
                var attrs = prop.GetCustomAttributes(true);
                foreach (var attr in attrs)
                {
                    var requiredAttr = attr as RequiredAttribute;
                    if (requiredAttr != null)
                        requiredPropertyStrings.Add(prop.Name);
                }
            }

            return requiredPropertyStrings;
        }

        public static bool TrySetNestedValue(string propertyString, object parentInstance, object value)
        {
            try
            {
                SetNestedValue(propertyString, parentInstance, value);
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Recurse member instance to change its value
        /// </summary>
        /// <param name="propertyString">Property string to change</param>
        /// <param name="parentInstance">Instance to modify</param>
        /// <param name="value">Value to modify</param>
        public static void SetNestedValue(string propertyString, object parentInstance, object value)
        {
            var propertyNames = propertyString.Split('.');
            var firstPropertyName = propertyNames.First();
            var childInstance = parentInstance.GetType().GetProperty(firstPropertyName).GetValue(parentInstance);

            if (!propertyString.Contains("."))
            {
                parentInstance.GetType().GetProperty(firstPropertyName).SetValue(parentInstance, value);
            }
            else
            {
                string childPropertyString = string.Empty;
                for (int i = 1; i < propertyNames.Count(); i++)
                {
                    childPropertyString += propertyNames[i] + ".";
                }

                if (childPropertyString != string.Empty)
                {
                    childPropertyString = childPropertyString.Substring(0, childPropertyString.Length - 1);
                    SetNestedValue(childPropertyString, childInstance, value);
                }
            }
        }

        /// <summary>
        /// Recurse member instance to get its value
        /// </summary>
        /// <param name="propertyString">Property string to get</param>
        /// <param name="parentInstance">Instance to get</param>
        public static object GetNestedValue(string propertyString, object parentInstance)
        {
            var propertyNames = propertyString.Split('.');
            var firstPropertyName = propertyNames.First();
            var childInstance = parentInstance.GetType().GetProperty(firstPropertyName).GetValue(parentInstance);

            if (!propertyString.Contains("."))
            {
                return parentInstance.GetType().GetProperty(firstPropertyName).GetValue(parentInstance);
            }
            else
            {
                string childPropertyString = string.Empty;
                for (int i = 1; i < propertyNames.Count(); i++)
                {
                    childPropertyString += propertyNames[i] + ".";
                }

                childPropertyString = childPropertyString.Substring(0, childPropertyString.Length - 1);
                return GetNestedValue(childPropertyString, childInstance);
            }
        }

        public static EnumerationType GetEnumerateType(string value, string nextvalue, out long? differences, out long? startEnumeration, out int? numericIndex, out int numericFieldLength)
        {
            long? nextEnumerator = null;
            int? nextNumericIndex = null;
            int nextNumericFieldLength = 0;

            differences = null;
            numericIndex = StringFormatUtils.GetNumericIndex(value, out numericFieldLength);
            if (numericIndex != null)
                startEnumeration = Int64.Parse(value.Substring(numericIndex.Value, value.Length - numericIndex.Value));
            else
            {
                startEnumeration = null;
                return EnumerationType.None;
            }

            nextNumericIndex = StringFormatUtils.GetNumericIndex(nextvalue, out nextNumericFieldLength);
            if (nextNumericIndex != null)
            {
                if (numericIndex == nextNumericIndex)
                    nextEnumerator = Int64.Parse(nextvalue.Substring(nextNumericIndex.Value, nextvalue.Length - nextNumericIndex.Value));
                else
                    return EnumerationType.None;
            }

            if (startEnumeration < nextEnumerator)
            {
                if (startEnumeration != null && nextEnumerator != null)
                {
                    differences = (long)nextEnumerator - (long)startEnumeration;
                    return EnumerationType.Increase;
                }
                else
                    return EnumerationType.None;

            }
            else
            {
                if (startEnumeration != null && nextEnumerator != null)
                {
                    differences = (long)startEnumeration - (long)nextEnumerator;
                    return EnumerationType.Decrease;
                }
                else
                    return EnumerationType.None;
            }
        }

        public static PropertyInfo GetNestedPropertyInfo(string propertyString, object parentInstance)
        {
            var propertyNames = propertyString.Split('.');
            var firstPropertyName = propertyNames.First();

            object childInstance = parentInstance.GetType().GetProperty(firstPropertyName).GetValue(parentInstance);

            if (!propertyString.Contains("."))
            {
                return parentInstance.GetType().GetProperty(firstPropertyName);
            }
            else
            {
                string childPropertyString = string.Empty;
                for (int i = 1; i < propertyNames.Count(); i++)
                {
                    childPropertyString += propertyNames[i] + ".";
                }

                childPropertyString = childPropertyString.Substring(0, childPropertyString.Length - 1);
                return GetNestedPropertyInfo(childPropertyString, childInstance);
            }
        }

        public static string GetNameOf<T>(Expression<Func<T>> property)
        {
            return (property.Body as MemberExpression).Member.Name;
        }


        public static string GetEditSettingsDisplayMemberValue(object editSettings, string searchData)
        {
            var copyColumnValueMember = (string)editSettings.GetType().GetProperty("ValueMember").GetValue(editSettings);
            var copyColumnDisplayMember = (string)editSettings.GetType().GetProperty("DisplayMember").GetValue(editSettings);
            var copyColumnItemsSource = (IEnumerable<object>)editSettings.GetType().GetProperty("ItemsSource").GetValue(editSettings);

            string displayValue = string.Empty;

            if (copyColumnItemsSource == null || (copyColumnValueMember == null || copyColumnValueMember == string.Empty) || (copyColumnDisplayMember == null || copyColumnDisplayMember == string.Empty))
            {
                return searchData;
            }

            foreach (var copyColumnItem in copyColumnItemsSource)
            {
                var itemDisplayMemberPropertyInfo = copyColumnItem.GetType().GetProperty(copyColumnDisplayMember);
                var itemValueMemberPropertyInfo = copyColumnItem.GetType().GetProperty(copyColumnValueMember);
                if (itemValueMemberPropertyInfo.GetValue(copyColumnItem) != null && itemValueMemberPropertyInfo.GetValue(copyColumnItem).ToString().ToUpper() == searchData.ToUpper())
                {
                    displayValue = itemDisplayMemberPropertyInfo.GetValue(copyColumnItem).ToString();
                    break;
                }
            }

            return displayValue;
        }
    }
}