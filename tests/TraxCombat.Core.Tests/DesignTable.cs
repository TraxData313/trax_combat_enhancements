using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>docs/DESIGN.md's "## Parameters" table: every key and its INITIAL value (the
/// Default column). The "## Planned parameters" table is not read.</summary>
internal static class DesignTable
{
    /// <summary>A file of the repo, found by walking up from the test binaries.</summary>
    public static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not find " + relative + " above " + AppContext.BaseDirectory);
    }

    /// <summary>Key → Default-column text, in table order.</summary>
    public static Dictionary<string, string> Rows()
    {
        var lines = File.ReadAllLines(RepoFile(Path.Combine("docs", "DESIGN.md")));
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        bool inSection = false;
        var row = new Regex(@"^\|\s*`(?<key>[A-Za-z0-9]+)`\s*\|\s*(?<def>[^|]+?)\s*\|");
        foreach (var line in lines)
        {
            if (line.StartsWith("## ")) inSection = line.Trim() == "## Parameters";
            if (!inSection) continue;
            var m = row.Match(line);
            if (m.Success) rows.Add(m.Groups["key"].Value, m.Groups["def"].Value);
        }
        if (rows.Count == 0) throw new InvalidDataException("no parameter rows found in DESIGN.md");
        return rows;
    }

    /// <summary>A Default-column text as the JSON value defaults.json would hold: true/false, a
    /// whole number (an integer token), or a number.</summary>
    public static JToken ToToken(string text)
    {
        if (text == "true") return new JValue(true);
        if (text == "false") return new JValue(false);
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole)) return new JValue(whole);
        return new JValue(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// The unit tests run the schema on DESIGN.md's Default column (the INITIAL values), not on
/// defaults.json: Anton tunes defaults.json, and a tuned default must never break a test that
/// counts blows. defaults.json itself is checked by <see cref="DefaultsFileTests"/> (and its
/// embedded copy by the offline smoke, which runs the real DLL).
/// </summary>
internal static class TestDefaults
{
    public const string SourceName = "DESIGN.md's Default column (unit tests)";

#pragma warning disable CA2255 // a test assembly: the schema must see DESIGN's values before any test touches it
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void UseDesignValues()
    {
        var values = DesignTable.Rows().ToDictionary(r => r.Key, r => DesignTable.ToToken(r.Value));
        DefaultsFile.UseValuesForTests(values, SourceName);
    }
}
