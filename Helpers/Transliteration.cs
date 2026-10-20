using System.Text;

namespace CityFix_backend.Helpers
{
    // Ukrainian → Latin by the official system (Cabinet of Ministers resolution No. 55, 2010), used for
    // people's names: Оксана Мельник → Oksana Melnyk, Юлія → Yuliia, Згурський → Zghurskyi.
    // Є, Ї, Й, Ю, Я are written Ye, Yi, Y, Yu, Ya at the start of a word and ie, i, i, iu, ia elsewhere;
    // the soft sign and the apostrophe are dropped.
    public static class Transliteration
    {
        private static readonly Dictionary<char, string> Letters = new()
        {
            ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "h", ['ґ'] = "g", ['д'] = "d", ['е'] = "e",
            ['є'] = "ie", ['ж'] = "zh", ['з'] = "z", ['и'] = "y", ['і'] = "i", ['ї'] = "i", ['й'] = "i",
            ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r",
            ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch",
            ['ш'] = "sh", ['щ'] = "shch", ['ь'] = "", ['ю'] = "iu", ['я'] = "ia", ['\''] = "", ['’'] = "", ['ʼ'] = ""
        };

        private static readonly Dictionary<char, string> WordStart = new()
        {
            ['є'] = "ye", ['ї'] = "yi", ['й'] = "y", ['ю'] = "yu", ['я'] = "ya"
        };

        public static string ToLatin(string text)
        {
            var result = new StringBuilder(text.Length * 2);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                var lower = char.ToLowerInvariant(c);
                var atWordStart = i == 0 || !char.IsLetter(text[i - 1]) && text[i - 1] is not ('\'' or '’' or 'ʼ');

                string? latin;
                if (lower == 'г' && i > 0 && char.ToLowerInvariant(text[i - 1]) == 'з') latin = "gh";
                else if (atWordStart && WordStart.TryGetValue(lower, out var start)) latin = start;
                else if (!Letters.TryGetValue(lower, out latin)) { result.Append(c); continue; }

                if (latin.Length > 0 && char.IsUpper(c))
                {
                    // Capital first letter only (Shch, not SHCH), unless the whole word is in capitals.
                    var nextUpper = i + 1 < text.Length && char.IsUpper(text[i + 1]);
                    latin = nextUpper ? latin.ToUpperInvariant() : char.ToUpperInvariant(latin[0]) + latin[1..];
                }
                result.Append(latin);
            }
            return result.ToString();
        }
    }
}
