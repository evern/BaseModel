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
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using static BaseModel.Data.Helpers.DataUtils;

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
        public Func<object, TProjection> instantFeedbackEntityConversionFunc;
        public CopyPasteHelper(IsValidProjectionFunc isValidProjectionFunc = null, Func<TProjection, bool> onBeforePasteWithValidationFunc = null, IDialogService errorMessagesDialogService = null, Func<TProjection, string, object, bool, string> unifiedValueValidationCallback = null, Func<TProjection, ColumnBase, string, List<UndoRedoArg<TProjection>>, bool> funcManualCellPastingIsContinue = null, Func<List<KeyValuePair<ColumnBase, string>>, TProjection, bool, bool> funcManualRowPastingIsContinue = null, Action<string, object, object, TProjection, bool> cellValueChanging = null, Action<string, object, object, TProjection, bool> cellValueChanged = null, Action<TProjection> newRowInitialization = null, Action<IEnumerable<ErrorMessage>> formatErrorMessages = null, Func<object, TProjection> instantFeedbackEntityConversionFunc = null)
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
            this.instantFeedbackEntityConversionFunc = instantFeedbackEntityConversionFunc;
        }

        public List<TProjection> PastingFromClipboardCellLevel<TView>(GridControl gridControl, string[] RowData, EntitiesUndoRedoManager<TProjection> undo_redo_manager, out List<ErrorMessage> errorMessages)
            where TView : DataViewBase
        {
            var gridView = gridControl.View;

            errorMessages = new List<ErrorMessage>();
            HashSet<TProjection> preValidatedProjections = new HashSet<TProjection>();
            List<TProjection> validatedProjections = new List<TProjection>();
            List<UndoRedoArg<TProjection>> undoRedoArguments = new List<UndoRedoArg<TProjection>>();
            if (gridView.ActiveEditor == null)
            {
                TableView gridTableView = gridView as TableView;
                if(gridTableView != null)
                {
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
                        foreach (var selected_cell in selected_cells)
                        {

                            int row_handle = selected_cell.RowHandle;
                            TProjection editing_row;
                            if (instantFeedbackEntityConversionFunc != null)
                                editing_row = instantFeedbackEntityConversionFunc(gridControl.GetRow(row_handle));
                            else
                                editing_row = (TProjection)gridControl.GetRow(row_handle);

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
                        //row visible index can't be retrieved from grid control in detailed descriptor
                        if (first_row_visible_index < 1)
                            first_row_visible_index = first_row_handle;

                        int last_row_visible_index = gridControl.GetRowVisibleIndexByHandle(last_row_handle);
                        if (last_row_visible_index < 1)
                            last_row_visible_index = last_row_handle;

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
                                if (current_row_handle < 1)
                                    current_row_handle = current_row_visible_index;

                                object rowObject = gridControl.GetRow(current_row_handle);
                                if (rowObject == null)
                                    continue;

                                TProjection editing_row;
                                if (instantFeedbackEntityConversionFunc != null)
                                    editing_row = instantFeedbackEntityConversionFunc(gridControl.GetRow(current_row_handle));
                                else
                                    editing_row = (TProjection)gridControl.GetRow(current_row_handle);

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

                            if (validate_row != null)
                                preValidatedProjections.Add(validate_row);

                            pasteValueRowOffset += 1;
                            if (pasteValueRowOffset >= grouped_results[pasteValueColumnOffset].Count)
                                pasteValueRowOffset = 0;
                        }
                    }

                    undo_redo_manager?.PauseActionId();
                    foreach (TProjection preValidatedProjection in preValidatedProjections)
                    {
                        string errorMessage = string.Empty;
                        List<KeyValuePair<string, string>> constraintIssues = new List<KeyValuePair<string, string>>();
                        if (isValidProjectionFunc != null && isValidProjectionFunc(preValidatedProjection, validatedProjections, ref errorMessage, out constraintIssues))
                            if (onBeforePasteWithValidationFunc != null)
                            {
                                if (onBeforePasteWithValidationFunc(preValidatedProjection))
                                {
                                    IEnumerable<UndoRedoArg<TProjection>> projection_undo_redos = undoRedoArguments.Where(x => x.Projection == preValidatedProjection);
                                    foreach (UndoRedoArg<TProjection> projection_undo_redo in projection_undo_redos)
                                    {
                                        cellValueChanging?.Invoke(projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, projection_undo_redo.Projection, false);
                                        cellValueChanged?.Invoke(projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, projection_undo_redo.Projection, false);

                                        undo_redo_manager?.AddUndo(projection_undo_redo.Projection, projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, EntityMessageType.Changed);
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

                                    undo_redo_manager?.AddUndo(projection_undo_redo.Projection, projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, EntityMessageType.Changed);
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
            }

            undo_redo_manager?.UnpauseActionId();
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
                    List<KeyValuePair<string, string>> constraintIssues = new List<KeyValuePair<string, string>>();

                    if (isValidProjectionFunc != null)
                    {
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
                    else
                    {
                        validatedProjections.Add(projection);
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
        public static T getEditSettingsValueMemberValue<T>(object editSettings, string searchData)
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

                string displayMemberStr = itemDisplayMemberPropertyInfo.GetValue(copyColumnItem) == null ? string.Empty : itemDisplayMemberPropertyInfo.GetValue(copyColumnItem).ToString().ToUpper();
                if (displayMemberStr.Trim() == searchData.ToUpper().Trim())
                {
                    editValue = (T)itemValueMemberPropertyInfo.GetValue(copyColumnItem);
                    break;
                }
            }

            return editValue;
        }

        public static PasteResult pasteDataInProjectionColumn<TProjection>(TProjection projection, ColumnBase column, string pasteData, out string errorMessage, List<UndoRedoArg<TProjection>> undoRedoArguments = null, Func<TProjection, ColumnBase, string, List<UndoRedoArg<TProjection>>, bool> funcManualCellPastingIsContinue = null, string alternateFieldName = "")
        {
            errorMessage = string.Empty;
            if (column.ReadOnly)
                return PasteResult.Skip;

            EditableColumn editableColumn = column as EditableColumn;
            string fieldName = alternateFieldName != string.Empty ? alternateFieldName : editableColumn != null ? editableColumn.RealFieldName : column.FieldName;
            pasteData = pasteData.Trim();

            try
            {
                PropertyInfo columnPropertyInfo = DataUtils.GetNestedPropertyInfo(fieldName, projection);
                if (columnPropertyInfo != null)
                {
                    if (funcManualCellPastingIsContinue == null || (funcManualCellPastingIsContinue != null && funcManualCellPastingIsContinue.Invoke(projection, column, pasteData, undoRedoArguments)))
                    {
                        if (pasteData == string.Empty && Nullable.GetUnderlyingType(columnPropertyInfo.PropertyType) != null)
                        {
                            if (Attribute.IsDefined(columnPropertyInfo, typeof(RequiredAttribute)))
                                return PasteResult.FailOnRequired;

                            return trySetValueInProjection(projection, fieldName, null, out errorMessage, undoRedoArguments);
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
                                    return trySetValueInProjection(projection, fieldName, guid_value, out errorMessage, undoRedoArguments);
                                else
                                    return PasteResult.Skip;
                            }
                            //lookupedit under datatemplate are detected is texteditsettings
                            else if ((editSettings != null || column.ActualEditSettings.GetType() == typeof(TextEditSettings)) && pasteData != Guid.Empty.ToString())
                            {
                                Guid new_guid = new Guid(pasteData);
                                return trySetValueInProjection(projection, fieldName, new_guid, out errorMessage, undoRedoArguments);
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

                            return trySetValueInProjection(projection, fieldName, new_string, out errorMessage, undoRedoArguments);
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
                                    return trySetValueInProjection(projection, fieldName, enum_value, out errorMessage, undoRedoArguments);
                            }
                        }
                        else if (columnPropertyInfo.PropertyType == typeof(decimal) || columnPropertyInfo.PropertyType == typeof(decimal?) || columnPropertyInfo.PropertyType == typeof(int) || columnPropertyInfo.PropertyType == typeof(int?) || columnPropertyInfo.PropertyType == typeof(double) || columnPropertyInfo.PropertyType == typeof(double?))
                        {
                            var rgx = new Regex("[A-Za-z\\$]");
                            var cleanColumnString = rgx.Replace(pasteData, string.Empty);
                            bool isPercentColumn = fieldName.Contains('%') || fieldName.ToUpper().Contains("PERCENT");
                            if (isPercentColumn)
                                cleanColumnString = cleanColumnString.Replace("%", "");

                            if (columnPropertyInfo.PropertyType == typeof(decimal) ||
                                columnPropertyInfo.PropertyType == typeof(decimal?))
                            {
                                decimal decimal_value;
                                if (decimal.TryParse(cleanColumnString, out decimal_value))
                                {
                                    if (isPercentColumn)
                                    {
                                        if (decimal_value > 1)
                                            decimal_value /= 100;
                                        //else when user copy from grid and paste it will be the actual value
                                    }

                                    return trySetValueInProjection(projection, fieldName, decimal_value, out errorMessage, undoRedoArguments);
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
                                        return trySetValueInProjection(projection, fieldName, int_value, out errorMessage, undoRedoArguments);
                                    else
                                        return PasteResult.Skip;
                                }
                                else
                                {
                                    int int_value;
                                    if (int.TryParse(cleanColumnString, out int_value))
                                        return trySetValueInProjection(projection, fieldName, int_value, out errorMessage, undoRedoArguments);
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
                                    if (fieldName.Contains('%') || fieldName.ToUpper().Contains("PERCENT"))
                                        double_value /= 100;

                                    return trySetValueInProjection(projection, fieldName, double_value, out errorMessage, undoRedoArguments);
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
                                return trySetValueInProjection(projection, fieldName, datetime_value, out errorMessage, undoRedoArguments);
                            else
                                return PasteResult.Skip;
                        }
                        else if (column.ActualEditSettings is ComboBoxEditSettings)
                        {
                            ComboBoxEditSettings comboboxEditSettings = column.ActualEditSettings as ComboBoxEditSettings;
                            CheckedComboBoxStyleSettings checkedComboBoxStyleSettings = comboboxEditSettings.StyleSettings as CheckedComboBoxStyleSettings;

                            if (checkedComboBoxStyleSettings != null)
                            {
                                var copyColumnDisplayMember = (string)comboboxEditSettings.GetType().GetProperty("DisplayMember").GetValue(comboboxEditSettings);
                                var copyColumnItemsSource = (IEnumerable<object>)comboboxEditSettings.GetType().GetProperty("ItemsSource").GetValue(comboboxEditSettings);
                                var copyColumnTag = (IEnumerable<object>)comboboxEditSettings.GetType().GetProperty("Tag").GetValue(comboboxEditSettings);
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
                                    return trySetValueInProjection(projection, fieldName, setValues, out errorMessage, undoRedoArguments);
                                else
                                    return PasteResult.Skip;
                            }
                            else if(comboboxEditSettings != null)
                            {
                                var copyColumnItemsSource = (IEnumerable<object>)comboboxEditSettings.GetType().GetProperty("ItemsSource").GetValue(comboboxEditSettings);
                                if(copyColumnItemsSource.Count() > 0)
                                {
                                    var itemSourceFirstProperty = copyColumnItemsSource.First();
                                    if(itemSourceFirstProperty.GetType() == typeof(EnumMemberInfo))
                                    {
                                        EnumMemberInfo findEnumValueFromString = (EnumMemberInfo)copyColumnItemsSource.FirstOrDefault(x => x.ToString() == pasteData);
                                        if (findEnumValueFromString != null)
                                        {
                                            Type enumType;
                                            if (columnPropertyInfo.PropertyType.IsGenericType && columnPropertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                                                enumType = columnPropertyInfo.PropertyType.GetGenericArguments().First();
                                            else
                                                enumType = columnPropertyInfo.PropertyType;

                                            object enumValue = System.Enum.Parse(enumType, pasteData);
                                            return trySetValueInProjection(projection, fieldName, enumValue, out errorMessage, undoRedoArguments);
                                        }
                                    }
                                }
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
                                    return trySetValueInProjection(projection, fieldName, (bool)booleanValue, out errorMessage, undoRedoArguments);
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

        public static PasteResult trySetValueInProjection<TProjection>(TProjection projection, string column_name, object new_value, out string error_message, List<UndoRedoArg<TProjection>> undoRedoArguments = null, Func<TProjection, string, object, bool, string> unifiedValueValidationCallback = null)
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

        /// <summary>
        /// For the purpose of presentation, variation code must always be empty
        /// But when budget is edited, findExistingOrAddNewLine will handle the difference between null and string.empty values
        /// </summary>
        public static string NormalizeString(string strValue)
        {
            if (strValue == null)
                return string.Empty;

            return strValue;
        }

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

        public static Type GetEnumerableType(Type type)
        {
            if (type.IsInterface && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];
            foreach (Type intType in type.GetInterfaces())
            {
                if (intType.IsGenericType
                    && intType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return intType.GetGenericArguments()[0];
                }
            }
            return null;
        }

        public static string GetNameOf<T>(Expression<Func<T>> property)
        {
            return (property.Body as MemberExpression).Member.Name;
        }

        public static IEnumerable<T> GetValuesOf<T>(Expression<Func<T>> property)
        {
            foreach (T enumProperty in (T[])Enum.GetValues(typeof(T)))
            {
                yield return enumProperty;
            }
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

        /// <summary>
        /// Determine whether other entities in the collection shares any common combination of unique constraints
        /// And since this is designed to be called from cell value changing the entity would not have been updated with the new value
        /// Hence fieldName and newValue is used for validation
        /// </summary>
        /// <param name="entity">The entity to be validated</param>
        /// <param name="fieldName">Fieldname of the current changing cell</param>
        /// <param name="newValue">New value of the current changing cell</param>
        /// <param name="errorMessage">Error message to notify the user of conflicting constraints</param>
        /// <returns>Returns true if no other entity contains similar constraint member values</returns>
        public static bool IsValidEntityCellValue<TProjection>(IEnumerable<TProjection> entities, TProjection entity, string fieldName, object newValue, ref string errorMessage, out string invalidFieldName)
        {
            List<KeyValuePair<string, string>> constraintIssues;
            invalidFieldName = string.Empty;
            bool isUnique = IsUniqueEntityConstraintValues(entities, entity, null, ref errorMessage, out constraintIssues, new KeyValuePair<string, object>(fieldName, newValue));
            if (isUnique)
                invalidFieldName = string.Empty;
            else
            {
                foreach (var constraintIssue in constraintIssues)
                {
                    invalidFieldName += constraintIssue.Key + ": " + constraintIssue.Value + " ,";
                }

                invalidFieldName = invalidFieldName.Substring(0, invalidFieldName.Length - 2);
            }

            return isUnique;
        }

        /// <summary>
        /// Gets the concatenated string value for constraint field name for an entity
        /// </summary>
        /// <param name="entity">Entity to retrieve the field name</param>
        /// <param name="errorMessage">Error message to be populated with entity member constraint field names</param>
        /// <param name="keyValuePairNewFieldValue">In some instance the new value isn't yet updated on the entity, so this provides other ways pass in the new value</param>
        /// <returns>Concatenated constraint value string</returns>
        public static bool IsUniqueEntityConstraintValues<TProjection>(IEnumerable<TProjection> entities, TProjection entity, IEnumerable<TProjection> preCommittedProjections, ref string errorMessage, out List<KeyValuePair<string, string>> constraintIssues,
            KeyValuePair<string, object>? keyValuePairNewFieldValue = null)
        {
            constraintIssues = new List<KeyValuePair<string, string>>();
            var currentEntityConcatenatedConstraints = string.Empty;

            var constraintMemberPropertyStrings =
                DataUtils.GetConstraintPropertyStrings(typeof(TProjection));
            if (constraintMemberPropertyStrings == null)
                return true;
            else if (keyValuePairNewFieldValue != null &&
                     !constraintMemberPropertyStrings.Contains(((KeyValuePair<string, object>)keyValuePairNewFieldValue).Key))
                return true;

            foreach (var constraintMemberPropertyString in constraintMemberPropertyStrings)
            {
                object constraintMemberPropertyValue = null;
                if (keyValuePairNewFieldValue == null)
                    constraintMemberPropertyValue = DataUtils.GetNestedValue(constraintMemberPropertyString, entity);
                else
                {
                    var keyValuePairForNewFieldValue =
                        (KeyValuePair<string, object>)keyValuePairNewFieldValue;
                    if (constraintMemberPropertyString == keyValuePairForNewFieldValue.Key)
                        constraintMemberPropertyValue = keyValuePairForNewFieldValue.Value;
                    else
                        constraintMemberPropertyValue = DataUtils.GetNestedValue(constraintMemberPropertyString, entity);
                }

                if (constraintMemberPropertyValue != null)
                {
                    var immediatePropertyString = constraintMemberPropertyString.Split('.').Last();
                    string constraintMemberPropertyStringFormat;
                    if (constraintMemberPropertyValue.GetType() == typeof(decimal))
                        constraintMemberPropertyStringFormat = ((decimal)constraintMemberPropertyValue).ToString("0.00");
                    else
                        constraintMemberPropertyStringFormat = constraintMemberPropertyValue.ToString();
                    currentEntityConcatenatedConstraints += constraintMemberPropertyStringFormat;

                    constraintIssues.Add(new KeyValuePair<string, string>(immediatePropertyString, constraintMemberPropertyStringFormat));
                }
            }

            return IsConstraintExistsInOtherEntities<TProjection>(entities, entity, preCommittedProjections, currentEntityConcatenatedConstraints,
                constraintMemberPropertyStrings, ref errorMessage, out constraintIssues);
        }


        /// <summary>
        /// Formatting navigation key
        /// </summary>
        public static string FormatNavigationKey(string navigationKey)
        {
            string uniqueNavKeyFormat = string.Empty;
            if (navigationKey != null)
            {
                uniqueNavKeyFormat = navigationKey.Replace("-", "");
                if (uniqueNavKeyFormat.Length >= 8)
                    uniqueNavKeyFormat = uniqueNavKeyFormat.Substring(1, 8);
            }

            return uniqueNavKeyFormat;
        }

        /// <summary>
        /// Determine whether current entity constraint exists in other entity
        /// </summary>
        /// <param name="entity">The entity to be validated</param>
        /// <param name="entityConstraint">Constraint string of the current entity</param>
        /// <param name="constraintMemberPropertyInfos">Constraint property infos</param>
        /// <param name="constraintErrorMessage">Error message to notify the user of conflicting constraints</param>
        /// <returns>Returns true if no other entity contains similar constraint member values</returns>
        public static bool IsConstraintExistsInOtherEntities<TProjection>(IEnumerable<TProjection> entities, TProjection entity, IEnumerable<TProjection> preCommittedProjections, string entityConstraint,
            IEnumerable<string> constraintMemberPropertyStrings, ref string constraintErrorMessage, out List<KeyValuePair<string, string>> constraintFieldNames)
        {
            constraintFieldNames = new List<KeyValuePair<string, string>>();
            if (entityConstraint == string.Empty)
                return true;

            var keyPropertyInfo = DataUtils.GetKeyPropertyInfo(typeof(TProjection));
            object exclusionKeyValue = null;
            if (keyPropertyInfo != null)
                exclusionKeyValue = keyPropertyInfo.GetValue(entity);

            List<Tuple<IEnumerable<TProjection>, bool>> allEntities = new List<Tuple<IEnumerable<TProjection>, bool>>();
            allEntities.Add(new Tuple<IEnumerable<TProjection>, bool>(entities, true));
            if (preCommittedProjections != null)
                allEntities.Add(new Tuple<IEnumerable<TProjection>, bool>(preCommittedProjections, false));

            foreach (var entityTuples in allEntities)
            {
                bool shouldValidateKey = entityTuples.Item2;
                foreach (var otherEntity in entityTuples.Item1)
                {
                    if (shouldValidateKey && keyPropertyInfo != null)
                    {
                        var otherKey = keyPropertyInfo.GetValue(otherEntity);
                        if (otherKey == null)
                            continue;

                        if (otherKey.Equals(exclusionKeyValue))
                            continue;
                    }

                    List<KeyValuePair<string, string>> errorValuePairs = new List<KeyValuePair<string, string>>();
                    var otherEntityConcatenatedConstraints = string.Empty;
                    foreach (var constraintMemberPropertyString in constraintMemberPropertyStrings)
                    {
                        var constraintMemberPropertyValue = DataUtils.GetNestedValue(constraintMemberPropertyString,
                            otherEntity);
                        if (constraintMemberPropertyValue != null)
                        {
                            string constraintMemberPropertyStringFormat;
                            if (constraintMemberPropertyValue.GetType() == typeof(decimal))
                                constraintMemberPropertyStringFormat =
                                    ((decimal)constraintMemberPropertyValue).ToString("0.00");
                            else
                                constraintMemberPropertyStringFormat = constraintMemberPropertyValue.ToString();

                            otherEntityConcatenatedConstraints += constraintMemberPropertyStringFormat;

                            errorValuePairs.Add(new KeyValuePair<string, string>(constraintMemberPropertyString, constraintMemberPropertyStringFormat));
                        }
                    }

                    if (otherEntityConcatenatedConstraints != string.Empty && otherEntityConcatenatedConstraints == entityConstraint)
                    {
                        IEnumerable<KeyValuePair<string, string>> validIssues = errorValuePairs.Where(x => x.Value != string.Empty);
                        foreach (KeyValuePair<string, string> constraintIssue in validIssues)
                        {
                            if (constraintIssue.Key == validIssues.Last().Key && constraintIssue.Key != validIssues.First().Key)
                            {
                                constraintErrorMessage = constraintErrorMessage.Substring(0, constraintErrorMessage.Length - 2);
                                constraintErrorMessage += " and ";
                            }

                            string propertyStringFormat = constraintIssue.Key.Replace("GUID_", string.Empty);
                            //propertyStringFormat = StringFormatUtils.DisplayCamelCaseString(propertyStringFormat);
                            constraintErrorMessage += "[" + propertyStringFormat + "] = " + constraintIssue.Value + ", ";
                        }

                        constraintErrorMessage = "Entry already exist for " + constraintErrorMessage;
                        constraintFieldNames = errorValuePairs.ToList();
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Check if entity have key value
        /// </summary>
        /// <param name="entity">The entity to be validated</param>
        /// <param name="errorMessage">Error mesasage formatted with key property info</param>
        /// <returns></returns>
        public static bool IsRequiredAttributesHasValue<TEntity, TProjection>(TProjection entity, ref string errorMessage)
        {
            IEnumerable<string> requiredPropertyStrings;
            if (typeof(TProjection) == typeof(TEntity))
                requiredPropertyStrings = DataUtils.GetRequiredPropertyStrings(typeof(TProjection));
            else
                requiredPropertyStrings = DataUtils.GetRequiredPropertyStringsForProjection(typeof(TProjection));

            var requiredPropertyNames = string.Empty;
            if (requiredPropertyStrings == null || requiredPropertyStrings.Count() == 0)
                return true;
            else
            {
                foreach (var requiredPropertyString in requiredPropertyStrings)
                {
                    var requiredPropertyValue = DataUtils.GetNestedValue(requiredPropertyString, entity);
                    if (requiredPropertyValue == null || requiredPropertyValue.ToString() == Guid.Empty.ToString())
                        requiredPropertyNames += requiredPropertyString.Replace("GUID_", string.Empty).Split('.').Last() + ", ";
                }

                if (requiredPropertyNames != string.Empty)
                {
                    errorMessage = string.Format("{0} value missing",
                        requiredPropertyNames.Substring(0, requiredPropertyNames.Length - 2));
                    return false;
                }
                else
                    return true;
            }
        }

        public enum PasteResult
        {
            Success,
            Skip,
            Failed,
            FailOnRequired
        }
    }
}