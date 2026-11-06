using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SSMSTabManager
{
    public static class QueryFileName
    {
        private const int MaxNameLength = 100;
        private const string Extension = ".sql";

        public static string FromFirstLine(string firstLine, DateTime now)
        {
            var title = GetCommentTitle(firstLine);
            return string.IsNullOrEmpty(title) ? now.ToString("yyyy-MM-dd_HHmmss") : title;
        }

        public static string GetUniquePath(string directory, string name, DateTime now)
        {
            var path = Path.Combine(directory, name + Extension);
            if (!File.Exists(path))
            {
                return path;
            }

            var withTime = name + "_" + now.ToString("HHmmss");
            path = Path.Combine(directory, withTime + Extension);
            for (var i = 2; File.Exists(path); i++)
            {
                path = Path.Combine(directory, withTime + "_" + i + Extension);
            }
            return path;
        }

        public static string GetCommentTitle(string firstLine)
        {
            var line = (firstLine ?? string.Empty).Trim();
            if (!line.StartsWith("--"))
            {
                return null;
            }

            var title = Sanitize(line.TrimStart('-').Trim());
            return title.Length > MaxNameLength ? title.Substring(0, MaxNameLength).TrimEnd(' ', '.') : title;
        }

        private static string Sanitize(string text)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var replaced = new string(text.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return Regex.Replace(replaced, @"\s+", " ").Trim(' ', '.');
        }
    }
}
