using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.DirectoryServices;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using MaterialDesignThemes.Wpf;
namespace Add_Printer
{
    public partial class MainWindow : Window
    {
        private readonly PaletteHelper _paletteHelper = new();

        private const string Domain = "dsg.dk";
        private const string Username = "ServiceAccount";
        private const string Password = "Password";

        public MainWindow()
        {
            InitializeComponent();
            string sysCult = CultureInfo.CurrentUICulture.Name;
            var items = LanguageComboBox.Items.OfType<ComboBoxItem>();
            var match = items.FirstOrDefault(i => (string)i.Tag == sysCult)
                     ?? items.FirstOrDefault(i => ((string)i.Tag).StartsWith(sysCult.Split('-')[0], StringComparison.OrdinalIgnoreCase));
            LanguageComboBox.SelectedItem = match ?? LanguageComboBox.Items[0];
        }

        private void Info_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show(
                "Add Printer App v1.0\n\n" +
                "Search by printer name and location.\n" +
                "Right-click to connect or install drivers.\n" +
                "Light/Dark toggle in the top-right corner.",
                "About Add Printer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

        private void SwitchTheme_Click(object sender, RoutedEventArgs e)
        {
            var theme = _paletteHelper.GetTheme();
            theme.SetBaseTheme(
                theme.GetBaseTheme() == BaseTheme.Dark
                    ? BaseTheme.Light
                    : BaseTheme.Dark);
            _paletteHelper.SetTheme(theme);
        }

        private void SearchPrinters_Click(object sender, RoutedEventArgs e)
        {
            string nameQuery = NameSearchTextBox.Text.Trim();
            string locationQuery = LocationSearchTextBox.Text.Trim();

            if (string.IsNullOrEmpty(nameQuery) && string.IsNullOrEmpty(locationQuery))
            {
                MessageBox.Show("Please enter a name or location to search.",
                                "Information",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            var printers = new ObservableCollection<PrinterInfo>();
            try
            {
                string ldapPath = $"LDAP://{Domain}/DC=dsg,DC=dk";
                using var entry = new DirectoryEntry(ldapPath, $"{Domain}\\{Username}", Password);
                using var searcher = new DirectorySearcher(entry);
                var filters = new List<string> { "(objectCategory=printQueue)" };
                if (!string.IsNullOrEmpty(nameQuery))
                    filters.Add($"(name=*{nameQuery}*)");
                if (!string.IsNullOrEmpty(locationQuery))
                    filters.Add($"(location=*{locationQuery}*)");
                searcher.Filter = "(&" + string.Concat(filters) + ")";
                searcher.PropertiesToLoad.Add("name");
                searcher.PropertiesToLoad.Add("location");
                searcher.PropertiesToLoad.Add("description");
                searcher.PropertiesToLoad.Add("serverName");
                searcher.PropertiesToLoad.Add("printShareName");

                foreach (SearchResult res in searcher.FindAll())
                {
                    string name = res.Properties["name"][0].ToString();
                    string location = res.Properties.Contains("location")
                                      ? res.Properties["location"][0].ToString()
                                      : string.Empty;
                    string comment = res.Properties.Contains("description")
                                      ? res.Properties["description"][0].ToString()
                                      : string.Empty;

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
                        Location = location,
                        Comment = comment,
                        UncPath = unc
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching printers: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }

            PrintersListView.ItemsSource = printers;
        }

        private void PrintersListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
            => ConnectPrinter();

        private void ConnectPrinterMenu_Click(object sender, RoutedEventArgs e)
            => ConnectPrinter();

        private void ConnectPrinter()
        {
            if (!(PrintersListView.SelectedItem is PrinterInfo pi)) return;

            try
            {
                dynamic network = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Network"));
                network.AddWindowsPrinterConnection(pi.UncPath);
                network.SetDefaultPrinter(pi.UncPath);

                MessageBox.Show($"Printer '{pi.DisplayName}' added for the current user.",
                                "Printer Added",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add printer: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void InstallDrivers_Click(object sender, RoutedEventArgs e)
        {
            if (!(PrintersListView.SelectedItem is PrinterInfo pi))
            {
                MessageBox.Show("Please select a printer first.",
                                "Information",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
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
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                    ConnectPrinter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to install driver: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item)
            {
                string culture = item.Tag?.ToString() ?? "en-US";
                var dictUri = new Uri($"Resources/Strings.{culture}.xaml", UriKind.Relative);
                var dict = new ResourceDictionary { Source = dictUri };

                var merged = Application.Current.Resources.MergedDictionaries;
                for (int i = merged.Count - 1; i >= 0; i--)
                {
                    var md = merged[i];
                    if (md.Source != null && md.Source.OriginalString.Contains("Resources/Strings."))
                        merged.Remove(md);
                }

                merged.Add(dict);
            }
        }
    }
}
