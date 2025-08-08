using Schneegans.Unattend;

namespace WinDevGen;

class Generate {
    public static void Main(string[] args) {
        string? iso = null;

        foreach (var arg in args) {
            if (arg.StartsWith("--iso=")) {
                iso = arg["--iso=".Length..];
            }
        }
        string? outDir = null;
        outDir ??= Path.Join(Environment.CurrentDirectory, "out");
        string? cfgDir = null;
        cfgDir ??= Path.Join(Environment.CurrentDirectory, "config");
        string esdCacheDirectory = Path.Join(Directory.GetCurrentDirectory(), "cache/esd");

        string outISO = Path.Join(outDir, "devwin.iso");
        string singleOutISO = Path.Join(outDir, "singledevwin.iso");
        string outXML = Path.Join(outDir, "autounattend.xml");

        try {
            var opts = new DevConfig.WinDevOpts {
                UnattGen = new UnattendGenerator(),
                UnattConfig = DevConfig.DefaultUnattConfig,
                ConfigFiles = new DevConfig.ConfigFiles {
                    TaskbarIconsXml = File.ReadAllText(Path.Join(cfgDir, "TaskbarIcons.xml")),
                    StartPinsJson = File.ReadAllText(Path.Join(cfgDir, "StartPins.json")),
                    SystemScript = File.ReadAllText(Path.Join(cfgDir, "System.ps1")),
                    FirstLogonScript = File.ReadAllText(Path.Join(cfgDir, "FirstLogon.ps1")),
                    UserOnceScript = File.ReadAllText(Path.Join(cfgDir, "UserOnce.ps1")),
                    DefaultUserScript = File.ReadAllText(Path.Join(cfgDir, "DefaultUser.ps1")),
                }
            };

            using var generator = new BaseWinDevGen(opts, esdCacheDirectory, img: iso);
            FileUtils.WriteXml(generator.UnattendXml, outXML);
            generator.BuildSingleIso(singleOutISO);
            if (File.Exists(outISO)) { File.Delete(outISO); }
            generator.Pack(outISO);

        } catch (Exception ex) {
            Console.Error.WriteLine($"{ex.Message}{ex.StackTrace}");
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
            throw;
        }

    }
}
