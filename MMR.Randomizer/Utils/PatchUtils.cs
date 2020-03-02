using MMR.Randomizer.Patch;
using System;

namespace MMR.Randomizer.Utils
{
    public static class PatchUtils
    {
        public static Tuple<Version, Version> TryInferVersionRange(PatchFormat format)
        {
            if (format == PatchFormat.V1)
                return new Tuple<Version, Version>(new Version(1, 10, 0, 0), new Version(1, 10, 0, 2));
            else if (format == PatchFormat.V2)
                return new Tuple<Version, Version>(new Version(1, 10, 0, 3), new Version(1, 10, 0, 5));
            else if (format == PatchFormat.V3)
                return new Tuple<Version, Version>(new Version(1, 11, 0, 0), new Version(1, 11, 0, 2));
            else
                return null;
        }
    }
}
