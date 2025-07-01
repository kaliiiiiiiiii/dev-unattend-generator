using System;
using System.Collections.Immutable;
using System.Xml;
using System.IO;
using System.Drawing;

namespace Schneegans.Unattend;

class Example
{
  public static void Main(string[] args)
  {
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
  }
}