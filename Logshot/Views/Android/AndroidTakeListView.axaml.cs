using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Logshot.ViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Logshot.Views.Android;

public partial class AndroidTakeListView : UserControl
{
    private const double TouchSlop = 12;
    private DayViewModel? _dayVm;
    private ScrollViewer? _listScrollViewer;
    private ContentControl? _stickyHeaderHost;
    private Border? _scrollPositionPreview;
    private TextBlock? _scrollPositionPreviewText;
    private Border? _scrollbarTouchTarget;
    private TakeListGroupHeaderViewModel? _currentStickyHeader;
    private IPointer? _touchPointer;
    private IPointer? _scrollbarThumbPointer;
    private Point _touchStartPoint;
    private bool _isTouchDragging;
    private bool _isScrollbarThumbDragging;
    private bool _suppressNextTouch;
    private double _scrollbarPreviewY;
    private double _scrollbarStartOffsetY;

    public AndroidTakeListView()
    {
        InitializeComponent();
        _stickyHeaderHost = this.FindControl<ContentControl>("StickyGroupHeader");
        _scrollPositionPreview = this.FindControl<Border>("ScrollPositionPreview");
        _scrollPositionPreviewText = this.FindControl<TextBlock>("ScrollPositionPreviewText");
        _scrollbarTouchTarget = this.FindControl<Border>("ScrollbarTouchTarget");

        AddHandler(InputElement.PointerPressedEvent, Touch_PointerPressed, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerMovedEvent, Touch_PointerMoved, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerReleasedEvent, Touch_PointerReleased, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerCaptureLostEvent, Touch_PointerCaptureLost, RoutingStrategies.Tunnel);
        AddHandler(InputElement.TappedEvent, Touch_Tapped, RoutingStrategies.Tunnel);
        Loaded += (_, _) => AttachListScrollViewer();
        Unloaded += (_, _) => DetachListScrollViewer();

        DataContextChanged += (_, _) =>
        {
            if (_dayVm != null)
            {
                _dayVm.Takes.CollectionChanged -= Takes_CollectionChanged;
                _dayVm.PropertyChanged -= DayVm_PropertyChanged;
                _dayVm.FlatTakeList.CollectionChanged -= FlatTakeList_CollectionChanged;
            }
            if (DataContext is DayViewModel dayVm)
            {
                _dayVm = dayVm;
                _dayVm.Takes.CollectionChanged += Takes_CollectionChanged;
                _dayVm.PropertyChanged += DayVm_PropertyChanged;
                _dayVm.FlatTakeList.CollectionChanged += FlatTakeList_CollectionChanged;
            }

            _currentStickyHeader = null;
            HideScrollPositionPreview();
            if (_stickyHeaderHost != null)
            {
                _stickyHeaderHost.Content = null;
                _stickyHeaderHost.IsVisible = false;
            }
            AttachListScrollViewer();
        };
    }

    private void Touch_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
            return;

        _touchPointer = e.Pointer;
        _touchStartPoint = e.GetPosition(this);
        _isTouchDragging = false;
        _suppressNextTouch = false;

