using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Provides reusable component templates and visual profile inference for common electrical parts.
/// </summary>
public static class ElectricalComponentCatalog
{
    public static class Profiles
    {
        public const string ConduitEmt = "conduit_emt";
        public const string ConduitPvc = "conduit_pvc";
        public const string ConduitRigidMetal = "conduit_rigid_metal";
        public const string ConduitFlexibleMetal = "conduit_flexible_metal";

        public const string BoxJunction = "box_junction";
        public const string BoxPull = "box_pull";
        public const string BoxFloor = "box_floor";
        public const string BoxDisconnectSwitch = "box_disconnect_switch";

        public const string PanelLighting = "panel_lighting";
        public const string PanelDistribution = "panel_distribution";
        public const string PanelSwitchboard = "panel_switchboard";
        public const string PanelMcc = "panel_mcc";

        public const string TrayLadder = "tray_ladder";
        public const string TrayWireMesh = "tray_wire_mesh";
        public const string TraySolidBottom = "tray_solid_bottom";

        public const string SupportUnistrut = "support_unistrut";
        public const string SupportWallBracket = "support_wall_bracket";
        public const string SupportTrapeze = "support_trapeze";

        public const string HangerThreadedRod = "hanger_threaded_rod";
        public const string HangerSeismicBrace = "hanger_seismic_brace";
    }

    private static readonly IReadOnlyDictionary<ComponentType, string> DefaultProfileByType = new Dictionary<ComponentType, string>
    {
        [ComponentType.Conduit] = Profiles.ConduitEmt,
        [ComponentType.Box] = Profiles.BoxJunction,
        [ComponentType.Panel] = Profiles.PanelDistribution,
        [ComponentType.Support] = Profiles.SupportUnistrut,
        [ComponentType.CableTray] = Profiles.TrayLadder,
        [ComponentType.Hanger] = Profiles.HangerThreadedRod
    };

