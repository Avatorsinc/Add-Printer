using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.DirectoryServices;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;       // for ComboBoxItem, SelectionChangedEventArgs
using System.Windows.Input;
using Microsoft.Win32;               // for OpenFileDialog
using MaterialDesignThemes.Wpf;      // for PaletteHelper

namespace Add_Printer
{
    // UI and what's behind
    public partial class MainWindow : Window
    {
        // PaletteHelper from MaterialDesign http://materialdesigninxaml.net
        private readonly PaletteHelper _paletteHelper = new();

        public MainWindow()
        {
            InitializeComponent();

            // Automatically pick the user’s current UI language in the dropdown
            string sysCult = CultureInfo.CurrentUICulture.Name;
            var items = LanguageComboBox.Items.OfType<ComboBoxItem>();
            var exactMatch = items.FirstOrDefault(i => (string)i.Tag == sysCult);
            var partialMatch = items.FirstOrDefault(i =>
                ((string)i.Tag).StartsWith(sysCult.Split('-')[0], StringComparison.OrdinalIgnoreCase));
            LanguageComboBox.SelectedItem = exactMatch ?? partialMatch ?? LanguageComboBox.Items[0];
        }

        // Show an “About” dialog when the info button is clicked
        private void Info_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show(
                "Add Printer App v1.0\n\n" +
                "• Search by printer name and location\n" +
                "• Right-click to connect or install drivers\n" +
                "• Toggle light/dark theme from the top-right",
                "About Add Printer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

        // Toggles between light and dark themes
        private void SwitchTheme_Click(object sender, RoutedEventArgs e)
        {
            var theme = _paletteHelper.GetTheme();
            var isDark = theme.GetBaseTheme() == BaseTheme.Dark;
            theme.SetBaseTheme(isDark ? BaseTheme.Light : BaseTheme.Dark);
            _paletteHelper.SetTheme(theme);
        }

        // Fired when the user hits the “Search” button
        private void SearchPrinters_Click(object sender, RoutedEventArgs e)
        {
            // Grab the text from the two search boxes
            string nameQuery = NameSearchTextBox.Text.Trim();
            string locationQuery = LocationSearchTextBox.Text.Trim();

            // If nothing is entered, show a friendly reminder
            if (string.IsNullOrEmpty(nameQuery) && string.IsNullOrEmpty(locationQuery))
            {
                MessageBox.Show("Please enter a name or location to search.",
                                "Information",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            // Search results 
            var printers = new ObservableCollection<PrinterInfo>();

            try
            {
                //
                // 1) Bind to RootDSE (no creds) to discover the defaultNamingContext
                //
                using var root = new DirectoryEntry("LDAP://RootDSE");
                string baseDn = root.Properties["defaultNamingContext"].Value.ToString();

                //
                // 2) Bind to the domain naming context *as the current user*
                //
                using var entry = new DirectoryEntry($"LDAP://{baseDn}")
                {
                    AuthenticationType = AuthenticationTypes.Secure
                };
                using var searcher = new DirectorySearcher(entry);

                //
                // build an LDAP filter that ANDs objectCategory=printQueue,
                // plus name=*…* and/or location=*…* if supplied
                //
                var filters = new List<string> { "(objectCategory=printQueue)" };
                if (!string.IsNullOrEmpty(nameQuery)) filters.Add($"(name=*{nameQuery}*)");
                if (!string.IsNullOrEmpty(locationQuery)) filters.Add($"(location=*{locationQuery}*)");

                searcher.Filter = "(&" + string.Concat(filters) + ")";

                // load all the attributes we need
                foreach (var prop in new[] { "name", "location", "description", "serverName", "printShareName" })
                    searcher.PropertiesToLoad.Add(prop);

                // run the query
                foreach (SearchResult res in searcher.FindAll())
                {
                    string name = res.Properties["name"][0].ToString();
                    string location = res.Properties.Contains("location")
                                      ? res.Properties["location"][0].ToString()
                                      : "";
                    string comment = res.Properties.Contains("description")
                                      ? res.Properties["description"][0].ToString()
                                      : "";

                    // build a UNC share path
                    string server = res.Properties.Contains("serverName")
                                    ? res.Properties["serverName"][0].ToString()
                                    : null;
                    string share = res.Properties.Contains("printShareName")
                                    ? res.Properties["printShareName"][0].ToString()
                                    : name;
                    string uncPath = server != null
                                     ? $@"\\{server}\{share}"
                                     : share;

                    printers.Add(new PrinterInfo
                    {
                        DisplayName = name,
                        Location = location,
                        Comment = comment,
                        UncPath = uncPath
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

            // Show the results in the ListView
            PrintersListView.ItemsSource = printers;
        }

        // Both double-click and the “Connect” menu item use this helper
        private void PrintersListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
            => ConnectPrinter();
        private void ConnectPrinterMenu_Click(object sender, RoutedEventArgs e)
            => ConnectPrinter();

        // Core logic to add the selected printer for the current user
        private void ConnectPrinter()
        {
            if (PrintersListView.SelectedItem is not PrinterInfo pi)
                return;

            try
            {
                dynamic network = Activator.CreateInstance(
                    Type.GetTypeFromProgID("WScript.Network"));
                network.AddWindowsPrinterConnection(pi.UncPath);
                network.SetDefaultPrinter(pi.UncPath);

                MessageBox.Show($"Printer '{pi.DisplayName}' added successfully!",
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

        // Handler for “Install Drivers” in the right-click menu
        private void InstallDrivers_Click(object sender, RoutedEventArgs e)
        {
            if (PrintersListView.SelectedItem is not PrinterInfo pi)
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
                    $"Driver '{driverName}' installed.\nConnect '{pi.DisplayName}' now?",
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

        // Change the UI language when the user picks a new item in the combo box
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item)
            {
                string culture = item.Tag?.ToString() ?? "en-US";
                var dictUri = new Uri($"Resources/Strings.{culture}.xaml", UriKind.Relative);
                var dict = new ResourceDictionary { Source = dictUri };
                var merged = Application.Current.Resources.MergedDictionaries;

                // Remove any old language dictionaries
                for (int i = merged.Count - 1; i >= 0; i--)
                {
                    if (merged[i].Source?.OriginalString.Contains("Resources/Strings.") == true)
                        merged.RemoveAt(i);
                }

                // Load the new one
                merged.Add(dict);
            }
        }
    }
}
