using System.Linq;
using System.Globalization;
using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Rendering;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Converts generated drawing artifacts such as schedules, legends, and title
/// blocks into persisted <see cref="MarkupRecord"/> annotations.
/// </summary>
public sealed class DrawingAnnotationMarkupService
{
    public const string DefaultLayerId = "markup-default";
    public const string AnnotationGroupIdField = "AnnotationGroupId";
    public const string AnnotationKindField = "AnnotationKind";
    public const string AnnotationTextRoleField = "AnnotationTextRole";
    public const string AnnotationTextKeyField = "AnnotationTextKey";
    public const string AnnotationRowIndexField = "AnnotationRowIndex";
    public const string AnnotationColumnIndexField = "AnnotationColumnIndex";
    public const string ScheduleTableAnnotationKind = "ScheduleTable";
    public const string SymbolLegendAnnotationKind = "SymbolLegend";
    public const string TitleBlockAnnotationKind = "TitleBlock";
    public const string ComponentParameterTagAnnotationKind = "ComponentParameterTag";
    public const string LiveScheduleInstanceIdField = "LiveScheduleInstanceId";
    public const string TextRoleTitle = "Title";
    public const string TextRoleHeader = "Header";
    public const string TextRoleCell = "Cell";
    public const string TextRoleFieldLabel = "FieldLabel";
    public const string TextRoleFieldValue = "FieldValue";
    public const string TextRoleZoneLabel = "ZoneLabel";
    public const string ComponentParameterTagComponentIdField = "ComponentParameterTagComponentId";
    public const string ComponentParameterTagTargetField = "ComponentParameterTagTarget";
    public const string ComponentParameterTagParameterIdField = "ComponentParameterTagParameterId";
    public const string TextAlignField = "TextAlign";
    public const string BoldField = "Bold";
    public const double PdfPointsPerInch = 72.0;

    public IReadOnlyList<MarkupRecord> CreateScheduleTableMarkups(
        ScheduleTable table,
        Point origin,
        string layerId = DefaultLayerId,
        string? groupId = null,
        string? liveScheduleInstanceId = null)
    {
        return CreateScheduleTableMarkups(table, origin, layerId, ScheduleTableAnnotationKind, groupId, liveScheduleInstanceId);
    }

    private IReadOnlyList<MarkupRecord> CreateScheduleTableMarkups(
        ScheduleTable table,
        Point origin,
        string layerId,
        string annotationKind,
        string? groupId,
        string? liveScheduleInstanceId)
    {
        var markups = new List<MarkupRecord>();
        var resolvedGroupId = string.IsNullOrWhiteSpace(groupId)
            ? Guid.NewGuid().ToString("N")
            : groupId;

        var totalRect = new Rect(origin.X, origin.Y, table.TotalWidth, table.TotalHeight);
        var titleRect = new Rect(origin.X, origin.Y, table.TotalWidth, table.TitleHeight);
        var headerRect = new Rect(origin.X, origin.Y + table.TitleHeight, table.TotalWidth, table.RowHeight);
        var dataTop = origin.Y + table.TitleHeight + table.RowHeight;

        markups.Add(CreateRectangleMarkup(
            totalRect,
            layerId,
            resolvedGroupId,
            strokeColor: "#FF111827",
            strokeWidth: 1.4,
            fillColor: null,
            subject: "Table Border",
            label: table.Title));

        markups.Add(CreateRectangleMarkup(
            titleRect,
            layerId,
            resolvedGroupId,
            strokeColor: "#FF1F2937",
            strokeWidth: 1.0,
            fillColor: "#FFF3F4F6",
            subject: "Table Title",
            label: table.Title));

        markups.Add(CreateRectangleMarkup(
            headerRect,
            layerId,
            resolvedGroupId,
            strokeColor: "#FF374151",
            strokeWidth: 1.0,
            fillColor: "#FFE5E7EB",
            subject: "Table Header",
            label: $"{table.Title} Header"));

        var titleMarkup = CreateTextMarkup(
            table.Title,
            new Point(origin.X + table.TotalWidth / 2.0, origin.Y + table.TitleHeight * 0.68),
            layerId,
            resolvedGroupId,
            fontSize: 12.0,
            strokeColor: "#FF111827",
            subject: "Table Title",
            label: table.Title,
            align: TextAlign.Center,
            bold: true);
        SetStructuredTextMetadata(titleMarkup, annotationKind, TextRoleTitle, table.Title);
        markups.Add(titleMarkup);

        double currentX = origin.X;
        for (int i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];
            var headerAnchor = GetCellTextAnchor(
                new Rect(currentX, headerRect.Y, column.Width, table.RowHeight),
                column.Alignment,
                fontSize: 9.0);

            var headerMarkup = CreateTextMarkup(
                column.Header,
                headerAnchor,
                layerId,
                resolvedGroupId,
                fontSize: 9.0,
                strokeColor: "#FF111827",
                subject: "Table Header",
                label: column.Header,
                align: ToTextAlign(column.Alignment),
                bold: true);
            SetStructuredTextMetadata(headerMarkup, annotationKind, TextRoleHeader, column.Header, columnIndex: i);
            markups.Add(headerMarkup);

            currentX += column.Width;
            if (i < table.Columns.Count - 1)
            {
                markups.Add(CreateLineMarkup(
                    new Point(currentX, headerRect.Y),
                    new Point(currentX, totalRect.Bottom),
                    layerId,
                    resolvedGroupId,
                    strokeColor: "#FF6B7280",
                    strokeWidth: 0.9,
                    subject: "Table Divider",
                    label: table.Title));
            }
        }

