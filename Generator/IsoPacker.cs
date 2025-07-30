using System.Collections.Concurrent;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management.Automation; // Microsoft.PowerShell.SDK

#if __UNO__
#else
using AlphaFile = Alphaleonis.Win32.Filesystem.File;
# endif

namespace Generate;

public class IsoPacker : IDisposable {
    public string IsoPath { get; }

    public ElToritoBootCatalog ElToritoBootCatalog { get; }
    private bool disposed = false;
    private readonly Lock _logLock = new();
    public string TmpExtractPath { get; } = Path.Combine(Path.GetTempPath(), "iso_extract_" + Guid.NewGuid().ToString("N"));
    public IsoPacker(string isoPath) {
        IsoPath = isoPath;
        ElToritoBootCatalog = ElToritoParser.ParseElToritoData(IsoPath);
        if (Directory.Exists(TmpExtractPath))
            Directory.Delete(TmpExtractPath, true);
        Directory.CreateDirectory(TmpExtractPath);

        string mountedDrive = MountIso() ?? throw new Exception("Failed to mount ISO.");
        try {
            CopyWithMetadata(mountedDrive, TmpExtractPath);
        } finally {
            DismountIso();
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
            if (Directory.Exists(TmpExtractPath)) {
                DeleteDirectory(TmpExtractPath);
                Directory.Delete(TmpExtractPath);
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

        string exePath = Path.Combine(
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
            throw new InvalidOperationException("Nothing to repack. Run Extract() first.");

        string oscdimgPath = FindOscdimg();

        // Locate all required boot files with normalized paths
        string etfsbootPath = Path.Combine(TmpExtractPath, "boot", "etfsboot.com");
        // string bootx64Path = Path.Combine(TmpExtractPath, "efi", "boot", "bootx64.efi");
        string efisysPath = Path.Combine(TmpExtractPath, "efi", "microsoft", "boot", "efisys.bin");

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
            Arguments = string.Join(" ", arguments),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(oscdimgPath) // Important!
        };

        Console.WriteLine($"Executing: {psi.FileName} {psi.Arguments}");

        using Process proc = new() { StartInfo = psi };
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

    private string MountIso() {
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

        if (ps.HadErrors)
            throw new InvalidOperationException(GetPowerShellErrors(ps));

        if (result.Count == 0 || result[0] == null)
            throw new InvalidOperationException("Failed to get drive letter for mounted ISO.");

        return result[0].ToString() + ":\\";
    }

    private void DismountIso() {
        using PowerShell ps = PowerShell.Create();
        ps.AddScript("Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass");
        ps.AddScript($"Dismount-DiskImage -ImagePath '{IsoPath}'");
        ps.Invoke();
        if (ps.HadErrors)
            throw new InvalidOperationException(GetPowerShellErrors(ps));
    }

    private void CopyWithMetadata(string sourceDrive, string destPath, int maxThreads = 16) {
        if (!Directory.Exists(sourceDrive))
            throw new DirectoryNotFoundException($"Source '{sourceDrive}' not found.");

        // Create all directories first (preserve structure)
        foreach (var dirPath in Directory.EnumerateDirectories(sourceDrive, "*", SearchOption.AllDirectories)) {
            var relativePath = Path.GetRelativePath(sourceDrive, dirPath);
            var targetDirPath = Path.Combine(destPath, relativePath);
            Directory.CreateDirectory(targetDirPath);
        }

        // Enumerate all files
        var files = Directory.EnumerateFiles(sourceDrive, "*", SearchOption.AllDirectories);

        // Copy files in parallel
        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, sourceFile => {
            var relativePath = Path.GetRelativePath(sourceDrive, sourceFile);
            var destFile = Path.Combine(destPath, relativePath);

            try {
                // Copy file (choose SystemFile or AlphaFile)
                File.Copy(sourceFile, destFile, overwrite: true);

                // Preserve timestamps and attributes
                var sourceInfo = new FileInfo(sourceFile);
                File.SetCreationTime(destFile, sourceInfo.CreationTime);
                File.SetLastWriteTime(destFile, sourceInfo.LastWriteTime);
                File.SetLastAccessTime(destFile, sourceInfo.LastAccessTime);
                File.SetAttributes(destFile, sourceInfo.Attributes);

                // Copy ACL using Alphaleonis (better ACL support)
                var security = AlphaFile.GetAccessControl(sourceFile);
                try {
                    AlphaFile.SetAccessControl(destFile, security);
                } catch (Exception ex) {
                    string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Setting ACL failed: {ex.Message}{Environment.NewLine}";
                    lock (_logLock) {
                        File.AppendAllText("out/error.log", logMessage);
                    }
                }
            } catch (Exception ex) {
                Console.Error.WriteLine($"Error copying '{sourceFile}': {ex.Message}");
            }
        });
    }

    private static void DeleteDirectory(string path) {
        if (!Directory.Exists(path))
            return;

        var errors = new ConcurrentBag<Exception>();

        string[] files;
        string[] dirs;

        try {
            files = Directory.GetFiles(path);
            dirs = Directory.GetDirectories(path);
        } catch (Exception ex) {
            throw new IOException($"Failed to enumerate contents of '{path}'", ex);
        }

        // Remove attributes and delete files
        Parallel.ForEach(files, file => {
            try {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            } catch (Exception ex) {
                errors.Add(new IOException($"Failed to delete file '{file}'", ex));
            }
        });

        // Recursively delete directories
        Parallel.ForEach(dirs, dir => {
            try {
                ClearAttributes(dir);
                Directory.Delete(dir, true);
            } catch (Exception ex) {
                errors.Add(new IOException($"Failed to delete subdirectory '{dir}'", ex));
            }
        });

        if (!errors.IsEmpty)
            throw new AggregateException("One or more errors occurred while deleting directory contents.", errors);
    }
    private static void ClearAttributes(string dir) {
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) {
            try {
                File.SetAttributes(file, FileAttributes.Normal);
            } catch { /* Optionally collect error */ }
        }

        foreach (var subDir in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories)) {
            try {
                File.SetAttributes(subDir, FileAttributes.Normal);
            } catch { /* Optionally collect error */ }
        }
    }
}
