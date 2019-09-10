using DevExpress.Utils;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interactivity;
using System.Windows.Threading;

namespace BaseModel.View.Behaviors
{
    public class DetailSyncBehavior : Behavior<GridControl>
    {
        private int onAttached = 0;
        private WidthAdjustmentConverter widthAdjustmentConverter = new WidthAdjustmentConverter();
        private double widthAdjustmentValue = 0.0f;

        private Dictionary<int, GridColumn> masterColumns = new Dictionary<int, GridColumn>();
        private Dictionary<int, GridColumn> detailColumns = new Dictionary<int, GridColumn>();

        private void SetColumnBinding(GridColumn masterCol, GridColumn detailCol)
        {
            DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).AddValueChanged(masterCol, OnMasterColumnVisibleChanged);
            DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).AddValueChanged(detailCol, OnDetailColumnVisibleChanged);
            detailCol.AllowResizing = DefaultBoolean.False;

            if (detailCol.VisibleIndex == 0)
                detailCol.SetBinding(GridColumn.WidthProperty, new Binding("ActualDataWidth") { Source = masterCol, Converter = widthAdjustmentConverter, ConverterParameter = widthAdjustmentValue });
            else
                detailCol.SetBinding(GridColumn.WidthProperty, new Binding("ActualDataWidth") { Source = masterCol });

