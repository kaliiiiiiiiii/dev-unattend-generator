using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management.Automation; // Microsoft.PowerShell.SDK


namespace Generate;

public enum FileType {
    ISO,
    ESD,
}

public class IsoPacker : IDisposable {
    public string IsoPath { get; }

    public FileType FileType { get; }

    public ElToritoBootCatalog? ElToritoBootCatalog { get; }
    private bool disposed = false;
    private bool mounted = false;
    public string TmpExtractPath { get; } = Path.Join(Path.GetTempPath(), "iso_extract_" + Guid.NewGuid().ToString("N"));
    public IsoPacker(string isoPath) {
        IsoPath = isoPath;

        string extension = Path.GetExtension(IsoPath) ?? throw new Exception("Expected an extension for isoPath");
        switch (extension) {
            case ".iso": {
                    if (Directory.Exists(TmpExtractPath))
                        Directory.Delete(TmpExtractPath, true);
                    Directory.CreateDirectory(TmpExtractPath);
                    FileType = FileType.ISO;
                    ElToritoBootCatalog = ElToritoParser.ParseElToritoData(IsoPath);
                    var mountedDrive = MountIso() ?? throw new Exception("Failed to mount ISO.");
                    try {
                        FileUtils.CopyWithMetadata(mountedDrive, TmpExtractPath);
                    } finally {
                        DismountIso();
                    }
                    break;
                }
            case ".esd": {
                    FileType = FileType.ESD;
                    Thread.Sleep(1000); // sleep 1 second
                    try {
                        MountEsd(IsoPath);
                    } catch (Exception ex) {
                        try {
                            DismountEsd(false);
                        } catch {
                            Console.Error.WriteLine($"{ex.Message}{ex.StackTrace}");
                            Console.Error.WriteLine($"Durning the handling of this exception, another exception occurred:");
                            throw;
                        }
                        throw;
                    }

                    break;
                }
            default:
                throw new Exception($"Unknown file extension: {extension}");

        }

        Console.CancelKeyPress += OnCancelKeyPress;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    ~IsoPacker() { Dispose(); }

    public void Dispose() {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e) {
        Cleanup();
    }

    private void OnProcessExit(object? sender, EventArgs e) {
        Cleanup();
    }

    protected virtual void Cleanup() {
        if (!disposed) {
            if (FileType == FileType.ISO && Directory.Exists(TmpExtractPath)) {
                FileUtils.DeleteDirectory(TmpExtractPath);
            } else if (FileType == FileType.ESD) {
                DismountEsd(false);
            } else {
                throw new Exception("Unknown FileType");
            }
            disposed = true;
        }
    }

    private static string GetPowerShellErrors(PowerShell ps) {
        return string.Join(Environment.NewLine, ps.Streams.Error.Select(e => e.ToString()));
    }
    private static string FindOscdimg() {
        string? archFolder = RuntimeInformation.ProcessArchitecture switch {
            Architecture.X86 => "x86",
            Architecture.X64 => "amd64",
            Architecture.Arm => "arm",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException("Unsupported architecture.")
        };

        string exePath = Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Windows Kits", "10", "Assessment and Deployment Kit",
            "Deployment Tools", archFolder, "Oscdimg", "oscdimg.exe"
        );

        if (!File.Exists(exePath)) {
            throw new FileNotFoundException($"oscdimg.exe not found at expected path: {exePath}");
        }

        return exePath;
    }