    // Model units are feet; vendor dimensions published in inches were converted to feet here.
    private static readonly IReadOnlyList<ElectricalComponent> ComponentTemplates = new List<ElectricalComponent>
    {
        new ConduitComponent
        {
            Name = "EMT Conduit 1 in x 10 ft",
            VisualProfile = Profiles.ConduitEmt,
            ConduitType = "EMT (Trade 1 in)",
            Diameter = 0.0969,
            Length = 10.0,
            BendRadius = 1.0,
            Parameters =
            {
                Material = "Steel EMT",
                Color = "#6B7A8F",
                Width = 0.0969,
                Height = 0.0969,
                Depth = 10.0,
                Manufacturer = "Wheatland Tube (Zekelman Industries)",
                PartNumber = "101568",
                ReferenceUrl = "https://www.standardelectricsupply.com/Conduit-101568-Conduit"
            }
        },
        new ConduitComponent
        {
            Name = "PVC Sch 40 Conduit 3/4 in x 10 ft",
            VisualProfile = Profiles.ConduitPvc,
            ConduitType = "PVC Sch 40 (Trade 3/4 in)",
            Diameter = 0.0875,
            Length = 10.0,
            BendRadius = 1.25,
            Parameters =
            {
                Material = "PVC",
                Color = "#D2B48C",
                Width = 0.0875,
                Height = 0.0875,
                Depth = 10.0,
                Manufacturer = "CANTEX",
                PartNumber = "A52AG12",
                ReferenceUrl = "https://www.marsmfg.com/electrical/non-metallic-conduit-fittings/non-metallic-conduit/pvc-conduit-sch40-3-4in-x-10ft-ul651?item=A52AG12"
            }
        },
        new ConduitComponent
        {
            Name = "Rigid Metal Conduit 1 in x 10 ft",
            VisualProfile = Profiles.ConduitRigidMetal,
            ConduitType = "RMC (Trade 1 in)",
            Diameter = 0.1096,
            Length = 10.0,
            BendRadius = 1.5,
            Parameters =
            {
                Material = "Rigid Steel",
                Color = "#4E5A66",
                Width = 0.1096,
                Height = 0.1096,
                Depth = 10.0,
                Manufacturer = "Wheatland Tube (Zekelman Industries)",
                PartNumber = "1-HW",
                ReferenceUrl = "https://www.standardelectricsupply.com/Conduit-1-HW-Conduit"
            }
        },
        new ConduitComponent
        {
            Name = "Flexible Metal Conduit 3/4 in x 25 ft",
            VisualProfile = Profiles.ConduitFlexibleMetal,
            ConduitType = "FMC Aluminum (Trade 3/4 in)",
            Diameter = 0.0804,
            Length = 25.0,
            BendRadius = 0.75,
            Parameters =
            {
                Material = "Flexible Aluminum",
                Color = "#7F8A96",
                Width = 0.0804,
                Height = 0.0804,
                Depth = 25.0,
                Manufacturer = "Zoro Select",
                PartNumber = "5602-24-00",
                ReferenceUrl = "https://www.zoro.com/zoro-select-flex-conduit-aluminum-34-in-trade-size-25-l-ft-5602-24-00/i/G812105730/"
            }
        },

        new BoxComponent
        {
            Name = "4x4 Junction Box",
            VisualProfile = Profiles.BoxJunction,
            BoxType = "Junction Box",
            KnockoutCount = 8,
            Parameters =
            {
                Width = 0.3333,
                Height = 0.1667,
                Depth = 0.3333,
                Material = "Galvanized Steel",
                Color = "#A3ADB7",
                Manufacturer = "RACO (Hubbell)",
                PartNumber = "8232",
                ReferenceUrl = "https://www.hubbell.com/raco/en/Products/Electrical-Electronic/Boxes/Square-Boxes-Covers/4-in-Square-Box-Welded-2-18-in-Deep-Eight-12-in-KO-s-and-Four-NMC-Clamps/p/1670054"
            }
        },
        new BoxComponent
        {
            Name = "Pull Box 12 x 12 x 6 in",
            VisualProfile = Profiles.BoxPull,
            BoxType = "Pull Box",
            KnockoutCount = 12,
            Parameters =
            {
                Width = 1.0,
                Height = 0.5,
                Depth = 1.0,
                Material = "Steel",
                Color = "#8E9AA6",
                Manufacturer = "GS Metals",
                PartNumber = "ASG12X12X6",
                ReferenceUrl = "https://gsmetals.com/products/12-x-12-x-6-pull-boxes"
            }
        },
        new BoxComponent
        {
            Name = "Floor Box",
            VisualProfile = Profiles.BoxFloor,
            BoxType = "Floor Box",
            KnockoutCount = 6,
            Parameters =
            {
                Width = 0.7658,
                Height = 0.34,
                Depth = 0.63,
                Material = "Brass/Steel",
                Color = "#B8B09E",
                Manufacturer = "Hubbell Wiring Device-Kellems",
                PartNumber = "PFBRG1",
                ReferenceUrl = "https://www.hubbell.com/wiringdevice-kellems/en/Products/Electrical-Electronic/Wiring-Devices/Floor-Boxes/Flush-Poke-Throughs/Poke-Through-Flush-Rectangular-Stamp-Steel-1-Gang/p/1633121"
            }
        },
        new BoxComponent
        {
            Name = "Disconnect Switch",
            VisualProfile = Profiles.BoxDisconnectSwitch,
            BoxType = "Disconnect Switch",
            KnockoutCount = 4,
            Parameters =
            {
                Width = 0.631,
                Height = 1.215,
                Depth = 0.51,
                Material = "NEMA Enclosure",
                Color = "#9CA7B3",
                Manufacturer = "Eaton",
                PartNumber = "AHDS60AC",
                ReferenceUrl = "https://www.eaton.com/us/en-us/skuPage.AHDS60AC.html"
            }
        },

        new PanelComponent
        {
            Name = "Lighting Panelboard",
            VisualProfile = Profiles.PanelLighting,
            PanelType = "Lighting Panelboard",
            CircuitCount = 42,
            Amperage = 400,
            Parameters =
            {
                Width = 1.667,
                Height = 5.667,
                Depth = 0.479,
                Material = "Painted Steel",
                Color = "#A9B4BE",
                Manufacturer = "Siemens",
                PartNumber = "P1X42MC400AT",
                ReferenceUrl = "https://www.essentialparts.com/p1x42mc400at-siemens-panelboard-distribution"
            }
        },
        new PanelComponent
        {
            Name = "Distribution Panel",
            VisualProfile = Profiles.PanelDistribution,
            PanelType = "Distribution Panel",
            CircuitCount = 84,
            Amperage = 1600,
            Parameters =
            {
                Width = 1.25,
                Height = 5.083,
                Depth = 0.5,
                Material = "Painted Steel",
                Color = "#8D99A6",
                Manufacturer = "Eaton",
                PartNumber = "PRL1X1600X42C",
                ReferenceUrl = "https://www.borderstates.com/electrical-distribution/boxes-and-enclosures/panels-and-switchboards/panels-and-panelboard-boxes/panel-interior-42-circuit-1600a-main-lug-208y-120vac-3ph-4w/p/11349405"
            }
        },
        new PanelComponent
        {
            Name = "Switchboard",
            VisualProfile = Profiles.PanelSwitchboard,
            PanelType = "Switchboard",
            CircuitCount = 84,
            Amperage = 6000,
            Parameters =
            {
                Width = 5.333,
                Height = 7.5,
                Depth = 3.5,
                Material = "Switchgear Steel",
                Color = "#6F7A86",
                Manufacturer = "Eaton",
                PartNumber = "RMBBP624",
                ReferenceUrl = "https://www.eaton.com/us/en-us/skuPage.RMBBP624.html"
            }
        },
        new PanelComponent
        {
            Name = "Motor Control Center",
            VisualProfile = Profiles.PanelMcc,
            PanelType = "CENTERLINE 2100 MCC",
            CircuitCount = 30,
            Amperage = 600,
            Parameters =
            {
                Width = 1.667,
                Height = 7.5,
                Depth = 1.25,
                Material = "Switchgear Steel",
                Color = "#6B7682",
                Manufacturer = "Rockwell Automation",
                PartNumber = "Bulletin 2100",
                ReferenceUrl = "https://literature.rockwellautomation.com/idc/groups/literature/documents/sg/2100-sg003_-en-p.pdf"
            }
        },

        new CableTrayComponent
        {
            Name = "Ladder Cable Tray",
            VisualProfile = Profiles.TrayLadder,
            TrayType = "Ladder",
            TrayWidth = 1.0,
            TrayDepth = 0.3215,
            Length = 10.0,
            Parameters =
            {
                Width = 1.0,
                Height = 0.3215,
                Depth = 10.0,
                Material = "Aluminum",
                Color = "#B2BAC2",
                Manufacturer = "Eaton B-Line",
                PartNumber = "24A09-12-120",
                ReferenceUrl = "https://www.eaton.com/us/en-us/skuPage.24A09-12-120.html"
            }
        },
        new CableTrayComponent
        {
            Name = "Wire Mesh Tray",
            VisualProfile = Profiles.TrayWireMesh,
            TrayType = "Wire Mesh",
            TrayWidth = 0.6667,
            TrayDepth = 0.1667,
            Length = 9.833,
            Parameters =
            {
                Width = 0.6667,
                Height = 0.1667,
                Depth = 9.833,
                Material = "Zinc Steel",
                Color = "#9CA5AE",
                Manufacturer = "Legrand Cablofil",
                PartNumber = "941108",
                ReferenceUrl = "https://www.cesco.com/Legrand-Legrand-941108-2-x-8-Wire-Mesh-Cable-Tray_1"
            }
        },
        new CableTrayComponent
        {
            Name = "Solid Bottom Tray",
            VisualProfile = Profiles.TraySolidBottom,
            TrayType = "Solid Bottom",
            TrayWidth = 1.0,
            TrayDepth = 0.3215,
            Length = 10.0,
            Parameters =
            {
                Width = 1.0,
                Height = 0.3215,
                Depth = 10.0,
                Material = "Galvanized Steel",
                Color = "#89939E",
                Manufacturer = "Eaton B-Line",
                PartNumber = "KRA4ASB-12-120",
                ReferenceUrl = "https://www.eaton.com/us/en-us/skuPage.KRA4ASB-12-120.html"
            }
        },

        new SupportComponent
        {
            Name = "Unistrut Channel P1000",
            VisualProfile = Profiles.SupportUnistrut,
            SupportType = "Unistrut",
            LoadCapacity = 300,
            Parameters =
            {
                Width = 0.1354,
                Height = 0.1354,
                Depth = 10.0,
                Material = "Unistrut",
                Color = "#6E7781",
                Manufacturer = "Unistrut",
                PartNumber = "P1000",
                ReferenceUrl = "https://www.unistrutohio.com/p1000-unistrut-channel"
            }
        },
        new SupportComponent
        {
            Name = "Wall Bracket SB21312KFB",
            VisualProfile = Profiles.SupportWallBracket,
            SupportType = "Wall Bracket",
            LoadCapacity = 200,
            Parameters =
            {
                Width = 0.1354,
                Height = 1.094,
                Depth = 1.156,
                Material = "Steel Bracket",
                Color = "#6A7380",
                Manufacturer = "Eaton B-Line",
                PartNumber = "SB21312KFB",
                ReferenceUrl = "https://www.kirbyrisk.com/products/eaton-b-line-series-fasteners-hardware-channel-fittings-sb21312kfb"
            }
        },
        new SupportComponent
        {
            Name = "Trapeze Support FTB12CTBLE",
            VisualProfile = Profiles.SupportTrapeze,
            SupportType = "Trapeze",
            LoadCapacity = 500,
            Parameters =
            {
                Width = 0.0833,
                Height = 1.2,
                Depth = 1.3075,
                Material = "Channel + Rod",
                Color = "#5F6872",
                Manufacturer = "Eaton B-Line",
                PartNumber = "FTB12CTBLE",
                ReferenceUrl = "https://www.eaton.com/us/en-us/skuPage.FTB12CTBLE.html"
            }
        },

        new HangerComponent
        {
            Name = "Threaded Rod 3/8 in x 10 ft",
            VisualProfile = Profiles.HangerThreadedRod,
            HangerType = "Threaded Rod",
            RodDiameter = 0.03125,
            RodLength = 10.0,
            LoadCapacity = 150,
            Parameters =
            {
                Width = 0.03125,
                Height = 10.0,
                Depth = 0.03125,
                Material = "Galvanized Steel",
                Color = "#9099A3",
                Manufacturer = "Eaton B-Line",
                PartNumber = "ATR-3/8X120-ZN",
                ReferenceUrl = "https://www.eaton.com/us/en-us/skuPage.ATR-3%2F8X120-ZN.html"
            }
        },
        new HangerComponent
        {
            Name = "Seismic Brace Clamp 1/2 in",
            VisualProfile = Profiles.HangerSeismicBrace,
            HangerType = "Seismic Brace",
            RodDiameter = 0.0417,
            RodLength = 0.2792,
            LoadCapacity = 280,
            Parameters =
            {
                Width = 0.1354,
                Height = 0.2792,
                Depth = 0.0208,
                Material = "Seismic Steel",
                Color = "#7A838D",
                Manufacturer = "Eaton B-Line",
                PartNumber = "B650-1/2GRN",
                ReferenceUrl = "https://www.eaton.com/us/en-us/skuPage.B650-1-2GRN.html"
            }
        }
    };

