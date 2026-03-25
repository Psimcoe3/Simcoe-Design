using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private readonly ClipboardService _clipboardService = new();

    // ── Cut / Copy / Paste ───────────────────────────────────────────────────

    private void CopyComponent_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedComponents();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select one or more components first.", "Copy",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _clipboardService.Copy(selected);
        ActionLogService.Instance.Log(LogCategory.Edit, "Copied components",
            $"Count: {selected.Count}");
    }

    private void CutComponent_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedComponents();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select one or more components first.", "Cut",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var originals = _clipboardService.Cut(selected);

        var actions = originals.Select(c =>
            (IUndoableAction)new RemoveComponentAction(_viewModel.Components, c)).ToList();
        var composite = new CompositeAction($"Cut {originals.Count} component(s)", actions);
        _viewModel.UndoRedo.Execute(composite);

        _viewModel.SelectedComponent = null;
        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, "Cut components",
            $"Count: {originals.Count}");
    }

    private void PasteComponent_Click(object sender, RoutedEventArgs e)
    {
        if (!_clipboardService.HasContent)
        {
            MessageBox.Show("Nothing on the clipboard.", "Paste",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var posStr = PromptInput("Paste", "Insertion point (X, Y, Z):", "0, 0, 0");
        if (posStr == null) return;

        var parts = posStr.Split(',');
        if (parts.Length < 2) return;
        if (!double.TryParse(parts[0].Trim(), out double x)) return;
        if (!double.TryParse(parts[1].Trim(), out double y)) return;
        double z = parts.Length >= 3 && double.TryParse(parts[2].Trim(), out double zv) ? zv : 0;

        var pasted = _clipboardService.Paste(new Point3D(x, y, z));
        ExecutePasteActions(pasted);
    }

    private void PasteInPlace_Click(object sender, RoutedEventArgs e)
    {
        if (!_clipboardService.HasContent)
        {
            MessageBox.Show("Nothing on the clipboard.", "Paste in Place",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var pasted = _clipboardService.PasteInPlace();
        ExecutePasteActions(pasted);
    }

    private void ExecutePasteActions(List<ElectricalComponent> pasted)
    {
        if (pasted.Count == 0) return;

        var actions = pasted.Select(c =>
            (IUndoableAction)new AddComponentAction(_viewModel.Components, c)).ToList();
        var composite = new CompositeAction($"Paste {pasted.Count} component(s)", actions);
        _viewModel.UndoRedo.Execute(composite);

        // Select the pasted components
        _viewModel.SelectedComponentIds.Clear();
        foreach (var c in pasted)
            _viewModel.SelectedComponentIds.Add(c.Id);
        if (pasted.Count == 1)
            _viewModel.SelectedComponent = pasted[0];

        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, "Pasted components",
            $"Count: {pasted.Count}");
    }

    // ── Symbol Legend ─────────────────────────────────────────────────────────

    private void InsertSymbolLegend_Click(object sender, RoutedEventArgs e)
    {
        var library = new ElectricalSymbolLibrary();
        var legendService = new SymbolLegendService();
        var legend = legendService.GenerateLegend(_viewModel.Components.ToList(), library);

        if (legend.Entries.Count == 0)
        {
            MessageBox.Show("No components placed in the drawing to generate a legend from.",
                "Symbol Legend", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryPromptForDocumentPoint(
                "Insert Symbol Legend",
                "Top-left insertion point (X, Y document units):",
                "120, 420",
                out var origin))
        {
            return;
        }

        var markups = _drawingAnnotationMarkupService.CreateSymbolLegendMarkups(legend, origin);
        InsertGeneratedMarkups(
            markups,
            "Insert symbol legend",
            "Inserted symbol legend",
            $"Entries: {legend.Entries.Count}");
    }

    // ── Title Block ──────────────────────────────────────────────────────────

    private void InsertTitleBlock_Click(object sender, RoutedEventArgs e)
    {
        var sizeOptions = string.Join("\n", Enum.GetValues<PaperSizeType>()
            .Select((s, i) => $"{i + 1}. {s}"));
        var input = PromptInput("Title Block",
            $"Select paper size:\n\n{sizeOptions}", "2");
        if (!int.TryParse(input, out int idx)) return;
        var sizes = Enum.GetValues<PaperSizeType>();
        if (idx < 1 || idx > sizes.Length) return;

        var paperSize = sizes[idx - 1];
        var service = new TitleBlockService();
        var template = service.GetDefaultTemplate(paperSize);

        // Fill template with project info
        template.ProjectName = "Untitled Project";
        template.DrawnBy = Environment.UserName;
        template.Date = DateTime.Now.ToString("MM/dd/yyyy");

        var geometry = service.GenerateBorderGeometry(template);

        if (!TryPromptForDocumentPoint(
                "Insert Title Block",
                "Sheet origin (top-left) in document units (X, Y):",
                "72, 72",
                out var origin))
        {
            return;
        }

        var markups = _drawingAnnotationMarkupService.CreateTitleBlockMarkups(geometry, origin);
        InsertGeneratedMarkups(
            markups,
            "Insert title block",
            "Inserted title block",
            $"Paper: {paperSize}, Size: {template.PaperWidth}x{template.PaperHeight}");
    }

    // ── Offset Conduit ───────────────────────────────────────────────────────

    private void OffsetConduit_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent is not ConduitComponent conduit)
        {
            MessageBox.Show("Select a conduit component first.", "Offset Conduit",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var distStr = PromptInput("Offset Conduit",
            "Enter offset distance (positive = left, negative = right):", "1.0");
        if (!double.TryParse(distStr, out double dist) || Math.Abs(dist) < 0.001) return;

        var direction = dist >= 0 ? OffsetDirection.Left : OffsetDirection.Right;
        var newConduit = OffsetService.CreateParallelConduit(conduit, Math.Abs(dist), direction);
        newConduit.LayerId = conduit.LayerId;

        _viewModel.UndoRedo.Execute(new AddComponentAction(_viewModel.Components, newConduit));
        _viewModel.SelectedComponent = newConduit;
        QueueSceneRefresh(update2D: true, update3D: true, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, "Offset conduit created",
            $"Distance: {dist:F3}, Direction: {direction}");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private List<ElectricalComponent> GetSelectedComponents()
    {
        if (_viewModel.SelectedComponentIds.Count > 0)
        {
            return _viewModel.Components
                .Where(c => _viewModel.SelectedComponentIds.Contains(c.Id))
                .ToList();
        }

        if (_viewModel.SelectedComponent != null)
            return new List<ElectricalComponent> { _viewModel.SelectedComponent };

        return new List<ElectricalComponent>();
    }
}
