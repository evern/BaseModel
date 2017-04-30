using DevExpress.Xpf.Core.Serialization;
using DevExpress.Xpf.Grid;

namespace BaseModel.Misc
{
    public class GridControlEx : GridControl
    {
        public GridControlEx()
        {
            this.AddHandler(DXSerializer.AllowPropertyEvent,
                    new AllowPropertyEventHandler(grid_AllowedProperty));
        }

        void grid_AllowedProperty(object sender, AllowPropertyEventArgs e)
        {
            e.Allow = e.DependencyProperty != GridControl.FilterStringProperty 
                   && e.DependencyProperty != GridControl.GroupSummarySourceProperty 
                   && e.DependencyProperty != GridControl.TotalSummarySourceProperty;
        }
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
}