        markups.Add(CreateLineMarkup(
            new Point(origin.X, headerRect.Bottom),
            new Point(totalRect.Right, headerRect.Bottom),
            layerId,
            resolvedGroupId,
            strokeColor: "#FF374151",
            strokeWidth: 1.0,
            subject: "Table Divider",
            label: table.Title));

        for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var rowTop = dataTop + rowIndex * table.RowHeight;
            var rowBottom = rowTop + table.RowHeight;

            markups.Add(CreateLineMarkup(
                new Point(origin.X, rowBottom),
                new Point(totalRect.Right, rowBottom),
                layerId,
                resolvedGroupId,
                strokeColor: "#FFD1D5DB",
                strokeWidth: 0.7,
                subject: "Table Divider",
                label: table.Title));

            currentX = origin.X;
            var row = table.Rows[rowIndex];
            for (int colIndex = 0; colIndex < table.Columns.Count; colIndex++)
            {
                var column = table.Columns[colIndex];
                var cellRect = new Rect(currentX, rowTop, column.Width, table.RowHeight);
                var value = colIndex < row.Length ? row[colIndex] ?? string.Empty : string.Empty;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    var cellMarkup = CreateTextMarkup(
                        value,
                        GetCellTextAnchor(cellRect, column.Alignment, fontSize: 8.6),
                        layerId,
                        resolvedGroupId,
                        fontSize: 8.6,
                        strokeColor: "#FF111827",
                        subject: "Table Cell",
                        label: column.Header,
                        align: ToTextAlign(column.Alignment));
                    SetStructuredTextMetadata(cellMarkup, annotationKind, TextRoleCell, column.Header, rowIndex, colIndex);
                    markups.Add(cellMarkup);
                }

