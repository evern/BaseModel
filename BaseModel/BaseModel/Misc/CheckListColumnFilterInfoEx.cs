using BaseModel.Data.Helpers;
using DevExpress.Data.Filtering;
using DevExpress.Mvvm;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Editors.Settings;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Grid.LookUp;
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
        bool _useSecondMethod = false;
        public CheckListColumnFilterInfoEx(ColumnBase column, bool useSecondMethod = false)
            : base(column)
        {
            //second method filter does filtering by display text
            _useSecondMethod = useSecondMethod;
        }

        protected override void AfterPopupOpening(PopupBaseEdit popup)
        {
            ComboBoxEdit comboBox = (ComboBoxEdit)popup;
            comboBox.PopupContentSelectionChanged += new SelectionChangedEventHandler(PopupListBoxSelectionChanged);
            RecreateSelectedItems(comboBox);
        }

        protected override List<object> GetDefaultItems(bool addShowAllItem)
        {
            List<object> defaultItems = new List<object>();
            //defaultItems.Add(new CustomComboBoxItem() { DisplayValue = "(Blanks)", EditValue = null });
            defaultItems.Add(new CustomComboBoxItem() { DisplayValue = "(Blanks)", EditValue = new FunctionOperator(FunctionOperatorType.IsNullOrEmpty, new OperandProperty(Column.FieldName)) });
            defaultItems.Add(new CustomComboBoxItem() { DisplayValue = "(Non blanks)", EditValue = new FunctionOperator(FunctionOperatorType.IsNullOrEmpty, new OperandProperty(Column.FieldName)).Not() });
            return defaultItems;
        }

        protected override void UpdatePopupData(PopupBaseEdit popup, object[] values)
        {   
            ComboBoxEdit comboBox = (ComboBoxEdit)popup;
            Type editSettingsType = base.Column.ActualEditSettings.GetType();
            object editSettings = null;
            if (editSettingsType == typeof(ComboBoxEditSettings))
                editSettings = base.Column.ActualEditSettings as ComboBoxEditSettings;
            else if (editSettingsType == typeof(LookUpEditSettings))
                editSettings = base.Column.ActualEditSettings as LookUpEditSettingsBase;

            List<object> items = new List<object>();
            bool addDefaultFilters = false;
            if (editSettings != null)
            {
                var copyColumnItemsSource = (IEnumerable<object>)editSettings.GetType().GetProperty("ItemsSource").GetValue(editSettings);
                if (copyColumnItemsSource != null && copyColumnItemsSource.First().GetType() == typeof(EnumMemberInfo))
                    defaultMethod(addDefaultFilters, items);
                else
                {
                    Dictionary<object, string> columnValues = GetColumnValues(copyColumnItemsSource, comboBox.DisplayMember, comboBox.ValueMember, out addDefaultFilters);
                    if (addDefaultFilters)
                        items.AddRange(GetDefaultItems(true));

                    if (columnValues != null)
                    {
                        foreach (KeyValuePair<object, string> value in columnValues.OrderBy(x => x.Key))
                        {
                            items.Add(new CustomComboBoxItem() { DisplayValue = value.Value, EditValue = value.Key });
                        }
                    }
                }
            }
            else
                defaultMethod(addDefaultFilters, items);
			
            comboBox.ItemsPanel = FilterPopupVirtualizingStackPanel.GetItemsPanelTemplate(items.Count);//GetItemsPanel(items.Count);
            comboBox.ItemsSource = items;
            RecreateSelectedItems(comboBox);
        }

        private void defaultMethod(bool addDefaultFilters, List<object> items)
        {
            List<object> columnValues = GetColumnValues(out addDefaultFilters);
            if (addDefaultFilters)
                items.AddRange(GetDefaultItems(true));

            foreach (object value in columnValues.OrderBy(x => x.ToString()))
            {
                items.Add(new CustomComboBoxItem() { DisplayValue = value, EditValue = value });
            }
        }

        private List<object> GetColumnValues(out bool addDefaultFilters)
        {
            addDefaultFilters = false;
            List<object> result = new List<object>();
            GridControl grid = View.DataControl as GridControl;
            IList list = grid.ItemsSource as IList;
            for (int i = 0; i < list.Count; i++)
            {
                int rowHandle = grid.GetRowHandleByListIndex(i);
                object value = grid.GetCellValue(rowHandle, Column.FieldName);
                if (value != null)
                {
                    if (!result.Contains(value))
                        result.Add(value);
                }
                else
                    addDefaultFilters = true;

            }
            return result;
        }

        private Dictionary<object, string> GetColumnValues(IEnumerable itemSource, string displayMember, string valueMember, out bool addDefaultFilters)
        {
            addDefaultFilters = false;
            if (itemSource == null)
                return null;

            try
            {
                Dictionary<object, string> result = new Dictionary<object, string>();
                GridControl grid = View.DataControl as GridControl;
                IList list = grid.ItemsSource as IList;
                List<object> columnValues = GetColumnValues(out addDefaultFilters);
                foreach (var listItem in list)
                {
                    object value = DataUtils.GetNestedValue(Column.FieldName, listItem);
                    if (value != null)
                    {
                        if(columnValues.Any(x => x.ToString() == value.ToString()))
                        {
                            string itemDisplay = getItemSourceDisplayMember(itemSource, displayMember, valueMember, value.ToString());
                            if (_useSecondMethod)
                            {
                                if (itemDisplay != string.Empty && !result.Any(x => x.Key.ToString() == itemDisplay.ToString()) && itemDisplay != null)
                                    result.Add(itemDisplay, itemDisplay);
                            }
                            else if (itemDisplay != string.Empty && !result.Any(x => x.Key.ToString() == value.ToString()) && itemDisplay != null)
                                result.Add(value, itemDisplay);
                        }
                    }
                    else
                        addDefaultFilters = true;
                }

                return result;
            }
            catch(Exception e)
            {
                string s = e.ToString();
                return null;
            }
        }

        private string getItemSourceDisplayMember(IEnumerable itemSource, string displayMember, string valueMember, string currentValue)
        {
            try
            {
                foreach (var item in itemSource)
                {
                    object value = item.GetType().GetProperty(valueMember).GetValue(item);
                    if (value.ToString() == currentValue)
                        return item.GetType().GetProperty(displayMember).GetValue(item).ToString();
                }
            }
            catch(Exception e)
            {
                string s = e.ToString();
            }

            return string.Empty;
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

                if(editor.SelectedItems.Count > 1)
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

        internal void UpdateColumnFilterIfNeeded(CriteriaOperator op)
        {
            if (Equals(op, null))
                return;

            if (((GridControl)((GridColumn)Column).View.DataControl).FilterString != string.Empty)
            {
                string currentCriteria = removePreviousFilter(Column.FieldName, ((GridControl)((GridColumn)Column).View.DataControl).FilterCriteria.ToString());
                if (currentCriteria != string.Empty)
                    currentCriteria += " And " + op.ToString();
                else
                    currentCriteria = op.ToString();

                try
                {
                    ((GridControl)((GridColumn)Column).View.DataControl).FilterCriteria = CriteriaOperator.Parse(currentCriteria);
                }
                catch(Exception e)
                {
                    string s = e.ToString();
                    ((GridControl)((GridColumn)Column).View.DataControl).FilterString = string.Empty;
                }
            }
            else
                ((GridControl)((GridColumn)Column).View.DataControl).FilterCriteria = op;
        }

        private string removePreviousFilter(string columnName, string currentOperatorString)
        {
            string newOperator = string.Empty;
            List<string> operatorArr = currentOperatorString.Split(new string[] { " And " }, StringSplitOptions.None).ToList();
            foreach(string op in operatorArr)
            {
                if (!op.Contains(columnName))
                    newOperator += op + " And ";
            }

            if (newOperator.Contains(" And "))
                return newOperator.Substring(0, newOperator.Length - 5);
            else
                return string.Empty;
        }
    }
}
