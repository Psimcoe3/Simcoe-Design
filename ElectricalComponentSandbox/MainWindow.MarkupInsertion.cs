using System.Linq;
using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private readonly DrawingAnnotationMarkupService _drawingAnnotationMarkupService = new();

    private bool TryPromptForDocumentPoint(string title, string prompt, string defaultValue, out Point point)
    {
        point = default;
        var input = PromptInput(title, prompt, defaultValue);
        if (input == null)
            return false;

        if (!TryParsePoint(input, out var x, out var y))
        {
            MessageBox.Show("Invalid point. Use format: X, Y", title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        point = new Point(x, y);
        return true;
    }

    private void InsertGeneratedMarkups(
        IReadOnlyList<MarkupRecord> markups,
        string transactionName,
        string logAction,
        string logDetails,
        bool selectLastMarkup = true)
    {
        if (markups.Count == 0)
            return;

        var actions = markups
            .Select(markup => (IUndoableAction)new ViewModelMarkupAddAction(_viewModel, markup))
            .ToList();

        _viewModel.UndoRedo.Execute(new CompositeAction(transactionName, actions));

        if (selectLastMarkup)
            _viewModel.MarkupTool.SelectedMarkup = markups[^1];

        QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        ActionLogService.Instance.Log(LogCategory.Edit, logAction, logDetails);
    }
}

internal sealed class ViewModelMarkupAddAction : IUndoableAction
{
    private readonly MainViewModel _viewModel;
    private readonly MarkupRecord _markup;

    public ViewModelMarkupAddAction(MainViewModel viewModel, MarkupRecord markup)
    {
        _viewModel = viewModel;
        _markup = markup;
    }

    public string Description => $"Add {_markup.TypeDisplayText}";

    public void Execute() => _viewModel.AddMarkup(_markup);

    public void Undo() => _viewModel.RemoveMarkup(_markup.Id);
}

internal sealed class ViewModelLiveScheduleAddAction : IUndoableAction
{
    private readonly MainViewModel _viewModel;
    private readonly DrawingSheet _sheet;
    private readonly LiveScheduleInstance _instance;

    public ViewModelLiveScheduleAddAction(MainViewModel viewModel, DrawingSheet sheet, LiveScheduleInstance instance)
    {
        _viewModel = viewModel;
        _sheet = sheet;
        _instance = instance;
    }

    public string Description => $"Insert {_instance.DisplayName}";

    public void Execute() => _viewModel.AddLiveScheduleInstance(_sheet, _instance);

    public void Undo() => _viewModel.RemoveLiveScheduleInstance(_sheet, _instance.Id);
}
