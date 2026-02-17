using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Microsoft.Win32;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.ViewModels;
using ElectricalComponentSandbox.Services;
using HelixToolkit.Wpf;

namespace ElectricalComponentSandbox;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private string? _currentFilePath;
    private readonly Dictionary<ModelVisual3D, ElectricalComponent> _visualToComponentMap = new();
    private bool _isEditingConduitPath = false;
    private readonly List<ModelVisual3D> _bendPointHandles = new();
    private ModelVisual3D? _draggedHandle = null;
    private Point _lastMousePosition;
    
    // 2D canvas state
    private bool _isDragging2D = false;
    private FrameworkElement? _draggedElement2D = null;
    private readonly Dictionary<FrameworkElement, ElectricalComponent> _canvasToComponentMap = new();
    
    // Constants for bend point visualization
    private const double BendPointHandleRadius = 0.3;
    private static readonly Color EditModeButtonColor = Color.FromRgb(255, 200, 100);
    private static readonly Color BendPointHandleColor = Colors.Orange;
    
    // Constants for smooth conduit rendering
    private const int MaxSegmentResolution = 50;
    private const int MinSegmentResolution = 5;
    private const double ResolutionScaleFactor = 10.0;
    
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }
    
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedComponent))
        {
            UpdatePropertiesPanel();
            UpdateViewport();
            Update2DCanvas();
        }
        else if (e.PropertyName == nameof(MainViewModel.Components))
        {
            UpdateViewport();
            Update2DCanvas();
        }
    }
    
    private void UpdatePropertiesPanel()
    {
        var component = _viewModel.SelectedComponent;
        if (component == null)
        {
            ClearPropertiesPanel();
            return;
        }
        
        NameTextBox.Text = component.Name;
        TypeTextBox.Text = component.Type.ToString();
        
        PositionXTextBox.Text = component.Position.X.ToString("F2");
        PositionYTextBox.Text = component.Position.Y.ToString("F2");
        PositionZTextBox.Text = component.Position.Z.ToString("F2");
        
        RotationXTextBox.Text = component.Rotation.X.ToString("F2");
        RotationYTextBox.Text = component.Rotation.Y.ToString("F2");
        RotationZTextBox.Text = component.Rotation.Z.ToString("F2");
        
        WidthTextBox.Text = component.Parameters.Width.ToString("F2");
        HeightTextBox.Text = component.Parameters.Height.ToString("F2");
        DepthTextBox.Text = component.Parameters.Depth.ToString("F2");
        MaterialTextBox.Text = component.Parameters.Material;
        ElevationTextBox.Text = component.Parameters.Elevation.ToString("F2");
        ColorTextBox.Text = component.Parameters.Color;
        
        // Set layer combo
        var layer = _viewModel.Layers.FirstOrDefault(l => l.Id == component.LayerId);
        if (layer != null)
            LayerComboBox.SelectedItem = layer;
        
        // Update conduit-specific properties
        if (component is ConduitComponent conduit)
        {
            ConduitProperties.Visibility = Visibility.Visible;
            BendPointsTextBlock.Text = conduit.BendPoints.Count.ToString();
        }
        else
        {
            ConduitProperties.Visibility = Visibility.Collapsed;
        }
    }
    
    private void ClearPropertiesPanel()
    {
        NameTextBox.Text = string.Empty;
        TypeTextBox.Text = string.Empty;
        PositionXTextBox.Text = string.Empty;
        PositionYTextBox.Text = string.Empty;
        PositionZTextBox.Text = string.Empty;
        RotationXTextBox.Text = string.Empty;
        RotationYTextBox.Text = string.Empty;
        RotationZTextBox.Text = string.Empty;
        WidthTextBox.Text = string.Empty;
        HeightTextBox.Text = string.Empty;
        DepthTextBox.Text = string.Empty;
        MaterialTextBox.Text = string.Empty;
        ElevationTextBox.Text = string.Empty;
        ColorTextBox.Text = string.Empty;
    }
    
    private void UpdateViewport()
    {
        // Clear existing models and mapping
        var itemsToRemove = Viewport.Children.OfType<ModelVisual3D>()
            .Where(m => m.Content is GeometryModel3D).ToList();
        
        foreach (var item in itemsToRemove)
        {
            Viewport.Children.Remove(item);
        }
        
        _visualToComponentMap.Clear();
        
        // Add components to viewport
        foreach (var component in _viewModel.Components)
        {
            // Check layer visibility
            var layer = _viewModel.Layers.FirstOrDefault(l => l.Id == component.LayerId);
            if (layer != null && !layer.IsVisible) continue;
            
            AddComponentToViewport(component);
        }
    }
    
    private void AddComponentToViewport(ElectricalComponent component)
    {
        var visual = new ModelVisual3D();
        var geometry = CreateComponentGeometry(component);
        
        var color = (Color)ColorConverter.ConvertFromString(component.Parameters.Color);
        var material = new DiffuseMaterial(new SolidColorBrush(color));
        
        Material appliedMaterial;
        if (component == _viewModel.SelectedComponent)
        {
            var materialGroup = new MaterialGroup();
            materialGroup.Children.Add(material);
            materialGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(80, 255, 165, 0))));
            appliedMaterial = materialGroup;
        }
        else
        {
            appliedMaterial = material;
        }
        
        var model = new GeometryModel3D(geometry, appliedMaterial);
        
        // Apply transformations
        var transformGroup = new Transform3DGroup();
        transformGroup.Children.Add(new TranslateTransform3D(component.Position.X, component.Position.Y, component.Position.Z));
        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), component.Rotation.X)));
        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), component.Rotation.Y)));
        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), component.Rotation.Z)));
        transformGroup.Children.Add(new ScaleTransform3D(component.Scale.X, component.Scale.Y, component.Scale.Z));
        
        model.Transform = transformGroup;
        visual.Content = model;
        
        Viewport.Children.Add(visual);
        _visualToComponentMap[visual] = component;
    }
    
    private MeshGeometry3D CreateComponentGeometry(ElectricalComponent component)
    {
        var builder = new MeshBuilder();
        
        switch (component.Type)
        {
            case ComponentType.Conduit:
                if (component is ConduitComponent conduit)
                {
                    CreateConduitGeometry(builder, conduit);
                }
                break;
                
            case ComponentType.Hanger:
                if (component is HangerComponent hanger)
                {
                    // Hanger rendered as a vertical rod
                    builder.AddCylinder(
                        new Point3D(0, 0, 0),
                        new Point3D(0, hanger.RodLength, 0),
                        hanger.RodDiameter, 12);
                }
                break;
                
            case ComponentType.CableTray:
                if (component is CableTrayComponent tray)
                {
                    // Cable tray rendered as an open-top box (U-channel)
                    var pathPts = tray.GetPathPoints();
                    if (pathPts.Count >= 2)
                    {
                        for (int i = 0; i < pathPts.Count - 1; i++)
                        {
                            var p1 = pathPts[i];
                            var p2 = pathPts[i + 1];
                            var dir = p2 - p1;
                            var segLen = dir.Length;
                            builder.AddBox(
                                new Point3D((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, (p1.Z + p2.Z) / 2),
                                tray.TrayWidth, tray.TrayDepth, segLen);
                        }
                    }
                    else
                    {
                        builder.AddBox(new Point3D(0, 0, 0), tray.TrayWidth, tray.TrayDepth, tray.Length);
                    }
                }
                break;
                
            case ComponentType.Box:
            case ComponentType.Panel:
            case ComponentType.Support:
                builder.AddBox(new Point3D(0, 0, 0), 
                    component.Parameters.Width, 
                    component.Parameters.Height, 
                    component.Parameters.Depth);
                break;
        }
        
        return builder.ToMesh();
    }
    
    private void CreateConduitGeometry(MeshBuilder builder, ConduitComponent conduit)
    {
        var pathPoints = conduit.GetPathPoints();
        
        if (pathPoints.Count == 2)
        {
            builder.AddCylinder(pathPoints[0], pathPoints[1], conduit.Diameter, 20);
            return;
        }
        
        var smoothPoints = GenerateSmoothPath(pathPoints, conduit.BendRadius);
        
        if (smoothPoints.Count >= 2)
        {
            for (int i = 0; i < smoothPoints.Count - 1; i++)
            {
                builder.AddCylinder(smoothPoints[i], smoothPoints[i + 1], conduit.Diameter, 20);
            }
        }
    }
    
    private List<Point3D> GenerateSmoothPath(List<Point3D> controlPoints, double bendRadius)
    {
        var smoothPoints = new List<Point3D>();
        
        if (controlPoints.Count < 2)
            return smoothPoints;
        
        int segmentResolution = Math.Min(MaxSegmentResolution, 
            Math.Max(MinSegmentResolution, (int)(bendRadius * ResolutionScaleFactor)));
        
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            Point3D p0 = (i == 0) ? controlPoints[i] : controlPoints[i - 1];
            Point3D p1 = controlPoints[i];
            Point3D p2 = controlPoints[i + 1];
            Point3D p3 = (i + 2 < controlPoints.Count) ? controlPoints[i + 2] : controlPoints[i + 1];
            
            for (int j = 0; j < segmentResolution; j++)
            {
                double t = (double)j / segmentResolution;
                Point3D interpolated = CatmullRomInterpolate(p0, p1, p2, p3, t);
                smoothPoints.Add(interpolated);
            }
        }
        
        smoothPoints.Add(controlPoints[controlPoints.Count - 1]);
        
        return smoothPoints;
    }
    
    private Point3D CatmullRomInterpolate(Point3D p0, Point3D p1, Point3D p2, Point3D p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;
        
        double c0 = -0.5 * t3 + t2 - 0.5 * t;
        double c1 = 1.5 * t3 - 2.5 * t2 + 1.0;
        double c2 = -1.5 * t3 + 2.0 * t2 + 0.5 * t;
        double c3 = 0.5 * t3 - 0.5 * t2;
        
        double x = c0 * p0.X + c1 * p1.X + c2 * p2.X + c3 * p3.X;
        double y = c0 * p0.Y + c1 * p1.Y + c2 * p2.Y + c3 * p3.Y;
        double z = c0 * p0.Z + c1 * p1.Z + c2 * p2.Z + c3 * p3.Z;
        
        return new Point3D(x, y, z);
    }
    
    // ===== 2D Plan Canvas =====
    
    private void Update2DCanvas()
    {
        PlanCanvas.Children.Clear();
        _canvasToComponentMap.Clear();
        
        // Draw grid if enabled
        if (_viewModel.ShowGrid)
        {
            Draw2DGrid();
        }
        
        // Draw components
        foreach (var component in _viewModel.Components)
        {
            var layer = _viewModel.Layers.FirstOrDefault(l => l.Id == component.LayerId);
            if (layer != null && !layer.IsVisible) continue;
            
            Draw2DComponent(component);
        }
    }
    
    private void Draw2DGrid()
    {
        double gridSize = _viewModel.GridSize * 20; // Scale for display
        for (double x = 0; x < PlanCanvas.Width; x += gridSize)
        {
            var line = new Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = PlanCanvas.Height,
                Stroke = Brushes.LightGray, StrokeThickness = 0.5
            };
            PlanCanvas.Children.Add(line);
        }
        for (double y = 0; y < PlanCanvas.Height; y += gridSize)
        {
            var line = new Line
            {
                X1 = 0, Y1 = y, X2 = PlanCanvas.Width, Y2 = y,
                Stroke = Brushes.LightGray, StrokeThickness = 0.5
            };
            PlanCanvas.Children.Add(line);
        }
    }
    
    private void Draw2DComponent(ElectricalComponent component)
    {
        var color = (Color)ColorConverter.ConvertFromString(component.Parameters.Color);
        var brush = new SolidColorBrush(color);
        var isSelected = component == _viewModel.SelectedComponent;
        
        FrameworkElement element;
        
        double canvasX = 1000 + component.Position.X * 20; // Center + scale
        double canvasY = 1000 - component.Position.Z * 20; // Flip Y for plan view
        
        switch (component.Type)
        {
            case ComponentType.Conduit:
                if (component is ConduitComponent conduit)
                {
                    var pathPts = conduit.GetPathPoints();
                    if (pathPts.Count >= 2)
                    {
                        var polyline = new Polyline
                        {
                            Stroke = brush,
                            StrokeThickness = Math.Max(2, conduit.Diameter * 10),
                            StrokeLineJoin = PenLineJoin.Round
                        };
                        foreach (var pt in pathPts)
                        {
                            polyline.Points.Add(new Point(
                                canvasX + pt.X * 20,
                                canvasY - pt.Z * 20));
                        }
                        if (isSelected)
                        {
                            polyline.Stroke = Brushes.Orange;
                            polyline.StrokeThickness += 2;
                        }
                        PlanCanvas.Children.Add(polyline);
                        _canvasToComponentMap[polyline] = component;
                        return;
                    }
                }
                element = CreateRectElement(component.Parameters.Width * 20, component.Parameters.Depth * 20, brush, isSelected);
                break;
                
            case ComponentType.CableTray:
                element = CreateRectElement(component.Parameters.Width * 20, component.Parameters.Depth * 20, brush, isSelected);
                break;
                
            case ComponentType.Hanger:
                var ellipse = new Ellipse
                {
                    Width = 10, Height = 10,
                    Fill = brush,
                    Stroke = isSelected ? Brushes.Orange : Brushes.Black,
                    StrokeThickness = isSelected ? 3 : 1
                };
                Canvas.SetLeft(ellipse, canvasX - 5);
                Canvas.SetTop(ellipse, canvasY - 5);
                PlanCanvas.Children.Add(ellipse);
                _canvasToComponentMap[ellipse] = component;
                return;
                
            default:
                element = CreateRectElement(component.Parameters.Width * 20, component.Parameters.Height * 20, brush, isSelected);
                break;
        }
        
        Canvas.SetLeft(element, canvasX - ((element as Rectangle)?.Width ?? 20) / 2);
        Canvas.SetTop(element, canvasY - ((element as Rectangle)?.Height ?? 20) / 2);
        PlanCanvas.Children.Add(element);
        _canvasToComponentMap[element] = component;
    }
    
    private Rectangle CreateRectElement(double width, double height, Brush fill, bool isSelected)
    {
        return new Rectangle
        {
            Width = Math.Max(5, width),
            Height = Math.Max(5, height),
            Fill = fill,
            Stroke = isSelected ? Brushes.Orange : Brushes.Black,
            StrokeThickness = isSelected ? 3 : 1
        };
    }
    
    // ===== 2D Canvas mouse handlers =====
    
    private void PlanCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(PlanCanvas);
        
        // Check if clicking on an existing component
        var hit = PlanCanvas.InputHitTest(pos) as FrameworkElement;
        if (hit != null && _canvasToComponentMap.ContainsKey(hit))
        {
            _viewModel.SelectedComponent = _canvasToComponentMap[hit];
            _isDragging2D = true;
            _draggedElement2D = hit;
            _lastMousePosition = pos;
            PlanCanvas.CaptureMouse();
            return;
        }
        
        // No component hit
        PlanCanvas.CaptureMouse();
    }
    
    private void PlanCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(PlanCanvas);
        
        if (_isDragging2D && _draggedElement2D != null && _viewModel.SelectedComponent != null)
        {
            var delta = pos - _lastMousePosition;
            var worldDelta = new Vector3D(delta.X / 20.0, 0, -delta.Y / 20.0);
            _viewModel.MoveComponent(worldDelta);
            _lastMousePosition = pos;
            Update2DCanvas();
        }
    }
    
    private void PlanCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging2D = false;
        _draggedElement2D = null;
        PlanCanvas.ReleaseMouseCapture();
    }
    
    private void PlanCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double zoom = e.Delta > 0 ? 1.1 : 0.9;
        PlanCanvasScale.ScaleX *= zoom;
        PlanCanvasScale.ScaleY *= zoom;
        
        // Clamp zoom
        PlanCanvasScale.ScaleX = Math.Max(0.1, Math.Min(10, PlanCanvasScale.ScaleX));
        PlanCanvasScale.ScaleY = Math.Max(0.1, Math.Min(10, PlanCanvasScale.ScaleY));
    }
    
    // ===== Component Add Handlers =====
    
    private void AddConduit_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddComponent(ComponentType.Conduit);
    }
    
    private void AddBox_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddComponent(ComponentType.Box);
    }
    
    private void AddPanel_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddComponent(ComponentType.Panel);
    }
    
    private void AddSupport_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddComponent(ComponentType.Support);
    }
    
    private void AddCableTray_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddComponent(ComponentType.CableTray);
    }
    
    private void AddHanger_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddComponent(ComponentType.Hanger);
    }
    
    private void DeleteComponent_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DeleteSelectedComponent();
    }
    
    private void LibraryItem_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (LibraryListBox.SelectedItem is ElectricalComponent component)
        {
            _viewModel.AddComponent(component.Type);
        }
    }
    
    // ===== Undo/Redo =====
    
    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Undo();
        UpdateViewport();
        Update2DCanvas();
    }
    
    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Redo();
        UpdateViewport();
        Update2DCanvas();
    }
    
    // ===== Layer Management =====
    
    private void AddLayer_Click(object sender, RoutedEventArgs e)
    {
        var name = $"Layer {_viewModel.Layers.Count + 1}";
        _viewModel.AddLayer(name);
    }
    
    private void RemoveLayer_Click(object sender, RoutedEventArgs e)
    {
        if (LayerListBox.SelectedItem is Layer layer)
        {
            _viewModel.RemoveLayer(layer);
        }
    }
    
    private void LayerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LayerListBox.SelectedItem is Layer layer)
        {
            _viewModel.ActiveLayer = layer;
        }
    }
    
    // ===== Unit System =====
    
    private void UnitSystem_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null) return;

        if (UnitSystemCombo?.SelectedItem is ComboBoxItem item)
        {
            _viewModel.UnitSystemName = item.Content?.ToString() ?? "Imperial";
        }
    }
    
    // ===== View Switching =====
    
    private void Show2DView_Click(object sender, RoutedEventArgs e)
    {
        ViewTabs.SelectedIndex = 1;
        Update2DCanvas();
    }
    
    private void Show3DView_Click(object sender, RoutedEventArgs e)
    {
        ViewTabs.SelectedIndex = 0;
    }
    
    // ===== PDF Underlay =====
    
    private void ImportPdf_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf|Image Files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|All Files (*.*)|*.*",
            Title = "Import PDF/Image Underlay"
        };
        
        if (dialog.ShowDialog() == true)
        {
            _viewModel.PdfUnderlay = new PdfUnderlay
            {
                FilePath = dialog.FileName,
                Opacity = PdfOpacitySlider.Value,
                IsLocked = PdfLockCheck.IsChecked ?? true
            };
            
            MessageBox.Show($"Underlay imported: {System.IO.Path.GetFileName(dialog.FileName)}\n" +
                "Use 'Calibrate Scale' to set the drawing scale.", 
                "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    private void PdfOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel?.PdfUnderlay != null)
        {
            _viewModel.PdfUnderlay.Opacity = e.NewValue;
        }
    }
    
    private void PdfLock_Changed(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.PdfUnderlay != null)
        {
            _viewModel.PdfUnderlay.IsLocked = PdfLockCheck.IsChecked ?? true;
        }
    }
    
    private void CalibrateScale_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.PdfUnderlay == null)
        {
            MessageBox.Show("Please import a PDF underlay first.", "No Underlay", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        MessageBox.Show("PDF Scale Calibration:\n\n" +
            "1. Click two known points on the PDF\n" +
            "2. Enter the real-world distance between them\n\n" +
            "This feature requires picking two points on the 2D canvas.",
            "Calibrate Scale", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    // ===== 3D Viewport interaction =====
    
    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(Viewport);
        var hits = Viewport3DHelper.FindHits(Viewport.Viewport, position);
        
        if (_isEditingConduitPath && _viewModel.SelectedComponent is ConduitComponent conduit)
        {
            var handleHit = hits?
                .Select(hit => hit.Visual)
                .OfType<ModelVisual3D>()
                .FirstOrDefault(v => _bendPointHandles.Contains(v));
            
            if (handleHit != null)
            {
                _draggedHandle = handleHit;
                _lastMousePosition = position;
                Mouse.Capture(Viewport);
                Viewport.MouseMove += Viewport_MouseMove;
                Viewport.MouseLeftButtonUp += Viewport_MouseLeftButtonUp;
                e.Handled = true;
                return;
            }
            
            var rayHit = hits?.FirstOrDefault();
            if (rayHit != null)
            {
                var hitPoint = rayHit.Position;
                var offset = hitPoint - _viewModel.SelectedComponent.Position;
                var localPoint = new Point3D(offset.X, offset.Y, offset.Z);
                
                conduit.BendPoints.Add(localPoint);
                UpdateViewport();
                ShowBendPointHandles();
                e.Handled = true;
                return;
            }
        }
        
        var matchedComponent = hits?
            .Select(hit => hit.Visual)
            .OfType<ModelVisual3D>()
            .Where(visual => _visualToComponentMap.ContainsKey(visual))
            .Select(visual => _visualToComponentMap[visual])
            .FirstOrDefault();
        
        _viewModel.SelectedComponent = matchedComponent;
        
        if (_isEditingConduitPath && matchedComponent is ConduitComponent)
        {
            ShowBendPointHandles();
        }
    }
    
    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedHandle == null || _viewModel.SelectedComponent is not ConduitComponent conduit)
            return;
        
        var position = e.GetPosition(Viewport);
        
        int handleIndex = _bendPointHandles.IndexOf(_draggedHandle);
        if (handleIndex >= 0)
        {
            if (handleIndex < conduit.BendPoints.Count)
            {
                var hits = Viewport3DHelper.FindHits(Viewport.Viewport, position);
                if (hits != null && hits.Any())
                {
                    var hitPoint = hits.First().Position;
                    var offset = hitPoint - _viewModel.SelectedComponent.Position;
                    var newPoint = new Point3D(offset.X, offset.Y, offset.Z);
                    
                    if (_viewModel.SnapToGrid)
                    {
                        newPoint.X = Math.Round(newPoint.X / _viewModel.GridSize) * _viewModel.GridSize;
                        newPoint.Y = Math.Round(newPoint.Y / _viewModel.GridSize) * _viewModel.GridSize;
                        newPoint.Z = Math.Round(newPoint.Z / _viewModel.GridSize) * _viewModel.GridSize;
                    }
                    
                    conduit.BendPoints[handleIndex] = newPoint;
                    UpdateViewport();
                    ShowBendPointHandles();
                }
            }
        }
        
        _lastMousePosition = position;
    }
    
    private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _draggedHandle = null;
        Mouse.Capture(null);
        Viewport.MouseMove -= Viewport_MouseMove;
        Viewport.MouseLeftButtonUp -= Viewport_MouseLeftButtonUp;
    }
    
    private void ClearBendPoints_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent is ConduitComponent conduit)
        {
            if (MessageBox.Show("Clear all bend points from this conduit?", "Confirm", 
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                conduit.BendPoints.Clear();
                UpdateViewport();
                UpdatePropertiesPanel();
                
                if (_isEditingConduitPath)
                {
                    ShowBendPointHandles();
                }
            }
        }
    }
    
    private void DeleteLastBendPoint_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent is ConduitComponent conduit && conduit.BendPoints.Count > 0)
        {
            conduit.BendPoints.RemoveAt(conduit.BendPoints.Count - 1);
            UpdateViewport();
            UpdatePropertiesPanel();
            
            if (_isEditingConduitPath)
            {
                ShowBendPointHandles();
            }
        }
        else
        {
            MessageBox.Show("No bend points to delete.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    private void ApplyProperties_Click(object sender, RoutedEventArgs e)
    {
        var component = _viewModel.SelectedComponent;
        if (component == null) return;
        
        try
        {
            component.Name = NameTextBox.Text;
            
            component.Position = new Point3D(
                double.Parse(PositionXTextBox.Text),
                double.Parse(PositionYTextBox.Text),
                double.Parse(PositionZTextBox.Text));
            
            component.Rotation = new Vector3D(
                double.Parse(RotationXTextBox.Text),
                double.Parse(RotationYTextBox.Text),
                double.Parse(RotationZTextBox.Text));
            
            component.Parameters.Width = double.Parse(WidthTextBox.Text);
            component.Parameters.Height = double.Parse(HeightTextBox.Text);
            component.Parameters.Depth = double.Parse(DepthTextBox.Text);
            component.Parameters.Material = MaterialTextBox.Text;
            component.Parameters.Elevation = double.Parse(ElevationTextBox.Text);
            component.Parameters.Color = ColorTextBox.Text;
            
            // Update layer assignment
            if (LayerComboBox.SelectedItem is Layer layer)
            {
                component.LayerId = layer.Id;
            }
            
            UpdateViewport();
            Update2DCanvas();
            MessageBox.Show("Properties updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error updating properties: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // ===== Project File Operations =====
    
    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Create a new project? Any unsaved changes will be lost.", "New Project", 
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _viewModel.Components.Clear();
            _viewModel.Layers.Clear();
            _viewModel.PdfUnderlay = null;
            _viewModel.UndoRedo.Clear();
            _currentFilePath = null;
            Title = "Electrical Component Sandbox";
            
            // Re-initialize default layer
            var defaultLayer = Layer.CreateDefault();
            _viewModel.Layers.Add(defaultLayer);
            _viewModel.ActiveLayer = defaultLayer;
            
            UpdateViewport();
            Update2DCanvas();
        }
    }
    
    private async void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = Services.ProjectFileService.GetFileFilter(),
            Title = "Open Project File"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var project = await _viewModel.ProjectFileService.LoadProjectAsync(dialog.FileName);
                if (project != null)
                {
                    _viewModel.LoadFromProject(project);
                    _currentFilePath = dialog.FileName;
                    Title = $"Electrical Component Sandbox - {System.IO.Path.GetFileName(dialog.FileName)}";
                    UpdateViewport();
                    Update2DCanvas();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private async void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveProjectAs_Click(sender, e);
            return;
        }
        
        await SaveProjectAsync(_currentFilePath);
    }
    
    private async void SaveProjectAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = Services.ProjectFileService.GetFileFilter(),
            Title = "Save Project File"
        };
        
        if (dialog.ShowDialog() == true)
        {
            await SaveProjectAsync(dialog.FileName);
            _currentFilePath = dialog.FileName;
            Title = $"Electrical Component Sandbox - {System.IO.Path.GetFileName(dialog.FileName)}";
        }
    }
    
    private async Task SaveProjectAsync(string filePath)
    {
        try
        {
            var project = _viewModel.ToProjectModel();
            await _viewModel.ProjectFileService.SaveProjectAsync(project, filePath);
            MessageBox.Show("Project saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent == null)
        {
            MessageBox.Show("Please select a component to export.", "No Component", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Export to JSON"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _viewModel.FileService.ExportToJsonAsync(_viewModel.SelectedComponent, dialog.FileName);
                MessageBox.Show("Component exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private async void ExportBomCsv_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.Components.Any())
        {
            MessageBox.Show("No components to export.", "No Components", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var dialog = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            Title = "Export Bill of Materials"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _viewModel.BomExport.ExportToCsvAsync(_viewModel.Components, dialog.FileName);
                MessageBox.Show("BOM exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting BOM: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        
        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (e.Key == Key.S)
            {
                SaveProjectAs_Click(sender, e);
                e.Handled = true;
            }
        }
        else if (modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    NewProject_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.O:
                    OpenProject_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.S:
                    SaveProject_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.E:
                    ExportJson_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.Z:
                    Undo_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.Y:
                    Redo_Click(sender, e);
                    e.Handled = true;
                    break;
            }
        }
        else if (modifiers == ModifierKeys.None)
        {
            if (e.Key == Key.Delete)
            {
                DeleteComponent_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _isEditingConduitPath)
            {
                ToggleEditConduitPath_Click(sender, e);
                e.Handled = true;
            }
        }
    }
    
    private void ToggleEditConduitPath_Click(object sender, RoutedEventArgs e)
    {
        _isEditingConduitPath = !_isEditingConduitPath;
        
        if (_isEditingConduitPath)
        {
            EditConduitPathButton.Background = new SolidColorBrush(EditModeButtonColor);
            EditConduitPathButton.Content = "Exit Edit Mode";
            
            if (_viewModel.SelectedComponent is ConduitComponent)
            {
                ShowBendPointHandles();
                MessageBox.Show("Edit Mode Active:\n" +
                    "• Click on conduit to add bend points\n" +
                    "• Drag orange handles to move bend points\n" +
                    "• Use 'Clear All Bend Points' to reset\n" +
                    "• Click 'Exit Edit Mode' when done", 
                    "Edit Conduit Path", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _isEditingConduitPath = false;
                EditConduitPathButton.Background = System.Windows.SystemColors.ControlBrush;
                EditConduitPathButton.Content = "Edit Conduit Path";
                MessageBox.Show("Please select a conduit component first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else
        {
            EditConduitPathButton.Background = System.Windows.SystemColors.ControlBrush;
            EditConduitPathButton.Content = "Edit Conduit Path";
            HideBendPointHandles();
            
            if (_draggedHandle != null)
            {
                _draggedHandle = null;
                Mouse.Capture(null);
                Viewport.MouseMove -= Viewport_MouseMove;
                Viewport.MouseLeftButtonUp -= Viewport_MouseLeftButtonUp;
            }
        }
    }
    
    private void ShowBendPointHandles()
    {
        HideBendPointHandles();
        
        if (_viewModel.SelectedComponent is not ConduitComponent conduit)
            return;
        
        var pathPoints = conduit.GetPathPoints();
        
        for (int i = 1; i < pathPoints.Count; i++)
        {
            var point = pathPoints[i];
            var handle = CreateBendPointHandle(point);
            _bendPointHandles.Add(handle);
            Viewport.Children.Add(handle);
        }
    }
    
    private void HideBendPointHandles()
    {
        foreach (var handle in _bendPointHandles)
        {
            Viewport.Children.Remove(handle);
        }
        _bendPointHandles.Clear();
    }
    
    private ModelVisual3D CreateBendPointHandle(Point3D position)
    {
        var visual = new ModelVisual3D();
        var builder = new MeshBuilder();
        builder.AddSphere(new Point3D(0, 0, 0), BendPointHandleRadius, 12, 12);
        
        var material = new DiffuseMaterial(new SolidColorBrush(BendPointHandleColor));
        var emissive = new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(100, 255, 165, 0)));
        var materialGroup = new MaterialGroup();
        materialGroup.Children.Add(material);
        materialGroup.Children.Add(emissive);
        
        var model = new GeometryModel3D(builder.ToMesh(), materialGroup);
        
        if (_viewModel.SelectedComponent != null)
        {
            var transformGroup = new Transform3DGroup();
            var globalPos = _viewModel.SelectedComponent.Position + new Vector3D(position.X, position.Y, position.Z);
            transformGroup.Children.Add(new TranslateTransform3D(globalPos.X, globalPos.Y, globalPos.Z));
            model.Transform = transformGroup;
        }
        
        visual.Content = model;
        return visual;
    }
}
