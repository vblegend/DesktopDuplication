using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DesktopDuplication.Demo
{

    internal class BitBlockProfile
    {
        public Int32 Left { get; set; }
        public Int32 Top { get; set; }
        public Int32 Width { get; set; }
        public Int32 Height { get; set; }
        public Byte[] Data { get; set; }
        public Byte Bit { get; set; } = 4;
    }



    internal class ImageClipper : IDisposable
    {
        public Bitmap bitSource { get; private set; }
        public Int32 Width { get; private set; }
        public Int32 Height { get; private set; }
        public BitmapData lockedData { get; private set; }


        public ImageClipper(Bitmap bitSource)
        {
            this.bitSource = bitSource;
            this.Width = bitSource.Width;
            this.Height = bitSource.Height;

        }


        public static unsafe BitBlockProfile ClipImage(Bitmap source, Rectangle rect)
        {
            // source.PixelFormat
            Byte bit = 4;
            var left = Math.Max(0, rect.Left);
            var top = Math.Max(0, rect.Top);
            var width = Math.Min(source.Width, rect.Right) - left;
            var height = Math.Min(source.Height, rect.Bottom) - top;
            if (width <= 0 || height <= 0) return null;
            var result = new Byte[width * height * bit];
            var lockedData = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            Int32 offset = 0;
            Int32 rowLenght = width * bit;
            for (int i = 0; i < height; i++)
            {
                int bitsIndex = (left * bit) + ((top + i) * lockedData.Stride);
                Marshal.Copy(lockedData.Scan0 + bitsIndex, result, offset, rowLenght);
                offset += rowLenght;
            }
            source.UnlockBits(lockedData);
            return new BitBlockProfile()
            {
                Left = left,
                Top = top,
                Width = width,
                Height = height,
                Bit = bit,
                Data = result
            };

        }

        public BitBlockProfile Clip(Rectangle rect)
        {
            Byte bit = 4;
            var left = Math.Max(0, rect.Left);
            var top = Math.Max(0, rect.Top);
            var width = Math.Min(this.Width, rect.Right) - left;
            var height = Math.Min(this.Height, rect.Bottom) - top;
            if (width <= 0 || height <= 0) return null;
            var result = new Byte[width * height * bit];
            this.lockedData = this.bitSource.LockBits(new Rectangle(0, 0, this.Width, this.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            Int32 offset = 0;
            Int32 rowLenght = width * bit;
            for (int i = 0; i < height; i++)
            {
                int bitsIndex = (left * bit) + ((top + i) * this.lockedData.Stride);
                Marshal.Copy(this.lockedData.Scan0 + bitsIndex, result, offset, rowLenght);
                offset += rowLenght;
            }
            this.bitSource.UnlockBits(this.lockedData);
            return new BitBlockProfile()
            {
                Left = left,
                Top = top,
                Width = width,
                Height = height,
                Bit = bit,
                Data = result
            };

        }




        public Byte[] Clip2(Rectangle rect)
        {
            Byte bit = 4;
            var left = Math.Max(0, rect.Left);
            var top = Math.Max(0, rect.Top);
            var width = Math.Min(this.Width, rect.Right) - left;
            var height = Math.Min(this.Height, rect.Bottom) - top;
            if (width <= 0 || height <= 0) return null;
            Int32 rowLength = width * bit;
            var rowBytes = new Byte[rowLength];
            using (var steam = new MemoryStream())
            {
                using (var writer = new BinaryWriter(steam, Encoding.UTF8, true))
                {
                    writer.Write((Byte)254);
                    writer.Write(left);
                    writer.Write(top);
                    writer.Write(width);
                    writer.Write(height);
                    for (int i = 0; i < height; i++)
                    {
                        int bitsIndex = (left * bit) + ((top + i) * lockedData.Stride);
                        Marshal.Copy(this.lockedData.Scan0 + bitsIndex, rowBytes, 0, rowLength);
                        writer.Write(rowBytes);
                    }
                    return steam.ToArray();
                }
         
            }
        }



        private Boolean Paste(Bitmap destinaction, BitBlockProfile profile)
        {
            Byte bit = 4;
            if (profile.Left < 0 || profile.Top < 0) return false;
            if (profile.Left + profile.Width > destinaction.Width) return false;
            if (profile.Top + profile.Height > destinaction.Height) return false;
            var lockedData = destinaction.LockBits(new Rectangle(0, 0, destinaction.Width, destinaction.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Int32 offset = 0;
            Int32 rowLenght = profile.Width * bit;
            for (int i = 0; i < profile.Height; i++)
            {
                int origIndex = (profile.Left * bit) + ((profile.Top + i) * lockedData.Stride);
                Marshal.Copy(profile.Data, offset, lockedData.Scan0 + origIndex, rowLenght);
                offset += rowLenght;
            }
            destinaction.UnlockBits(lockedData);
            return true;
        }





        public void Dispose()
        {
            if (this.lockedData != null)
            {
                this.bitSource.UnlockBits(this.lockedData);
                this.lockedData = null;
            }
        }





    }
}
