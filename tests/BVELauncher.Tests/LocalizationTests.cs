// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Xml.Linq;

namespace BVELauncher.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void JapaneseAndEnglishResourceKeysMatchAndAreNonempty()
    {
        string directory = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(directory, "BVELauncher.sln"))) directory = Path.GetDirectoryName(directory) ?? throw new DirectoryNotFoundException();
        string strings = Path.Combine(directory, "src", "BVELauncher.App", "Strings");
        var ja = Read("ja-JP"); var en = Read("en-US");
        Assert.Equal(ja.Keys.Order(), en.Keys.Order()); Assert.True(ja.Count > 250);
        Assert.All(ja.Values.Concat(en.Values), value => Assert.False(string.IsNullOrWhiteSpace(value)));
        Assert.Equal("BVELauncher", ja["ProductDisplayName"]);
        Dictionary<string, string> Read(string language) => XDocument.Load(Path.Combine(strings, language, "Resources.resw")).Root!.Elements("data").ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")!.Value);
    }
}
