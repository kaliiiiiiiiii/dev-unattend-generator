namespace WinDevGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

public class ValidationError(string message) : Exception(message) { };

public class ElToritoBootCatalog {
    public const int VirtualSectorSize = 512;
    public const int PhysicalSectorSize = 2048;

    public class BootEntry {
        public byte BootIndicator { get; set; } = 0x00;
        public ushort LoadSegment { get; set; } = 0x0000;
        public byte SystemType { get; set; } = 0x00;
        public ushort SectorCount { get; set; } = 0x0000;
        public uint LoadRBA { get; set; } = 0x00000000;
        public byte PlatformId { get; set; } = 0x00;
        public string PlatformName { get; set; } = "Unknown";
        public string MediaDescription { get; set; } = "Unknown";
        public byte[]? BootImage { get; set; }
        public string BootImageSha { get; set; } = "N/A";

        public void Log() {
            Console.WriteLine("┌───────────────────────────────────");
            Console.WriteLine($"│ Boot Entry");
            Console.WriteLine("├───────────────────────────────────");
            Console.WriteLine($"│ {"Boot Indicator:",-20} 0x{BootIndicator:X2} {(BootIndicator == 0x88 ? "(bootable)" : "")}");
            Console.WriteLine($"│ {"Load Segment:",-20} 0x{LoadSegment:X4}");
            Console.WriteLine($"│ {"System Type:",-20} 0x{SystemType:X2}");
            Console.WriteLine($"│ {"Sector Count:",-20} {SectorCount}");
            Console.WriteLine($"│ {"Load RBA:",-20} {LoadRBA}");
            Console.WriteLine($"│ {"Platform Name:",-20} {PlatformName}");
            Console.WriteLine($"│ {"Platform ID:",-20} {PlatformId:X2}");
            Console.WriteLine($"│ {"SHA256:",-20} {BootImageSha}");
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
        0x00 => "x86 (BIOS)",
        0x01 => "PowerPC",
        0x02 => "Mac",
        0xEF => "EFI (UEFI)",
        _ => $"Unknown (0x{id:X2})"
    };

    public void ValidateBootEntriesEqual(List<BootEntry> newEntries) {
        // Check if counts match
        if (Entries.Count != newEntries.Count) {
            throw new ValidationError($"Entry count mismatch. Original: {Entries.Count}, New: {newEntries.Count}");
        }

        // Compare each entry
        for (int i = 0; i < Entries.Count; i++) {
            var original = Entries[i];
            var newEntry = newEntries[i];

            if (original.BootIndicator != newEntry.BootIndicator)
                throw new ValidationError($"BootIndicator mismatch at index {i}. Original: {original.BootIndicator}, New: {newEntry.BootIndicator}");

            if (original.LoadSegment != newEntry.LoadSegment)
                throw new ValidationError($"LoadSegment mismatch at index {i}. Original: {original.LoadSegment}, New: {newEntry.LoadSegment}");

            if (original.SystemType != newEntry.SystemType)
                throw new ValidationError($"SystemType mismatch at index {i}. Original: {original.SystemType}, New: {newEntry.SystemType}");

            if (original.SectorCount != newEntry.SectorCount)
                throw new ValidationError($"SectorCount mismatch at index {i}. Original: {original.SectorCount}, New: {newEntry.SectorCount}");

            if (original.LoadRBA != newEntry.LoadRBA)
                throw new ValidationError($"LoadRBA mismatch at index {i}. Original: {original.LoadRBA}, New: {newEntry.LoadRBA}");
            if (original.BootImageSha != newEntry.BootImageSha)
                throw new ValidationError($"BootImageSha mismatch at index {i}. Original: {original.BootImageSha}, New: {newEntry.BootImageSha}");
        }
    }

    public void Log() {
        // Calculate SHA256 of the catalog data if available


        Console.WriteLine("╔═══════════════════════════════════════");
        Console.WriteLine($"║ El Torito Boot Catalog ({Path.GetFileName(IsoPath)})");
        Console.WriteLine("╠═══════════════════════════════════════");
        Console.WriteLine($"║ {"Valid:",-20} {IsValid}");
        Console.WriteLine($"║ {"Manufacturer:",-20} {Manufacturer}");
        Console.WriteLine($"║ {"Platform ID:",-20} 0x{PlatformId:X2} ({PlatformName})");
        Console.WriteLine($"║ {"Catalog Sector:",-20} {CatalogSector}");
        Console.WriteLine($"║ {"Boot Entries:",-20} {Entries.Count}");
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

            int offset = 32; // Start after validation entry

            while (offset + 32 <= sector.Length) {
                byte entryType = sector[offset];

                if (entryType == 0x00) // End of entries
                    break;

                if (entryType == 0x91) { // Section header entry
                    byte platformId = sector[offset + 1];
                    ushort numEntries = BitConverter.ToUInt16(sector, offset + 2);
                    string idString = System.Text.Encoding.ASCII.GetString(sector, offset + 4, 28).TrimEnd('\0');
                    offset += 32;
                    continue;
                } else if (entryType == 0x88 || entryType == 0x00) { // Boot entry
                    var entry = new ElToritoBootCatalog.BootEntry {
                        BootIndicator = sector[offset],
                        LoadSegment = BitConverter.ToUInt16(sector, offset + 2),
                        PlatformId = sector[offset + 4],
                        SectorCount = BitConverter.ToUInt16(sector, offset + 6),
                        LoadRBA = BitConverter.ToUInt32(sector, offset + 8),
                        PlatformName = catalog.PlatformName,
                    };

                    entry.BootImage = ExtractBootImage(entry, isoPath);
                    if (entry.BootImage != null) {
                        byte[] hashBytes = SHA256.HashData(entry.BootImage);
                        entry.BootImageSha = Convert.ToHexStringLower(hashBytes);
                    }
                    catalog.Entries.Add(entry);
                    offset += 32;
                } else {
                    // Unknown entry type, skip
                    Console.WriteLine($"Unknown entry type 0x{entryType:X2} at offset {offset}");
                    offset += 32;
                }
            }
        } catch (Exception ex) {
            Console.WriteLine($"Error parsing El Torito data: {ex.Message}");
        }


        return catalog;
    }
    public static byte[]? ExtractBootImage(ElToritoBootCatalog.BootEntry entry, string isoPath, string? outputPath = null) {
        long sectorCount = entry.SectorCount;
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