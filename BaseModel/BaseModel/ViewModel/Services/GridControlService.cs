using BaseModel.Data.Helpers;
using BaseModel.Misc;
using DevExpress.Data;
using DevExpress.Data.Extensions;
using DevExpress.Data.Filtering;
using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Editors.Settings;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.SpellChecker;
using DevExpress.XtraSpellChecker;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace BaseModel.ViewModel.Services
{
    public interface IGridControlService
    {
        //for debugging purpose
        GridControl GridControl { get; set; }
        void BeginDataUpdate();
        void EndDataUpdate();
        void SetRowExpandedByColumnValue(string field_name, IHaveExpandState row);
        CriteriaOperator FilterCriteria { get; set; }
        void ClearFilterCriteria();
        void RefreshSummary();
        void RefreshData();
        IEnumerable<object> GetVisibleRowObjects();
        void HighlightIncorrectText(SpellChecker spellChecker);
        void SetExcelFilterPopUpMode();
        void ClearSorting();
        void ClearGrouping();
        void SortBy(string fieldName);
        void GroupBy(string fieldName);
        void CopyWithHeader();
        void ClearSummary();
        void ExpandAllGroups();
        void CollapseAllGroups();
        void AddSummary(string fieldName, SummaryItemType summaryType, string displayFormat);
        void ChangeSummary(string oldFieldName, string newFieldName);
        void MasterDetail_ExpandAll();
        void MasterDetail_CollapseAllButThis();
        void MasterDetail_CollapseAll();
        void CombineMasterDetailSearch();
        void ExpandAllMasterRows();
        void CollapseAllMasterRows();
        void SetFilterCriteria(string filter_string);
        int[] GetSelectedRowHandles();
        int GetListIndexByRowHandle(int rowHandle);
        int GetRowHandleByListIndex(int listIndex);
        void RemoveSelectedRows(int[] rowHandles);
        void RefreshRowByListIndex(int listIndex);
        ObservableCollection<Misc.GroupInfo> GetExpansionState();
        GridColumnCollection GridColumns();
        object GetRow(int rowHandle);
        void SetExpansionState(ObservableCollection<Misc.GroupInfo> states);
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

        public int[] GetSelectedRowHandles()
        {
            return GridControl.GetSelectedRowHandles();
        }

        public void RemoveSelectedRows(int[] rowHandles)
        {
            TableView view = GridControl.View as TableView;
            if(view != null)
            {
                foreach (int handle in rowHandles.OrderByDescending(x => x))
                {
                    view.DeleteRow(handle);
                }
            }
        }

        public object GetRow(int rowHandle)
        {
            return GridControl.GetRow(rowHandle);
        }

        public int GetListIndexByRowHandle(int rowHandle)
        {
            return GridControl.GetListIndexByRowHandle(rowHandle);
        }

        public int GetRowHandleByListIndex(int listIndex)
        {
            return GridControl.GetRowHandleByListIndex(listIndex);
        }

        public void SetFilterCriteria(string filter_string)
        {
            GridControl.FilterCriteria = CriteriaOperator.Parse(filter_string);
        }

        public void CombineMasterDetailSearch()
        {
            if (GridControl == null)
                return;

            var detailDescriptor = GridControl.DetailDescriptor as DataControlDetailDescriptor;
            if (detailDescriptor == null)
                return;

            TableView view = GridControl.View as TableView;

            if(view != null)
            {
                operands = new List<OperandProperty>();
                foreach(GridColumn column in GridControl.Columns)
                {
                    operands.Add(new OperandProperty(column.FieldName));
                }

                GridControl.SubstituteFilter += GridControl_SubstituteFilter;
                GridControl.MasterRowExpanded += GridControl_MasterRowExpanded;
            }
        }

        public ObservableCollection<Misc.GroupInfo> GetExpansionState()
        {
            GridControlEx gridControlEx = GridControl as GridControlEx;
            if(gridControlEx != null)
            {
                return gridControlEx.States;
            }

            return new ObservableCollection<Misc.GroupInfo>();
        }

        public void SetExpansionState(ObservableCollection<Misc.GroupInfo> states)
        {
            GridControlEx gridControlEx = GridControl as GridControlEx;
            if (gridControlEx != null)
            {
                gridControlEx.States = states;
            }
        }

        private void GridControl_SubstituteFilter(object sender, DevExpress.Data.SubstituteFilterEventArgs e)
        {
            TableView view = GridControl.View as TableView;
            if (string.IsNullOrEmpty(view.SearchString))
                return;
            e.Filter = new GroupOperator(GroupOperatorType.Or, e.Filter, GetDetailFilter(view.SearchString));
        }

        private void GridControl_MasterRowExpanded(object sender, RowEventArgs e)
        {
            var detailView = GetDetailView(e.RowHandle);
            if (detailView == null)
                return;

            TableView view = GridControl.View as TableView;
            detailView.ShowSearchPanelMode = ShowSearchPanelMode.Never;
            BindingOperations.SetBinding(detailView, DataViewBase.SearchStringProperty, new Binding("SearchString") { Source = view });
        }

        //Unstable
        public void ExpandAllMasterRows()
        {
            for (int i = 0; i < GridControl.VisibleRowCount; i++)
            {
                var handle = GridControl.GetRowHandleByVisibleIndex(i);
                GridControl.ExpandMasterRow(handle);
            }
        }

        //Unstable
        public void CollapseAllMasterRows()
        {
            for (int i = 0; i < GridControl.VisibleRowCount; i++)
            {
                var handle = GridControl.GetRowHandleByVisibleIndex(i);
                GridControl.CollapseMasterRow(handle);
            }
        }

        List<OperandProperty> operands;
        AggregateOperand GetDetailFilter(string searchString)
        {
            GroupOperator detailOperator = new GroupOperator(GroupOperatorType.Or);
            foreach (var op in operands)
                detailOperator.Operands.Add(new FunctionOperator(FunctionOperatorType.Contains, op, new OperandValue(searchString)));
            return new AggregateOperand("DetailEntities", Aggregate.Exists, detailOperator);
        }

        TableView GetDetailView(int rowHandle)
        {
            if (GridControl == null)
                return null;

            GridControl masterGrid = GridControl;
            var detail = masterGrid.GetDetail(rowHandle) as GridControl;
            return detail == null ? null : detail.View as TableView;
        }

        public CriteriaOperator FilterCriteria
        {
            get
            {
                return GridControl == null ? null : GridControl.FilterCriteria;
            }
            set
            {
                if (GridControl == null)
                    return;

                GridControl.FilterCriteria = value;
            }
        }

        public void BeginDataUpdate()
        {
            if (GridControl == null)
                return;

            GridControl.BeginDataUpdate();
        }

        public void EndDataUpdate()
        {
            if (GridControl == null)
                return;

            GridControl.EndDataUpdate();
        }

        public void ClearFilterCriteria()
        {
            if (GridControl == null)
                return;

            GridControl.FilterCriteria = null;
        }
        public void SetExcelFilterPopUpMode()
        {
            if (GridControl == null)
                return;

            foreach (GridColumn grid_column in GridControl.Columns)
            {
                DateEditSettings dateEditSettings = grid_column.EditSettings as DateEditSettings;
                if (dateEditSettings != null)
                {
                    grid_column.FilterPopupMode = FilterPopupMode.DateSmart;
                }

                SpinEditSettings spinEditSettings = grid_column.EditSettings as SpinEditSettings;
                if(spinEditSettings != null)
                {
                    grid_column.CustomColumnFilterPopupTemplate = Application.Current.Resources["RangeFilterTemplate"] as DataTemplate;
                }
            }
        }

        public void ClearSorting()
        {
            if (GridControl == null)
                return;

            GridControl.ClearSorting();
        }

        public void SortBy(string fieldName)
        {
            if (GridControl == null)
                return;

            GridControl.SortBy(fieldName);
        }

        public void ClearGrouping()
        {
            if (GridControl == null)
                return;

            GridControl.ClearGrouping();
        }

        public void GroupBy(string fieldName)
        {
            if (GridControl == null)
                return;

            GridControl.GroupBy(fieldName);
        }

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

        public IEnumerable<object> GetVisibleRowObjects()
        {
            List<object> visible_row_object = new List<object>();
            GridControl.ExpandAllGroups();
            for (int i = 0; i < GridControl.VisibleRowCount; i++)
            {
                if(!GridControl.IsGroupRowHandle(GridControl.GetRowHandleByVisibleIndex(i)))
                {
                    object dataRow = GridControl.GetRow(GridControl.GetRowHandleByVisibleIndex(i));
                    visible_row_object.Add(dataRow);
                }
            }
            return visible_row_object;
        }

        public void ClearSummary()
        {
            GridControl.TotalSummary.Clear();
            GridControl.GroupSummary.Clear();
        }

        public void AddSummary(string fieldName, SummaryItemType summaryType, string displayFormat)
        {
            GridSummaryItem gridSummaryItem = new GridSummaryItem() { FieldName = fieldName, SummaryType = summaryType, DisplayFormat = displayFormat };
            if(!gridSummaryItemExists(GridControl.TotalSummary, fieldName))
                GridControl.TotalSummary.Add(gridSummaryItem);

            if (!gridSummaryItemExists(GridControl.GroupSummary, fieldName))
                GridControl.GroupSummary.Add(gridSummaryItem);
        }

        private bool gridSummaryItemExists(GridSummaryItemCollection gridSummaryItems, string fieldName)
        {
            for(int i = 0;i < gridSummaryItems.Count;i++)
            {
                if (gridSummaryItems[i].FieldName == fieldName)
                    return true;
            }

            return false;
        }

        public void ChangeSummary(string oldFieldName, string newFieldName)
        {
            int findSummaryIndex = this.GridControl.TotalSummary.FindIndex(x => x.FieldName == oldFieldName);
            if(findSummaryIndex != -1)
            {
                var findSummary = this.GridControl.TotalSummary[findSummaryIndex];
                findSummary.FieldName = newFieldName;
            }
        }

        public void ExpandAllGroups()
        {
            if (GridControl == null)
                return;

            GridControl.ExpandAllGroups();
        }

        public void CollapseAllGroups()
        {
            if (GridControl == null)
                return;

            GridControl.CollapseAllGroups();
        }

        public GridColumnCollection GridColumns()
        {
            if (GridControl == null)
                return null;

            return GridControl.Columns;
        }

        public void RefreshData()
        {
            if (GridControl == null)
                return;
            
            GridControl.RefreshData();
        }

        public void RefreshRowByListIndex(int listIndex)
        {
            if (GridControl == null)
                return;

            int rowHandle = GridControl.GetRowHandleByListIndex(listIndex);
            GridControl.RefreshRow(rowHandle);
        }

        public void CopyWithHeader()
        {
            if (GridControl == null)
                return;

            GridControl.ClipboardCopyMode = ClipboardCopyMode.IncludeHeader;
            GridControl.CopySelectedItemsToClipboard();
            GridControl.ClipboardCopyMode = ClipboardCopyMode.ExcludeHeader;
        }

        public void RefreshSummary()
        {
            if (GridControl == null)
                return;

            GridControl.UpdateGroupSummary();
            GridControl.UpdateTotalSummary();
        }

        public void MasterDetail_ExpandAll()
        {
            if (GridControl == null)
                return;

            int dataRowCount = GridControl.VisibleRowCount - 1;
            for (int rowHandle = 0; rowHandle < dataRowCount; rowHandle++)
                GridControl.ExpandMasterRow(rowHandle);
        }

        public void MasterDetail_CollapseAllButThis()
        {
            if (GridControl == null)
                return;

            int dataRowCount = GridControl.VisibleRowCount - 1;
            DataViewBase view = GridControl.View;
            if (view.FocusedRowHandle >= 0)
                GridControl.ExpandMasterRow(view.FocusedRowHandle);
            for (int rowHandle = 0; rowHandle < dataRowCount; rowHandle++)
            {
                if (rowHandle != view.FocusedRowHandle)
                    GridControl.CollapseMasterRow(rowHandle);
            }
        }

        public void MasterDetail_CollapseAll()
        {
            if (GridControl == null)
                return;

            int dataRowCount = GridControl.VisibleRowCount - 1;
            for (int rowHandle = 0; rowHandle < dataRowCount; rowHandle++)
                GridControl.CollapseMasterRow(rowHandle);
        }

        DependencyPropertyKey HighlightedTextPropertyKey;
        public void HighlightIncorrectText(SpellChecker spellChecker)
        {
            foreach (GridColumn column in GridControl.Columns)
            {
                var editor = column.ActualEditSettings;
                if (editor.GetType() == typeof(TextEditSettings))
                {
                    TextEditSettings textEditor = editor as TextEditSettings;
                    for (int i = 0; i < GridControl.VisibleRowCount; i++)
                    {
                        int rowHandle = GridControl.GetRowHandleByVisibleIndex(i);
                        object cellValue = GridControl.GetCellValue(rowHandle, column);
                        if (cellValue == null)
                            continue;

                        string cellText = cellValue.ToString();
                        List<WrongWordRecord> wrongWords = spellChecker.CheckText(cellText);

                        if (wrongWords.Count > 0)
                        {
                            var fieldInfo = typeof(TextEdit).GetField("HighlightedTextPropertyKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                            HighlightedTextPropertyKey = fieldInfo.GetValue(textEditor) as DependencyPropertyKey;

                            fieldInfo = typeof(TextEdit).GetField("HighlightedTextCriteriaPropertyKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                            DependencyPropertyKey HighlightedTextCriteriaPropertyKey = fieldInfo.GetValue(textEditor) as DependencyPropertyKey;

                            textEditor.SetValue(HighlightedTextPropertyKey, wrongWords.First().Word);
                            textEditor.SetValue(HighlightedTextCriteriaPropertyKey, HighlightedTextCriteria.Contains);
                        }
                    }
                }
            }
        }
    }
}
