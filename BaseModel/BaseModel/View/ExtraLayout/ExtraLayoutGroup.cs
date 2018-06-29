using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.LayoutControl;
using System.Windows.Controls;

namespace BaseModel.View.ExtraLayout
{
    public class ExtraLayoutGroup : LayoutGroup
    {
        public GridLength LayoutItemSize
        {
            get { return (GridLength)GetValue(LayoutItemSizeProperty); }
            set { SetValue(LayoutItemSizeProperty, value); }
        }
        public static readonly DependencyProperty LayoutItemSizeProperty =
            DependencyProperty.Register("LayoutItemSize", typeof(GridLength), typeof(ExtraLayoutGroup), new PropertyMetadata(new GridLength(0, GridUnitType.Star)));

        protected override Size OnMeasure(Size constraint)
        {
            if (constraint.Width == 0 &&
                constraint.Height == 0)
                return base.OnMeasure(constraint);

            Size maxConstraint = new Size(Double.PositiveInfinity, Double.PositiveInfinity);

            Size defaultConstraint = constraint;
            constraint = base.OnMeasure(constraint);
            Size minConstraint = new Size(Math.Min(defaultConstraint.Width, constraint.Width),
                                          Math.Min(defaultConstraint.Height, constraint.Height));

            double starCount = 0;

            #region Create GroupLists
            List<ExtraLayoutGroup> listPixel = new List<ExtraLayoutGroup>();
            List<ExtraLayoutGroup> listStar = new List<ExtraLayoutGroup>();

            foreach (UIElement child in Children)
            {
                if (child is ExtraLayoutGroup)
                {
                    ExtraLayoutGroup group = (ExtraLayoutGroup)child;

                    if (Parent is ExtraLayoutControl)
                    {
                        switch (((ExtraLayoutControl)Parent).Orientation)
                        {
                            case Orientation.Horizontal:
                                ((FrameworkElement)child).HorizontalAlignment = HorizontalAlignment.Stretch;
                                break;
                            case Orientation.Vertical:
                                ((FrameworkElement)child).VerticalAlignment = VerticalAlignment.Stretch;
                                break;
                        }
                    }
                    if (Parent is ExtraLayoutGroup)
                    {
                        switch (((ExtraLayoutGroup)Parent).Orientation)
                        {
                            case Orientation.Horizontal:
                                ((FrameworkElement)child).HorizontalAlignment = HorizontalAlignment.Stretch;
                                break;
                            case Orientation.Vertical:
                                ((FrameworkElement)child).VerticalAlignment = VerticalAlignment.Stretch;
                                break;
                        }
                    }

                    if (group.LayoutItemSize.Value > 0)
                        switch (group.LayoutItemSize.GridUnitType)
                        {
                            case GridUnitType.Pixel:
                                listPixel.Add((ExtraLayoutGroup)child);
                                break;
                            case GridUnitType.Star:
                                listStar.Add((ExtraLayoutGroup)child);
                                starCount += ((ExtraLayoutGroup)child).LayoutItemSize.Value;
                                break;
                        }
                }
            }

            if (listPixel.Count == 0 && listStar.Count == 0)
                return constraint;

            #endregion

            #region Resize Pixeled

            foreach (ExtraLayoutGroup group in listPixel)
            {
                if (Orientation == Orientation.Horizontal)
                    group.Width = group.LayoutItemSize.Value;
                else
                    group.Height = group.LayoutItemSize.Value;

                group.InvalidateMeasure();
                group.Measure(minConstraint);
            }

            #endregion

            constraint = base.OnMeasure(minConstraint);
            minConstraint.Width = Math.Min(minConstraint.Width, constraint.Width);
            minConstraint.Height = Math.Min(minConstraint.Height, constraint.Height);

            if (starCount == 0)
                return minConstraint;

            double starSize = 0;

            #region Calc StarSize

            Double usedSize = (Orientation == Orientation.Horizontal) ? constraint.Width : constraint.Height;
            foreach (ExtraLayoutGroup group in listStar)
                usedSize -= (Orientation == Orientation.Horizontal) ? group.DesiredSize.Width : group.DesiredSize.Height;

            Double availableSize = ((Orientation == Orientation.Horizontal) ? minConstraint.Width :
                                                                              minConstraint.Height) - usedSize;
            starSize = availableSize / starCount;

            #endregion

            #region Resize Starred

            foreach (ExtraLayoutGroup group in listStar)
            {
                if (Orientation == Orientation.Horizontal)
                    group.Width = group.LayoutItemSize.Value * starSize;
                else
                    group.Height = group.LayoutItemSize.Value * starSize;

                group.InvalidateMeasure();
                group.Measure((Orientation == Orientation.Horizontal) ? new Size(group.Width, minConstraint.Height) :
                                                                        new Size(minConstraint.Width, group.Height));
            }

            #endregion

            return base.OnMeasure(minConstraint);
        }

    }
}
