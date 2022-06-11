using Desktop.Snapshot;
using IronSnappy;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Desktop.Demo
{
    public class FrameUpdatedRegion
    {
        public Rectangle Rectangle;
        public long TickCount;
    }

    public partial class Form1 : Form
    {
        private DesktopMirror Mirror;
        private Queue<FrameUpdatedRegion> UpdatedRegions = new Queue<FrameUpdatedRegion>();
        private Bitmap screen;
        private Pen redLine = new Pen(Color.Red, 1);
        private CursorInfo cursor = new CursorInfo();
        private Bitmap cursorIcon;
        private Int32 UpdateStream;
        private Int32 Fps;

        public Form1()
        {
            InitializeComponent();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            this.Text = $"Fps:{Fps} ,Up Stream:{UpdateStream / 1024}Kb";

            UpdateStream = 0;
            Fps = 0;
            //this.screen.Save(@"C:\Users\liu.yandong.hanks\Desktop\sp.png", ImageFormat.Png);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //test();
            Mirror = new DesktopMirror(0, 0);
            Mirror.FrameEvent += Mirror_FrameEvent;
            Mirror.CursorEvent += Mirror_CursorEvent;
            this.screen = new Bitmap(Mirror.ClientWidth, Mirror.ClientHeight);

            Mirror.Start();
        }

        private unsafe void test()
        {
            IntPtr hglobal = Marshal.AllocHGlobal(Mirror.ClientWidth * Mirror.ClientHeight * 4);
            Int32 index = 1;
            var bitmap = new Bitmap(1920, 1080);
            var lockdata = bitmap.LockBits(new Rectangle(0, 0, Mirror.ClientWidth, Mirror.ClientHeight), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var sw = Stopwatch.StartNew();
            Int32* argb = (Int32*)lockdata.Scan0;
            Int32* argb2 = (Int32*)hglobal;
            for (int x = 0; x < Mirror.ClientWidth; x++)
            {
                for (int y = 0; y < Mirror.ClientHeight; y++)
                {
                    if (*argb != *argb2)
                    {
                        index++;
                    }
                    argb2++;
                    argb++;
                }
            }
            argb--;
            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);

            Console.WriteLine(*argb);
            bitmap.UnlockBits(lockdata);

            bitmap.Dispose();

            Marshal.FreeHGlobal(hglobal);
        }



        /// <summary>
        /// 壓縮圖片 /// </summary>
        /// <param name="fileStream">圖片流</param>
        /// <param name="quality">壓縮質量0-100之間 數值越大質量越高</param>
        /// <returns></returns>
        private byte[] CompressionImage(Stream fileStream, long quality)
        {
            using (System.Drawing.Image img = System.Drawing.Image.FromStream(fileStream))
            {
                using (Bitmap bitmap = new Bitmap(img))
                {
                    ImageCodecInfo CodecInfo = GetEncoder(img.RawFormat);
                    System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
                    EncoderParameters myEncoderParameters = new EncoderParameters(1);
                    EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, quality);
                    myEncoderParameters.Param[0] = myEncoderParameter;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bitmap.Save(ms, CodecInfo, myEncoderParameters);
                        myEncoderParameters.Dispose();
                        myEncoderParameter.Dispose();
                        return ms.ToArray();
                    }
                }
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                { return codec; }
            }
            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var sx = this.ClientSize.Width / (Mirror.ClientWidth + 0.0F);
            var sy = this.ClientSize.Height / (Mirror.ClientHeight + 0.0F);

            lock (this.screen)
            {
                e.Graphics.DrawImage(this.screen, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
            }


            foreach (var item in UpdatedRegions)
            {
                e.Graphics.DrawRectangle(redLine, item.Rectangle.Left * sx, item.Rectangle.Top * sy, item.Rectangle.Width * sx, item.Rectangle.Height * sy);
            }
            if (cursorIcon != null)
            {
                e.Graphics.DrawImage(this.cursorIcon, new RectangleF(this.cursor.Location.X * sx, this.cursor.Location.Y * sy, this.cursor.Size.Width * sx, this.cursor.Size.Height * sy), new RectangleF(0, 0, this.cursor.Size.Width, this.cursor.Size.Height), GraphicsUnit.Pixel);
            }


        }


        private void Mirror_CursorEvent(CursorInfo cursor)
        {
            if ((cursor.Type & CursorChangeType.Shape) == CursorChangeType.Shape)
            {
                if (this.cursorIcon != null) this.cursorIcon.Dispose();
                if (cursor.Icon != null)
                {
                    this.cursorIcon = cursor.Icon.Clone() as Bitmap;
                    this.cursor.Size = cursor.Size;
                }
            }
            if ((cursor.Type & CursorChangeType.Position) == CursorChangeType.Position && !cursor.Location.IsEmpty)
            {
                this.cursor.Location = cursor.Location;
            }

            if (!this.IsDisposed && !this.Disposing)
            {
                this.Invoke(new Action(this.Refresh));
            }
        }




        private void Mirror_FrameEvent(SnapshotFrameInfo frame)
        {
            Fps++;
            while (UpdatedRegions.Count > 0)
            {
                var element = UpdatedRegions.Peek();
                if (Environment.TickCount - element.TickCount > 200)
                {
                    UpdatedRegions.Dequeue();
                }
                else
                {
                    break;
                }
            }


            //using (var view = new BitmapView(frame.Image))
            //{
            //    foreach (var updated in frame.UpdatedRegions)
            //    {
            //        var result = view.ClipImage(updated);
            //        if (result == null) continue;
            //        byte[] compressed = Snappy.Encode(result.Data);
            //        UpdateStream += compressed.Length;
            //    }
            //}




            lock (this.screen)
            {
                using (var g = Graphics.FromImage(screen))
                {
                    foreach (var moved in frame.MovedRegions)
                    {
                        g.DrawImage(frame.Image, moved.Source.X, moved.Source.Y, moved.Destination, GraphicsUnit.Pixel);
                        UpdatedRegions.Enqueue(new FrameUpdatedRegion()
                        {
                            Rectangle = moved.Destination,
                            TickCount = Environment.TickCount
                        });
                    }
                    foreach (var updated in frame.UpdatedRegions)
                    {
                        var bitSize = updated.Width * updated.Height * 4;
                        //UpdateStream += bitSize;
                        g.DrawImage(frame.Image, updated.Location.X, updated.Location.Y, updated, GraphicsUnit.Pixel);
                        UpdatedRegions.Enqueue(new FrameUpdatedRegion()
                        {
                            Rectangle = updated,
                            TickCount = Environment.TickCount
                        });
                    }
                }
            }
            if (!this.IsDisposed && !this.Disposing)
            {
                this.Invoke(new Action(this.Refresh));
            }

        }

        public override void Refresh()
        {
            if (this.IsDisposed || this.Disposing) return;
            base.Refresh();
        }



        private void Form1_Shown(object sender, EventArgs e)
        {

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Mirror.FrameEvent -= Mirror_FrameEvent;
            Mirror.CursorEvent -= Mirror_CursorEvent;
            Mirror.Stop();
        }
    }
}
