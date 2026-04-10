using System.Windows;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Shared geometry helpers for selecting and moving rendered markups on the 2D canvas.
/// </summary>
public sealed class MarkupInteractionService
{
    private const double MinimumScale = 0.05;
    private const double MinimumArcSweepDegrees = 1.0;
    private static readonly IReadOnlyList<MarkupArcAngleHandle> ArcAngleHandles = new[] { MarkupArcAngleHandle.Start, MarkupArcAngleHandle.End };
    private static readonly IReadOnlyList<MarkupArcAngleHandle> EndOnlyArcAngleHandle = new[] { MarkupArcAngleHandle.End };

    public IReadOnlyList<MarkupRecord> GetSelectionSet(MarkupRecord selectedMarkup, IEnumerable<MarkupRecord> allMarkups)
    {
        var groupId = GetGroupId(selectedMarkup);
        if (string.IsNullOrWhiteSpace(groupId))
            return new[] { selectedMarkup };

        return allMarkups
            .Where(markup => string.Equals(GetGroupId(markup), groupId, StringComparison.Ordinal))
            .ToList();
    }

    public Rect GetAggregateBounds(IEnumerable<MarkupRecord> markups)
    {
        Rect bounds = Rect.Empty;

        foreach (var markup in markups)
        {
            var markupBounds = GetBounds(markup);
            if (markupBounds == Rect.Empty)
                continue;

            bounds = bounds == Rect.Empty ? markupBounds : Rect.Union(bounds, markupBounds);
        }

        return bounds;
    }

    public bool CanResize(IEnumerable<MarkupRecord> markups)
    {
        var hasAny = false;
        foreach (var markup in markups)
        {
            hasAny = true;
            if (!IsResizable(markup))
                return false;
        }

        return hasAny;
    }

    public bool CanRotate(IEnumerable<MarkupRecord> markups)
    {
        var hasAny = false;
        foreach (var markup in markups)
        {
            hasAny = true;
            if (!IsRotatable(markup))
                return false;
        }

        return hasAny;
    }

    public bool CanEditVertices(MarkupRecord markup)
    {
        return markup.Type switch
        {
            MarkupType.Polyline => true,
            MarkupType.Polygon => true,
            MarkupType.Callout => true,
            MarkupType.LeaderNote => true,
            MarkupType.RevisionCloud => true,
            MarkupType.Dimension => true,
            MarkupType.Measurement => true,
            _ => false
        };
    }

    public bool CanEditRadius(MarkupRecord markup)
    {
        return markup.Type switch
        {
            MarkupType.Circle => markup.Vertices.Count >= 1,
            MarkupType.Arc => markup.Vertices.Count >= 1,
            MarkupType.Dimension or MarkupType.Measurement => IsAngularGeometryEditable(markup) || IsArcLengthGeometryEditable(markup),
            _ => false
        };
    }

    public bool CanEditArcAngles(MarkupRecord markup)
    {
        if (markup.Type == MarkupType.Arc)
            return markup.Vertices.Count >= 1 && markup.Radius > 0.1;

        return IsAngularGeometryEditable(markup) || IsArcLengthGeometryEditable(markup);
    }

    public bool CanInsertVertices(MarkupRecord markup)
    {
        return markup.Type switch
        {
            MarkupType.Polyline => true,
            MarkupType.Polygon => true,
            MarkupType.Callout => true,
            MarkupType.LeaderNote => true,
            MarkupType.RevisionCloud => true,
            _ => false
        };
    }

    public int GetMinimumVertexCount(MarkupRecord markup)
    {
        return markup.Type switch
        {
            MarkupType.Polygon => 3,
            MarkupType.Polyline => 2,
            MarkupType.RevisionCloud => 2,
            MarkupType.Callout => 2,
            MarkupType.LeaderNote => 2,
            MarkupType.Dimension => 2,
            MarkupType.Measurement => 2,
            _ => 0
        };
    }

    public bool CanDeleteVertex(MarkupRecord markup)
    {
        return CanEditVertices(markup) && markup.Vertices.Count > GetMinimumVertexCount(markup);
    }

    public IReadOnlyList<Point> GetVertexHandlePoints(MarkupRecord markup)
    {
        return CanEditVertices(markup) ? markup.Vertices : Array.Empty<Point>();
    }

    public Point GetRadiusHandlePoint(MarkupRecord markup)
    {
        if (!CanEditRadius(markup))
            return default;

        if (IsAngularGeometryEditable(markup))
        {
            var vertex = markup.Vertices[0];
            var firstRayEnd = markup.Vertices[1];
            var firstRayAngleDeg = Math.Atan2(firstRayEnd.Y - vertex.Y, firstRayEnd.X - vertex.X) * 180.0 / Math.PI;
            return GetPolarPoint(vertex, Math.Max(markup.Radius, 0.1), firstRayAngleDeg + markup.ArcSweepDeg / 2.0);
        }

        if (IsArcLengthGeometryEditable(markup))
        {
            var arcCenter = GetArcLengthCenter(markup);
            return GetPolarPoint(arcCenter, Math.Max(markup.Radius, 0.1), markup.ArcStartDeg + markup.ArcSweepDeg / 2.0);
        }

        var center = markup.Vertices[0];
        var angleDeg = markup.Type == MarkupType.Arc
            ? markup.ArcStartDeg + markup.ArcSweepDeg / 2.0
            : 0.0;
        return GetPolarPoint(center, markup.Radius, angleDeg);
    }

    public Point GetRadiusPivotPoint(MarkupRecord markup)
    {
        if (!CanEditRadius(markup))
            return default;

        if (IsArcLengthGeometryEditable(markup))
            return GetArcLengthCenter(markup);

        return markup.Vertices[0];
    }

    public IReadOnlyList<MarkupArcAngleHandle> GetArcAngleHandles(MarkupRecord markup)
    {
        if (!CanEditArcAngles(markup))
            return Array.Empty<MarkupArcAngleHandle>();

        return markup.Type == MarkupType.Arc ? ArcAngleHandles : EndOnlyArcAngleHandle;
    }

    public Point GetArcAngleHandlePoint(MarkupRecord markup, MarkupArcAngleHandle handle)
    {
        if (!CanEditArcAngles(markup) || handle == MarkupArcAngleHandle.None)
            return default;

        if (IsAngularGeometryEditable(markup))
            return handle == MarkupArcAngleHandle.End ? markup.Vertices[2] : default;

        if (IsArcLengthGeometryEditable(markup))
            return handle == MarkupArcAngleHandle.End ? markup.Vertices[1] : default;

        var angleDeg = handle == MarkupArcAngleHandle.Start
            ? markup.ArcStartDeg
            : markup.ArcStartDeg + markup.ArcSweepDeg;
        return GetPolarPoint(markup.Vertices[0], markup.Radius, angleDeg);
    }

