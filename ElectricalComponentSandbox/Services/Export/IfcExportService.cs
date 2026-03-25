using System.IO;
using System.Text;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services.Export;

/// <summary>
/// Writes an IFC4 P21 (STEP Physical File) with electrical components mapped to
/// standard IfcElectricalDomain entities with real geometry.
///
/// Geometry implemented:
///   IfcExtrudedAreaSolid for boxes, panels, supports, cable trays, hangers
///   IfcExtrudedAreaSolid (circular profile) for conduit segments
///   IfcLocalPlacement with actual X, Y, Z coordinates
///   IfcShapeRepresentation → IfcProductDefinitionShape
///   IfcPropertySet for component parameters
/// </summary>
public class IfcExportService
{
    // ── Configuration ─────────────────────────────────────────────────────────

    public string ApplicationName  { get; set; } = "SimcoeDesign ElectricalComponentSandbox";
    public string ProjectName      { get; set; } = "Electrical Project";
    public string AuthorName       { get; set; } = Environment.UserName;
    public string Organisation     { get; set; } = "Simcoe Design";

    // ── Entity counter ────────────────────────────────────────────────────────

    private int _id;
    private int NextId() => ++_id;

    // ── Public API ────────────────────────────────────────────────────────────

    public void ExportToIfc(IEnumerable<ElectricalComponent> components, string outputPath)
    {
        _id = 0;
        var lines = new List<string>();

        // ── Header ────────────────────────────────────────────────────────────
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");

        lines.Add("ISO-10303-21;");
        lines.Add("HEADER;");
        lines.Add($"FILE_DESCRIPTION(('IFC4 export from {ApplicationName}'),'2;1');");
        lines.Add($"FILE_NAME('{Path.GetFileName(outputPath)}','{now}',('{AuthorName}'),('{Organisation}'),'','','');");
        lines.Add("FILE_SCHEMA(('IFC4'));");
        lines.Add("ENDSEC;");
        lines.Add("");
        lines.Add("DATA;");

        // ── Shared geometry context ───────────────────────────────────────────
        int idOrigin     = NextId();
        int idDirZ       = NextId();
        int idDirX       = NextId();
        int idPlacement0 = NextId();
        int idLocalWorld = NextId();

        lines.Add($"#{idOrigin}     = IFCCARTESIANPOINT((0.,0.,0.));");
        lines.Add($"#{idDirZ}       = IFCDIRECTION((0.,0.,1.));");
        lines.Add($"#{idDirX}       = IFCDIRECTION((1.,0.,0.));");
        lines.Add($"#{idPlacement0} = IFCAXIS2PLACEMENT3D(#{idOrigin},#{idDirZ},#{idDirX});");
        lines.Add($"#{idLocalWorld} = IFCLOCALPLACEMENT($,#{idPlacement0});");

        // Geometric representation context
        int idRepCtx = NextId();
        lines.Add($"#{idRepCtx} = IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.0E-5,#{idPlacement0},$);");

        // ── IfcProject context ────────────────────────────────────────────────
        int idOwnerHist = NextId();
        int idProject   = NextId();
        int idSite      = NextId();
        int idBuilding  = NextId();
        int idStorey    = NextId();

        lines.Add($"#{idOwnerHist} = IFCOWNERHISTORY($,$,$,$.NOTDEFINED.,$,$,$,0);");
        lines.Add($"#{idProject}   = IFCPROJECT('{NewIfcGuid()}',#{idOwnerHist},'{EscapeString(ProjectName)}',$,$,$,$,(#{idRepCtx}),$);");
        lines.Add($"#{idSite}      = IFCSITE('{NewIfcGuid()}',#{idOwnerHist},'Site',$,$,#{idLocalWorld},$,$,.ELEMENT.,$,$,$,$,$);");
        lines.Add($"#{idBuilding}  = IFCBUILDING('{NewIfcGuid()}',#{idOwnerHist},'Building',$,$,#{idLocalWorld},$,$,.ELEMENT.,$,$,$);");
        lines.Add($"#{idStorey}    = IFCBUILDINGSTOREY('{NewIfcGuid()}',#{idOwnerHist},'Level 1',$,$,#{idLocalWorld},$,$,.ELEMENT.,0.);");

        // ── Component entities ────────────────────────────────────────────────
        var storeyRelItems = new List<int>();

        foreach (var comp in components)
        {
            int entityId = WriteComponent(comp, lines, idOwnerHist, idLocalWorld, idRepCtx);
            if (entityId > 0)
                storeyRelItems.Add(entityId);
        }

        // ── Aggregation relationships ─────────────────────────────────────────
        lines.Add($"#{NextId()} = IFCRELAGGREGATES('{NewIfcGuid()}',#{idOwnerHist},$,$,#{idProject},(#{idSite}));");
        lines.Add($"#{NextId()} = IFCRELAGGREGATES('{NewIfcGuid()}',#{idOwnerHist},$,$,#{idSite},(#{idBuilding}));");
        lines.Add($"#{NextId()} = IFCRELAGGREGATES('{NewIfcGuid()}',#{idOwnerHist},$,$,#{idBuilding},(#{idStorey}));");

        if (storeyRelItems.Count > 0)
        {
            var contained = string.Join(",", storeyRelItems.Select(i => $"#{i}"));
            lines.Add($"#{NextId()} = IFCRELCONTAINEDINSPATIALSTRUCTURE('{NewIfcGuid()}',#{idOwnerHist},$,$,({contained}),#{idStorey});");
        }

        lines.Add("ENDSEC;");
        lines.Add("END-ISO-10303-21;");

        File.WriteAllLines(outputPath, lines, Encoding.ASCII);
    }

