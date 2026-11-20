using System.Collections.Generic;
using System.Text;

namespace DriftKuwait
{
    /// Minimal Arabic shaper for Unity's left-to-right text: picks the joined letter forms
    /// (isolated / final / initial / medial), builds lam-alef ligatures, strips harakat,
    /// then reverses for display. Use it for pure-Arabic strings; render numbers separately.
    public static class ArabicShaper
    {
        // isolated, final, initial, medial (0 = form does not exist)
        static readonly Dictionary<char, char[]> Forms = new Dictionary<char, char[]>
        {
            ['ء'] = new[] { 'ﺀ', '\0', '\0', '\0' },
            ['آ'] = new[] { 'ﺁ', 'ﺂ', '\0', '\0' },
            ['أ'] = new[] { 'ﺃ', 'ﺄ', '\0', '\0' },
            ['ؤ'] = new[] { 'ﺅ', 'ﺆ', '\0', '\0' },
            ['إ'] = new[] { 'ﺇ', 'ﺈ', '\0', '\0' },
            ['ئ'] = new[] { 'ﺉ', 'ﺊ', 'ﺋ', 'ﺌ' },
            ['ا'] = new[] { 'ﺍ', 'ﺎ', '\0', '\0' },
            ['ب'] = new[] { 'ﺏ', 'ﺐ', 'ﺑ', 'ﺒ' },
            ['ة'] = new[] { 'ﺓ', 'ﺔ', '\0', '\0' },
            ['ت'] = new[] { 'ﺕ', 'ﺖ', 'ﺗ', 'ﺘ' },
            ['ث'] = new[] { 'ﺙ', 'ﺚ', 'ﺛ', 'ﺜ' },
            ['ج'] = new[] { 'ﺝ', 'ﺞ', 'ﺟ', 'ﺠ' },
            ['ح'] = new[] { 'ﺡ', 'ﺢ', 'ﺣ', 'ﺤ' },
            ['خ'] = new[] { 'ﺥ', 'ﺦ', 'ﺧ', 'ﺨ' },
            ['د'] = new[] { 'ﺩ', 'ﺪ', '\0', '\0' },
            ['ذ'] = new[] { 'ﺫ', 'ﺬ', '\0', '\0' },
            ['ر'] = new[] { 'ﺭ', 'ﺮ', '\0', '\0' },
            ['ز'] = new[] { 'ﺯ', 'ﺰ', '\0', '\0' },
            ['س'] = new[] { 'ﺱ', 'ﺲ', 'ﺳ', 'ﺴ' },
            ['ش'] = new[] { 'ﺵ', 'ﺶ', 'ﺷ', 'ﺸ' },
            ['ص'] = new[] { 'ﺹ', 'ﺺ', 'ﺻ', 'ﺼ' },
            ['ض'] = new[] { 'ﺽ', 'ﺾ', 'ﺿ', 'ﻀ' },
            ['ط'] = new[] { 'ﻁ', 'ﻂ', 'ﻃ', 'ﻄ' },
            ['ظ'] = new[] { 'ﻅ', 'ﻆ', 'ﻇ', 'ﻈ' },
            ['ع'] = new[] { 'ﻉ', 'ﻊ', 'ﻋ', 'ﻌ' },
            ['غ'] = new[] { 'ﻍ', 'ﻎ', 'ﻏ', 'ﻐ' },
            ['ف'] = new[] { 'ﻑ', 'ﻒ', 'ﻓ', 'ﻔ' },
            ['ق'] = new[] { 'ﻕ', 'ﻖ', 'ﻗ', 'ﻘ' },
            ['ك'] = new[] { 'ﻙ', 'ﻚ', 'ﻛ', 'ﻜ' },
            ['ل'] = new[] { 'ﻝ', 'ﻞ', 'ﻟ', 'ﻠ' },
            ['م'] = new[] { 'ﻡ', 'ﻢ', 'ﻣ', 'ﻤ' },
            ['ن'] = new[] { 'ﻥ', 'ﻦ', 'ﻧ', 'ﻨ' },
            ['ه'] = new[] { 'ﻩ', 'ﻪ', 'ﻫ', 'ﻬ' },
            ['و'] = new[] { 'ﻭ', 'ﻮ', '\0', '\0' },
            ['ى'] = new[] { 'ﻯ', 'ﻰ', '\0', '\0' },
            ['ي'] = new[] { 'ﻱ', 'ﻲ', 'ﻳ', 'ﻴ' },
        };

        // lam + (alef variant) -> isolated, final ligature
        static readonly Dictionary<char, char[]> LamAlef = new Dictionary<char, char[]>
        {
            ['آ'] = new[] { 'ﻵ', 'ﻶ' },
            ['أ'] = new[] { 'ﻷ', 'ﻸ' },
            ['إ'] = new[] { 'ﻹ', 'ﻺ' },
            ['ا'] = new[] { 'ﻻ', 'ﻼ' },
        };

        static bool IsHaraka(char c) => (c >= 'ً' && c <= 'ٟ') || c == 'ٰ';
        static bool JoinsForward(char c) => Forms.TryGetValue(c, out var f) && f[2] != '\0';
        static bool IsLetter(char c) => Forms.ContainsKey(c);

        public static string Shape(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var clean = new List<char>(s.Length);
            foreach (char c in s) if (!IsHaraka(c)) clean.Add(c);

            var shaped = new List<char>(clean.Count);
            for (int i = 0; i < clean.Count; i++)
            {
                char c = clean[i];
                if (!IsLetter(c)) { shaped.Add(c); continue; }

                char prev = i > 0 ? clean[i - 1] : '\0';
                bool joinPrev = prev != '\0' && JoinsForward(prev);

                // Lam-alef ligature
                if (c == 'ل' && i + 1 < clean.Count && LamAlef.TryGetValue(clean[i + 1], out var lig))
                {
                    shaped.Add(joinPrev ? lig[1] : lig[0]);
                    i++;
                    continue;
                }

                char next = i + 1 < clean.Count ? clean[i + 1] : '\0';
                bool joinNext = JoinsForward(c) && next != '\0' && IsLetter(next);
                var f = Forms[c];
                char form;
                if (joinPrev && joinNext && f[3] != '\0') form = f[3];
                else if (joinPrev && f[1] != '\0') form = f[1];
                else if (joinNext && f[2] != '\0') form = f[2];
                else form = f[0];
                shaped.Add(form);
            }

            // Reverse for LTR rendering, but keep runs of Latin digits/letters in order.
            var sb = new StringBuilder(shaped.Count);
            int k = shaped.Count - 1;
            while (k >= 0)
            {
                if (IsLtr(shaped[k]))
                {
                    int end = k;
                    while (k >= 0 && (IsLtr(shaped[k]) || (shaped[k] == ' ' && k > 0 && IsLtr(shaped[k - 1])))) k--;
                    for (int j = k + 1; j <= end; j++) sb.Append(shaped[j]);
                }
                else
                {
                    char ch = shaped[k];
                    sb.Append(Mirror(ch));
                    k--;
                }
            }
            return sb.ToString();
        }

        static bool IsLtr(char c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == ',';

        static char Mirror(char c)
        {
            switch (c)
            {
                case '(': return ')';
                case ')': return '(';
                case '[': return ']';
                case ']': return '[';
                case '«': return '»';
                case '»': return '«';
                default: return c;
            }
        }
    }
}