    public bool TryGetArcAnglePivotPoint(MarkupRecord markup, MarkupArcAngleHandle handle, out Point pivot)
    {
        pivot = default;
        if (!CanEditArcAngles(markup) || handle == MarkupArcAngleHandle.None)
            return false;

        if (markup.Type == MarkupType.Arc)
        {
            pivot = markup.Vertices[0];
            return true;
        }

        if (IsAngularGeometryEditable(markup) && handle == MarkupArcAngleHandle.End)
        {
            pivot = markup.Vertices[0];
            return true;
        }

        if (IsArcLengthGeometryEditable(markup) && handle == MarkupArcAngleHandle.End)
        {
            pivot = GetArcLengthCenter(markup);
            return true;
        }

        return false;
    }

    public bool HitTestRadiusHandle(Point point, MarkupRecord markup, double tolerance)
    {
        if (!CanEditRadius(markup))
            return false;

        return (GetRadiusHandlePoint(markup) - point).Length <= tolerance;
    }

    public MarkupArcAngleHandle HitTestArcAngleHandle(Point point, MarkupRecord markup, double tolerance)
    {
        if (!CanEditArcAngles(markup))
            return MarkupArcAngleHandle.None;

        foreach (var handle in GetArcAngleHandles(markup).Reverse())
        {
            if ((GetArcAngleHandlePoint(markup, handle) - point).Length <= tolerance)
                return handle;
        }

        return MarkupArcAngleHandle.None;
    }

    public int HitTestVertexHandle(Point point, MarkupRecord markup, double tolerance)
    {
        if (!CanEditVertices(markup))
            return -1;

        for (int i = markup.Vertices.Count - 1; i >= 0; i--)
        {
            if ((markup.Vertices[i] - point).Length <= tolerance)
                return i;
        }

        return -1;
    }

    public bool TryFindInsertionPoint(Point point, MarkupRecord markup, double tolerance, out int insertIndex, out Point projectedPoint)
    {
        insertIndex = -1;
        projectedPoint = default;

        if (!CanInsertVertices(markup) || markup.Vertices.Count < 2)
            return false;

        var bestDistanceSq = tolerance * tolerance;
        var bestIndex = -1;
        Point bestPoint = default;

        for (int i = 0; i < markup.Vertices.Count - 1; i++)
        {
            var candidatePoint = ProjectPointToSegment(point, markup.Vertices[i], markup.Vertices[i + 1]);
            var distanceSq = DistanceSquared(point, candidatePoint);
            if (distanceSq > bestDistanceSq)
                continue;

            bestDistanceSq = distanceSq;
            bestIndex = i + 1;
            bestPoint = candidatePoint;
        }

        if (markup.Type == MarkupType.Polygon && markup.Vertices.Count >= 3)
        {
            var closingPoint = ProjectPointToSegment(point, markup.Vertices[^1], markup.Vertices[0]);
            var closingDistanceSq = DistanceSquared(point, closingPoint);
            if (closingDistanceSq <= bestDistanceSq)
            {
                bestIndex = markup.Vertices.Count;
                bestPoint = closingPoint;
                bestDistanceSq = closingDistanceSq;
            }
        }

        if (bestIndex < 0)
            return false;

        insertIndex = bestIndex;
        projectedPoint = bestPoint;
        return true;
    }

    public MarkupResizeHandle HitTestResizeHandle(Point point, Rect bounds, double tolerance)
    {
        if (bounds == Rect.Empty)
            return MarkupResizeHandle.None;

        var bestHandle = MarkupResizeHandle.None;
        var bestDistance = double.MaxValue;

        foreach (var handle in Enum.GetValues<MarkupResizeHandle>())
        {
            if (handle == MarkupResizeHandle.None)
                continue;

            var handlePoint = GetResizeHandlePoint(bounds, handle);
            var distance = (handlePoint - point).Length;
            if (distance <= tolerance && distance < bestDistance)
            {
                bestHandle = handle;
                bestDistance = distance;
            }
        }

        return bestHandle;
    }

    public Point GetResizeHandlePoint(Rect bounds, MarkupResizeHandle handle)
    {
        return handle switch
        {
            MarkupResizeHandle.TopLeft => bounds.TopLeft,
            MarkupResizeHandle.Top => new Point(bounds.X + bounds.Width / 2.0, bounds.Y),
            MarkupResizeHandle.TopRight => bounds.TopRight,
            MarkupResizeHandle.Right => new Point(bounds.Right, bounds.Y + bounds.Height / 2.0),
            MarkupResizeHandle.BottomLeft => bounds.BottomLeft,
            MarkupResizeHandle.BottomRight => bounds.BottomRight,
            MarkupResizeHandle.Bottom => new Point(bounds.X + bounds.Width / 2.0, bounds.Bottom),
            MarkupResizeHandle.Left => new Point(bounds.X, bounds.Y + bounds.Height / 2.0),
            _ => new Point(bounds.X + bounds.Width / 2.0, bounds.Y + bounds.Height / 2.0)
        };
    }

    public Point GetRotationHandlePoint(Rect bounds, double rotationDegrees, double offset)
    {
        if (bounds == Rect.Empty)
            return default;

        var center = GetRectCenter(bounds);
        var basePoint = new Point(center.X, bounds.Top - Math.Max(0.0, offset));
        return RotatePoint(basePoint, center, rotationDegrees);
    }

    public Rect BuildResizedBounds(Rect originalBounds, Point dragPoint, MarkupResizeHandle handle, double minimumSize, bool preserveAspectRatio = false, bool resizeFromCenter = false)
    {
        if (originalBounds == Rect.Empty || handle == MarkupResizeHandle.None)
            return originalBounds;

        if (!preserveAspectRatio && !resizeFromCenter)
            return BuildFreeformResizedBounds(originalBounds, dragPoint, handle, minimumSize);

        var (horizontalDirection, verticalDirection) = GetResizeHandleDirection(handle);
        var originalWidth = Math.Max(originalBounds.Width, 0.0001);
        var originalHeight = Math.Max(originalBounds.Height, 0.0001);

        if (resizeFromCenter)
        {
            return BuildCenteredResizedBounds(
                originalBounds,
                dragPoint,
                horizontalDirection,
                verticalDirection,
                minimumSize,
                preserveAspectRatio,
                originalWidth,
                originalHeight);
        }

        return BuildAnchoredResizedBounds(
            originalBounds,
            dragPoint,
            horizontalDirection,
            verticalDirection,
            minimumSize,
            preserveAspectRatio,
            originalWidth,
            originalHeight);
    }