    // ── Component mapping ─────────────────────────────────────────────────────

    private int WriteComponent(ElectricalComponent comp, List<string> lines,
                                int ownerHist, int worldPlacement, int repCtx)
    {
        var name     = EscapeString(string.IsNullOrEmpty(comp.Name) ? comp.GetType().Name : comp.Name);
        var typeDesc = comp.GetType().Name;

        // Write placement with actual coordinates
        int placId = WriteComponentPlacement(comp, lines, worldPlacement);

        // Write geometry
        int shapeId = WriteComponentGeometry(comp, lines, repCtx);

        // Map component type to IFC entity
        int id = NextId();
        string ifcGuid = NewIfcGuid();

        switch (comp)
        {
            case ConduitComponent conduit:
                lines.Add($"#{id} = IFCCABLECARRIERSEGMENT('{ifcGuid}',#{ownerHist},'{name}',$,'{typeDesc}',#{placId},#{shapeId},$,.CONDUIT.);");
                WriteConduitPSet(conduit, lines, ownerHist, id);
                break;

            case PanelComponent panel:
                lines.Add($"#{id} = IFCDISTRIBUTIONCONTROLELEMENTTYPE('{ifcGuid}',#{ownerHist},'{name}',$,'{typeDesc}',#{placId},#{shapeId},$,.NOTDEFINED.);");
                WritePanelPSet(panel, lines, ownerHist, id);
                break;

            case BoxComponent:
                lines.Add($"#{id} = IFCJUNCTIONBOX('{ifcGuid}',#{ownerHist},'{name}',$,'{typeDesc}',#{placId},#{shapeId},$,.DATA.);");
                WriteComponentPSet(comp, lines, ownerHist, id);
                break;

            default:
                lines.Add($"#{id} = IFCDISTRIBUTIONELEMENT('{ifcGuid}',#{ownerHist},'{name}',$,'{typeDesc}',#{placId},#{shapeId},$);");
                WriteComponentPSet(comp, lines, ownerHist, id);
                break;
        }

        return id;
    }

    private int WriteComponentPlacement(ElectricalComponent comp, List<string> lines, int worldPlacement)
    {
        double x = comp.Position.X;
        double y = comp.Position.Y;
        double z = comp.Parameters.Elevation;

        int ptId   = NextId();
        int dirZId = NextId();
        int dirXId = NextId();
        int axId   = NextId();
        int placId = NextId();

        // Apply rotation around Z axis
        double rotRad = comp.Rotation.Z * Math.PI / 180.0;
        double cosR = Math.Cos(rotRad);
        double sinR = Math.Sin(rotRad);

        lines.Add($"#{ptId}   = IFCCARTESIANPOINT(({R(x)},{R(y)},{R(z)}));");
        lines.Add($"#{dirZId} = IFCDIRECTION((0.,0.,1.));");
        lines.Add($"#{dirXId} = IFCDIRECTION(({R(cosR)},{R(sinR)},0.));");
        lines.Add($"#{axId}   = IFCAXIS2PLACEMENT3D(#{ptId},#{dirZId},#{dirXId});");
        lines.Add($"#{placId} = IFCLOCALPLACEMENT(#{worldPlacement},#{axId});");

        return placId;
    }

