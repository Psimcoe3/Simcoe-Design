using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.Dimensioning;
using HelixToolkit.Wpf;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private static DimensionSnapKind ToDimensionSnapKind(CustomDimensionSnapMode mode)
    {
        return mode switch
        {
            CustomDimensionSnapMode.Point => DimensionSnapKind.Point,
            CustomDimensionSnapMode.Edge => DimensionSnapKind.Edge,
            CustomDimensionSnapMode.Face => DimensionSnapKind.Face,
            CustomDimensionSnapMode.Center => DimensionSnapKind.Center,
            CustomDimensionSnapMode.Intersection => DimensionSnapKind.Intersection,
            _ => DimensionSnapKind.None
        };
    }

    private static CustomDimensionSnapMode ToCustomDimensionSnapMode(DimensionSnapKind kind)
    {
        return kind switch
        {
            DimensionSnapKind.Point => CustomDimensionSnapMode.Point,
            DimensionSnapKind.Edge => CustomDimensionSnapMode.Edge,
            DimensionSnapKind.Face => CustomDimensionSnapMode.Face,
            DimensionSnapKind.Center => CustomDimensionSnapMode.Center,
            DimensionSnapKind.Intersection => CustomDimensionSnapMode.Intersection,
            _ => CustomDimensionSnapMode.Auto
        };
    }

    private double GetDimensionSnapTolerancePixels(IReadOnlyList<DimensionSnapCandidateInfo> candidates)
    {
        var referencePoint = candidates.Count > 0
            ? candidates[0].WorldPoint
            : _viewModel.SelectedComponent?.Position ?? new Point3D(0, 0, 0);

        var worldUnitsPerPixel = EstimateWorldUnitsPerPixel(referencePoint);
        var scale = Math.Clamp(worldUnitsPerPixel / BaselineWorldUnitsPerPixel, 0.75, 2.5);
        return BaseDimensionSnapTolerancePx * scale;
    }

    private static bool AreEquivalentAnchors(CustomDimensionAnchor a, CustomDimensionAnchor b)
    {
        if (!string.Equals(a.ComponentId, b.ComponentId, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrWhiteSpace(a.ComponentId))
            return (a.LocalPoint - b.LocalPoint).Length <= 1e-4;

        return (a.WorldPoint - b.WorldPoint).Length <= 1e-4;
    }

    private static bool CanDimensionPair(CustomDimensionAnchor first, CustomDimensionAnchor second)
    {
        return !AreEquivalentAnchors(first, second);
    }

    private bool TryGetComponentFromHit(Viewport3DHelper.HitResult hit, out ElectricalComponent component, out Transform3D componentTransform)
    {
        component = null!;
        componentTransform = Transform3D.Identity;
        if (hit.Visual is not ModelVisual3D visual ||
            !_visualToComponentMap.TryGetValue(visual, out var resolvedComponent) ||
            resolvedComponent == null)
        {
            return false;
        }

        component = resolvedComponent;
        componentTransform = CreateComponentTransform(component);
        return true;
    }

    private bool TryGetSnappedCustomDimensionAnchor(Point screenPoint, IReadOnlyList<Viewport3DHelper.HitResult> hits, out CustomDimensionAnchor anchor, out CustomDimensionSnapMode modeUsed)
    {
        anchor = null!;
        modeUsed = _customDimensionSnapMode;
        if (hits.Count == 0)
            return false;

        var candidates = new List<DimensionSnapCandidateInfo>();
        var componentHits = new List<(Viewport3DHelper.HitResult Hit, ElectricalComponent Component, Transform3D Transform, Point3D LocalHit, ComponentSemanticReferences References)>();
        var semanticByComponentId = new Dictionary<string, ComponentSemanticReferences>(StringComparer.Ordinal);

        foreach (var hit in hits)
        {
            if (!TryGetComponentFromHit(hit, out var component, out var transform))
                continue;

            if (transform.Inverse == null)
                continue;

            var localHit = transform.Inverse.Transform(hit.Position);
            if (!semanticByComponentId.TryGetValue(component.Id, out var semanticReferences))
            {
                if (!TryBuildComponentSemanticReferences(component, out semanticReferences))
                    continue;
                semanticByComponentId[component.Id] = semanticReferences;
            }

            componentHits.Add((hit, component, transform, localHit, semanticReferences));

            void AddCandidate(CustomDimensionSnapMode mode, Point3D localPoint)
            {
                var worldPoint = transform.Transform(localPoint);
                var screenDistance = ComputeScreenSnapDistance(screenPoint, worldPoint);
                if (screenDistance == double.MaxValue)
                    return;

                candidates.Add(new DimensionSnapCandidateInfo
                {
                    Kind = ToDimensionSnapKind(mode),
                    ElementId = component.Id,
                    WorldPoint = worldPoint,
                    LocalPoint = localPoint,
                    ScreenDistancePx = screenDistance,
                    IsVisibleInView = true,
                    IsValidForDimension = true,
                    HasStableReference = true
                });
            }

            Point3D? bestFaceLocal = null;
            var bestFaceDistance = double.MaxValue;
            foreach (var face in semanticReferences.FaceLocals)
            {
                var projected = ProjectPointOntoFace(face, localHit);
                var distance = (projected - localHit).LengthSquared;
                if (distance < bestFaceDistance)
                {
                    bestFaceDistance = distance;
                    bestFaceLocal = projected;
                }
            }

            if (bestFaceLocal.HasValue)
                AddCandidate(CustomDimensionSnapMode.Face, bestFaceLocal.Value);
            else
                AddCandidate(CustomDimensionSnapMode.Face, localHit);

            AddCandidate(CustomDimensionSnapMode.Center, semanticReferences.CenterLocal);

            if (semanticReferences.PointLocals.Count > 0)
            {
                var nearestPoint = semanticReferences.PointLocals[0];
                var nearestPointDistance = (nearestPoint - localHit).LengthSquared;
                for (var i = 1; i < semanticReferences.PointLocals.Count; i++)
                {
                    var point = semanticReferences.PointLocals[i];
                    var distance = (point - localHit).LengthSquared;
                    if (distance < nearestPointDistance)
                    {
                        nearestPointDistance = distance;
                        nearestPoint = point;
                    }
                }

                AddCandidate(CustomDimensionSnapMode.Point, nearestPoint);
            }

            Point3D? bestEdgeLocal = null;
            var bestEdgeDistance = double.MaxValue;
            foreach (var edge in semanticReferences.EdgeLocals)
            {
                var projected = ClosestPointOnSegment(edge.StartLocal, edge.EndLocal, localHit);
                var distance = (projected - localHit).LengthSquared;
                if (distance < bestEdgeDistance)
                {
                    bestEdgeDistance = distance;
                    bestEdgeLocal = projected;
                }
            }

            if (bestEdgeLocal.HasValue)
                AddCandidate(CustomDimensionSnapMode.Edge, bestEdgeLocal.Value);
        }

        if (componentHits.Count >= 2)
        {
            for (var i = 0; i < componentHits.Count - 1; i++)
            {
                for (var j = i + 1; j < componentHits.Count; j++)
                {
                    var a = componentHits[i];
                    var b = componentHits[j];
                    if (string.Equals(a.Component.Id, b.Component.Id, StringComparison.Ordinal))
                        continue;

                    var intersectionWorld = new Point3D(
                        (a.Transform.Transform(a.LocalHit).X + b.Transform.Transform(b.LocalHit).X) / 2.0,
                        (a.Transform.Transform(a.LocalHit).Y + b.Transform.Transform(b.LocalHit).Y) / 2.0,
                        (a.Transform.Transform(a.LocalHit).Z + b.Transform.Transform(b.LocalHit).Z) / 2.0);
                    var score = ComputeScreenSnapDistance(screenPoint, intersectionWorld);
                    if (score == double.MaxValue)
                        continue;

                    candidates.Add(new DimensionSnapCandidateInfo
                    {
                        Kind = DimensionSnapKind.Intersection,
                        ElementId = null,
                        WorldPoint = intersectionWorld,
                        LocalPoint = default,
                        ScreenDistancePx = score,
                        IsVisibleInView = true,
                        IsValidForDimension = true,
                        HasStableReference = false
                    });
                }
            }
        }

        if (candidates.Count == 0)
            return false;

        var requestedKind = _customDimensionSnapMode == CustomDimensionSnapMode.Auto
            ? DimensionSnapKind.None
            : ToDimensionSnapKind(_customDimensionSnapMode);
        var selectionContext = new DimensionSnapSelectionContext
        {
            SnapTolerancePx = GetDimensionSnapTolerancePixels(candidates),
            RequestedKind = requestedKind,
            LastPreviewSnap = _customDimensionPlacementState.LastPreviewSnap
        };

        var selectedCandidate = _dimensionSnapSelectionService.SelectBestCandidate(candidates, selectionContext);
        if (selectedCandidate == null)
            return false;

        _customDimensionPlacementState.LastPreviewSnap = selectedCandidate;

        anchor = new CustomDimensionAnchor
        {
            ComponentId = selectedCandidate.ElementId,
            LocalPoint = selectedCandidate.LocalPoint,
            WorldPoint = selectedCandidate.WorldPoint
        };
        modeUsed = ToCustomDimensionSnapMode(selectedCandidate.Kind);
        return true;
    }

    private bool TryResolveCustomDimensionAnchorWorldPoint(CustomDimensionAnchor anchor, out Point3D worldPoint)
    {
        if (!string.IsNullOrEmpty(anchor.ComponentId))
        {
            var component = _viewModel.Components.FirstOrDefault(c => string.Equals(c.Id, anchor.ComponentId, StringComparison.Ordinal));
            if (component != null)
            {
                worldPoint = CreateComponentTransform(component).Transform(anchor.LocalPoint);
                return true;
            }
        }

        worldPoint = anchor.WorldPoint;
        return true;
    }

    private bool IsCustomDimensionAnchorVisible(CustomDimensionAnchor anchor, IReadOnlyDictionary<string, bool> layerVisibilityById)
    {
        if (string.IsNullOrEmpty(anchor.ComponentId))
            return true;

        var component = _viewModel.Components.FirstOrDefault(c => string.Equals(c.Id, anchor.ComponentId, StringComparison.Ordinal));
        return component != null && IsLayerVisible(layerVisibilityById, component.LayerId);
    }
}