    public static IReadOnlyList<ElectricalComponent> CreateLibraryTemplates()
    {
        return ComponentTemplates.Select(CloneTemplate).ToList();
    }

    public static ElectricalComponent CreateDefaultComponent(ComponentType type)
    {
        var profile = DefaultProfileByType.TryGetValue(type, out var mappedProfile)
            ? mappedProfile
            : string.Empty;

        var template = ComponentTemplates.FirstOrDefault(t =>
                t.Type == type && string.Equals(GetProfile(t), profile, StringComparison.OrdinalIgnoreCase))
            ?? ComponentTemplates.FirstOrDefault(t => t.Type == type)
            ?? throw new ArgumentException($"No component template found for type '{type}'.");

        return CloneTemplate(template);
    }

    public static ElectricalComponent CloneTemplate(ElectricalComponent template)
    {
        ElectricalComponent clone = template switch
        {
            ConduitComponent conduit => CloneConduit(conduit),
            BoxComponent box => CloneBox(box),
            PanelComponent panel => ClonePanel(panel),
            SupportComponent support => CloneSupport(support),
            CableTrayComponent tray => CloneCableTray(tray),
            HangerComponent hanger => CloneHanger(hanger),
            _ => throw new ArgumentException($"Unsupported component type '{template.GetType().Name}'.")
        };

        CopyCommon(template, clone);
        return clone;
    }

