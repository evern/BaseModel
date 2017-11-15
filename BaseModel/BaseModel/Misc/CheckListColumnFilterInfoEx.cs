using DevExpress.Data.Filtering;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace BaseModel.Misc
{
    public class CheckListColumnFilterInfoEx : CheckedListColumnFilterInfo
    {
        public CheckListColumnFilterInfoEx(ColumnBase column)
            : base(column)
        {
        }

        protected override void AfterPopupOpening(PopupBaseEdit popup)
        {
            //base.AfterPopupOpening(popup);
            ComboBoxEdit comboBox = (ComboBoxEdit)popup;
            comboBox.PopupContentSelectionChanged += new SelectionChangedEventHandler(PopupListBoxSelectionChanged);
            RecreateSelectedItems(comboBox);
        }

        protected override List<object> GetDefaultItems(bool addShowAllItem)
        {
            List<object> defaultItems = new List<object>();
            defaultItems.Add(new CustomComboBoxItem() { DisplayValue = "(Blanks)", EditValue = null });
            defaultItems.Add(new CustomComboBoxItem() { DisplayValue = "(Non blanks)", EditValue = new CustomComboBoxItem() { EditValue = new FunctionOperator(FunctionOperatorType.IsNullOrEmpty, new OperandProperty(Column.FieldName)).Not() } });
            return defaultItems;
        }

        protected override void UpdatePopupData(PopupBaseEdit popup, object[] values)
        {   //((IDataProviderOwner)Column.View.DataControl).
            //base.UpdatePopupData(popup, values);
            ComboBoxEdit comboBox = (ComboBoxEdit)popup;
            List<object> items = new List<object>();
            items.AddRange(GetDefaultItems(true));
            List<object> columnValues = GetColumnValues();
            foreach (object value in columnValues)
            {
                items.Add(new CustomComboBoxItem() { DisplayValue = value, EditValue = value });
            }
            //items.AddRange(values);			
            comboBox.ItemsPanel = FilterPopupVirtualizingStackPanel.GetItemsPanelTemplate(items.Count);//GetItemsPanel(items.Count);
            comboBox.ItemsSource = items;
            RecreateSelectedItems(comboBox);
        }

        private List<object> GetColumnValues()
        {
            List<object> result = new List<object>();
            GridControl grid = View.DataControl as GridControl;
            IList list = grid.ItemsSource as IList;
            for (int i = 0; i < list.Count; i++)
            {
                int rowHandle = grid.GetRowHandleByListIndex(i);
                object value = grid.GetCellValue(rowHandle, Column.FieldName);
                if (!result.Contains(value) && value != null)
                    result.Add(value);
            }
            return result;
        }

        protected override void ClearPopupData(PopupBaseEdit popup)
        {
            base.ClearPopupData(popup);
            ((ComboBoxEdit)popup).PopupContentSelectionChanged -= new SelectionChangedEventHandler(PopupListBoxSelectionChanged);
        }

        List<object> SelectedItems = new List<object>();

        void PopupListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxEdit editor = (ComboBoxEdit)sender;
            if (e.AddedItems.Count == (((IList)editor.ItemsSource)).Count)
            {
                editor.PopupContentSelectionChanged -= new SelectionChangedEventHandler(PopupListBoxSelectionChanged);
                editor.SelectedItems.Clear();
                SelectedItems.Clear();
                editor.SelectAllItems();
                editor.SelectedItems.RemoveAt(1);
                foreach (object obj in editor.SelectedItems)
                    SelectedItems.Add(obj);
                editor.PopupContentSelectionChanged += new SelectionChangedEventHandler(PopupListBoxSelectionChanged);
                UpdateColumnFilterIfNeeded(CreateInOperator(SelectedItems));
                return;
            }
            if (e.AddedItems != null && e.AddedItems.Count == 1 && e.AddedItems[0] is CustomComboBoxItem && ((CustomComboBoxItem)e.AddedItems[0]).EditValue is CustomComboBoxItem)
            {
                CustomComboBoxItem item = (CustomComboBoxItem)e.AddedItems[0];
                editor.PopupContentSelectionChanged -= new SelectionChangedEventHandler(PopupListBoxSelectionChanged);
                editor.SelectedItems.Clear();
                SelectedItems.Clear();
                editor.SelectedItems.Add(item);
                editor.PopupContentSelectionChanged += new SelectionChangedEventHandler(PopupListBoxSelectionChanged);
                UpdateColumnFilterIfNeeded((item.EditValue as CustomComboBoxItem).EditValue as CriteriaOperator);
                return;
            }

            foreach (object obj in e.RemovedItems)
                SelectedItems.Remove(obj);

            foreach (object obj in e.AddedItems)
                SelectedItems.Add(obj);
            UpdateColumnFilterIfNeeded(CreateInOperator(SelectedItems));
        }

        void RecreateSelectedItems(ComboBoxEdit comboBox)
        {
            comboBox.SelectedItems.Clear();
            SelectedItems.Clear();
            CriteriaOperator op = View.DataControl.GetColumnFilterCriteria(Column);
            if (Object.ReferenceEquals(op, null))
                return;
            if (op is UnaryOperator)
            {
                comboBox.SelectedIndex = 1;
                SelectedItems.Add(comboBox.SelectedItems[0]);
            }
            else
                if (op is InOperator)
                RecreateSelectedItemsCore(comboBox, (InOperator)op);
        }

        void RecreateSelectedItemsCore(ComboBoxEdit comboBox, InOperator op)
        {
            foreach (OperandValue opValue in op.Operands)
            {
                object item = FindItem((IEnumerable)comboBox.ItemsSource, opValue.Value);
                if (item != null)
                {
                    comboBox.SelectedItems.Add(item);
                    if (!SelectedItems.Contains(item))
                    {
                        SelectedItems.Add(item);
                    }
                }
            }
        }

        InOperator CreateInOperator(IEnumerable items)
        {
            List<CriteriaOperator> list = new List<CriteriaOperator>();
            foreach (object item in items)
            {
                list.Add(new OperandValue(GetValue(item)));
            }
            if (list.Count == 0)
                return null;
            InOperator op = new InOperator(new OperandProperty(Column.FieldName));
            op.Operands.AddRange(list);
            return op;
        }

        internal new void UpdateColumnFilterIfNeeded(CriteriaOperator op)
        {
            ((GridControl)((GridColumn)Column).View.DataControl).FilterCriteria = op;
        }
    }
}
