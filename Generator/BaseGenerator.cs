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

        var config = GenerateSettings(generator);


        // throw new Exception("");

        var xml = generator.GenerateXml(config);
        var xmlPath = WriteXmlFile(xml, outputDir);

        string singleOutISO = "singledevwin.iso";
        string outISO = Path.Join(outputDir, "devwin.iso");
        if (File.Exists(outISO)) { File.Delete(outISO); }
        if (File.Exists(singleOutISO)) { File.Delete(singleOutISO); }
        CreateIso(Path.Join(outputDir, singleOutISO), xmlPath);
#if __UNO__ // not on windows
        Console.WriteLine("Generating iso is currently only supported on Windows")
#else // on windows, continuing

        if (iso == null) {
            var downloader = new WindowsEsdDownloader();
            string language = "en-US";
            string edition = "Professional";
            string architecture = !config.ProcessorArchitectures.IsEmpty ? config.ProcessorArchitectures.First().ToString() : "x64";
            iso = downloader.Download(language, edition, architecture);
        }
        var packer = new IsoPacker(iso);
        try {
            var catalog = packer.ElToritoBootCatalog;
            catalog?.Log();
            WriteXmlFile(xml, packer.TmpExtractPath);
            var newCatalog = packer.RepackTo(outISO);
            newCatalog.Log();
            catalog?.ValidateBootEntriesEqual(newCatalog.Entries);
        } finally {
            packer.Dispose();
        }
#endif

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
        string outputDir = Path.Join(Environment.CurrentDirectory, "out");
        if (!Directory.Exists(outputDir)) {
            Directory.CreateDirectory(outputDir);
        }
        return outputDir;
    }

    protected static string WriteXmlFile(XmlDocument xml, string outputDir) {
        string path = Path.Join(outputDir, "autounattend.xml");
        File.WriteAllBytes(path, UnattendGenerator.Serialize(xml));
        return path;
    }

    protected static void CreateIso(string isoPath, string xmlPath) {

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
