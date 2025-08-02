using System.Diagnostics;
using System.Management.Automation;
using System.Runtime.InteropServices.Marshalling;
using System.Text.RegularExpressions;

namespace WinDevGen;


public partial class Dism : IImgPacker {

    public static class CompressType {
        public const string Fast = "fast";
        public const string Max = "max";
        public const string None = "none";
        public const string Recovery = "recovery";

        public const string NotSpecified = null;

        public static bool IsValid(string? value) =>
            value == Fast || value == Max || value == None || value == Recovery || value == null;
    }

    public record ImageInfo {
        public required int Index;
        public required string Name;
        public required string Description;
        public required long Size;
    }

    public string ImgPath { get; }

    public ElToritoBootCatalog? ElToritoBootCatalog { get; }
    public TempFile TmpFile { get; } = new TempFile();

    public string MountPath { get; }

    public readonly bool ReadOnly;
    public readonly bool CommitOnDispose;

    private bool disposed = false;

    public Dism(string imgPath, bool as_esd = false, int? index = 1, string? image_name = null, bool as_readonly = true, string? mountPath = null, bool commitOnDispose = false) {
        ImgPath = imgPath;
        ReadOnly = as_readonly;
        CommitOnDispose = commitOnDispose;
        if (as_esd) {
            MountPath = MountEsdMedia(imgPath, mountPath);
        } else {
            MountPath = MountImg(imgPath, index: index, image_name: image_name, as_readonly: as_readonly, mountPath: mountPath);
        }
    }

