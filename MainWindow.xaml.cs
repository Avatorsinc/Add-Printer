using System;
using System.Collections.ObjectModel;
using System.DirectoryServices;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using MaterialDesignThemes.Wpf;

namespace Add_Printer
{
    public partial class MainWindow : Window
    {
        private readonly PaletteHelper _paletteHelper = new();

        private readonly string _domain = "dsg.dk";
        private readonly string _username = "ServiceAccount";
        private readonly string _password = "Password";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Info_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Add Printer App v1.0\n\n" +
                "Search for on-prem AD printers and install them to your PC.\n" +
                "Right-click a printer to connect or install drivers.\n" +
                "Light/Dark toggle in the top-right corner.",
                "About Add Printer",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private void SwitchTheme_Click(object sender, RoutedEventArgs e)
        {
            var theme = _paletteHelper.GetTheme();
            theme.SetBaseTheme(theme.GetBaseTheme() == BaseTheme.Dark
                ? BaseTheme.Light
                : BaseTheme.Dark);
            _paletteHelper.SetTheme(theme);
        }

        private void SearchPrinters_Click(object sender, RoutedEventArgs e)
        {
            var query = SearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show(
                    "Please enter a printer name to search.",
                    "Information",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            var printers = new ObservableCollection<PrinterInfo>();
            try
            {
                string ldapPath = $"LDAP://{_domain}/DC=dsg,DC=dk";
                using var entry = new DirectoryEntry(ldapPath, $"{_domain}\\{_username}", _password);
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectCategory=printQueue)(name=*{query}*))"
                };
                searcher.PropertiesToLoad.Add("name");
                searcher.PropertiesToLoad.Add("serverName");
                searcher.PropertiesToLoad.Add("printShareName");

                foreach (SearchResult res in searcher.FindAll())
                {
                    string name = res.Properties["name"][0].ToString();
                    string server = res.Properties.Contains("serverName")
                        ? res.Properties["serverName"][0].ToString()
                        : null;
                    string share = res.Properties.Contains("printShareName")
                        ? res.Properties["printShareName"][0].ToString()
                        : name;
                    string unc = server != null
                        ? $@"\\{server}\{share}"
                        : share;

                    printers.Add(new PrinterInfo
                    {
                        DisplayName = name,
                        UncPath = unc
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error searching printers: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }

            PrintersListView.ItemsSource = printers;
        }

        // Handles the double-click on the ListView
        private void PrintersListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConnectPrinter();
        }

        // Handles the ContextMenu "Connect Printer" click
        private void ConnectPrinterMenu_Click(object sender, RoutedEventArgs e)
        {
            ConnectPrinter();
        }

        // Shared logic to add the printer for the current user
        private void ConnectPrinter()
        {
            if (!(PrintersListView.SelectedItem is PrinterInfo pi)) return;
            try
            {
                dynamic network = Activator.CreateInstance(
                    Type.GetTypeFromProgID("WScript.Network"));
                network.AddWindowsPrinterConnection(pi.UncPath);
                network.SetDefaultPrinter(pi.UncPath);

                MessageBox.Show(
                    $"Printer '{pi.DisplayName}' added for the current user.",
                    "Printer Added",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to add printer: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        // ContextMenu "Install Drivers…" click
        private void InstallDrivers_Click(object sender, RoutedEventArgs e)
        {
            if (!(PrintersListView.SelectedItem is PrinterInfo pi))
            {
                MessageBox.Show("Please select a printer first.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Filter = "Printer INF|*.inf",
                Title = "Select Printer Driver INF"
            };
            if (dlg.ShowDialog() != true) return;

            string infPath = dlg.FileName;
            string driverName = Path.GetFileNameWithoutExtension(infPath);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"printui.dll,PrintUIEntry /ia /m \"{driverName}\" /f \"{infPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                })?.WaitForExit();

                var result = MessageBox.Show(
                    $"Driver '{driverName}' installed successfully.\nConnect printer '{pi.DisplayName}' now?",
                    "Driver Installed",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                    ConnectPrinter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to install driver: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }
}
