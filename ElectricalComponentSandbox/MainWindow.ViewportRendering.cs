using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;
using HelixToolkit.Wpf;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private readonly List<Visual3D> _billboardLabelVisuals = new();

    private void SetVisualStyle3D(VisualStyle3D style)
    {
        _activeVisualStyle3D = style;
        UpdateViewport();
    }

    private Material Build3DMaterial(Color baseColor, bool isSelected)
        => MaterialFactory.Build(_activeVisualStyle3D, baseColor, isSelected);

    private void AddComponentBillboardLabels(IReadOnlyDictionary<string, bool> layerVisibility)
    {
        foreach (var visual in _billboardLabelVisuals)
            Viewport.Children.Remove(visual);
        _billboardLabelVisuals.Clear();

        foreach (var component in _viewModel.Components)
        {
            if (!IsLayerVisible(layerVisibility, component.LayerId))
                continue;

            var label = new BillboardTextVisual3D
            {
                Position = new Point3D(component.Position.X, component.Position.Y + 0.5, component.Position.Z),
                Text = component.Name,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                FontSize = 13,
                Padding = new Thickness(3, 1, 3, 1)
            };

            Viewport.Children.Add(label);
            _billboardLabelVisuals.Add(label);
        }
    }

    private void UpdateViewport()
    {
        PruneCustomDimensions();
        ClearCustomDimensionPreview();
        ClearSelectionDimensionVisuals();

        for (int index = Viewport.Children.Count - 1; index >= 0; index--)
        {
            if (Viewport.Children[index] is ModelVisual3D visual && visual.Content is GeometryModel3D)
                Viewport.Children.RemoveAt(index);
        }

        _pdfUnderlayVisual3D = null;
        _visualToComponentMap.Clear();
        var layerVisibilityById = BuildLayerVisibilityLookup();
        AddPdfUnderlayToViewport();

        foreach (var component in _viewModel.Components)
        {
            if (!IsLayerVisible(layerVisibilityById, component.LayerId))
                continue;
            if (_sectionCutActive && component.Position.Y > _sectionCutY)
                continue;

            AddComponentToViewport(component);
        }

        UpdateConduitRuns3D();
        AddSelectionDimensionAnnotations(layerVisibilityById);
        AddCustomDimensionAnnotations(layerVisibilityById);
        RefreshConduitActionUiState();
        UpdateCustomDimensionUiState();

        if (_showComponentLabels)
            AddComponentBillboardLabels(layerVisibilityById);
    }

    private static Transform3DGroup CreateComponentTransform(ElectricalComponent component)
    {
        var transformGroup = new Transform3DGroup();
        transformGroup.Children.Add(new TranslateTransform3D(component.Position.X, component.Position.Y, component.Position.Z));
        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), component.Rotation.X)));
        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), component.Rotation.Y)));
        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), component.Rotation.Z)));
        transformGroup.Children.Add(new ScaleTransform3D(component.Scale.X, component.Scale.Y, component.Scale.Z));
        return transformGroup;
    }

    private void AddComponentToViewport(ElectricalComponent component)
    {
        var visual = new ModelVisual3D();
        var geometry = CreateComponentGeometry(component);
        var color = ResolveComponentColor(component, Colors.SlateGray);
        var appliedMaterial = Build3DMaterial(color, _viewModel.IsComponentSelected(component));
        var model = new GeometryModel3D(geometry, appliedMaterial)
        {
            Transform = CreateComponentTransform(component)
        };

        visual.Content = model;
        Viewport.Children.Add(visual);
        _visualToComponentMap[visual] = component;
    }

    private MeshGeometry3D CreateComponentGeometry(ElectricalComponent component)
    {
        var builder = new MeshBuilder();
        var profile = ElectricalComponentCatalog.GetProfile(component);

        switch (component.Type)
        {
            case ComponentType.Conduit when component is ConduitComponent conduit:
                CreateConduitGeometry(builder, conduit, profile);
                break;
            case ComponentType.Hanger when component is HangerComponent hanger:
                CreateHangerGeometry(builder, hanger, profile);
                break;
            case ComponentType.CableTray when component is CableTrayComponent tray:
                CreateCableTrayGeometry(builder, tray, profile);
                break;
            case ComponentType.Box:
                if (component is BoxComponent box)
                    CreateBoxGeometry(builder, box, profile);
                else
                    builder.AddBox(new Point3D(0, 0, 0), component.Parameters.Width, component.Parameters.Height, component.Parameters.Depth);
                break;
            case ComponentType.Panel:
                if (component is PanelComponent panel)
                    CreatePanelGeometry(builder, panel, profile);
                else
                    builder.AddBox(new Point3D(0, 0, 0), component.Parameters.Width, component.Parameters.Height, component.Parameters.Depth);
                break;
            case ComponentType.Support:
                if (component is SupportComponent support)
                    CreateSupportGeometry(builder, support, profile);
                else
                    builder.AddBox(new Point3D(0, 0, 0), component.Parameters.Width, component.Parameters.Height, component.Parameters.Depth);
                break;
        }

        return builder.ToMesh();
    }

    private void CreateConduitGeometry(MeshBuilder builder, ConduitComponent conduit, string profile)
    {
        var pathPoints = conduit.GetPathPoints();
        if (pathPoints.Count < 2)
            return;

        var renderPath = pathPoints.Count == 2 ? pathPoints : GenerateSmoothPath(pathPoints, conduit.BendRadius);
        if (renderPath.Count < 2)
            return;

        var radius = Math.Max(0.03, conduit.Diameter * 0.5);
        var thetaDiv = 20;
        switch (profile)
        {
            case ElectricalComponentCatalog.Profiles.ConduitPvc:
                radius *= 1.08;
                thetaDiv = 16;
                break;
            case ElectricalComponentCatalog.Profiles.ConduitRigidMetal:
                radius *= 1.18;
                thetaDiv = 24;
                break;
            case ElectricalComponentCatalog.Profiles.ConduitFlexibleMetal:
                radius *= 0.95;
                thetaDiv = 14;
                break;
        }

        for (int index = 0; index < renderPath.Count - 1; index++)
            builder.AddCylinder(renderPath[index], renderPath[index + 1], radius, thetaDiv);

        for (int index = 1; index < renderPath.Count - 1; index++)
        {
            var couplingRadius = profile == ElectricalComponentCatalog.Profiles.ConduitRigidMetal ? radius * 1.24 : radius * 1.14;
            builder.AddSphere(renderPath[index], couplingRadius, 10, 8);
        }

        if (profile == ElectricalComponentCatalog.Profiles.ConduitFlexibleMetal)
            AddFlexibleConduitRibbing(builder, renderPath, radius);
        else if (profile == ElectricalComponentCatalog.Profiles.ConduitRigidMetal)
            AddRigidConduitEndCollars(builder, renderPath, radius);
    }

    private static void AddFlexibleConduitRibbing(MeshBuilder builder, IReadOnlyList<Point3D> points, double baseRadius)
    {
        const double spacing = 0.45;
        var ribRadius = baseRadius * 1.06;

        for (int index = 0; index < points.Count - 1; index++)
        {
            var segment = points[index + 1] - points[index];
            var length = segment.Length;
            if (length < 1e-4)
                continue;

            var dir = segment;
            dir.Normalize();
            var ribCount = (int)(length / spacing);
            for (int rib = 1; rib < ribCount; rib++)
            {
                var center = points[index] + dir * (rib * spacing);
                var half = dir * 0.04;
                builder.AddCylinder(center - half, center + half, ribRadius, 8);
            }
        }
    }

    private static void AddRigidConduitEndCollars(MeshBuilder builder, IReadOnlyList<Point3D> points, double radius)
    {
        if (points.Count < 2)
            return;

        var collarLength = Math.Max(0.08, radius * 0.9);
        AddEndCollar(builder, points[0], points[1], collarLength, radius * 1.26);
        AddEndCollar(builder, points[^1], points[^2], collarLength, radius * 1.26);
    }

    private static void AddEndCollar(MeshBuilder builder, Point3D endPoint, Point3D adjacentPoint, double length, double radius)
    {
        var dir = endPoint - adjacentPoint;
        if (dir.Length < 1e-5)
            return;

        dir.Normalize();
        builder.AddCylinder(endPoint - dir * length, endPoint, radius, 14);
    }

    private static void CreateBoxGeometry(MeshBuilder builder, BoxComponent box, string profile)
    {
        var width = Math.Max(0.1, box.Parameters.Width);
        var height = Math.Max(0.1, box.Parameters.Height);
        var depth = Math.Max(0.1, box.Parameters.Depth);
        builder.AddBox(new Point3D(0, height / 2, 0), width, height, depth);

        switch (profile)
        {
            case ElectricalComponentCatalog.Profiles.BoxPull:
                AddBoxKnockouts(builder, width, height, depth, 3, 0.9);
                builder.AddBox(new Point3D(0, height + 0.08, 0), width * 0.92, 0.16, depth * 0.92);
                break;
            case ElectricalComponentCatalog.Profiles.BoxFloor:
                builder.AddCylinder(new Point3D(0, height + 0.02, 0), new Point3D(0, height + 0.16, 0), Math.Min(width, depth) * 0.3, 20);
                builder.AddCylinder(new Point3D(0, height + 0.16, 0), new Point3D(0, height + 0.3, 0), Math.Min(width, depth) * 0.08, 14);
                break;
            case ElectricalComponentCatalog.Profiles.BoxDisconnectSwitch:
                builder.AddBox(new Point3D(0, height * 0.65, depth * 0.52), width * 0.42, height * 0.38, 0.22);
                builder.AddCylinder(new Point3D(width * 0.2, height * 0.65, depth * 0.62), new Point3D(width * 0.42, height * 0.65, depth * 0.62), Math.Min(width, height) * 0.06, 14);
                break;
            default:
                AddBoxKnockouts(builder, width, height, depth, 2, 1.0);
                break;
        }
    }

    private static void AddBoxKnockouts(MeshBuilder builder, double width, double height, double depth, int countPerSide, double scale)
    {
        var radius = Math.Max(0.06, Math.Min(width, depth) * 0.08 * scale);
        var y = height * 0.55;
        var zStep = depth / (countPerSide + 1);
        var xStep = width / (countPerSide + 1);

        for (int index = 1; index <= countPerSide; index++)
        {
            var z = -depth / 2 + zStep * index;
            builder.AddCylinder(new Point3D(-width / 2 - 0.08, y, z), new Point3D(-width / 2 + 0.08, y, z), radius, 10);
            builder.AddCylinder(new Point3D(width / 2 - 0.08, y, z), new Point3D(width / 2 + 0.08, y, z), radius, 10);
        }

        for (int index = 1; index <= countPerSide; index++)
        {
            var x = -width / 2 + xStep * index;
            builder.AddCylinder(new Point3D(x, y, -depth / 2 - 0.08), new Point3D(x, y, -depth / 2 + 0.08), radius, 10);
            builder.AddCylinder(new Point3D(x, y, depth / 2 - 0.08), new Point3D(x, y, depth / 2 + 0.08), radius, 10);
        }
    }

    private static void CreatePanelGeometry(MeshBuilder builder, PanelComponent panel, string profile)
    {
        var width = Math.Max(1, panel.Parameters.Width);
        var height = Math.Max(1, panel.Parameters.Height);
        var depth = Math.Max(0.6, panel.Parameters.Depth);

        switch (profile)
        {
            case ElectricalComponentCatalog.Profiles.PanelSwitchboard:
                CreateSegmentedPanel(builder, width, height, depth, 4, true);
                break;
            case ElectricalComponentCatalog.Profiles.PanelMcc:
                CreateSegmentedPanel(builder, width, height, depth, 3, false);
                AddHorizontalCompartmentSeams(builder, width, height, depth, 5);
                break;
            case ElectricalComponentCatalog.Profiles.PanelLighting:
                builder.AddBox(new Point3D(0, height / 2, 0), width, height, depth);
                AddVerticalPanelSeams(builder, width, height, depth, 2);
                AddPanelHandle(builder, width, height, depth, -width * 0.18);
                break;
            default:
                builder.AddBox(new Point3D(0, height / 2, 0), width, height, depth);
                AddVerticalPanelSeams(builder, width, height, depth, 3);
                AddPanelHandle(builder, width, height, depth, width * 0.2);
                break;
        }
    }

    private static void CreateSegmentedPanel(MeshBuilder builder, double width, double height, double depth, int sections, bool addCenterBus)
    {
        var sectionWidth = width / sections;
        for (int index = 0; index < sections; index++)
        {
            var centerX = -width / 2 + sectionWidth * index + sectionWidth / 2;
            builder.AddBox(new Point3D(centerX, height / 2, 0), sectionWidth * 0.96, height, depth);
            AddPanelHandle(builder, width, height, depth, centerX + sectionWidth * 0.25);
        }

        if (addCenterBus)
            builder.AddBox(new Point3D(0, height * 0.5, depth * 0.46), width * 0.04, height * 0.88, 0.2);
    }

    private static void AddVerticalPanelSeams(MeshBuilder builder, double width, double height, double depth, int seamCount)
    {
        for (int index = 1; index <= seamCount; index++)
        {
            var x = -width / 2 + width * index / (seamCount + 1);
            builder.AddBox(new Point3D(x, height / 2, depth * 0.52), Math.Max(0.04, width * 0.01), height * 0.92, 0.08);
        }
    }

    private static void AddHorizontalCompartmentSeams(MeshBuilder builder, double width, double height, double depth, int seamCount)
    {
        for (int index = 1; index <= seamCount; index++)
        {
            var y = height * index / (seamCount + 1);
            builder.AddBox(new Point3D(0, y, depth * 0.52), width * 0.92, Math.Max(0.04, height * 0.008), 0.08);
        }
    }

    private static void AddPanelHandle(MeshBuilder builder, double width, double height, double depth, double xOffset)
    {
        var clampX = Math.Max(-width * 0.46, Math.Min(width * 0.46, xOffset));
        var y = height * 0.55;
        var z = depth * 0.54;
        builder.AddCylinder(new Point3D(clampX, y - height * 0.08, z), new Point3D(clampX, y + height * 0.08, z), Math.Max(0.04, width * 0.012), 12);
    }

    private static void CreateSupportGeometry(MeshBuilder builder, SupportComponent support, string profile)
    {
        var width = Math.Max(0.08, support.Parameters.Width);
        var height = Math.Max(0.08, support.Parameters.Height);
        var length = Math.Max(0.2, support.Parameters.Depth);

        switch (profile)
        {
            case ElectricalComponentCatalog.Profiles.SupportTrapeze:
                var rodOffset = Math.Max(0.2, length * 0.35);
                builder.AddBox(new Point3D(0, 0, 0), length, Math.Max(0.08, width), Math.Max(0.08, width));
                builder.AddCylinder(new Point3D(-rodOffset, 0, 0), new Point3D(-rodOffset, Math.Max(1.2, height), 0), Math.Max(0.04, width * 0.35), 12);
                builder.AddCylinder(new Point3D(rodOffset, 0, 0), new Point3D(rodOffset, Math.Max(1.2, height), 0), Math.Max(0.04, width * 0.35), 12);
                break;
            case ElectricalComponentCatalog.Profiles.SupportWallBracket:
                var armLength = Math.Max(0.5, length);
                var armThickness = Math.Max(0.08, width * 0.5);
                builder.AddBox(new Point3D(0, height * 0.5, 0), armThickness, height, armLength);
                builder.AddBox(new Point3D(armLength * 0.5, armThickness * 0.5, 0), armLength, armThickness, armThickness);
                builder.AddCylinder(new Point3D(-armThickness * 0.45, height * 0.4, -armLength * 0.2), new Point3D(armThickness * 0.45, height * 0.4, -armLength * 0.2), armThickness * 0.28, 10);
                break;
            default:
                CreateUnistrutGeometry(builder, width, height, length);
                break;
        }
    }

    private static void CreateUnistrutGeometry(MeshBuilder builder, double width, double height, double length)
    {
        var channelWidth = Math.Max(0.12, width);
        var channelHeight = Math.Max(0.12, height);
        var wall = Math.Max(0.015, Math.Min(channelWidth, channelHeight) * 0.18);
        var halfWidth = channelWidth / 2;
        var halfHeight = channelHeight / 2;

        builder.AddBox(new Point3D(-halfWidth + wall / 2, 0, 0), wall, channelHeight, length);
        builder.AddBox(new Point3D(0, halfHeight - wall / 2, 0), channelWidth, wall, length);
        builder.AddBox(new Point3D(0, -halfHeight + wall / 2, 0), channelWidth, wall, length);

        var slotSpacing = Math.Max(0.5, length / 6);
        var slotRadius = Math.Max(0.03, wall * 0.8);
        for (double z = -length / 2 + slotSpacing; z < length / 2 - slotSpacing / 2; z += slotSpacing)
            builder.AddCylinder(new Point3D(-halfWidth - 0.01, 0, z), new Point3D(-halfWidth + wall + 0.01, 0, z), slotRadius, 10);
    }

    private static void CreateCableTrayGeometry(MeshBuilder builder, CableTrayComponent tray, string profile)
    {
        var points = tray.GetPathPoints();
        if (points.Count < 2)
        {
            points =
            [
                new Point3D(0, 0, 0),
                new Point3D(Math.Max(1.0, tray.Length), 0, 0)
            ];
        }

        var trayWidth = Math.Max(1.0, tray.TrayWidth);
        var trayDepth = Math.Max(0.5, tray.TrayDepth);
        var railRadius = Math.Max(0.08, trayDepth * 0.12);

        for (int index = 0; index < points.Count - 1; index++)
        {
            var start = points[index];
            var end = points[index + 1];
            var dir = end - start;
            var length = dir.Length;
            if (length < 1e-4)
                continue;

            dir.Normalize();
            var side = new Vector3D(-dir.Z, 0, dir.X);
            if (side.Length < 1e-5)
                side = new Vector3D(1, 0, 0);
            else
                side.Normalize();

            var up = new Vector3D(0, 1, 0);
            var halfW = trayWidth / 2;
            var railYOffset = trayDepth * 0.45;
            var leftStart = start + side * halfW + up * railYOffset;
            var leftEnd = end + side * halfW + up * railYOffset;
            var rightStart = start - side * halfW + up * railYOffset;
            var rightEnd = end - side * halfW + up * railYOffset;

            builder.AddCylinder(leftStart, leftEnd, railRadius, 12);
            builder.AddCylinder(rightStart, rightEnd, railRadius, 12);

            switch (profile)
            {
                case ElectricalComponentCatalog.Profiles.TrayWireMesh:
                    AddTrayRungs(builder, start, dir, side, up, length, halfW, railYOffset, Math.Max(0.8, trayWidth * 0.3), railRadius * 0.65);
                    AddTrayLongitudinalWires(builder, start, end, side, up, railYOffset * 0.5, halfW, railRadius * 0.5);
                    break;
                case ElectricalComponentCatalog.Profiles.TraySolidBottom:
                    AddTrayRungs(builder, start, dir, side, up, length, halfW, 0.1, Math.Max(0.7, trayDepth * 0.35), railRadius * 0.75);
                    builder.AddCylinder(start + up * 0.05, end + up * 0.05, railRadius * 0.85, 10);
                    break;
                default:
                    AddTrayRungs(builder, start, dir, side, up, length, halfW, railYOffset, Math.Max(1.4, trayWidth * 0.5), railRadius * 0.75);
                    break;
            }
        }
    }

    private static void AddTrayRungs(MeshBuilder builder, Point3D segmentStart, Vector3D direction, Vector3D side, Vector3D up,
        double segmentLength, double halfWidth, double yOffset, double spacing, double radius)
    {
        for (double dist = spacing; dist < segmentLength; dist += spacing)
        {
            var center = segmentStart + direction * dist + up * yOffset;
            builder.AddCylinder(center - side * halfWidth, center + side * halfWidth, Math.Max(0.04, radius), 10);
        }
    }

    private static void AddTrayLongitudinalWires(MeshBuilder builder, Point3D start, Point3D end, Vector3D side, Vector3D up,
        double yOffset, double halfWidth, double radius)
    {
        var offsetA = side * (halfWidth * 0.35) + up * yOffset;
        var offsetB = side * (-halfWidth * 0.35) + up * yOffset;
        builder.AddCylinder(start + offsetA, end + offsetA, Math.Max(0.03, radius), 10);
        builder.AddCylinder(start + offsetB, end + offsetB, Math.Max(0.03, radius), 10);
    }

    private static void CreateHangerGeometry(MeshBuilder builder, HangerComponent hanger, string profile)
    {
        var rodDiameter = Math.Max(0.08, hanger.RodDiameter);
        var rodLength = Math.Max(0.5, hanger.RodLength);
        var start = new Point3D(0, 0, 0);
        var end = new Point3D(0, rodLength, 0);

        if (profile == ElectricalComponentCatalog.Profiles.HangerSeismicBrace)
        {
            var braceEnd = new Point3D(rodLength * 0.65, rodLength * 0.65, 0);
            builder.AddCylinder(start, braceEnd, rodDiameter * 0.9, 12);
            builder.AddBox(new Point3D(0, 0, 0), rodDiameter * 2.4, rodDiameter * 0.8, rodDiameter * 2.4);
            builder.AddBox(new Point3D(braceEnd.X, braceEnd.Y, braceEnd.Z), rodDiameter * 2.0, rodDiameter * 0.8, rodDiameter * 2.0);
        }
        else
        {
            builder.AddCylinder(start, end, rodDiameter, 12);
            var nutHeight = Math.Max(0.06, rodDiameter * 0.5);
            builder.AddBox(new Point3D(0, rodLength * 0.82, 0), rodDiameter * 1.8, nutHeight, rodDiameter * 1.8);
            builder.AddBox(new Point3D(0, rodLength * 0.18, 0), rodDiameter * 1.6, nutHeight, rodDiameter * 1.6);
        }
    }
}