    public static string GetProfile(ElectricalComponent component)
    {
        if (!string.IsNullOrWhiteSpace(component.VisualProfile))
            return component.VisualProfile;

        return component switch
        {
            ConduitComponent conduit => InferConduitProfile(conduit),
            BoxComponent box => InferBoxProfile(box),
            PanelComponent panel => InferPanelProfile(panel),
            SupportComponent support => InferSupportProfile(support),
            CableTrayComponent tray => InferTrayProfile(tray),
            HangerComponent hanger => InferHangerProfile(hanger),
            _ => DefaultProfileByType.TryGetValue(component.Type, out var profile) ? profile : string.Empty
        };
    }

    private static string InferConduitProfile(ConduitComponent conduit)
    {
        var text = conduit.ConduitType?.ToLowerInvariant() ?? string.Empty;
        if (text.Contains("pvc"))
            return Profiles.ConduitPvc;
        if (text.Contains("rigid") || text.Contains("rmc") || text.Contains("imc"))
            return Profiles.ConduitRigidMetal;
        if (text.Contains("flex"))
            return Profiles.ConduitFlexibleMetal;
        return Profiles.ConduitEmt;
    }

    private static string InferBoxProfile(BoxComponent box)
    {
        var text = box.BoxType?.ToLowerInvariant() ?? string.Empty;
        if (text.Contains("pull"))
            return Profiles.BoxPull;
        if (text.Contains("floor"))
            return Profiles.BoxFloor;
        if (text.Contains("disconnect"))
            return Profiles.BoxDisconnectSwitch;
        return Profiles.BoxJunction;
    }

