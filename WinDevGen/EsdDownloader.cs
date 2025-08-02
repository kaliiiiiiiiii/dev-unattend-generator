using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Wmhelp.XPath2;

namespace WinDevGen;

public class WindowsEsdDownloader {
    private static readonly HttpClient _httpClient = new();
    public readonly string CacheDirectory;
    private readonly XDocument XmlDoc;

    public WindowsEsdDownloader(string cacheDirectory) {
        CacheDirectory = cacheDirectory;
        Directory.CreateDirectory(CacheDirectory); // Ensure cache directory exists
        using var task = _httpClient.GetByteArrayAsync("https://go.microsoft.com/fwlink/?LinkId=2156292");
        task.Wait();
        byte[] xmlBytes = CabParser.ExtractFile(task.Result, "products.xml");
        using var stream = new MemoryStream(xmlBytes);
        XmlDoc = XDocument.Load(stream);
    }

    public IEnumerable<string> Languages =>
    XmlDoc.XPath2SelectValues("//File/LanguageCode")
           .Cast<string>()
           .Distinct()
           .OrderBy(x => x);

    public IEnumerable<string> GetEditions(string language) =>
        XmlDoc.XPath2SelectValues($"//File/Edition")
            .Cast<string>()
           .Distinct()
           .OrderBy(x => x);

    public IEnumerable<string> GetArchitectures() =>
        XmlDoc.XPath2SelectValues($"//File/Architecture")
            .Cast<string>()
           .Distinct()
           .OrderBy(x => x);

    public TempFile DownloadTmp(string language, string edition, string architecture) {
        string path = Download(language, edition, architecture);
        var tmpFile = new TempFile(extension:".esd");
        File.Copy(path, tmpFile.Path, overwrite:true);
        return tmpFile;
    }
    public string Download(string language, string edition, string architecture) {
        var fileXml = GetFileXml(language, edition, architecture);
        var fileName = GetElementValue(fileXml, "FileName");
        var fileUrl = GetElementValue(fileXml, "FilePath");
        var expectedSha1 = GetElementValue(fileXml, "Sha1");

        // cache file name: {original_name}-{language}-{edition}-{architecture}-{sha1}.esd
        var cacheFileName = $"{Path.GetFileNameWithoutExtension(fileName)}-{language}-{edition}-{architecture}-{expectedSha1}.esd";
        var cacheFilePath = Path.Join(CacheDirectory, cacheFileName);

        // Check if file already exists in cache
        if (File.Exists(cacheFilePath)) {
            // Verify the existing file's hash
            using var sha1 = SHA1.Create();
            string existingSha1;
            using (var existingFileStream = File.OpenRead(cacheFilePath)) {
                existingSha1 = Convert.ToHexString(sha1.ComputeHash(existingFileStream));
            }
            ;


            if (string.Equals(expectedSha1, existingSha1, StringComparison.OrdinalIgnoreCase)) {
                return cacheFilePath;
            }
            Console.Error.WriteLine($"Found existing modified or corrupted file: {cacheFilePath}. Expected {expectedSha1}, but got {existingSha1}. Deleting the file and downloading again.");
            File.Delete(cacheFilePath);
        }

        // Download the file
        using (var responseStream = _httpClient.GetStreamAsync(fileUrl).Result) {
            using var fileStream = File.Create(cacheFilePath);
            responseStream.CopyTo(fileStream);
        }
        ;
        // Verify the downloaded file
        string actualSha1;
        using (var verifyStream = File.OpenRead(cacheFilePath)) {
            actualSha1 = Convert.ToHexString(SHA1.Create().ComputeHash(verifyStream));
        }
        ;

        if (!string.Equals(expectedSha1, actualSha1, StringComparison.OrdinalIgnoreCase)) {
            File.Delete(cacheFilePath);
            throw new InvalidOperationException("SHA-1 verification failed");
        }
        return cacheFilePath;
    }

    public string GetSha1(string language, string edition, string architecture) {
        var fileXml = GetFileXml(language, edition, architecture);
        return GetElementValue(fileXml, "Sha1");
    }

    public string GetUrl(string language, string edition, string architecture) {
        var fileXml = GetFileXml(language, edition, architecture);
        return GetElementValue(fileXml, "FilePath");
    }

    private XElement GetFileXml(string language, string edition, string architecture) {
        if (string.Equals(architecture, "amd64", StringComparison.OrdinalIgnoreCase)) {
            architecture = "x64";
        }
        string param = $@"
[
  matches(LanguageCode, '^{RegEsc(language)}$', 'i') and
  matches(Edition, '^{RegEsc(edition)}$', 'i') and
  matches(Architecture, '^{RegEsc(architecture)}$', 'i')
]";
        return XmlDoc.XPath2SelectElement($"//File{param}")
            ?? throw new ArgumentException($"No matching file found for the specified parameters: \n{param}");
    }

    private static string GetElementValue(XElement parent, string elementName) {
        return parent.Element(elementName)?.Value
            ?? throw new ArgumentException($"{elementName} not found in XML");
    }
    private static string RegEsc(string input) {
        // Escape regex special chars for XPath regex, roughly same as .NET Regex.Escape
        return Regex.Escape(input);
    }
}