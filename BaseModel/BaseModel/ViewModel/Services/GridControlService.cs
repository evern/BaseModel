using BaseModel.Data.Helpers;
using BaseModel.Misc;
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace BaseModel.ViewModel.Services
{
    public interface IGridControlService
    {
        void SetRowExpandedByColumnValue(string field_name, IHaveExpandState row);
        void RefreshSummary();
        void RefreshData();
        IEnumerable<object> GetVisibleRowObjects();
        void HighlightIncorrectText(SpellChecker spellChecker);
        void SetCheckedListFilterPopUpMode();
        void SetGridColumnSortMode();

        void MasterDetail_ExpandAll();
        void MasterDetail_CollapseAllButThis();
        void MasterDetail_CollapseAll();
        void CombineMasterDetailSearch();
        void SetFilterCriteria(string filter_string);
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

        public void SetCheckedListFilterPopUpMode()
        {
            if (GridControl == null)
                return;

            foreach(GridColumn grid_column in GridControl.Columns)
            {
                grid_column.FilterPopupMode = FilterPopupMode.CheckedList;
            }
        }

        public void SetGridColumnSortMode()
        {
            if (GridControl == null)
                return;

            foreach (GridColumn grid_column in GridControl.Columns)
            {
                grid_column.SortMode = DevExpress.XtraGrid.ColumnSortMode.DisplayText;
            }
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
            for (int i = 0; i < GridControl.VisibleRowCount; i++)
            {
                object dataRow = GridControl.GetRow(GridControl.GetRowHandleByVisibleIndex(i));
                visible_row_object.Add(dataRow);
            }
            return visible_row_object;
        }

        public void RefreshData()
        {
            if (GridControl == null)
                return;

            GridControl.RefreshData();
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
