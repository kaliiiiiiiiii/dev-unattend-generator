using System.Diagnostics;
using System.Management.Automation; // Microsoft.PowerShell.SDK

namespace Generate;

public class IsoPacker: IDisposable{
    public string IsoPath { get; }
    private bool disposed = false;

    public string TmpExtractPath { get; } = Path.Combine(Path.GetTempPath(), "iso_extract_" + Guid.NewGuid().ToString("N"));

    public IsoPacker(string isoPath){
        IsoPath = isoPath;
        if (Directory.Exists(TmpExtractPath))
            Directory.Delete(TmpExtractPath, true);
        Directory.CreateDirectory(TmpExtractPath);

        string mountedDrive = MountIso();
        if (mountedDrive == null)
            throw new Exception("Failed to mount ISO.");

        try
        {
            CopyWithMetadata(mountedDrive, TmpExtractPath);
        }
        finally
        {
            DismountIso();
        }
    }

     ~IsoPacker(){
        
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    protected virtual void Cleanup(){
        if (!disposed){
            try{
                if (Directory.Exists(TmpExtractPath)){
                    Directory.Delete(TmpExtractPath, true);
                }
            }
            catch{}
            disposed = true;
        }
    }

    private string GetPowerShellErrors(PowerShell ps)
    {
        return string.Join(Environment.NewLine, ps.Streams.Error.Select(e => e.ToString()));
    }

    public void RepackTo(string newIsoPath){
        if (!Directory.Exists(TmpExtractPath))
            throw new InvalidOperationException("Nothing to repack. Run Extract() first.");

        var psi = new ProcessStartInfo{
            FileName = "oscdimg.exe", // Must be installed (e.g., part of Windows ADK)
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

    private string MountIso()
    {
        using PowerShell ps = PowerShell.Create();

        // Mount the ISO
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

    private void DismountIso(){
        using PowerShell ps = PowerShell.Create();
        ps.AddScript($"Dismount-DiskImage -ImagePath '{IsoPath}'");
        ps.Invoke();
    }

    private static void CopyWithMetadata(string sourceDrive, string destPath){
        var psi = new ProcessStartInfo
        {
            FileName = "robocopy",
            Arguments = $"\"{sourceDrive}\" \"{destPath}\" /E /COPYALL /R:0 /NFL /NDL /NP /NJH /NJS",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process, got null");
        process.WaitForExit();

        if (process.ExitCode >= 8)
            throw new Exception("Robocopy failed: " + process.StandardError.ReadToEnd());
    }
}
