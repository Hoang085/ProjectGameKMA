using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public static class KeyUtil
{
    // Tạo subjectKey ổn định: không dấu, viết thường, viết liền
    public static string MakeKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unknown";
        string s = name.Trim().ToLowerInvariant();

        // bỏ dấu tiếng Việt
        s = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        s = sb.ToString().Normalize(NormalizationForm.FormC);

        // bỏ hết ký tự không phải a-z0-9 (viết liền)
        s = Regex.Replace(s, @"[^a-z0-9]+", "");

        if (string.IsNullOrEmpty(s)) s = "unknown";
        return s;
    }
}
