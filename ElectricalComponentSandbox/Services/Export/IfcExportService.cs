using System.IO;
using System.Text;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services.Export;

/// <summary>
/// Writes an IFC4 P21 (STEP Physical File) with electrical components mapped to
/// standard IfcElectricalDomain entities.
///
/// Subset implemented:
///   IfcProject / IfcSite / IfcBuilding / IfcBuildingStorey  — minimal context
///   IfcCableCarrierSegment   — conduit runs
///   IfcDistributionFlowElement — panels and junction boxes
///   IfcFlowFitting            — conduit fittings / elbows
///   IfcLocalPlacement / IfcAxis2Placement3D  — origin-only placement
///   IfcProductDefinitionShape — empty for now (geometry not exported)
/// </summary>
public class IfcExportService
{
    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>Application name written to the FILE_DESCRIPTION header.</summary>
    public string ApplicationName  { get; set; } = "SimcoeDesign ElectricalComponentSandbox";
    /// <summary>Project name embedded in IfcProject.</summary>
    public string ProjectName      { get; set; } = "Electrical Project";
    /// <summary>Author string for FILE_NAME AUTHOR field.</summary>
    public string AuthorName       { get; set; } = Environment.UserName;
    /// <summary>Originating organisation string.</summary>
    public string Organisation     { get; set; } = "Simcoe Design";

    // ── Entity counter ────────────────────────────────────────────────────────

    private int _id;
    private int NextId() => ++_id;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Exports <paramref name="components"/> to an IFC4 P21 text file at <paramref name="outputPath"/>.
    /// </summary>
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

        // ── Shared geometry helpers ───────────────────────────────────────────
        int idOrigin      = NextId(); // IfcCartesianPoint (0,0,0)
        int idDirZ        = NextId(); // IfcDirection (0,0,1)
        int idDirX        = NextId(); // IfcDirection (1,0,0)
        int idPlacement0  = NextId(); // IfcAxis2Placement3D — world origin
        int idLocalWorld  = NextId(); // IfcLocalPlacement — world placement

        lines.Add($"#{idOrigin}     = IFCCARTESIANPOINT((0.,0.,0.));");
        lines.Add($"#{idDirZ}       = IFCDIRECTION((0.,0.,1.));");
        lines.Add($"#{idDirX}       = IFCDIRECTION((1.,0.,0.));");
        lines.Add($"#{idPlacement0} = IFCAXIS2PLACEMENT3D(#{idOrigin},#{idDirZ},#{idDirX});");
        lines.Add($"#{idLocalWorld} = IFCLOCALPLACEMENT($,#{idPlacement0});");

        // ── IfcProject context ────────────────────────────────────────────────
        int idOwnerHist  = NextId();
        int idProject    = NextId();
        int idSite       = NextId();
        int idBuilding   = NextId();
        int idStorey     = NextId();

        string guid1 = NewIfcGuid();
        lines.Add($"#{idOwnerHist} = IFCOWNERHISTORY($,$,$,$.NOTDEFINED.,$,$,$,0);");
        lines.Add($"#{idProject}   = IFCPROJECT('{guid1}',#{idOwnerHist},'{EscapeString(ProjectName)}',$,$,$,$,$,$);");
        lines.Add($"#{idSite}      = IFCSITE('{NewIfcGuid()}',#{idOwnerHist},'Site',$,$,#{idLocalWorld},$,$,.ELEMENT.,$,$,$,$,$);");
        lines.Add($"#{idBuilding}  = IFCBUILDING('{NewIfcGuid()}',#{idOwnerHist},'Building',$,$,#{idLocalWorld},$,$,.ELEMENT.,$,$,$);");
        lines.Add($"#{idStorey}    = IFCBUILDINGSTOREY('{NewIfcGuid()}',#{idOwnerHist},'Level 1',$,$,#{idLocalWorld},$,$,.ELEMENT.,0.);");

        // ── Component entities ────────────────────────────────────────────────
        var storeyRelItems = new List<int>();

        foreach (var comp in components)
        {
            int entityId = WriteComponent(comp, lines, idOwnerHist, idLocalWorld);
            if (entityId > 0)
                storeyRelItems.Add(entityId);
        }

        // ── Aggregation relationships ─────────────────────────────────────────
        lines.Add($"#{ NextId()} = IFCRELAGGREGATES('{NewIfcGuid()}',#{idOwnerHist},$,$,#{idProject},(#{idSite}));");
        lines.Add($"#{ NextId()} = IFCRELAGGREGATES('{NewIfcGuid()}',#{idOwnerHist},$,$,#{idSite},(#{idBuilding}));");
        lines.Add($"#{ NextId()} = IFCRELAGGREGATES('{NewIfcGuid()}',#{idOwnerHist},$,$,#{idBuilding},(#{idStorey}));");

        if (storeyRelItems.Count > 0)
        {
            var contained = string.Join(",", storeyRelItems.Select(i => $"#{i}"));
            lines.Add($"#{ NextId()} = IFCRELCONTAINEDINSPATIALSTRUCTURE('{NewIfcGuid()}',#{idOwnerHist},$,$,({contained}),#{idStorey});");
        }

        lines.Add("ENDSEC;");
        lines.Add("END-ISO-10303-21;");

