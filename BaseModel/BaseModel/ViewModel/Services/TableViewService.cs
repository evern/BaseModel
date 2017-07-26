using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BaseModel.ViewModel.Services
{
    public interface ITableViewService
    {
        bool ExportToXls(string exportPath);
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
    }
}
