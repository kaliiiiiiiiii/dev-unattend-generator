using System.Collections.Immutable;
using System.Xml;
using System.Drawing;
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

    static Configuration GenerateSettings(UnattendGenerator generator)
    {
        return Configuration.Default with
        {
            LanguageSettings = new UnattendedLanguageSettings(
                generator.Lookup<ImageLanguage>("en-US"),
                new LocaleAndKeyboard(
                    generator.Lookup<UserLocale>("en-US"),
                    generator.Lookup<KeyboardIdentifier>("00060409")
                ),
                null, null,
                generator.Lookup<GeoLocation>("223")
            ),
            AccountSettings = new InteractiveLocalAccountSettings(),
            PartitionSettings = new InteractivePartitionSettings(),
            InstallFromSettings = new AutomaticInstallFromSettings(),
            DiskAssertionSettings = new SkipDiskAssertionSettings(),
            EditionSettings = new CustomEditionSettings(productKey: "VK7JG-NPHTM-C97JM-9MPGT-3V66T"),
            LockoutSettings = new DefaultLockoutSettings(),
            PasswordExpirationSettings = new UnlimitedPasswordExpirationSettings(),
            ProcessAuditSettings = new DisabledProcessAuditSettings(),
            ComputerNameSettings = new RandomComputerNameSettings(),
            TimeZoneSettings = new ExplicitTimeZoneSettings(
                new TimeOffset("W. Europe Standard Time", "(UTC+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna")
            ),
            WifiSettings = new InteractiveWifiSettings(),
            WdacSettings = new SkipWdacSettings(),
            ProcessorArchitectures = [ProcessorArchitecture.amd64],
            Components = ImmutableDictionary.Create<ComponentAndPass, string>(),
            Bloatwares = ImmutableList.CreateRange([
                generator.Lookup<Bloatware>("RemoveCopilot"),
                generator.Lookup<Bloatware>("RemoveOneDrive"),
            ]),
            ExpressSettings = ExpressSettingsMode.DisableAll,
            ScriptSettings = new ScriptSettings(Scripts: [], RestartExplorer: false),
            KeySettings = new SkipKeySettings(),
            WallpaperSettings = new SolidWallpaperSettings(Color.Black),
            ColorSettings = new CustomColorSettings(
                ColorTheme.Dark, ColorTheme.Dark, false, false, false, Color.FromArgb(0, 120, 215)
            ),
            BypassRequirementsCheck = true,
            BypassNetworkCheck = true,
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
            StartPinsSettings = new CustomStartPinsSettings(
                Json: "{\"pinnedList\":[{\"desktopAppLink\":\"%APPDATA%\\\\Microsoft\\\\Windows\\\\Start Menu\\\\Programs\\\\File Explorer.lnk\"}]}"
            ),
            StartTilesSettings = new EmptyStartTilesSettings(),
            StickyKeysSettings = new DisabledStickyKeysSettings(),
            CompactOsMode = CompactOsModes.Default,
            TaskbarIcons = new CustomTaskbarIcons(Xml: @"<?xml version=""1.0"" encoding=""utf-8""?>
<LayoutModificationTemplate
    xmlns=""http://schemas.microsoft.com/Start/2014/LayoutModification""
    xmlns:defaultlayout=""http://schemas.microsoft.com/Start/2014/FullDefaultLayout""
    xmlns:start=""http://schemas.microsoft.com/Start/2014/StartLayout""
    xmlns:taskbar=""http://schemas.microsoft.com/Start/2014/TaskbarLayout""
    Version=""1"">
  <CustomTaskbarLayoutCollection PinListPlacement=""Replace"">
    <defaultlayout:TaskbarLayout>
      <taskbar:TaskbarPinList>
        <taskbar:DesktopApp DesktopApplicationID=""Microsoft.Windows.Explorer""/>
      </taskbar:TaskbarPinList>
    </defaultlayout:TaskbarLayout>
  </CustomTaskbarLayoutCollection>
</LayoutModificationTemplate>"),
            Effects = new DefaultEffects(),
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
