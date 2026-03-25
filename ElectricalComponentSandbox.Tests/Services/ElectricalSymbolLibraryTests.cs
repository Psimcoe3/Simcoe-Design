using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ElectricalSymbolLibraryTests
{
    private readonly ElectricalSymbolLibrary _lib = new();

    [Fact]
    public void Library_HasAtLeast20Symbols()
    {
        Assert.True(_lib.Symbols.Count >= 20);
    }

    [Fact]
    public void GetSymbol_ExistingName_ReturnsSymbol()
    {
        var sym = _lib.GetSymbol("Duplex Receptacle");
        Assert.NotNull(sym);
        Assert.Equal("Duplex Receptacle", sym!.Name);
    }

    [Fact]
    public void GetSymbol_CaseInsensitive()
    {
        var sym = _lib.GetSymbol("duplex receptacle");
        Assert.NotNull(sym);
    }

    [Fact]
    public void GetSymbol_UnknownName_ReturnsNull()
    {
        Assert.Null(_lib.GetSymbol("Nonexistent Widget"));
    }

    [Fact]
    public void GetByCategory_Receptacles_ReturnsMultiple()
    {
        var receptacles = _lib.GetByCategory("Receptacles");
        Assert.True(receptacles.Count >= 4);
        Assert.All(receptacles, s => Assert.Equal("Receptacles", s.Category));
    }

    [Fact]
    public void GetCategories_ReturnsDistinctCategories()
    {
        var categories = _lib.GetCategories();
        Assert.True(categories.Count >= 5);
        Assert.Contains("Receptacles", categories);
        Assert.Contains("Switches", categories);
        Assert.Contains("Lighting", categories);
        Assert.Contains("Distribution", categories);
        Assert.Contains("Fire Alarm", categories);
    }

    [Fact]
    public void AllSymbols_HaveNonEmptyPrimitives()
    {
        foreach (var kvp in _lib.Symbols)
        {
            Assert.True(kvp.Value.Primitives.Count > 0,
                $"Symbol '{kvp.Key}' has no primitives");
        }
    }

    [Fact]
    public void AllSymbols_HaveNameAndCategory()
    {
        foreach (var kvp in _lib.Symbols)
        {
            Assert.False(string.IsNullOrEmpty(kvp.Value.Name),
                $"Symbol has empty name");
            Assert.False(string.IsNullOrEmpty(kvp.Value.Category),
                $"Symbol '{kvp.Value.Name}' has empty category");
        }
    }
}
