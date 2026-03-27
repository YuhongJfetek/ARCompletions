using System.Collections.Generic;
using System.Linq;

namespace ARCompletions.Services;

public static class PlatformPermissionHelper
{
    // Compute differences between existing and desired permission ids for a role
    public static (IEnumerable<string> ToAdd, IEnumerable<string> ToRemove) ComputeDiffs(IEnumerable<string> existing, IEnumerable<string> desired)
    {
        var ex = new HashSet<string>(existing ?? Enumerable.Empty<string>());
        var de = new HashSet<string>(desired ?? Enumerable.Empty<string>());

        var toAdd = de.Except(ex).ToList();
        var toRemove = ex.Except(de).ToList();

        return (toAdd, toRemove);
    }
}
