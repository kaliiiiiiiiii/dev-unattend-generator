A for development and privacy optimized unattend generator

## Download
`autounattend.xml` or packed into`devwin.iso` file from the [latest release](https://github.com/kaliiiiiiiiii/dev-unattend-generator/releases/latest)

## Configuration files
see [docs/config.md](docs/config.md)

## Installing
> [!WARNING]  
> This project mainly targets Windows 11+ and might not work on previous versions as expected.

See [schneegans.de/windows/unattend-generator/usage/](https://schneegans.de/windows/unattend-generator/usage/)

## Security
Assumes trust:
1. This source code:)
   1. Upstream [unattend-generator](https://github.com/cschneegans/unattend-generator) (my [fork](https://github.com/kaliiiiiiiiii/unattend-generator/tree/master))
3. Microsoft
   1. Installation Media from [endpoint](https://go.microsoft.com/fwlink/?LinkId=2156292) ([src](https://github.com/kaliiiiiiiiii/dev-unattend-generator/blob/master/WinDevGen/EsdDownloader.cs)) (or the provided img file)
   2. The build environmemt (Windows)
4. Project [dependencies](https://github.com/kaliiiiiiiiii/dev-unattend-generator/blob/master/WinDevGen/WinDevGen.csproj) (Open-Source)
    1. [AlphaFs](https://github.com/alphaleonis/AlphaFS) (Used for ISO=>ISO)
    2. [LTRData.DiscUtils.Iso9660](https://github.com/DiscUtils/DiscUtils/tree/develop/Library/DiscUtils.Iso9660) (Used for `singledevwin.iso`)
    3. [Microsoft.PowerShell.SDK](https://github.com/PowerShell/PowerShell/tree/master/src/Microsoft.PowerShell.SDK) (used for ISO=>ISO)
    4. [Xpath2](https://github.com/StefH/XPath2.Net) (Used for 1.i)

#### Build from scratch
1. Download (verify signature) and install Windows 11 based on the [official instructions](https://www.microsoft.com/en-us/software-download/windows11)
2. Download (verify signature) and install the [Windows ADK](https://learn.microsoft.com/en-us/windows-hardware/get-started/adk-install) and [dotnet](https://dotnet.microsoft.com/en-us/download)
3. Download (verify signature) this repository ([direct url](https://github.com/kaliiiiiiiiii/dev-unattend-generator/archive/refs/heads/master.zip))
4. Run `build.bat --publish`
5. run `out/bin/WinDevGen.exe`
6. flash the generated `devwin.iso` using `C:\Windows\System32\isoburn.exe`

## TODO's
- [ ] only add required images from esd based on `ImageInfo`
- [ ] automatically setup `Admin`, `User` and `Guest` users.
- [ ] predownload chockolatey packages using [completely-offline-install](https://docs.chocolatey.org/en-us/choco/setup/#completely-offline-install)
- [ ] Automatically ask to change passwords for created users.
- [ ] properly cleanup on CTRL+C
- [ ] support config over [NJsonSchema](https://github.com/RicoSuter/NJsonSchema)
- [ ] test ISO=>ISO mode
- [ ] use empty vhd mount for ISO=>ISO mode
- [ ] ~~Cross-Platform, optimization: Move to [ManagedWimLib](https://github.com/ied206/ManagedWimLib/)~~
- [ ] Minimize third-party dependencies. 

## Developing
The configuration can be changed in [WinDevGen/WinDevGen.cs](https://github.com/kaliiiiiiiiii/dev-unattend-generator/tree/master/WinDevGen/WinDevGen.cs)

> [!Note]  
> The language must match the ISO's language. Otherwise,` windows will default to the ISO's language.


#### Building
Building the executable
```bash
build.bat
```

Output:
- `out/bin/` - folder containing the executable `WinDevGen.exe` and other required files.

##### Running
Dependencies:
- [Windows ADK](https://learn.microsoft.com/en-us/windows-hardware/get-started/adk-install) must be installed
- Must run on Windows for generating `devwin.iso`

Automatically download installation media to ./cache/esd and apply config.
```bash
out/bin/WinDevGen.exe
```
This must run as admin due to `dism.exe` usage.

Apply config to a given iso installation media (discouraged)
```bash
out/bin/WinDevGen.exe --iso=./Win11_24H2_English_x64.iso
```

Outputs
- `out/autounattend.xml` - the config xml
- `out/singledevwin.iso` - an iso containing autounattend.xml
- `out/devwin.iso`- a full installation media

## Known Bugs

#### Dism fails on GH actions
```bash
dism.exe /Mount-Image /ImageFile:C:\Users\runneradmin\AppData\Local\Temp\tmpv0c0u2.esd /MountDir:C:\Users\runneradmin\AppData\Local\Temp\dism_img_mount_713b9d2e2c624d6a888e2ced0285c133 /ReadOnly /index:1
Error: 11
An attempt was made to load a program with an incorrect format.
dism.cs:line 133
dism.cs:line 234
dism.cs:line 45
```
# References
- [cschneegans/unattend-generator](https://github.com/cschneegans/unattend-generator) the original generator
- [kaliiiiiiiiii/unattend-generator](https://github.com/kaliiiiiiiiii/unattend-generator) my custom generator
- [download-windows-esd](https://github.com/mattieb/download-windows-esd)
- [windows-esd-to-iso](https://github.com/mattieb/windows-esd-to-iso)

# Licences

The Generator itsself is licenced under [MIT](https://github.com/kaliiiiiiiiii/unattend-generator) \
This project is licenced under MIT as well.
