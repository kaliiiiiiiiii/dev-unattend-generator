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

## Developing
The configuration can be changed in [Generator/Generate.cs](https://github.com/kaliiiiiiiiii/dev-unattend-generator/blob/v0.0.0.0.4/Generator/Generate.cs#L30-L133)

> [!Note]  
> The language must match the ISO's language. Otherwise, windows will default to the ISO's language.


#### Building
Building the executable
```bash
build.bat
```

Output:
- `out/bin/` - folder containing the executable `Generate.exe` and other required files.

##### Running
Dependencies:
- [Windows ADK](https://learn.microsoft.com/en-us/windows-hardware/get-started/adk-install) must be installed
- Must run on Windows for generating `devwin.iso`

Automatically download installation media to ./cache/esd and apply config.
```bash
out/bin/Generate.exe
```
This must run as admin due to `dism.exe` usage.

Apply config to a given iso installation media (discouraged)
```bash
out/bin/Generate.exe --iso=./Win11_24H2_English_x64.iso
```

Outputs
- `out/autounattend.xml` - the config xml
- `out/singledevwin.iso` - an iso containing autounattend.xml
- `out/devwin.iso`- a full installation media
# References
- [cschneegans/unattend-generator](https://github.com/cschneegans/unattend-generator) the original generator
- [kaliiiiiiiiii/unattend-generator](https://github.com/kaliiiiiiiiii/unattend-generator) my custom generator
- [download-windows-esd](https://github.com/mattieb/download-windows-esd)

# Licences

The Generator itsself is licenced under [MIT](https://github.com/kaliiiiiiiiii/unattend-generator) \
This project is licenced under MIT as well.