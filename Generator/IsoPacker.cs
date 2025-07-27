using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management.Automation; // Microsoft.PowerShell.SDK

# if __UNO__
#else
using AlphaFile = Alphaleonis.Win32.Filesystem.File;
# endif

namespace Generate;

public class IsoPacker : IDisposable {
    public string IsoPath { get; }
    private bool disposed = false;

    public string TmpExtractPath { get; } = Path.Combine(Path.GetTempPath(), "iso_extract_" + Guid.NewGuid().ToString("N"));
    public IsoPacker(string isoPath) {
        IsoPath = isoPath;
        if (Directory.Exists(TmpExtractPath))
            Directory.Delete(TmpExtractPath, true);
        Directory.CreateDirectory(TmpExtractPath);

        string mountedDrive = MountIso();
        if (mountedDrive == null)
            throw new Exception("Failed to mount ISO.");

        try {
            CopyWithMetadata(mountedDrive, TmpExtractPath);
        } finally {
            DismountIso();
        }
    }

    ~IsoPacker() { Dispose(); }

    public void Dispose() {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    protected virtual void Cleanup() {
        if (!disposed) {
            try {
                if (Directory.Exists(TmpExtractPath)) {
                    Directory.Delete(TmpExtractPath, true);
                }
            } catch { }
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
    public void RepackTo(string newIsoPath) {
        if (!Directory.Exists(TmpExtractPath))
            throw new InvalidOperationException("Nothing to repack. Run Extract() first.");
        string oscdimgPath = FindOscdimg();
        var psi = new ProcessStartInfo {
            FileName = oscdimgPath, // Must be installed (e.g., part of Windows ADK)
            Arguments = $"-lNEWISO -m -o -u2 \"{TmpExtractPath}\" \"{newIsoPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process, got null");
        proc.WaitForExit();

        if (proc.ExitCode != 0)
            throw new Exception("ISO repack failed: " + proc.StandardError.ReadToEnd());
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

    private static void CopyWithMetadata(string sourceDrive, string destPath, int maxThreads = 16) {
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
                    Console.Error.WriteLine($"Setting ACL failed:'{sourceFile}':\n {ex.Message}");
                }
            } catch (Exception ex) {
                    Console.Error.WriteLine($"Error copying '{sourceFile}': {ex.Message}");
                }
        });
    }
}
