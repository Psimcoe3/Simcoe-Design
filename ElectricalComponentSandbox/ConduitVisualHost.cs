using System.Windows;
using System.Windows.Media;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private sealed class ConduitVisualHost : FrameworkElement
    {
        private readonly VisualCollection _visuals;
        private readonly DrawingVisual _drawingVisual;

        public ConduitVisualHost()
        {
            _visuals = new VisualCollection(this);
            _drawingVisual = new DrawingVisual();
            _visuals.Add(_drawingVisual);

            // Initialize visual tree first so any property-change callbacks
            // that query VisualChildrenCount cannot hit null state.
            IsHitTestVisible = false;
            SnapsToDevicePixels = true;
        }

        public void Render(Action<DrawingContext> draw)
        {
            using var dc = _drawingVisual.RenderOpen();
            draw(dc);
        }

        protected override int VisualChildrenCount => _visuals?.Count ?? 0;

        protected override Visual GetVisualChild(int index)
        {
            return _visuals[index];
        }
    }
}
