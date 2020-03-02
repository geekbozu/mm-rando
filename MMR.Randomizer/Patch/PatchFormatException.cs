using MMR.Randomizer.Utils;
using System;

namespace MMR.Randomizer.Patch
{
    public class PatchFormatException : Exception
    {
        /// <summary>
        /// Current <see cref="PatchFormat"/> value.
        /// </summary>
        public PatchFormat Current { get; }

        /// <summary>
        /// <see cref="PatchFormat"/> value found in file.
        /// </summary>
        public PatchFormat Found { get; }

        /// <summary>
        /// Least recent acceptable <see cref="PatchFormat"/> value.
        /// </summary>
        public PatchFormat Previous { get; }

        public override string Message => String.Format("Incompatible patch versions: expected version in range [{0}, {1}], but found version {2}",
            (uint)this.Previous, (uint)this.Current, (uint)this.Found);

        public PatchFormatException(PatchFormat current, PatchFormat previous, PatchFormat found)
        {
            this.Current = current;
            this.Found = found;
            this.Previous = previous;
        }

        /// <summary>
        /// Get a <see cref="PatchFormatException"/> from a <see cref="PatchFormat"/> value found in a patch file.
        /// </summary>
        /// <param name="found">Value</param>
        /// <returns>PatchVersionException</returns>
        public static PatchFormatException From(PatchFormat found)
        {
            return new PatchFormatException(PatchHeader.PATCH_FORMAT, PatchHeader.PREVIOUS_FORMAT, found);
        }
    }
}
