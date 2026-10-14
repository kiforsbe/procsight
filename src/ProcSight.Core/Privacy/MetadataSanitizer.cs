using System.Text.RegularExpressions;
namespace ProcSight.Core.Privacy;
public static class MetadataSanitizer
{
    public static string Normalize(string value)
    {
        var normalized=Regex.Replace(value,@"(?i)[a-z]:\\Users\\[^\\\s]+", "<UserProfile>",RegexOptions.None,TimeSpan.FromMilliseconds(100));
        normalized=Regex.Replace(normalized,@"(?i)\\\\[^\\\s]+\\", "<NetworkRoot>\\",RegexOptions.None,TimeSpan.FromMilliseconds(100));
        normalized=Regex.Replace(normalized,@"(?i)(?:Bearer\s+|(?:token|password|secret|api[_-]?key)[=:]\s*)[^\s]+","<RedactedSecret>",RegexOptions.None,TimeSpan.FromMilliseconds(100));
        return normalized.Length > 1024 ? normalized[..1024] : normalized;
    }
}
