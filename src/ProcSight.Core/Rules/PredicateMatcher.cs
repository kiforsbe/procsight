using ProcSight.Core.Models;
using ProcSight.Core.Privacy;
namespace ProcSight.Core.Rules;
public static class PredicateMatcher
{
    public static bool Matches(Predicate predicate,string observed)
    {
        observed=MetadataSanitizer.Normalize(observed);
        var value=MetadataSanitizer.Normalize(predicate.Value);
        return predicate.Kind==PredicateKind.Exact ? string.Equals(observed,value,StringComparison.OrdinalIgnoreCase) :
            observed.Equals(value,StringComparison.OrdinalIgnoreCase) || observed.StartsWith(value.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);
    }
}
