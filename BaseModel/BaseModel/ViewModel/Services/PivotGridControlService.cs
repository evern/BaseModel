using BaseModel.Data.Helpers;
using BaseModel.Misc;
using DevExpress.Data;
using DevExpress.Data.Extensions;
using DevExpress.Data.Filtering;
using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Editors.Settings;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.PivotGrid;
using DevExpress.Xpf.SpellChecker;
using DevExpress.XtraSpellChecker;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace BaseModel.ViewModel.Services
{
    public interface IPivotGridControlService
    {
        void ExportToExcel(string filePath);
    }

    public class PivotGridControlService : ServiceBase, IPivotGridControlService
    {
        public PivotGridControl PivotGridControl
        {
            get { return (PivotGridControl)GetValue(PivotGridControlProperty); }
            set { SetValue(PivotGridControlProperty, value); }
        }
        
        public static readonly DependencyProperty PivotGridControlProperty =
            DependencyProperty.Register("PivotGridControl", typeof(PivotGridControl), typeof(PivotGridControlService), new PropertyMetadata(null));

        public void ExportToExcel(string filePath)
        {
            PivotGridControl.ExportToXlsx(filePath);
        }
    }
}
