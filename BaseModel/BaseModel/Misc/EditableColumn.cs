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

    public class EditingAttachedBehavior : Behavior<GridControlEx>
    {
        public GridControlEx Grid { get { return AssociatedObject; } }
        public InstantFeedbackTableView View { 
            get 
            {
                if (Grid == null) return null;
                return (InstantFeedbackTableView)Grid.View; 
            } 
        }

        public event SaveChangesEventHandler SaveChanges;
        protected override void OnAttached()
        {
            base.OnAttached();
            if (!Grid.IsInstantFeedbackMode)
                return;

            Grid.CustomUnboundColumnData += new GridColumnDataEventHandler(Grid_CustomUnboundColumnData);
            Grid.AsyncOperationStarted += new RoutedEventHandler(GridControl_AsyncOperationStarted);
            Grid.AsyncOperationCompleted += new RoutedEventHandler(GridControl_AsyncOperationCompleted);
            View.ShownEditor += new EditorEventHandler(View_ShownEditor);
            View.CellValueChanging += new CellValueChangedEventHandler(View_CellValueChanging);
            View.CellValueChanged += new CellValueChangedEventHandler(View_CellValueChanged);
            View.FocusedRowChanging += new CancelledEventHandler(View_FocusedRowChanging);
            View.HiddenEditor += new EditorEventHandler(View_HiddenEditor);
        }

        private void View_HiddenEditor(object sender, EditorEventArgs e)
        {
            if (View == null)
                return;

            View.PostEditor();
        }

        bool cancellationFlag = false;
        private void GridControl_AsyncOperationStarted(object sender, RoutedEventArgs e)
        {
            cancellationFlag = true;
        }
        private void View_FocusedRowChanging(object sender, CancelledEventArgs e)
        {
            e.Cancel = cancellationFlag;
        }

        private void GridControl_AsyncOperationCompleted(object sender, RoutedEventArgs e)
        {
            cancellationFlag = false;
        }

        void View_CellValueChanging(object sender, CellValueChangedEventArgs e)
        {
            if (View == null)
                return;

            if (View.ActiveEditor != null && e.Column is EditableColumn)
            {
                View.ActiveEditor.IsReadOnly = false;
            }
        }

        void View_CellValueChanged(object sender, CellValueChangedEventArgs e)
        {
            if (View == null)
                return;

            if (View.ActiveEditor != null && e.Column is EditableColumn)
            {
                if (SaveChanges != null)
                {
                    SaveChanges(this, saveEvent);
                }
            }
        }

        protected override void OnDetaching()
        {
            Grid.CustomUnboundColumnData -= new GridColumnDataEventHandler(Grid_CustomUnboundColumnData);
            Grid.AsyncOperationStarted -= new RoutedEventHandler(GridControl_AsyncOperationStarted);
            Grid.AsyncOperationCompleted -= new RoutedEventHandler(GridControl_AsyncOperationCompleted);
            View.ShownEditor -= new EditorEventHandler(View_ShownEditor);
            View.CellValueChanging -= new CellValueChangedEventHandler(View_CellValueChanging);
            View.CellValueChanged -= new CellValueChangedEventHandler(View_CellValueChanged);
            View.FocusedRowChanging -= new CancelledEventHandler(View_FocusedRowChanging);
            View.HiddenEditor -= new EditorEventHandler(View_HiddenEditor);

            base.OnDetaching();
        }

        void View_ShownEditor(object sender, EditorEventArgs e)
        {
            if (e.Column is EditableColumn)
                e.Editor.IsReadOnly = false;
        }

        GridColumnDataEventArgs saveEvent;
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
                saveEvent = e;
                //if (SaveChanges != null)
                //    SaveChanges(this, e);
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
