
using System.Xml;

using DiscUtils.Iso9660;
using Schneegans.Unattend;

namespace WinDevGen;

interface IImgPacker:IDisposable {
    public string MountPath { get; }
}


public class BaseWinDevGen : IImgPacker {

    public string MountPath { get; }

    public readonly UnattendGenerator UnattGen;
    public readonly XmlDocument UnattendXml;

    public readonly DevConfig.WinDevOpts Opts;

    public readonly string ImgPath;

    private readonly TempFile TmpImg;

    private readonly bool FromIso;

    private readonly IImgPacker Packer;

    public BaseWinDevGen(DevConfig.WinDevOpts winDevOpts, string? img = null) {
        UnattGen = winDevOpts.UnattGen;
        if (img != null) {
            string extension = Path.GetExtension(ImgPath) ?? throw new Exception("Expected an extension for isoPath");
            switch (extension) {
                case ".iso": {
                        FromIso = true;
                        break;
                    }
                case ".esd": {
                        FromIso = false;
                        break;
                    }

                default: {
                        throw new Exception($"Unknown file extension: {extension}");
                    }
            }
        }

        if (winDevOpts.UnattConfig.TaskbarIcons is not (DefaultTaskbarIcons or EmptyTaskbarIcons)) {
            throw new Exception("");
        }
        // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/modifier/Script.cs#L68-L101
        List<Script> scripts = [
            new(winDevOpts.ConfigFiles.SystemScript, ScriptPhase.System, ScriptType.Ps1),
            new Script(@"
                Windows Registry Editor Version 5.00

                [HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System]
                ""NoLocalPasswordResetQuestions""=dword:00000001
            ",
            ScriptPhase.System,ScriptType.Reg),
            new(winDevOpts.ConfigFiles.FirstLogonScript, ScriptPhase.FirstLogon, ScriptType.Ps1),
            new(winDevOpts.ConfigFiles.UserOnceScript, ScriptPhase.UserOnce, ScriptType.Ps1),
            new(winDevOpts.ConfigFiles.DefaultUserScript, ScriptPhase.DefaultUser, ScriptType.Ps1),
        ];

        List<Bloatware> bloatwares =
                [
                    winDevOpts.UnattGen.Lookup<Bloatware>("RemoveCopilot"),
            winDevOpts.UnattGen.Lookup<Bloatware>("RemoveOneDrive"),
            winDevOpts.UnattGen.Lookup<Bloatware>("RemoveSkype"),
            winDevOpts.UnattGen.Lookup<Bloatware>("RemoveXboxApps"),
            winDevOpts.UnattGen.Lookup<Bloatware>("RemoveNews"),
            winDevOpts.UnattGen.Lookup<Bloatware>("RemoveWeather"),
            winDevOpts.UnattGen.Lookup<Bloatware>("RemoveToDO"),
            winDevOpts.UnattGen.Lookup<Bloatware>("RemoveSolitaire"),
            winDevOpts.UnattGen.Lookup<Bloatware>("RemoveMaps"),
            winDevOpts.UnattGen.Lookup<Bloatware>("RemoveOffice365"),
            winDevOpts.UnattGen.Lookup<Bloatware>("RemoveFamily"),
            winDevOpts.UnattGen.Lookup<Bloatware>("RemoveDevHome"),
            winDevOpts.UnattGen.Lookup<Bloatware>("RemoveBingSearch")
                ];

        foreach (var script in winDevOpts.UnattConfig.ScriptSettings.Scripts) {
            scripts.Add(script);
        }
        foreach (var bloatware in winDevOpts.UnattConfig.Bloatwares) {
            bloatwares.Add(bloatware);
        }

        winDevOpts.UnattConfig = winDevOpts.UnattConfig with {
            ScriptSettings = new ScriptSettings(scripts, winDevOpts.UnattConfig.ScriptSettings.RestartExplorer),
            Bloatwares = [.. bloatwares],
            //TODO:handle when these already are configured//exist
            TaskbarIcons = new CustomTaskbarIcons(Xml: winDevOpts.ConfigFiles.TaskbarIconsXml),
            StartPinsSettings = new CustomStartPinsSettings(Json: winDevOpts.ConfigFiles.StartPinsJson),
        };
        Opts = winDevOpts;

        UnattendXml = winDevOpts.UnattGen.GenerateXml(Opts.UnattConfig);


#if __UNO__ // not on windows
#else // on windows, continuing
        Console.CancelKeyPress += OnCancelKeyPress;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        if (img == null) {
            var downloader = new WindowsEsdDownloader();
            string language = "en-US";
            string edition = "Professional";
            string architecture = !Opts.UnattConfig.ProcessorArchitectures.IsEmpty ? Opts.UnattConfig.ProcessorArchitectures.First().ToString() : "x64";
            TmpImg = downloader.DownloadTmp(language, edition, architecture);
            img = TmpImg.Path;
        } else {
            TmpImg = new TempFile();
        }
        ImgPath = img;
        if (FromIso) {
            Packer = new UdfIso(ImgPath);
        } else {
            Packer = new Dism(ImgPath, as_esd: true, mountPath: MountPath);
        }
        MountPath = Packer.MountPath;
    }

#endif
    public void Pack(string isoPath) {
        if (Packer is UdfIso packer) {
            var catalog = packer.ElToritoBootCatalog;
            HandleMount();
            var newCatalog = packer.Pack(isoPath);
            newCatalog.Log();
            catalog?.ValidateBootEntriesEqual(newCatalog.Entries);
        } else {
            HandleMount();

        }
        UdfIso.Pack(MountPath, isoPath);
    }

    ~BaseWinDevGen() { Dispose(); }

    public void Dispose() {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e) {
        Cleanup();
    }

    private void OnProcessExit(object? sender, EventArgs e) {
        Cleanup();
    }

    protected virtual void Cleanup() {
        try {
            Packer.Dispose();
        } finally {
            TmpImg.Dispose();
        }
    }

    public void BuildSingleIso(string isoPath) {
        CreateSingleIso(isoPath, UnattendXml);
    }
    public static void CreateSingleIso(string isoPath, XmlDocument xml) {
        var xmlbytes = UnattendGenerator.Serialize(xml);

        using FileStream isoStream = File.Create(isoPath);
        CDBuilder builder = new() {
            UseJoliet = true,
            VolumeIdentifier = "DEVWIN"
        };

        builder.AddFile("autounattend.xml", xmlbytes);
        builder.Build(isoStream);
    }

    public void HandleMount() {
        FileUtils.WriteXml(UnattendXml, MountPath);
    }
}
