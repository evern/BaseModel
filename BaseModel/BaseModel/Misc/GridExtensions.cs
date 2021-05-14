using DevExpress.Utils.Serializing;
using DevExpress.Xpf.Core.Serialization;
using DevExpress.Xpf.Grid;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows;
using System.Threading.Tasks;
using DevExpress.Data.Async.Helpers;
using DevExpress.Data;

namespace BaseModel.Misc
{
    public delegate void CancelledEventHandler(object sender, CancelledEventArgs e);
    public class InstantFeedbackTableView : TableView
    {
        public event CancelledEventHandler FocusedRowChanging;
        public InstantFeedbackTableView()
        {

        }

        static InstantFeedbackTableView()
        {
            TopRowIndexProperty.OverrideMetadata(typeof(InstantFeedbackTableView), new FrameworkPropertyMetadata(GridControl.InvalidRowHandle, null, (d, e) => ((InstantFeedbackTableView)d).CoerceTopRowIndex((int)e)));
            FocusedRowHandleProperty.OverrideMetadata(typeof(InstantFeedbackTableView), new FrameworkPropertyMetadata(GridControl.InvalidRowHandle, null, (d, e) => ((InstantFeedbackTableView)d).CoerceFocusedRowHandle((int)e)));
        }

        /// <summary>
        /// In AsyncOperation start return old top row index
        /// </summary>
        object CoerceTopRowIndex(int value)
        {
            if (TopRowIndex == value)
                return value;

            if (FocusedRowChanging != null)
            {
                CancelledEventArgs e = new CancelledEventArgs(TopRowIndex, value);
                FocusedRowChanging(this, e);
                if (e.Cancel)
                    return TopRowIndex; //TopRowIndex is old value, return old value during cancellation (AsyncOperation)
            }

            return value;
        }

        object CoerceFocusedRowHandle(int value)
        {
            if (FocusedRowHandle == value) 
                return value;

            if (FocusedRowChanging != null)
            {
                CancelledEventArgs e = new CancelledEventArgs(FocusedRowHandle, value);
                FocusedRowChanging(this, e);
                if (e.Cancel)
                    //because Grid_CustomUnboundColumn doesn't call IsSetData when CurrentItem is the same after editing, it however always works on first visible row
                    return 0;
                    //when the cause is determined the following can be restored
                    //return FocusedRowHandle; //FocusedRowHandle is old value, return old value during cancellation (AsyncOperation)
            }
            return value;
        }
    }

    public class CancelledEventArgs : EventArgs
    {
        public int NewRowHandle { get; private set; }
        public int OldRowHandle { get; private set; }
        public bool Cancel { get; set; }

        public CancelledEventArgs(int oldRowHandle, int newRowHandle)
        {
            OldRowHandle = oldRowHandle;
            NewRowHandle = newRowHandle;
            Cancel = false;
        }
    }

    public class GridControlEx : GridControl
    {
        public bool IsInstantFeedbackMode { get; set; }

        public GridControlEx()
        {
            AsyncOperationCompleted += GridControlEx_AsyncOperationCompleted;
        }

        static GridControlEx()
        {
        }

        ObservableCollection<GroupInfo> tempGroupStates;
        public void SaveExpansionStates()
        {
            tempGroupStates = GetGroupStates();

        }
        private void GridControlEx_AsyncOperationCompleted(object sender, RoutedEventArgs e)
        {
            if(IsInstantFeedbackMode && tempGroupStates != null)
            {
                States = tempGroupStates;
                tempGroupStates = null;
            }
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

        void grid_ItemsSourceChanged(object sender, ItemsSourceChangedEventArgs e)
        {
            ItemsSourceChanged -= grid_ItemsSourceChanged;
            if (IsLoaded)
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    View.FocusedRowHandle = 0;
                }), System.Windows.Threading.DispatcherPriority.Render, null);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    View.FocusedRowHandle = 0;
                }), System.Windows.Threading.DispatcherPriority.Loaded, null);
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

        public TableViewEx()
        {
            //this.PreviewKeyDown += TableViewEx_PreviewKeyDown;
            //this.ShownEditor += TableViewEx_ShownEditor;
            //this.HiddenEditor += TableViewEx_HiddenEditor;
        }

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
