using System;
using System.IO;

namespace MMR.Randomizer.Utils
{
    public static class VersionUtils
    {
        /// <summary>
        /// Assembly <see cref="Version"/> of the current Randomizer.
        /// </summary>
        public static Version AssemblyVersion { get => typeof(Randomizer).Assembly.GetName().Version; }

        /// <summary>
        /// Read a <see cref="Version"/> from a <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader">Reader</param>
        /// <returns>Version</returns>
        public static Version Read(BinaryReader reader)
        {
            var major = ReadWriteUtils.ReadS32(reader);
            var minor = ReadWriteUtils.ReadS32(reader);
            var build = ReadWriteUtils.ReadS32(reader);
            var revision = ReadWriteUtils.ReadS32(reader);
            return new Version(major, minor, build, revision);
        }

        /// <summary>
        /// Write a <see cref="Version"/> to a <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="writer">Writer</param>
        /// <param name="version">Version</param>
        public static void Write(BinaryWriter writer, Version version)
        {
            ReadWriteUtils.WriteS32(writer, version.Major);
            ReadWriteUtils.WriteS32(writer, version.Minor);
            ReadWriteUtils.WriteS32(writer, version.Build);
            ReadWriteUtils.WriteS32(writer, version.Revision);
        }
    }
}
