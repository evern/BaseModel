using DevExpress.Mvvm;
using DevExpress.Mvvm.UI.Interactivity;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BaseModel.Misc
{
    public delegate void SaveChangesEventHandler(object sender, GridColumnDataEventArgs e);

    public class EditingAttachedBehavior : Behavior<GridControl>
    {
        public GridControl Grid { get { return AssociatedObject; } }
        public TableView View { get { return (TableView)Grid.View; } }

        public event SaveChangesEventHandler SaveChanges;
        protected override void OnAttached()
        {
            base.OnAttached();
            Grid.CustomUnboundColumnData += new GridColumnDataEventHandler(Grid_CustomUnboundColumnData);
            View.ShownEditor += new EditorEventHandler(View_ShownEditor);
            View.CellValueChanging += new CellValueChangedEventHandler(View_CellValueChanging);
        }

        void View_CellValueChanging(object sender, CellValueChangedEventArgs e)
        {
            if (View.ActiveEditor != null && e.Column is EditableColumn)
                View.ActiveEditor.IsReadOnly = false;
        }
        protected override void OnDetaching()
        {
            Grid.CustomUnboundColumnData -= new GridColumnDataEventHandler(Grid_CustomUnboundColumnData);
            View.ShownEditor -= new EditorEventHandler(View_ShownEditor);
            View.CellValueChanging -= new CellValueChangedEventHandler(View_CellValueChanging);

            base.OnDetaching();
        }
        void View_ShownEditor(object sender, EditorEventArgs e)
        {
            if (e.Column is EditableColumn)
                e.Editor.IsReadOnly = false;
        }
        void Grid_CustomUnboundColumnData(object sender, GridColumnDataEventArgs e)
        {
            EditableColumn column = e.Column as EditableColumn;
            
            if (column == null) return;
            if (e.IsGetData)
            {
                var value = e.GetListSourceFieldValue(column.RealFieldName);
                e.Value = value;
                return;
            }
            if (e.IsSetData)
            {
                if (SaveChanges != null)
                    SaveChanges(this, e);
                return;
            }
        }
    }
    public class EditableColumn : GridColumn
    {
        public static readonly DependencyProperty RealFieldNameProperty =
            DependencyProperty.Register("RealFieldName", typeof(string), typeof(EditableColumn), new PropertyMetadata(string.Empty));
        public string RealFieldName
        {
            get { return (string)GetValue(RealFieldNameProperty); }
            set { SetValue(RealFieldNameProperty, value); }
        }
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            FieldName = RealFieldName + "_Editable";
        }
    }
}
