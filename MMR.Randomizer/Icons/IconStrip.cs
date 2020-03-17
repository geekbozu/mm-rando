using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using System;
using MMR.Randomizer.Utils;
using MMR.Randomizer.Models;
using MMR.Randomizer.Asm;
using System.Linq;
using SixLabors.ImageSharp.Formats.Png;

namespace MMR.Randomizer.Icons
{
    public class IconStrip : IDisposable
    {
        /// <summary>
        /// Icon images.
        /// </summary>
        public Image<Rgba32>[] Icons { get; }

        /// <summary>
        /// Size of resulting <see cref="Image"/>.
        /// </summary>
        public Size Size { get; }

        /// <summary>
        /// Padding between icons on the X axis.
        /// </summary>
        public int Padding { get; }

        /// <summary>
        /// 
        /// </summary>
        Image<Rgba32> Result { get; set; }

        private IconStrip(Image<Rgba32>[] icons, int padding = 4)
        {
            this.Icons = icons;
            this.Padding = padding;
            this.Size = CalculateSize();
        }

        Size CalculateSize()
        {
            var width = 0;
            var height = 0;

            foreach (var icon in this.Icons)
            {
                width += icon.Width;
                height = Math.Max(height, icon.Height);
            }

            // Allocate extra width for padding between icons.
            width += (this.Icons.Length - 1) * this.Padding;

            return new Size(width, height);
        }

        public Image<Rgba32> Draw()
        {
            var width = this.Size.Width;
            var height = this.Size.Height;
            int x = 0;

            var image = new Image<Rgba32>(width, height);
            foreach (var icon in this.Icons)
            {
                var point = new Point(x, 0);
                image.Mutate(ctx => ctx.DrawImage(icon, point, PixelColorBlendingMode.Normal, 1.0f));
                x += (icon.Width + this.Padding);
            }

            return StoreResultingImage(image);
        }

        public void Save(string filename)
        {
            this.Draw().Save(filename, new PngEncoder());
        }

        Image<Rgba32> StoreResultingImage(Image<Rgba32> result)
        {
            if (this.Result != null)
                this.Result.Dispose();
            this.Result = result;
            return result;
        }

        public static IconStrip Load(HashData hash, Symbols symbols)
        {
            var index = RomUtils.AddrToFile(0xA36C10);
            RomUtils.CheckCompressed(index);
            var data = RomData.MMFileList[index].Data;
            var archive = new ArchiveTable(data);

            Console.WriteLine("HASH VALUE: 0x{0:X8}", hash.Value);

            var hashTable = symbols.ReadHashIconsTable();
            var hashIndexes = hash.GetIndexes();

            var icons = hashIndexes.Select(idx =>
            {
                idx = hashTable[idx];
                var raw = archive.GetDataDecompressed(data, idx);
                return Image.LoadPixelData<Rgba32>(raw, 32, 32);
            });

            return new IconStrip(icons.ToArray());
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    foreach (var icon in this.Icons)
                        icon.Dispose();
                    if (this.Result != null)
                        this.Result.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~IconStrip()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
