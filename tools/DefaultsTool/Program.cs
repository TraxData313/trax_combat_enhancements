using System.Text;
using TraxCombat.Core;

namespace TraxCombat.Tools;

/// <summary>
/// defaults.json upkeep (DESIGN §2c, <see cref="DefaultsFile"/>).
///
///   refresh (default)  rewrite every // comment, group heading and the key order from the
///                      schema; every value is kept exactly. Refuses (exit 1) while a key is
///                      missing or unknown or a value is bad - it never invents or changes a value.
///   check              report only; exit 1 unless the file is complete and in step.
///   [--file path]      another file (e.g. one exported from MCM) instead of the repo's.
///
/// To add a new setting's default: put "Key": value anywhere inside the braces, then refresh.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        string command = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? "refresh";
        int fileAt = Array.IndexOf(args, "--file");
        string? path = fileAt >= 0 && fileAt + 1 < args.Length ? Path.GetFullPath(args[fileAt + 1]) : FindRepoFile();
        if (path == null || !File.Exists(path))
        {
            Console.Error.WriteLine("defaults.json not found - run from inside the repo, or pass --file <path>.");
            return 2;
        }

        string text = File.ReadAllText(path, Encoding.UTF8);
        var check = DefaultsFile.Check(text);
        Console.WriteLine(path);

        switch (command)
        {
            case "check":
                Console.WriteLine(check.Describe());
                return check.Ok ? 0 : 1;

            case "refresh":
                if (!check.ValuesOk)
                {
                    Console.WriteLine(check.Describe());
                    Console.WriteLine();
                    Console.WriteLine("Not refreshed: fix the lines above by hand first - refresh never invents or changes a value.");
                    Console.WriteLine("(A new setting: add \"Key\": value anywhere inside the braces, then refresh again.)");
                    return 1;
                }
                if (check.CommentsDifference == null)
                {
                    Console.WriteLine("already in step with the schema - " + check.Values.Count + " values, nothing to do");
                    return 0;
                }
                File.WriteAllText(path, DefaultsFile.Write(check.Values), new UTF8Encoding(false));
                var again = DefaultsFile.Check(File.ReadAllText(path, Encoding.UTF8));
                if (!again.Ok)
                {
                    Console.WriteLine("rewritten, but the result does not check clean (a bug): " + again.Describe());
                    return 1;
                }
                Console.WriteLine("refreshed: comments, headings and order rewritten from the schema; all " + check.Values.Count + " values kept");
                return 0;

            default:
                Console.Error.WriteLine("unknown command '" + command + "' - use refresh (default) or check, optionally with --file <path>");
                return 2;
        }
    }

    /// <summary>defaults.json beside TraxCombatEnhancements.sln, from the current folder upwards.</summary>
    private static string? FindRepoFile()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TraxCombatEnhancements.sln")))
                return Path.Combine(dir.FullName, DefaultsFile.FileName);
        }
        return null;
    }
}
