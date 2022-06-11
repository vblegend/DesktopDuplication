using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

namespace BitMapTest
{
    public partial class Form1 : Form
    {
        Stopwatch sw = new Stopwatch();
        public Form1()
        {
            InitializeComponent();
            Form1_SizeChanged(null, null);
            comboBox1.SelectedIndex = 0;

        }
        void button1_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image != null)
                pictureBox1.Image.Dispose();
            pictureBox1.Image = getScreen();


        }

        Bitmap getScreen()
        {
            int iWidth = Screen.PrimaryScreen.Bounds.Width;
            //屏幕高
            int iHeight = Screen.PrimaryScreen.Bounds.Height;
            //按照屏幕宽高创建位图
            Bitmap img = new Bitmap(iWidth, iHeight);
            //从一个继承自Image类的对象中创建Graphics对象
            Graphics gc = Graphics.FromImage(img);
            //抓屏并拷贝到myimage里
            gc.CopyFromScreen(new Point(0, 0), new Point(0, 0), new Size(iWidth, iHeight));
            gc.Dispose();
            return img;
        }


        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            pictureBox1.Width = this.Width / 2;
            pictureBox2.Width = pictureBox1.Width;
            pictureBox1.Height = this.Height - 50;
            pictureBox2.Height = pictureBox1.Height;
            pictureBox2.Left = pictureBox1.Width;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null || pictureBox2.Image == null)
                return;
            sw.Restart();
            var Diff = new ImgDiff(pictureBox1.Image.Width, pictureBox1.Image.Height);
            Diff.diff((Bitmap)pictureBox1.Image);
            Diff.getChanges();
            Diff.diff((Bitmap)pictureBox2.Image);
            sw.Stop();
            var cgs = Diff.getChanges(false);
            label1.Text = "用时" + sw.Elapsed.TotalMilliseconds + "ms共" + cgs.Count + "处不同";
            if (comboBox1.SelectedIndex == 1)
            {
                pictureBox2.Image.Dispose();
                pictureBox2.Image = Diff.getDiffBitmap();

            }
            else if (comboBox1.SelectedIndex == 0)
            {
                var g = Graphics.FromImage(pictureBox2.Image);
                foreach (var p in cgs)
                {
                    var rc = Diff.getPixe(p);
                    g.DrawRectangle(Pens.Red, new Rectangle(p.X * Diff.TileSize, p.Y * Diff.TileSize, rc.Width, rc.Height));
                }
                g.Dispose();
                pictureBox2.Refresh();
            }
            else if (comboBox1.SelectedIndex == 2)
            {
                var bitmap = new Bitmap(pictureBox2.Width, pictureBox2.Height);
                var g = Graphics.FromImage(bitmap);
                var bitmmaps = Diff.getDiffBitmaps();
                var y = 0;
                var x = 0;
                foreach (var b in bitmmaps)
                {
                    g.DrawImage(b, new Point(x, y));
                    x += Diff.PixeSize;
                    if (x > pictureBox2.Width)
                    {
                        y += Diff.PixeSize;
                        x = 0;
                    }
                }
                g.Dispose();
                pictureBox2.Image = bitmap;
            }
            Diff.clearChanges();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (pictureBox2.Image != null)
                pictureBox2.Image.Dispose();
            pictureBox2.Image = getScreen();
        }

        private void pictureBox1_DoubleClick(object sender, EventArgs e)
        {
            var d = new OpenFileDialog();
            d.Filter = "*.jpg|*.jpg|*.png|*.png|*.bmp|*.bmp";
            var pict = (PictureBox)sender;
            if (d.ShowDialog() == DialogResult.OK)
            {
                pict.Image = Image.FromFile(d.FileName);
            }
            else
            {
                pict.Image = null;
            }
        }


    }
}
