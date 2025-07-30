using System.Collections.Immutable;
using System.Drawing;
using Schneegans.Unattend;

namespace Generate;

class Generate : BaseGenerator {
    public static void Main(string[] args) {
        string? iso = null;
        File.WriteAllText("out/error.log", string.Empty);

        foreach (var arg in args) {
            if (arg.StartsWith("--iso=")) {
                iso = arg["--iso=".Length..];
            }
        }
        new Generate().Run(iso);
    }

    protected override Configuration GenerateSettings(UnattendGenerator generator) {

        // checkout https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/Main.cs
        return Configuration.Default with {
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
            // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/Bloatware.json

            // https://github.com/cschneegans/unattend-generator/blob/37887a00d9fb5061e4e74ce6b8a82fbb11fc6e87/Main.cs#L597
            // Remove{token ?? displayName.Replace(" ", "")}
            Bloatwares = ImmutableList.CreateRange(
            [
                generator.Lookup<Bloatware>("RemoveCopilot"),
                generator.Lookup<Bloatware>("RemoveOneDrive"),
                generator.Lookup<Bloatware>("RemoveSkype"),
                generator.Lookup<Bloatware>("RemoveXboxApps"),
                generator.Lookup<Bloatware>("RemoveNews"),
                generator.Lookup<Bloatware>("RemoveWeather"),
                generator.Lookup<Bloatware>("RemoveToDO"),
                generator.Lookup<Bloatware>("RemoveSolitaire"),
                generator.Lookup<Bloatware>("RemoveMaps"),
                generator.Lookup<Bloatware>("RemoveOffice365"),
                generator.Lookup<Bloatware>("RemoveFamily"),
                generator.Lookup<Bloatware>("RemoveDevHome"),
                generator.Lookup<Bloatware>("RemoveBingSearch")
            ]
            ),

            ExpressSettings = ExpressSettingsMode.DisableAll,

            // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/modifier/Script.cs#L68-L101
            ScriptSettings = new ScriptSettings(Scripts:
                [
                    new(SystemScript, ScriptPhase.System, ScriptType.Ps1),
                     new Script(@"
                        Windows Registry Editor Version 5.00

                        [HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System]
                        ""NoLocalPasswordResetQuestions""=dword:00000001
                    ",
                    ScriptPhase.System,ScriptType.Reg),
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

            StartPinsSettings = new CustomStartPinsSettings(Json: StartPinsJson),
            StartTilesSettings = new EmptyStartTilesSettings(), // win10 xml for StartPinsSettings

            StickyKeysSettings = new DisabledStickyKeysSettings(),
            CompactOsMode = CompactOsModes.Default,
            TaskbarIcons = new CustomTaskbarIcons(Xml: TaskbarIconsXml),
            Effects = new DefaultEffects(),
            // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/DesktopIcon.json
            DesktopIcons = new DefaultDesktopIconSettings(),
        };
    }
}
