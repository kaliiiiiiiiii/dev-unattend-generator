namespace Generate;

public sealed class TempDirectory : IDisposable {
    public string Path { get; }

    public TempDirectory() {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path);
    }

    public void Dispose() {
        try {
            if (Directory.Exists(Path)) {
                Directory.Delete(Path, true);
            }
        } catch {
            // Suppress cleanup errors
        }
    }
}

// Disposable temporary file wrapper
public sealed class TempFile() : IDisposable {
    public string Path { get; } = System.IO.Path.GetTempFileName();

	public void Dispose() {
        try {
            if (File.Exists(Path)) {
                File.Delete(Path);
            }
        } catch {
            // Suppress cleanup errors
        }
    }
}