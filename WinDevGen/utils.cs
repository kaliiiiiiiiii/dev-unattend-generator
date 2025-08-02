using System.Xml;
using System.Collections.Concurrent;

using Schneegans.Unattend;

#if __UNO__
#else
using AlphaFile = Alphaleonis.Win32.Filesystem.File;
# endif

namespace WinDevGen;

public sealed class TempDirectory : IDisposable {
    public string Path { get; }

    public TempDirectory() {
        Path = System.IO.Path.Join(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path);
    }

    public void Dispose() {
        if (Directory.Exists(Path)) {
            FileUtils.DeleteDirectory(Path);
        }
    }
}

// Disposable temporary file wrapper
public sealed class TempFile : IDisposable {
    public string Path { get; }

    public TempFile(string? extension = null) {
        string tempFile = System.IO.Path.GetTempFileName();

        if (!string.IsNullOrWhiteSpace(extension)) {
            if (!extension.StartsWith("."))
                extension = "." + extension;

            string newPath = System.IO.Path.ChangeExtension(tempFile, extension);

            // Rename the temp file to have the specified extension
            File.Move(tempFile, newPath);
            Path = newPath;
        } else {
            Path = tempFile;
        }
    }

    public void Dispose() {
        if (File.Exists(Path)) {
            File.Delete(Path);
        }
    }
}


static class FileUtils {

    public static string WriteXml(XmlDocument xml, string outputDir) {
        string path = Path.Join(outputDir, "autounattend.xml");
        File.WriteAllBytes(path, UnattendGenerator.Serialize(xml));
        return path;
    }
    public static void CopyWithMetadata(string sourceDrive, string destPath, int maxThreads = 16) {
        if (!Directory.Exists(sourceDrive))
            throw new DirectoryNotFoundException($"Source '{sourceDrive}' not found.");

        // Create all directories first (preserve structure)
        foreach (var dirPath in Directory.EnumerateDirectories(sourceDrive, "*", SearchOption.AllDirectories)) {
            var relativePath = Path.GetRelativePath(sourceDrive, dirPath);
            var targetDirPath = Path.Join(destPath, relativePath);
            Directory.CreateDirectory(targetDirPath);
        }

        // Enumerate all files
        var files = Directory.EnumerateFiles(sourceDrive, "*", SearchOption.AllDirectories);

        Lock _logLock = new();

        // Copy files in parallel
        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, sourceFile => {
            var relativePath = Path.GetRelativePath(sourceDrive, sourceFile);
            var destFile = Path.Join(destPath, relativePath);

            try {
                // Copy file (choose SystemFile or AlphaFile)
                File.Copy(sourceFile, destFile, overwrite: true);

                // Preserve timestamps and attributes
                var sourceInfo = new FileInfo(sourceFile);
                File.SetCreationTime(destFile, sourceInfo.CreationTime);
                File.SetLastWriteTime(destFile, sourceInfo.LastWriteTime);
                File.SetLastAccessTime(destFile, sourceInfo.LastAccessTime);
                File.SetAttributes(destFile, sourceInfo.Attributes);

#if __UNO__
#else // on windows
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
#endif

            } catch (Exception ex) {
                Console.Error.WriteLine($"Error copying '{sourceFile}': {ex.Message}");
            }
        });
    }

    public static void DeleteDirectory(string path) {
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
        Directory.Delete(path);
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