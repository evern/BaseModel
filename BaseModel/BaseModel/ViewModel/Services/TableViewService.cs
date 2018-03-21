using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace BaseModel.ViewModel.Services
{
    public interface ITableViewService
    {
        bool ExportToXls(string exportPath);
        void CommitEditing();
        void AddFormatCondition(FormatConditionBase item);
        void ApplyDefaultF2Behavior();
        void SetImmediateUpdateRowPosition(bool updatePositionImmediately);
        void ScrollToLast();
    }

    public class TableViewService : ServiceBase, ITableViewService
    {
        public TableView TableView
        {
            get { return (TableView)GetValue(TableViewProperty); }
            set { SetValue(TableViewProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Camera.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TableViewProperty =
            DependencyProperty.Register("TableView", typeof(TableView), typeof(TableViewService), new PropertyMetadata(null));

        public bool ExportToXls(string exportPath)
        {
            if (this.TableView != null)
            {
                try
                {
                    TableView.ExportToXlsx(exportPath);
                    Process.Start(exportPath);
                    return true;
                }
                catch
                {

                }
            }

            return false;
        }

        public void ScrollToLast()
        {
            if (this.TableView == null)
                return;

            var lastColumn = TableView.VisibleColumns != null ? TableView.VisibleColumns.LastOrDefault() : null;
            if (lastColumn != null)
                ((ITableView)TableView).TableViewBehavior.MakeColumnVisible(lastColumn);
        }

        public void ApplyDefaultF2Behavior()
        {
            if (this.TableView == null)
                return;

            TableView.HiddenEditor += TableView_HiddenEditor;
            TableView.PreviewKeyDown += TableView_PreviewKeyDown;
            TableView.PreviewMouseDown += TableView_PreviewMouseDown;
        }

        private void TableView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if(e.ChangedButton == MouseButton.Left)
            {
                string s = e.Source.ToString();
                TableView.AllowEditing = true;
                //TableView.ShowEditor();
            }
        }

        private void TableView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            //if (e.Key == Key.F2 || e.Key == Key.Return)
            //{
                TableView.AllowEditing = true;
                //TableView.ShowEditor();
            //}
        }

        private void TableView_HiddenEditor(object sender, EditorEventArgs e)
        {
            TableView.AllowEditing = false;
        }

        public void AddFormatCondition(FormatConditionBase item)
        {
            if (this.TableView == null)
                return;

            TableView.FormatConditions.Add(item);
        }

        public void CommitEditing()
        {
            if (this.TableView == null)
                return;

            TableView.CommitEditing();
            TableView.MoveNextRow();
        }

        public void SetImmediateUpdateRowPosition(bool updatePositionImmediately)
        {
            if (this.TableView == null)
                return;

            this.TableView.ImmediateUpdateRowPosition = updatePositionImmediately;
        }
    }
}
