using System.Text;
using BVELauncher.Bve;
using BVELauncher.Core;
namespace BVELauncher.Tests;
public sealed class ParserTests
{
    [Theory]
    [InlineData("utf-8", false, "\r\n")]
    [InlineData("utf-8", true, "\n")]
    [InlineData("shift_jis", false, "\r\n")]
    [InlineData("windows-1252", false, "\n")]
    [InlineData("utf-16", true, "\r\n")]
    public void RoundTripPreservesEveryByte(string encodingName, bool bom, string newline)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(encodingName);
        var text = string.Join(newline, new[] { "BveTs Scenario 2.00:" + encodingName, "", "; keep", "tItLe = Test # tail", "UnknownKey = opaque", "Vehicle = ", "" });
        var bytes = (bom ? encoding.GetPreamble() : []).Concat(encoding.GetBytes(text)).ToArray();
        var document = BveDocument.Parse(bytes);
        Assert.Equal(bytes, document.ToBytes());
        Assert.Equal("Test", document.Get("Title"));
        Assert.Equal("opaque", document.Get("UnknownKey"));
        Assert.Equal("", document.Get("Vehicle"));
    }
    [Fact]
    public void OverridePreservesCommentsOrderCasingAndUnknownFields()
    {
        var document = BveDocument.Parse(Encoding.UTF8.GetBytes("BveTs Scenario 2.00\n# top\nvEhIcLe  =  old.txt  ; tail\nOpaque = keep\nRoute = map.txt\n"));
        document.Set("Vehicle", "new.txt");
        document.Set("VehicleTitle", "車両");
        var text = Encoding.UTF8.GetString(document.ToBytes());
        Assert.Contains("vEhIcLe  =  new.txt  ; tail\nOpaque = keep", text);
        Assert.Equal("map.txt", document.Get("Route"));
        Assert.Equal("車両", document.Get("VehicleTitle"));
    }
    [Fact]
    public void LegacyCp932WithoutHeaderEncodingIsReadable()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var document = BveDocument.Parse(Encoding.GetEncoding(932).GetBytes("BveTs Scenario 2.00\r\nTitle = 日本語\r\n"));
        Assert.Equal("日本語", document.Get("Title"));
        Assert.Equal(932, document.Encoding.CodePage);
    }
    [Fact]
    public void WeightedReferencesRetainAllCandidates()
        => Assert.Equal(new[] { "晴れ.txt", "雨.txt" }, BveAssetReader.WeightedPaths("晴れ.txt * 2.5 | 雨.txt"));
    [Fact]
    public void MapScannerHandlesCommentsQuotedPathsAndMultilineStatements()
    {
        var references = MapReferenceScanner.Scan("BveTs Map 2.02\n# Structure.Load('fake');\n// Sound.Load('fake');\nStructure . Load (\n 'a#b.csv'\n); Sound3D.Load('sound.csv'); Train['local'].Load('train.txt'); include 'part.txt';");
        Assert.Equal(4, references.Count);
        Assert.Equal("a#b.csv", references[0].Path);
        Assert.Equal(AssetKind.Train, references[2].Kind);
        Assert.Equal(AssetKind.Map, references[3].Kind);
    }
    [Theory]
    [InlineData("BveTs Vehicle 2.00", AssetKind.Vehicle)]
    [InlineData("BveTs Vehicle Parameters 1.00", AssetKind.Parameters)]
    [InlineData("BveTs Vehicle Sound 3.00", AssetKind.VehicleSound)]
    [InlineData("BveTs Scenario broken", AssetKind.Scenario)]
    public void IdentificationUsesHeader(string header, AssetKind expected)
        => Assert.Equal(expected, BveAssetReader.Identify(header, "arbitrary.dat"));
}
