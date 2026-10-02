// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.Bve;
namespace BVELauncher.Tests;
public sealed class KeyboardPresetTests
{
    [Fact]
    public void MapsNativeTabSeparatedAssignments()
    {
        var changes = KeyboardPresetReader.Read("axis-0-0\t40\r\naxis-3-5\t0\r\nsw-3\t109\r\nats-15\t48\r\nsim-12\t80\r\n");
        Assert.Equal("40", changes["KeyAxis0_0"]);
        Assert.Equal("0", changes["KeyAxis3_5"]);
        Assert.Equal("109", changes["KeySwitch3"]);
        Assert.Equal("48", changes["KeyAts15"]);
        Assert.Equal("80", changes["KeySimulation12"]);
    }
    [Theory]
    [InlineData("axis-4-0\t10")]
    [InlineData("ats-16\t10")]
    [InlineData("sim-0\t-1")]
    [InlineData("sw-0\t65536")]
    [InlineData("sw-0\t1\nsw-0\t2")]
    [InlineData("unknown\t1")]
    public void RejectsInvalidOrAmbiguousPreset(string text) => Assert.Throws<InvalidDataException>(() => KeyboardPresetReader.Read(text));
}
