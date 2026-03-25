using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using Newtonsoft.Json;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Provides Cut/Copy/Paste operations for electrical components.
/// Serializes components to JSON for clipboard storage, supporting
/// offset pasting and multi-component operations.
/// </summary>
public class ClipboardService
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Objects,
        Formatting = Formatting.None
    };

    private List<ClipboardEntry> _buffer = new();
    private Point3D _basePoint;
    private ClipboardOperation _lastOperation;

    public bool HasContent => _buffer.Count > 0;
    public int Count => _buffer.Count;
    public ClipboardOperation LastOperation => _lastOperation;

    /// <summary>
    /// Copies components to the internal clipboard buffer.
    /// </summary>
    public void Copy(IReadOnlyList<ElectricalComponent> components, Point3D? basePoint = null)
    {
        if (components.Count == 0) return;

        _buffer.Clear();
        _basePoint = basePoint ?? components[0].Position;
        _lastOperation = ClipboardOperation.Copy;

        foreach (var comp in components)
        {
            var json = JsonConvert.SerializeObject(comp, JsonSettings);
            _buffer.Add(new ClipboardEntry
            {
                Json = json,
                OriginalId = comp.Id,
                RelativePosition = new Vector3D(
                    comp.Position.X - _basePoint.X,
                    comp.Position.Y - _basePoint.Y,
                    comp.Position.Z - _basePoint.Z)
            });
        }
    }

    /// <summary>
    /// Cuts components: copies them and returns the originals for removal.
    /// The caller is responsible for removing from the collection via undo actions.
    /// </summary>
    public IReadOnlyList<ElectricalComponent> Cut(
        IReadOnlyList<ElectricalComponent> components, Point3D? basePoint = null)
    {
        Copy(components, basePoint);
        _lastOperation = ClipboardOperation.Cut;
        return components;
    }

    /// <summary>
    /// Pastes components at the specified insertion point.
    /// Each pasted component gets a new unique Id.
    /// </summary>
    public List<ElectricalComponent> Paste(Point3D insertionPoint)
    {
        var results = new List<ElectricalComponent>();

        foreach (var entry in _buffer)
        {
            var comp = JsonConvert.DeserializeObject<ElectricalComponent>(entry.Json, JsonSettings);
            if (comp == null) continue;

            comp.Id = Guid.NewGuid().ToString();
            comp.Position = new Point3D(
                insertionPoint.X + entry.RelativePosition.X,
                insertionPoint.Y + entry.RelativePosition.Y,
                insertionPoint.Z + entry.RelativePosition.Z);

            results.Add(comp);
        }

        return results;
    }

    /// <summary>
    /// Pastes with an offset from the original base point.
    /// Useful for "Paste in Place" with a displacement.
    /// </summary>
    public List<ElectricalComponent> PasteWithOffset(Vector3D offset)
    {
        var point = new Point3D(
            _basePoint.X + offset.X,
            _basePoint.Y + offset.Y,
            _basePoint.Z + offset.Z);
        return Paste(point);
    }

    /// <summary>
    /// Pastes in the original position (duplicate in place).
    /// Applies a small default offset so components are visually distinct.
    /// </summary>
    public List<ElectricalComponent> PasteInPlace(double defaultOffset = 1.0)
    {
        return PasteWithOffset(new Vector3D(defaultOffset, 0, defaultOffset));
    }

    /// <summary>
    /// Clears the clipboard buffer.
    /// </summary>
    public void Clear()
    {
        _buffer.Clear();
        _lastOperation = ClipboardOperation.None;
    }
}

public enum ClipboardOperation
{
    None,
    Copy,
    Cut
}

internal class ClipboardEntry
{
    public string Json { get; set; } = string.Empty;
    public string OriginalId { get; set; } = string.Empty;
    public Vector3D RelativePosition { get; set; }
}
