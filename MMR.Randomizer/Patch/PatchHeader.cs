using MMR.Randomizer.Utils;
using System;
using System.IO;

namespace MMR.Randomizer.Patch
{
    public class PatchHeader
    {
        /// <summary>
        /// Patch file magic number ("MMRP").
        /// </summary>
        public static readonly uint PATCH_MAGIC = 0x4D4D5250;

        /// <summary>
        /// Most recent <see cref="PatchFormat"/> value.
        /// </summary>
        public static readonly PatchFormat PATCH_FORMAT = PatchFormat.V4;

        /// <summary>
        /// Least recent <see cref="PatchFormat"/> which can be applied.
        /// </summary>
        public static readonly PatchFormat PREVIOUS_FORMAT = PatchFormat.V2;

        /// <summary>
        /// Patch file magic number.
        /// </summary>
        public uint Magic { get; }

        /// <summary>
        /// Patch format version.
        /// </summary>
        public PatchFormat Format { get; }

        /// <summary>
        /// Assembly <see cref="System.Version"/> of software which generated the patch.
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Whether or not the given <see cref="PatchFormat"/> encodes the assembly <see cref="System.Version"/>.
        /// </summary>
        /// <param name="format">Patch format</param>
        /// <returns>True if the <see cref="System.Version"/> is encoded, false if not</returns>
        public static bool HasVersionField(PatchFormat format) => PatchFormat.V4 <= format;

        /// <summary>
        /// Range of acceptable <see cref="PatchFormat"/> versions which can be applied.
        /// </summary>
        public static Tuple<PatchFormat, PatchFormat> AcceptableFormatRange =>
            new Tuple<PatchFormat, PatchFormat>(PatchFormat.V2, PATCH_FORMAT);

        public PatchHeader()
            : this(PatchHeader.PATCH_MAGIC, PatchHeader.PATCH_FORMAT, VersionUtils.AssemblyVersion)
        {
        }

        public PatchHeader(uint magic, PatchFormat format, Version version = null)
        {
            this.Magic = magic;
            this.Format = format;
            this.Version = version;
        }

        /// <summary>
        /// Whether or not this patch is from a previous version.
        /// </summary>
        public bool IsPreviousVersion =>
            this.Format < PatchHeader.PATCH_FORMAT || this.Version < VersionUtils.AssemblyVersion;

        /// <summary>
        /// Whether or not a patch file with this <see cref="PatchFormat"/> can be applied.
        /// </summary>
        public bool CanApply {
            get {
                var acceptable = AcceptableFormatRange;
                var inRange = (acceptable.Item1 <= this.Format) && (this.Format <= acceptable.Item2);
                return inRange;
            }
        }

        /// <summary>
        /// Get the <see cref="System.Version"/>, or infer it for patches with older <see cref="PatchFormat"/> values.
        /// </summary>
        public Version InferredVersion
        {
            get {
                // Attempt to infer version from patch format (for older patch files)
                var inferred = PatchUtils.TryInferVersionRange(this.Format);
                if (inferred != null)
                    return inferred.Item2;
                // Return parsed version
                else if (this.Version != null)
                    return this.Version;
                else
                    throw new Exception("Unable to infer patch format version");
            }
        }

        /// <summary>
        /// Read a <see cref="PatchHeader"/> from a <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader">Reader</param>
        /// <returns>PatchHeader</returns>
        public static PatchHeader Read(BinaryReader reader)
        {
            var magic = ReadWriteUtils.ReadU32(reader);
            var format = (PatchFormat)ReadWriteUtils.ReadU32(reader);

            if (HasVersionField(format))
            {
                var version = VersionUtils.Read(reader);
                return new PatchHeader(magic, format, version);
            }
            else
            {
                return new PatchHeader(magic, format);
            }
        }

        /// <summary>
        /// Validate values in the <see cref="PatchHeader"/>.
        /// </summary>
        public void Validate()
        {
            // Make sure this is a patch file by checking the magic value
            ValidateMagic();

            // Check that this patch format is supported
            ValidateFormat();
        }

        /// <summary>
        /// Validate magic value and throw a <see cref="PatchMagicException"/> if invalid.
        /// </summary>
        public void ValidateMagic()
        {
            if (this.Magic != PatchHeader.PATCH_MAGIC)
            {
                throw new PatchMagicException(this.Magic);
            }
        }

        /// <summary>
        /// Validate patch <see cref="PatchFormat"/> value and throw a <see cref="PatchFormatException"/> if invalid.
        /// </summary>
        public void ValidateFormat()
        {
            if (!this.CanApply)
            {
                throw PatchFormatException.From(this.Format);
            }
        }

        /// <summary>
        /// Write a <see cref="PatchHeader"/>
        /// </summary>
        /// <param name="writer"></param>
        public void Write(BinaryWriter writer)
        {
            ReadWriteUtils.WriteU32(writer, this.Magic);
            ReadWriteUtils.WriteU32(writer, (uint)this.Format);
            VersionUtils.Write(writer, this.Version);
        }
    }
}
