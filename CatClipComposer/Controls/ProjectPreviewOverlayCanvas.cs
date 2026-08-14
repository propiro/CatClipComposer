using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Controls;

public sealed class PreviewOverlaySelectedEventArgs(Guid itemId) : EventArgs
{
    public Guid ItemId { get; } = itemId;
}

public sealed class PreviewOverlayTransformEventArgs(
    Guid itemId,
    double x,
    double y,
    double scale,
    double rotationDegrees) : EventArgs
{
    public Guid ItemId { get; } = itemId;

    public double X { get; } = x;

    public double Y { get; } = y;

    public double Scale { get; } = scale;

    public double RotationDegrees { get; } = rotationDegrees;

}

public sealed class PreviewOverlayEditEventArgs(Guid itemId) : EventArgs
{
    public Guid ItemId { get; } = itemId;
}

public sealed class PreviewOverlayOpenEditorEventArgs(Guid itemId) : EventArgs
{
    public Guid ItemId { get; } = itemId;
}

public sealed class ProjectPreviewOverlayCanvas : Canvas
{
    private const double DirectManipulationMinimumScale = 0.05;
    private readonly Dictionary<string, BitmapSource?> _imageCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> _staleItemIds = [];
    private readonly Dictionary<Guid, ProjectTimelineItem> _renderedItemSnapshots = [];
    private IReadOnlyList<ProjectTimelineItem> _items = [];
    private Guid? _selectedItemId;
    private Guid? _editingItemId;
    private int _outputWidth = 1920;
    private int _outputHeight = 1080;
    private bool _hasRenderedPreview;
    private InteractionState? _interaction;

    public ProjectPreviewOverlayCanvas()
    {
        Background = Brushes.Transparent;
        ClipToBounds = true;
        Focusable = true;
        SizeChanged += (_, _) => Redraw();
        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        PreviewMouseMove += OnPreviewMouseMove;
        PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        PreviewKeyDown += OnPreviewKeyDown;
        LostMouseCapture += (_, _) => CompleteInteraction();
    }

    public event EventHandler<PreviewOverlaySelectedEventArgs>? OverlaySelected;

    public event EventHandler<PreviewOverlayTransformEventArgs>? OverlayTransformChanged;

    public event EventHandler<PreviewOverlayEditEventArgs>? OverlayEditAccepted;

    public event EventHandler<PreviewOverlayEditEventArgs>? OverlayEditCanceled;

