using DevExpress.Utils.Serializing;
using DevExpress.Xpf.Core.Serialization;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.ObjectModel;

namespace BaseModel.Misc
{
    public class GridControlEx : GridControl
    {
        public GridControlEx()
        {
        }

        [XtraSerializableProperty]
        public ObservableCollection<GroupInfo> States
        {
            get { return GetGroupStates(); }
            set { Dispatcher.BeginInvoke(new Action(() => SaveGroupStates(value))); }
        }

        private void SaveGroupStates(ObservableCollection<GroupInfo> states)
        {
            foreach (var state in states)
            {
                if (!IsGroupRowHandle(state.RowHandle))
                    continue;
                if (state.State)
                    ExpandGroupRow(state.RowHandle);
                else
                    CollapseGroupRow(state.RowHandle);
            }
        }

        private ObservableCollection<GroupInfo> GetGroupStates()
        {
            var states = new ObservableCollection<GroupInfo>();
            for (int i = 0; i < VisibleRowCount; i++)
            {
                var handle = GetRowHandleByVisibleIndex(i);
                if (!IsGroupRowHandle(handle))
                    continue;
                states.Add(new GroupInfo { RowHandle = handle, State = IsGroupRowExpanded(handle) });
            }
            return states;
        }
    }

    [Serializable]
    public class GroupInfo
    {
        public int RowHandle { get; set; }
        public bool State { get; set; }
    }

    public class TableViewEx : TableView
    {
        //public bool isEditorActive;

        //public TableViewEx()
        //{
        //    this.PreviewKeyDown += TableViewEx_PreviewKeyDown;
        //    this.ShownEditor += TableViewEx_ShownEditor;
        //    this.HiddenEditor += TableViewEx_HiddenEditor;
        //}

        //private void TableViewEx_HiddenEditor(object sender, EditorEventArgs e)
        //{
        //    isEditorActive = false;
        //}

        //private void TableViewEx_ShownEditor(object sender, EditorEventArgs e)
        //{
        //    isEditorActive = true;
        //}

        //void TableViewEx_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        //{
        //    if (e.Key == Key.Enter)
        //    {
        //        Dispatcher.BeginInvoke(new Action(() =>
        //        {
        //            CommitEditing();
        //            MoveNextRow();
        //        }));
        //    }
        //}
    }

    public class GridColumnEx : GridColumn
    {
        protected override ColumnFilterInfoBase CreateColumnFilterInfo()
        {
            switch (FilterPopupMode)
            {
                case FilterPopupMode.List:
                    return new ListColumnFilterInfo(this);
                case FilterPopupMode.CheckedList:
                    return new CheckedListColumnFilterInfo(this);
                case FilterPopupMode.Custom:
                    return new CheckedListColumnFilterInfo(this);
                    //return new CheckListColumnFilterInfoEx(this, true);
                    //return new CustomColumnFilterInfo(this);
            }
            return null;
        }
    }
}
