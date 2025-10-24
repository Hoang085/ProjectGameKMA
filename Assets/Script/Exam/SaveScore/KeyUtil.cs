using System.Globalization;
using System.Text;

public static class KeyUtil
{
    public static string MakeKey(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";

        // hạ chữ + chuẩn hóa khoảng trắng/underscore
        s = s.Trim().ToLowerInvariant()
             .Replace('_', ' ')
             .Replace("đ", "d")   // 👈 map đ
             .Replace("Đ", "d");  // 👈 map Đ (phòng trường hợp còn sót)

        // bỏ dấu tiếng Việt
        var nf = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(nf.Length);
        foreach (var ch in nf)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue; // bỏ dấu kết hợp

            // chỉ giữ a-z 0-9
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                sb.Append(ch);
            else if (ch == ' ') { /* bỏ luôn khoảng trắng */ }
        }
        return sb.ToString();
    }
}

