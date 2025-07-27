using System.Xml;
using DiscUtils.Iso9660;

using Schneegans.Unattend;

namespace Generate;

abstract class BaseGenerator {
    protected string TaskbarIconsXml { get; private set; } = "";
    protected string StartPinsJson { get; private set; } = "";
    protected string SystemScript { get; private set; } = "";
    protected string FirstLogonScript { get; private set; } = "";
    protected string UserOnceScript { get; private set; } = "";
    protected string DefaultUserScript { get; private set; } = "";

    public void Run(string? iso) {
        LoadConfigFiles();

        var outputDir = EnsureOutputDirectory();
        var generator = new UnattendGenerator();

        var xml = generator.GenerateXml(GenerateSettings(generator));
        var xmlPath = WriteXmlFile(xml, outputDir);

        string outISO = "out/devwin.iso";
        if (File.Exists(outISO)) { File.Delete(outISO); }
        if (iso != null) {
            using var packer = new IsoPacker(iso);
            WriteXmlFile(xml, packer.TmpExtractPath);
            packer.RepackTo(outISO);

        } else {
            CreateIso(outputDir, xmlPath);
        }
    }

    private void LoadConfigFiles() {
        TaskbarIconsXml = File.ReadAllText("config/TaskbarIcons.xml");
        StartPinsJson = File.ReadAllText("config/StartPins.json");
        SystemScript = File.ReadAllText("config/System.ps1");
        FirstLogonScript = File.ReadAllText("config/FirstLogon.ps1");
        UserOnceScript = File.ReadAllText("config/UserOnce.ps1");
        DefaultUserScript = File.ReadAllText("config/DefaultUser.ps1");
    }

    protected static string EnsureOutputDirectory() {
        string outputDir = Path.Combine(Environment.CurrentDirectory, "out");
        if (!Directory.Exists(outputDir)) {
            Directory.CreateDirectory(outputDir);
        }
        return outputDir;
    }

    protected static string WriteXmlFile(XmlDocument xml, string outputDir) {
        string path = Path.Combine(outputDir, "autounattend.xml");
        File.WriteAllBytes(path, UnattendGenerator.Serialize(xml));
        return path;
    }

    protected static void CreateIso(string outputDir, string xmlPath) {
        string isoPath = Path.Combine(outputDir, "devwin.iso");

        using FileStream isoStream = File.Create(isoPath);
        CDBuilder builder = new() {
            UseJoliet = true,
            VolumeIdentifier = "DEVWIN"
        };

        builder.AddFile("autounattend.xml", xmlPath);
        builder.Build(isoStream);
    }

    protected abstract Configuration GenerateSettings(UnattendGenerator generator);
}
