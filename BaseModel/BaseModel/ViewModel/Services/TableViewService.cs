using DevExpress.Export;
using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Editors.Settings;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Printing;
using DevExpress.XtraPrinting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace BaseModel.ViewModel.Services
{
    public interface ITableViewService
    {
        bool ExportToPDF(string exportPath);
        bool ExportToXls(string exportPath, bool isDataAware);
        bool ExportToXls(MemoryStream stream);
        void CommitEditing();
        void AddFormatCondition(FormatConditionBase item);
        void ApplyDefaultF2Behavior();
        void SetImmediateUpdateRowPosition(bool updatePositionImmediately);
        void ScrollToLast();
        void ApplyBestFit();
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

        public bool ExportToXls(string exportPath, bool isDataAware = true)
        {
            if (this.TableView != null)
            {
                try
                {
                    TableView.ExportToXlsx(exportPath, new XlsxExportOptionsEx { ExportType = isDataAware ? ExportType.DataAware : ExportType.WYSIWYG });
                    Process.Start(exportPath);
                    return true;
                }
                catch
                {

                }
            }

            return false;
        }

        public bool ExportToXls(MemoryStream stream)
        {
            if (this.TableView != null)
            {
                try
                {
                    TableView.ExportToXlsx(stream);
                    return true;
                }
                catch
                {

                }
            }

            return false;
        }

        public bool ExportToPDF(string exportPath)
        {
            if (this.TableView != null)
            {
                try
                {
                    var link = new PrintableControlLink(TableView)
                    {
                        Margins = new System.Drawing.Printing.Margins(150, 150, 150, 150),
                        PaperKind = System.Drawing.Printing.PaperKind.A3Rotated
                    };

                    link.CreateDocument(true);
                    link.ExportToPdf(exportPath);

                    Process.Start(exportPath);
                    //TableView.PrintAutoWidth = true;
                    //PrintableControlLink link = new PrintableControlLink(TableView);
                    //link.Landscape = true;
                    //link.PaperKind = System.Drawing.Printing.PaperKind.A3;

                    //TableView.ExportToPdf(exportPath);
                    //Process.Start(exportPath);
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

        public void ApplyBestFit()
        {
            if (this.TableView == null)
                return;

            GridControl gridControl = (GridControl)TableView.Parent;
            TableView.BestFitMaxRowCount = 20;
            gridControl.BeginDataUpdate();
            foreach (var column in gridControl.Columns)
            {
                //for excelsmart filters to work
                var comboBoxEditSettings = column.EditSettings as ComboBoxEditSettings;
                if (comboBoxEditSettings != null)
                {
                    column.ColumnFilterMode = ColumnFilterMode.DisplayText;
                    comboBoxEditSettings.IncrementalFiltering = true;
                    comboBoxEditSettings.ValidateOnTextInput = true;
                }

                if (!column.Visible)
                    continue;

                if (column.AllowBestFit == DevExpress.Utils.DefaultBoolean.False)
                    continue;

                column.BestFitMode = DevExpress.Xpf.Core.BestFitMode.VisibleRows;
                double defaultMaxWidth = column.MaxWidth;
                double defaultMinWidth = column.MinWidth;
                column.MaxWidth = 250;
                column.MinWidth = 50;
                var textEditSetting = column.EditSettings as TextEditSettings;
                if (textEditSetting == null || textEditSetting.TextWrapping == TextWrapping.NoWrap)
                {
                    try
                    {
                        TableView.BestFitColumn(column);
                    }
                    catch
                    {

                    }
                }

                column.MaxWidth = defaultMaxWidth;
                column.MinWidth = defaultMinWidth;
            }

            gridControl.EndDataUpdate();
            //TableView.BestFitColumns();
        }

        public void SetImmediateUpdateRowPosition(bool updatePositionImmediately)
        {
            if (this.TableView == null)
                return;

            this.TableView.ImmediateUpdateRowPosition = updatePositionImmediately;
        }
    }
}
