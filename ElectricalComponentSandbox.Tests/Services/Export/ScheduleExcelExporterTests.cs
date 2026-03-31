using System.IO;
using ClosedXML.Excel;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.Export;

namespace ElectricalComponentSandbox.Tests.Services.Export;

public class ScheduleExcelExporterTests
{
    [Fact]
    public void ExportSchedule_WithProjectParameters_WritesProjectParameterSheetAndBindingSummary()
    {
        var component = ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box);
        var parameter = new ProjectParameterDefinition
        {
            Id = "width-param",
            Name = "Shared Width",
            Value = 4.25,
            Formula = "2 + 2.25"
        };
        component.Parameters.SetBinding(ProjectParameterBindingTarget.Width, parameter.Id);

        var exportPath = Path.Combine(Path.GetTempPath(), $"schedule-{Guid.NewGuid():N}.xlsx");

        try
        {
            var sut = new ScheduleExcelExporter();

            sut.ExportSchedule(new[] { component }, exportPath, new[] { parameter });

            using var workbook = new XLWorkbook(exportPath);
            var allComponents = workbook.Worksheet("All Components");
            var projectParameters = workbook.Worksheet("Project Parameters");

            Assert.Equal("Parameter Bindings", allComponents.Cell(2, 13).GetString());
            Assert.Contains("W=Shared Width", allComponents.Cell(3, 13).GetString(), StringComparison.OrdinalIgnoreCase);

            Assert.Equal("Name", projectParameters.Cell(2, 1).GetString());
            Assert.Equal("Shared Width", projectParameters.Cell(3, 1).GetString());
            Assert.Equal("2 + 2.25", projectParameters.Cell(3, 3).GetString());
            Assert.Equal("W", projectParameters.Cell(3, 4).GetString());
            Assert.Contains("1 comp", projectParameters.Cell(3, 5).GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("OK", projectParameters.Cell(3, 6).GetString());
        }
        finally
        {
            if (File.Exists(exportPath))
                File.Delete(exportPath);
        }
    }
}