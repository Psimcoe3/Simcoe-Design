using System.Windows;

namespace ElectricalComponentSandbox.Models;

public enum LiveScheduleKind
{
    Equipment,
    Conduit,
    CircuitSummary,
    ProjectParameter
}

public sealed class LiveScheduleInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public LiveScheduleKind Kind { get; set; }

    public Point Origin { get; set; }

    public string LayerId { get; set; } = "markup-default";

    public string GroupId { get; set; } = Guid.NewGuid().ToString("N");

    public string DisplayName => Kind switch
    {
        LiveScheduleKind.Equipment => "Equipment Schedule",
        LiveScheduleKind.Conduit => "Conduit Schedule",
        LiveScheduleKind.CircuitSummary => "Circuit Summary",
        LiveScheduleKind.ProjectParameter => "Project Parameter Schedule",
        _ => "Schedule"
    };
}