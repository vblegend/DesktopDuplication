using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopDuplication.Demo
{

    public class FrameUpdatedRegion
    {
        public Rectangle Rectangle;
        public long TickCount;
    }



    public partial class FormDemo : Form
    {

        private Queue<FrameUpdatedRegion> UpdatedRegions = new Queue<FrameUpdatedRegion>();


        private DesktopDuplicator desktopDuplicator;
        private Bitmap screen;
        private DesktopFrame frame = null;
        private Int32 frameNum = 0;
        private CursorInfo cursorInfo;
        private Pen redLine = new Pen(Color.Red, 1);


        public FormDemo()
        {
            InitializeComponent();


        }


        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawImage(this.screen, 0, 0);
            foreach (var item in UpdatedRegions)
            {
                e.Graphics.DrawRectangle(redLine, item.Rectangle);
            }
            this.DrawCursor(e.Graphics);
        }



        private void FormDemo_Load(object sender, EventArgs e)
        {
            this.screen = new Bitmap(1920, 1080);
            this.desktopDuplicator = new DesktopDuplicator(0);
            this.cursorInfo = new CursorInfo();
            _ = Task.Factory.StartNew(() => CaptureScreenEvery(), TaskCreationOptions.LongRunning);
        }


        private async Task CaptureScreenEvery()
        {


            while (!this.IsDisposed)
            {
                TakeScreenshot();
                await Task.Delay(10);
            }
        }




        private void TakeScreenshot()
        {


            Application.DoEvents();



            frameNum++;
            try
            {
                frame = desktopDuplicator.GetLatestFrame();
            }
            catch
            {
                desktopDuplicator = new DesktopDuplicator(0);
                return;
            }

            if (frame != null && frame.DesktopImage != null)
            {

                var sw = Stopwatch.StartNew();
                //var clipper = new ImageClipper(frame.DesktopImage);
                //using ()
                {
                    var r1 = ImageClipper.ClipImage(frame.DesktopImage, new Rectangle(100, 100, 120, 120));
                    //var r2 = clipper.Clip(new Rectangle(100, 100, 100, 100));
                    //var r3 = clipper.Clip(new Rectangle(356, 687, 150, 200));
                }
                //clipper.Dispose();
                sw.Stop();

                Console.WriteLine(sw.ElapsedMilliseconds + "ms");
            }








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
            if (frame != null)
            {
                cursorInfo.Size = frame.CursorSize;
                if (!frame.CursorLocation.IsEmpty)
                {
                    cursorInfo.Location = frame.CursorLocation;
                }
                using (var g = Graphics.FromImage(screen))
                {
                    if (frame.DesktopImage != null)
                    {

                        var sw = Stopwatch.StartNew();
                        //var clipper = new ImageClipper(frame.DesktopImage);
                        //using ()
                        {
                            var r1 = ImageClipper.ClipImage(frame.DesktopImage, new Rectangle(0, 0, 120, 120));
                            //var r2 = clipper.Clip(new Rectangle(100, 100, 100, 100));
                            //var r3 = clipper.Clip(new Rectangle(356, 687, 150, 200));
                        }
                        //clipper.Dispose();
                        sw.Stop();

                        Console.WriteLine(sw.ElapsedMilliseconds + "ms");



                        foreach (var moved in frame.MovedRegions)
                        {
                            g.DrawImage(frame.DesktopImage, moved.Source.X, moved.Source.Y, moved.Destination, GraphicsUnit.Pixel);
                            UpdatedRegions.Enqueue(new FrameUpdatedRegion()
                            {
                                Rectangle = moved.Destination,
                                TickCount = Environment.TickCount
                            });
                        }
                        foreach (var updated in frame.UpdatedRegions)
                        {
                            g.DrawImage(frame.DesktopImage, updated.Location.X, updated.Location.Y, updated, GraphicsUnit.Pixel);
                            UpdatedRegions.Enqueue(new FrameUpdatedRegion()
                            {
                                Rectangle = updated,
                                TickCount = Environment.TickCount
                            });

                        }
                    }
                }

              

                this.Invoke(new Action(() =>
                {
                    this.Refresh();
                }));
            //   
            }

        }




        private void DrawCursor(Graphics graphics)
        {
            if (cursorInfo != null && !cursorInfo.Location.IsEmpty)
            {
                var cursor = NataveAPI.GetCursor();
                if (cursor != null)
                {
                    cursor.DrawStretched(graphics, new Rectangle(cursorInfo.Location, cursorInfo.Size));
                    cursor.Dispose();
                }
            }
        }



        private void timer1_Tick(object sender, EventArgs e)
        {
            this.Text = "FPS:" + frameNum.ToString();

            frameNum = 0;
            //this.screen.Save(@"C:\Users\liu.yandong.hanks\Desktop\sp.png", ImageFormat.Png);
        }

    }
}
