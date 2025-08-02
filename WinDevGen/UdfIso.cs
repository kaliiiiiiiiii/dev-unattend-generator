using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management.Automation;

namespace WinDevGen;

public class UdfIso : IImgPacker {
    public string IsoPath { get; }

    public ElToritoBootCatalog? ElToritoBootCatalog { get; }
    public string MountPath { get; }

    public readonly bool Readonly;
    private bool disposed = false;

    public UdfIso(string isoPath, bool as_readonly = true, string? mountPath = null) {
        IsoPath = isoPath;
        Readonly = as_readonly;

        ElToritoBootCatalog = ElToritoParser.ParseElToritoData(IsoPath);
        MountPath = Mount(isoPath, as_readonly: as_readonly, mountPath: mountPath);
    }

    ~UdfIso() { Dispose(); }

    public void Dispose() {
        Cleanup();

        if (!disposed) {
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e) {
        Cleanup();
    }

    private void OnProcessExit(object? sender, EventArgs e) {
        Cleanup();
    }

    protected virtual void Cleanup() {
        if (Readonly) {
            Unmount(IsoPath);
        } else {
            FileUtils.DeleteDirectory(MountPath);
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

    public ElToritoBootCatalog Pack(string newIsoPath) {
        return Pack(MountPath, newIsoPath);
    }

    public static ElToritoBootCatalog Pack(string directory, string newIsoPath) {
        // packs a windows installation folder or mountpoint into an iso
        string oscdimgPath = FindOscdimg();

        // Locate all required boot files with normalized paths
        string etfsbootPath = Path.Join(directory, "boot", "etfsboot.com");
        // string bootx64Path = Path.Join(TmpExtractPath, "efi", "boot", "bootx64.efi");
        string efisysPath = Path.Join(directory, "efi", "microsoft", "boot", "efisys.bin");

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
			directory,
            Path.GetFullPath(newIsoPath),
            
			// 	Specifies the value to use for the platform ID in the El Torito catalog. The default ID is 0xEF to represent a Unified Extensible Firmware Interface (UEFI) system. 0x00 represents a BIOS system.
            $"-p{0xEF:X2}",
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
        proc.Start();
        string output = proc.StandardOutput.ReadToEnd();
        string error = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        Console.WriteLine(output);
        if (proc.ExitCode != 0) {
            if (File.Exists(newIsoPath)) {
                File.Delete(newIsoPath);
            }
            throw new Exception($"ISO creation with executing: {psi.FileName} {string.Join(" ", psi.ArgumentList)} failed (Code {proc.ExitCode})\n" +
                              $"Output:\n{output}\n" +
                              $"Error:\n{error}");
        }

        return ElToritoParser.ParseElToritoData(newIsoPath);
    }

    public static string Mount(string isoPath, bool as_readonly = true, string? mountPath = null) {
        if (mountPath != null) {
            if (as_readonly) {
                throw new Exception("mountPath can only be specified for non-readonly (writable) mode");
            }
            mountPath = Path.Join(Path.GetTempPath(), "dism_img_mount_" + Guid.NewGuid().ToString("N"));
            if (!Directory.Exists(mountPath)) {
                Directory.CreateDirectory(mountPath);
            }
        }

        using PowerShell ps = PowerShell.Create();

        // Mount the ISO
        // https://learn.microsoft.com/en-us/powershell/module/storage/mount-diskimage?view=windowsserver2025-ps
        string tmpMountPath;
        try {
            ps.AddScript("Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass");
            ps.AddScript($"Mount-DiskImage -ImagePath '{isoPath}'");
            ps.Invoke();
            if (ps.HadErrors) {
                throw new InvalidOperationException(GetPowerShellErrors(ps));
            }

            ps.Commands.Clear();

            // Get drive letter
            ps.AddScript($@"
            $img = Get-DiskImage -ImagePath '{isoPath}'
            Get-Volume -DiskImage $img | Select-Object -ExpandProperty DriveLetter
            ");
            var result = ps.Invoke();

            if (ps.HadErrors)
                throw new InvalidOperationException(GetPowerShellErrors(ps));


            if (result.Count == 0 || result[0] == null)
                throw new InvalidOperationException("Failed to get drive letter for mounted ISO.");

            tmpMountPath = result[0].ToString() + ":\\";

            if (!as_readonly && mountPath != null) {
                try {
                    FileUtils.CopyWithMetadata(tmpMountPath, mountPath);
                } catch (Exception ex) {
                    try {
                        FileUtils.DeleteDirectory(mountPath);
                    } catch {
                        Console.Error.WriteLine($"{ex.Message}{ex.StackTrace}");
                        Console.Error.WriteLine($"Durning the handling of this exception, another exception occurred:");
                        throw;
                    }
                    throw;
                }
            } else {
                mountPath = tmpMountPath;
            }
        } catch (Exception ex) {
            // mounting failed, cleanup
            try {
                Unmount(isoPath);

            } catch {
                Console.Error.WriteLine($"{ex.Message}{ex.StackTrace}");
                Console.Error.WriteLine($"Durning the handling of this exception, another exception occurred:");
                throw;
            }
            throw;
        }
        return mountPath;
    }

    public static void Unmount(string isoPath) {
        using PowerShell ps = PowerShell.Create();
        ps.AddScript("Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass");
        ps.AddScript($"Dismount-DiskImage -ImagePath '{isoPath}'");
        ps.Invoke();
        if (ps.HadErrors)
            throw new InvalidOperationException(GetPowerShellErrors(ps));
    }
}