    private int WriteComponentGeometry(ElectricalComponent comp, List<string> lines, int repCtx)
    {
        int solidId;

        if (comp is ConduitComponent conduit)
        {
            solidId = WriteCircularExtrusion(lines,
                conduit.Diameter / 2.0,
                conduit.Length);
        }
        else
        {
            double w = comp.Parameters.Width * comp.Scale.X;
            double d = comp.Parameters.Depth * comp.Scale.Y;
            double h = comp.Parameters.Height * comp.Scale.Z;

            solidId = WriteRectangularExtrusion(lines, w, d, h);
        }

        // IfcShapeRepresentation
        int repId = NextId();
        lines.Add($"#{repId} = IFCSHAPEREPRESENTATION(#{repCtx},'Body','SweptSolid',(#{solidId}));");

        // IfcProductDefinitionShape
        int prodShapeId = NextId();
        lines.Add($"#{prodShapeId} = IFCPRODUCTDEFINITIONSHAPE($,$,(#{repId}));");

        return prodShapeId;
    }

    private int WriteRectangularExtrusion(List<string> lines, double width, double depth, double height)
    {
        double hw = width / 2.0;
        double hd = depth / 2.0;

        int p1 = NextId(), p2 = NextId(), p3 = NextId(), p4 = NextId();
        lines.Add($"#{p1} = IFCCARTESIANPOINT(({R(-hw)},{R(-hd)}));");
        lines.Add($"#{p2} = IFCCARTESIANPOINT(({R(hw)},{R(-hd)}));");
        lines.Add($"#{p3} = IFCCARTESIANPOINT(({R(hw)},{R(hd)}));");
        lines.Add($"#{p4} = IFCCARTESIANPOINT(({R(-hw)},{R(hd)}));");

        int polyId = NextId();
        lines.Add($"#{polyId} = IFCPOLYLINE((#{p1},#{p2},#{p3},#{p4},#{p1}));");

        int profileId = NextId();
        lines.Add($"#{profileId} = IFCARBITRARYCLOSEDPROFILEDEF(.AREA.,$,#{polyId});");

        int posId = NextId();
        int originId = NextId();
        int dirId = NextId();
        lines.Add($"#{originId} = IFCCARTESIANPOINT((0.,0.,0.));");
        lines.Add($"#{dirId}    = IFCDIRECTION((0.,0.,1.));");
        lines.Add($"#{posId}    = IFCAXIS2PLACEMENT3D(#{originId},#{dirId},$);");

        int solidId = NextId();
        lines.Add($"#{solidId} = IFCEXTRUDEDAREASOLID(#{profileId},#{posId},#{dirId},{R(height)});");

        return solidId;
    }

    private int WriteCircularExtrusion(List<string> lines, double radius, double length)
    {
        int profileId = NextId();
        lines.Add($"#{profileId} = IFCCIRCLEPROFILEDEF(.AREA.,$,$,{R(radius)});");

        int originId = NextId();
        int dirId = NextId();
        int posId = NextId();
        lines.Add($"#{originId} = IFCCARTESIANPOINT((0.,0.,0.));");
        lines.Add($"#{dirId}    = IFCDIRECTION((0.,0.,1.));");
        lines.Add($"#{posId}    = IFCAXIS2PLACEMENT3D(#{originId},#{dirId},$);");

        int solidId = NextId();
        lines.Add($"#{solidId} = IFCEXTRUDEDAREASOLID(#{profileId},#{posId},#{dirId},{R(length)});");

        return solidId;
    }

    // ── Property sets ─────────────────────────────────────────────────────────

    private void WriteConduitPSet(ConduitComponent conduit, List<string> lines, int ownerHist, int entityId)
    {
        var props = new List<int>();

        props.Add(WritePropReal(lines, "NominalLength", conduit.Length));
        props.Add(WritePropReal(lines, "TradeSize", conduit.Diameter));
        props.Add(WritePropLabel(lines, "ConduitType", conduit.ConduitType));
        props.Add(WritePropReal(lines, "BendRadius", conduit.BendRadius));
        props.Add(WritePropLabel(lines, "Material", conduit.Parameters.Material));
        props.Add(WritePropLabel(lines, "Manufacturer", conduit.Parameters.Manufacturer));
        props.Add(WritePropLabel(lines, "PartNumber", conduit.Parameters.PartNumber));

        WritePSet(lines, ownerHist, entityId, "Pset_CableCarrierSegmentTypeConduit", props);
    }