    ~Dism() { Dispose(); }

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
        UnmountImg(MountPath, commit: CommitOnDispose);
    }

    // https://regex101.com/r/WJrdDG/1
    [GeneratedRegex(@"^Index : (\d+)\s+Name : (.+)\s+Description : (.+)\s+Size : ([\d,]+) bytes", RegexOptions.Multiline)]
    private static partial Regex ImageInfoRegex();

    public static List<ImageInfo> GetImgInfo(string esdPath) {
        List<string> args = [
            "/Get-ImageInfo",
            $"/ImageFile:{esdPath}"
        ];

        string output = Cmd(args).OutPut;

        var matches = ImageInfoRegex().Matches(output);
        var imageInfos = new List<ImageInfo>();

        foreach (Match match in matches) {
            if (match.Groups.Count == 5) {
                imageInfos.Add(new ImageInfo {
                    Index = int.Parse(match.Groups[1].Value),
                    Name = match.Groups[2].Value.Trim(),
                    Description = match.Groups[3].Value.Trim(),
                    Size = long.Parse(match.Groups[4].Value.Replace(",", ""))
                });
            }
        }

        return imageInfos;
    }

    public static string MountImg(string imgPath, int? index = 1, string? image_name = null, bool as_readonly = true, string? mountPath = null) {
        // attempts to unmount if something fails automatically
        // https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-image-management-command-line-options-s14?view=windows-11#mount-image

        // parse args
        mountPath ??= Path.Join(Path.GetTempPath(), "dism_img_mount_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(mountPath)) {
            Directory.CreateDirectory(mountPath);
        }
        List<string> args = [
            "/Mount-Image",
            $"/ImageFile:{imgPath}",
            $"/MountDir:{mountPath}"
                ];

        if (as_readonly) {
            args.Add("/ReadOnly");
        }

        if (index == null && image_name == null) {
            throw new ValidationError("One of index or image_name must be provided");
        } else if (image_name != null) {
            args.Add($"/Name:{image_name}");
        } else if (index != null) {
            args.Add($"/index:{index}");
        } else {
            throw new ValidationError("index and image_name cannot be provided both");
        }
        try {
            Cmd(args);
        } catch (Exception ex) {
            try {
                Directory.Delete(mountPath);
            } catch { // directory isn't empty
                try {
                    UnmountImg(mountPath, commit: false);
                    FileUtils.DeleteDirectory(mountPath);
                } catch {
                    Console.Error.WriteLine($"{ex.Message}{ex.StackTrace}");
                    Console.Error.WriteLine($"Durning the handling of this exception, another exception occurred:");
                    throw;
                }
                throw;
            }
            throw;

        }
        return mountPath;
    }
    public static void UnmountImg(string mountPath, bool commit = false) {
        // https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-image-management-command-line-options-s14?view=windows-11#unmount-image
        mountPath ??= Path.Join(Path.GetTempPath(), "dism_img_mount_" + Guid.NewGuid().ToString("N"));

        List<string> args = [
            "/Unmount-Image",
            $"/MountDir:{mountPath}",
            commit ? "/commit" : "/discard"
            ];
        // The provider VHDManager does not support CreateDismImage on C:// ...
        // assumption: file dosen't exist
        Cmd(args, [50]);
    }
    public static (string OutPut ,int ExitCode) Cmd(List<string> args, List<int>? okExitCodes = null) {
        // https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-image-management-command-line-options-s14?view=windows-11
        var psi = new ProcessStartInfo {
            FileName = "dism.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args) {
            psi.ArgumentList.Add(arg);
        }

        using var proc = new Process { StartInfo = psi };

        proc.Start();
        string output = proc.StandardOutput.ReadToEnd();
        string error = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        if (proc.ExitCode == 740) {
            throw new Exception("dism requires elevated privileges");
        } else if (okExitCodes != null && okExitCodes.Contains(proc.ExitCode)){
        } else if (proc.ExitCode != 0) {
            throw new Exception($"Executing: {psi.FileName} {string.Join(" ", psi.ArgumentList)} failed (Code {proc.ExitCode})\nOutput:\n{output}\nError:\n{error}");
        }
        return (output, proc.ExitCode);
    }
    public static void ExportImg(string imagePath, string destImg, int? sourceIndex = 1, string? sourceName = null, string? destName = null, string? compressType = null, bool bootable = false) {
        // https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-image-management-command-line-options-s14?view=windows-11#export-image
        if (!CompressType.IsValid(compressType))
            throw new ArgumentException("Invalid mode");
        List<string> args = [
            "/Export-Image",
            $"/SourceImageFile:{imagePath}",
            $"/DestinationImageFile:{destImg}"
        ];

        if (sourceIndex == null && sourceName == null) {
            throw new ValidationError("One of index or image_name must be provided");
        } else if (sourceName != null) {
            args.Add($"/SourceName:{sourceName}");
        } else if (sourceIndex != null) {
            args.Add($"/SourceIndex:{sourceIndex}");
        } else {
            throw new ValidationError("index and image_name cannot be provided both");
        }

        if (compressType != CompressType.NotSpecified) {
            args.Add($"/Compress:{compressType}");
        }
        if (bootable) {
            args.Add("/Bootable");
        }
        if (destName != null) {
            args.Add($"/DestinationName:{destName}");
        }
        Cmd(args);
    }

    public static string MountEsdMedia(string imgPath, string? mountPath) {
        mountPath ??= Path.Join(Path.GetTempPath(), "dism_img_mount_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(mountPath)) {
            Directory.CreateDirectory(mountPath);
        }
        var imgInfo = GetImgInfo(imgPath);
        try {
            MountImg(imgPath, index: 1, mountPath: mountPath);
            // Create sources directory if it doesn't exist
            Directory.CreateDirectory(Path.Combine(mountPath, "sources"));

            // Export image 2 to boot.wim (Windows PE)
            ExportImg(imgPath, Path.Combine(mountPath, "sources", "boot.wim"), sourceIndex: 2, compressType: Dism.CompressType.Max);

            // Export image 3 to boot.wim (Windows Setup)
            ExportImg(imgPath, Path.Combine(mountPath, "sources", "boot.wim"), sourceIndex: 3, bootable: true);

            // Export remaining images (4+) to install.esd
            for (int index = 4; index <= imgInfo.Count; index++) {
                ExportImg(imgPath,
                    Path.Combine(mountPath, "sources", "install.esd"),
                    sourceIndex: index,
                     compressType: index == 4 ? CompressType.Recovery : null
                );
            }
        } catch (Exception ex) {
            try {
                UnmountImg(mountPath, commit: false);
            } catch {
                Console.Error.WriteLine($"{ex.Message}{ex.StackTrace}");
                Console.Error.WriteLine($"Durning the handling of this exception, another exception occurred:");
                throw;
            }
            throw;
        }
        return mountPath;
    }
}