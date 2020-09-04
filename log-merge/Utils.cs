using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace log_merge
{
    public static class Utils
    {
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
    }
}
