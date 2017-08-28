using BaseModel.Attributes;
using BaseModel.Misc;
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
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;

namespace BaseModel.Data.Helpers
{   
    public class CopyPasteHelper<TProjection>
        where TProjection : class, new()
    {
        public delegate bool IsValidProjectionFunc(TProjection projection, ref string errorMessage);
        readonly IsValidProjectionFunc isValidProjectionFunc;
        readonly Func<TProjection, bool> onBeforePasteWithValidationFunc;
        readonly IMessageBoxService messageBoxService;
        readonly Func<TProjection, string, object, bool> validateSetValueCallBack;

        public CopyPasteHelper(IsValidProjectionFunc isValidProjectionFunc = null, Func<TProjection, bool> onBeforePasteWithValidationFunc = null, IMessageBoxService messageBoxService = null, Func<TProjection, string, object, bool> validateSetValueCallBack = null)
        {
            this.isValidProjectionFunc = isValidProjectionFunc;
            this.onBeforePasteWithValidationFunc = onBeforePasteWithValidationFunc;
            this.messageBoxService = messageBoxService;
            this.validateSetValueCallBack = validateSetValueCallBack;
        }

        public class UndoRedoArg
        {
            public TProjection Projection { get; set; }
            public string FieldName { get; set; }
            public object OldValue { get; set; }
            public object NewValue { get; set; }
        }

        public enum PasteResult
        {
            Success, 
            Skip, 
            Failed, 
            FailOnRequired
        }

        public List<TProjection> PastingFromClipboardCellLevel<TView>(GridControl gridControl, string[] RowData, EntitiesUndoRedoManager<TProjection> undo_redo_manager)
            where TView : DataViewBase
        {
            var gridView = gridControl.View;

            HashSet<TProjection> preValidatedProjections = new HashSet<TProjection>();
            List<TProjection> pasteProjections = new List<TProjection>();
            List<UndoRedoArg> undoRedoArguments = new List<UndoRedoArg>();
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
                    return pasteProjections;

                var selected_cells_groupby_columns = selected_cells.GroupBy(x => x.Column.FieldName).Select(group => new { FieldName = group.Key, Cells = group.ToList() });
                if (grouped_results.Count == 0)
                {
                    foreach(var selected_cell in selected_cells)
                    {
                        int row_handle = selected_cell.RowHandle;
                        TProjection editing_row = (TProjection)gridControl.GetRow(row_handle);
                        PasteResult result = pasteDataInProjectionColumn(editing_row, selected_cell.Column, string.Empty, undoRedoArguments);
                        if (!preValidatedProjections.Any(x => x.GetHashCode() == editing_row.GetHashCode()))
                            preValidatedProjections.Add(editing_row);
                    }
                }
                //if copied only a row
                else if(grouped_results.All(x => x.Count == 1) && (grouped_results.Count == 1 || (selected_cells_groupby_columns.Count() == grouped_results.Count)))
                {
                    int column_offset = 0;
                    foreach(var selected_column in selected_cells_groupby_columns)
                    {
                        int validated_column_offset = column_offset > (grouped_results.Count - 1) ? grouped_results.Count - 1 : column_offset;
                        var paste_value = grouped_results[validated_column_offset];
                        column_offset += 1;
                        //since we've already verified that each column group only has a row
                        string paste_data = paste_value.First();
                        List<GridColumn> visible_columns = gridTableView.VisibleColumns.ToList();

                        foreach (var selected_cell in selected_column.Cells)
                        {
                            int column_visible_index = selected_cell.Column.VisibleIndex;
                            int row_handle = selected_cell.RowHandle;
                            GridColumn current_column = visible_columns[column_visible_index];
                            TProjection editing_row = (TProjection)gridControl.GetRow(row_handle);
                            PasteResult result = pasteDataInProjectionColumn(editing_row, current_column, paste_data, undoRedoArguments);

                            if(result == PasteResult.FailOnRequired)
                            {
                                messageBoxService.ShowMessage("Cannot set null in required cell, operation has been terminated");
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
                    GridCell first_selected_cell = selected_cells.First();
                    int first_row_handle = first_selected_cell.RowHandle;
                    int first_row_visible_index = gridControl.GetRowVisibleIndexByHandle(first_row_handle);
                    int first_column_visible_index = first_selected_cell.Column.VisibleIndex;
                    List<GridColumn> visible_columns = gridTableView.VisibleColumns.ToList();

                    for (int i = 0; i < grouped_results.Count; i++)
                    {
                        GridColumn current_column = visible_columns[first_column_visible_index + i];
                        string column_name = current_column.FieldName;
                        int row_visible_index_offset = 0;

                        foreach (string rowValue in grouped_results[i])
                        {
                            int current_row_visible_index = first_row_visible_index + row_visible_index_offset;
                            int current_row_handle = gridControl.GetRowHandleByVisibleIndex(current_row_visible_index);
                            TProjection editing_row = (TProjection)gridControl.GetRow(current_row_handle);
                            row_visible_index_offset += 1;

                            PasteResult result = pasteDataInProjectionColumn(editing_row, current_column, rowValue, undoRedoArguments);
                            if (result == PasteResult.FailOnRequired)
                            {
                                messageBoxService.ShowMessage("Cannot set null in required cell, operation has been terminated");
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
                    var errorMessage = "Duplicate exists on constraint field named: ";
                    if (isValidProjectionFunc(preValidatedProjection, ref errorMessage))
                        if (onBeforePasteWithValidationFunc != null)
                        {
                            if (onBeforePasteWithValidationFunc(preValidatedProjection))
                            {
                                IEnumerable<UndoRedoArg> projection_undo_redos = undoRedoArguments.Where(x => x.Projection == preValidatedProjection);
                                foreach (UndoRedoArg projection_undo_redo in projection_undo_redos)
                                    undo_redo_manager.AddUndo(projection_undo_redo.Projection, projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, EntityMessageType.Changed);

                                pasteProjections.Add(preValidatedProjection);
                            }
                        }
                        else
                        {
                            IEnumerable<UndoRedoArg> projection_undo_redos = undoRedoArguments.Where(x => x.Projection == preValidatedProjection);
                            foreach (UndoRedoArg projection_undo_redo in projection_undo_redos)
                                undo_redo_manager.AddUndo(projection_undo_redo.Projection, projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, EntityMessageType.Changed);

                            pasteProjections.Add(preValidatedProjection);
                        }
                    else
                    {
                        if (messageBoxService != null)
                        {
                            errorMessage += " , paste operation will be terminated";
                            messageBoxService.ShowMessage(errorMessage, CommonResources.Exception_UpdateErrorCaption, MessageButton.OK);
                        }

                        break;
                    }
                }

            }

            undo_redo_manager.UnpauseActionId();
            return pasteProjections;
        }

        public List<TProjection> PastingFromClipboardTreeListCellLevel<TView>(GridControl gridControl, string[] RowData, EntitiesUndoRedoManager<TProjection> undo_redo_manager)
            where TView : DataViewBase
        {
            var gridView = gridControl.View;

            HashSet<TProjection> preValidatedProjections = new HashSet<TProjection>();
            List<TProjection> pasteProjections = new List<TProjection>();
            List<UndoRedoArg> undoRedoArguments = new List<UndoRedoArg>();
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
                    return pasteProjections;

                var selected_cells_groupby_columns = selected_cells.GroupBy(x => x.Column.FieldName).Select(group => new { FieldName = group.Key, Cells = group.ToList() });
                if (grouped_results.Count == 0)
                {
                    foreach (var selected_cell in selected_cells)
                    {
                        int row_handle = selected_cell.RowHandle;
                        TProjection editing_row = (TProjection)gridControl.GetRow(row_handle);
                        PasteResult result = pasteDataInProjectionColumn(editing_row, selected_cell.Column, string.Empty, undoRedoArguments);
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
                            PasteResult result = pasteDataInProjectionColumn(editing_row, current_column, paste_data, undoRedoArguments);

                            if (result == PasteResult.FailOnRequired)
                            {
                                messageBoxService.ShowMessage("Cannot set null in required cell, operation has been terminated");
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

                            PasteResult result = pasteDataInProjectionColumn(editing_row, current_column, rowValue, undoRedoArguments);
                            if (result == PasteResult.FailOnRequired)
                            {
                                messageBoxService.ShowMessage("Cannot set null in required cell, operation has been terminated");
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
                    var errorMessage = "Duplicate exists on constraint field named: ";
                    if (isValidProjectionFunc(preValidatedProjection, ref errorMessage))
                        if (onBeforePasteWithValidationFunc != null)
                        {
                            if (onBeforePasteWithValidationFunc(preValidatedProjection))
                            {
                                IEnumerable<UndoRedoArg> projection_undo_redos = undoRedoArguments.Where(x => x.Projection == preValidatedProjection);
                                foreach (UndoRedoArg projection_undo_redo in projection_undo_redos)
                                    undo_redo_manager.AddUndo(projection_undo_redo.Projection, projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, EntityMessageType.Changed);

                                pasteProjections.Add(preValidatedProjection);
                            }
                        }
                        else
                        {
                            IEnumerable<UndoRedoArg> projection_undo_redos = undoRedoArguments.Where(x => x.Projection == preValidatedProjection);
                            foreach (UndoRedoArg projection_undo_redo in projection_undo_redos)
                                undo_redo_manager.AddUndo(projection_undo_redo.Projection, projection_undo_redo.FieldName, projection_undo_redo.OldValue, projection_undo_redo.NewValue, EntityMessageType.Changed);

                            pasteProjections.Add(preValidatedProjection);
                        }
                    else
                    {
                        if (messageBoxService != null)
                        {
                            errorMessage += " , paste operation will be terminated";
                            messageBoxService.ShowMessage(errorMessage, CommonResources.Exception_UpdateErrorCaption, MessageButton.OK);
                        }

                        break;
                    }
                }

            }

            undo_redo_manager.UnpauseActionId();
            return pasteProjections;
        }

        public List<TProjection> PastingFromClipboard<TView>(GridControl gridControl, string[] RowData)
        where TView : DataViewBase
        {
            var gridView = gridControl.View;

            List<TProjection> pasteProjections = new List<TProjection>();
            if (gridView.ActiveEditor == null && (gridView.GetType() == typeof(TView)))
            {
                var gridTView = gridView as TView;
                TableView gridTableView = gridTView as TableView;
                TreeListView gridTreeListView = gridTView as TreeListView;

                foreach (var Row in RowData)
                {
                    TProjection projection = new TProjection();

                    var ColumnStrings = Row.Split('\t');
                    for (var i = 0; i < ColumnStrings.Count(); i++)
                    {
                        ColumnBase copyColumn = gridTableView != null ? gridTableView.VisibleColumns[i] : gridTreeListView.VisibleColumns[i];
                        PasteResult result = pasteDataInProjectionColumn(projection, copyColumn, ColumnStrings[i]);
                        if (result == PasteResult.Skip)
                            continue;
                    }

                    var errorMessage = "Duplicate exists on constraint field named: ";
                    if (isValidProjectionFunc(projection, ref errorMessage))
                        if (onBeforePasteWithValidationFunc != null)
                        {
                            if (onBeforePasteWithValidationFunc(projection))
                                pasteProjections.Add(projection);
                        }
                        else
                            pasteProjections.Add(projection);
                    else
                    {
                        if(messageBoxService != null)
                        {
                            errorMessage += " , paste operation will be terminated";
                            messageBoxService.ShowMessage(errorMessage, CommonResources.Exception_UpdateErrorCaption, MessageButton.OK);
                        }

                        break;
                    }
                }
            }

            return pasteProjections;
        }

        public PasteResult pasteDataInProjectionColumn(TProjection projection, ColumnBase column, string pasteData, List<UndoRedoArg> undoRedoArguments = null)
        {
            if (column.ReadOnly)
                return PasteResult.Skip;

            string column_name = column.FieldName;
            PropertyInfo columnPropertyInfo = DataUtils.GetNestedPropertyInfo(column_name, projection);
            try
            {
                if (columnPropertyInfo != null)
                    if(pasteData == string.Empty && Nullable.GetUnderlyingType(columnPropertyInfo.PropertyType) != null)
                    {
                        if (Attribute.IsDefined(columnPropertyInfo, typeof(RequiredAttribute)))
                            return PasteResult.FailOnRequired;

                        return tryPasteNewValueInProjectionColumn(projection, column_name, null, undoRedoArguments);
                    }
                    else if (columnPropertyInfo.PropertyType == typeof(Guid?) || columnPropertyInfo.PropertyType == typeof(Guid))
                    {
                        object cellTemplate = column.CellTemplate;
                        DataTemplate dataTemplate = cellTemplate as DataTemplate;
                        Type editSettingsType;
                        //if (dataTemplate != null && dataTemplate.HasContent)
                        //    editSettingsType = dataTemplate.LoadContent().GetType();
                        //else
                        editSettingsType = column.ActualEditSettings.GetType();

                        object editSettings = null;
                        if (editSettingsType == typeof(ComboBoxEditSettings))
                            editSettings = column.ActualEditSettings as ComboBoxEditSettings;
                        else if (editSettingsType == typeof(LookUpEditSettings))
                            editSettings = column.ActualEditSettings as LookUpEditSettingsBase;
                        //else if (editSettingsType == typeof(ComboBoxEdit) && dataTemplate.HasContent)
                        //    editSettings = dataTemplate.LoadContent() as ComboBoxEdit;

                        if (editSettings != null)
                        {
                            var copyColumnValueMember = (string)editSettings.GetType().GetProperty("ValueMember").GetValue(editSettings);
                            var copyColumnDisplayMember = (string)editSettings.GetType().GetProperty("DisplayMember").GetValue(editSettings);
                            var copyColumnItemsSource = (IEnumerable<object>)editSettings.GetType().GetProperty("ItemsSource").GetValue(editSettings);
                            Guid? guid_value = null;
                            foreach (var copyColumnItem in copyColumnItemsSource)
                            {
                                var itemDisplayMemberPropertyInfo =
                                    copyColumnItem.GetType().GetProperty(copyColumnDisplayMember);
                                var itemValueMemberPropertyInfo =
                                    copyColumnItem.GetType().GetProperty(copyColumnValueMember);
                                if (itemDisplayMemberPropertyInfo.GetValue(copyColumnItem).ToString().ToUpper() == pasteData.ToUpper())
                                {
                                    guid_value = (Guid)itemValueMemberPropertyInfo.GetValue(copyColumnItem);
                                    break;
                                }
                            }

                            if (guid_value != null)
                                return tryPasteNewValueInProjectionColumn(projection, column_name, guid_value, undoRedoArguments);
                            else
                                return PasteResult.Skip;
                        }
                        else if (editSettings != null && pasteData != Guid.Empty.ToString())
                        {
                            Guid new_guid = new Guid(pasteData);
                            return tryPasteNewValueInProjectionColumn(projection, column_name, new_guid, undoRedoArguments);
                        }
                    }
                    else if (columnPropertyInfo.PropertyType == typeof(string))
                    {
                        string new_string = pasteData.ToString();
                        if(new_string == string.Empty)
                        {
                            if (Attribute.IsDefined(columnPropertyInfo, typeof(RequiredAttribute)))
                                return PasteResult.FailOnRequired;
                        }

                        return tryPasteNewValueInProjectionColumn(projection, column_name, new_string, undoRedoArguments);
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
                                return tryPasteNewValueInProjectionColumn(projection, column_name, enum_value, undoRedoArguments);
                        }
                    }
                    else if (columnPropertyInfo.PropertyType == typeof(decimal) || columnPropertyInfo.PropertyType == typeof(decimal?) || columnPropertyInfo.PropertyType == typeof(int) || columnPropertyInfo.PropertyType == typeof(int?) || columnPropertyInfo.PropertyType == typeof(double) || columnPropertyInfo.PropertyType == typeof(double?))
                    {
                        var rgx = new Regex("[^0-9a-z\\.]");
                        var cleanColumnString = rgx.Replace(pasteData, string.Empty);

                        if (columnPropertyInfo.PropertyType == typeof(decimal) ||
                            columnPropertyInfo.PropertyType == typeof(decimal?))
                        {
                            decimal decimal_value;
                            if (decimal.TryParse(cleanColumnString, out decimal_value))
                            {
                                if (column_name.Contains('%') || column_name.ToUpper().Contains("PERCENT"))
                                {
                                    if(decimal_value > 1)
                                        decimal_value /= 100;
                                    //else when user copy from grid and paste it will be the actual value
                                }

                                return tryPasteNewValueInProjectionColumn(projection, column_name, decimal_value, undoRedoArguments);
                            }
                            else
                                return PasteResult.Skip;
                        }
                        else if (columnPropertyInfo.PropertyType == typeof(int) ||
                                 columnPropertyInfo.PropertyType == typeof(int?))
                        {
                            int int_value;
                            if (int.TryParse(cleanColumnString, out int_value))
                                return tryPasteNewValueInProjectionColumn(projection, column_name, int_value, undoRedoArguments);
                            else
                                return PasteResult.Skip;
                        }
                        else if (columnPropertyInfo.PropertyType == typeof(double) ||
                                 columnPropertyInfo.PropertyType == typeof(double?))
                        {
                            double double_value;
                            if (double.TryParse(cleanColumnString, out double_value))
                            {
                                if (column_name.Contains('%') || column_name.ToUpper().Contains("PERCENT"))
                                    double_value /= 100;

                                return tryPasteNewValueInProjectionColumn(projection, column_name, double_value, undoRedoArguments);
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
                            return tryPasteNewValueInProjectionColumn(projection, column_name, datetime_value, undoRedoArguments);
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

                            if(setValues.Count > 0)
                                return tryPasteNewValueInProjectionColumn(projection, column_name, setValues, undoRedoArguments);
                            else
                                return PasteResult.Skip;
                        }
                        else
                            return PasteResult.Skip;
                    }
                    else
                        return PasteResult.Skip;
                else
                    return PasteResult.Skip;
            }
            catch
            {
                return PasteResult.Skip;
            }

            return PasteResult.Success;
        }

        private PasteResult tryPasteNewValueInProjectionColumn(TProjection projection, string column_name, object new_value, List<UndoRedoArg> undoRedoArguments = null)
        {
            if (validateSetValueCallBack == null || validateSetValueCallBack(projection, column_name, new_value))
            {
                object old_value = DataUtils.GetNestedValue(column_name, projection);
                if (!DataUtils.TrySetNestedValue(column_name, projection, new_value))
                    return PasteResult.Skip;
                else if (undoRedoArguments != null)
                    undoRedoArguments.Add(new UndoRedoArg() { FieldName = column_name, Projection = projection, OldValue = old_value, NewValue = new_value });

                return PasteResult.Success;
            }
            else
                return PasteResult.Skip;
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

        public static object ShallowCopy(object copyObject, object objectToCopy, bool copyVirtualProperties = false)
        {
            if (copyObject == null || objectToCopy == null)
                return null;

            PropertyInfo keyProperty = objectToCopy.GetType().GetProperties().FirstOrDefault(x => x.GetCustomAttributes().Any(y => y.GetType() == typeof(KeyAttribute)));
            IEnumerable<PropertyInfo> objectToCopyProperties = objectToCopy.GetType().GetProperties().Where(p => !p.GetCustomAttributes().Any(attr => attr.GetType() == typeof(ProjectionPropertyAttribute)));
            if(!copyVirtualProperties)
                objectToCopyProperties = objectToCopyProperties.Where(p => !p.GetGetMethod().IsVirtual);

            
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

                copyObjectProperty.SetValue(copyObject, objectToCopyValue);
            }

            return copyObject;
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

        public static PropertyInfo GetNestedPropertyInfo(string propertyString, object parentInstance)
        {
            var propertyNames = propertyString.Split('.');
            var firstPropertyName = propertyNames.First();
            var childInstance = parentInstance.GetType().GetProperty(firstPropertyName).GetValue(parentInstance);

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
    }
}