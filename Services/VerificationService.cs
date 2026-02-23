using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;

namespace SmartSlot.Services
{
    public class VerificationService
    {
        // Extract vehicle plate number using regex pattern
        public string ExtractPlateNumber(string text)
        {
            // Indian plate pattern: TN01AB1234
            var pattern = @"[A-Z]{2}[\s-]?[0-9]{2}[\s-]?[A-Z]{1,2}[\s-]?[0-9]{4}";
            var match = Regex.Match(text.ToUpper(), pattern);
            return match.Success ? Regex.Replace(match.Value, @"[\s-]", "") : "";
        }

        // Extract name from document text
        public string ExtractName(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Clean line breaks
            text = text.Replace("\\r\\n", "\n")
                       .Replace("\\R\\N", "\n")
                       .Replace("\\n", "\n")
                       .Replace("\r\n", "\n")
                       .Replace("\r", "\n");

            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            var skipWords = new[] {
        "address", "signature", "holder", "cuddalore", "nagar",
        "street", "road", "dist", "pin", "state", "dob", "validity",
        "issue", "rto", "transport", "vehicle", "engine", "chassis",
        "fuel", "class", "registration", "tamil", "nadu", "india",
        "driving", "licence", "license", "card", "date", "birth",
        "father", "husband", "son", "of", "w/o", "s/o", "d/o",
        "blood", "cov", "ref", "mobile", "phone", "flat", "plot",
        "door", "near", "post", "taluk", "village", "ward",
        "south", "north", "east", "west", "colony", "govi",
        "addres", "vanniyar", "ayam", "cudoal", "www", "http",
        "regn", "number", "reg", "no", "form", "smart", "card",
        "republic", "government", "dept", "department", "ministry",
        "authority", "serial", "dl", "rc", "book", "type",
        "owner", "name", "registered", "registrant",
        "permanent", "present", "financer", "hypothecation",
        "manufacturer", "model", "colour", "color", "maker",
        "body", "seating", "standing", "unladen", "laden",
        "insurance", "fitness", "tax", "permit", "pucc",
        "noc", "blacklist", "challan", "financed"
    };

            foreach (var line in lines)
            {
                var clean = line.Trim();

                // Remove label prefixes like "NAME:", "Name:", "NAME " etc
                clean = System.Text.RegularExpressions.Regex.Replace(
                    clean, @"^(NAME|name|Name)\s*[:\.]?\s*", "").Trim();

                // Skip empty or too short
                if (clean.Length < 2) continue;

                // Skip lines starting with relation prefixes
                if (System.Text.RegularExpressions.Regex.IsMatch(clean,
                    @"^(OF|S/O|W/O|D/O|C/O)\s", RegexOptions.IgnoreCase))
                    continue;

                // Skip lines with numbers
                if (System.Text.RegularExpressions.Regex.IsMatch(clean, @"\d{2,}"))
                    continue;

                // Skip if contains skip words
                bool shouldSkip = skipWords.Any(s =>
                    clean.ToLower().Split(' ').Any(w => w == s));
                if (shouldSkip) continue;

                // Skip special character heavy lines
                if (clean.Count(c => !char.IsLetterOrDigit(c) && c != ' ' && c != '.') > 2)
                    continue;

                // Pattern 1 — Initial + Name: "G MARUDHAJALAMOORTHY"
                // Single letter followed by long name
                if (System.Text.RegularExpressions.Regex.IsMatch(clean,
                    @"^[A-Z]\s+[A-Za-z]{4,}"))
                {
                    var name = clean.Replace(".", " ").Trim().ToUpper();
                    name = System.Text.RegularExpressions.Regex.Replace(
                        name, @"\s+", " ").Trim();
                    return name;
                }

                // Pattern 2 — Name with initial at end: "MARUDHAJALAMOORTHY G"
                // Long name followed by single letter
                if (System.Text.RegularExpressions.Regex.IsMatch(clean,
                    @"^[A-Za-z]{4,}.*\s+[A-Z]\.?$"))
                {
                    // Move initial to front
                    var parts = clean.Trim().ToUpper().Split(' ');
                    var initial = parts.Last().Replace(".", "");
                    var namePart = string.Join(" ", parts.Take(parts.Length - 1));
                    var name = $"{initial} {namePart}".Trim();
                    name = System.Text.RegularExpressions.Regex.Replace(
                        name, @"\s+", " ").Trim();
                    return name;
                }

                // Pattern 3 — Normal full name: "MARUDHAJALAMOORTHY"
                if (System.Text.RegularExpressions.Regex.IsMatch(clean,
                    @"^[A-Za-z\s\.]+$")
                    && clean.Length >= 4
                    && clean.Length <= 60)
                {
                    var name = clean.Replace(".", " ").Trim().ToUpper();
                    name = System.Text.RegularExpressions.Regex.Replace(
                        name, @"\s+", " ").Trim();
                    return name;
                }
            }

            return "";
        }


