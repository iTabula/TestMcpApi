using System.Text.RegularExpressions;

namespace TestMcpApi.Helpers
{
    public static class Common
    {
        public static int CalculateLevenshteinDistance(string s, string t)
        {
            if(string.IsNullOrEmpty(s)) return 0;
            if (string.IsNullOrEmpty(t)) return 0;

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }
        public static string GetSoundex(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "0000";

            string output = name.ToUpper().Substring(0, 1);
            string previousCode = GetSoundexDigit(name[0]);

            for (int i = 1; i < name.Length && output.Length < 4; i++)
            {
                string currentCode = GetSoundexDigit(name[i]);
                // Only append if it's a new sound and not a vowel/silent letter
                if (currentCode != "" && currentCode != previousCode)
                {
                    output += currentCode;
                }
                previousCode = currentCode;
            }

            return output.PadRight(4, '0');
        }

        private static string GetSoundexDigit(char c)
        {
            return char.ToUpper(c) switch
            {
                'B' or 'F' or 'P' or 'V' => "1",
                'C' or 'G' or 'J' or 'K' or 'Q' or 'S' or 'X' or 'Z' => "2",
                'D' or 'T' => "3",
                'L' => "4",
                'M' or 'N' => "5",
                'R' => "6",
                _ => "" // Vowels and H, W, Y are ignored
            };
        }

        // Example Usage
        // Console.WriteLine(GetSoundex("Robert")); // Output: R163
        // Console.WriteLine(GetSoundex("Rupert")); // Output: R163 (Matches!)
        public static int CalculateSoundexDifference(string name1, string name2)
        {
            if (string.IsNullOrEmpty(name1) || string.IsNullOrEmpty(name2))
                return 0;

            // 1. Get Soundex codes for both names
            string s1 = GetSoundex(name1); // Use your existing Soundex method
            string s2 = GetSoundex(name2);

            // 2. If codes are identical, return max score
            if (s1 == s2) return 4;

            int result = 0;

            // 3. Compare the first character (always a letter)
            if (s1[0] == s2[0]) result++;

            // 4. Compare the numeric parts (characters 2, 3, and 4)
            // Common logic checks for existence of substrings or position matches
            string sub1 = s1.Substring(1, 3);
            if (s2.Contains(sub1)) return result + 3;

            string sub2 = s1.Substring(1, 2);
            if (s2.Contains(sub2)) result += 2;
            else if (s2.Contains(s1[1].ToString())) result += 1;

            return result;
        }
        public static string FormatPhoneNumber(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "Invalid Input";

            // 1. Remove all non-digit characters (keeps only 0-9)
            string cleanInput = Regex.Replace(input, @"\D", "");

            // 2. Ensure the resulting string is exactly 10 digits
            if (cleanInput.Length != 10)
            {
                throw new ArgumentException("Input must contain exactly 10 digits.");
            }

            // 3. Format to "### ### ####"
            return Regex.Replace(cleanInput, @"(\d{3})(\d{3})(\d{4})", "$1 $2 $3");
        }
    }
}
