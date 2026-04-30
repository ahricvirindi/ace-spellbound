using System.Drawing.Imaging;
using System.Text;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;

namespace ACE.Mods.Spellbound.DatExtractor;

internal static class Program
{
    private static int Main(string[] args)
    {
        // ACE.DatLoader's BinaryReaderExtensions.ReadObfuscatedString uses codepage 1252,
        // which is not registered by default on .NET 5+. PortalDatDatabase's ctor reads
        // GeneratorTable before any DatLoader code path that registers the provider, so
        // do it here.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        string datsDir = "DATS";
        string outDir = Path.Combine("out", "dat-extract");

        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--dats": datsDir = args[i + 1]; i++; break;
                case "--out": outDir = args[i + 1]; i++; break;
            }
        }

        datsDir = Path.GetFullPath(datsDir);
        outDir = Path.GetFullPath(outDir);

        if (!Directory.Exists(datsDir))
        {
            Console.Error.WriteLine($"DATS directory not found: {datsDir}");
            return 1;
        }

        Console.WriteLine($"DATS:   {datsDir}");
        Console.WriteLine($"Output: {outDir}");
        Console.WriteLine();
        Console.WriteLine("Loading client_portal.dat (cell dat skipped)...");
        DatManager.Initialize(datsDir, keepOpen: true, loadCell: false);
        if (DatManager.PortalDat is null)
        {
            Console.Error.WriteLine("Failed to load client_portal.dat");
            return 1;
        }

        Console.WriteLine($"  {DatManager.PortalDat.AllFiles.Count:N0} files in portal dat");
        Console.WriteLine();

        var skillsDir = Path.Combine(outDir, "skills");
        var spellsDir = Path.Combine(outDir, "spells");
        var uncategorizedDir = Path.Combine(outDir, "uncategorized");
        Directory.CreateDirectory(skillsDir);
        Directory.CreateDirectory(spellsDir);
        Directory.CreateDirectory(uncategorizedDir);

        var categorized = new HashSet<uint>();

        ExtractSkills(skillsDir, categorized);
        ExtractSpells(spellsDir, categorized);
        ExtractUncategorized(uncategorizedDir, categorized);

        Console.WriteLine();
        Console.WriteLine("Done.");
        return 0;
    }

    private static void ExtractSkills(string dir, HashSet<uint> categorized)
    {
        Console.WriteLine("Extracting skill icons...");
        var taken = new HashSet<string>();
        int ok = 0, fail = 0;

        foreach (var skill in DatManager.PortalDat!.SkillTable.SkillBaseHash.Values)
        {
            if (skill.IconId == 0 || string.IsNullOrWhiteSpace(skill.Name)) continue;

            var slug = UniqueSlug(skill.Name, taken);
            try
            {
                if (TrySaveTexture(skill.IconId, dir, slug))
                {
                    categorized.Add(skill.IconId);
                    ok++;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  skill '{skill.Name}' ({skill.IconId:X8}): {ex.Message}");
                fail++;
            }
        }

        Console.WriteLine($"  {ok} skill icons extracted ({fail} failed)");
        Console.WriteLine();
    }

    private static void ExtractSpells(string dir, HashSet<uint> categorized)
    {
        Console.WriteLine("Extracting spell icons...");
        var taken = new HashSet<string>();
        int ok = 0, fail = 0;

        foreach (var spell in DatManager.PortalDat!.SpellTable.Spells.Values)
        {
            if (spell.Icon == 0 || string.IsNullOrWhiteSpace(spell.Name)) continue;

            var slug = UniqueSlug(spell.Name, taken);
            try
            {
                if (TrySaveTexture(spell.Icon, dir, slug))
                {
                    categorized.Add(spell.Icon);
                    ok++;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  spell '{spell.Name}' ({spell.Icon:X8}): {ex.Message}");
                fail++;
            }
        }

        Console.WriteLine($"  {ok} spell icons extracted ({fail} failed)");
        Console.WriteLine();
    }

    private static void ExtractUncategorized(string dir, HashSet<uint> categorized)
    {
        Console.WriteLine("Extracting uncategorized textures...");
        int ok = 0, fail = 0, lastReport = 0;

        foreach (var entry in DatManager.PortalDat!.AllFiles)
        {
            if (entry.Value.GetFileType(DatDatabaseType.Portal) != DatFileType.Texture) continue;
            if (categorized.Contains(entry.Key)) continue;

            try
            {
                var tex = DatManager.PortalDat.ReadFromDat<Texture>(entry.Key);
                if (tex.Length == 0) continue;
                tex.ExportTexture(dir);
                ok++;
            }
            catch (Exception ex)
            {
                fail++;
                if (fail <= 5)
                    Console.Error.WriteLine($"  {entry.Key:X8}: {ex.Message}");
            }

            if (ok - lastReport >= 500)
            {
                Console.WriteLine($"  ...{ok:N0} so far");
                lastReport = ok;
            }
        }

        Console.WriteLine($"  {ok:N0} uncategorized textures extracted ({fail} failed)");
    }

    private static bool TrySaveTexture(uint id, string dir, string baseName)
    {
        var tex = DatManager.PortalDat!.ReadFromDat<Texture>(id);
        if (tex.Length == 0) return false;

        if (tex.Format == SurfacePixelFormat.PFID_CUSTOM_RAW_JPEG)
        {
            File.WriteAllBytes(Path.Combine(dir, baseName + ".jpg"), tex.SourceData);
        }
        else
        {
            using var bmp = tex.GetBitmap();
            bmp.Save(Path.Combine(dir, baseName + ".png"), ImageFormat.Png);
        }
        return true;
    }

    private static string UniqueSlug(string name, HashSet<string> taken)
    {
        var slug = Slugify(name);
        if (slug.Length == 0) slug = "unnamed";
        if (taken.Add(slug)) return slug;

        for (int i = 2; ; i++)
        {
            var candidate = $"{slug}_{i}";
            if (taken.Add(candidate)) return candidate;
        }
    }

    private static string Slugify(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '_')
                sb.Append('_');
        }
        while (sb.Length > 0 && sb[^1] == '_')
            sb.Length--;
        return sb.ToString();
    }
}
