using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace SSMSTabManager
{
    public static class TabColor
    {
        private static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>
        {
            ["cervena"] = Color.FromRgb(0xF2, 0x8B, 0x82),
            ["zelena"] = Color.FromRgb(0x81, 0xC9, 0x95),
            ["modra"] = Color.FromRgb(0x8A, 0xB4, 0xF8),
            ["zluta"] = Color.FromRgb(0xFD, 0xD6, 0x63),
            ["fialova"] = Color.FromRgb(0xC5, 0x8A, 0xF9),
        };

        public static Color? FromHeader(string firstLine, string secondLine)
        {
            if (string.IsNullOrEmpty(QueryFileName.GetCommentTitle(firstLine)))
            {
                return null;
            }

            var line = (secondLine ?? string.Empty).Trim();
            return line.StartsWith("--") ? FromName(line.TrimStart('-').Trim()) : null;
        }

        public static bool IsColorComment(string line)
        {
            var trimmed = (line ?? string.Empty).Trim();
            return trimmed.StartsWith("--") && FromName(trimmed.TrimStart('-').Trim()) != null;
        }

        public static Color? FromName(string name)
        {
            var key = RemoveDiacritics(name ?? string.Empty).ToLowerInvariant();
            return Colors.TryGetValue(key, out var color) ? color : (Color?)null;
        }

        private static string RemoveDiacritics(string text)
        {
            var chars = text.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray();
            return new string(chars).Normalize(NormalizationForm.FormC);
        }
    }
}
