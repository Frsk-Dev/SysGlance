# SysGlance

<p align="center">
  Lightweight Windows taskbar overlay for live system metrics.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8" />
  <img src="https://img.shields.io/badge/WPF-UI-0C54C2?style=for-the-badge&logo=windows&logoColor=white" alt="WPF" />
  <img src="https://img.shields.io/badge/Windows-Win64-0078D6?style=for-the-badge&logo=windows11&logoColor=white" alt="Windows x64" />
  <img src="https://img.shields.io/badge/LibreHardwareMonitor-0.9.5-1F6FEB?style=for-the-badge" alt="LibreHardwareMonitor" />
</p>

<p align="center">
  <a href="https://github.com/Frsk-Dev/SysGlance/releases/latest">
    <img src="https://img.shields.io/badge/Download-Latest%20Release-2EA043?style=for-the-badge&logo=github&logoColor=white" alt="Download Latest Release" />
  </a>
</p>

## About

SysGlance displays real-time PC stats directly in a clean, customizable taskbar overlay.

- Live monitoring for CPU, GPU, RAM, FPS, and network download/upload speed
- Sensor visibility, ordering, and color customization
- Mini graphs on cards (last 1, 5, or 15 minutes), switchable per sensor
- Desktop widgets: floating, resizable sensor panels with their own sensors and look; click-through, optional always-on-top, snap to screen edges and each other, auto-hide on monitors running a full-screen app
- Optional: show your sensors on a Corsair Xeneon Edge with the SysGlance iCUE widget (secure pairing; hold a card to drag it, or hold it still to show or hide cards)
- One-click updates, automatic settings backups, and a guided tour on first start

## Download

1. Open the repository **Releases** page.
2. Download the latest `SysGlance.exe` asset.
3. Save it anywhere on your PC (for example `C:\Tools\SysGlance\`).

After that, SysGlance tells you when a new version is out and updates itself in one click.

## Use

1. Run `SysGlance.exe`. The first time, Settings opens with a short tour.
2. Later, open **Settings** from the tray icon.
3. Choose your sensors, order, colors, and which ones get a mini graph.
4. Startup, updates, backups, and uninstall are under the ⚙ button next to the close button in Settings.

### Xeneon Edge (optional)

1. Click **Get the iCUE widget** in Settings (or download `SysGlance.icuewidget` from the latest release) and double-click it to add it to iCUE 5.44 or later.
2. Put the widget on your Edge and set its **Data Source** to *SysGlance App* or *Auto*.
3. Press **Connect** on the Edge, then press **Trust** in SysGlance when the codes match.
4. If it can't connect, allow `localhost:5123` for SysGlance in **iCUE Settings → Widget Access**.

## Tech Stack

- C# / .NET 8 (`net8.0-windows`)
- WPF + Windows Forms integration
- LibreHardwareMonitor (`LibreHardwareMonitorLib`)
- MSI Afterburner shared memory integration
