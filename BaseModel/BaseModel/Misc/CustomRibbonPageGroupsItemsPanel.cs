using System;
using System.Windows;
using DevExpress.Xpf.Ribbon;


namespace BaseModel.Misc
{
    public enum GroupAlignment { Left, Right }
    public class CustomRibbonPageGroupsItemsPanel : RibbonPageGroupsItemsPanel
    {
        public static readonly DependencyProperty GroupAlignmentProperty = DependencyProperty.RegisterAttached("GroupAlignment", typeof(GroupAlignment), typeof(CustomRibbonPageGroupsItemsPanel), new UIPropertyMetadata(GroupAlignment.Left));

        public static GroupAlignment GetGroupAlignment(DependencyObject target)
        {
            if (target == null)
                return GroupAlignment.Left;

            return (GroupAlignment)target.GetValue(GroupAlignmentProperty);
        }
        public static void SetGroupAlignment(DependencyObject target, GroupAlignment value)
        {
            target.SetValue(GroupAlignmentProperty, value);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Rect rect = new Rect();
            Rect rect2 = new Rect();
            rect2.X = finalSize.Width;
            for (int i = 0; i < InternalChildren.Count; i++)
            {
                UIElement child = InternalChildren[i] as UIElement;
                Size sz = new Size(child.DesiredSize.Width, Math.Max(child.DesiredSize.Height, finalSize.Height));
                rect.Size = sz;
                rect2.Size = sz;
                RibbonPageGroupControl content = (RibbonPageGroupControl)child;
                GroupAlignment align = GetGroupAlignment(content.PageGroup);
                if (align == GroupAlignment.Left)
                {
                    child.Arrange(rect);
                    rect.X += rect.Width;
                }
                else
                {
                    rect2.X -= child.DesiredSize.Width;
                    child.Arrange(rect2);
                }
            }
            return finalSize;
        }
    }

    public class CustomRibbonPagesItemsPanel : RibbonPagesItemsPanel
    {
        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (UIElement child in Children)
            {
                RibbonPageGroupsControl groupsControl = child as RibbonPageGroupsControl;
                if(groupsControl.Page != null)
                {
                    if (groupsControl.Page.IsSelected)
                    {
                        child.Opacity = 1;
                        child.IsHitTestVisible = true;
                    }
                    else
                    {
                        child.Opacity = 0;
                        child.IsHitTestVisible = false;
                    }
                    child.Arrange(new Rect(0, 0, Math.Max(child.DesiredSize.Width, finalSize.Width), finalSize.Height));
                }
            }
            return finalSize;
        }
    }
}
