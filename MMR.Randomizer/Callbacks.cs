using MMR.Randomizer.Patch;
using System;

namespace MMR.Randomizer
{
    /// <summary>
    /// Callbacks which may be used when performing randomization.
    /// </summary>
    public class Callbacks
    {
        /// <summary>
        /// Callback function used to prompt the user with a warning if attempting to apply a patch from a prior randomizer version.
        /// </summary>
        public Func<PatchHeader, bool> PatchVersionWarning { get; set; }
    }
}