                currentX += column.Width;
            }
        }

        if (!string.IsNullOrWhiteSpace(liveScheduleInstanceId))
        {
            foreach (var markup in markups)
                markup.Metadata.CustomFields[LiveScheduleInstanceIdField] = liveScheduleInstanceId;
        }

        return markups;
    }

    public IReadOnlyList<MarkupRecord> CreateSymbolLegendMarkups(
        SymbolLegend legend,
        Point origin,
        string layerId = DefaultLayerId)
    {
        var library = new ElectricalSymbolLibrary();
        var table = new ScheduleTable
        {
            Title = legend.Title,
            TitleHeight = legend.TitleHeight,
            RowHeight = legend.RowHeight,
            Columns =
            {
                new ScheduleColumn
                {
                    Header = "SYMBOL",
                    Width = legend.SymbolColumnWidth,
                    Alignment = HorizontalAlignment.Center
                },
                new ScheduleColumn
                {
                    Header = "NAME",
                    Width = legend.NameColumnWidth,
                    Alignment = HorizontalAlignment.Left
                },
                new ScheduleColumn
                {
                    Header = "DESCRIPTION",
                    Width = legend.DescriptionColumnWidth,
                    Alignment = HorizontalAlignment.Left
                },
                new ScheduleColumn
                {
                    Header = "COUNT",
                    Width = 50,
                    Alignment = HorizontalAlignment.Center
                }
            }
        };

        foreach (var entry in legend.Entries)
        {
            table.Rows.Add(new[]
            {
                string.Empty,
                entry.SymbolName,
                entry.Description,
                entry.Count > 0 ? entry.Count.ToString(CultureInfo.InvariantCulture) : "-"
            });
        }

        var markups = CreateScheduleTableMarkups(table, origin, layerId, SymbolLegendAnnotationKind, groupId: null, liveScheduleInstanceId: null).ToList();
        var groupId = markups
            .Select(m => GetCustomField(m, AnnotationGroupIdField))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? Guid.NewGuid().ToString("N");

        var symbolColumnWidth = legend.SymbolColumnWidth;
        var firstDataRowTop = origin.Y + table.TitleHeight + table.RowHeight;

        for (int i = 0; i < legend.Entries.Count; i++)
        {
            var entry = legend.Entries[i];
            var symbol = entry.SymbolDefinition ?? library.GetSymbol(entry.SymbolName);
            if (symbol is null)
                continue;

            var cellRect = new Rect(
                origin.X,
                firstDataRowTop + i * table.RowHeight,
                symbolColumnWidth,
                table.RowHeight);

            markups.AddRange(CreateSymbolMarkups(symbol, cellRect, layerId, groupId));
        }

        return markups;
    }

    public IReadOnlyList<MarkupRecord> CreateTitleBlockMarkups(
        TitleBlockBorderGeometry geometry,
        Point origin,
        string layerId = DefaultLayerId,
        double pointsPerInch = PdfPointsPerInch)
    {
        var markups = new List<MarkupRecord>();
        var groupId = Guid.NewGuid().ToString("N");

        var outerRect = ScaleRect(geometry.OuterBorder, origin, pointsPerInch);
        var innerRect = ScaleRect(geometry.InnerBorder, origin, pointsPerInch);
        var titleBlockRect = ScaleRect(geometry.TitleBlockRect, origin, pointsPerInch);

        markups.Add(CreateRectangleMarkup(
            outerRect,
            layerId,
            groupId,
            strokeColor: "#FF111827",
            strokeWidth: 1.5,
            fillColor: null,
            subject: "Sheet Border",
            label: "Outer Border"));

        markups.Add(CreateRectangleMarkup(
            innerRect,
            layerId,
            groupId,
            strokeColor: "#FF1F2937",
            strokeWidth: 1.0,
            fillColor: null,
            subject: "Sheet Border",
            label: "Inner Border"));

        markups.Add(CreateRectangleMarkup(
            titleBlockRect,
            layerId,
            groupId,
            strokeColor: "#FF374151",
            strokeWidth: 1.0,
            fillColor: null,
            subject: "Title Block",
            label: "Title Block"));

        foreach (var zone in geometry.ZoneMarks)
        {
            var position = ScalePoint(zone.Position, origin, pointsPerInch);
            var tickEnd = GetZoneTickEnd(position, zone, innerRect);
            var labelPoint = GetZoneLabelPoint(position, zone, innerRect);

            markups.Add(CreateLineMarkup(
                position,
                tickEnd,
                layerId,
                groupId,
                strokeColor: "#FF4B5563",
                strokeWidth: 0.9,
                subject: "Zone Mark",
                label: zone.Label));

            var zoneLabelMarkup = CreateTextMarkup(
                zone.Label,
                labelPoint,
                layerId,
                groupId,
                fontSize: 8.0,
                strokeColor: "#FF374151",
                subject: "Zone Label",
                label: zone.Label,
                align: TextAlign.Center,
                bold: true);
            SetStructuredTextMetadata(zoneLabelMarkup, TitleBlockAnnotationKind, TextRoleZoneLabel, zone.Label);
            markups.Add(zoneLabelMarkup);
        }

        foreach (var cell in geometry.TitleBlockCells)
        {
            var rect = ScaleRect(new Rect(cell.X, cell.Y, cell.Width, cell.Height), origin, pointsPerInch);
            markups.Add(CreateRectangleMarkup(
                rect,
                layerId,
                groupId,
                strokeColor: "#FF6B7280",
                strokeWidth: 0.9,
                fillColor: null,
                subject: "Title Block Cell",
                label: cell.Label));

            var labelMarkup = CreateTextMarkup(
                cell.Label,
                new Point(rect.X + 4.0, rect.Y + 10.0),
                layerId,
                groupId,
                fontSize: 6.6,
                strokeColor: "#FF6B7280",
                subject: "Title Block Label",
                label: cell.Label,
                bold: true);
            SetStructuredTextMetadata(labelMarkup, TitleBlockAnnotationKind, TextRoleFieldLabel, cell.Label);
            markups.Add(labelMarkup);

            if (!string.IsNullOrWhiteSpace(cell.Value))
            {
                var valueMarkup = CreateTextMarkup(
                    cell.Value,
                    new Point(rect.X + 4.0, rect.Y + Math.Min(rect.Height - 4.0, 22.0)),
                    layerId,
                    groupId,
                    fontSize: 8.8,
                    strokeColor: "#FF111827",
                    subject: "Title Block Value",
                    label: cell.Label);
                SetStructuredTextMetadata(valueMarkup, TitleBlockAnnotationKind, TextRoleFieldValue, cell.Label);
                markups.Add(valueMarkup);
            }
        }

        return markups;
    }

    public MarkupRecord CreateComponentParameterTagMarkup(
        string componentId,
        ProjectParameterBindingTarget target,
        string label,
        string valueText,
        Point origin,
        string? parameterId = null,
        string layerId = DefaultLayerId)
    {
        var groupId = Guid.NewGuid().ToString("N");
        var markup = CreateTextMarkup(
            string.IsNullOrWhiteSpace(label) ? valueText : $"{label}: {valueText}",
            origin,
            layerId,
            groupId,
            fontSize: 10.5,
            strokeColor: "#FF111827",
            subject: "Component Parameter Tag",
            label: string.IsNullOrWhiteSpace(label) ? target.GetDisplayName() : label,
            bold: true);
        SetStructuredTextMetadata(markup, ComponentParameterTagAnnotationKind, TextRoleFieldValue, string.IsNullOrWhiteSpace(label) ? target.GetDisplayName() : label);
        markup.Metadata.CustomFields[ComponentParameterTagComponentIdField] = componentId;
        markup.Metadata.CustomFields[ComponentParameterTagTargetField] = target.ToString();
        if (!string.IsNullOrWhiteSpace(parameterId))
            markup.Metadata.CustomFields[ComponentParameterTagParameterIdField] = parameterId;

        return markup;
    }

    public static void UpdateTextMarkupText(MarkupRecord markup, string text)
    {
        var anchor = markup.Vertices.Count > 0 ? markup.Vertices[0] : markup.BoundingRect.Location;
        var align = TextAlign.Left;
        if (markup.Metadata.CustomFields.TryGetValue(TextAlignField, out var alignValue) &&
            Enum.TryParse(alignValue, ignoreCase: true, out TextAlign parsedAlign))
        {
            align = parsedAlign;
        }

        markup.TextContent = text;
        markup.BoundingRect = EstimateTextBounds(anchor, text, markup.Appearance.FontSize, align);
        markup.Metadata.ModifiedUtc = DateTime.UtcNow;
    }

    private static IEnumerable<MarkupRecord> CreateSymbolMarkups(
        SymbolDefinition symbol,
        Rect targetCellRect,
        string layerId,
        string groupId)
    {
        var markups = new List<MarkupRecord>();
        var scale = Math.Min(
            (targetCellRect.Width - 8.0) / Math.Max(symbol.Width, 1.0),
            (targetCellRect.Height - 8.0) / Math.Max(symbol.Height, 1.0));
        scale = Math.Max(0.1, scale);

        var center = new Point(
            targetCellRect.X + targetCellRect.Width / 2.0,
            targetCellRect.Y + targetCellRect.Height / 2.0);

        foreach (var primitive in symbol.Primitives)
        {
            switch (primitive.Type)
            {
                case SymbolPrimitiveType.Line when primitive.Points.Count >= 2:
                    markups.Add(CreateLineMarkup(
                        TransformPoint(primitive.Points[0], center, scale),
                        TransformPoint(primitive.Points[1], center, scale),
                        layerId,
                        groupId,
                        strokeColor: "#FF111827",
                        strokeWidth: 1.0,
                        subject: "Legend Symbol",
                        label: symbol.Name));
                    break;

                case SymbolPrimitiveType.Circle when primitive.Points.Count >= 1:
                    markups.Add(CreateCircleMarkup(
                        TransformPoint(primitive.Points[0], center, scale),
                        primitive.Radius * scale,
                        layerId,
                        groupId,
                        strokeColor: "#FF111827",
                        strokeWidth: 1.0,
                        fillColor: primitive.IsFilled ? "#FF111827" : null,
                        subject: "Legend Symbol",
                        label: symbol.Name));
                    break;

                case SymbolPrimitiveType.Arc when primitive.Points.Count >= 1:
                    markups.Add(CreateArcMarkup(
                        TransformPoint(primitive.Points[0], center, scale),
                        primitive.Radius * scale,
                        primitive.StartAngle,
                        primitive.SweepAngle,
                        layerId,
                        groupId,
                        strokeColor: "#FF111827",
                        strokeWidth: 1.0,
                        subject: "Legend Symbol",
                        label: symbol.Name));
                    break;

                case SymbolPrimitiveType.Rectangle when primitive.Points.Count >= 1:
                    var topLeft = TransformPoint(primitive.Points[0], center, scale);
                    var rect = new Rect(
                        topLeft.X,
                        topLeft.Y,
                        primitive.Width * scale,
                        primitive.Height * scale);
                    markups.Add(CreateRectangleMarkup(
                        rect,
                        layerId,
                        groupId,
                        strokeColor: "#FF111827",
                        strokeWidth: 1.0,
                        fillColor: primitive.IsFilled ? "#FF111827" : null,
                        subject: "Legend Symbol",
                        label: symbol.Name));
                    break;

                case SymbolPrimitiveType.Text when primitive.Points.Count >= 1:
                    markups.Add(CreateTextMarkup(
                        primitive.Text,
                        TransformPoint(primitive.Points[0], center, scale),
                        layerId,
                        groupId,
                        fontSize: 7.6,
                        strokeColor: "#FF111827",
                        subject: "Legend Symbol",
                        label: symbol.Name,
                        align: TextAlign.Center,
                        bold: true));
                    break;

                case SymbolPrimitiveType.Polyline when primitive.Points.Count >= 2:
                    var transformed = primitive.Points.Select(point => TransformPoint(point, center, scale)).ToList();
                    var isClosed = transformed.Count >= 3 && transformed.First() == transformed.Last();
                    markups.Add(CreatePolylineMarkup(
                        transformed,
                        layerId,
                        groupId,
                        strokeColor: "#FF111827",
                        strokeWidth: 1.0,
                        fillColor: primitive.IsFilled || isClosed ? "#14000000" : null,
                        closed: isClosed,
                        subject: "Legend Symbol",
                        label: symbol.Name));
                    break;
            }
        }

        return markups;
    }

    private static MarkupRecord CreateRectangleMarkup(
        Rect rect,
        string layerId,
        string groupId,
        string strokeColor,
        double strokeWidth,
        string? fillColor,
        string subject,
        string label)
    {
        return CreateMarkup(
            MarkupType.Rectangle,
            layerId,
            groupId,
            strokeColor,
            strokeWidth,
            fillColor,
            subject,
            label,
            new[] { new Point(rect.Left, rect.Top), new Point(rect.Right, rect.Bottom) },
            markup => markup.BoundingRect = rect);
    }

    private static MarkupRecord CreateCircleMarkup(
        Point center,
        double radius,
        string layerId,
        string groupId,
        string strokeColor,
        double strokeWidth,
        string? fillColor,
        string subject,
        string label)
    {
        return CreateMarkup(
            MarkupType.Circle,
            layerId,
            groupId,
            strokeColor,
            strokeWidth,
            fillColor,
            subject,
            label,
            new[] { center },
            markup => markup.Radius = radius);
    }

    private static MarkupRecord CreateArcMarkup(
        Point center,
        double radius,
        double startAngle,
        double sweepAngle,
        string layerId,
        string groupId,
        string strokeColor,
        double strokeWidth,
        string subject,
        string label)
    {
        return CreateMarkup(
            MarkupType.Arc,
            layerId,
            groupId,
            strokeColor,
            strokeWidth,
            fillColor: null,
            subject,
            label,
            new[] { center },
            markup =>
            {
                markup.Radius = radius;
                markup.ArcStartDeg = startAngle;
                markup.ArcSweepDeg = sweepAngle;
            });
    }

    private static MarkupRecord CreateLineMarkup(
        Point start,
        Point end,
        string layerId,
        string groupId,
        string strokeColor,
        double strokeWidth,
        string subject,
        string label)
    {
        return CreatePolylineMarkup(
            new[] { start, end },
            layerId,
            groupId,
            strokeColor,
            strokeWidth,
            fillColor: null,
            closed: false,
            subject,
            label);
    }

    private static MarkupRecord CreatePolylineMarkup(
        IEnumerable<Point> points,
        string layerId,
        string groupId,
        string strokeColor,
        double strokeWidth,
        string? fillColor,
        bool closed,
        string subject,
        string label)
    {
        var list = points.ToList();
        return CreateMarkup(
            closed ? MarkupType.Polygon : MarkupType.Polyline,
            layerId,
            groupId,
            strokeColor,
            strokeWidth,
            fillColor,
            subject,
            label,
            list.ToArray());
    }

    private static MarkupRecord CreateTextMarkup(
        string text,
        Point anchor,
        string layerId,
        string groupId,
        double fontSize,
        string strokeColor,
        string subject,
        string label,
        TextAlign align = TextAlign.Left,
        bool bold = false)
    {
        return CreateMarkup(
            MarkupType.Text,
            layerId,
            groupId,
            strokeColor,
            strokeWidth: 0.0,
            fillColor: null,
            subject,
            label,
            new[] { anchor },
            markup =>
            {
                markup.TextContent = text;
                markup.Appearance.FontSize = fontSize;
                markup.Metadata.CustomFields[TextAlignField] = align.ToString();
                markup.Metadata.CustomFields[BoldField] = bold ? bool.TrueString : bool.FalseString;
                markup.BoundingRect = EstimateTextBounds(anchor, text, fontSize, align);
            });
    }

    private static MarkupRecord CreateMarkup(
        MarkupType type,
        string layerId,
        string groupId,
        string strokeColor,
        double strokeWidth,
        string? fillColor,
        string subject,
        string label,
        params Point[] vertices)
    {
        return CreateMarkup(
            type,
            layerId,
            groupId,
            strokeColor,
            strokeWidth,
            fillColor,
            subject,
            label,
            vertices,
            configure: null);
    }

    private static MarkupRecord CreateMarkup(
        MarkupType type,
        string layerId,
        string groupId,
        string strokeColor,
        double strokeWidth,
        string? fillColor,
        string subject,
        string label,
        IReadOnlyList<Point> vertices,
        Action<MarkupRecord>? configure)
    {
        var markup = new MarkupRecord
        {
            Type = type,
            LayerId = layerId,
            Appearance = new MarkupAppearance
            {
                StrokeColor = strokeColor,
                StrokeWidth = strokeWidth,
                FillColor = fillColor ?? "#00000000",
                FontFamily = "Segoe UI"
            },
            Metadata = new MarkupMetadata
            {
                Label = label,
                Subject = subject,
                CustomFields =
                {
                    [AnnotationGroupIdField] = groupId
                }
            }
        };

        markup.Vertices.AddRange(vertices);
        configure?.Invoke(markup);
        if (markup.BoundingRect == Rect.Empty)
            markup.UpdateBoundingRect();
        return markup;
    }

    private static void SetStructuredTextMetadata(
        MarkupRecord markup,
        string annotationKind,
        string textRole,
        string? textKey = null,
        int? rowIndex = null,
        int? columnIndex = null)
    {
        markup.Metadata.CustomFields[AnnotationKindField] = annotationKind;
        markup.Metadata.CustomFields[AnnotationTextRoleField] = textRole;

        if (!string.IsNullOrWhiteSpace(textKey))
            markup.Metadata.CustomFields[AnnotationTextKeyField] = textKey;

        if (rowIndex.HasValue)
            markup.Metadata.CustomFields[AnnotationRowIndexField] = rowIndex.Value.ToString(CultureInfo.InvariantCulture);

        if (columnIndex.HasValue)
            markup.Metadata.CustomFields[AnnotationColumnIndexField] = columnIndex.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static Point GetCellTextAnchor(Rect rect, HorizontalAlignment alignment, double fontSize)
    {
        return alignment switch
        {
            HorizontalAlignment.Center => new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height * 0.68),
            HorizontalAlignment.Right => new Point(rect.Right - 4.0, rect.Y + rect.Height * 0.68),
            _ => new Point(rect.X + 4.0, rect.Y + rect.Height * 0.68)
        };
    }

    private static TextAlign ToTextAlign(HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.Center => TextAlign.Center,
        HorizontalAlignment.Right => TextAlign.Right,
        _ => TextAlign.Left
    };

    private static Rect EstimateTextBounds(Point anchor, string text, double fontSize, TextAlign align)
    {
        var width = Math.Max(fontSize, text.Length * fontSize * 0.55);
        var height = fontSize * 1.35;
        var x = align switch
        {
            TextAlign.Center => anchor.X - width / 2.0,
            TextAlign.Right => anchor.X - width,
            _ => anchor.X
        };

        return new Rect(x, anchor.Y - height, width, height);
    }

    private static Point TransformPoint(Point point, Point center, double scale)
    {
        return new Point(center.X + point.X * scale, center.Y + point.Y * scale);
    }

    private static Rect ScaleRect(Rect rect, Point origin, double factor)
    {
        return new Rect(
            origin.X + rect.X * factor,
            origin.Y + rect.Y * factor,
            rect.Width * factor,
            rect.Height * factor);
    }

    private static Point ScalePoint(Point point, Point origin, double factor)
    {
        return new Point(origin.X + point.X * factor, origin.Y + point.Y * factor);
    }

    private static Point GetZoneTickEnd(Point position, ZoneMark zone, Rect innerRect)
    {
        const double tickLength = 12.0;
        if (zone.IsHorizontal)
        {
            var isTop = Math.Abs(position.Y - innerRect.Top) < Math.Abs(position.Y - innerRect.Bottom);
            return new Point(position.X, position.Y + (isTop ? -tickLength : tickLength));
        }

        var isLeft = Math.Abs(position.X - innerRect.Left) < Math.Abs(position.X - innerRect.Right);
        return new Point(position.X + (isLeft ? -tickLength : tickLength), position.Y);
    }

    private static Point GetZoneLabelPoint(Point position, ZoneMark zone, Rect innerRect)
    {
        const double labelOffset = 20.0;
        if (zone.IsHorizontal)
        {
            var isTop = Math.Abs(position.Y - innerRect.Top) < Math.Abs(position.Y - innerRect.Bottom);
            return new Point(position.X, position.Y + (isTop ? -labelOffset : labelOffset));
        }

        var isLeft = Math.Abs(position.X - innerRect.Left) < Math.Abs(position.X - innerRect.Right);
        return new Point(position.X + (isLeft ? -labelOffset : labelOffset), position.Y + 3.0);
    }

    private static string? GetCustomField(MarkupRecord markup, string key)
    {
        return markup.Metadata.CustomFields.TryGetValue(key, out var value) ? value : null;
    }
}