        // Compare two strings with similarity score
        public bool IsMatch(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;

            a = a.Trim().ToUpper().Replace(" ", "");
            b = b.Trim().ToUpper().Replace(" ", "");

            // Exact match
            if (a == b) return true;

            // One contains the other
            if (a.Contains(b) || b.Contains(a)) return true;

            // Remove initials for comparison
            // "G MARUDHAJALAMOORTHY" → "MARUDHAJALAMOORTHY"
            string coreA = System.Text.RegularExpressions.Regex.Replace(
                a, @"(^[A-Z]\s+|\s+[A-Z]$)", "").Trim();
            string coreB = System.Text.RegularExpressions.Regex.Replace(
                b, @"(^[A-Z]\s+|\s+[A-Z]$)", "").Trim();

            // Core name exact match (ignoring initials)
            if (coreA == coreB) return true;
            if (coreA.Contains(coreB) || coreB.Contains(coreA)) return true;

            // Similarity on full name
            int similarityFull = CalculateSimilarity(a, b);
            if (similarityFull >= 0.75) return true;

            // Similarity on core name (without initials)
            int similarityCore = CalculateSimilarity(coreA, coreB);
            if (similarityCore >= 0.75) return true;

            // Missing character check (ANPR misses chars)
            // TN31B4114 vs TN31CB4114
            string longer = a.Length >= b.Length ? a : b;
            string shorter = a.Length < b.Length ? a : b;

            if (longer.Length - shorter.Length <= 2)
            {
                for (int i = 0; i <= longer.Length - shorter.Length; i++)
                {
                    var candidate = longer.Remove(i, longer.Length - shorter.Length);
                    if (candidate == shorter) return true;
                }
            }

            // Longest word match — if biggest word matches >= 0.60 similarity
            var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Where(w => w.Length > 3)
                          .OrderByDescending(w => w.Length)
                          .ToList();

            var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Where(w => w.Length > 3)
                          .OrderByDescending(w => w.Length)
                          .ToList();

            if (wordsA.Count > 0 && wordsB.Count > 0)
            {
                string longestA = wordsA.First();
                string longestB = wordsB.First();

                // Exact longest word match
                if (longestA == longestB) return true;

                // Longest word similarity >= 0.60
                int longestSimilarity = CalculateSimilarity(longestA, longestB);
                if (longestA.Length > 5 && longestSimilarity >= 60) return true;

                // Any word pair match with similarity >= 0.60
                foreach (var wa in wordsA)
                {
                    foreach (var wb in wordsB)
                    {
                        if (wa.Length > 4 && wb.Length > 4)
                        {
                            int pairSimilarity = CalculateSimilarity(wa, wb);
                            if (pairSimilarity >= 60) return true;
                        }
                    }
                }
            }

            return false;
        }



        // Levenshtein distance similarity
        private int CalculateSimilarity(string s1, string s2)
        {
            int maxLen = Math.Max(s1.Length, s2.Length);
            if (maxLen == 0) return 100;

            int distance = LevenshteinDistance(s1, s2);
            return (int)((1.0 - (double)distance / maxLen) * 100);
        }

        private int LevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            var d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + (s[i - 1] == t[j - 1] ? 0 : 1));

            return d[n, m];
        }

        // Calculate risk score
        public int CalculateRiskScore(bool plateMatch, bool nameMatch, bool hasExif, bool normalSize)
        {
            int score = 0;

            // Plate match — 50 points (mandatory)
            if (plateMatch) score += 50;

            // Name match — 50 points (mandatory)
            if (nameMatch) score += 50;

            return score;
        }
    }
}