    private static string InferPanelProfile(PanelComponent panel)
    {
        var text = panel.PanelType?.ToLowerInvariant() ?? string.Empty;
        if (text.Contains("switchboard"))
            return Profiles.PanelSwitchboard;
        if (text.Contains("mcc") || text.Contains("motor control"))
            return Profiles.PanelMcc;
        if (text.Contains("lighting"))
            return Profiles.PanelLighting;
        return Profiles.PanelDistribution;
    }

    private static string InferSupportProfile(SupportComponent support)
    {
        var text = support.SupportType?.ToLowerInvariant() ?? string.Empty;
        if (text.Contains("unistrut"))
            return Profiles.SupportUnistrut;
        if (text.Contains("wall"))
            return Profiles.SupportWallBracket;
        if (text.Contains("trapeze"))
            return Profiles.SupportTrapeze;
        return Profiles.SupportWallBracket;
    }

    private static string InferTrayProfile(CableTrayComponent tray)
    {
        var text = tray.TrayType?.ToLowerInvariant() ?? string.Empty;
        if (text.Contains("wire") || text.Contains("mesh") || text.Contains("basket"))
            return Profiles.TrayWireMesh;
        if (text.Contains("solid"))
            return Profiles.TraySolidBottom;
        return Profiles.TrayLadder;
    }

