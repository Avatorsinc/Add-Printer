# Add Printer

A simple WPF desktop app for Windows that lets end-users search for and install on-premises Active Directory printers by **name** or **location**, right from a modern Material Design UI. The app supports:

- **Search** by printer name and/or location  
- **One-click connect** (maps and sets default printer for the current user)  
- **Driver installation** via INF file  
- **Light/Dark** theme toggle  
- **Multi-language** UI (English, Polski, Dansk, Deutsch)  

---

## 🚀 Features

- **Search Printers**  
  Query your AD `printQueue` objects using filters on `name` and `location`.  
- **Connect Printer**  
  Uses the `WScript.Network` COM API under the logged-in user context, no extra credentials needed.  
- **Install Drivers**  
  Right-click a result to browse for a `.inf` driver package and install it automatically.  
- **Material Design**  
  Built with [MaterialDesignThemes.Wpf](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) for a sleek, modern look.  
- **Multilingual**  
  Resource dictionaries under `Resources/Strings.*.xaml` with language auto-detection and manual override.  

---

## 📦 Repository Structure

/Add-Printer/ ← WPF application project
├─ Assets/ ← Static images (logo, icons)
├─ Resources/ ← Language resource dictionaries
├─ MainWindow.xaml(.cs) ← UI and code-behind
├─ PrinterInfo.cs ← Simple model for printer entries
├─ App.xaml(.cs) ← Application startup & merged dictionaries
└─ … ← other project files

/AddPrinterInstaller/ ← Visual Studio Setup Project (MSI)
├─ AddPrinterInstaller.vdproj
└─ … ← installer configuration (File System view, shortcuts)

yaml
Copy
Edit

---

## 💻 Prerequisites

- **Windows 10 or higher**  
- **.NET 8.0 Desktop Runtime** (bundled in the installer or install separately)  
- **Visual Studio 2022+** (for building from source)  
- **Administrative rights** (to install the MSI or add printers)  

---

## 🔧 Build & Run

1. **Clone the repo**  
   ```bash
   git clone https://github.com/Avatorsinc/Add-Printer.git
   cd add-printer
Open in Visual Studio
Load the Add-Printer.sln solution.

Restore NuGet packages
Visual Studio will auto-restore on load.

Set startup project
Right-click the Add-Printer WPF project → Set as Startup Project.

Build & Run
Press F5 or Ctrl+F5 to launch the app.

🛠️ Packaging as MSI
Open the AddPrinterInstaller setup project.

In the File System editor, right-click Application Folder → Add → Project Output… → Select Add-Printer → Primary Output.

Optionally add content files (e.g. logo.png, .runtimeconfig.json) via Project Output → Content Files.

Create shortcuts under User’s Programs Menu if desired.

Build the installer. The resulting AddPrinterInstaller.msi will bundle the EXE and all required DLLs.

Distribute via Intune, SCCM, or any standard software deployment tool.

🌐 Localization
All UI text lives in resource dictionaries:

Resources/Strings.en-US.xaml

Resources/Strings.pl-PL.xaml

Resources/Strings.da-DK.xaml

Resources/Strings.de-DE.xaml

At startup the app picks your Windows CurrentUICulture. You can also manually override via the language dropdown in the top-right.

🤝 Contributing
Fork the repo

Create a feature branch: git checkout -b feature/YourFeature

Commit your changes: git commit -m "Add amazing feature"

Push to your fork: git push origin feature/YourFeature

Open a Pull Request

Please keep PRs small and focused, and follow the existing C# coding conventions.

📄 License
This project is released under the MIT License. See LICENSE for details.
