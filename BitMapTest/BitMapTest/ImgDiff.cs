using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BitMapTest
{
    public class NetPixe
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
        public NetPixe(int w, int h, int of, int pixeSize)
        {
            Width = w;
            Height = h;
            Offset = of;
            Length = w * h * pixeSize;
        }
    }
    public class ImgDiff
    {
        byte[] _pixelBuffer;
        PixelFormat _format = PixelFormat.Format24bppRgb;
        NetPixe[,] _pixes; // [x,y][块]
        List<Point> Changes = new List<Point>();
        object locobj = new object();
        int tileSize = 32, pixeSize = 3;
        int _w, _h;
        int _pw, _ph;
        int _bufstride;
        int _imgstride;
        int _imgstrideScale;
        public int TileSize { get => tileSize; set => tileSize = value; }
        public PixelFormat PixelFormat
        {
            get => _format;
            set
            {
                _format = value;
            }
        }
        public int Width { get => _w; }
        public int Height { get => _h; }
        public int PixeSize { get => pixeSize; }
        public int Bufstride { get => _bufstride; }
        public Bitmap GetBitmap()
        {
            unsafe
            {
                fixed (byte* p = _pixelBuffer)
                {
                    return new Bitmap(_w, _h, _bufstride, PixelFormat, (IntPtr)p);
                }
            }
        }
        public List<Bitmap> getDiffBitmaps()
        {
            var list = new List<Bitmap>();
            unsafe
            {
                fixed (byte* bufp = _pixelBuffer)
                {
                    foreach (var p in Changes)
                    {
                        var piex = getPixe(p);
                        var bytes = new byte[piex.Length];
                        fixed (byte* imgp = bytes)
                        {
                            var _width = piex.Width * pixeSize;
                            copy(bufp + piex.Offset, imgp, _width, piex.Height, _bufstride, _width);
                            list.Add(new Bitmap(_width, piex.Height, _width, PixelFormat, (IntPtr)imgp));
                        }
                    }
                }
            }

            return list;
        }
        public Bitmap getDiffBitmap()
        {
            var bytes = new byte[_pixelBuffer.Length];
            Bitmap bitmap = null;
            unsafe
            {
                fixed (byte* p = bytes)
                {
                    fixed (byte* p2 = _pixelBuffer)
                    {
                        foreach (var c in Changes)
                        {
                            var pixed = getPixe(c);
                            copy(p2 + pixed.Offset, p + pixed.Offset, pixed.Width * pixeSize, pixed.Height, _bufstride, _bufstride);
                        }
                        bitmap = new Bitmap(_w, _h, _bufstride, PixelFormat, (IntPtr)p);
                    }
                }
            }
            return bitmap;
        }
        public NetPixe getPixe(Point p)
        {
            return _pixes[p.X, p.Y];
        }
        public byte[] getPixeData(Point p)
        {
            var pixe = getPixe(p);
            var buf = new byte[pixe.Length];
            Buffer.BlockCopy(_pixelBuffer, pixe.Offset, buf, 0, buf.Length);
            return buf;
        }
        void addChange(int x, int y)
        {
            lock (locobj)
            {
                Changes.Add(new Point(x, y));
            }
        }
        public void clearChanges()
        {
            lock (locobj)
            {
                Changes.Clear();
            }
        }
        public List<Point> getChanges(bool clear = true)
        {
            List<Point> list = new List<Point>();
            lock (locobj)
            {
                Changes.ForEach(i => list.Add(i));
                if (clear)
                    Changes.Clear();
            }
            return list;
        }
        public void diff(Bitmap img)
        {
            if (img == null)
                return;
            BitmapData limg = null;
            try
            {
                unsafe
                {

                    limg = img.LockBits(new Rectangle(0, 0, _w, _h), ImageLockMode.ReadOnly, _format);
                    _imgstride = limg.Stride;
                    _imgstrideScale = _imgstride - _bufstride;
                    var scan0 = (byte*)limg.Scan0;
                    for (var y = 0; y < _ph; y++)//逐方块行
                    {
                        var minH = Math.Min(_h - y * tileSize, tileSize);
                        for (var x = 0; x < _pw; x++)//方块列
                        {
                            var minW = Math.Min(_w - x * tileSize, tileSize);
                            var offset = y * tileSize * _bufstride + x * tileSize * pixeSize;//数组偏移量
                            fixed (byte* bufP = _pixelBuffer)
                            {
                                byte* offset_scanP = scan0 + offset + _imgstrideScale;//当前方块数字指针
                                byte* offset_bufP = bufP + offset;//当前图片数字指针
                                var __ww = minW * pixeSize;
                                if (_pixes[x, y] == null)
                                {
                                    _pixes[x, y] = new NetPixe(minW, minH, offset, pixeSize);
                                    copy(offset_scanP, offset_bufP, __ww, minH);
                                    Changes.Add(new Point(x, y));
                                }
                                else
                                {
                                    var row_bufP = offset_bufP;//备份方块偏移量指针 作为行指针
                                    var row_scanP = offset_scanP;
                                    for (var y1 = 0; y1 < minH; y1++)
                                    {
                                        var col_bufP = row_bufP;
                                        var col_scanP = row_scanP;
                                        for (var x1 = 0; x1 < __ww; x1++)
                                        {
                                            if (col_bufP[0] != col_scanP[0])
                                            {
                                                copy(offset_scanP, offset_bufP, __ww, minH);
                                                Changes.Add(new Point(x, y));
                                                goto B;
                                            }
                                            col_bufP++;
                                            col_scanP++;
                                        }
                                        row_bufP += _bufstride;//行指针偏移一行
                                        row_scanP += _imgstride;//行指针偏移一行
                                    }
                                B:;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }
            finally
            {
                img.UnlockBits(limg);
            }
        }
        unsafe void copy(byte* sourceP, byte* distinationP, int width, int height)
        {
            copy(sourceP, distinationP, width, height, _imgstride, _bufstride);
        }
        unsafe void copy(byte* sourceP, byte* distinationP, int width, int height, int _sourceStride, int _distinationStride)
        {

            for (var y1 = 0; y1 < height; y1++)
            {
                var col_sourceP = sourceP;
                var col_distinationP = distinationP;
                for (var x1 = 0; x1 < width; x1++)
                {
                    *col_distinationP++ = *col_sourceP++;
                }
                distinationP += _distinationStride;
                sourceP += _sourceStride;
            }
        }
        public ImgDiff(int w, int h)
        {
            _w = w;
            _bufstride = _w * pixeSize;
            _h = h;
            _pw = (_w + tileSize - 1) / tileSize;
            _ph = (_h + tileSize - 1) / tileSize;
            _pixelBuffer = new byte[_w * _h * pixeSize];
            _pixes = new NetPixe[_pw, _ph];

        }
    }
}
