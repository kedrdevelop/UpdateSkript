using System;
using System.Linq;
using System.Xml.Linq;

namespace UpdateSkriptApp.Services
{
    public class DellDriverPackInfo
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public static class DellCatalogParser
    {
        public static DellDriverPackInfo? FindMatch(string xmlContent, string model)
        {
            try
            {
                var doc = XDocument.Parse(xmlContent);
                
                // Pure C# logic for matching
                var driverPack = doc.Descendants("DriverPack")
                    .FirstOrDefault(dp => 
                        dp.Descendants("SupportedModel").Any(m => m.Element("Display")?.Value.Contains(model, StringComparison.OrdinalIgnoreCase) == true) &&
                        dp.Attribute("osCode")?.Value == "WT64A" // Windows 10/11 x64
                    );

                if (driverPack != null)
                {
                    string path = driverPack.Attribute("path")?.Value ?? "";
                    return new DellDriverPackInfo 
                    { 
                        Path = path,
                        Name = System.IO.Path.GetFileName(path)
                    };
                }
            }
            catch { /* Parsing error */ }

            return null;
        }
    }
}
