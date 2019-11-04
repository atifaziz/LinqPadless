using System.Collections.Generic;
using System.IO;

static class Extensions
{
    public static IEnumerable<DirectoryInfo> AncestorsAndSelf(this DirectoryInfo dir)
    {
        if (dir == null) throw new System.ArgumentNullException(nameof(dir));
        return _(); IEnumerable<DirectoryInfo> _()
        {
            for (; dir != null; dir = dir.Parent)
                yield return dir;
        }
    }
}