    public event EventHandler<PreviewOverlayOpenEditorEventArgs>? OverlayOpenEditorRequested;

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || _interaction is null)
        {
            return;
        }

        var itemId = _interaction.ItemId;
        CompleteInteraction();
        _selectedItemId = itemId;
        OverlaySelected?.Invoke(this, new PreviewOverlaySelectedEventArgs(itemId));
        OverlayOpenEditorRequested?.Invoke(this, new PreviewOverlayOpenEditorEventArgs(itemId));
        e.Handled = true;
    }

    public void Configure(
        int outputWidth,
        int outputHeight,
        IReadOnlyList<ProjectTimelineItem> items,
        Guid? selectedItemId,
        bool hasRenderedPreview)
    {
        _outputWidth = Math.Max(1, outputWidth);
        _outputHeight = Math.Max(1, outputHeight);
        _items = items;
        _selectedItemId = selectedItemId;
        _hasRenderedPreview = hasRenderedPreview;
        if (_hasRenderedPreview)
        {
            foreach (var item in _items.Where(item => !_staleItemIds.Contains(item.Id)))
            {
                _renderedItemSnapshots.TryAdd(item.Id, CloneOverlayState(item));
            }
        }
        if (_interaction is null)
        {
            Redraw();
        }
    }

    public void Select(Guid? itemId)
    {
        if (_selectedItemId == itemId)
        {
            return;
        }

        _selectedItemId = itemId;
        Redraw();
    }

    public void MarkPreviewRendered()
    {
        _staleItemIds.Clear();
        _renderedItemSnapshots.Clear();
        foreach (var item in _items)
        {
            _renderedItemSnapshots[item.Id] = CloneOverlayState(item);
        }

        _hasRenderedPreview = true;
        Redraw();
    }

    public void MarkItemStale(Guid itemId)
    {
        _staleItemIds.Add(itemId);
        Redraw();
    }

    private void Redraw()
    {
        Children.Clear();
        var viewport = GetVideoViewport();
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        foreach (var item in _items)
        {
            var visual = CreateItemVisual(item, viewport);
            Children.Add(visual.Element);
            SetLeft(visual.Element, visual.Left);
            SetTop(visual.Element, visual.Top);
            if (item.Id == _editingItemId)
            {
                var actionPanel = CreateActionPanel(item.Id);
                Children.Add(actionPanel);
                SetLeft(actionPanel, Math.Clamp(visual.Left, 2, Math.Max(2, ActualWidth - 116)));
                SetTop(actionPanel, Math.Clamp(
                    visual.Top + visual.Height + 4,
                    2,
                    Math.Max(2, ActualHeight - 30)));
            }

            if (_hasRenderedPreview && _staleItemIds.Contains(item.Id) &&
                _renderedItemSnapshots.TryGetValue(item.Id, out var renderedState) &&
                HasMovedFromRenderedState(item, renderedState))
            {
                var markerVisual = CreateStaleMarkerVisual(renderedState, viewport);
                Children.Add(markerVisual.Element);
                SetLeft(markerVisual.Element, markerVisual.Left);
                SetTop(markerVisual.Element, markerVisual.Top);
            }
        }
    }

    private ItemVisual CreateItemVisual(ProjectTimelineItem item, Rect viewport)
    {
        var selected = item.Id == _selectedItemId;
        var editing = item.Id == _editingItemId;
        var (baseWidth, baseHeight, content) = CreateContent(item, viewport);
        var transformScale = item.HasCustomOverlayTransform
            ? OverlayTransformValues.NormalizeScale(item.OverlayScale)
            : 1;
        var width = Math.Max(18, baseWidth * transformScale);
        var height = Math.Max(18, baseHeight * transformScale);
        var center = ResolveCenter(item, viewport, width, height);
        var root = new Grid
        {
            Width = width,
            Height = height,
            Background = Brushes.Transparent,
            Cursor = item.IsTransformLocked ? Cursors.Arrow : Cursors.SizeAll,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(
                item.HasCustomOverlayTransform
                    ? OverlayTransformValues.NormalizeRotation(item.OverlayRotationDegrees)
                    : 0),
            ToolTip = item.IsTransformLocked
                ? "Overlay transform is locked. Use the lock button in Project Layers Data to unlock it."
                : editing
                ? "Drag to move. Drag the lower-right square to scale. Drag the upper-center circle to rotate."
                : "Click to select this overlay."
        };
        var showContentProxy = !_hasRenderedPreview || _staleItemIds.Contains(item.Id);
        root.Children.Add(new Viewbox
        {
            Stretch = Stretch.Fill,
            Opacity = showContentProxy
                ? Math.Clamp(item.OverlayOpacity, 0, 1)
                : 0,
            Child = content
        });
        root.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(selected
                ? Color.FromRgb(255, 204, 102)
                : Color.FromArgb(130, 200, 192, 178)),
            BorderThickness = new Thickness(selected ? 2 : 1),
            Background = Brushes.Transparent,
            IsHitTestVisible = false
        });
        root.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                _selectedItemId = item.Id;
                OverlaySelected?.Invoke(this, new PreviewOverlaySelectedEventArgs(item.Id));
                OverlayOpenEditorRequested?.Invoke(this, new PreviewOverlayOpenEditorEventArgs(item.Id));
                e.Handled = true;
                return;
            }

            if (item.IsTransformLocked)
            {
                _selectedItemId = item.Id;
                OverlaySelected?.Invoke(this, new PreviewOverlaySelectedEventArgs(item.Id));
                Redraw();
                e.Handled = true;
                return;
            }

            BeginInteraction(item, InteractionKind.Move, viewport, e);
        };

        if (editing)
        {
            var resizeHandle = CreateHandle(Brushes.White, Cursors.SizeNWSE, "Scale overlay");
            resizeHandle.HorizontalAlignment = HorizontalAlignment.Right;
            resizeHandle.VerticalAlignment = VerticalAlignment.Bottom;
            resizeHandle.MouseLeftButtonDown += (_, e) => BeginInteraction(item, InteractionKind.Scale, viewport, e);
            root.Children.Add(resizeHandle);

            var rotationHandle = CreateRotationHandle();
            rotationHandle.HorizontalAlignment = HorizontalAlignment.Center;
            rotationHandle.VerticalAlignment = VerticalAlignment.Top;
            rotationHandle.MouseLeftButtonDown += (_, e) => BeginInteraction(item, InteractionKind.Rotate, viewport, e);
            root.Children.Add(rotationHandle);
        }

        return new ItemVisual(root, center.X - width / 2, center.Y - height / 2, height);
    }

    private ItemVisual CreateStaleMarkerVisual(ProjectTimelineItem item, Rect viewport)
    {
        var (baseWidth, baseHeight, _) = CreateContent(item, viewport);
        var transformScale = item.HasCustomOverlayTransform
            ? OverlayTransformValues.NormalizeScale(item.OverlayScale)
            : 1;
        var width = Math.Max(18, baseWidth * transformScale);
        var height = Math.Max(18, baseHeight * transformScale);
        var center = ResolveCenter(item, viewport, width, height);
        var marker = CreateMovedContentMarker();
        marker.Width = width;
        marker.Height = height;
        marker.RenderTransformOrigin = new Point(0.5, 0.5);
        marker.RenderTransform = new RotateTransform(
            item.HasCustomOverlayTransform
                ? OverlayTransformValues.NormalizeRotation(item.OverlayRotationDegrees)
                : 0);
        return new ItemVisual(marker, center.X - width / 2, center.Y - height / 2, height);
    }

    private (double Width, double Height, FrameworkElement Content) CreateContent(
        ProjectTimelineItem item,
        Rect viewport)
    {
        var outputScale = viewport.Width / _outputWidth;
        if (item.Kind is ProjectItemKind.ImageOverlay or ProjectItemKind.VideoOverlay)
        {
            var source = LoadImage(item.SourcePath);
            var pixelWidth = source?.PixelWidth ?? 480;
            var pixelHeight = source?.PixelHeight ?? 320;
            var fittedWidth = Math.Min(480, Math.Max(1, pixelWidth));
            var fittedHeight = fittedWidth * Math.Max(1, pixelHeight) / Math.Max(1, pixelWidth);
            return (
                Math.Max(18, fittedWidth * outputScale),
                Math.Max(18, fittedHeight * outputScale),
                source is not null
                    ? new Image
                    {
                        Source = source,
                        Stretch = Stretch.Fill,
                        SnapsToDevicePixels = true
                    }
                    : CreateMovingOverlayPlaceholder(item));
        }

        var fontSize = Math.Max(1, item.FontSize * outputScale);
        var fontFamily = new FontFamily(string.IsNullOrWhiteSpace(item.FontFamily) ? "Segoe UI" : item.FontFamily);
        var formatted = new FormattedText(
            string.IsNullOrEmpty(item.Text) ? "Text" : item.Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            fontSize,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var text = new TextBlock
        {
            Text = string.IsNullOrEmpty(item.Text) ? "Text" : item.Text,
            FontFamily = fontFamily,
            FontSize = fontSize,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect
            {
                BlurRadius = 3,
                ShadowDepth = 1,
                Color = Colors.Black,
                Opacity = 0.9
            }
        };
        return (
            Math.Max(28, formatted.WidthIncludingTrailingWhitespace + 12),
            Math.Max(22, formatted.Height + 10),
            text);
    }

    private static FrameworkElement CreateMovingOverlayPlaceholder(ProjectTimelineItem item) => new Border
    {
        Background = new SolidColorBrush(Color.FromArgb(210, 32, 31, 29)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(155, 148, 136)),
        BorderThickness = new Thickness(1),
        Child = new TextBlock
        {
            Text = item.Kind == ProjectItemKind.VideoOverlay
                ? $"GIF / VIDEO\n{System.IO.Path.GetFileName(item.SourcePath)}"
                : "IMAGE",
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(218, 213, 203)),
            FontSize = 12
        }
    };

    private BitmapSource? LoadImage(string path)
    {
        if (_imageCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        BitmapSource? source = null;
        try
        {
            if (File.Exists(path))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                source = image;
            }
        }
        catch
        {
            // The renderer reports missing/invalid media. Keep the preview gizmo usable meanwhile.
        }

        _imageCache[path] = source;
        return source;
    }

    private Point ResolveCenter(ProjectTimelineItem item, Rect viewport, double width, double height)
    {
        if (item.HasCustomOverlayTransform)
        {
            return new Point(
                viewport.Left + viewport.Width * OverlayTransformValues.NormalizeCoordinate(item.OverlayX),
                viewport.Top + viewport.Height * OverlayTransformValues.NormalizeCoordinate(item.OverlayY));
        }

        var margin = 28 * viewport.Width / _outputWidth;
        return item.Position switch
        {
            OverlayPosition.TopLeft => new Point(viewport.Left + margin + width / 2, viewport.Top + margin + height / 2),
            OverlayPosition.TopRight => new Point(viewport.Right - margin - width / 2, viewport.Top + margin + height / 2),
            OverlayPosition.BottomLeft => new Point(viewport.Left + margin + width / 2, viewport.Bottom - margin - height / 2),
            OverlayPosition.BottomRight => new Point(viewport.Right - margin - width / 2, viewport.Bottom - margin - height / 2),
            _ => new Point(viewport.Left + viewport.Width / 2, viewport.Top + viewport.Height / 2)
        };
    }

    private static Border CreateHandle(Brush fill, Cursor cursor, string toolTip) => new()
    {
        Width = 12,
        Height = 12,
        Margin = new Thickness(2),
        Background = fill,
        BorderBrush = Brushes.Black,
        BorderThickness = new Thickness(1),
        Cursor = cursor,
        ToolTip = toolTip
    };

    private static Ellipse CreateRotationHandle() => new()
    {
        Width = 13,
        Height = 13,
        Margin = new Thickness(0, 2, 0, 0),
        Fill = new SolidColorBrush(Color.FromRgb(255, 204, 102)),
        Stroke = Brushes.Black,
        StrokeThickness = 1,
        Cursor = Cursors.Hand,
        ToolTip = "Rotate overlay"
    };

    private static Grid CreateMovedContentMarker()
    {
        var marker = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(72, 20, 19, 18)),
            IsHitTestVisible = false,
            ToolTip = "This overlay moved after the displayed project frame was prerendered. Prerender the frame again to refresh it."
        };
        marker.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(210, 235, 115, 101)),
            BorderThickness = new Thickness(2)
        });
        marker.Children.Add(new System.Windows.Shapes.Path
        {
            Stretch = Stretch.Fill,
            Margin = new Thickness(5),
            Stroke = new SolidColorBrush(Color.FromArgb(205, 235, 115, 101)),
            StrokeThickness = 2,
            Data = Geometry.Parse("M 0,0 L 1,1 M 1,0 L 0,1")
        });
        marker.Children.Add(new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 3, 6, 3),
            Background = new SolidColorBrush(Color.FromArgb(225, 16, 15, 14)),
            Child = new TextBlock
            {
                Text = "MOVED CONTENT\nPRERENDER FRAME",
                TextAlignment = TextAlignment.Center,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(246, 213, 207))
            }
        });
        return marker;
    }

    private static ProjectTimelineItem CloneOverlayState(ProjectTimelineItem item) => new()
    {
        Id = item.Id,
        Kind = item.Kind,
        SourcePath = item.SourcePath,
        Text = item.Text,
        FontFamily = item.FontFamily,
        FontSize = item.FontSize,
        Position = item.Position,
        HasCustomOverlayTransform = item.HasCustomOverlayTransform,
        OverlayX = item.OverlayX,
        OverlayY = item.OverlayY,
        OverlayScale = item.OverlayScale,
        OverlayRotationDegrees = item.OverlayRotationDegrees,
        OverlayOpacity = item.OverlayOpacity
    };

    private static bool HasMovedFromRenderedState(
        ProjectTimelineItem current,
        ProjectTimelineItem rendered) =>
        current.Position != rendered.Position ||
        current.HasCustomOverlayTransform != rendered.HasCustomOverlayTransform ||
        Math.Abs(current.OverlayX - rendered.OverlayX) > 0.0001 ||
        Math.Abs(current.OverlayY - rendered.OverlayY) > 0.0001 ||
        Math.Abs(current.OverlayScale - rendered.OverlayScale) > 0.0001 ||
        Math.Abs(current.OverlayRotationDegrees - rendered.OverlayRotationDegrees) > 0.0001;

    private StackPanel CreateActionPanel(Guid itemId)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = new SolidColorBrush(Color.FromArgb(220, 28, 28, 27))
        };
        var accept = new Button
        {
            Content = "OK",
            MinWidth = 50,
            Padding = new Thickness(7, 2, 7, 2),
            ToolTip = "Apply overlay transform (Enter)"
        };
        accept.Click += (_, e) =>
        {
            OverlayEditAccepted?.Invoke(this, new PreviewOverlayEditEventArgs(itemId));
            e.Handled = true;
        };
        var cancel = new Button
        {
            Content = "Cancel",
            MinWidth = 58,
            Margin = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(7, 2, 7, 2),
            ToolTip = "Cancel overlay transform (Esc)"
        };
        cancel.Click += (_, e) =>
        {
            OverlayEditCanceled?.Invoke(this, new PreviewOverlayEditEventArgs(itemId));
            e.Handled = true;
        };
        panel.Children.Add(accept);
        panel.Children.Add(cancel);
        return panel;
    }

    private void BeginInteraction(
        ProjectTimelineItem item,
        InteractionKind kind,
        Rect viewport,
        MouseButtonEventArgs e)
    {
        var visualBounds = ResolveVisualBounds(item, viewport);
        var pointer = e.GetPosition(this);
        var center = new Point(visualBounds.Left + visualBounds.Width / 2, visualBounds.Top + visualBounds.Height / 2);
        var initialX = (center.X - viewport.Left) / viewport.Width;
        var initialY = (center.Y - viewport.Top) / viewport.Height;
        _selectedItemId = item.Id;
        _editingItemId = item.Id;
        OverlaySelected?.Invoke(this, new PreviewOverlaySelectedEventArgs(item.Id));
        _interaction = new InteractionState(
            item.Id,
            kind,
            viewport,
            pointer,
            center,
            initialX,
            initialY,
            item.HasCustomOverlayTransform ? item.OverlayScale : 1,
            item.HasCustomOverlayTransform ? item.OverlayRotationDegrees : 0);
        CaptureMouse();
        Focus();
        Redraw();
        e.Handled = true;
    }

    private Rect ResolveVisualBounds(ProjectTimelineItem item, Rect viewport)
    {
        var (baseWidth, baseHeight, _) = CreateContent(item, viewport);
        var scale = item.HasCustomOverlayTransform ? item.OverlayScale : 1;
        var width = Math.Max(18, baseWidth * OverlayTransformValues.NormalizeScale(scale));
        var height = Math.Max(18, baseHeight * OverlayTransformValues.NormalizeScale(scale));
        var center = ResolveCenter(item, viewport, width, height);
        return new Rect(center.X - width / 2, center.Y - height / 2, width, height);
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_interaction is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var pointer = e.GetPosition(this);
        var dx = pointer.X - _interaction.PointerStart.X;
        var dy = pointer.Y - _interaction.PointerStart.Y;
        if (!_interaction.HasChanged && Math.Sqrt(dx * dx + dy * dy) < 1)
        {
            return;
        }

        var x = _interaction.InitialX;
        var y = _interaction.InitialY;
        var scale = _interaction.InitialScale;
        var rotation = _interaction.InitialRotation;
        switch (_interaction.Kind)
        {
            case InteractionKind.Move:
                x = Math.Clamp(x + dx / _interaction.Viewport.Width, 0, 1);
                y = Math.Clamp(y + dy / _interaction.Viewport.Height, 0, 1);
                break;
            case InteractionKind.Scale:
                var initialDistance = Distance(_interaction.PointerStart, _interaction.Center);
                var currentDistance = Distance(pointer, _interaction.Center);
                scale = Math.Clamp(
                    _interaction.InitialScale * currentDistance / Math.Max(1, initialDistance),
                    DirectManipulationMinimumScale,
                    OverlayTransformValues.MaximumScale);
                break;
            case InteractionKind.Rotate:
                var initialAngle = GetAngle(_interaction.Center, _interaction.PointerStart);
                var currentAngle = GetAngle(_interaction.Center, pointer);
                rotation = OverlayTransformValues.NormalizeRotation(
                    _interaction.InitialRotation + currentAngle - initialAngle);
                break;
        }

        _interaction = _interaction with
        {
            LastX = x,
            LastY = y,
            LastScale = scale,
            LastRotation = rotation,
            HasChanged = true
        };
        _staleItemIds.Add(_interaction.ItemId);
        OverlayTransformChanged?.Invoke(
            this,
            new PreviewOverlayTransformEventArgs(
                _interaction.ItemId,
                x,
                y,
                scale,
                rotation));
        Redraw();
        e.Handled = true;
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_interaction is null)
        {
            return;
        }

        CompleteInteraction();
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_editingItemId.HasValue)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            OverlayEditAccepted?.Invoke(this, new PreviewOverlayEditEventArgs(_editingItemId.Value));
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            OverlayEditCanceled?.Invoke(this, new PreviewOverlayEditEventArgs(_editingItemId.Value));
            e.Handled = true;
        }
    }

    private void CompleteInteraction()
    {
        _interaction = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        Redraw();
    }

    public void CompleteEdit(Guid itemId, bool accepted)
    {
        if (_interaction?.ItemId == itemId)
        {
            _interaction = null;
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }
        }

        if (_editingItemId == itemId)
        {
            _editingItemId = null;
        }

        if (!accepted)
        {
            _staleItemIds.Remove(itemId);
        }

        Redraw();
    }

    private Rect GetVideoViewport()
    {
        var scale = Math.Min(ActualWidth / _outputWidth, ActualHeight / _outputHeight);
        if (!double.IsFinite(scale) || scale <= 0)
        {
            return Rect.Empty;
        }

        var width = _outputWidth * scale;
        var height = _outputHeight * scale;
        return new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height);
    }

    private static double Distance(Point first, Point second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double GetAngle(Point center, Point point) =>
        Math.Atan2(point.Y - center.Y, point.X - center.X) * 180 / Math.PI;

    private sealed record ItemVisual(FrameworkElement Element, double Left, double Top, double Height);

    private sealed record InteractionState(
        Guid ItemId,
        InteractionKind Kind,
        Rect Viewport,
        Point PointerStart,
        Point Center,
        double InitialX,
        double InitialY,
        double InitialScale,
        double InitialRotation)
    {
        public double LastX { get; init; } = InitialX;

        public double LastY { get; init; } = InitialY;

        public double LastScale { get; init; } = InitialScale;

        public double LastRotation { get; init; } = InitialRotation;

        public bool HasChanged { get; init; }
    }

    private enum InteractionKind
    {
        Move,
        Scale,
        Rotate
    }
}
