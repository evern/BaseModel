using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Editors.Helpers;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BaseModel.Misc
{
public class PopupBaseEditEx : PopupBaseEdit {
        static PopupBaseEditEx() {
            AllowRecreatePopupContentProperty.OverrideMetadata(typeof(PopupBaseEditEx), new FrameworkPropertyMetadata(false));
            FocusPopupOnOpenProperty.OverrideMetadata(typeof(PopupBaseEditEx), new FrameworkPropertyMetadata(true));
        }

        GridControl grid;
        protected GridControl Grid {
            get {
                return grid;
            }
            set {
                if (grid != null) grid.PreviewKeyDown -= OnGridPreviewKeyDown;
                grid = value;
                if (grid != null) grid.PreviewKeyDown += OnGridPreviewKeyDown;
            }
        }
        TableView view;
        protected TableView View {
            get {
                return view;
            }
            set {
                if (view != null) view.RowDoubleClick -= OnRowDoubleClick;
                view = value;
                if (view != null) view.RowDoubleClick += OnRowDoubleClick;
            }
        }

        public string ValueMember { get; set; }

        public PopupBaseEditEx() {
            PopupOpened += OnPopupOpened;
            EditValueChanged += OnEditValueChanged;
            //PreviewKeyDown += OnEditorPreviewKeyDown;
        }

        void OnEditValueChanged(object sender, EditValueChangedEventArgs e) {
            UpdateGridSelection();
        }
        //void OnEditorPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
        //    //if(e.Key!=...)
        //    ShowPopup();
        //}
        void OnPopupOpened(object sender, RoutedEventArgs e) {
            Grid = LayoutTreeHelper.GetVisualChildren((DependencyObject)PopupBaseEditHelper.GetPopup(this).PopupContent).OfType<GridControl>().FirstOrDefault();
            View = (TableView)Grid.View;
            UpdateGridSelection();
        }
        void OnGridPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter)
                PostSelectedValueAndClose();
        }
        void OnRowDoubleClick(object sender, RowDoubleClickEventArgs e) {
            PostSelectedValueAndClose();
        }
        void UpdateGridSelection() {
            if (Grid == null)
                return;
            var rowHandle = Grid.FindRowByValue(ValueMember, EditValue);
            if (Grid.IsValidRowHandle(rowHandle))
                Grid.View.FocusedRowHandle = rowHandle;
        }
        void PostSelectedValueAndClose() {
            EditValue = ValueMember == null ? Grid.GetRow(Grid.View.FocusedRowHandle) : Grid.GetCellValue(Grid.View.FocusedRowHandle, ValueMember);
            ClosePopup();
            ((TextBox)EditCore).SelectAll();
        }
    }
}
