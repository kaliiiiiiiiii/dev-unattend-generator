using System.Collections.Immutable;
using System.Xml;
using System.Drawing;
using DiscUtils.Iso9660;

namespace Schneegans.Unattend;

class Generate
{
  public static void Main(string[] args){
    UnattendGenerator generator = new();

    // checkout https://github.com/kaliiiiiiiiii/unattend-generator/blob/master/Main.cs
    XmlDocument xml = generator.GenerateXml(
      Configuration.Default with
      {
        LanguageSettings = new UnattendedLanguageSettings(
          ImageLanguage: generator.Lookup<ImageLanguage>("en-US"),
          LocaleAndKeyboard: new LocaleAndKeyboard(
            generator.Lookup<UserLocale>("en-US"),
            generator.Lookup<KeyboardIdentifier>("00060409") // colemak
          ),
          LocaleAndKeyboard2: null,
          LocaleAndKeyboard3: null,
          GeoLocation: generator.Lookup<GeoLocation>("223") // switzerland
        ),
        Bloatwares = ImmutableList.CreateRange(
          [
            generator.Lookup<Bloatware>("RemoveCopilot"),
          ]
        ),
        BypassRequirementsCheck = true,
        ShowFileExtensions = true,
        EnableLongPaths = true,
        HideEdgeFre = true,
        DeleteWindowsOld = true,
        DisableBingResults = true,
        StickyKeysSettings = new DisabledStickyKeysSettings(),
        ColorSettings = new CustomColorSettings(
          SystemTheme: ColorTheme.Dark,
          AppsTheme: ColorTheme.Dark,
          EnableTransparency: false,
          AccentColorOnStart: false,
          AccentColorOnBorders: false,
          AccentColor: Color.FromArgb(0, 120, 215)
        )
      }
    );

    string outputDir = Path.Combine(Environment.CurrentDirectory, "out");
    if (!Directory.Exists(outputDir)){
      Directory.CreateDirectory(outputDir);
    }
    string path = Path.Combine(outputDir, "autounattend.xml");
    File.WriteAllBytes(path, UnattendGenerator.Serialize(xml));
    string isoPath = Path.Combine(outputDir, "devwin.iso");

    using (FileStream isoStream = File.Create(isoPath)) {
        CDBuilder builder = new CDBuilder
        {
            UseJoliet = true,
            VolumeIdentifier = "DEVWIN"
        };

        // Add autounattend.xml to the root of the ISO
        builder.AddFile("autounattend.xml", path);

        builder.Build(isoStream);
    }
  }
}