    private void WritePanelPSet(PanelComponent panel, List<string> lines, int ownerHist, int entityId)
    {
        var props = new List<int>();

        props.Add(WritePropInt(lines, "CircuitCount", panel.CircuitCount));
        props.Add(WritePropReal(lines, "Amperage", panel.Amperage));
        props.Add(WritePropLabel(lines, "PanelType", panel.PanelType));
        props.Add(WritePropReal(lines, "Elevation", panel.Parameters.Elevation));
        props.Add(WritePropLabel(lines, "Manufacturer", panel.Parameters.Manufacturer));
        props.Add(WritePropLabel(lines, "PartNumber", panel.Parameters.PartNumber));

        WritePSet(lines, ownerHist, entityId, "Pset_ElectricalPanel", props);
    }

    private void WriteComponentPSet(ElectricalComponent comp, List<string> lines, int ownerHist, int entityId)
    {
        var props = new List<int>();

        props.Add(WritePropReal(lines, "Width", comp.Parameters.Width));
        props.Add(WritePropReal(lines, "Height", comp.Parameters.Height));
        props.Add(WritePropReal(lines, "Depth", comp.Parameters.Depth));
        props.Add(WritePropReal(lines, "Elevation", comp.Parameters.Elevation));
        props.Add(WritePropLabel(lines, "Material", comp.Parameters.Material));
        props.Add(WritePropLabel(lines, "Manufacturer", comp.Parameters.Manufacturer));
        props.Add(WritePropLabel(lines, "PartNumber", comp.Parameters.PartNumber));

        WritePSet(lines, ownerHist, entityId, "Pset_ComponentParameters", props);
    }

    private void WritePSet(List<string> lines, int ownerHist, int entityId,
        string psetName, List<int> propIds)
    {
        int psetId = NextId();
        var propRefs = string.Join(",", propIds.Select(i => $"#{i}"));
        lines.Add($"#{psetId} = IFCPROPERTYSET('{NewIfcGuid()}',#{ownerHist},'{psetName}',$,({propRefs}));");

        int relId = NextId();
        lines.Add($"#{relId} = IFCRELDEFINESBYPROPERTIES('{NewIfcGuid()}',#{ownerHist},$,$,(#{entityId}),#{psetId});");
    }

    private int WritePropReal(List<string> lines, string name, double value)
    {
        int valId = NextId();
        int propId = NextId();
        lines.Add($"#{valId}  = IFCREAL({R(value)});");
        lines.Add($"#{propId} = IFCPROPERTYSINGLEVALUE('{name}',$,#{valId},$);");
        return propId;
    }

    private int WritePropInt(List<string> lines, string name, int value)
    {
        int valId = NextId();
        int propId = NextId();
        lines.Add($"#{valId}  = IFCINTEGER({value});");
        lines.Add($"#{propId} = IFCPROPERTYSINGLEVALUE('{name}',$,#{valId},$);");
        return propId;
    }

    private int WritePropLabel(List<string> lines, string name, string value)
    {
        int valId = NextId();
        int propId = NextId();
        lines.Add($"#{valId}  = IFCLABEL('{EscapeString(value)}');");
        lines.Add($"#{propId} = IFCPROPERTYSINGLEVALUE('{name}',$,#{valId},$);");
        return propId;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string R(double v)
    {
        var s = v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        return s.Contains('.') ? s : s + ".";
    }

    private static string EscapeString(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("'", "''");
    }

    private static string NewIfcGuid()
    {
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";
        var bytes = Guid.NewGuid().ToByteArray();
        var sb    = new StringBuilder(22);
        ulong part1 = BitConverter.ToUInt64(bytes, 0);
        ulong part2 = BitConverter.ToUInt64(bytes, 8);
        for (int i = 0; i < 11; i++)
        {
            sb.Append(chars[(int)(part1 % 64)]); part1 /= 64;
        }
        for (int i = 0; i < 11; i++)
        {
            sb.Append(chars[(int)(part2 % 64)]); part2 /= 64;
        }
        return sb.ToString();
    }
}