    public ElToritoBootCatalog RepackTo(string newIsoPath) {
        if (!Directory.Exists(TmpExtractPath))
            throw new InvalidOperationException($"Nothing found at TmpExtractPath {TmpExtractPath}");

        string oscdimgPath = FindOscdimg();

        // Locate all required boot files with normalized paths
        string etfsbootPath = Path.Join(TmpExtractPath, "boot", "etfsboot.com");
        // string bootx64Path = Path.Join(TmpExtractPath, "efi", "boot", "bootx64.efi");
        string efisysPath = Path.Join(TmpExtractPath, "efi", "microsoft", "boot", "efisys.bin");

        // Verify all required boot files exist with full error details
        if (!File.Exists(etfsbootPath))
            throw new FileNotFoundException($"BIOS boot file not found at: {Path.GetFullPath(etfsbootPath)}");
        if (!File.Exists(efisysPath))
            throw new FileNotFoundException($"UEFI boot image not found at: {Path.GetFullPath(efisysPath)}");

        // https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/oscdimg-command-line-options
        var arguments = new List<string> {
            // Volume Label
			"-lDevWin_ISO_windows",
            "-m", // Ignores the maximum size limit of an image.
				  // "-o", // encode duplicate files only once (optimize)
			"-u2", // Produces an image that contains only the UDF file system.
            "-h", // Includes hidden files and directories in the source path of the image.
			TmpExtractPath,
            Path.GetFullPath(newIsoPath),
            
			// 	Specifies the value to use for the platform ID in the El Torito catalog. The default ID is 0xEF to represent a Unified Extensible Firmware Interface (UEFI) system. 0x00 represents a BIOS system.
            $"-p{ElToritoBootCatalog?.PlatformId ?? 0xEF:X2}",
            $"-bootdata:2#p0,e,b{etfsbootPath}#pEF,e,b{efisysPath}", // multi-boot entries https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/oscdimg-command-line-options?view=windows-11#use-multi-boot-entries-to-create-a-bootable-image
            
            // Specifies a text file that has a layout for the files to be put in the image.
            // "-yo<bootOrder.txt>"
        };

        var psi = new ProcessStartInfo {
            FileName = oscdimgPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(oscdimgPath) // Important!
        };
        foreach (var arg in arguments) {
            psi.ArgumentList.Add(arg);
        }

        using Process proc = new() { StartInfo = psi };
        Console.WriteLine($"Executing: {psi.FileName} {string.Join(" ", psi.ArgumentList)}");
        proc.Start();
        string output = proc.StandardOutput.ReadToEnd();
        string error = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        Console.WriteLine(output);
        if (proc.ExitCode != 0) {
            throw new Exception($"ISO creation failed (Code {proc.ExitCode})\n" +
                              $"Output:\n{output}\n" +
                              $"Error:\n{error}");
        }

        return ElToritoParser.ParseElToritoData(IsoPath);
    }

    private string? MountIso() {
        if (!mounted) {
            using PowerShell ps = PowerShell.Create();

            // Mount the ISO
            ps.AddScript("Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass");
            ps.AddScript($"Mount-DiskImage -ImagePath '{IsoPath}'");
            ps.Invoke();

            if (ps.HadErrors)
                throw new InvalidOperationException(GetPowerShellErrors(ps));

            ps.Commands.Clear();

            // Get drive letter
            ps.AddScript($@"
        $img = Get-DiskImage -ImagePath '{IsoPath}'
        Get-Volume -DiskImage $img | Select-Object -ExpandProperty DriveLetter
        ");
            var result = ps.Invoke();

            mounted = true;
            if (ps.HadErrors)
                throw new InvalidOperationException(GetPowerShellErrors(ps));


            if (result.Count == 0 || result[0] == null)
                throw new InvalidOperationException("Failed to get drive letter for mounted ISO.");

            return result[0].ToString() + ":\\";
        }
        return null;
    }

    private void DismountIso() {
        if (mounted) {
            using PowerShell ps = PowerShell.Create();
            ps.AddScript("Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass");
            ps.AddScript($"Dismount-DiskImage -ImagePath '{IsoPath}'");
            ps.Invoke();
            if (ps.HadErrors)
                throw new InvalidOperationException(GetPowerShellErrors(ps));
            mounted = false;
        }
    }
    public void MountEsd(string esdPath) {
        if (!mounted) {
            var psi = new ProcessStartInfo {
                FileName = "dism.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            psi.ArgumentList.Add("/Mount-Wim");
            psi.ArgumentList.Add($"/WimFile:{esdPath}");
            psi.ArgumentList.Add("/index:1");
            psi.ArgumentList.Add($"/MountDir:{TmpExtractPath}");

            using var proc = new Process { StartInfo = psi };

            proc.Start();
            mounted = true;
            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode == 740) {
                mounted = false;
                throw new Exception("dism requires elevated privileges");
            } else if (proc.ExitCode != 0) {
                throw new Exception($"ESD mount failed (Code {proc.ExitCode})\nOutput:\n{output}\nError:\n{error}");
            }
        }
    }
    public void DismountEsd(bool commitChanges) {
        if (mounted) {
            var psi = new ProcessStartInfo {
                FileName = "dism.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("/Unmount-Wim");
            psi.ArgumentList.Add($"/MountDir:{TmpExtractPath}");
            psi.ArgumentList.Add(commitChanges ? "/commit" : "/discard");

            using var proc = new Process { StartInfo = psi };

            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode == 740) {
                mounted = false;
                throw new Exception("dism requires elevated privileges");
            } else if (proc.ExitCode != 0) {
                throw new Exception($"ESD unmount failed (Code {proc.ExitCode})\nOutput:\n{output}\nError:\n{error}");
            }

            try { Directory.Delete(TmpExtractPath); } catch { }
            mounted = false;
        }
    }
}
