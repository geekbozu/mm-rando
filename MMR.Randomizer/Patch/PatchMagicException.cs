using System;

namespace MMR.Randomizer.Patch
{
    public class PatchMagicException : Exception
    {
        /// <summary>
        /// Magic value found in patch file.
        /// </summary>
        public uint Found { get; }

        public override string Message => String.Format("Bad patch magic: 0x{0:X8}", this.Found);

        public PatchMagicException(uint found)
        {
            this.Found = found;
        }
    }
}
