using System.Xml.Linq;

var path = @"C:\Users\turko\source\repos\Accounting\Accounting.Tests\TestResults\90d9c59f-c835-4ac5-99b2-510daf9ef636\coverage.cobertura.xml";

if (!File.Exists(path))
{
    Console.WriteLine($"File not found: {path}");
    return;
}

var xdoc = XDocument.Load(path);

var packages = xdoc.Descendants("package")
    .Where(p => p.Attribute("name")?.Value.StartsWith("Accounting.Application") == true ||
                p.Attribute("name")?.Value.StartsWith("Accounting.Domain") == true);

long totalLines = 0;
long coveredLines = 0;

foreach (var package in packages)
{
    var classes = package.Descendants("class");
    foreach (var cls in classes)
    {
        var lines = cls.Descendants("line");
        foreach (var line in lines)
        {
            totalLines++;
            if (int.TryParse(line.Attribute("hits")?.Value, out int hits) && hits > 0)
            {
                coveredLines++;
            }
        }
    }
}

Console.WriteLine($"Total Lines (App+Domain): {totalLines}");
Console.WriteLine($"Covered Lines: {coveredLines}");
if (totalLines > 0)
    Console.WriteLine($"Coverage: {(double)coveredLines / totalLines * 100:F2}%");
