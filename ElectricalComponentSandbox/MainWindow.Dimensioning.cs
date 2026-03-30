using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using HelixToolkit.Wpf;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    internal void UpdatePropertiesPanelForTesting() => UpdatePropertiesPanel();

    private DimensionAxisOffsets GetDimensionAxisOffsets(ElectricalComponent component)
    {
        if (!_dimensionOffsetsByComponentId.TryGetValue(component.Id, out var offsets))
        {
            offsets = new DimensionAxisOffsets();
            _dimensionOffsetsByComponentId[component.Id] = offsets;
        }

        return offsets;
    }

    private double GetDimensionAxisOffset(ElectricalComponent component, char axis)
    {
        var offsets = GetDimensionAxisOffsets(component);
        return axis switch
        {
            'X' => offsets.X,
            'Y' => offsets.Y,
            'Z' => offsets.Z,
            _ => 0.0
        };
    }

    private void SetDimensionAxisOffset(ElectricalComponent component, char axis, double offsetFeet)
    {
        var offsets = GetDimensionAxisOffsets(component);
        var sanitized = Math.Max(0.0, offsetFeet);
        switch (axis)
        {
            case 'X':
                offsets.X = sanitized;
                break;
            case 'Y':
                offsets.Y = sanitized;
                break;
            case 'Z':
                offsets.Z = sanitized;
                break;
        }
    }

    private double GetDisplayIncrementFeet()
    {
        var denominator = Math.Max(1, _dimensionInchFractionDenominator);
        return 1.0 / (UnitConversionService.InchesPerFoot * denominator);
    }

    private double RoundLengthToDisplayIncrement(double value)
    {
        var incrementFeet = GetDisplayIncrementFeet();
        return Math.Round(value / incrementFeet, MidpointRounding.AwayFromZero) * incrementFeet;
    }

    private string FormatLengthForInput(double value)
    {
        if (_dimensionDisplayMode == DimensionDisplayMode.FeetInches)
            return UnitConversionService.FormatFeetInches(value, _dimensionInchFractionDenominator);

        var rounded = RoundLengthToDisplayIncrement(value);
        return rounded.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    private string FormatLengthForDisplay(double value)
    {
        if (_dimensionDisplayMode == DimensionDisplayMode.FeetInches)
            return UnitConversionService.FormatFeetInches(value, _dimensionInchFractionDenominator);

        var rounded = RoundLengthToDisplayIncrement(value);
        return $"{rounded.ToString("0.#####", CultureInfo.InvariantCulture)} ft";
    }

    private static string BuildDimensionLabel(IReadOnlyCollection<string> labels)
    {
        if (labels.Count == 0)
            return "Dimension";
        if (labels.Count == 1)
            return labels.First();

        return string.Join("/", labels);
    }

    private List<DimensionEntry> BuildDimensionEntries(ElectricalComponent component)
    {
        var entries = new List<DimensionEntry>();

        void Add(string label, double value)
        {
            if (IsFinitePositiveLength(value))
                entries.Add(new DimensionEntry(label, value));
        }

        Add("Width", component.Parameters.Width);
        Add("Height", component.Parameters.Height);
        Add("Depth", component.Parameters.Depth);

        switch (component)
        {
            case ConduitComponent conduit:
                SyncConduitDimensionalParameters(conduit);
                Add("Diameter", conduit.Diameter);
                Add("Length", conduit.Length);
                Add("Bend Radius", conduit.BendRadius);

                var bendSetting = TryGetConduitImperialBendSetting(conduit);
                if (bendSetting != null)
                {
                    Add("Take-up (90)", bendSetting.DeductFeet);
                    Add("Min Bend Spacing", bendSetting.MinimumDistanceBetweenBendsFeet);
                    Add("Min Kick-90", bendSetting.MinimumDistanceKick90Feet);
                    Add("Min Offset Spacing", bendSetting.MinimumDistanceOffsetFeet);
                    Add("Min 3-Point Saddle", bendSetting.MinimumDistanceSaddle3PointFeet);
                    Add("Min 4-Point Saddle", bendSetting.MinimumDistanceSaddle4PointFeet);
                    Add("Min Stub", bendSetting.DefaultEndLengthStubFeet);

                    foreach (var minimumHeight in bendSetting.MinimumHeightByAngleFeet.OrderBy(entry => entry.Key))
                    {
                        if (minimumHeight.Value > InViewDimensionMinSpan)
                            Add($"Min Height @{minimumHeight.Key:0.#}deg", minimumHeight.Value);
                    }
                }

                var tradeSize = ResolveConduitTradeSize(conduit);
                var spacingFeet = tradeSize == null ? null : _stratusImperialDefaults.FindParallelSpacingFeet(tradeSize, tradeSize);
                if (spacingFeet.HasValue && spacingFeet.Value > 0.0)
                    Add("Parallel Spacing (CTC)", spacingFeet.Value);
                break;

            case CableTrayComponent tray:
                Add("Tray Width", tray.TrayWidth);
                Add("Tray Depth", tray.TrayDepth);
                Add("Length", tray.Length);
                break;

            case HangerComponent hanger:
                Add("Rod Diameter", hanger.RodDiameter);
                Add("Rod Length", hanger.RodLength);
                break;
        }

        return entries;
    }

    private void UpdateSelectedDimensionsDisplay(ElectricalComponent? component)
    {
        if (component == null)
        {
            UpdateSelectedDimensionsDisplay(Array.Empty<ElectricalComponent>());
            return;
        }

        UpdateSelectedDimensionsDisplay(new[] { component });
    }

    private void UpdateSelectedDimensionsDisplay(IReadOnlyList<ElectricalComponent> components)
    {
        if (SelectedDimensionsListBox == null)
            return;

        if (components.Count == 0)
        {
            SelectedDimensionsListBox.ItemsSource = null;
            SelectedDimensionsListBox.Items.Clear();
            return;
        }

        var entries = components
            .SelectMany(BuildDimensionEntries)
            .ToList();
        if (entries.Count == 0)
        {
            SelectedDimensionsListBox.ItemsSource = new[] { "No dimensions available." };
            return;
        }

        var incrementFeet = GetDisplayIncrementFeet();
        var uniqueRows = entries
            .GroupBy(entry => (long)Math.Round(entry.FeetValue / incrementFeet, MidpointRounding.AwayFromZero))
            .Select(group =>
            {
                var quantizedFeet = group.Key * incrementFeet;
                var labels = group.Select(g => g.Label).Distinct(StringComparer.Ordinal).ToList();
                return $"{BuildDimensionLabel(labels)}: {FormatLengthForDisplay(quantizedFeet)}";
            })
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();

        SelectedDimensionsListBox.ItemsSource = uniqueRows;
    }

    private double ParseLengthInput(string input, string fieldName)
    {
        if (UnitConversionService.TryParseLength(input, out var value))
            return value;

        throw new FormatException($"Invalid {fieldName} value. Use feet-inches (example: 1'-3 1/2\") or decimal feet.");
    }

    private static Point3D[] BuildBoundsCorners(Rect3D bounds)
    {
        var minX = bounds.X;
        var minY = bounds.Y;
        var minZ = bounds.Z;
        var maxX = bounds.X + bounds.SizeX;
        var maxY = bounds.Y + bounds.SizeY;
        var maxZ = bounds.Z + bounds.SizeZ;

        return
        [
            new Point3D(minX, minY, minZ),
            new Point3D(minX, minY, maxZ),
            new Point3D(minX, maxY, minZ),
            new Point3D(minX, maxY, maxZ),
            new Point3D(maxX, minY, minZ),
            new Point3D(maxX, minY, maxZ),
            new Point3D(maxX, maxY, minZ),
            new Point3D(maxX, maxY, maxZ)
        ];
    }

    private static Rect3D CreateBoundsFromPoints(IReadOnlyList<Point3D> points)
    {
        if (points.Count == 0)
            return Rect3D.Empty;

        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var minZ = points.Min(p => p.Z);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);
        var maxZ = points.Max(p => p.Z);
        return new Rect3D(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
    }

    private static bool IsValidBounds(Rect3D bounds)
    {
        return !bounds.IsEmpty &&
               !double.IsNaN(bounds.SizeX) &&
               !double.IsNaN(bounds.SizeY) &&
               !double.IsNaN(bounds.SizeZ) &&
               !double.IsInfinity(bounds.SizeX) &&
               !double.IsInfinity(bounds.SizeY) &&
               !double.IsInfinity(bounds.SizeZ);
    }

    private bool TryGetComponentWorldBounds(ElectricalComponent component, out Rect3D worldBounds)
    {
        var geometry = CreateComponentGeometry(component);
        if (geometry == null)
        {
            worldBounds = Rect3D.Empty;
            return false;
        }

        var transform = CreateComponentTransform(component);
        worldBounds = transform.TransformBounds(geometry.Bounds);
        return IsValidBounds(worldBounds);
    }

    private bool TryGetSelectedComponentWorldBounds(out Rect3D worldBounds)
    {
        var selected = _viewModel.SelectedComponent;
        if (selected == null)
        {
            worldBounds = Rect3D.Empty;
            return false;
        }

        return TryGetComponentWorldBounds(selected, out worldBounds);
    }

    private double EstimateWorldUnitsPerPixel(Point3D referencePoint)
    {
        if (Viewport.Camera is PerspectiveCamera perspective)
        {
            var distance = (perspective.Position - referencePoint).Length;
            var fovRadians = perspective.FieldOfView * Math.PI / 180.0;
            var worldHeight = 2.0 * Math.Max(0.001, distance) * Math.Tan(fovRadians / 2.0);
            var viewportHeight = Math.Max(1.0, Viewport.ActualHeight);
            return worldHeight / viewportHeight;
        }

        if (Viewport.Camera is OrthographicCamera orthographic)
        {
            var viewportHeight = Math.Max(1.0, Viewport.ActualHeight);
            return Math.Max(0.0001, orthographic.Width / viewportHeight);
        }

        return 0.01;
    }

    private List<Point3D> GetComponentDimensionReferencePoints(ElectricalComponent component, Transform3D transform, Rect3D worldBounds)
    {
        switch (component)
        {
            case ConduitComponent conduit:
                return conduit.GetPathPoints()
                    .Select(transform.Transform)
                    .ToList();

            case CableTrayComponent tray:
                return tray.GetPathPoints()
                    .Select(transform.Transform)
                    .ToList();

            default:
                return BuildBoundsCorners(worldBounds).ToList();
        }
    }

    private static List<AxisDimensionSpan> BuildPrimaryAxisDimensionSpans(Rect3D bounds)
    {
        var spans = new List<AxisDimensionSpan>(3);
        var maxX = bounds.X + bounds.SizeX;
        var maxY = bounds.Y + bounds.SizeY;
        var maxZ = bounds.Z + bounds.SizeZ;

        if (bounds.SizeX >= InViewDimensionMinSpan)
            spans.Add(new AxisDimensionSpan('X', bounds.SizeX, bounds.X, maxX));
        if (bounds.SizeY >= InViewDimensionMinSpan)
            spans.Add(new AxisDimensionSpan('Y', bounds.SizeY, bounds.Y, maxY));
        if (bounds.SizeZ >= InViewDimensionMinSpan)
            spans.Add(new AxisDimensionSpan('Z', bounds.SizeZ, bounds.Z, maxZ));

        return spans;
    }

    private static double SelectVisibleEdge(double min, double max, double cameraValue)
    {
        var center = (min + max) * 0.5;
        return cameraValue >= center ? max : min;
    }

    private static double GetOutwardSign(double selectedEdge, double min, double max)
    {
        var center = (min + max) * 0.5;
        return selectedEdge >= center ? 1.0 : -1.0;
    }

    private DimensionEdgePlacement BuildDimensionEdgePlacement(Rect3D bounds)
    {
        var minX = bounds.X;
        var minY = bounds.Y;
        var minZ = bounds.Z;
        var maxX = bounds.X + bounds.SizeX;
        var maxY = bounds.Y + bounds.SizeY;
        var maxZ = bounds.Z + bounds.SizeZ;

        Point3D cameraPosition;
        if (Viewport.Camera is ProjectionCamera projectionCamera)
        {
            cameraPosition = projectionCamera.Position;
        }
        else
        {
            cameraPosition = new Point3D(
                maxX + Math.Max(1.0, bounds.SizeX),
                maxY + Math.Max(1.0, bounds.SizeY),
                maxZ + Math.Max(1.0, bounds.SizeZ));
        }

        var edgeX = SelectVisibleEdge(minX, maxX, cameraPosition.X);
        var edgeY = SelectVisibleEdge(minY, maxY, cameraPosition.Y);
        var edgeZ = SelectVisibleEdge(minZ, maxZ, cameraPosition.Z);

        return new DimensionEdgePlacement(
            edgeX,
            edgeY,
            edgeZ,
            GetOutwardSign(edgeX, minX, maxX),
            GetOutwardSign(edgeY, minY, maxY),
            GetOutwardSign(edgeZ, minZ, maxZ));
    }

    private bool TryGetViewportCameraPosition(out Point3D cameraPosition)
    {
        if (Viewport.Camera is ProjectionCamera projectionCamera)
        {
            cameraPosition = projectionCamera.Position;
            return true;
        }

        cameraPosition = default;
        return false;
    }

    private static Rect3D CreateParameterPrismBounds(double width, double height, double depth)
    {
        var clampedWidth = Math.Max(InViewDimensionMinSpan, width);
        var clampedHeight = Math.Max(InViewDimensionMinSpan, height);
        var clampedDepth = Math.Max(InViewDimensionMinSpan, depth);
        return new Rect3D(
            -clampedWidth / 2.0,
            0.0,
            -clampedDepth / 2.0,
            clampedWidth,
            clampedHeight,
            clampedDepth);
    }

    private bool TryGetRenderedLocalDimensionBounds(ElectricalComponent component, out Rect3D localBounds)
    {
        var geometry = CreateComponentGeometry(component);
        if (geometry != null && IsValidBounds(geometry.Bounds))
        {
            localBounds = geometry.Bounds;
            return true;
        }

        localBounds = Rect3D.Empty;
        return false;
    }

    private bool TryGetNominalLocalDimensionBounds(ElectricalComponent component, out Rect3D localBounds)
    {
        static bool IsValidLength(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > InViewDimensionMinSpan;

        static double ChooseLength(double primary, double fallback)
        {
            if (IsValidLength(primary))
                return primary;
            if (IsValidLength(fallback))
                return fallback;
            return InViewDimensionMinSpan;
        }

        switch (component)
        {
            case HangerComponent hanger:
                localBounds = CreateParameterPrismBounds(
                    ChooseLength(component.Parameters.Width, hanger.RodDiameter),
                    ChooseLength(component.Parameters.Height, hanger.RodLength),
                    ChooseLength(component.Parameters.Depth, hanger.RodDiameter));
                return IsValidBounds(localBounds);

            case ConduitComponent conduit:
                if (TryGetRenderedLocalDimensionBounds(component, out localBounds))
                    return true;

                localBounds = CreateParameterPrismBounds(
                    ChooseLength(component.Parameters.Width, conduit.Diameter),
                    ChooseLength(component.Parameters.Height, conduit.Diameter),
                    ChooseLength(component.Parameters.Depth, conduit.Length));
                return IsValidBounds(localBounds);

            case CableTrayComponent tray:
                if (TryGetRenderedLocalDimensionBounds(component, out localBounds))
                    return true;

                localBounds = CreateParameterPrismBounds(
                    ChooseLength(component.Parameters.Width, tray.TrayWidth),
                    ChooseLength(component.Parameters.Height, tray.TrayDepth),
                    ChooseLength(component.Parameters.Depth, tray.Length));
                return IsValidBounds(localBounds);

            case BoxComponent:
            case PanelComponent:
            case SupportComponent:
            default:
                localBounds = CreateParameterPrismBounds(
                    ChooseLength(component.Parameters.Width, 0.0),
                    ChooseLength(component.Parameters.Height, 0.0),
                    ChooseLength(component.Parameters.Depth, 0.0));
                return IsValidBounds(localBounds);
        }
    }

    private static void AddUniqueSemanticPoint(List<Point3D> points, Point3D candidate)
    {
        const double tolerance = 1e-5;
        foreach (var existing in points)
        {
            if ((existing - candidate).Length <= tolerance)
                return;
        }

        points.Add(candidate);
    }

    private static List<SemanticEdge> BuildBoundsEdges(Rect3D bounds)
    {
        var corners = BuildBoundsCorners(bounds);
        return
        [
            new SemanticEdge(corners[0], corners[4]),
            new SemanticEdge(corners[1], corners[5]),
            new SemanticEdge(corners[2], corners[6]),
            new SemanticEdge(corners[3], corners[7]),
            new SemanticEdge(corners[0], corners[2]),
            new SemanticEdge(corners[1], corners[3]),
            new SemanticEdge(corners[4], corners[6]),
            new SemanticEdge(corners[5], corners[7]),
            new SemanticEdge(corners[0], corners[1]),
            new SemanticEdge(corners[2], corners[3]),
            new SemanticEdge(corners[4], corners[5]),
            new SemanticEdge(corners[6], corners[7])
        ];
    }

    private static List<SemanticFace> BuildBoundsFaces(Rect3D bounds)
    {
        var minX = bounds.X;
        var minY = bounds.Y;
        var minZ = bounds.Z;
        var maxX = bounds.X + bounds.SizeX;
        var maxY = bounds.Y + bounds.SizeY;
        var maxZ = bounds.Z + bounds.SizeZ;

        return
        [
            new SemanticFace('X', minX, minY, maxY, minZ, maxZ),
            new SemanticFace('X', maxX, minY, maxY, minZ, maxZ),
            new SemanticFace('Y', minY, minX, maxX, minZ, maxZ),
            new SemanticFace('Y', maxY, minX, maxX, minZ, maxZ),
            new SemanticFace('Z', minZ, minX, maxX, minY, maxY),
            new SemanticFace('Z', maxZ, minX, maxX, minY, maxY)
        ];
    }

    private static Point3D ProjectPointOntoFace(SemanticFace face, Point3D localPoint)
    {
        return face.Axis switch
        {
            'X' => new Point3D(
                face.AxisValue,
                Math.Max(face.MinA, Math.Min(face.MaxA, localPoint.Y)),
                Math.Max(face.MinB, Math.Min(face.MaxB, localPoint.Z))),
            'Y' => new Point3D(
                Math.Max(face.MinA, Math.Min(face.MaxA, localPoint.X)),
                face.AxisValue,
                Math.Max(face.MinB, Math.Min(face.MaxB, localPoint.Z))),
            _ => new Point3D(
                Math.Max(face.MinA, Math.Min(face.MaxA, localPoint.X)),
                Math.Max(face.MinB, Math.Min(face.MaxB, localPoint.Y)),
                face.AxisValue)
        };
    }

    private bool TryBuildComponentSemanticReferences(ElectricalComponent component, out ComponentSemanticReferences references)
    {
        references = null!;
        if (!TryGetNominalLocalDimensionBounds(component, out var localBounds))
            return false;

        var semantic = new ComponentSemanticReferences
        {
            LocalBounds = localBounds,
            CenterLocal = new Point3D(
                localBounds.X + localBounds.SizeX / 2.0,
                localBounds.Y + localBounds.SizeY / 2.0,
                localBounds.Z + localBounds.SizeZ / 2.0)
        };

        semantic.FaceLocals.AddRange(BuildBoundsFaces(localBounds));

        switch (component)
        {
            case ConduitComponent conduit:
            {
                var pathPoints = conduit.GetPathPoints();
                foreach (var point in pathPoints)
                    AddUniqueSemanticPoint(semantic.PointLocals, point);

                for (var index = 0; index < pathPoints.Count - 1; index++)
                {
                    if ((pathPoints[index + 1] - pathPoints[index]).Length >= InViewDimensionMinSpan)
                        semantic.EdgeLocals.Add(new SemanticEdge(pathPoints[index], pathPoints[index + 1]));
                }
                break;
            }

            case CableTrayComponent tray:
            {
                var pathPoints = tray.GetPathPoints();
                foreach (var point in pathPoints)
                    AddUniqueSemanticPoint(semantic.PointLocals, point);

                for (var index = 0; index < pathPoints.Count - 1; index++)
                {
                    if ((pathPoints[index + 1] - pathPoints[index]).Length >= InViewDimensionMinSpan)
                        semantic.EdgeLocals.Add(new SemanticEdge(pathPoints[index], pathPoints[index + 1]));
                }
                break;
            }

            default:
                foreach (var corner in BuildBoundsCorners(localBounds))
                    AddUniqueSemanticPoint(semantic.PointLocals, corner);

                semantic.EdgeLocals.AddRange(BuildBoundsEdges(localBounds));
                break;
        }

        AddUniqueSemanticPoint(semantic.PointLocals, semantic.CenterLocal);

        if (semantic.PointLocals.Count == 0)
        {
            foreach (var corner in BuildBoundsCorners(localBounds))
                AddUniqueSemanticPoint(semantic.PointLocals, corner);
        }

        if (semantic.EdgeLocals.Count == 0)
        {
            foreach (var edge in BuildBoundsEdges(localBounds))
            {
                if ((edge.EndLocal - edge.StartLocal).Length >= InViewDimensionMinSpan)
                    semantic.EdgeLocals.Add(edge);
            }
        }

        references = semantic;
        return true;
    }

    private List<AxisDimensionGuide> BuildAxisDimensionGuides(Rect3D localBounds, Transform3D transform)
    {
        var guides = new List<AxisDimensionGuide>(3);
        if (!IsValidBounds(localBounds))
            return guides;

        var minX = localBounds.X;
        var minY = localBounds.Y;
        var minZ = localBounds.Z;
        var maxX = minX + localBounds.SizeX;
        var maxY = minY + localBounds.SizeY;
        var maxZ = minZ + localBounds.SizeZ;

        var localCenter = new Point3D(
            minX + localBounds.SizeX / 2.0,
            minY + localBounds.SizeY / 2.0,
            minZ + localBounds.SizeZ / 2.0);

        var hasCamera = TryGetViewportCameraPosition(out var cameraWorld);
        Point3D localCamera;
        if (hasCamera && transform.Inverse != null)
        {
            localCamera = transform.Inverse.Transform(cameraWorld);
        }
        else
        {
            localCamera = new Point3D(
                maxX + Math.Max(1.0, localBounds.SizeX),
                maxY + Math.Max(1.0, localBounds.SizeY),
                maxZ + Math.Max(1.0, localBounds.SizeZ));
        }

        var xEdge = SelectVisibleEdge(minX, maxX, localCamera.X);
        var yEdge = SelectVisibleEdge(minY, maxY, localCamera.Y);
        var zEdge = SelectVisibleEdge(minZ, maxZ, localCamera.Z);
        var cornerLocal = new Point3D(xEdge, yEdge, zEdge);

        var centerWorld = transform.Transform(localCenter);
        var cornerWorld = transform.Transform(cornerLocal);

        AxisDimensionGuide? CreateGuide(char axis, Point3D startLocal, Point3D endLocal, double valueFeet)
        {
            if (valueFeet < InViewDimensionMinSpan)
                return null;

            var edgeStart = transform.Transform(startLocal);
            var edgeEnd = transform.Transform(endLocal);
            var axisDirection = edgeEnd - edgeStart;
            if (axisDirection.Length < 1e-6)
                return null;
            axisDirection.Normalize();

            var outward = cornerWorld - centerWorld;
            outward -= axisDirection * Vector3D.DotProduct(outward, axisDirection);

            if (outward.Length < 1e-6 && hasCamera)
            {
                outward = cameraWorld - centerWorld;
                outward -= axisDirection * Vector3D.DotProduct(outward, axisDirection);
            }

            if (outward.Length < 1e-6)
            {
                outward = Vector3D.CrossProduct(axisDirection, new Vector3D(0, 1, 0));
                if (outward.Length < 1e-6)
                    outward = Vector3D.CrossProduct(axisDirection, new Vector3D(1, 0, 0));
            }

            if (outward.Length < 1e-6)
                return null;

            outward.Normalize();

            var midpoint = new Point3D(
                (edgeStart.X + edgeEnd.X) / 2.0,
                (edgeStart.Y + edgeEnd.Y) / 2.0,
                (edgeStart.Z + edgeEnd.Z) / 2.0);
            var centerToMid = midpoint - centerWorld;
            if (Vector3D.DotProduct(outward, centerToMid) < 0)
                outward = -outward;

            return new AxisDimensionGuide(axis, valueFeet, edgeStart, edgeEnd, outward);
        }

        var xGuide = CreateGuide('X', new Point3D(minX, yEdge, zEdge), new Point3D(maxX, yEdge, zEdge), localBounds.SizeX);
        if (xGuide.HasValue)
            guides.Add(xGuide.Value);

        var yGuide = CreateGuide('Y', new Point3D(xEdge, minY, zEdge), new Point3D(xEdge, maxY, zEdge), localBounds.SizeY);
        if (yGuide.HasValue)
            guides.Add(yGuide.Value);

        var zGuide = CreateGuide('Z', new Point3D(xEdge, yEdge, minZ), new Point3D(xEdge, yEdge, maxZ), localBounds.SizeZ);
        if (zGuide.HasValue)
            guides.Add(zGuide.Value);

        return guides;
    }

    private void AddDimensionTicks(Point3D start, Point3D end, AxisDimensionGuide guide)
    {
        var axisDirection = end - start;
        if (axisDirection.Length < 1e-6)
            return;
        axisDirection.Normalize();

        var tickDirection = Vector3D.CrossProduct(axisDirection, guide.Outward);
        if (tickDirection.Length < 1e-6 && TryGetViewportCameraPosition(out var cameraPosition))
        {
            var toCamera = cameraPosition - start;
            tickDirection = Vector3D.CrossProduct(axisDirection, toCamera);
        }

        if (tickDirection.Length < 1e-6)
            return;

        tickDirection.Normalize();

        var dimensionLength = (end - start).Length;
        var tickHalfLength = Math.Max(0.03, dimensionLength * 0.03);

        AddDimensionLineVisual(start - tickDirection * tickHalfLength, start + tickDirection * tickHalfLength, guide.Axis);
        AddDimensionLineVisual(end - tickDirection * tickHalfLength, end + tickDirection * tickHalfLength, guide.Axis);
    }

    private void DrawAxisDimensionGuide(AxisDimensionGuide guide, double axisOffset)
    {
        var clampedOffset = Math.Max(0.0, axisOffset);
        var offsetVector = guide.Outward * clampedOffset;
        var lineStart = guide.EdgeStart + offsetVector;
        var lineEnd = guide.EdgeEnd + offsetVector;

        AddDimensionLineVisual(lineStart, lineEnd, guide.Axis);
        AddDimensionLineVisual(guide.EdgeStart, lineStart, guide.Axis);
        AddDimensionLineVisual(guide.EdgeEnd, lineEnd, guide.Axis);
        AddDimensionTicks(lineStart, lineEnd, guide);

        var dimensionLength = (guide.EdgeEnd - guide.EdgeStart).Length;
        var textOffset = Math.Max(0.04, dimensionLength * 0.06);
        var labelPosition = new Point3D(
            (lineStart.X + lineEnd.X) / 2.0 + guide.Outward.X * textOffset,
            (lineStart.Y + lineEnd.Y) / 2.0 + guide.Outward.Y * textOffset,
            (lineStart.Z + lineEnd.Z) / 2.0 + guide.Outward.Z * textOffset);

        AddDimensionTextVisual(labelPosition, $"{guide.Axis}: {FormatLengthForDisplay(guide.ValueFeet)}", guide.Axis);
    }

    private Vector3D BuildCameraFacingOutward(Point3D start, Point3D end)
    {
        var axisDirection = end - start;
        if (axisDirection.Length < 1e-6)
            return new Vector3D(0, 1, 0);

        axisDirection.Normalize();
        var midpoint = new Point3D(
            (start.X + end.X) / 2.0,
            (start.Y + end.Y) / 2.0,
            (start.Z + end.Z) / 2.0);

        Vector3D outward;
        if (TryGetViewportCameraPosition(out var cameraPosition))
        {
            outward = cameraPosition - midpoint;
            outward -= axisDirection * Vector3D.DotProduct(outward, axisDirection);
        }
        else
        {
            outward = Vector3D.CrossProduct(axisDirection, new Vector3D(0, 1, 0));
        }

        if (outward.Length < 1e-6)
            outward = Vector3D.CrossProduct(axisDirection, new Vector3D(1, 0, 0));
        if (outward.Length < 1e-6)
            outward = new Vector3D(0, 1, 0);

        outward.Normalize();
        return outward;
    }

    private void AddCustomDimensionAnnotations(IReadOnlyDictionary<string, bool> layerVisibilityById)
    {
        if (_customDimensionAnnotations.Count == 0)
            return;

        foreach (var annotation in _customDimensionAnnotations)
        {
            if (!IsCustomDimensionAnchorVisible(annotation.Start, layerVisibilityById) ||
                !IsCustomDimensionAnchorVisible(annotation.End, layerVisibilityById))
            {
                continue;
            }

            if (!TryResolveCustomDimensionAnchorWorldPoint(annotation.Start, out var startWorld) ||
                !TryResolveCustomDimensionAnchorWorldPoint(annotation.End, out var endWorld))
            {
                continue;
            }

            var delta = endWorld - startWorld;
            if (delta.Length < InViewDimensionMinSpan)
                continue;

            var outward = BuildCameraFacingOutward(startWorld, endWorld);
            var guide = new AxisDimensionGuide(annotation.Axis, delta.Length, startWorld, endWorld, outward);

            ElectricalComponent? offsetComponent = null;
            if (!string.IsNullOrEmpty(annotation.Start.ComponentId))
                offsetComponent = _viewModel.Components.FirstOrDefault(c => string.Equals(c.Id, annotation.Start.ComponentId, StringComparison.Ordinal));
            if (offsetComponent == null && !string.IsNullOrEmpty(annotation.End.ComponentId))
                offsetComponent = _viewModel.Components.FirstOrDefault(c => string.Equals(c.Id, annotation.End.ComponentId, StringComparison.Ordinal));

            var axisOffset = offsetComponent == null ? 0.0 : GetDimensionAxisOffset(offsetComponent, annotation.Axis);
            DrawAxisDimensionGuide(guide, axisOffset);
        }
    }

    private void AddDimensionLineVisual(Point3D start, Point3D end, char axis)
    {
        var line = new LinesVisual3D
        {
            Color = Colors.Black,
            Thickness = 1.8
        };
        line.Points.Add(start);
        line.Points.Add(end);
        Viewport.Children.Add(line);
        _selectionDimensionVisuals.Add(line);
        _dimensionVisualAxisMap[line] = axis;
    }

    private void AddDimensionTextVisual(Point3D position, string text, char axis)
    {
        var label = new BillboardTextVisual3D
        {
            Position = position,
            Text = text,
            Foreground = Brushes.Black,
            Background = Brushes.White,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.8),
            FontSize = 14,
            Padding = new Thickness(4, 2, 4, 2)
        };
        Viewport.Children.Add(label);
        _selectionDimensionVisuals.Add(label);
        _dimensionVisualAxisMap[label] = axis;
    }

    private void DrawXAxisDimensions(Rect3D bounds, IReadOnlyList<AxisDimensionSpan> spans, double axisOffset, DimensionEdgePlacement placement)
    {
        if (spans.Count == 0)
            return;

        var effectiveOffset = Math.Max(0.0, axisOffset);
        var stackOffset = Math.Max(0.08, Math.Max(bounds.SizeX, bounds.SizeZ) * 0.04);
        var textOffset = Math.Max(0.04, stackOffset * 0.5);
        var anchorY = placement.EdgeY;
        var anchorZ = placement.EdgeZ;
        var baseY = anchorY + placement.OutwardY * effectiveOffset;
        var baseZ = anchorZ + placement.OutwardZ * effectiveOffset;

        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            var stack = index * stackOffset;
            var y = baseY + placement.OutwardY * stack;
            var z = baseZ + placement.OutwardZ * stack;
            var x1 = span.MinCoordinate;
            var x2 = span.MaxCoordinate;
            var start = new Point3D(x1, y, z);
            var end = new Point3D(x2, y, z);
            AddDimensionLineVisual(start, end, 'X');
            AddDimensionLineVisual(new Point3D(x1, anchorY, anchorZ), start, 'X');
            AddDimensionLineVisual(new Point3D(x2, anchorY, anchorZ), end, 'X');

            var labelPos = new Point3D((x1 + x2) / 2.0, y + placement.OutwardY * textOffset, z + placement.OutwardZ * textOffset);
            AddDimensionTextVisual(labelPos, $"X: {FormatLengthForDisplay(span.ValueFeet)}", 'X');
        }
    }

    private void DrawYAxisDimensions(Rect3D bounds, IReadOnlyList<AxisDimensionSpan> spans, double axisOffset, DimensionEdgePlacement placement)
    {
        if (spans.Count == 0)
            return;

        var effectiveOffset = Math.Max(0.0, axisOffset);
        var stackOffset = Math.Max(0.08, Math.Max(bounds.SizeX, bounds.SizeZ) * 0.04);
        var textOffset = Math.Max(0.04, stackOffset * 0.5);
        var anchorX = placement.EdgeX;
        var anchorZ = placement.EdgeZ;
        var baseX = anchorX + placement.OutwardX * effectiveOffset;
        var baseZ = anchorZ + placement.OutwardZ * effectiveOffset;

        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            var stack = index * stackOffset;
            var x = baseX + placement.OutwardX * stack;
            var z = baseZ + placement.OutwardZ * stack;
            var y1 = span.MinCoordinate;
            var y2 = span.MaxCoordinate;
            var start = new Point3D(x, y1, z);
            var end = new Point3D(x, y2, z);
            AddDimensionLineVisual(start, end, 'Y');
            AddDimensionLineVisual(new Point3D(anchorX, y1, anchorZ), start, 'Y');
            AddDimensionLineVisual(new Point3D(anchorX, y2, anchorZ), end, 'Y');

            var labelPos = new Point3D(x + placement.OutwardX * textOffset, (y1 + y2) / 2.0, z + placement.OutwardZ * textOffset);
            AddDimensionTextVisual(labelPos, $"Y: {FormatLengthForDisplay(span.ValueFeet)}", 'Y');
        }
    }

    private void DrawZAxisDimensions(Rect3D bounds, IReadOnlyList<AxisDimensionSpan> spans, double axisOffset, DimensionEdgePlacement placement)
    {
        if (spans.Count == 0)
            return;

        var effectiveOffset = Math.Max(0.0, axisOffset);
        var stackOffset = Math.Max(0.08, Math.Max(bounds.SizeX, bounds.SizeZ) * 0.04);
        var textOffset = Math.Max(0.04, stackOffset * 0.5);
        var anchorX = placement.EdgeX;
        var anchorY = placement.EdgeY;
        var baseX = anchorX + placement.OutwardX * effectiveOffset;
        var baseY = anchorY + placement.OutwardY * effectiveOffset;

        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            var stack = index * stackOffset;
            var x = baseX + placement.OutwardX * stack;
            var y = baseY + placement.OutwardY * stack;
            var z1 = span.MinCoordinate;
            var z2 = span.MaxCoordinate;
            var start = new Point3D(x, y, z1);
            var end = new Point3D(x, y, z2);
            AddDimensionLineVisual(start, end, 'Z');
            AddDimensionLineVisual(new Point3D(anchorX, anchorY, z1), start, 'Z');
            AddDimensionLineVisual(new Point3D(anchorX, anchorY, z2), end, 'Z');

            var labelPos = new Point3D(x + placement.OutwardX * textOffset, y + placement.OutwardY * textOffset, (z1 + z2) / 2.0);
            AddDimensionTextVisual(labelPos, $"Z: {FormatLengthForDisplay(span.ValueFeet)}", 'Z');
        }
    }

    private void ClearSelectionDimensionVisuals()
    {
        foreach (var visual in _selectionDimensionVisuals)
            Viewport.Children.Remove(visual);
        _selectionDimensionVisuals.Clear();
        _dimensionVisualAxisMap.Clear();
    }

    private void AddSelectionDimensionAnnotations(IReadOnlyDictionary<string, bool> layerVisibilityById)
    {
        ClearSelectionDimensionVisuals();

        var selected = _viewModel.SelectedComponent;
        if (selected == null)
            return;
        if (!IsLayerVisible(layerVisibilityById, selected.LayerId))
            return;
        if (!TryGetComponentWorldBounds(selected, out _))
            return;

        var transform = CreateComponentTransform(selected);
        if (!TryGetNominalLocalDimensionBounds(selected, out var localBounds))
            return;

        var guides = BuildAxisDimensionGuides(localBounds, transform);
        if (guides.Count == 0)
            return;

        var guideOffsets = GetDimensionAxisOffsets(selected);
        foreach (var guide in guides)
        {
            var axisOffset = guide.Axis switch
            {
                'X' => guideOffsets.X,
                'Y' => guideOffsets.Y,
                'Z' => guideOffsets.Z,
                _ => 0.0
            };

            DrawAxisDimensionGuide(guide, axisOffset);
        }
    }

    private void UpdatePropertiesPanel()
    {
        var selectedComponents = GetSelectedComponents();
        if (selectedComponents.Count == 0)
        {
            ClearPropertiesPanel();
            return;
        }

        if (selectedComponents.Count == 1)
        {
            UpdateSingleComponentPropertiesPanel(selectedComponents[0]);
            return;
        }

        UpdateMultiSelectionPropertiesPanel(selectedComponents);
    }

    private void UpdateSingleComponentPropertiesPanel(ElectricalComponent component)
    {
        SetPropertiesPanelMultiSelectMode(isMultiSelection: false);

        NameTextBox.Text = component.Name;
        TypeTextBox.Text = component.Type.ToString();
        PositionXTextBox.Text = FormatLengthForInput(component.Position.X);
        PositionYTextBox.Text = FormatLengthForInput(component.Position.Y);
        PositionZTextBox.Text = FormatLengthForInput(component.Position.Z);
        RotationXTextBox.Text = component.Rotation.X.ToString("F2");
        RotationYTextBox.Text = component.Rotation.Y.ToString("F2");
        RotationZTextBox.Text = component.Rotation.Z.ToString("F2");
        WidthTextBox.Text = FormatLengthForInput(component.Parameters.Width);
        HeightTextBox.Text = FormatLengthForInput(component.Parameters.Height);
        DepthTextBox.Text = FormatLengthForInput(component.Parameters.Depth);
        MaterialTextBox.Text = component.Parameters.Material;
        ElevationTextBox.Text = FormatLengthForInput(component.Parameters.Elevation);
        ColorTextBox.Text = component.Parameters.Color;
        ManufacturerTextBox.Text = component.Parameters.Manufacturer;
        PartNumberTextBox.Text = component.Parameters.PartNumber;
        ReferenceUrlTextBox.Text = component.Parameters.ReferenceUrl;
        UpdateReferenceAssignmentUi(new[] { component });
        UpdateSelectedDimensionsDisplay(component);

        var layer = _viewModel.Layers.FirstOrDefault(current => current.Id == component.LayerId);
        if (layer != null)
            LayerComboBox.SelectedItem = layer;

        if (component is ConduitComponent conduit)
        {
            ConduitProperties.Visibility = Visibility.Visible;
            BendPointsTextBlock.Text = conduit.BendPoints.Count.ToString();
            ClearBendPointsButton.IsEnabled = true;
        }
        else
        {
            ConduitProperties.Visibility = Visibility.Collapsed;
        }

        UpdateCustomDimensionUiState();
    }

    private void UpdateMultiSelectionPropertiesPanel(IReadOnlyList<ElectricalComponent> components)
    {
        SetPropertiesPanelMultiSelectMode(isMultiSelection: true);

        NameTextBox.Text = string.Empty;
        TypeTextBox.Text = TryGetSharedValue(components, component => component.Type, type => type.ToString())
            ?? $"Mixed ({components.Count} selected)";
        PositionXTextBox.Text = string.Empty;
        PositionYTextBox.Text = string.Empty;
        PositionZTextBox.Text = string.Empty;
        RotationXTextBox.Text = string.Empty;
        RotationYTextBox.Text = string.Empty;
        RotationZTextBox.Text = string.Empty;
        WidthTextBox.Text = TryGetSharedValue(components, component => component.Parameters.Width, FormatLengthForInput) ?? string.Empty;
        HeightTextBox.Text = TryGetSharedValue(components, component => component.Parameters.Height, FormatLengthForInput) ?? string.Empty;
        DepthTextBox.Text = TryGetSharedValue(components, component => component.Parameters.Depth, FormatLengthForInput) ?? string.Empty;
        MaterialTextBox.Text = TryGetSharedValue(components, component => component.Parameters.Material, value => value) ?? string.Empty;
        ElevationTextBox.Text = TryGetSharedValue(components, component => component.Parameters.Elevation, FormatLengthForInput) ?? string.Empty;
        ColorTextBox.Text = TryGetSharedValue(components, component => component.Parameters.Color, value => value) ?? string.Empty;
        ManufacturerTextBox.Text = TryGetSharedValue(components, component => component.Parameters.Manufacturer, value => value) ?? string.Empty;
        PartNumberTextBox.Text = TryGetSharedValue(components, component => component.Parameters.PartNumber, value => value) ?? string.Empty;
        ReferenceUrlTextBox.Text = TryGetSharedValue(components, component => component.Parameters.ReferenceUrl, value => value) ?? string.Empty;
        UpdateReferenceAssignmentUi(components);
        UpdateSelectedDimensionsDisplay(components);

        var sharedLayerId = TryGetSharedValue(components, component => component.LayerId, value => value);
        LayerComboBox.SelectedItem = sharedLayerId == null
            ? null
            : _viewModel.Layers.FirstOrDefault(current => current.Id == sharedLayerId);

        ConduitProperties.Visibility = Visibility.Collapsed;
        ClearBendPointsButton.IsEnabled = false;

        var distinctTypes = components.Select(component => component.Type).Distinct().ToList();
        var typeSummary = distinctTypes.Count == 1
            ? distinctTypes[0].ToString()
            : "mixed component types";
        MultiSelectionSummaryTextBlock.Text =
            $"Editing {components.Count} selected components ({typeSummary}). Shared values are shown; blank fields indicate mixed values. Apply Changes only updates fields you fill in for the whole selection.";

        UpdateCustomDimensionUiState();
    }

    private void SetPropertiesPanelMultiSelectMode(bool isMultiSelection)
    {
        MultiSelectionSummaryTextBlock.Visibility = isMultiSelection ? Visibility.Visible : Visibility.Collapsed;
        ApplyPropertiesButton.Content = isMultiSelection ? "Apply Shared Changes" : "Apply Changes";

        NameTextBox.IsEnabled = !isMultiSelection;
        PositionXTextBox.IsEnabled = !isMultiSelection;
        PositionYTextBox.IsEnabled = !isMultiSelection;
        PositionZTextBox.IsEnabled = !isMultiSelection;
        RotationXTextBox.IsEnabled = !isMultiSelection;
        RotationYTextBox.IsEnabled = !isMultiSelection;
        RotationZTextBox.IsEnabled = !isMultiSelection;
        OpenReferenceButton.IsEnabled = !isMultiSelection;
    }

    private static string? TryGetSharedValue<T>(IReadOnlyList<ElectricalComponent> components, Func<ElectricalComponent, T> selector, Func<T, string> formatter)
    {
        if (components.Count == 0)
            return null;

        var first = selector(components[0]);
        for (var index = 1; index < components.Count; index++)
        {
            if (!EqualityComparer<T>.Default.Equals(first, selector(components[index])))
                return null;
        }

        return formatter(first);
    }

    private void ClearPropertiesPanel()
    {
        SetPropertiesPanelMultiSelectMode(isMultiSelection: false);
        MultiSelectionSummaryTextBlock.Text = string.Empty;
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
        ManufacturerTextBox.Text = string.Empty;
        PartNumberTextBox.Text = string.Empty;
        ReferenceUrlTextBox.Text = string.Empty;
        UpdateReferenceAssignmentUi(Array.Empty<ElectricalComponent>());
        LayerComboBox.SelectedItem = null;
        ConduitProperties.Visibility = Visibility.Collapsed;
        ClearBendPointsButton.IsEnabled = false;
        UpdateSelectedDimensionsDisplay(Array.Empty<ElectricalComponent>());
        UpdateCustomDimensionUiState();
    }
}