using ElectricalComponentSandbox.Services.RevitIntrospection;

namespace ElectricalComponentSandbox.Tests.Services.RevitIntrospection;

public sealed class GeometryKeywordIndexerTests
{
    [Fact]
    public void BuildGeometryIndex_FindsGeometryNamespacesTypesAndMembers()
    {
        var assemblies = new List<ManagedAssemblyMetadata>
        {
            new()
            {
                FileName = "RevitDB.dll",
                FullPath = @"C:\Fake\RevitDB.dll",
                AssemblyName = "RevitDB",
                Types =
                [
                    new ManagedTypeMetadata(
                        Namespace: "Autodesk.Revit.DB.Geometry",
                        FullTypeName: "Autodesk.Revit.DB.Solid",
                        Methods:
                        [
                            "Autodesk.Revit.DB.Solid.Intersect(Face) : Solid",
                            "Autodesk.Revit.DB.Solid.GetSurfaceArea() : Double"
                        ],
                        Properties:
                        [
                            "Autodesk.Revit.DB.Solid.Faces : FaceArray"
                        ]),
                    new ManagedTypeMetadata(
                        Namespace: "Autodesk.Revit.DB",
                        FullTypeName: "Autodesk.Revit.DB.Element",
                        Methods:
                        [
                            "Autodesk.Revit.DB.Element.GetParameterValue() : Double"
                        ],
                        Properties:
                        [
                            "Autodesk.Revit.DB.Element.Parameters : ParameterSet"
                        ])
                ]
            }
        };

        var indexer = new GeometryKeywordIndexer();
        var section = indexer.BuildGeometryIndex(assemblies, maxItemsPerSection: 10);

        Assert.Contains(section.TopNamespaces, item => item.Contains("Autodesk.Revit.DB.Geometry", StringComparison.Ordinal));
        Assert.Contains(section.TopTypes, item => item.Contains("Autodesk.Revit.DB.Solid", StringComparison.Ordinal));
        Assert.Contains(section.NotableMethods, item => item.Contains("Intersect", StringComparison.Ordinal));
        Assert.Contains(section.NotableProperties, item => item.Contains(".Faces", StringComparison.Ordinal));
    }

    [Fact]
    public void UnitsAndParametersInspector_FindsUnitsAndParameterEntries()
    {
        var assemblies = new List<ManagedAssemblyMetadata>
        {
            new()
            {
                FileName = "ForgeUnits.dll",
                FullPath = @"C:\Fake\ForgeUnits.dll",
                AssemblyName = "ForgeUnits",
                Types =
                [
                    new ManagedTypeMetadata(
                        Namespace: "Autodesk.Forge.Units",
                        FullTypeName: "Autodesk.Forge.Units.UnitConverter",
                        Methods:
                        [
                            "Autodesk.Forge.Units.UnitConverter.Convert(Double, UnitSpec, UnitSpec) : Double"
                        ],
                        Properties:
                        [
                            "Autodesk.Forge.Units.UnitConverter.Spec : UnitSpec"
                        ])
                ]
            }
        };

        var indexer = new GeometryKeywordIndexer();
        var inspector = new UnitsAndParametersInspector(indexer);
        var section = inspector.BuildUnitsAndParametersIndex(assemblies, maxItemsPerSection: 10);

        Assert.Contains(section.TopNamespaces, item => item.Contains("Autodesk.Forge.Units", StringComparison.Ordinal));
        Assert.Contains(section.TopTypes, item => item.Contains("UnitConverter", StringComparison.Ordinal));
        Assert.Contains(section.NotableMethods, item => item.Contains("Convert", StringComparison.Ordinal));
    }
}