            masterColumns[masterCol.VisibleIndex] = masterCol;
            detailColumns[detailCol.VisibleIndex] = detailCol;
        }

        private void RemoveColumnBinding(GridColumn masterCol, GridColumn detailCol)
        {
            if (masterCol != null)
                DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).RemoveValueChanged(masterCol, OnMasterColumnVisibleChanged);
            DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).RemoveValueChanged(detailCol, OnDetailColumnVisibleChanged);
            BindingOperations.ClearBinding(detailCol, GridColumn.WidthProperty);
            detailCol.AllowResizing = DefaultBoolean.Default;
        }

        private void SetWidthBindings()
        {
            TableView masterView = MasterGrid.View as TableView;
            TableView detailView = DetailGrid.View as TableView;

            foreach (GridColumn detailCol in detailView.VisibleColumns)
            {
                if (detailCol.VisibleIndex < 0 || detailCol.VisibleIndex >= masterView.VisibleColumns.Count)
                    continue;

                GridColumn masterCol = masterView.VisibleColumns[detailCol.VisibleIndex];
                SetColumnBinding(masterCol, detailCol);
            }
        }

        private void RemoveWidthBindings()
        {
            TableView detailView = DetailGrid.View as TableView;
            foreach (GridColumn detailCol in detailView.VisibleColumns)
            {
                RemoveColumnBinding(null, detailCol);
            }

            masterColumns.Clear();
            detailColumns.Clear();
        }

        private bool CheckNeedReset(GridColumn masterCol, GridColumn detailCol)
        {
            int key = detailCol.VisibleIndex;

            GridColumn storedMasterColumn = null;
            GridColumn storedDetailColumn = null;
            masterColumns.TryGetValue(key, out storedMasterColumn);
            detailColumns.TryGetValue(key, out storedDetailColumn);

            if (masterCol == null && storedMasterColumn != null)
            {
                DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).RemoveValueChanged(storedMasterColumn, OnMasterColumnVisibleChanged);
                return true;
            }

            if (storedDetailColumn == null)
                return true;

            if (storedMasterColumn == null)
                return true;

            if (masterCol.GetHashCode() != storedMasterColumn.GetHashCode() || detailCol.GetHashCode() != storedDetailColumn.GetHashCode())
            {
                DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).RemoveValueChanged(storedMasterColumn, OnMasterColumnVisibleChanged);
                DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).RemoveValueChanged(storedDetailColumn, OnDetailColumnVisibleChanged);
                return true;
            }

            return false;
        }

        private void ResetWidthBindings()
        {
            TableView masterView = MasterGrid.View as TableView;
            TableView detailView = DetailGrid.View as TableView;

            foreach (GridColumn detailCol in detailView.VisibleColumns)
            {
                GridColumn masterCol = null;
                if (detailCol.VisibleIndex >= 0 && detailCol.VisibleIndex < masterView.VisibleColumns.Count)
                    masterCol = masterView.VisibleColumns[detailCol.VisibleIndex];

                if (!CheckNeedReset(masterCol, detailCol))
                    continue;

                RemoveColumnBinding(masterCol, detailCol);
                if (masterCol != null)
                    SetColumnBinding(masterCol, detailCol);
            }
        }

        private void OnMasterColumnVisibleChanged(object sender, EventArgs e)
        {
            GridColumn masterColumn = (GridColumn)sender;
            if (!masterColumn.Visible)
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).RemoveValueChanged(masterColumn, OnMasterColumnVisibleChanged);
                    ResetWidthBindings();
                }), DispatcherPriority.Render);
            }
        }

        private void MasterGridColumns_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (onAttached != 0)
                return;

            if (e.Action == NotifyCollectionChangedAction.Reset)
                ResetWidthBindings();
        }

        private void OnDetailColumnVisibleChanged(object sender, EventArgs e)
        {
            GridColumn detailColumn = (GridColumn)sender;
            if (!detailColumn.Visible)
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).RemoveValueChanged(detailColumn, OnDetailColumnVisibleChanged);
                    BindingOperations.ClearBinding(detailColumn, GridColumn.WidthProperty);
                    ResetWidthBindings();
                }), DispatcherPriority.Render);
            }
        }

        private void DetailGrid_ItemsSourceChanged(object sender, ItemsSourceChangedEventArgs e)
        {
            if (onAttached == 0)
                ResetWidthBindings();
        }

        private void DetailGridColumns_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (onAttached != 0)
                return;

            if (e.Action == NotifyCollectionChangedAction.Reset)
                ResetWidthBindings();
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            DetailGrid.Dispatcher.BeginInvoke(new Action(() => {
                onAttached++;
                {
                    //MasterGrid = LayoutHelper.FindParentObject<GridControl>(DetailGrid.TemplatedParent);
                    var mainGrid = (DetailGrid.OwnerDetailDescriptor as DataControlDetailDescriptor).Parent;
                    MasterGrid = mainGrid as GridControl;
                    if(MasterGrid != null)
                    {
                        widthAdjustmentValue = (MasterGrid.View as TableView).ExpandDetailButtonWidth;
                        FrameworkElement content = LayoutHelper.FindElement(MasterGrid, (element) => element is ContentPresenter && element.Name == "content");
                        if (content != null)
                            widthAdjustmentValue += content.Margin.Left;
                        SetWidthBindings();
                        DetailGrid.ItemsSourceChanged += DetailGrid_ItemsSourceChanged;
                        DetailGrid.Columns.CollectionChanged += DetailGridColumns_CollectionChanged;
                        MasterGrid.Columns.CollectionChanged += MasterGridColumns_CollectionChanged;
                    }
                }
                onAttached--;
            }), DispatcherPriority.Render);
        }

        protected override void OnDetaching()
        {
            DetailGrid.ItemsSourceChanged -= DetailGrid_ItemsSourceChanged;
            DetailGrid.Columns.CollectionChanged -= DetailGridColumns_CollectionChanged;
            RemoveWidthBindings();
            MasterGrid.Columns.CollectionChanged -= MasterGridColumns_CollectionChanged;
            base.OnDetaching();
        }

        public GridControl DetailGrid
        {
            get { return AssociatedObject; }
        }
        public GridControl MasterGrid { get; set; }
    }

    public class WidthAdjustmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double) || !(parameter is double))
                return value;

            double doubleValue = (double)value;
            double doubleParameter = (double)parameter;

            if (Double.IsNaN(doubleValue) || Double.IsNaN(doubleParameter))
                return doubleValue;

            return doubleValue;
            //return doubleValue - doubleParameter;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
