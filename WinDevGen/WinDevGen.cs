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

        string outISO = Path.Join(outDir, "devwin.iso");
        string singleOutISO = Path.Join(outDir, "singledevwin.iso"); ;

        try {
            var opts = new DevConfig.WinDevOpts {
                UnattGen = new UnattendGenerator(),
                UnattConfig = DevConfig.DefaultUnattConfig,
                ConfigFiles = new DevConfig.ConfigFiles {
                    TaskbarIconsXml = File.ReadAllText(Path.Join(cfgDir,"TaskbarIcons.xml")),
                    StartPinsJson = File.ReadAllText(Path.Join(cfgDir,"config/StartPins.json")),
                    SystemScript = File.ReadAllText(Path.Join(cfgDir,"config/System.ps1")),
                    FirstLogonScript = File.ReadAllText(Path.Join(cfgDir,"config/FirstLogon.ps1")),
                    UserOnceScript = File.ReadAllText(Path.Join(cfgDir,"config/UserOnce.ps1")),
                    DefaultUserScript = File.ReadAllText(Path.Join(cfgDir,"config/DefaultUser.ps1")),
                }
            };

			using var generator = new BaseWinDevGen(opts, img: iso);
			generator.BuildSingleIso(singleOutISO);
			if (File.Exists(outISO)) { File.Delete(outISO); }
			generator.Pack(outISO);

		} catch (Exception ex) {
            Console.Error.WriteLine($"{ex.Message}{ex.StackTrace}");
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }

    }
}
