namespace Generate;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Security.Cryptography;


public class ElToritoBootCatalog {
    public const int VirtualSectorSize = 512;
    public const int PhysicalSectorSize = 2048;

    public class BootEntry {
        public byte BootIndicator { get; set; } = 0x00;
        public byte MediaType { get; set; } = 0x00;
        public ushort LoadSegment { get; set; } = 0x0000;
        public byte SystemType { get; set; } = 0x00;
        public ushort SectorCount { get; set; } = 0x0000;
        public uint LoadRBA { get; set; } = 0x00000000;
        public string PlatformId { get; set; } = "Unknown";
        public long ActualSectorCount { get; set; } = 0;
        public string MediaDescription { get; set; } = "Unknown";

        public void CalculateActualSectorCount() {
            switch (MediaType) {
                case 0: // No emulation
                    ActualSectorCount = 0;
                    MediaDescription = "No emulation";
                    break;
                case 1: // 1.2MB floppy
                    ActualSectorCount = (1200 * 1024) / VirtualSectorSize;
                    MediaDescription = "1.2MB floppy";
                    break;
                case 2: // 1.44MB floppy
                    ActualSectorCount = (1440 * 1024) / VirtualSectorSize;
                    MediaDescription = "1.44MB floppy";
                    break;
                case 3: // 2.88MB floppy
                    ActualSectorCount = (2880 * 1024) / VirtualSectorSize;
                    MediaDescription = "2.88MB floppy";
                    break;
                case 4: // Hard disk
                    ActualSectorCount = SectorCount; // Will be updated after reading MBR
                    MediaDescription = "Hard disk";
                    break;
                default:
                    ActualSectorCount = SectorCount;
                    MediaDescription = $"Unknown (0x{MediaType:X2})";
                    break;
            }
        }

        public void Log() {
            Console.WriteLine("┌───────────────────────────────────");
            Console.WriteLine($"│ Boot Entry");
            Console.WriteLine("├───────────────────────────────────");
            Console.WriteLine($"│ {"Boot Indicator:",-20} 0x{BootIndicator:X2} {(BootIndicator == 0x88 ? "(bootable)" : "")}");
            Console.WriteLine($"│ {"Media Type:",-20} 0x{MediaType:X2} ({MediaDescription})");
            Console.WriteLine($"│ {"Load Segment:",-20} 0x{LoadSegment:X4}");
            Console.WriteLine($"│ {"System Type:",-20} 0x{SystemType:X2}");
            Console.WriteLine($"│ {"Sector Count:",-20} {SectorCount} (reported)");
            Console.WriteLine($"│ {"Actual Sectors:",-20} {ActualSectorCount} (calculated)");
            Console.WriteLine($"│ {"Load RBA:",-20} {LoadRBA}");
            Console.WriteLine($"│ {"Platform ID:",-20} {PlatformId}");
            Console.WriteLine("└───────────────────────────────────");
        }
    }

    public required string IsoPath { get; set; }

