using DevExpress.Utils;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
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
        private double leftMarginWidthAdjustmentValue = 0.0f;
        public static readonly DependencyProperty IsUseTagProperty = DependencyProperty.Register("IsUseTag", typeof(bool), typeof(DetailSyncBehavior), new PropertyMetadata(false));
        public static readonly DependencyProperty FirstColumnFieldNameProperty = DependencyProperty.Register("FirstColumnFieldName", typeof(string), typeof(DetailSyncBehavior), new PropertyMetadata(string.Empty));

        public bool IsUseTag
        {
            get { return (bool)GetValue(IsUseTagProperty); }
            set { SetValue(IsUseTagProperty, value); }
        }

        public string FirstColumnFieldName
        {
            get { return (string)GetValue(FirstColumnFieldNameProperty); }
            set { SetValue(FirstColumnFieldNameProperty, value); }
        }

        private void SetColumnBinding(GridColumn masterCol, GridColumn detailCol)
        {
            TableView masterView = MasterGrid.View as TableView;
            foreach (GridColumn masterColumn in masterView.VisibleColumns)
            {
                DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleIndexProperty, typeof(GridColumn)).AddValueChanged(masterColumn, OnMasterColumnPropertyChanged);
                DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).AddValueChanged(masterColumn, OnMasterColumnPropertyChanged);
                DependencyPropertyDescriptor.FromProperty(GridColumn.WidthProperty, typeof(GridColumn)).AddValueChanged(masterColumn, OnMasterColumnPropertyChanged);
            }

            DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).AddValueChanged(detailCol, OnDetailColumnPropertyChanged);
            detailCol.AllowResizing = DefaultBoolean.False;

            if (detailCol.VisibleIndex == 0 || detailCol.FieldName == FirstColumnFieldName)
            {
                detailCol.SetBinding(GridColumn.WidthProperty, new Binding("ActualDataWidth") { Source = masterCol, Converter = widthAdjustmentConverter, ConverterParameter = widthAdjustmentValue });
                //not setting visibility property here because first column cannot be hidden
            }
            else
            {
                string s;
                if (detailCol.FieldName == "Entity.DropDownIndirectBudget")
                    s = string.Empty;

                detailCol.SetBinding(GridColumn.WidthProperty, new Binding("ActualDataWidth") { Source = masterCol });
                detailCol.SetBinding(GridColumn.VisibleProperty, new Binding("Visible") { Source = masterCol });
                detailCol.SetBinding(GridColumn.VisibleIndexProperty, new Binding("VisibleIndex") { Source = masterCol });
            }
        }

        private void RemoveColumnBinding(GridColumn masterCol, GridColumn detailCol)
        {
            TableView masterView = MasterGrid.View as TableView;
            foreach (GridColumn masterColumn in masterView.VisibleColumns)
            {
                DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleIndexProperty, typeof(GridColumn)).RemoveValueChanged(masterColumn, OnMasterColumnPropertyChanged);
                DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).RemoveValueChanged(masterColumn, OnMasterColumnPropertyChanged);
                DependencyPropertyDescriptor.FromProperty(GridColumn.WidthProperty, typeof(GridColumn)).RemoveValueChanged(masterColumn, OnMasterColumnPropertyChanged);
            }

            DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).RemoveValueChanged(detailCol, OnDetailColumnPropertyChanged);
            BindingOperations.ClearBinding(detailCol, GridColumn.WidthProperty);
            BindingOperations.ClearBinding(detailCol, GridColumn.VisibleProperty);
            detailCol.AllowResizing = DefaultBoolean.Default;
        }

        private void SetWidthBindings()
        {
            TableView masterView = MasterGrid.View as TableView;
            TableView detailView = DetailGrid.View as TableView;

            foreach (GridColumn detailCol in detailView.VisibleColumns)
            {
                //if (detailCol.VisibleIndex < 0 || detailCol.VisibleIndex >= masterView.VisibleColumns.Count)
                //    continue;

                //GridColumn masterCol = masterView.VisibleColumns[detailCol.VisibleIndex];
                GridColumn masterCol = null;
                if(!IsUseTag)
                    masterCol = masterView.VisibleColumns.FirstOrDefault(x => x.HeaderCaption.ToString() == detailCol.HeaderCaption.ToString());
                else
                    masterCol = masterView.VisibleColumns.FirstOrDefault(x => x.HeaderCaption.ToString() == detailCol.Tag.ToString());

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
        }

        private void ResetWidthBindings()
        {
            TableView masterView = MasterGrid.View as TableView;
            TableView detailView = DetailGrid.View as TableView;

            bool check = false;
            foreach (GridColumn detailCol in detailView.VisibleColumns)
            {
                GridColumn masterCol = null;

                if(IsUseTag)
                {
                    if (detailCol.Tag != null && detailCol.Tag.ToString() != string.Empty)
                    {
                        IEnumerable<GridColumn> masterColumnsWithHeader = masterView.VisibleColumns.Where(x => x.HeaderCaption.ToString() != string.Empty);
                        masterCol = masterColumnsWithHeader.FirstOrDefault(x => x.HeaderCaption.ToString() == detailCol.Tag.ToString());
                    }
                    else if (masterView.VisibleColumns.Count > 0)
                        masterCol = masterView.VisibleColumns.First();

                    IEnumerable<GridColumn> masterColumnsWithoutChild = masterView.VisibleColumns.Where(x => !detailView.VisibleColumns.Any(y => y.Tag != null && y.Tag.ToString() == x.HeaderCaption.ToString()));
                    widthAdjustmentValue = masterColumnsWithoutChild.Sum(x => x.Width.Value);
                }
                else
                {
                    if (detailCol.HeaderCaption.ToString() != string.Empty)
                    {
                        IEnumerable<GridColumn> masterColumnsWithHeader = masterView.VisibleColumns.Where(x => x.HeaderCaption.ToString() != string.Empty);
                        masterCol = masterColumnsWithHeader.FirstOrDefault(x => x.HeaderCaption.ToString() == detailCol.HeaderCaption.ToString());
                    }
                    else if (masterView.VisibleColumns.Count > 0)
                        masterCol = masterView.VisibleColumns.First();

                    IEnumerable<GridColumn> masterColumnsWithoutChild = masterView.VisibleColumns.Where(x => !detailView.VisibleColumns.Any(y => y.HeaderCaption.ToString() == x.HeaderCaption.ToString()));
                    widthAdjustmentValue = masterColumnsWithoutChild.Sum(x => x.Width.Value);
                    //Debug.Print(widthAdjustmentValue.ToString());
                    //widthAdjustmentValue = 500;
                }

                RemoveColumnBinding(masterCol, detailCol);
                if (masterCol != null)
                    SetColumnBinding(masterCol, detailCol);

                if(check)
                {
                    Debug.Print(detailCol.VisibleIndex + " " + detailCol.FieldName);
                }
            }
        }

        private void OnMasterColumnPropertyChanged(object sender, EventArgs e)
        {
            GridColumn masterColumn = (GridColumn)sender;
            Dispatcher.BeginInvoke(new Action(() => {

                DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleIndexProperty, typeof(GridColumn)).RemoveValueChanged(masterColumn, OnMasterColumnPropertyChanged);
                DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).RemoveValueChanged(masterColumn, OnMasterColumnPropertyChanged);
                DependencyPropertyDescriptor.FromProperty(GridColumn.WidthProperty, typeof(GridColumn)).RemoveValueChanged(masterColumn, OnMasterColumnPropertyChanged);
                ResetWidthBindings();
            }), DispatcherPriority.Render);
        }

        private void MasterGridColumns_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (onAttached != 0)
                return;

            if (e.Action == NotifyCollectionChangedAction.Reset)
                ResetWidthBindings();
        }

        private void OnDetailColumnPropertyChanged(object sender, EventArgs e)
        {
            GridColumn detailColumn = (GridColumn)sender;
            if (!detailColumn.Visible)
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    DependencyPropertyDescriptor.FromProperty(GridColumn.VisibleProperty, typeof(GridColumn)).RemoveValueChanged(detailColumn, OnDetailColumnPropertyChanged);
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
                        leftMarginWidthAdjustmentValue = (MasterGrid.View as TableView).ExpandDetailButtonWidth;
                        FrameworkElement content = LayoutHelper.FindElement(MasterGrid, (element) => element is ContentPresenter && element.Name == "content");
                        if (content != null)
                            leftMarginWidthAdjustmentValue += content.Margin.Left;
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

            if (Double.IsNaN(doubleParameter))
                return doubleValue;
            else
                return doubleParameter;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
