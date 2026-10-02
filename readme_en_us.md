# BVELauncher

[Japanese README](README.md) is the authoritative version.

BVELauncher is an unofficial Windows launcher for BVE Trainsim 5.8 and 6.
Download `BVELauncher-win-x64.zip` from the [latest release](https://github.com/mituba4154/BVELauncher/releases/latest), extract the complete ZIP, and run `BVELauncher.App.exe`.
Windows 10 version 2004 or later / Windows 11 x64 is required. The .NET and Windows App SDK runtimes are included. BVE, BveEX and add-ons are separate products and are not included.

## Getting started

1. If BVE is missing, choose **Install BVE** to open its official website and follow the installation instructions. Then choose **Check again for installed BVE**. If you already installed BVE in another location, register `BveTs.exe` in Settings.
2. Standard scenario folders are detected automatically. Use the folder button to add other scenario locations. Large libraries take longer to scan initially; parsed metadata is cached across sessions.
3. Click a scenario to drive. Search by route, vehicle or author and use groups, favorites or list/card views to find scenarios.
4. To change vehicles without modifying the scenario file, use its context menu. Vehicle names and file locations are displayed and support multiple search terms. Save a default vehicle in scenario settings if desired.
5. For keys, graphics and sound, use **Settings → BVE → Open BVE to configure settings**. Load a scenario in BVE, right-click the driving screen and choose Settings. BVE saves these preferences for future sessions; the launcher does not edit them. Expand **Show details** to inspect file locations.

## Importing content and extensions

Drop a ZIP, 7z, folder or scenario file into the library or choose Import. Review the destination and conflicts before confirming. Follow the publisher instructions for required folder layout and dependencies.
Import history and backups are available in Settings.

For BveEX extensions, open the official download page from Settings → BVE, accept the publisher terms yourself, and download the package. Select **Install a downloaded file**, choose the ZIP/7z/DLL, review its destination, then confirm the import.
TsHud requires BveEX 2.1.0 or later. For other DLLs, use **Install another extension DLL** and enter the DLL file name specified by the publisher. Only that DLL is installed; follow the publisher instructions for any additional dependency files.

## Warnings and shortcuts

A warning triangle can indicate missing references. Open diagnostics from the scenario context menu to see details. Warnings are advisory; a scenario can launch if the scenario and BVE executable exist. Some scenarios require separately distributed vehicles or common asset packages.

`Ctrl+F`: search; `Ctrl+R`: rescan; `Ctrl+,`: Settings; `Enter`: drive the selected scenario.
Application data is stored in `%LOCALAPPDATA%\BVELauncher`. Existing files are backed up before import overwrites. Aliases and default vehicles are stored by the launcher.
The launcher does not patch BVE, execute plug-in DLLs, send telemetry or require an online account.

## License

Original code, scripts, UI, documentation and original assets are licensed under [GNU GPL version 3 or any later version](LICENSE), SPDX `GPL-3.0-or-later`, without warranty. See [NOTICE](NOTICE) for scope. Third-party dependencies retain their original licenses in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
Corresponding source and build scripts are available from each release tag.
