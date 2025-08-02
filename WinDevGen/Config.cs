using System.Drawing;
using System.Collections.Immutable;
using Schneegans.Unattend;

namespace WinDevGen;

public static class DevConfig {
    public record ConfigFiles {
        public string TaskbarIconsXml = "";
        public string StartPinsJson = "";
        public string SystemScript = "";
        public string FirstLogonScript = "";
        public string UserOnceScript = "";
        public string DefaultUserScript = "";
    };

    public static Configuration DefaultUnattConfig => new(
        LanguageSettings: new UnattendedLanguageSettings(
                // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/ImageLanguage.json
                ImageLanguage: new ImageLanguage(
                     id: "en-US",
                    displayName: "English"
                ),
                LocaleAndKeyboard: new LocaleAndKeyboard(
                    // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/UserLocale.json
                    new UserLocale(
                        id: "en-US",
                        displayName: "English (United States)",
                        keyboardLayout: new KeyboardIdentifier(
                            id: "00060409",
                            displayName: "Colemak",
                            type: InputType.Keyboard
                        ),
                        lcid: "0409",
                        geoLocation: new GeoLocation(
                            id: "223",
                            displayName: "Switzerland"
                        )
                    ),

                    // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/KeyboardIdentifier.json
                    new KeyboardIdentifier(
                            id: "00060409",
                            displayName: "Colemak",
                            type: InputType.Keyboard
                        )
                ),
                LocaleAndKeyboard2: null,
                LocaleAndKeyboard3: null,
                // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/GeoId.json
                GeoLocation: new GeoLocation(
                            id: "223",
                            displayName: "Switzerland"
                        ) // switzerland
                ),
        AccountSettings: new InteractiveLocalAccountSettings(),
        PartitionSettings: new InteractivePartitionSettings(),
        InstallFromSettings: new AutomaticInstallFromSettings(),
        DiskAssertionSettings: new SkipDiskAssertionSettings(),

        /* {
           "Id": "pro",
           "DisplayName": "Pro",
           "ProductKey": "VK7JG-NPHTM-C97JM-9MPGT-3V66T",
           "Visible": true
       }, */
        // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/WindowsEdition.json
        EditionSettings: new UnattendedEditionSettings(new WindowsEdition("pro", "Pro", "VK7JG-NPHTM-C97JM-9MPGT-3V66T", true)),

        LockoutSettings: new DefaultLockoutSettings(),
        PasswordExpirationSettings: new UnlimitedPasswordExpirationSettings(),
        ProcessAuditSettings: new DisabledProcessAuditSettings(),
        ComputerNameSettings: new RandomComputerNameSettings(),

        // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/TimeOffset.json
        TimeZoneSettings: new ExplicitTimeZoneSettings(
                TimeZone: new TimeOffset(
                    id: "W. Europe Standard Time",
                    displayName: "(UTC+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna"
                )
                ),

        WifiSettings: new InteractiveWifiSettings(),
        WdacSettings: new SkipWdacSettings(),
        ProcessorArchitectures: [ProcessorArchitecture.amd64],

        // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/Component.json
        Components: ImmutableDictionary.Create<ComponentAndPass, string>(),
        // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/Bloatware.json

        // https://github.com/cschneegans/unattend-generator/blob/37887a00d9fb5061e4e74ce6b8a82fbb11fc6e87/Main.cs#L597
        // Remove{token ?? displayName.Replace(" ", "")}
        Bloatwares: [],
        ExpressSettings: ExpressSettingsMode.DisableAll,
        ScriptSettings: new ScriptSettings(Scripts: [], RestartExplorer: false),
        KeySettings: new SkipKeySettings(),
        WallpaperSettings: new SolidWallpaperSettings(Color.Black),
        LockScreenSettings: new DefaultLockScreenSettings(),
        ColorSettings: new CustomColorSettings(
                SystemTheme: ColorTheme.Dark,
                AppsTheme: ColorTheme.Dark,
                EnableTransparency: false,
                AccentColorOnStart: false,
                AccentColorOnBorders: false,
                AccentColor: Color.FromArgb(0, 120, 215)
                ),
        PESettings: new DefaultPESettings(),
        BypassRequirementsCheck: true,
        BypassNetworkCheck: true, // installation fails if this is true (https://github.com/kaliiiiiiiiii/unattend-generator/issues/2)
        EnableLongPaths: true,
        EnableRemoteDesktop: false,
        HardenSystemDriveAcl: true,
        AllowPowerShellScripts: false,
        DisableLastAccess: true,
        PreventAutomaticReboot: true,
        DisableDefender: false,
        DisableSac: false,
        DisableUac: false,
        DisableSmartScreen: false,
        DisableFastStartup: false,
        DisableSystemRestore: false,
        TurnOffSystemSounds: true,
        DisableAppSuggestions: true,
        DisableWidgets: true,
        VBoxGuestAdditions: false,
        VMwareTools: false,
        VirtIoGuestTools: false,
        PreventDeviceEncryption: false,
        ClassicContextMenu: true,
        LeftTaskbar: false,
        HideTaskViewButton: false,
        ShowFileExtensions: true,
        ShowAllTrayIcons: false,
        HideFiles: HideModes.None,
        HideEdgeFre: true,
        DisableEdgeStartupBoost: true,
        MakeEdgeUninstallable: true,
        LaunchToThisPC: true,
        DisableWindowsUpdate: false,
        DisablePointerPrecision: true,
        DeleteWindowsOld: true,
        DisableBingResults: true,
        UseConfigurationSet: false,
        HidePowerShellWindows: false,
        ShowEndTask: true,
        TaskbarSearch: TaskbarSearchMode.Hide,

        StartPinsSettings: new DefaultStartPinsSettings(),
        StartTilesSettings: new EmptyStartTilesSettings(), // win10 xml for StartPinsSettings

        StickyKeysSettings: new DisabledStickyKeysSettings(),
        CompactOsMode: CompactOsModes.Default,
        TaskbarIcons: new DefaultTaskbarIcons(),
        Effects: new DefaultEffects(),
        // https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/resource/DesktopIcon.json
        DesktopIcons: new DefaultDesktopIconSettings(),
        StartFolderSettings: new DefaultStartFolderSettings()
    );

    public record WinDevOpts {
        public required UnattendGenerator UnattGen;
        public required Configuration UnattConfig;
        public required ConfigFiles ConfigFiles;

    }
}

