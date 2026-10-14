# SysGlance

<p align="center">
  Live PC stats on your taskbar, your desktop, and your Corsair Xeneon Edge.
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
  <a href="https://ko-fi.com/frskdevelopment">
    <img src="https://img.shields.io/badge/Support-Ko--fi-FF5E5B?style=for-the-badge&logo=kofi&logoColor=white" alt="Support on Ko-fi" />
  </a>
</p>

## About

SysGlance reads your hardware with LibreHardwareMonitor and, when it's running, MSI Afterburner. It shows the readings you pick in up to three places:

- **Taskbar overlay**: a compact line of stats docked to your taskbar.
- **Desktop widgets**: floating panels of gauge cards on any monitor.
- **Xeneon Edge** (optional): the SysGlance iCUE widget on Corsair's touchscreen.

## Features

### Sensors

- More than 100 sensors: CPU and GPU temperature, usage, clocks, power and fans; per-core CPU readings; VRAM; RAM; FPS with 1% and 0.1% lows; frame time; and network download and upload speed.
- Pick which sensors show, their order, and a colour for each.
- Mini graphs of the last 1, 5 or 15 minutes, which you can turn on or off for each sensor.

### Desktop widgets

- As many widgets as you like, each with its own sensors, colours, card style (gauges or numbers), spacing and graphs.
- Clicks pass through them, so they never get in the way. Use **Edit layout** to move and resize them.
- They snap to screen edges and to each other while you move them. Hold Ctrl to place one freely.
- Optionally always on top. They hide on a monitor while a full-screen game or video runs there.

### Xeneon Edge (optional)

- A native iCUE widget that shows your SysGlance sensors, with mini graphs.
- Secure pairing: press **Connect** on the Edge and confirm the matching code on your PC.
- On the Edge, hold a card and drag to reorder, or hold it still to show or hide cards. Your layout is saved in SysGlance.
- The widget also works on its own with iCUE's sensors, without SysGlance.

### App

- One-click updates: SysGlance tells you when a new version is out and installs it for you.
- Automatic settings backups from the last 10 starts, plus backup and restore to a file.
- Start with Windows, and a guided tour the first time you open it.
- Uninstall from inside the app, with the option to keep your settings.

## Requirements

- Windows 10 or 11, 64-bit.
- Administrator rights: Windows asks when SysGlance starts, because reading hardware sensors needs them.
- Optional: [MSI Afterburner](https://www.msi.com/Landing/afterburner/graphics-cards) running, for FPS, frame time and extra GPU sensors.
- Optional, for the Xeneon Edge: iCUE 5.44 or later.

## Download

1. Open the [latest release](https://github.com/Frsk-Dev/SysGlance/releases/latest).
2. Download `SysGlance.exe`.
3. Save it anywhere on your PC, for example `C:\Tools\SysGlance\`.

There's no installer. After the first download, SysGlance updates itself.

## Use

1. Run `SysGlance.exe`. The first time, Settings opens with a short tour.
2. After that, open **Settings** from the SysGlance icon in the system tray.
3. Choose your sensors, their order and colours, and which ones get a mini graph.
4. To add a desktop widget, click **+ Add widget** in Settings, then **Edit layout** to place it.
5. Startup, updates, backups and uninstall are under the ⚙ button next to the close button in Settings.

### Set up the Xeneon Edge

1. Click **Get the iCUE widget** in Settings, or download `SysGlance.icuewidget` from the latest release. Double-click it to add it to iCUE.
2. Put the widget on your Edge and set its **Data Source** to *SysGlance App* or *Auto*.
3. Press **Connect** on the Edge, then press **Trust** in SysGlance when the codes match.
4. If it can't connect, allow `localhost:5123` for SysGlance in **iCUE Settings → Widget Access**.

## Privacy

SysGlance doesn't send your data anywhere. The Xeneon Edge widget connects only to SysGlance on your own PC (`localhost`), and only after you trust it. The only internet request is the update check, which asks GitHub for the latest release.

## Tech stack

- C# / .NET 8 (`net8.0-windows`)
- WPF + Windows Forms integration
- LibreHardwareMonitor (`LibreHardwareMonitorLib`)
- MSI Afterburner shared memory integration

## Support

SysGlance is free. If you find it useful, you can [buy me a coffee on Ko-fi](https://ko-fi.com/frskdevelopment). Bug reports and ideas are welcome in [Issues](https://github.com/Frsk-Dev/SysGlance/issues).
