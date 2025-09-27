using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public static class KeyUtil
{
    // Tạo subjectKey ổn định: không dấu, chữ thường, gạch dưới
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

        // thay space & ký tự lạ -> _
        s = Regex.Replace(s, @"[^a-z0-9]+", "_");
        s = Regex.Replace(s, "_{2,}", "_").Trim('_');
        if (string.IsNullOrEmpty(s)) s = "unknown";
        return s;
    }
}
