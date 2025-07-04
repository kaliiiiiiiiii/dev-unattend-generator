using System.Xml;
using DiscUtils.Iso9660;

namespace Schneegans.Unattend;

class Generate
{
    public static void Main(string[] args)
    {
        var outputDir = EnsureOutputDirectory();
        var generator = new UnattendGenerator();

        var xml = generator.GenerateXml(GenerateSettings(generator));
        var xmlPath = WriteXmlFile(xml, outputDir);
        CreateIso(outputDir, xmlPath);
    }

    static string EnsureOutputDirectory()
    {
        string outputDir = Path.Combine(Environment.CurrentDirectory, "out");
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
        return outputDir;
    }

    static Configuration GenerateSettings(UnattendGenerator generator){
        // checkout https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/Main.cs
        return Configuration.Default with
        {
            BypassRequirementsCheck = true,
            BypassNetworkCheck = true, // installation fails if this is true (https://github.com/kaliiiiiiiiii/unattend-generator/issues/2)
        };
    }

    static string WriteXmlFile(XmlDocument xml, string outputDir)
    {
        string path = Path.Combine(outputDir, "autounattend.xml");
        File.WriteAllBytes(path, UnattendGenerator.Serialize(xml));
        return path;
    }

    static void CreateIso(string outputDir, string xmlPath)
    {
        string isoPath = Path.Combine(outputDir, "devwin.iso");

        using FileStream isoStream = File.Create(isoPath);
        CDBuilder builder = new CDBuilder
        {
            UseJoliet = true,
            VolumeIdentifier = "DEVWIN"
        };

        builder.AddFile("autounattend.xml", xmlPath);
        builder.Build(isoStream);
    }
}