    private static Rect BuildFreeformResizedBounds(Rect originalBounds, Point dragPoint, MarkupResizeHandle handle, double minimumSize)
    {
        if (originalBounds == Rect.Empty || handle == MarkupResizeHandle.None)
            return originalBounds;

        if (handle is MarkupResizeHandle.Top or MarkupResizeHandle.Bottom)
        {
            var edgeMinY = handle == MarkupResizeHandle.Top
                ? Math.Min(dragPoint.Y, originalBounds.Bottom - minimumSize)
                : originalBounds.Top;
            var edgeMaxY = handle == MarkupResizeHandle.Bottom
                ? Math.Max(dragPoint.Y, originalBounds.Top + minimumSize)
                : originalBounds.Bottom;

            return new Rect(new Point(originalBounds.Left, edgeMinY), new Point(originalBounds.Right, edgeMaxY));
        }

        if (handle is MarkupResizeHandle.Left or MarkupResizeHandle.Right)
        {
            var edgeMinX = handle == MarkupResizeHandle.Left
                ? Math.Min(dragPoint.X, originalBounds.Right - minimumSize)
                : originalBounds.Left;
            var edgeMaxX = handle == MarkupResizeHandle.Right
                ? Math.Max(dragPoint.X, originalBounds.Left + minimumSize)
                : originalBounds.Right;

            return new Rect(new Point(edgeMinX, originalBounds.Top), new Point(edgeMaxX, originalBounds.Bottom));
        }

        var fixedCorner = handle switch
        {
            MarkupResizeHandle.TopLeft => originalBounds.BottomRight,
            MarkupResizeHandle.TopRight => originalBounds.BottomLeft,
            MarkupResizeHandle.BottomLeft => originalBounds.TopRight,
            MarkupResizeHandle.BottomRight => originalBounds.TopLeft,
            _ => originalBounds.BottomRight
        };

        var minX = Math.Min(fixedCorner.X, dragPoint.X);
        var maxX = Math.Max(fixedCorner.X, dragPoint.X);
        var minY = Math.Min(fixedCorner.Y, dragPoint.Y);
        var maxY = Math.Max(fixedCorner.Y, dragPoint.Y);

        if (maxX - minX < minimumSize)
        {
            if (dragPoint.X <= fixedCorner.X)
                minX = maxX - minimumSize;
            else
                maxX = minX + minimumSize;
        }

        if (maxY - minY < minimumSize)
        {
            if (dragPoint.Y <= fixedCorner.Y)
                minY = maxY - minimumSize;
            else
                maxY = minY + minimumSize;
        }

        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    private static Rect BuildCenteredResizedBounds(
        Rect originalBounds,
        Point dragPoint,
        int horizontalDirection,
        int verticalDirection,
        double minimumSize,
        bool preserveAspectRatio,
        double originalWidth,
        double originalHeight)
    {
        var center = new Point(originalBounds.X + originalBounds.Width / 2.0, originalBounds.Y + originalBounds.Height / 2.0);
        var width = horizontalDirection == 0 ? originalBounds.Width : Math.Abs(dragPoint.X - center.X) * 2.0;
        var height = verticalDirection == 0 ? originalBounds.Height : Math.Abs(dragPoint.Y - center.Y) * 2.0;

        if (preserveAspectRatio)
        {
            if (horizontalDirection == 0 && verticalDirection != 0)
            {
                height = Math.Max(height, minimumSize);
                width = height * originalWidth / originalHeight;
            }
            else if (verticalDirection == 0 && horizontalDirection != 0)
            {
                width = Math.Max(width, minimumSize);
                height = width * originalHeight / originalWidth;
            }
            else
            {
                ConstrainSizeToAspectRatio(ref width, ref height, originalWidth, originalHeight);
            }

            ApplyMinimumSize(ref width, ref height, minimumSize, preserveAspectRatio: true, adjustWidth: horizontalDirection != 0, adjustHeight: verticalDirection != 0);
        }
        else
        {
            ApplyMinimumSize(ref width, ref height, minimumSize, preserveAspectRatio: false, adjustWidth: horizontalDirection != 0, adjustHeight: verticalDirection != 0);
        }

        return new Rect(
            center.X - width / 2.0,
            center.Y - height / 2.0,
            width,
            height);
    }

    private static Rect BuildAnchoredResizedBounds(
        Rect originalBounds,
        Point dragPoint,
        int horizontalDirection,
        int verticalDirection,
        double minimumSize,
        bool preserveAspectRatio,
        double originalWidth,
        double originalHeight)
    {
        var center = new Point(originalBounds.X + originalBounds.Width / 2.0, originalBounds.Y + originalBounds.Height / 2.0);

        if (horizontalDirection == 0)
        {
            var fixedY = verticalDirection < 0 ? originalBounds.Bottom : originalBounds.Top;
            var height = Math.Abs(dragPoint.Y - fixedY);
            var width = preserveAspectRatio ? height * originalWidth / originalHeight : originalBounds.Width;

            ApplyMinimumSize(ref width, ref height, minimumSize, preserveAspectRatio, adjustWidth: preserveAspectRatio, adjustHeight: true);

            var left = center.X - width / 2.0;
            var top = verticalDirection < 0 ? fixedY - height : fixedY;
            return new Rect(left, top, width, height);
        }

        if (verticalDirection == 0)
        {
            var fixedX = horizontalDirection < 0 ? originalBounds.Right : originalBounds.Left;
            var width = Math.Abs(dragPoint.X - fixedX);
            var height = preserveAspectRatio ? width * originalHeight / originalWidth : originalBounds.Height;

            ApplyMinimumSize(ref width, ref height, minimumSize, preserveAspectRatio, adjustWidth: true, adjustHeight: preserveAspectRatio);

            var left = horizontalDirection < 0 ? fixedX - width : fixedX;
            var top = center.Y - height / 2.0;
            return new Rect(left, top, width, height);
        }

        var anchorX = horizontalDirection < 0 ? originalBounds.Right : originalBounds.Left;
        var anchorY = verticalDirection < 0 ? originalBounds.Bottom : originalBounds.Top;
        var cornerWidth = Math.Abs(dragPoint.X - anchorX);
        var cornerHeight = Math.Abs(dragPoint.Y - anchorY);

        if (preserveAspectRatio)
            ConstrainSizeToAspectRatio(ref cornerWidth, ref cornerHeight, originalWidth, originalHeight);

        ApplyMinimumSize(ref cornerWidth, ref cornerHeight, minimumSize, preserveAspectRatio, adjustWidth: true, adjustHeight: true);

        var leftCorner = horizontalDirection < 0 ? anchorX - cornerWidth : anchorX;
        var topCorner = verticalDirection < 0 ? anchorY - cornerHeight : anchorY;
        return new Rect(leftCorner, topCorner, cornerWidth, cornerHeight);
    }

    private static void ConstrainSizeToAspectRatio(ref double width, ref double height, double originalWidth, double originalHeight)
    {
        var widthScale = width / originalWidth;
        var heightScale = height / originalHeight;

        if (widthScale >= heightScale)
            height = width * originalHeight / originalWidth;
        else
            width = height * originalWidth / originalHeight;
    }

    private static void ApplyMinimumSize(ref double width, ref double height, double minimumSize, bool preserveAspectRatio, bool adjustWidth, bool adjustHeight)
    {
        if (!preserveAspectRatio)
        {
            if (adjustWidth)
                width = Math.Max(width, minimumSize);
            if (adjustHeight)
                height = Math.Max(height, minimumSize);
            return;
        }

        var scale = 1.0;
        if (adjustWidth && width < minimumSize)
            scale = Math.Max(scale, minimumSize / Math.Max(width, 0.0001));
        if (adjustHeight && height < minimumSize)
            scale = Math.Max(scale, minimumSize / Math.Max(height, 0.0001));

        width *= scale;
        height *= scale;
    }

    private static (int Horizontal, int Vertical) GetResizeHandleDirection(MarkupResizeHandle handle)
    {
        return handle switch
        {
            MarkupResizeHandle.TopLeft => (-1, -1),
            MarkupResizeHandle.Top => (0, -1),
            MarkupResizeHandle.TopRight => (1, -1),
            MarkupResizeHandle.Right => (1, 0),
            MarkupResizeHandle.BottomRight => (1, 1),
            MarkupResizeHandle.Bottom => (0, 1),
            MarkupResizeHandle.BottomLeft => (-1, 1),
            MarkupResizeHandle.Left => (-1, 0),
            _ => (0, 0)
        };
    }

    public void Translate(MarkupRecord markup, Vector delta)
    {
        if (delta.X == 0 && delta.Y == 0)
            return;

        for (int i = 0; i < markup.Vertices.Count; i++)
            markup.Vertices[i] = markup.Vertices[i] + delta;

        if (markup.BoundingRect != Rect.Empty)
        {
            markup.BoundingRect = new Rect(
                markup.BoundingRect.X + delta.X,
                markup.BoundingRect.Y + delta.Y,
                markup.BoundingRect.Width,
                markup.BoundingRect.Height);
        }
        else
        {
            markup.UpdateBoundingRect();
        }

        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
    }

    public void MoveVertex(MarkupRecord markup, int vertexIndex, Point targetPoint)
    {
        if (!CanEditVertices(markup) || vertexIndex < 0 || vertexIndex >= markup.Vertices.Count)
            return;

        markup.Vertices[vertexIndex] = targetPoint;
        markup.UpdateBoundingRect();
        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
    }

    public void SetRadius(MarkupRecord markup, double radius)
    {
        if (!CanEditRadius(markup))
            return;

        if (IsAngularGeometryEditable(markup))
        {
            SetAngularGeometry(markup, Math.Abs(markup.ArcSweepDeg), radius);
            return;
        }

        if (IsArcLengthGeometryEditable(markup))
        {
            var arcLength = Math.Abs(markup.ArcSweepDeg) * Math.PI / 180.0 * Math.Max(markup.Radius, 0.1);
            SetArcLengthGeometry(markup, arcLength, radius);
            return;
        }

        markup.Radius = Math.Max(0.1, radius);
        markup.UpdateBoundingRect();
        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
    }

    public void SetArcAngle(MarkupRecord markup, MarkupArcAngleHandle handle, double angleDeg)
    {
        if (!CanEditArcAngles(markup) || handle == MarkupArcAngleHandle.None)
            return;

        if (IsAngularGeometryEditable(markup))
        {
            if (handle != MarkupArcAngleHandle.End)
                return;

            var vertex = markup.Vertices[0];
            var firstRayEnd = markup.Vertices[1];
            var firstRayAngleDeg = Math.Atan2(firstRayEnd.Y - vertex.Y, firstRayEnd.X - vertex.X) * 180.0 / Math.PI;
            var normalizedTarget = NormalizeAngleDegrees(angleDeg);
            var signedSweep = markup.ArcSweepDeg >= 0
                ? GetPositiveSweepDegrees(firstRayAngleDeg, normalizedTarget)
                : -GetPositiveSweepDegrees(normalizedTarget, firstRayAngleDeg);

            SetAngularGeometry(markup, Math.Abs(signedSweep), markup.Radius);
            return;
        }

        if (IsArcLengthGeometryEditable(markup))
        {
            if (handle != MarkupArcAngleHandle.End)
                return;

            var normalizedStart = NormalizeAngleDegrees(markup.ArcStartDeg);
            var normalizedTarget = NormalizeAngleDegrees(angleDeg);
            var nextSweep = markup.ArcSweepDeg >= 0
                ? GetPositiveSweepDegrees(normalizedStart, normalizedTarget)
                : -GetPositiveSweepDegrees(normalizedTarget, normalizedStart);

            SetArcLengthSweep(markup, nextSweep);
            return;
        }

        var normalizedAngle = NormalizeAngleDegrees(angleDeg);
        var isPositiveSweep = markup.ArcSweepDeg >= 0;
        var currentEndAngle = markup.ArcStartDeg + markup.ArcSweepDeg;
        double newStart;
        double newSweep;

        if (handle == MarkupArcAngleHandle.Start)
        {
            newStart = normalizedAngle;
            newSweep = isPositiveSweep
                ? GetPositiveSweepDegrees(newStart, currentEndAngle)
                : -GetPositiveSweepDegrees(currentEndAngle, newStart);
        }
        else
        {
            newStart = NormalizeAngleDegrees(markup.ArcStartDeg);
            newSweep = isPositiveSweep
                ? GetPositiveSweepDegrees(newStart, normalizedAngle)
                : -GetPositiveSweepDegrees(normalizedAngle, newStart);
        }

        if (Math.Abs(newSweep) < MinimumArcSweepDegrees)
            newSweep = isPositiveSweep ? MinimumArcSweepDegrees : -MinimumArcSweepDegrees;

        markup.ArcStartDeg = newStart;
        markup.ArcSweepDeg = newSweep;
        markup.UpdateBoundingRect();
        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
    }

    public void SetArcAngleFromPoint(MarkupRecord markup, MarkupArcAngleHandle handle, Point point, double snapIncrementDeg = 0)
    {
        if (!TryGetArcAnglePivotPoint(markup, handle, out var pivot))
            return;

        if ((point - pivot).Length <= double.Epsilon)
            return;

        var angleDeg = Math.Atan2(point.Y - pivot.Y, point.X - pivot.X) * 180.0 / Math.PI;
        if (snapIncrementDeg > 0)
            angleDeg = SnapAngleDegrees(angleDeg, snapIncrementDeg);

        SetArcAngle(markup, handle, angleDeg);
    }

    public bool SetArcGeometry(MarkupRecord markup, double radius, double startAngleDeg, double? sweepAngleDeg = null, double? endAngleDeg = null)
    {
        if (!CanEditArcAngles(markup))
            return false;

        var normalizedStart = NormalizeAngleDegrees(startAngleDeg);
        var nextSweep = sweepAngleDeg ?? markup.ArcSweepDeg;

        if (endAngleDeg.HasValue && !sweepAngleDeg.HasValue)
        {
            var normalizedEnd = NormalizeAngleDegrees(endAngleDeg.Value);
            var isPositiveSweep = markup.ArcSweepDeg >= 0;
            nextSweep = isPositiveSweep
                ? GetPositiveSweepDegrees(normalizedStart, normalizedEnd)
                : -GetPositiveSweepDegrees(normalizedEnd, normalizedStart);
        }

        if (Math.Abs(nextSweep) < MinimumArcSweepDegrees)
        {
            var positiveSweep = nextSweep >= 0 || Math.Abs(nextSweep) < double.Epsilon && markup.ArcSweepDeg >= 0;
            nextSweep = positiveSweep ? MinimumArcSweepDegrees : -MinimumArcSweepDegrees;
        }

        markup.Radius = Math.Max(0.1, radius);
        markup.ArcStartDeg = normalizedStart;
        markup.ArcSweepDeg = nextSweep;
        markup.UpdateBoundingRect();
        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
        return true;
    }

    public bool SetPolylineGeometry(MarkupRecord markup, List<Point> vertices)
    {
        if (!IsPolylineGeometryEditable(markup))
            return false;

        var minCount = markup.Type == MarkupType.Polygon ? 3 : 2;
        if (vertices.Count < minCount)
            return false;

        markup.Vertices.Clear();
        markup.Vertices.AddRange(vertices);
        markup.UpdateBoundingRect();
        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
        return true;
    }

    public bool SetBoundsGeometry(MarkupRecord markup, double width, double height)
    {
        if (!IsBoundsGeometryEditable(markup))
            return false;

        if (markup.Type == MarkupType.Text && markup.BoundingRect == Rect.Empty)
            return false;

        var bounds = GetBounds(markup);
        if (bounds == Rect.Empty)
            return false;

        var nextWidth = Math.Max(0.1, width);
        var nextHeight = Math.Max(0.1, height);
        var topLeft = bounds.TopLeft;
        var bottomRight = new Point(topLeft.X + nextWidth, topLeft.Y + nextHeight);
        var nextBounds = new Rect(topLeft, bottomRight);

        markup.BoundingRect = nextBounds;
        markup.Vertices.Clear();

        if (markup.Type == MarkupType.Text)
        {
            markup.Vertices.Add(GetTextAnchorFromBounds(markup, nextBounds));

            var scaleX = GetScaleFactor(bounds.Width, nextBounds.Width);
            var scaleY = GetScaleFactor(bounds.Height, nextBounds.Height);
            var styleScale = Math.Max(MinimumScale, Math.Min(scaleX, scaleY));
            markup.Appearance.FontSize = Math.Max(1.0, markup.Appearance.FontSize * styleScale);
            markup.Appearance.StrokeWidth = markup.Appearance.StrokeWidth <= 0
                ? 0
                : Math.Max(0.4, markup.Appearance.StrokeWidth * styleScale);
        }
        else
        {
            markup.Vertices.Add(topLeft);
            markup.Vertices.Add(bottomRight);
        }

        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
        return true;
    }

    public bool SetLineGeometry(MarkupRecord markup, double length, double angleDeg)
    {
        if (!IsLineGeometryEditable(markup))
            return false;

        var start = markup.Vertices[0];
        var nextLength = Math.Max(0.1, length);
        var nextEnd = GetPolarPoint(start, nextLength, angleDeg);
        return SetLineGeometryByEndpoints(markup, start, nextEnd);
    }

    public bool SetLineGeometryByEndpoints(MarkupRecord markup, Point startPoint, Point endPoint)
    {
        if (!IsLineGeometryEditable(markup))
            return false;

        var originalStart = markup.Vertices[0];
        var originalEnd = markup.Vertices[1];
        var start = startPoint;
        var nextEnd = endPoint;
        var originalAngleDeg = Math.Atan2(originalEnd.Y - originalStart.Y, originalEnd.X - originalStart.X) * 180.0 / Math.PI;
        var originalLength = (originalEnd - originalStart).Length;
        var nextLength = (nextEnd - start).Length;
        if (nextLength <= 0.1)
        {
            var fallbackAngleDeg = originalLength <= double.Epsilon
                ? 0.0
                : Math.Atan2(originalEnd.Y - originalStart.Y, originalEnd.X - originalStart.X) * 180.0 / Math.PI;
            nextEnd = GetPolarPoint(start, 0.1, fallbackAngleDeg);
            nextLength = (nextEnd - start).Length;
        }

        var nextAngleDeg = Math.Atan2(nextEnd.Y - start.Y, nextEnd.X - start.X) * 180.0 / Math.PI;
        var originalReferenceMidpoint = GetMidpoint(originalStart, originalEnd);
        markup.Vertices[1] = nextEnd;
        markup.Vertices[0] = start;

        if (markup.Vertices.Count > 2)
        {
            var nextMidpoint = GetMidpoint(start, nextEnd);
            var rotationDeltaDeg = NormalizeAngleDegrees(nextAngleDeg) - NormalizeAngleDegrees(originalAngleDeg);
            var scale = originalLength <= double.Epsilon ? 1.0 : nextLength / originalLength;

            for (int i = 2; i < markup.Vertices.Count; i++)
            {
                var offset = markup.Vertices[i] - originalReferenceMidpoint;
                markup.Vertices[i] = nextMidpoint + RotateAndScale(offset, rotationDeltaDeg, scale);
            }
        }

        markup.UpdateBoundingRect();
        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
        return true;
    }

    public bool SetAngularGeometry(MarkupRecord markup, double angleDeg, double radius)
    {
        if (!IsAngularGeometryEditable(markup))
            return false;

        var vertex = markup.Vertices[0];
        var firstRayEnd = markup.Vertices[1];
        var secondRayEnd = markup.Vertices[2];
        var firstRayAngleDeg = Math.Atan2(firstRayEnd.Y - vertex.Y, firstRayEnd.X - vertex.X) * 180.0 / Math.PI;
        var secondRayLength = Math.Max((secondRayEnd - vertex).Length, 0.1);
        var nextRadius = Math.Max(0.1, radius);
        var nextSweepMagnitude = Math.Clamp(Math.Abs(angleDeg), MinimumArcSweepDegrees, 359.0);
        var nextSweepDeg = markup.ArcSweepDeg < 0 ? -nextSweepMagnitude : nextSweepMagnitude;
        var nextSecondRayAngleDeg = firstRayAngleDeg + nextSweepDeg;

        markup.Vertices[2] = GetPolarPoint(vertex, secondRayLength, nextSecondRayAngleDeg);

        if (markup.Vertices.Count > 3)
        {
            var labelOffset = Math.Max(0, (markup.Vertices[3] - vertex).Length - Math.Max(markup.Radius, 0.1));
            var labelDistance = nextRadius + labelOffset;
            var labelAngleDeg = firstRayAngleDeg + nextSweepDeg / 2.0;
            markup.Vertices[3] = GetPolarPoint(vertex, labelDistance, labelAngleDeg);
        }

        markup.Radius = nextRadius;
        markup.ArcStartDeg = NormalizeAngleDegrees(firstRayAngleDeg);
        markup.ArcSweepDeg = nextSweepDeg;
        markup.UpdateBoundingRect();
        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
        return true;
    }

    public bool SetArcLengthGeometry(MarkupRecord markup, double arcLength, double radius)
    {
        if (!IsArcLengthGeometryEditable(markup))
            return false;

        var nextRadius = Math.Max(0.1, radius);
        var requestedSweepMagnitude = Math.Abs(arcLength) * 180.0 / (Math.PI * nextRadius);
        var nextSweepMagnitude = Math.Clamp(requestedSweepMagnitude, MinimumArcSweepDegrees, 359.0);
        var nextSweepDeg = markup.ArcSweepDeg < 0 ? -nextSweepMagnitude : nextSweepMagnitude;
        return SetArcLengthSweep(markup, nextSweepDeg, nextRadius);
    }

    public double SnapAngleDegrees(double angleDeg, double incrementDeg)
    {
        var normalizedAngle = NormalizeAngleDegrees(angleDeg);
        if (incrementDeg <= 0)
            return normalizedAngle;

        return NormalizeAngleDegrees(Math.Round(normalizedAngle / incrementDeg) * incrementDeg);
    }

    public bool InsertVertex(MarkupRecord markup, int insertIndex, Point point)
    {
        if (!CanInsertVertices(markup) || insertIndex <= 0 || insertIndex > markup.Vertices.Count)
            return false;

        markup.Vertices.Insert(insertIndex, point);
        markup.UpdateBoundingRect();
        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
        return true;
    }

    public bool DeleteVertex(MarkupRecord markup, int vertexIndex)
    {
        if (!CanDeleteVertex(markup) || vertexIndex < 0 || vertexIndex >= markup.Vertices.Count)
            return false;

        markup.Vertices.RemoveAt(vertexIndex);
        markup.UpdateBoundingRect();
        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
        return true;
    }

    public void Resize(MarkupRecord markup, MarkupGeometrySnapshot snapshot, Rect originalBounds, Rect targetBounds)
    {
        if (originalBounds == Rect.Empty || targetBounds == Rect.Empty)
        {
            Apply(markup, snapshot);
            return;
        }

        markup.Vertices.Clear();
        foreach (var point in snapshot.Vertices)
            markup.Vertices.Add(TransformPoint(point, originalBounds, targetBounds));

        var scaleX = GetScaleFactor(originalBounds.Width, targetBounds.Width);
        var scaleY = GetScaleFactor(originalBounds.Height, targetBounds.Height);
        var styleScale = Math.Max(MinimumScale, Math.Min(scaleX, scaleY));

        if (markup.Type == MarkupType.Circle || markup.Type == MarkupType.Arc)
        {
            markup.Radius = Math.Max(0.1, snapshot.Radius * styleScale);
            markup.BoundingRect = TransformRect(GetBounds(markup), originalBounds, targetBounds);
        }
        else if (snapshot.BoundingRect != Rect.Empty)
        {
            markup.BoundingRect = TransformRect(snapshot.BoundingRect, originalBounds, targetBounds);
        }
        else
        {
            markup.UpdateBoundingRect();
        }

        markup.Appearance.FontSize = Math.Max(1.0, snapshot.FontSize * styleScale);
        markup.Appearance.StrokeWidth = snapshot.StrokeWidth <= 0
            ? 0
            : Math.Max(0.4, snapshot.StrokeWidth * styleScale);
        markup.RotationDegrees = snapshot.RotationDegrees;
        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
    }

    public void Rotate(MarkupRecord markup, MarkupGeometrySnapshot snapshot, Point rotationCenter, double deltaDegrees)
    {
        markup.Vertices.Clear();
        foreach (var point in snapshot.Vertices)
            markup.Vertices.Add(RotatePoint(point, rotationCenter, deltaDegrees));

        if (UsesBoundsRotation(markup) && snapshot.BoundingRect != Rect.Empty)
        {
            var originalCenter = GetRectCenter(snapshot.BoundingRect);
            var nextCenter = RotatePoint(originalCenter, rotationCenter, deltaDegrees);
            markup.BoundingRect = new Rect(
                nextCenter.X - snapshot.BoundingRect.Width / 2.0,
                nextCenter.Y - snapshot.BoundingRect.Height / 2.0,
                snapshot.BoundingRect.Width,
                snapshot.BoundingRect.Height);
        }
        else if (markup.Vertices.Count > 0)
        {
            markup.UpdateBoundingRect();
        }
        else if (snapshot.BoundingRect != Rect.Empty)
        {
            var rotatedCorners = GetRectCorners(snapshot.BoundingRect)
                .Select(point => RotatePoint(point, rotationCenter, deltaDegrees));
            markup.BoundingRect = GetBoundsFromPoints(rotatedCorners);
        }
        else
        {
            markup.BoundingRect = Rect.Empty;
        }

        markup.RotationDegrees = NormalizeAngleDegrees(snapshot.RotationDegrees + deltaDegrees);
        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
    }

    public MarkupGeometrySnapshot Capture(MarkupRecord markup)
    {
        return new MarkupGeometrySnapshot
        {
            Vertices = markup.Vertices.ToList(),
            BoundingRect = markup.BoundingRect,
            Radius = markup.Radius,
            ArcStartDeg = markup.ArcStartDeg,
            ArcSweepDeg = markup.ArcSweepDeg,
            RotationDegrees = markup.RotationDegrees,
            StrokeColor = markup.Appearance.StrokeColor,
            StrokeWidth = markup.Appearance.StrokeWidth,
            FillColor = markup.Appearance.FillColor,
            Opacity = markup.Appearance.Opacity,
            FontFamily = markup.Appearance.FontFamily,
            FontSize = markup.Appearance.FontSize,
            DashArray = markup.Appearance.DashArray,
            ModifiedUtc = markup.Metadata.ModifiedUtc
        };
    }

    public void Apply(MarkupRecord markup, MarkupGeometrySnapshot snapshot)
    {
        markup.Vertices.Clear();
        markup.Vertices.AddRange(snapshot.Vertices);
        markup.BoundingRect = snapshot.BoundingRect;
        markup.Radius = snapshot.Radius;
        markup.ArcStartDeg = snapshot.ArcStartDeg;
        markup.ArcSweepDeg = snapshot.ArcSweepDeg;
        markup.RotationDegrees = snapshot.RotationDegrees;
        markup.Appearance.StrokeColor = snapshot.StrokeColor;
        markup.Appearance.StrokeWidth = snapshot.StrokeWidth;
        markup.Appearance.FillColor = snapshot.FillColor;
        markup.Appearance.Opacity = snapshot.Opacity;
        markup.Appearance.FontFamily = snapshot.FontFamily;
        markup.Appearance.FontSize = snapshot.FontSize;
        markup.Appearance.DashArray = snapshot.DashArray;
        markup.Metadata.ModifiedUtc = snapshot.ModifiedUtc;
    }

    public string? GetGroupId(MarkupRecord markup)
    {
        return markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationGroupIdField, out var groupId)
            ? groupId
            : null;
    }

