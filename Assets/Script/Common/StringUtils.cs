using System.Text;
using System.Text.RegularExpressions;

namespace HHH.Common
{
    public static class StringUtils
    {
        public static string ToMeaningfulName(this string value)
        {
            return Regex.Replace(value, "(?!^)([A-Z])", " $1");
        }

        public static string ToSnakeCase(string text)
        {
            if (text.Length < 2)
            {
                return text;
            }

            var sb = new StringBuilder();
            sb.Append(char.ToLowerInvariant(text[0]));
            for (var i = 1; i < text.Length; ++i)
            {
                var c = text[i];
                if (c == '_' && (i + 1 >= text.Length || char.IsUpper(text[i + 1]))) continue;
                if (char.IsUpper(c))
                {
                    sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        public static string ToMeaningfulCase(string text)
        {
            if (text.Length < 2)
            {
                return text;
            }

            var sb = new StringBuilder();
            sb.Append(text[0]);
            for (var i = 1; i < text.Length; ++i)
            {
                var c = text[i];
                if (char.IsUpper(c)) sb.Append(' ');
                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}