using System.Diagnostics;

namespace WinDevGen;

public static class CabParser {
    public static byte[] ExtractFile(byte[] cabData, string targetFileName) {
        using var tempDir = new TempDirectory();
        using var tempCabPath = new TempFile();
        File.WriteAllBytes(tempCabPath.Path, cabData);

        var psi = new ProcessStartInfo {
            FileName = "expand.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add($"-f:{targetFileName}");
        psi.ArgumentList.Add($"-r");
        psi.ArgumentList.Add(tempCabPath.Path);
        psi.ArgumentList.Add(tempDir.Path);

        using var proc = new Process { StartInfo = psi };

        proc.Start();

        string output = proc.StandardOutput.ReadToEnd();
        string error = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0) {
            throw new Exception($"CAB extraction failed (Code {proc.ExitCode})\nOutput:\n{output}\nError:\n{error}");
        }

        string extractedFilePath = Path.Join(tempDir.Path, targetFileName);
        if (!File.Exists(extractedFilePath)) {
            throw new Exception($"File '{targetFileName}' not found in CAB archive");
        }

        return File.ReadAllBytes(extractedFilePath);
    }
}