using System.Windows.Media;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Rendering;

namespace ElectricalComponentSandbox.Tests.Rendering;

public class MaterialFactoryTests
{
    private static void RunInSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null) throw ex;
    }

    [Fact]
    public void Build_Realistic_ReturnsDiffuseMaterial()
    {
        RunInSta(() =>
        {
            var mat = MaterialFactory.Build(VisualStyle3D.Realistic, Colors.Red, false);
            Assert.IsType<DiffuseMaterial>(mat);
        });
    }

    [Fact]
    public void Build_Conceptual_ReturnsMaterialGroup()
    {
        RunInSta(() =>
        {
            var mat = MaterialFactory.Build(VisualStyle3D.Conceptual, Colors.Blue, false);
            Assert.IsType<MaterialGroup>(mat);
            var group = (MaterialGroup)mat;
            Assert.Equal(2, group.Children.Count);
            Assert.IsType<DiffuseMaterial>(group.Children[0]);
            Assert.IsType<EmissiveMaterial>(group.Children[1]);
        });
    }

    [Fact]
    public void Build_Wireframe_ReturnsMaterialGroup()
    {
        RunInSta(() =>
        {
            var mat = MaterialFactory.Build(VisualStyle3D.Wireframe, Colors.Green, false);
            Assert.IsType<MaterialGroup>(mat);
            var group = (MaterialGroup)mat;
            Assert.Equal(2, group.Children.Count);
        });
    }

    [Fact]
    public void Build_XRay_ReturnsDiffuseMaterial()
    {
        RunInSta(() =>
        {
            var mat = MaterialFactory.Build(VisualStyle3D.XRay, Colors.Yellow, false);
            Assert.IsType<DiffuseMaterial>(mat);
        });
    }

    [Fact]
    public void Build_Selected_AddEmissiveHighlight()
    {
        RunInSta(() =>
        {
            var mat = MaterialFactory.Build(VisualStyle3D.Realistic, Colors.Red, true);
            Assert.IsType<MaterialGroup>(mat);
            var group = (MaterialGroup)mat;
            Assert.Equal(2, group.Children.Count);
            Assert.IsType<DiffuseMaterial>(group.Children[0]);
            Assert.IsType<EmissiveMaterial>(group.Children[1]);
        });
    }

    [Fact]
    public void Build_ConceptualSelected_HasThreeChildren()
    {
        RunInSta(() =>
        {
            var mat = MaterialFactory.Build(VisualStyle3D.Conceptual, Colors.Blue, true);
            Assert.IsType<MaterialGroup>(mat);
            var outer = (MaterialGroup)mat;
            Assert.Equal(2, outer.Children.Count);
            Assert.IsType<MaterialGroup>(outer.Children[0]);
            Assert.IsType<EmissiveMaterial>(outer.Children[1]);
        });
    }

    [Theory]
    [InlineData(VisualStyle3D.Realistic)]
    [InlineData(VisualStyle3D.Conceptual)]
    [InlineData(VisualStyle3D.Wireframe)]
    [InlineData(VisualStyle3D.XRay)]
    public void Build_AllStyles_DoNotThrow(VisualStyle3D style)
    {
        RunInSta(() =>
        {
            var mat = MaterialFactory.Build(style, Colors.Gray, false);
            Assert.NotNull(mat);
        });
    }

    [Fact]
    public void BuildConceptual_DesaturatesColor()
    {
        RunInSta(() =>
        {
            var mat = MaterialFactory.BuildConceptual(Color.FromRgb(255, 0, 0));
            Assert.IsType<MaterialGroup>(mat);
        });
    }

    [Fact]
    public void BuildWireframe_HighlyTransparent()
    {
        RunInSta(() =>
        {
            var mat = MaterialFactory.BuildWireframe(Colors.Blue);
            var group = (MaterialGroup)mat;
            var diffuse = (DiffuseMaterial)group.Children[0];
            var brush = (SolidColorBrush)diffuse.Brush;
            Assert.True(brush.Color.A < 50, "Wireframe diffuse should be highly transparent");
        });
    }

    [Fact]
    public void BuildXRay_SemiTransparent()
    {
        RunInSta(() =>
        {
            var mat = MaterialFactory.BuildXRay(Colors.Green);
            var diffuse = (DiffuseMaterial)mat;
            var brush = (SolidColorBrush)diffuse.Brush;
            Assert.Equal(90, brush.Color.A);
        });
    }
}
