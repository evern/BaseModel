using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DevExpress.Xpf.LayoutControl;

namespace BaseModel.View.ExtraLayout
{
    public class ExtraLayoutControl : DevExpress.Xpf.LayoutControl.LayoutControl
    {

        protected override Size OnMeasure(Size constraint)
        {
            if (constraint.Width == 0 &&
                constraint.Height == 0)
                return base.OnMeasure(constraint);

            Size maxConstraint = new Size(Double.PositiveInfinity, Double.PositiveInfinity);

            Size defaultConstraint = constraint;
            constraint = base.OnMeasure(constraint);

            if (Double.IsInfinity(defaultConstraint.Width) && Orientation == Orientation.Horizontal)
                defaultConstraint.Width = constraint.Width;
            if (Double.IsInfinity(defaultConstraint.Height) && Orientation == Orientation.Vertical)
                defaultConstraint.Height = constraint.Height;

            double starCount = 0;

            #region Create GroupLists
            List<ExtraLayoutGroup> listPixel = new List<ExtraLayoutGroup>();
            List<ExtraLayoutGroup> listStar = new List<ExtraLayoutGroup>();

            foreach (UIElement child in Children)
            {
                if (child is ExtraLayoutGroup)
                {
                    if (((ExtraLayoutGroup)child).LayoutItemSize.Value > 0)
                        switch (((ExtraLayoutGroup)child).LayoutItemSize.GridUnitType)
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
                group.Measure((Orientation == Orientation.Horizontal) ? new Size(group.LayoutItemSize.Value, maxConstraint.Height) :
                                                                        new Size(maxConstraint.Width, group.LayoutItemSize.Value));
            }

            #endregion

            constraint = base.OnMeasure(defaultConstraint);
            if (starCount == 0)
                return constraint;

            double starSize = 0;

            #region Calc StarSize

            Double usedSize = (Orientation == Orientation.Horizontal) ? constraint.Width : constraint.Height;
            foreach (ExtraLayoutGroup group in listStar)
                usedSize -= (Orientation == Orientation.Horizontal) ? group.DesiredSize.Width : group.DesiredSize.Height;

            Double availableSize = ((Orientation == Orientation.Horizontal) ? defaultConstraint.Width :
                                                                              defaultConstraint.Height) - usedSize;
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
                group.Measure((Orientation == Orientation.Horizontal) ? new Size(group.Width, maxConstraint.Height) :
                                                                        new Size(maxConstraint.Width, group.Height));
            }

            #endregion

            return base.OnMeasure(constraint);
        }
    }
}
