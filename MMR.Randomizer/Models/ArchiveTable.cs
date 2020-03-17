using MMR.Randomizer.Utils;
using MMR.Randomizer.Utils.Mzxrules;
using System;
using System.IO;

namespace MMR.Randomizer.Models
{
    /// <summary>
    /// 
    /// </summary>
    public class ArchiveTable
    {
        public uint Length { get; private set; }
        public uint[] Offsets { get; private set; }

        public ArchiveTable(byte[] file)
        {
            this.Length = (uint)file.Length;
            this.Offsets = ReadTable(file);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index">Archive entry index</param>
        /// <returns>Bounds</returns>
        public Tuple<uint, uint> GetBounds(uint index)
        {
            var initial = this.Offsets[0];
            if (index == 0)
                return new Tuple<uint, uint>(initial, this.Offsets[1] + initial);
            else if (index < (this.Offsets.Length - 1))
                return new Tuple<uint, uint>(this.Offsets[index] + initial, this.Offsets[index + 1] + initial);
            else
                return new Tuple<uint, uint>(this.Offsets[index] + initial, this.Length);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data">Underlying data</param>
        /// <param name="index">Archive entry index</param>
        /// <returns>Archived data</returns>
        public byte[] GetData(byte[] data, uint index)
        {
            var bounds = this.GetBounds(index);
            var length = bounds.Item2 - bounds.Item1;
            var result = new byte[length];
            Array.Copy(data, bounds.Item1, result, 0, result.Length);
            return result;
        }

        public byte[] GetDataDecompressed(byte[] data, uint index)
        {
            var compressed = this.GetData(data, index);
            using (var memoryStream = new MemoryStream(compressed))
            {
                return Yaz.Decode(memoryStream, compressed.Length);
            }
        }

        uint[] ReadTable(byte[] data)
        {
            using (var memoryStream = new MemoryStream(data))
            using (var reader = new BinaryReader(memoryStream))
            {
                // Read initial offset to determine size of offset table
                var initial = ReadWriteUtils.Byteswap32(reader.ReadUInt32());
                if (initial % 4 != 0)
                    throw new Exception("");

                // Create offset table array
                var count = (initial / 4);
                var offsets = new uint[count];
                offsets[0] = initial;

                // Read remainder of offset table
                for (uint i = 1; i < offsets.Length; i++)
                {
                    offsets[i] = ReadWriteUtils.Byteswap32(reader.ReadUInt32());
                }

                return offsets;
            }
        }
    }
}
