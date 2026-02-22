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
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                // Look for name patterns
                if (line.Contains("Name") || line.Contains("NAME"))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1)
                        return parts[1].Trim().ToUpper();
                }
            }
            return "";
        }

        // Compare two strings with similarity score
        public bool IsMatch(string str1, string str2, int threshold = 80)
        {
            if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
                return false;

            str1 = str1.ToUpper().Trim();
            str2 = str2.ToUpper().Trim();

            if (str1 == str2) return true;

            // Calculate similarity percentage
            int similarity = CalculateSimilarity(str1, str2);
            return similarity >= threshold;
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
        public int CalculateRiskScore(bool plateMatch, bool nameMatch,
            bool hasExifData, bool imageSizeNormal)
        {
            int score = 0;
            if (plateMatch) score += 40;
            if (nameMatch) score += 30;
            if (hasExifData) score += 20;
            if (imageSizeNormal) score += 10;
            return score;
        }
    }
}