    public List<BootEntry> Entries { get; } = new List<BootEntry>();
    public bool IsValid { get; set; }
    public byte PlatformId { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public uint CatalogSector { get; set; }

    
    public string PlatformName => GetPlatformName(PlatformId);

    public static string GetPlatformName(byte id) => id switch {
        0x00 => "x86",
        0x01 => "PowerPC",
        0x02 => "Mac",
        _ => $"Unknown (0x{id:X2})"
    };
    public byte[]? BootImage { get; set; }

    public void Log() {
        // Calculate SHA256 of the catalog data if available
        string catalogSha = "N/A";
        if (BootImage != null && BootImage.Length > 0) {
            byte[] hashBytes = SHA1.HashData(BootImage);
            catalogSha = Convert.ToHexStringLower(hashBytes);
        }

        Console.WriteLine("╔═══════════════════════════════════════");
        Console.WriteLine("║ El Torito Boot Catalog");
        Console.WriteLine("╠═══════════════════════════════════════");
        Console.WriteLine($"║ {"Valid:",-20} {IsValid}");
        Console.WriteLine($"║ {"Manufacturer:",-20} {Manufacturer}");
        Console.WriteLine($"║ {"Platform ID:",-20} 0x{PlatformId:X2} ({PlatformName})");
        Console.WriteLine($"║ {"Catalog Sector:",-20} {CatalogSector}");
        Console.WriteLine($"║ {"Boot Entries:",-20} {Entries.Count}");
        Console.WriteLine($"║ {"Catalog SHA256:",-20} {catalogSha}");
        Console.WriteLine("╚═══════════════════════════════════════");

        foreach (var entry in Entries) {
            Console.WriteLine();
            entry.Log();
        }

        if (Entries.Count == 0) {
            Console.WriteLine("  No boot entries found");
        }
    }
}
public static class ElToritoParser {
    public static ElToritoBootCatalog ParseElToritoData(string isoPath) {
        var catalog = new ElToritoBootCatalog() { IsoPath = isoPath };

        try {
            using var fs = new FileStream(isoPath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            // Read Sector 17 which should contain Boot Record Volume Descriptor
            fs.Seek(17 * ElToritoBootCatalog.PhysicalSectorSize, SeekOrigin.Begin);
            byte[] sector = reader.ReadBytes(ElToritoBootCatalog.PhysicalSectorSize);

            // Check for ISO 9660 and El Torito signatures
            string isoIdent = new(System.Text.Encoding.ASCII.GetChars(sector, 1, 5));
            string toritoSpec = new string(System.Text.Encoding.ASCII.GetChars(sector, 7, 32)).TrimEnd('\0');

            if (isoIdent != "CD001" || toritoSpec != "EL TORITO SPECIFICATION") {
                throw new InvalidDataException("This does not appear to be a bootable CD image");
            }

            // Get boot catalog sector (little-endian)
            uint catalogSector = BitConverter.ToUInt32(sector, 0x47);
            catalog.CatalogSector = catalogSector;

            // Read boot catalog
            fs.Seek((long)catalogSector * ElToritoBootCatalog.PhysicalSectorSize, SeekOrigin.Begin);
            sector = reader.ReadBytes(ElToritoBootCatalog.PhysicalSectorSize);

            // Parse validation entry (first 32 bytes)
            byte header = sector[0];
            byte platform = sector[1];
            string manufacturer = System.Text.Encoding.ASCII.GetString(sector, 4, 24).TrimEnd('\0');
            ushort signature = BitConverter.ToUInt16(sector, 30);

            if (header != 0x01 || signature != 0xAA55) {
                throw new InvalidDataException("Invalid validation entry in boot catalog");
            }

            catalog.IsValid = true;
            catalog.PlatformId = platform;
            catalog.Manufacturer = manufacturer;

            // Parse initial/default boot entry (next 32 bytes)
            if (sector.Length >= 64) // Ensure we have enough data
            {
                var entry = new ElToritoBootCatalog.BootEntry {
                    BootIndicator = sector[32],
                    MediaType = sector[33],
                    LoadSegment = BitConverter.ToUInt16(sector, 34),
                    SystemType = sector[36],
                    SectorCount = BitConverter.ToUInt16(sector, 38),
                    LoadRBA = BitConverter.ToUInt32(sector, 40),
                    PlatformId = catalog.PlatformName
                };

                entry.CalculateActualSectorCount();

                // Special handling for hard disk emulation
                if (entry.MediaType == 4) {
                    // Read MBR to determine actual image size
                    fs.Seek((long)entry.LoadRBA * ElToritoBootCatalog.PhysicalSectorSize, SeekOrigin.Begin);
                    byte[] mbr = reader.ReadBytes(ElToritoBootCatalog.PhysicalSectorSize);

                    // Get first partition info (offset 446 in MBR)
                    uint firstSector = BitConverter.ToUInt32(mbr, 446 + 8);
                    uint partitionSize = BitConverter.ToUInt32(mbr, 446 + 12);
                    entry.ActualSectorCount = firstSector + partitionSize;
                }

                if (entry.BootIndicator == 0x88) {
                    catalog.Entries.Add(entry);
                }
            }
        } catch (Exception ex) {
            Console.WriteLine($"Error parsing El Torito data: {ex.Message}");
        }

        catalog.BootImage = ExtractBootImage(catalog, isoPath);
        return catalog;
    }
    public static byte[]? ExtractBootImage(ElToritoBootCatalog catalog, string isoPath, string? outputPath = null) {
        if (catalog.Entries.Count == 0) {
            return null;
        }

        var entry = catalog.Entries[0];
        long sectorCount = entry.ActualSectorCount > 0 ? entry.ActualSectorCount : entry.SectorCount;
        long byteCount = sectorCount * ElToritoBootCatalog.VirtualSectorSize;

        using var fs = new FileStream(isoPath, FileMode.Open, FileAccess.Read);
        fs.Seek((long)entry.LoadRBA * ElToritoBootCatalog.PhysicalSectorSize, SeekOrigin.Begin);

        byte[] image = new byte[byteCount];
        int bytesRead = fs.Read(image, 0, image.Length);

        if (bytesRead != image.Length) {
            throw new IOException("Failed to read complete boot image");
        }

        if (!string.IsNullOrEmpty(outputPath)) {
            File.WriteAllBytes(outputPath, image);
            Console.WriteLine($"Boot image written to {outputPath}");
        }

        return image;
    }
}