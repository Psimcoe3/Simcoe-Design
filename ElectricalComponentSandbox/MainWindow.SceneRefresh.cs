using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using System.Windows.Threading;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private void BeginFastInteractionMode()
    {
        _lastInteractionInputUtc = DateTime.UtcNow;
        if (!_isFastInteractionMode)
        {
            RenderOptions.SetBitmapScalingMode(PlanCanvas, BitmapScalingMode.LowQuality);
            _isFastInteractionMode = true;
        }

        if (!_interactionQualityRestoreTimer.IsEnabled)
            _interactionQualityRestoreTimer.Start();
    }

    private void InteractionQualityRestoreTimer_Tick(object? sender, EventArgs e)
    {
        if ((DateTime.UtcNow - _lastInteractionInputUtc).TotalMilliseconds < 180)
            return;

        _interactionQualityRestoreTimer.Stop();
        if (_isFastInteractionMode)
        {
            RenderOptions.SetBitmapScalingMode(PlanCanvas, BitmapScalingMode.HighQuality);
            _isFastInteractionMode = false;
        }
    }

    private void QueueSceneRefresh(bool update2D = false, bool update3D = false, bool updateProperties = false)
    {
        _pending2DRefresh |= update2D;
        _pending3DRefresh |= update3D;
        _pendingPropertiesRefresh |= updateProperties;

        if (_queuedSceneRefresh)
            return;

        _queuedSceneRefresh = true;

        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            _queuedSceneRefresh = false;
            var do2D = _pending2DRefresh;
            var do3D = _pending3DRefresh;
            var doProperties = _pendingPropertiesRefresh;
            _pending2DRefresh = false;
            _pending3DRefresh = false;
            _pendingPropertiesRefresh = false;

            if (do3D)
                UpdateViewport();
            if (do2D)
                Update2DCanvas();
            if (doProperties)
                UpdatePropertiesPanel();
            UpdateStatusBar();
        }));
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedComponent) ||
            e.PropertyName == nameof(MainViewModel.SelectedComponentIds))
        {
            QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
        }
        else if (e.PropertyName == nameof(MainViewModel.Components))
        {
            QueueSceneRefresh(update2D: true, update3D: true);
        }
    }

    private void MarkupTool_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MarkupToolViewModel.SelectedMarkup))
        {
            UpdateContextualInspector();
            UpdateCanvasGuidance();
            SkiaBackground.RequestRedraw();
        }
    }

    private void LayerManager_LayerRowChanged(object? sender, LayerRowChangedEventArgs e)
    {
        var layerVisibilityById = BuildLayerVisibilityLookup();

        if (_viewModel.ActiveLayer is { } activeLayer &&
            !IsLayerVisible(layerVisibilityById, activeLayer.Id))
        {
            _viewModel.ActiveLayer = _viewModel.Layers.FirstOrDefault(layer => IsLayerVisible(layerVisibilityById, layer.Id))
                ?? _viewModel.Layers.FirstOrDefault();
        }

        _viewModel.SelectedComponentIds.RemoveWhere(id =>
            _viewModel.Components.FirstOrDefault(component => string.Equals(component.Id, id, StringComparison.Ordinal)) is { } component &&
            !IsLayerVisible(layerVisibilityById, component.LayerId));

        if (_viewModel.SelectedComponent is { } selectedComponent &&
            !IsLayerVisible(layerVisibilityById, selectedComponent.LayerId))
        {
            var visibleSelected = _viewModel.Components
                .Where(component => _viewModel.SelectedComponentIds.Contains(component.Id) && IsLayerVisible(layerVisibilityById, component.LayerId))
                .ToList();

            if (visibleSelected.Count > 0)
                _viewModel.SetSelectedComponents(visibleSelected, visibleSelected[0]);
            else
                _viewModel.ClearComponentSelection();
        }

        if (_viewModel.MarkupTool.SelectedMarkup is { } selectedMarkup &&
            !IsLayerVisible(layerVisibilityById, selectedMarkup.LayerId))
        {
            _viewModel.MarkupTool.SelectedMarkup = null;
        }

        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
    }
}
