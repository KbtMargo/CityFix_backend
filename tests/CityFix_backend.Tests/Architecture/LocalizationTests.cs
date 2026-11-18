using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CityFix_backend.Tests.Infrastructure;

namespace CityFix_backend.Tests.Architecture;

[Trait("Category", "Architecture")]
public partial class LocalizationTests
{
    private static Dictionary<string, string> Load(string file) =>
        XDocument.Load(Path.Combine(CityFixApp.ProjectDirectory, "Resources", file))
            .Root!.Elements("data")
            .ToDictionary(d => d.Attribute("name")!.Value, d => d.Element("value")?.Value ?? "");

    private static readonly Dictionary<string, string> Ukrainian = Load("SharedResource.resx");
    private static readonly Dictionary<string, string> English = Load("SharedResource.en.resx");

    [Fact]
    public void Both_languages_have_the_same_keys()
    {
        Assert.Empty(Ukrainian.Keys.Except(English.Keys));
        Assert.Empty(English.Keys.Except(Ukrainian.Keys));
    }

    [Fact]
    public void No_text_is_empty()
    {
        Assert.Empty(Ukrainian.Where(p => string.IsNullOrWhiteSpace(p.Value)).Select(p => "uk:" + p.Key)
            .Concat(English.Where(p => string.IsNullOrWhiteSpace(p.Value)).Select(p => "en:" + p.Key)));
    }

    [GeneratedRegex(@"\{(\d+)(?:[:,][^}]*)?\}")]
    private static partial Regex Placeholder();

    [Fact]
    public void Translations_use_the_same_placeholders()
    {
        static string Placeholders(string text) => string.Join(",", Placeholder().Matches(text).Select(m => m.Groups[1].Value).Distinct().Order());
        var mismatched = Ukrainian.Where(p => English.TryGetValue(p.Key, out var en) && Placeholders(p.Value) != Placeholders(en)).Select(p => p.Key).ToList();
        Assert.Empty(mismatched);
    }

    [Fact]
    public void English_texts_are_actually_translated()
    {
        var cyrillicInEnglish = English.Where(p => p.Value.Any(c => c is >= 'Ѐ' and <= 'ӿ') && !p.Key.StartsWith("Lang.")).Select(p => p.Key).ToList();
        Assert.Empty(cyrillicInEnglish);
    }

    private static IEnumerable<string> AttributeKeys()
    {
        var types = typeof(SharedResource).Assembly.GetTypes();
        foreach (var member in types.SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)))
        {
            if (member.GetCustomAttribute<DisplayAttribute>()?.Name is string display) yield return display;
            foreach (var validation in member.GetCustomAttributes<ValidationAttribute>())
            {
                if (validation.ErrorMessage is string message) yield return message;
            }
        }
    }

    [Fact]
    public void Every_display_name_and_validation_message_exists()
    {
        var keys = AttributeKeys().Distinct().ToList();
        Assert.NotEmpty(keys);
        Assert.Empty(keys.Where(k => !Ukrainian.ContainsKey(k)));
    }

    [GeneratedRegex("""(?:\bL\[|SharedText\.(?:Get|Format)\(|LabelKey\s*=\s*|ServiceValidationException\("[^"]*",\s*)"([A-Z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+)+)"[\],)]""")]
    private static partial Regex KeyUsage();

    [Fact]
    public void Every_key_used_in_code_and_views_exists()
    {
        var root = CityFixApp.ProjectDirectory;
        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".cshtml"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"));

        var used = files.SelectMany(f => KeyUsage().Matches(File.ReadAllText(f)).Select(m => (File: Path.GetRelativePath(root, f), Key: m.Groups[1].Value))).ToList();

        Assert.True(used.Count > 300, $"Only {used.Count} key usages found; the scanner is probably broken.");
        Assert.Empty(used.Where(u => !Ukrainian.ContainsKey(u.Key)).Select(u => $"{u.File}: {u.Key}").Distinct());
    }
}
