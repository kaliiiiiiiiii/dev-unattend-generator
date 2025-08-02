A for development and privacy optimized unattend generator

## Download
`autounattend.xml` or packed into`devwin.iso` file from the [latest release](https://github.com/kaliiiiiiiiii/dev-unattend-generator/releases/latest)

## Configuration files
see [docs/config.md](docs/config.md)

## Installing
> [!WARNING]  
> This project mainly targets Windows 11+ and might not work on previous versions as expected.

See [schneegans.de/windows/unattend-generator/usage/](https://schneegans.de/windows/unattend-generator/usage/)

## TODO's
- [ ] only add required images from esd based on `ImageInfo`
- [ ] automatically setup `Admin`, `User` and `Guest` users.
- [ ] predownload chockolatey packages using [completely-offline-install](https://docs.chocolatey.org/en-us/choco/setup/#completely-offline-install)
- [ ] Automatically ask to change passwords for created users.
- [ ] properly cleanup on CTRL+C
- [ ] test ISO=>ISO mode
- [ ] use empty vhd mount for ISO=>ISO mode

## Developing
The configuration can be changed in [WinDevGen/WinDevGen.cs](https://github.com/kaliiiiiiiiii/dev-unattend-generator/tree/master/WinDevGen/WinDevGen.cs)

> [!Note]  
> The language must match the ISO's language. Otherwise, windows will default to the ISO's language.


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
# References
- [cschneegans/unattend-generator](https://github.com/cschneegans/unattend-generator) the original generator
- [kaliiiiiiiiii/unattend-generator](https://github.com/kaliiiiiiiiii/unattend-generator) my custom generator
- [download-windows-esd](https://github.com/mattieb/download-windows-esd)
- [windows-esd-to-iso](https://github.com/mattieb/windows-esd-to-iso)

# Licences

The Generator itsself is licenced under [MIT](https://github.com/kaliiiiiiiiii/unattend-generator) \
This project is licenced under MIT as well.