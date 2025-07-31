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
- [ ] parse `oscdimg` output

<details>
<summary>Sample output</summary>

```
OSCDIMG 2.56 CD-ROM and DVD-ROM Premastering Utility
Copyright (C) Microsoft, 1993-2012. All rights reserved.
Licensed only for producing Microsoft authorized content.


Scanning source tree
Scanning source tree complete (969 files in 95 directories)

Computing directory information complete

Image file is 5818810368 bytes (before optimization)

Writing 969 files in 95 directories to D:\data\projects\dev-unattend-generator\out\devwin.iso


Storage optimization saved 25 files, 14342144 bytes (1% of image)

After optimization, image file is 5806698496 bytes
Space saved because of embedding, sparseness or optimization = 14342144
```

</details>

- [ ] compare both iso's for
    - file sha checksum diff
    - added files and directories
    - removed files and directories
- [ ] leftover 2x (instead of 1x) `desktop.ini`

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
out/bin/Generate.exe --iso=./Win11_24H2_English_x64.iso
```
The files will be packed into an empty iso file if `--iso=` is not provided. This requires the [Windows ADK](https://learn.microsoft.com/en-us/windows-hardware/get-started/adk-install) to be installed.

# References
- [cschneegans/unattend-generator](https://github.com/cschneegans/unattend-generator) the original generator
- [kaliiiiiiiiii/unattend-generator](https://github.com/kaliiiiiiiiii/unattend-generator) my custom generator
- [download-windows-esd](https://github.com/mattieb/download-windows-esd)

# Licences

The Generator itsself is licenced under [MIT](https://github.com/kaliiiiiiiiii/unattend-generator) \
This project is licenced under MIT as well.