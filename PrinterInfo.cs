namespace Add_Printer
{
    public class PrinterInfo
    {
        // Ensure these never end up null
        public string DisplayName { get; set; } = string.Empty;
        public string UncPath { get; set; } = string.Empty;
    }
}
