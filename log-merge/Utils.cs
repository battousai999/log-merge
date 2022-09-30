using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace log_merge
{
    public static class Utils
    {
        private static readonly Regex specifiesOffsetRegex = new Regex(@"[-+]\d{1,2}(:\d{2})$");

        public static IEnumerable<T> ToSingleton<T>(this T item)
        {
            yield return item;
        }

        public static string WithIndent(this string str, int indent)
        {
            return $"{new String(' ', indent)}{str}";
        }

        public static bool ContainsWildcards(this string text)
        {
            return (text.Contains("*") || text.Contains("?"));
        }

        public static string[] WriteSafeReadAllLines(String path)
        {
            using (var csv = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(csv))
            {
                List<string> file = new List<string>();
                while (!sr.EndOfStream)
                {
                    file.Add(sr.ReadLine());
                }

                return file.ToArray();
            }
        }

        public static (bool, string, DateTimeOffset?) ParseDateTimeOffset(string dateStr, bool parseAsUtc)
        {
            if (String.IsNullOrWhiteSpace(dateStr))
                return (false, dateStr, null);

            if (DateTimeOffset.TryParse(dateStr, out DateTimeOffset date))
            {
                var specifiesOffset = specifiesOffsetRegex.IsMatch(dateStr);
                var isUtc = date.Offset == TimeSpan.Zero;
                var annotatedDateStr = dateStr + " +00:00";

                if (!specifiesOffset && parseAsUtc && !isUtc)
                {
                    if (!DateTimeOffset.TryParse(annotatedDateStr, out date))
                        return (false, annotatedDateStr, null);
                }

                return (true, dateStr, date);
            }
            else
                return (false, dateStr, null);
        }
    }
}
