using System.Windows;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests.ViewModels;

public partial class MainViewModelTests
{
    [Fact]
    public void Constructor_InitializesLibraryComponents()
    {
        var vm = new MainViewModel();
        var expectedTemplates = ElectricalComponentCatalog.CreateLibraryTemplates();

        Assert.Equal(expectedTemplates.Count, vm.LibraryComponents.Count);
        foreach (var template in expectedTemplates)
        {
            Assert.Contains(vm.LibraryComponents, c => c.Type == template.Type && c.Name == template.Name);
        }
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Conduit);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Box);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Panel);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Support);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.CableTray);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Hanger);
    }

    [Fact]
    public void Constructor_ComponentsIsEmpty()
    {
        var vm = new MainViewModel();

        Assert.Empty(vm.Components);
    }

    [Fact]
    public void Constructor_DefaultSettings()
    {
        var vm = new MainViewModel();

        Assert.True(vm.ShowGrid);
        Assert.True(vm.SnapToGrid);
        Assert.Equal(1.0, vm.GridSize);
        Assert.Null(vm.SelectedComponent);
    }

    [Fact]
    public void SelectedComponent_PropertyChanged_Fires()
    {
        var vm = new MainViewModel();
        var propertyName = string.Empty;
        vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

        vm.AddComponent(ComponentType.Conduit);

        Assert.Equal(nameof(vm.SelectedComponent), propertyName);
    }

    [Fact]
    public void ShowGrid_PropertyChanged_Fires()
    {
        var vm = new MainViewModel();
        var propertyName = string.Empty;
        vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

        vm.ShowGrid = false;

        Assert.Equal(nameof(vm.ShowGrid), propertyName);
    }

    [Fact]
    public void SnapToGrid_PropertyChanged_Fires()
    {
        var vm = new MainViewModel();
        var propertyName = string.Empty;
        vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

        vm.SnapToGrid = false;

        Assert.Equal(nameof(vm.SnapToGrid), propertyName);
    }

    [Fact]
    public void GridSize_PropertyChanged_Fires()
    {
        var vm = new MainViewModel();
        var propertyName = string.Empty;
        vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

        vm.GridSize = 5.0;

        Assert.Equal(nameof(vm.GridSize), propertyName);
    }

    [Fact]
    public void GridSize_SetToZero_ClampsToMinimum()
    {
        var vm = new MainViewModel();

        vm.GridSize = 0;

        Assert.Equal(0.1, vm.GridSize);
    }

    [Fact]
    public void FileService_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.FileService);
    }

    [Fact]
    public void ProjectFileService_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.ProjectFileService);
    }

    [Fact]
    public void BomExport_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.BomExport);
    }

    [Fact]
    public void SnapService_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.SnapService);
    }

    [Fact]
    public void CalibrationService_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.CalibrationService);
    }

    [Fact]
    public void PdfUnderlay_Default_IsNull()
    {
        var vm = new MainViewModel();

        Assert.Null(vm.PdfUnderlay);
    }

    [Fact]
    public void PdfUnderlay_CanBeSet()
    {
        var vm = new MainViewModel();

        vm.PdfUnderlay = new PdfUnderlay
        {
            FilePath = @"C:\Plans\floor1.pdf",
            PageNumber = 2
        };

        Assert.NotNull(vm.PdfUnderlay);
        Assert.Equal(@"C:\Plans\floor1.pdf", vm.PdfUnderlay.FilePath);
        Assert.Equal(2, vm.PdfUnderlay.PageNumber);
    }
}