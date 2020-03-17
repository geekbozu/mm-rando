namespace MMR.Randomizer.Icons
{
    public class HashData
    {
        public byte[] Data { get; }

        public uint Value => ((uint)Data[0] << 24) | ((uint)Data[1] << 16) | ((uint)Data[2] << 8) | Data[3];

        public HashData(byte[] data)
        {
            this.Data = data;
        }

        public byte[] GetIndexes()
        {
            var value = this.Value;
            var indexes = new byte[5];
            for (int i = 0; i < indexes.Length; i++)
            {
                indexes[i] = (byte)((value >> 26) & 0x3F);
                value <<= 6;
            }
            return indexes;
        }
    }
}