        File.WriteAllLines(outputPath, lines, Encoding.ASCII);
    }

    // ── Component mapping ─────────────────────────────────────────────────────

    private int WriteComponent(ElectricalComponent comp, List<string> lines,
                                int ownerHist, int worldPlacement)
    {
        var name     = EscapeString(string.IsNullOrEmpty(comp.Name) ? comp.GetType().Name : comp.Name);
        var typeDesc = comp.GetType().Name;
        var guid     = NewIfcGuid();

        // Map component type to IFC entity name
        string ifcType = comp switch
        {
            ConduitComponent  => "IFCCABLECARRIERSEGMENT",
            PanelComponent    => "IFCDISTRIBUTIONFLOWelement",
            BoxComponent      => "IFCDISTRIBUTIONFLOWELEMENTTYPE",
            _                 => "IFCDISTRIBUTIONELEMENT"
        };

        int id = NextId();

        // Placement referencing world
        int placId = NextId();
        int axId   = NextId();
        int ptId   = WriteComponentOrigin(comp, lines);
        int dirZId = NextId();
        int dirXId = NextId();

        lines.Add($"#{dirZId} = IFCDIRECTION((0.,0.,1.));");
        lines.Add($"#{dirXId} = IFCDIRECTION((1.,0.,0.));");
        lines.Add($"#{axId}   = IFCAXIS2PLACEMENT3D(#{ptId},#{dirZId},#{dirXId});");
        lines.Add($"#{placId} = IFCLOCALPLACEMENT(#{worldPlacement},#{axId});");

        // PredefinedType for cable carrier
        string predefined = comp is ConduitComponent ? ".CONDUIT." : ".NOTDEFINED.";

        // Entity line — all fields except ObjectType/Description written as $ where not used
        if (comp is ConduitComponent conduit)
        {
            lines.Add($"#{id} = IFCCABLECARRIERSEGMENT('{guid}',#{ownerHist},'{name}',$,'{typeDesc}',#{placId},$,$,{predefined.ToUpper()});");

            // Property set for length
            int psetId = WriteConduitPSet(conduit, lines, ownerHist, id);
            _ = psetId;
        }
        else
        {
            lines.Add($"#{id} = {ifcType.ToUpper()}('{guid}',#{ownerHist},'{name}',$,'{typeDesc}',#{placId},$,$,.NOTDEFINED.);");
        }

        return id;
    }

    private int WriteComponentOrigin(ElectricalComponent comp, List<string> lines)
    {
        // X/Y are not on ComponentParameters; use 0,0 and elevation for Z
        double z = comp.Parameters.Elevation;
        int id = NextId();
        lines.Add($"#{id} = IFCCARTESIANPOINT((0.,0.,{FormatIfcReal(z)}));");
        return id;
    }

    private int WriteConduitPSet(ConduitComponent conduit, List<string> lines, int ownerHist, int conduitId)
    {
        double length     = conduit.Length;
        double tradeSize  = conduit.Diameter;
        string conduitType = conduit.ConduitType;

        int pLengthValId   = NextId();
        int pLengthId      = NextId();
        int pTradeSizeValId= NextId();
        int pTradeSizeId   = NextId();
        int pTypeValId     = NextId();
        int pTypeId        = NextId();
        int psetId         = NextId();
        int relId          = NextId();

        lines.Add($"#{pLengthValId}    = IFCREAL({FormatIfcReal(length)});");
        lines.Add($"#{pLengthId}       = IFCPROPERTYSINGLEVALUE('NominalLength',$,#{pLengthValId},$);");
        lines.Add($"#{pTradeSizeValId} = IFCREAL({FormatIfcReal(tradeSize)});");
        lines.Add($"#{pTradeSizeId}    = IFCPROPERTYSINGLEVALUE('TradeSize',$,#{pTradeSizeValId},$);");
        lines.Add($"#{pTypeValId}      = IFCLABEL('{EscapeString(conduitType)}');");
        lines.Add($"#{pTypeId}         = IFCPROPERTYSINGLEVALUE('ConduitType',$,#{pTypeValId},$);");
        lines.Add($"#{psetId}          = IFCPROPERTYSET('{NewIfcGuid()}',#{ownerHist},'Pset_CableCarrierSegmentTypeConduit',$,(#{pLengthId},#{pTradeSizeId},#{pTypeId}));");
        lines.Add($"#{relId}           = IFCRELDEFINESBYPROPERTIES('{NewIfcGuid()}',#{ownerHist},$,$,(#{conduitId}),#{psetId});");

        return psetId;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatIfcReal(double v)
    {
        // IFC P21 reals must have a decimal point
        var s = v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        return s.Contains('.') ? s : s + ".";
    }

    private static string EscapeString(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        // P21 strings: single-quotes doubled, backslashes doubled
        return value.Replace("\\", "\\\\").Replace("'", "''");
    }

    /// <summary>
    /// Generates a pseudo-random Base64-encoded IFC GUID (22 chars, IFC character set).
    /// Real projects should use a deterministic GUID derived from the component ID.
    /// </summary>
    private static string NewIfcGuid()
    {
        // IFC GUID uses a custom Base64 alphabet over the raw bytes of a GUID
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";
        var bytes = Guid.NewGuid().ToByteArray();
        var sb    = new StringBuilder(22);
        // Encode 16 bytes into 22 base-64 IFC characters
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
