using System;
using System.Collections.ObjectModel;
using System.DirectoryServices;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace Add_Printer
{
    public partial class MainWindow : Window
    {
        // Use target-typed new (IDE0090)
        private readonly PaletteHelper _paletteHelper = new();

        // Service account credentials (for searching in dsg.dk domain)
        private readonly string _domain = "dsg.dk";           // Your domain name
        private readonly string _username = "svcAccount";     // Service account username
        private readonly string _password = "Password123";    // Service account password

        public MainWindow()
        {
            InitializeComponent();
        }

        // Info button click handler to show about info
        private void Info_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Add Printer App v1.0\n\n" +
                "Search for on-prem AD printers and install them to your PC.\n" +
                "Light/Dark toggle in the top-right corner.",
                "About Add Printer",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        // Theme toggle button click handler to switch between light and dark modes
        private void SwitchTheme_Click(object sender, RoutedEventArgs e)
        {
            var theme = _paletteHelper.GetTheme();
            theme.SetBaseTheme(
                theme.GetBaseTheme() == BaseTheme.Dark
                    ? BaseTheme.Light
                    : BaseTheme.Dark
            );
            _paletteHelper.SetTheme(theme);
        }

        // Search button click handler to search printers in the "dsg.dk" domain
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
                // Construct the LDAP path to the "dsg.dk" domain
                string ldapPath = $"LDAP://{_domain}/DC=dsg,DC=dk";

                // Bind to the domain using service account credentials
                using var entry = new DirectoryEntry(ldapPath, $"{_domain}\\{_username}", _password);
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectCategory=printQueue)(name=*{query}*))"
                };
                searcher.PropertiesToLoad.Add("name");
                searcher.PropertiesToLoad.Add("serverName");
                searcher.PropertiesToLoad.Add("printShareName");

                // Search for printers and add them to the list
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

            // Set the list of printers to display in the UI
            PrintersListView.ItemsSource = printers;
        }

        // Double-click event to install the selected printer
        private void PrintersListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PrintersListView.SelectedItem is PrinterInfo pi)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        Arguments = $@"printui.dll,PrintUIEntry /in /n""{pi.UncPath}""",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    MessageBox.Show(
                        $"Installing printer: {pi.DisplayName}",
                        "Printer Install",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to install printer: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }
    }
}
