using BaseModel.Data.Helpers;
using BaseModel.Misc;
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

namespace BaseModel.ViewModel.Services
{
    public interface IGridControlService
    {
        void SetRowExpandedByColumnValue(string field_name, IHaveExpandState row);
        void RefreshSummary();
        IEnumerable<object> GetVisibleRowObjects();
        void HighlightIncorrectText(SpellChecker spellChecker);
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

        public void RefreshSummary()
        {
            if (GridControl == null)
                return;

            GridControl.UpdateGroupSummary();
            GridControl.UpdateTotalSummary();
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
