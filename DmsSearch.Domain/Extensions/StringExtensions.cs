using System.Text;

namespace DmsSearch.Domain.Extensions;

public static class StringExtensions
{
    public static string NormalizeTurkish(string input)
    {
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                                                != System.Globalization.UnicodeCategory.NonSpacingMark))
        {
            sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}