    private static Rect GetBounds(MarkupRecord markup)
    {
        if (markup.BoundingRect != Rect.Empty)
            return markup.BoundingRect;

        if ((markup.Type == MarkupType.Circle || markup.Type == MarkupType.Arc) && markup.Vertices.Count >= 1)
        {
            var center = markup.Vertices[0];
            return new Rect(center.X - markup.Radius, center.Y - markup.Radius, markup.Radius * 2, markup.Radius * 2);
        }

        if (markup.Vertices.Count == 0)
            return Rect.Empty;

        var minX = markup.Vertices.Min(point => point.X);
        var minY = markup.Vertices.Min(point => point.Y);
        var maxX = markup.Vertices.Max(point => point.X);
        var maxY = markup.Vertices.Max(point => point.Y);
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    private static bool IsResizable(MarkupRecord markup)
    {
        return markup.Type switch
        {
            MarkupType.Rectangle => true,
            MarkupType.Text => true,
            MarkupType.Stamp => true,
            MarkupType.Hyperlink => true,
            MarkupType.Box => true,
            MarkupType.Panel => true,
            MarkupType.Polyline => true,
            MarkupType.Polygon => true,
            _ => false
        };
    }

    private static bool IsRotatable(MarkupRecord markup)
    {
        return markup.Type switch
        {
            MarkupType.Rectangle => true,
            MarkupType.Text => true,
            MarkupType.Stamp => true,
            MarkupType.Hyperlink => true,
            MarkupType.Box => true,
            MarkupType.Panel => true,
            MarkupType.Polyline => true,
            MarkupType.Polygon => true,
            _ => false
        };
    }

    private static bool UsesBoundsRotation(MarkupRecord markup)
    {
        return markup.Type is MarkupType.Rectangle or MarkupType.Text or MarkupType.Stamp or MarkupType.Hyperlink or MarkupType.Box or MarkupType.Panel;
    }

    private static bool IsBoundsGeometryEditable(MarkupRecord markup)
    {
        return markup.Type is MarkupType.Rectangle or MarkupType.Text or MarkupType.Stamp or MarkupType.Hyperlink or MarkupType.Box or MarkupType.Panel;
    }

    private static Point GetTextAnchorFromBounds(MarkupRecord markup, Rect bounds)
    {
        var anchorX = bounds.Left;
        if (markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.TextAlignField, out var alignValue))
        {
            if (string.Equals(alignValue, "Center", StringComparison.OrdinalIgnoreCase))
                anchorX = bounds.Left + bounds.Width / 2.0;
            else if (string.Equals(alignValue, "Right", StringComparison.OrdinalIgnoreCase))
                anchorX = bounds.Right;
        }

        return new Point(anchorX, bounds.Bottom);
    }

