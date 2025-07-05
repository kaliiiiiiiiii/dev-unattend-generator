using System.Collections.Immutable;
using System.Xml;
using System.Drawing;
using DiscUtils.Iso9660;
using System.Reflection.Metadata;

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

        // read config files
        string taskBarIcons = File.ReadAllText("config/TaskbarIcons.xml");
        string startPins = File.ReadAllText("config/StartPins.json");

        // read scripts
        string SystemScript = File.ReadAllText("config/System.ps1");
        string FirstLogonScript = File.ReadAllText("config/FirstLogon.ps1");
        string UserOnceScript = File.ReadAllText("config/UserOnce.ps1");
        string DefaultUserScript = File.ReadAllText("config/DefaultUser.ps1");

        // checkout https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/Main.cs
        return Configuration.Default with
        {
            LanguageSettings = new UnattendedLanguageSettings(
            // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/ImageLanguage.json
            ImageLanguage: generator.Lookup<ImageLanguage>("en-US"),

            LocaleAndKeyboard: new LocaleAndKeyboard(
                // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/UserLocale.json
                generator.Lookup<UserLocale>("en-US"),

                // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/KeyboardIdentifier.json
                generator.Lookup<KeyboardIdentifier>("00060409") // 
            ),
            LocaleAndKeyboard2: null,
            LocaleAndKeyboard3: null,
            // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/GeoId.json
            GeoLocation: generator.Lookup<GeoLocation>("223") // switzerland
            ),
            AccountSettings = new InteractiveLocalAccountSettings(),
            PartitionSettings = new InteractivePartitionSettings(),
            InstallFromSettings = new AutomaticInstallFromSettings(),
            DiskAssertionSettings = new SkipDiskAssertionSettings(),

            /* {
               "Id": "pro",
               "DisplayName": "Pro",
               "ProductKey": "VK7JG-NPHTM-C97JM-9MPGT-3V66T",
               "Visible": true
           }, */
            // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/WindowsEdition.json
            EditionSettings = new CustomEditionSettings(productKey: "VK7JG-NPHTM-C97JM-9MPGT-3V66T"),

            LockoutSettings = new DefaultLockoutSettings(),
            PasswordExpirationSettings = new UnlimitedPasswordExpirationSettings(),
            ProcessAuditSettings = new DisabledProcessAuditSettings(),
            ComputerNameSettings = new RandomComputerNameSettings(),

            // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/TimeOffset.json
            TimeZoneSettings = new ExplicitTimeZoneSettings(
            TimeZone: new TimeOffset(
                id: "W. Europe Standard Time",
                displayName: "(UTC+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna"
            )
            ),

            WifiSettings = new InteractiveWifiSettings(),
            WdacSettings = new SkipWdacSettings(),
            ProcessorArchitectures = [ProcessorArchitecture.amd64],

            // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/Component.json
            Components = ImmutableDictionary.Create<ComponentAndPass, string>(),

            // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/modifier/Bloatware.cs
            // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/Bloatware.json
            Bloatwares = ImmutableList.CreateRange(
            [
                generator.Lookup<Bloatware>("RemoveCopilot"),
                generator.Lookup<Bloatware>("RemoveOneDrive"),
            ]
            ),

            ExpressSettings = ExpressSettingsMode.DisableAll,

            // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/modifier/Script.cs#L68-L101
            ScriptSettings = new ScriptSettings(Scripts:
                [
                    new(SystemScript, ScriptPhase.System, ScriptType.Ps1),
                    new(FirstLogonScript, ScriptPhase.FirstLogon, ScriptType.Ps1),
                    new(UserOnceScript, ScriptPhase.UserOnce, ScriptType.Ps1),
                    new(DefaultUserScript, ScriptPhase.DefaultUser, ScriptType.Ps1),
                ]
            , RestartExplorer: false),

            KeySettings = new SkipKeySettings(),
            WallpaperSettings = new SolidWallpaperSettings(Color.Black),
            ColorSettings = new CustomColorSettings(
            SystemTheme: ColorTheme.Dark,
            AppsTheme: ColorTheme.Dark,
            EnableTransparency: false,
            AccentColorOnStart: false,
            AccentColorOnBorders: false,
            AccentColor: Color.FromArgb(0, 120, 215)
            ),
            BypassRequirementsCheck = true,
            BypassNetworkCheck = true, // installation fails if this is true (https://github.com/kaliiiiiiiiii/unattend-generator/issues/2)
            EnableLongPaths = true,
            EnableRemoteDesktop = false,
            HardenSystemDriveAcl = true,
            AllowPowerShellScripts = false,
            DisableLastAccess = true,
            PreventAutomaticReboot = true,
            DisableDefender = false,
            DisableSac = false,
            DisableUac = false,
            DisableSmartScreen = false,
            DisableFastStartup = false,
            DisableSystemRestore = false,
            TurnOffSystemSounds = true,
            DisableAppSuggestions = true,
            DisableWidgets = true,
            VBoxGuestAdditions = false,
            VMwareTools = false,
            VirtIoGuestTools = false,
            PreventDeviceEncryption = false,
            ClassicContextMenu = true,
            LeftTaskbar = false,
            HideTaskViewButton = false,
            ShowFileExtensions = true,
            ShowAllTrayIcons = false,
            HideFiles = HideModes.None,
            HideEdgeFre = true,
            DisableEdgeStartupBoost = true,
            MakeEdgeUninstallable = true,
            LaunchToThisPC = true,
            DisableWindowsUpdate = false,
            DisablePointerPrecision = true,
            DeleteWindowsOld = true,
            DisableBingResults = true,
            UseConfigurationSet = false,
            HidePowerShellWindows = false,
            ShowEndTask = true,
            TaskbarSearch = TaskbarSearchMode.Hide,

            StartPinsSettings = new CustomStartPinsSettings(Json: startPins),
            StartTilesSettings = new EmptyStartTilesSettings(), // win10 xml for StartPinsSettings

            StickyKeysSettings = new DisabledStickyKeysSettings(),
            CompactOsMode = CompactOsModes.Default,
            TaskbarIcons = new CustomTaskbarIcons(Xml: taskBarIcons),
            Effects = new DefaultEffects(),
            // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/DesktopIcon.json
            DesktopIcons = new DefaultDesktopIconSettings(),
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