    private static string InferHangerProfile(HangerComponent hanger)
    {
        var text = hanger.HangerType?.ToLowerInvariant() ?? string.Empty;
        if (text.Contains("seismic") || text.Contains("brace"))
            return Profiles.HangerSeismicBrace;
        return Profiles.HangerThreadedRod;
    }

    private static void CopyCommon(ElectricalComponent source, ElectricalComponent target)
    {
        target.Name = source.Name;
        target.Type = source.Type;
        target.VisualProfile = GetProfile(source);
        target.Position = new Point3D(source.Position.X, source.Position.Y, source.Position.Z);
        target.Rotation = new Vector3D(source.Rotation.X, source.Rotation.Y, source.Rotation.Z);
        target.Scale = new Vector3D(source.Scale.X, source.Scale.Y, source.Scale.Z);
        target.Parameters = new ComponentParameters
        {
            Width = source.Parameters.Width,
            Height = source.Parameters.Height,
            Depth = source.Parameters.Depth,
            CatalogWidth = source.Parameters.CatalogWidth ?? source.Parameters.Width,
            CatalogHeight = source.Parameters.CatalogHeight ?? source.Parameters.Height,
            CatalogDepth = source.Parameters.CatalogDepth ?? source.Parameters.Depth,
            Material = source.Parameters.Material,
            Elevation = source.Parameters.Elevation,
            Color = source.Parameters.Color,
            Manufacturer = source.Parameters.Manufacturer,
            PartNumber = source.Parameters.PartNumber,
            ReferenceUrl = source.Parameters.ReferenceUrl
        };
        target.Constraints = source.Constraints.ToList();
        target.LayerId = source.LayerId;
    }

    private static ConduitComponent CloneConduit(ConduitComponent source)
    {
        var clone = new ConduitComponent
        {
            Diameter = source.Diameter,
            Length = source.Length,
            ConduitType = source.ConduitType,
            BendRadius = source.BendRadius,
            BendType = source.BendType
        };
        clone.BendPoints = source.BendPoints
            .Select(p => new Point3D(p.X, p.Y, p.Z))
            .ToList();
        return clone;
    }

    private static BoxComponent CloneBox(BoxComponent source)
    {
        return new BoxComponent
        {
            KnockoutCount = source.KnockoutCount,
            BoxType = source.BoxType
        };
    }

    private static PanelComponent ClonePanel(PanelComponent source)
    {
        return new PanelComponent
        {
            CircuitCount = source.CircuitCount,
            Amperage = source.Amperage,
            PanelType = source.PanelType
        };
    }

    private static SupportComponent CloneSupport(SupportComponent source)
    {
        return new SupportComponent
        {
            LoadCapacity = source.LoadCapacity,
            SupportType = source.SupportType
        };
    }

    private static CableTrayComponent CloneCableTray(CableTrayComponent source)
    {
        var clone = new CableTrayComponent
        {
            TrayWidth = source.TrayWidth,
            TrayDepth = source.TrayDepth,
            Length = source.Length,
            TrayType = source.TrayType
        };
        clone.PathPoints = source.PathPoints
            .Select(p => new Point3D(p.X, p.Y, p.Z))
            .ToList();
        return clone;
    }

    private static HangerComponent CloneHanger(HangerComponent source)
    {
        return new HangerComponent
        {
            RodDiameter = source.RodDiameter,
            RodLength = source.RodLength,
            HangerType = source.HangerType,
            LoadCapacity = source.LoadCapacity
        };
    }
}