    internal static bool IsPolylineGeometryEditable(MarkupRecord markup)
    {
        return markup.Type is MarkupType.Polyline or MarkupType.Polygon &&
               markup.Vertices.Count >= 2;
    }

    private static bool IsLineGeometryEditable(MarkupRecord markup)
    {
        if (markup.Type is not (MarkupType.Dimension or MarkupType.Measurement))
            return false;

        if (string.Equals(markup.Metadata.Subject, "ArcLength", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(markup.Metadata.Subject, "Angular", StringComparison.OrdinalIgnoreCase))
            return false;

        return markup.Vertices.Count >= 2 && markup.Vertices.Count <= 3;
    }

    private static bool IsAngularGeometryEditable(MarkupRecord markup)
    {
        return markup.Type is MarkupType.Dimension or MarkupType.Measurement &&
               string.Equals(markup.Metadata.Subject, "Angular", StringComparison.OrdinalIgnoreCase) &&
               markup.Vertices.Count >= 3;
    }

    private static bool IsArcLengthGeometryEditable(MarkupRecord markup)
    {
        return markup.Type is MarkupType.Dimension or MarkupType.Measurement &&
               string.Equals(markup.Metadata.Subject, "ArcLength", StringComparison.OrdinalIgnoreCase) &&
               markup.Vertices.Count >= 3 &&
               markup.Radius > 0.1;
    }

    private static Point GetArcLengthCenter(MarkupRecord markup)
    {
        var startPoint = markup.Vertices[0];
        var startAngleRad = markup.ArcStartDeg * Math.PI / 180.0;
        return new Point(
            startPoint.X - Math.Cos(startAngleRad) * markup.Radius,
            startPoint.Y - Math.Sin(startAngleRad) * markup.Radius);
    }

    private static bool SetArcLengthSweep(MarkupRecord markup, double sweepDeg, double? radiusOverride = null)
    {
        if (!IsArcLengthGeometryEditable(markup))
            return false;

        var nextRadius = Math.Max(0.1, radiusOverride ?? markup.Radius);
        var startAngleDeg = NormalizeAngleDegrees(markup.ArcStartDeg);
        var center = GetArcLengthCenter(markup);
        var positiveSweep = sweepDeg >= 0 || Math.Abs(sweepDeg) < double.Epsilon && markup.ArcSweepDeg >= 0;
        var nextSweepMagnitude = Math.Clamp(Math.Abs(sweepDeg), MinimumArcSweepDegrees, 359.0);
        var nextSweepDeg = positiveSweep ? nextSweepMagnitude : -nextSweepMagnitude;
        var endAngleDeg = startAngleDeg + nextSweepDeg;
        var labelOffset = markup.Vertices.Count > 2
            ? Math.Max(0, (markup.Vertices[2] - center).Length - Math.Max(markup.Radius, 0.1))
            : 0;
        var midAngleDeg = startAngleDeg + nextSweepDeg / 2.0;

        markup.Vertices[0] = GetPolarPoint(center, nextRadius, startAngleDeg);
        markup.Vertices[1] = GetPolarPoint(center, nextRadius, endAngleDeg);

        if (markup.Vertices.Count > 2)
            markup.Vertices[2] = GetPolarPoint(center, nextRadius + labelOffset, midAngleDeg);

        markup.Radius = nextRadius;
        markup.ArcStartDeg = startAngleDeg;
        markup.ArcSweepDeg = nextSweepDeg;
        markup.UpdateBoundingRect();
        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
        return true;
    }

    private static Point TransformPoint(Point point, Rect originalBounds, Rect targetBounds)
    {
        var normalizedX = Normalize(point.X, originalBounds.X, originalBounds.Width);
        var normalizedY = Normalize(point.Y, originalBounds.Y, originalBounds.Height);
        return new Point(
            targetBounds.X + normalizedX * targetBounds.Width,
            targetBounds.Y + normalizedY * targetBounds.Height);
    }

    private static Rect TransformRect(Rect rect, Rect originalBounds, Rect targetBounds)
    {
        var topLeft = TransformPoint(rect.TopLeft, originalBounds, targetBounds);
        var bottomRight = TransformPoint(rect.BottomRight, originalBounds, targetBounds);
        return new Rect(topLeft, bottomRight);
    }

    private static double Normalize(double value, double min, double size)
    {
        if (Math.Abs(size) < double.Epsilon)
            return 0;

        return (value - min) / size;
    }

    private static double GetScaleFactor(double originalSize, double targetSize)
    {
        if (Math.Abs(originalSize) < double.Epsilon)
            return 1.0;

        return targetSize / originalSize;
    }

    private static Point ProjectPointToSegment(Point point, Point segmentStart, Point segmentEnd)
    {
        var dx = segmentEnd.X - segmentStart.X;
        var dy = segmentEnd.Y - segmentStart.Y;
        var lengthSq = dx * dx + dy * dy;
        if (lengthSq < double.Epsilon)
            return segmentStart;

        var t = ((point.X - segmentStart.X) * dx + (point.Y - segmentStart.Y) * dy) / lengthSq;
        t = Math.Clamp(t, 0, 1);
        return new Point(segmentStart.X + t * dx, segmentStart.Y + t * dy);
    }

    private static Point GetPolarPoint(Point center, double radius, double angleDeg)
    {
        var angleRad = angleDeg * Math.PI / 180.0;
        return new Point(
            center.X + Math.Cos(angleRad) * radius,
            center.Y + Math.Sin(angleRad) * radius);
    }

    private static Point GetMidpoint(Point start, Point end)
    {
        return new Point((start.X + end.X) / 2.0, (start.Y + end.Y) / 2.0);
    }

    private static Vector RotateAndScale(Vector offset, double rotationDeg, double scale)
    {
        var radians = rotationDeg * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new Vector(
            (offset.X * cos - offset.Y * sin) * scale,
            (offset.X * sin + offset.Y * cos) * scale);
    }

    private static double NormalizeAngleDegrees(double angleDeg)
    {
        var normalized = angleDeg % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    private static double GetPositiveSweepDegrees(double startAngleDeg, double endAngleDeg)
    {
        return NormalizeAngleDegrees(endAngleDeg - startAngleDeg);
    }

    private static Point GetRectCenter(Rect rect)
    {
        return new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0);
    }

    private static IEnumerable<Point> GetRectCorners(Rect rect)
    {
        yield return rect.TopLeft;
        yield return rect.TopRight;
        yield return rect.BottomRight;
        yield return rect.BottomLeft;
    }

    private static Rect GetBoundsFromPoints(IEnumerable<Point> points)
    {
        var pointList = points.ToList();
        if (pointList.Count == 0)
            return Rect.Empty;

        var minX = pointList.Min(point => point.X);
        var minY = pointList.Min(point => point.Y);
        var maxX = pointList.Max(point => point.X);
        var maxY = pointList.Max(point => point.Y);
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    private static Point RotatePoint(Point point, Point center, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return new Point(
            center.X + dx * cos - dy * sin,
            center.Y + dx * sin + dy * cos);
    }

    private static double DistanceSquared(Point left, Point right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return dx * dx + dy * dy;
    }
}

public enum MarkupResizeHandle
{
    None,
    TopLeft,
    Top,
    TopRight,
    Right,
    Bottom,
    BottomLeft,
    BottomRight,
    Left
}

public enum MarkupArcAngleHandle
{
    None,
    Start,
    End
}

public sealed class MarkupGeometrySnapshot
{
    public List<Point> Vertices { get; init; } = new();
    public Rect BoundingRect { get; init; }
    public double Radius { get; init; }
    public double ArcStartDeg { get; init; }
    public double ArcSweepDeg { get; init; }
    public double RotationDegrees { get; init; }
    public string StrokeColor { get; init; } = string.Empty;
    public double StrokeWidth { get; init; }
    public string FillColor { get; init; } = string.Empty;
    public double Opacity { get; init; }
    public string FontFamily { get; init; } = string.Empty;
    public double FontSize { get; init; }
    public string DashArray { get; init; } = string.Empty;
    public DateTime ModifiedUtc { get; init; }
}
