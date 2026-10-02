// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BVELauncher.App.ViewModels;

public sealed class VehicleOption(VehicleEntry entry) : ObservableObject
{
    public VehicleEntry Entry { get; } = entry;
    public string DisplayName => Entry.DisplayName;
    public string SourceLabel => string.IsNullOrEmpty(Entry.Path) ? "" : Entry.SourceLabel;
}
