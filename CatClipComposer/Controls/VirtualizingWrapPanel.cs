using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CatClipComposer.Controls;

public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(210d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(164d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty StretchItemsProperty = DependencyProperty.Register(
        nameof(StretchItems),
        typeof(bool),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private Size _extent;
    private Size _viewport;
    private Point _offset;

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public bool StretchItems
    {
        get => (bool)GetValue(StretchItemsProperty);
        set => SetValue(StretchItemsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        var itemCount = owner?.Items.Count ?? 0;
        var availableViewportWidth = Math.Max(
            ActualWidth,
            Math.Max(ScrollOwner?.ViewportWidth ?? 0, (VisualTreeHelper.GetParent(this) as FrameworkElement)?.ActualWidth ?? 0));
        var viewportWidth = double.IsInfinity(availableSize.Width)
            ? Math.Max(ItemWidth, availableViewportWidth)
            : availableSize.Width;
        var viewportHeight = double.IsInfinity(availableSize.Height)
            ? ItemHeight
            : availableSize.Height;
        var itemsPerRow = GetItemsPerRow(viewportWidth);
        var effectiveItemWidth = StretchItems ? viewportWidth : ItemWidth;
        var rowCount = itemCount == 0 ? 0 : (int)Math.Ceiling(itemCount / (double)itemsPerRow);
        UpdateScrollInfo(
            new Size(viewportWidth, rowCount * ItemHeight),
            new Size(viewportWidth, viewportHeight));

        var firstRow = Math.Max(0, (int)Math.Floor(VerticalOffset / ItemHeight));
        var visibleRows = Math.Max(1, (int)Math.Ceiling(viewportHeight / ItemHeight) + 1);
        var firstIndex = Math.Min(itemCount, firstRow * itemsPerRow);
        var lastIndex = Math.Min(itemCount - 1, (firstRow + visibleRows) * itemsPerRow - 1);
        RecycleOutside(firstIndex, lastIndex);

        if (firstIndex <= lastIndex)
        {
            var start = ItemContainerGenerator.GeneratorPositionFromIndex(firstIndex);
            var childIndex = start.Offset == 0 ? start.Index : start.Index + 1;
            using var generation = ItemContainerGenerator.StartAt(
                start,
                GeneratorDirection.Forward,
                allowStartAtRealizedItem: true);
            for (var itemIndex = firstIndex; itemIndex <= lastIndex; itemIndex++, childIndex++)
            {
                if (ItemContainerGenerator.GenerateNext(out var newlyRealized) is not UIElement child)
                {
                    continue;
                }

                if (newlyRealized)
                {
                    if (childIndex >= InternalChildren.Count)
                    {
                        AddInternalChild(child);
                    }
                    else
                    {
                        InsertInternalChild(childIndex, child);
                    }

                    ItemContainerGenerator.PrepareItemContainer(child);
                }

                child.Measure(new Size(effectiveItemWidth, ItemHeight));
            }
        }

        return new Size(viewportWidth, viewportHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var itemsPerRow = GetItemsPerRow(finalSize.Width);
        var effectiveItemWidth = StretchItems ? finalSize.Width : ItemWidth;
        foreach (UIElement child in InternalChildren)
        {
            var index = IndexFromContainer(child);
            if (index < 0)
            {
                continue;
            }

            var row = index / itemsPerRow;
            var column = index % itemsPerRow;
            child.Arrange(new Rect(
                column * effectiveItemWidth,
                row * ItemHeight - VerticalOffset,
                effectiveItemWidth,
                ItemHeight));
        }

        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        base.OnItemsChanged(sender, args);
        InvalidateMeasure();
    }

    private int GetItemsPerRow(double width) => StretchItems
        ? 1
        : Math.Max(1, (int)Math.Floor(Math.Max(ItemWidth, width) / ItemWidth));

    private void RecycleOutside(int firstIndex, int lastIndex)
    {
        for (var childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--)
        {
            var child = InternalChildren[childIndex];
            var itemIndex = IndexFromContainer(child);
            if (itemIndex >= firstIndex && itemIndex <= lastIndex)
            {
                continue;
            }

            ItemContainerGenerator.Remove(new GeneratorPosition(childIndex, 0), 1);
            RemoveInternalChildRange(childIndex, 1);
        }
    }

    private void UpdateScrollInfo(Size extent, Size viewport)
    {
        var changed = extent != _extent || viewport != _viewport;
        _extent = extent;
        _viewport = viewport;
        SetVerticalOffset(VerticalOffset);
        if (changed)
        {
            ScrollOwner?.InvalidateScrollInfo();
        }
    }

    public bool CanHorizontallyScroll { get; set; }

    public bool CanVerticallyScroll { get; set; } = true;

    public double ExtentWidth => _extent.Width;

    public double ExtentHeight => _extent.Height;

    public double ViewportWidth => _viewport.Width;

    public double ViewportHeight => _viewport.Height;

    public double HorizontalOffset => _offset.X;

    public double VerticalOffset => _offset.Y;

    public ScrollViewer? ScrollOwner { get; set; }

    public void LineUp() => SetVerticalOffset(VerticalOffset - ItemHeight);

    public void LineDown() => SetVerticalOffset(VerticalOffset + ItemHeight);

    public void LineLeft()
    {
    }

    public void LineRight()
    {
    }

    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - ItemHeight * 3);

    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + ItemHeight * 3);

    public void MouseWheelLeft()
    {
    }

    public void MouseWheelRight()
    {
    }

    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);

    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);

    public void PageLeft()
    {
    }

    public void PageRight()
    {
    }

    public void SetHorizontalOffset(double offset)
    {
    }

    public void SetVerticalOffset(double offset)
    {
        var maximum = Math.Max(0, ExtentHeight - ViewportHeight);
        var normalized = Math.Clamp(offset, 0, maximum);
        if (Math.Abs(normalized - _offset.Y) < 0.01)
        {
            return;
        }

        _offset.Y = normalized;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        if (visual is not UIElement element)
        {
            return rectangle;
        }

        var index = IndexFromContainer(element);
        if (index < 0)
        {
            return rectangle;
        }

        var itemsPerRow = GetItemsPerRow(ViewportWidth);
        var top = index / itemsPerRow * ItemHeight;
        if (top < VerticalOffset)
        {
            SetVerticalOffset(top);
        }
        else if (top + ItemHeight > VerticalOffset + ViewportHeight)
        {
            SetVerticalOffset(top + ItemHeight - ViewportHeight);
        }

        return new Rect(0, top, StretchItems ? ViewportWidth : ItemWidth, ItemHeight);
    }

    private int IndexFromContainer(DependencyObject container) =>
        ItemsControl.GetItemsOwner(this)?.ItemContainerGenerator.IndexFromContainer(container) ?? -1;
}
