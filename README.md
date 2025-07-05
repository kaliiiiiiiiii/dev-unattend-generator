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
- leftover 2x (instead of 1x) `desktop.ini`

## Developing
The configuration can be changed in [Generator/Generate.cs](https://github.com/kaliiiiiiiiii/dev-unattend-generator/blob/v0.0.0.0.4/Generator/Generate.cs#L30-L133)

> [!Note]  
> The language must match the ISO's language. Otherwise, windows will default to the ISO's language.


#### Building
Building the executable
```bash
cd Generator
dotnet build -c Release
```

Running the executable
```bash
Generator/bin/Release/net9.0/Generate.exe
```

# References
- [cschneegans/unattend-generator](https://github.com/cschneegans/unattend-generator) the original generator
- [kaliiiiiiiiii/unattend-generator](https://github.com/kaliiiiiiiiii/unattend-generator) my custom generator

# Licences

The Generator itsself is licenced under [MIT](https://github.com/kaliiiiiiiiii/unattend-generator) \
This project is licenced under MIT as well.