        var source = e.Source as Visual;
        var isScrollbarThumb = source != null &&
            (source is Thumb || source.FindAncestorOfType<Thumb>() != null) &&
            (source is ScrollBar || source.FindAncestorOfType<ScrollBar>() != null);
        _scrollbarThumbPointer = isScrollbarThumb ? e.Pointer : null;
        _isScrollbarThumbDragging = false;
    }

    private void Touch_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch || !ReferenceEquals(e.Pointer, _touchPointer))
            return;

        var currentPoint = e.GetPosition(this);
        if (ReferenceEquals(e.Pointer, _scrollbarThumbPointer))
        {
            if (!_isScrollbarThumbDragging &&
                Math.Abs(currentPoint.Y - _touchStartPoint.Y) <= TouchSlop &&
                Math.Abs(currentPoint.X - _touchStartPoint.X) <= TouchSlop)
            {
                return;
            }

            _isScrollbarThumbDragging = true;
            _isTouchDragging = true;
            _suppressNextTouch = true;
            _scrollbarPreviewY = currentPoint.Y;
            if (_listScrollViewer != null && Bounds.Height > 0)
            {
                var maxOffset = Math.Max(0, _listScrollViewer.Extent.Height - _listScrollViewer.Viewport.Height);
                var offsetY = Math.Clamp(
                    _scrollbarStartOffsetY + (currentPoint.Y - _touchStartPoint.Y) * maxOffset / Bounds.Height,
                    0,
                    maxOffset);
                _listScrollViewer.Offset = new Vector(_listScrollViewer.Offset.X, offsetY);
            }
            UpdateScrollPositionPreview();
            return;
        }

        if (_isTouchDragging ||
            Math.Abs(currentPoint.X - _touchStartPoint.X) > TouchSlop ||
            Math.Abs(currentPoint.Y - _touchStartPoint.Y) > TouchSlop)
        {
            _isTouchDragging = true;
            _suppressNextTouch = true;

            var topLevel = TopLevel.GetTopLevel(this);
            topLevel?.FocusManager?.Focus(null);
        }
    }

    private void AttachListScrollViewer()
    {
        var scrollViewer = AndroidTakeListBox?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (ReferenceEquals(scrollViewer, _listScrollViewer))
            return;

        DetachListScrollViewer();
        _listScrollViewer = scrollViewer;
        if (_listScrollViewer != null)
        {
            ConfigureScrollbarTouchTargets();
            _listScrollViewer.ScrollChanged += ListScrollViewer_ScrollChanged;
            UpdateStickyHeader();
        }
    }

    private void ConfigureScrollbarTouchTargets()
    {
        if (_listScrollViewer == null)
            return;

        foreach (var scrollBar in _listScrollViewer.GetVisualDescendants()
                     .OfType<ScrollBar>()
                     .Where(scrollBar => scrollBar.Orientation == Avalonia.Layout.Orientation.Vertical))
        {
            scrollBar.AllowAutoHide = true;

            foreach (var thumb in scrollBar.GetVisualDescendants().OfType<Thumb>())
            {
                thumb.MinHeight = 48;
            }
        }
    }

    private void ScrollbarTouchTarget_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch || _listScrollViewer == null)
            return;

        _touchPointer = e.Pointer;
        _scrollbarThumbPointer = e.Pointer;
        _touchStartPoint = e.GetPosition(this);
        _scrollbarStartOffsetY = _listScrollViewer.Offset.Y;
        _isTouchDragging = false;
        _isScrollbarThumbDragging = false;
        e.Pointer.Capture(_scrollbarTouchTarget);
        e.Handled = true;
    }

    private void DetachListScrollViewer()
    {
        if (_listScrollViewer != null)
        {
            _listScrollViewer.ScrollChanged -= ListScrollViewer_ScrollChanged;
            _listScrollViewer = null;
        }
    }

    private void ListScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateStickyHeader();
    }

    private void FlatTakeList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateStickyHeader, Avalonia.Threading.DispatcherPriority.Render);
    }

    private void UpdateStickyHeader()
    {
        if (_dayVm == null || AndroidTakeListBox == null || _listScrollViewer == null || _stickyHeaderHost == null)
            return;

        if (_currentStickyHeader != null && !_dayVm.FlatTakeList.Contains(_currentStickyHeader))
        {
            var anchorTake = _currentStickyHeader.AnchorTake;
            _currentStickyHeader = _dayVm.FlatTakeList
                .OfType<TakeListGroupHeaderViewModel>()
                .FirstOrDefault(header => ReferenceEquals(header.AnchorTake, anchorTake));
        }

        if (_currentStickyHeader != null &&
            AndroidTakeListBox.ContainerFromItem(_currentStickyHeader) is Control currentContainer &&
            currentContainer.TranslatePoint(new Point(0, 0), _listScrollViewer) is Point currentOrigin &&
            currentOrigin.Y >= 0)
        {
            var currentHeaderIndex = _dayVm.FlatTakeList.IndexOf(_currentStickyHeader);
            var previousHeader = currentHeaderIndex > 0
                ? _dayVm.FlatTakeList
                    .Take(currentHeaderIndex)
                    .OfType<TakeListGroupHeaderViewModel>()
                    .LastOrDefault()
                : null;

            if (previousHeader != null)
            {
                _currentStickyHeader = previousHeader;
                _stickyHeaderHost.Content = previousHeader;
                _stickyHeaderHost.IsVisible = true;
            }
            else
            {
                _stickyHeaderHost.IsVisible = false;
            }

            UpdateScrollPositionPreview();
            return;
        }

        var crossedHeader = AndroidTakeListBox.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Where(item => item.DataContext is TakeListGroupHeaderViewModel)
            .Select(item => new
            {
                Header = (TakeListGroupHeaderViewModel)item.DataContext!,
                Origin = item.TranslatePoint(new Point(0, 0), _listScrollViewer)
            })
            .Where(item => item.Origin is Point origin && origin.Y < 0)
            .OrderByDescending(item => ((Point)item.Origin!).Y)
            .FirstOrDefault();

        if (crossedHeader != null)
        {
            _currentStickyHeader = crossedHeader.Header;
            _stickyHeaderHost.Content = crossedHeader.Header;
            _stickyHeaderHost.IsVisible = true;
        }
        else if (_currentStickyHeader == null)
        {
            _stickyHeaderHost.Content = null;
            _stickyHeaderHost.IsVisible = false;
        }

        UpdateScrollPositionPreview();
    }

    private void Touch_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch || !ReferenceEquals(e.Pointer, _touchPointer))
            return;

        if (_isTouchDragging && !_isScrollbarThumbDragging)
            e.Handled = true;

        if (ReferenceEquals(e.Pointer, _scrollbarThumbPointer))
        {
            _scrollbarThumbPointer = null;
            _isScrollbarThumbDragging = false;
            HideScrollPositionPreview();
        }

        _touchPointer = null;
        _isTouchDragging = false;
    }

    private void Touch_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (ReferenceEquals(e.Pointer, _touchPointer))
        {
            if (ReferenceEquals(e.Pointer, _scrollbarThumbPointer))
            {
                _scrollbarThumbPointer = null;
                _isScrollbarThumbDragging = false;
                HideScrollPositionPreview();
            }

            _touchPointer = null;
            _isTouchDragging = false;
        }
    }

    private void UpdateScrollPositionPreview()
    {
        if (!_isScrollbarThumbDragging || _scrollPositionPreview == null || _scrollPositionPreviewText == null)
            return;

        var headerTitle = _currentStickyHeader?.HeaderTitle;
        if (string.IsNullOrWhiteSpace(headerTitle))
        {
            _scrollPositionPreview.IsVisible = false;
            return;
        }

        var previewHeight = _scrollPositionPreview.Height;
        var top = Math.Clamp(_scrollbarPreviewY - previewHeight / 2, 0, Math.Max(0, Bounds.Height - previewHeight));
        _scrollPositionPreview.Margin = new Thickness(0, top, 36, 0);
        _scrollPositionPreviewText.Text = headerTitle;
        _scrollPositionPreview.IsVisible = true;
    }

    private void HideScrollPositionPreview()
    {
        if (_scrollPositionPreview != null)
            _scrollPositionPreview.IsVisible = false;
        if (_scrollPositionPreviewText != null)
            _scrollPositionPreviewText.Text = string.Empty;
    }

    private void Touch_Tapped(object? sender, TappedEventArgs e)
    {
        if (_suppressNextTouch)
        {
            e.Handled = true;
            _suppressNextTouch = false;
        }
    }

    private void DayVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DayViewModel.Takes) && _dayVm != null)
        {
            _dayVm.Takes.CollectionChanged -= Takes_CollectionChanged;
            _dayVm.Takes.CollectionChanged += Takes_CollectionChanged;
        }
    }

    private void Background_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Avalonia.Visual sourceVisual && sourceVisual.FindAncestorOfType<TextBox>() != null)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        topLevel?.FocusManager?.Focus(null);
    }

    private void Takes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var addedTake = e.Action == NotifyCollectionChangedAction.Add
            ? e.NewItems?.OfType<TakeViewModel>().FirstOrDefault()
            : null;

        if (addedTake != null && _dayVm != null && !_dayVm.IsLoadingTakes)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var listItem = _dayVm.FlatTakeList
                    .OfType<TakeListTakeViewModel>()
                    .FirstOrDefault(item => ReferenceEquals(item.Take, addedTake));

                if (listItem == null || AndroidTakeListBox == null)
                    return;

                AndroidTakeListBox.ScrollIntoView(listItem);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    AndroidTakeListBox.ScrollIntoView(listItem);

                    var container = AndroidTakeListBox.ContainerFromItem(listItem) as Control;
                    container?.BringIntoView();

                    if (container != null)
                    {
                        var firstTextBox = container.GetVisualDescendants()
                            .OfType<TextBox>()
                            .FirstOrDefault(tb => !tb.IsReadOnly && tb.IsEffectivelyVisible);

                        firstTextBox?.Focus();
                    }
                }, Avalonia.Threading.DispatcherPriority.Render);
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    private async void AddCamera_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DayViewModel dayVm)
            return;

        var textBox = this.FindControl<TextBox>("NewCameraLabelBox");
        var label = textBox?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(label))
            return;

        await dayVm.AddCameraCommand.ExecuteAsync(label);

        if (textBox is not null)
            textBox.Text = string.Empty;
    }
}