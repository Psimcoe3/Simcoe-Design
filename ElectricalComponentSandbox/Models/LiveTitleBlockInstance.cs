using System.Windows;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Models;

public sealed class LiveTitleBlockInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public Point Origin { get; set; }

    public string LayerId { get; set; } = "markup-default";

    public string GroupId { get; set; } = Guid.NewGuid().ToString("N");

    public TitleBlockTemplate Template { get; set; } = new()
    {
        Name = "Live Title Block",
        PaperSize = PaperSizeType.ANSI_B,
        BorderMargin = 0.5,
        TitleBlockHeight = 1.5,
        RevisionHistoryRows = 5,
        Scale = "AS NOTED"
    };

    public string DisplayName => $"Title Block - {Template.PaperSize}";
}