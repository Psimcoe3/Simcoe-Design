using System.IO;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class RunScheduleXmlServiceTests
{
    private static RunScheduleConfiguration SampleConfiguration()
    {
        var cfg = new RunScheduleConfiguration
        {
            MaximumWireFill = 0.4,
            WireDescriptionFormat = WireDescriptionFormat.Hyphen,
            StatusSetOnAssignRun = "Not Used",
        };
        cfg.WireSpecifications.Add(new WireSpecification
        {
            Name = "1P3W-CU",
            MaterialName = "Copper",
            FeederId = "1P3W-CU-100A",
            Amperage = 100,
            PhaseSize = "1",
            PhaseQuantity = 2,
            NeutralSize = "1",
            NeutralQuantity = 1,
            GroundSize = "6",
            GroundQuantity = 1,
            IsoGroundSize = "",
            IsoGroundQuantity = 0,
            ParallelQuantity = 1,
            ConduitSizeFeet = 0.125,
        });
        cfg.WireSpecifications.Add(new WireSpecification
        {
            Name = "1P3W-CU",
            MaterialName = "Copper",
            FeederId = "1P3W-CU-200A",
            Amperage = 200,
            PhaseSize = "3/0",
            PhaseQuantity = 2,
            NeutralSize = "3/0",
            NeutralQuantity = 1,
            GroundSize = "6",
            GroundQuantity = 1,
            ParallelQuantity = 1,
            ConduitSizeFeet = 0.16666666666666669,
        });
        cfg.WireSizes.Add(new WireSizeEntry
        {
            MaterialName = "Copper", Insulation = "THHN", Ampacity = 15, Gauge = "14",
            DiameterFeet = 0.0090833333333333339,
        });
        cfg.WireSizes.Add(new WireSizeEntry
        {
            MaterialName = "Copper", Insulation = "THHN", Ampacity = 100, Gauge = "1",
            DiameterFeet = 0.040 / 1.0,
        });
        return cfg;
    }

    [Fact]
    public void Serialize_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RunScheduleXmlService.Serialize(null!));
    }

    [Fact]
    public void Deserialize_BlankXml_Throws()
    {
        Assert.Throws<ArgumentException>(() => RunScheduleXmlService.Deserialize(""));
    }

    [Fact]
    public void Deserialize_WrongRoot_ThrowsInvalidData()
    {
        Assert.Throws<InvalidDataException>(() => RunScheduleXmlService.Deserialize("<Other/>"));
    }

    [Fact]
    public void Deserialize_MalformedXml_ThrowsInvalidData()
    {
        Assert.Throws<InvalidDataException>(() => RunScheduleXmlService.Deserialize("<unterminated"));
    }

    [Fact]
    public void Serialize_HasParameterPushDataRoot()
    {
        var xml = RunScheduleXmlService.Serialize(SampleConfiguration());

        Assert.Contains("<ParameterPushData", xml);
        Assert.Contains("xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"", xml);
        Assert.Contains("xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"", xml);
    }

    [Fact]
    public void Serialize_EmitsWireSpecificationDataInOrder()
    {
        var xml = RunScheduleXmlService.Serialize(SampleConfiguration());

        int spec100A = xml.IndexOf("1P3W-CU-100A", StringComparison.Ordinal);
        int spec200A = xml.IndexOf("1P3W-CU-200A", StringComparison.Ordinal);
        Assert.True(spec100A > 0);
        Assert.True(spec200A > 0);
        Assert.True(spec100A < spec200A, "specs should appear in insertion order");
    }

    [Fact]
    public void Roundtrip_PreservesWireSpecifications()
    {
        var original = SampleConfiguration();

        var xml = RunScheduleXmlService.Serialize(original);
        var roundtripped = RunScheduleXmlService.Deserialize(xml);

        Assert.Equal(original.WireSpecifications.Count, roundtripped.WireSpecifications.Count);
        for (int i = 0; i < original.WireSpecifications.Count; i++)
        {
            var a = original.WireSpecifications[i];
            var b = roundtripped.WireSpecifications[i];
            Assert.Equal(a.Name, b.Name);
            Assert.Equal(a.FeederId, b.FeederId);
            Assert.Equal(a.Amperage, b.Amperage, 6);
            Assert.Equal(a.PhaseSize, b.PhaseSize);
            Assert.Equal(a.PhaseQuantity, b.PhaseQuantity);
            Assert.Equal(a.NeutralSize, b.NeutralSize);
            Assert.Equal(a.GroundSize, b.GroundSize);
            Assert.Equal(a.GroundQuantity, b.GroundQuantity);
            Assert.Equal(a.ParallelQuantity, b.ParallelQuantity);
            Assert.Equal(a.ConduitSizeFeet, b.ConduitSizeFeet, 12);
        }
    }

    [Fact]
    public void Roundtrip_PreservesWireSizesAndHighlights()
    {
        var original = SampleConfiguration();
        original.SizeMismatch.HighlightColorValue = "Cyan";
        original.SizeMismatch.Enabled = false;

        var xml = RunScheduleXmlService.Serialize(original);
        var roundtripped = RunScheduleXmlService.Deserialize(xml);

        Assert.Equal(original.WireSizes.Count, roundtripped.WireSizes.Count);
        Assert.Equal("Cyan", roundtripped.SizeMismatch.HighlightColorValue);
        Assert.False(roundtripped.SizeMismatch.Enabled);
        Assert.Equal(0.4, roundtripped.MaximumWireFill, 6);
        Assert.Equal(WireDescriptionFormat.Hyphen, roundtripped.WireDescriptionFormat);
    }

    [Fact]
    public void Roundtrip_ToFile_PreservesConfiguration()
    {
        var path = Path.Combine(Path.GetTempPath(), $"run-schedule-{Guid.NewGuid():N}.xml");
        try
        {
            var original = SampleConfiguration();
            RunScheduleXmlService.SerializeToFile(original, path);

            Assert.True(File.Exists(path));
            var loaded = RunScheduleXmlService.DeserializeFromFile(path);

            Assert.Equal(original.WireSpecifications.Count, loaded.WireSpecifications.Count);
            Assert.Equal(original.WireSpecifications[0].FeederId, loaded.WireSpecifications[0].FeederId);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void DeserializeFromFile_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            RunScheduleXmlService.DeserializeFromFile(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.xml")));
    }

    [Fact]
    public void EvolveTemplateFormat_DeserializesIntoFullLibrary()
    {
        // Use a representative snippet of the eVolve template so we exercise
        // the exact element / casing the real file emits.
        string xml = """
<?xml version="1.0" encoding="utf-16"?>
<ParameterPushData xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RunData />
  <SystemColorsEnabled>true</SystemColorsEnabled>
  <SystemColors />
  <RunIdNotDefined><Enabled>true</Enabled><HighlightColorValue>Red</HighlightColorValue></RunIdNotDefined>
  <RunIdNotAssigned><Enabled>true</Enabled><HighlightColorValue>Orange</HighlightColorValue></RunIdNotAssigned>
  <SizeMismatch><Enabled>true</Enabled><HighlightColorValue>Green</HighlightColorValue></SizeMismatch>
  <StartMismatch><Enabled>false</Enabled><HighlightColorValue>Blue</HighlightColorValue></StartMismatch>
  <FinishMismatch><Enabled>false</Enabled><HighlightColorValue>Purple</HighlightColorValue></FinishMismatch>
  <TypeMismatch><Enabled>false</Enabled><HighlightColorValue>Cyan</HighlightColorValue></TypeMismatch>
  <SystemMismatch><Enabled>false</Enabled><HighlightColorValue>Fuchsia</HighlightColorValue></SystemMismatch>
  <AutomaticallyPushRunInfo>true</AutomaticallyPushRunInfo>
  <MaximumWireFill>0.4</MaximumWireFill>
  <WireDescriptionFormat>Hyphen</WireDescriptionFormat>
  <RunStatuses />
  <WireSpecifications>
    <WireSpecificationData>
      <Name>1P3W-CU</Name>
      <MaterialName>Copper</MaterialName>
      <FeederId>1P3W-CU-100A</FeederId>
      <Amperage>100</Amperage>
      <PhaseSize>1</PhaseSize>
      <PhaseQuantity>2</PhaseQuantity>
      <NeutralSize>1</NeutralSize>
      <NeutralQuantity>1</NeutralQuantity>
      <GroundSize>6</GroundSize>
      <GroundQuantity>1</GroundQuantity>
      <IsoGroundSize />
      <IsoGroundQuantity>0</IsoGroundQuantity>
      <ParallelQuantity>1</ParallelQuantity>
      <ConduitSize>0.125</ConduitSize>
    </WireSpecificationData>
  </WireSpecifications>
  <WireSizes>
    <WireSizeData>
      <MaterialName>Copper</MaterialName>
      <Insulation>THHN</Insulation>
      <Ampacity>15</Ampacity>
      <Gauge>14</Gauge>
      <Diameter>0.0090833333333333339</Diameter>
    </WireSizeData>
  </WireSizes>
  <StatusSetOnAssignRun>Not Used</StatusSetOnAssignRun>
</ParameterPushData>
""";

        var config = RunScheduleXmlService.Deserialize(xml);

        Assert.True(config.SystemColorsEnabled);
        Assert.True(config.AutomaticallyPushRunInfo);
        Assert.Equal(0.4, config.MaximumWireFill, 6);
        Assert.Equal(WireDescriptionFormat.Hyphen, config.WireDescriptionFormat);
        Assert.True(config.RunIdNotDefined.Enabled);
        Assert.False(config.StartMismatch.Enabled);

        Assert.Single(config.WireSpecifications);
        var spec = config.WireSpecifications[0];
        Assert.Equal("1P3W-CU", spec.Name);
        Assert.Equal("1P3W-CU-100A", spec.FeederId);
        Assert.Equal(100, spec.Amperage);
        Assert.Equal("1", spec.PhaseSize);
        Assert.Equal(2, spec.PhaseQuantity);
        Assert.Equal("", spec.IsoGroundSize);
        Assert.Equal(0, spec.IsoGroundQuantity);
        Assert.Equal(0.125, spec.ConduitSizeFeet, 6);

        Assert.Single(config.WireSizes);
        Assert.Equal("THHN", config.WireSizes[0].Insulation);
        Assert.Equal("14", config.WireSizes[0].Gauge);
        Assert.Equal("Not Used", config.StatusSetOnAssignRun);
    }
}
