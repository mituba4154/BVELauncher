// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BVELauncher.App;

public sealed class VehicleRow : UserControl
{
    public VehicleRow()
    {
        var title = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["BodyStrongTextBlockStyle"] };
        var source = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["CaptionTextBlockStyle"] };
        var layout = new StackPanel { Spacing = 4, Margin = new(4) }; layout.Children.Add(title); layout.Children.Add(source); Content = layout;
        // コンテナー再利用時にも、現在の候補とファイル名を表示する。
        DataContextChanged += (_, _) =>
        {
            var option = DataContext as VehicleOption;
            title.Text = option?.DisplayName ?? ""; source.Text = option?.SourceLabel ?? "";
            ToolTipService.SetToolTip(this, source.Text);
        };
    }
}
