using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class ClipboardServiceTests
{
    [Fact]
    public void Copy_SetsHasContentTrue()
    {
        var sut = new ClipboardService();
        var components = new[] { CreateComponent("Box 1", 0, 0, 0) };

        sut.Copy(components);

        Assert.True(sut.HasContent);
        Assert.Equal(1, sut.Count);
        Assert.Equal(ClipboardOperation.Copy, sut.LastOperation);
    }

    [Fact]
    public void Copy_PreservesCount()
    {
        var sut = new ClipboardService();
        var components = new[]
        {
            CreateComponent("Box 1", 0, 0, 0),
            CreateComponent("Box 2", 5, 0, 5)
        };

        sut.Copy(components);

        Assert.Equal(2, sut.Count);
    }

    [Fact]
    public void Paste_CreatesNewIds()
    {
        var sut = new ClipboardService();
        var original1 = CreateComponent("Box 1", 0, 0, 0);
        var original2 = CreateComponent("Box 2", 5, 0, 5);

        sut.Copy(new[] { original1, original2 });
        var pasted = sut.Paste(new Point3D(10, 2, 3));

        Assert.Equal(2, pasted.Count);
        Assert.All(pasted, item =>
        {
            Assert.NotEqual(original1.Id, item.Id);
            Assert.NotEqual(original2.Id, item.Id);
        });
        Assert.Equal(2, pasted.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public void Paste_AtInsertionPoint()
    {
        var sut = new ClipboardService();
        var source = CreateComponent("Box 1", 0, 0, 0);

        sut.Copy(new[] { source });
        var inserted = new Point3D(10, 2, 3);
        var pasted = sut.Paste(inserted);

        Assert.Single(pasted);
        Assert.Equal(inserted.X, pasted[0].Position.X);
        Assert.Equal(inserted.Y, pasted[0].Position.Y);
        Assert.Equal(inserted.Z, pasted[0].Position.Z);
    }

    [Fact]
    public void PasteInPlace_OffsetsFromOriginal()
    {
        var sut = new ClipboardService();
        var source = CreateComponent("Box 1", 0, 0, 0);

        sut.Copy(new[] { source });
        var pasted = sut.PasteInPlace(1.0);

        Assert.Single(pasted);
        Assert.Equal(1.0, pasted[0].Position.X);
        Assert.Equal(0.0, pasted[0].Position.Y);
        Assert.Equal(1.0, pasted[0].Position.Z);
    }

    [Fact]
    public void Cut_SetsOperationToCut()
    {
        var sut = new ClipboardService();
        var components = new[] { CreateComponent("Box 1", 0, 0, 0) };

        var cut = sut.Cut(components);

        Assert.Single(cut);
        Assert.Equal(ClipboardOperation.Cut, sut.LastOperation);
        Assert.True(sut.HasContent);
    }

    [Fact]
    public void Clear_RemovesContent()
    {
        var sut = new ClipboardService();
        var components = new[] { CreateComponent("Box 1", 0, 0, 0) };

        sut.Copy(components);
        sut.Clear();

        Assert.False(sut.HasContent);
        Assert.Equal(0, sut.Count);
        Assert.Equal(ClipboardOperation.None, sut.LastOperation);
    }

    [Fact]
    public void PasteWithOffset_AppliesOffset()
    {
        var sut = new ClipboardService();
        var source = CreateComponent("Box 1", 0, 0, 0);

        sut.Copy(new[] { source });
        var pasted = sut.PasteWithOffset(new Vector3D(2, 0, -3));

        Assert.Single(pasted);
        Assert.Equal(2.0, pasted[0].Position.X);
        Assert.Equal(0.0, pasted[0].Position.Y);
        Assert.Equal(-3.0, pasted[0].Position.Z);
    }

    private static ElectricalComponent CreateComponent(string name, double x, double y, double z)
    {
        var component = ElectricalComponentCatalog.CreateDefaultComponent(ComponentType.Box);
        component.Name = name;
        component.Position = new Point3D(x, y, z);
        return component;
    }
}
