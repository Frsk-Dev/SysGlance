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

- Live monitoring for CPU, GPU, RAM, and FPS metrics
- Sensor visibility, ordering, and color customization
- v1.1.0 includes iFrame dashboard support for Corsair Xeneon Edge

## Download

1. Open the repository **Releases** page.
2. Download the latest `SysGlance.exe` asset.
3. Save it anywhere on your PC (for example `C:\Tools\SysGlance\`).

## Use

1. Run `SysGlance.exe`.
2. Open **Settings** from the tray icon.
3. Choose your sensors, order, and colors.
4. Optional: enable the Xeneon Edge iFrame dashboard in settings.

## Tech Stack

- C# / .NET 8 (`net8.0-windows`)
- WPF + Windows Forms integration
- LibreHardwareMonitor (`LibreHardwareMonitorLib`)
- MSI Afterburner shared memory integration
