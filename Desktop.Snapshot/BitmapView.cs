using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Desktop.Snapshot
{
    public class BitBlockProfile
    {
        public Int32 Left { get; set; }
        public Int32 Top { get; set; }
        public Int32 Width { get; set; }
        public Int32 Height { get; set; }
        public Byte[] Data { get; set; }
        public Byte Bit { get; set; } = 4;
    }



    public class BitmapView : IDisposable
    {
        public Bitmap bitSource { get; private set; }
        public Int32 Width { get; private set; }
        public Int32 Height { get; private set; }
        public BitmapData lockedData { get; private set; }


        public BitmapView(Bitmap bitSource)
        {
            this.bitSource = bitSource;
            this.Width = bitSource.Width;
            this.Height = bitSource.Height;
            this.lockedData = this.bitSource.LockBits(new Rectangle(0, 0, bitSource.Width, bitSource.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        }


        public unsafe BitBlockProfile ClipImage(Rectangle rect)
        {
            Byte bit = 4;
            var left = Math.Max(0, rect.Left);
            var top = Math.Max(0, rect.Top);
            var width = Math.Min(this.Width, rect.Right) - left;
            var height = Math.Min(this.Height, rect.Bottom) - top;
            if (width <= 0 || height <= 0) return null;
            var result = new Byte[width * height * bit];

            Int32 offset = 0;
            Int32 rowLenght = width * bit;
            for (int i = 0; i < height; i++)
            {
                int bitsIndex = (left * bit) + ((top + i) * lockedData.Stride);
                Marshal.Copy(lockedData.Scan0 + bitsIndex, result, offset, rowLenght);
                offset += rowLenght;
            }
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

        public Byte[] ClipImageStream(Rectangle rect)
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

        public Boolean PasteImageFromStream(Stream profile)
        {
            Byte bit = 4;
            using (var reader = new BinaryReader(profile))
            {
                if(reader.ReadByte() != 254) throw new Exception("错误的文件格式。");
                var Left = reader.ReadInt32();
                var Top = reader.ReadInt32();
                var Width = reader.ReadInt32();
                var Height = reader.ReadInt32();
                Int32 rowLenght = Width * bit;
                for (int i = 0; i < Height; i++)
                {
                    Byte [] data = reader.ReadBytes(rowLenght);
                    int origIndex = (Left * bit) + ((Top + i) * lockedData.Stride);
                    Marshal.Copy(data, 0, lockedData.Scan0 + origIndex, rowLenght);
                }
                return true;
            }
        }



        public Boolean PasteImage(Bitmap destinaction, BitBlockProfile profile)
        {
            Byte bit = 4;
            if (profile.Left < 0 || profile.Top < 0) return false;
            if (profile.Left + profile.Width > destinaction.Width) return false;
            if (profile.Top + profile.Height > destinaction.Height) return false;
            Int32 offset = 0;
            Int32 rowLenght = profile.Width * bit;
            for (int i = 0; i < profile.Height; i++)
            {
                int origIndex = (profile.Left * bit) + ((profile.Top + i) * lockedData.Stride);
                Marshal.Copy(profile.Data, offset, lockedData.Scan0 + origIndex, rowLenght);
                offset += rowLenght;
            }
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
