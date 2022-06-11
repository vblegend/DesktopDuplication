using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SharpDX.Mathematics.Interop;
using System.Threading.Tasks;
using System.Threading;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;

namespace Desktop.Snapshot
{
    public delegate void FrameEventHandler(SnapshotFrameInfo frameInfo);
    public delegate void CursorEventHandler(CursorInfo cursor);
    public delegate void AdapterResetEventHandler(Output output, Adapter1 adapter, Rectangle screenRect);
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public Int32 x;
        public Int32 y;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public Int32 cbSize;        // Specifies the size, in bytes, of the structure. 
        public Int32 flags;         // Specifies the cursor state. This parameter can be one of the following values:
        public IntPtr hCursor;          // Handle to the cursor. 
        public POINT ptScreenPos;       // A POINT structure that receives the screen coordinates of the cursor. 
    }

    public class DesktopMirror : IDisposable
    {
        private const Int32 CURSOR_SHOWING = 0x0001;
        private const Int32 DI_NORMAL = 0x0003;

        [DllImport("msvcrt.dll")]
        private static extern Int32 memcmp(IntPtr lp1, IntPtr lp2, IntPtr count);


        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyHeight, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

        [DllImport("user32.dll", EntryPoint = "GetCursorInfo")]
        public static extern bool GetCursorInfo(out CURSORINFO pci);

        private RawPoint lastCursorLocation { get; set; }
        private Int32 graphicsCardAdapter { get; set; }
        private Int32 outputDevice { get; set; }
        private Adapter1 adapter { get; set; }
        private Device mDevice { get; set; }
        private Output output { get; set; }
        //private OutputDescription mOutputDesc;
        private Texture2DDescription mTextureDesc;
        private OutputDuplication mDeskDupl;
        private Bitmap bitmapBuffer { get; set; }
        private Texture2D desktopImageTexture { get; set; }
        private Rectangle boundsRect { get; set; }
        private CancellationTokenSource workCancelToken;
        private OutputDuplicateFrameInformation outFrameInfo;
        private Output1 output1;
        private Byte[] PtrShapeBuffer = new Byte[0];
        public event FrameEventHandler FrameEvent;
        public event CursorEventHandler CursorEvent;
        public event AdapterResetEventHandler ResetEvent;
        private Bitmap CursorIcon;


        public DesktopMirror(Int32 graphicsCardAdapter, Int32 outputDevice)
        {
            this.graphicsCardAdapter = graphicsCardAdapter;
            this.outputDevice = outputDevice;
            this.CursorIcon = new Bitmap(64, 64);
            this.Init();
        }


        public Int32 ClientLeft
        {
            get
            {
                return this.output.Description.DesktopBounds.Left;
            }
        }
        public Int32 ClientTop
        {
            get
            {
                return this.output.Description.DesktopBounds.Top;
            }
        }


        public Int32 ClientWidth
        {
            get
            {
                var bounds = this.output.Description.DesktopBounds;
                return bounds.Right - bounds.Left;
            }
        }


        public Int32 ClientHeight
        {
            get
            {
                var bounds = this.output.Description.DesktopBounds;
                return bounds.Bottom - bounds.Top;
            }
        }

        private void Init()
        {
            try
            {
                //if (this.adapter != null) this.adapter.Dispose(); 
                if (this.adapter == null)
                {
                    this.adapter = new Factory1().GetAdapter1(this.graphicsCardAdapter);
                }

            }
            catch (SharpDXException)
            {
                throw new Exception("Could not find the specified graphics card adapter.");
            }
            // if (this.mDevice != null) this.mDevice.Dispose();
            if (this.mDevice == null)
            {
                this.mDevice = new Device(adapter);
            }
            try
            {
                //if (this.output != null) this.output.Dispose();
                if (this.output == null)
                    this.output = this.adapter.GetOutput(this.outputDevice);
            }
            catch (SharpDXException)
            {
                throw new Exception("Could not find the specified output device.");
            }
            //if (this.output1 != null) this.output1.Dispose();
            if (this.output1 == null)
            {
                this.output1 = output.QueryInterface<Output1>();
            }
            //this.mOutputDesc = output.Description;
            this.mTextureDesc = new Texture2DDescription()
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm, //B8G8R8A8_UNorm
                Width = this.output.Description.DesktopBounds.Right - this.output.Description.DesktopBounds.Left,
                Height = this.output.Description.DesktopBounds.Bottom - this.output.Description.DesktopBounds.Top,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            try
            {
                if (this.mDeskDupl != null) this.mDeskDupl.Dispose();
                this.mDeskDupl = this.output1.DuplicateOutput(mDevice);
            }
            catch (SharpDXException ex)
            {
                if (ex.ResultCode.Code == SharpDX.DXGI.ResultCode.NotCurrentlyAvailable.Result.Code)
                {
                    throw new Exception("There is already the maximum number of applications using the Desktop Duplication API running, please close one of the applications and try again.");
                }
            }

            this.boundsRect = Rectangle.FromLTRB(output.Description.DesktopBounds.Left, output.Description.DesktopBounds.Top, this.output.Description.DesktopBounds.Right, this.output.Description.DesktopBounds.Bottom);
            this.ResetEvent?.Invoke(this.output, this.adapter, this.boundsRect);
            if (this.desktopImageTexture != null) this.desktopImageTexture.Dispose();
            desktopImageTexture = new Texture2D(mDevice, mTextureDesc);
            if (this.bitmapBuffer != null) this.bitmapBuffer.Dispose();
            this.bitmapBuffer = new Bitmap(this.output.Description.DesktopBounds.Right - this.output.Description.DesktopBounds.Left, this.output.Description.DesktopBounds.Bottom - this.output.Description.DesktopBounds.Top, PixelFormat.Format32bppArgb);
        }





        public void Start()
        {
            this.workCancelToken = new CancellationTokenSource();
            var task = Task.Factory.StartNew(this.workloop, this.workCancelToken.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }


        public void Stop()
        {
            this.workCancelToken.Cancel();
        }



        private void workloop( )
        {
            while (!this.IsDispose && !this.workCancelToken.Token.IsCancellationRequested)
            {
                Thread.Sleep(10);
                try
                {
                    if (this.RequestFrame()) continue;
                    var frameinfo = this.UpdateFrameBuffer();
                    if (frameinfo.Image != null)
                    {
                        this.FrameEvent?.Invoke(frameinfo);
                    }

                    var cur = this.UpdateCursor();
                    if (cur.Type != CursorChangeType.None)
                    {
                        this.CursorEvent?.Invoke(cur);
                    }
                    mDeskDupl.ReleaseFrame();
                }
                catch (SharpDX.SharpDXException ex)
                {
                    if (ex.Descriptor.ApiCode == "AccessLost")
                    {
                        // SharpDX.SharpDXException:“HRESULT: [0x887A0026], Module: [SharpDX.DXGI], ApiCode: [DXGI_ERROR_ACCESS_LOST/AccessLost], Message: 键控互斥已弃用。”
                        this.Init();
                        continue;
                    }
                    throw ex;
                }
                finally
                {

                }

            }

        }

        private SnapshotFrameInfo UpdateFrameBuffer()
        {
            SnapshotFrameInfo frameInfo = new SnapshotFrameInfo();
            if (outFrameInfo.TotalMetadataBufferSize > 0)
            {
                frameInfo.ProtectedContentMaskedOut = outFrameInfo.ProtectedContentMaskedOut;
                frameInfo.AccumulatedFrames = outFrameInfo.AccumulatedFrames;
                frameInfo.Image = this.bitmapBuffer;
                frameInfo.RectanglesCoalesced = outFrameInfo.RectsCoalesced;
                var mapDest = this.bitmapBuffer.LockBits(this.boundsRect, ImageLockMode.WriteOnly, this.bitmapBuffer.PixelFormat);
                var mapSource = mDevice.ImmediateContext.MapSubresource(desktopImageTexture, 0, MapMode.Read, MapFlags.None);

                // update move regions
                OutputDuplicateMoveRectangle[] movedRectangles = new OutputDuplicateMoveRectangle[outFrameInfo.TotalMetadataBufferSize];
                mDeskDupl.GetFrameMoveRects(movedRectangles.Length, movedRectangles, out var movedRegionsLength);
                // Get moved regions

                frameInfo.MovedRegions = new MovedRegion[movedRegionsLength / Marshal.SizeOf(typeof(OutputDuplicateMoveRectangle))];
                for (int i = 0; i < frameInfo.MovedRegions.Length; i++)
                {
                    frameInfo.MovedRegions[i] = new MovedRegion()
                    {
                        Source = new System.Drawing.Point(movedRectangles[i].SourcePoint.X, movedRectangles[i].SourcePoint.Y),
                        Destination = Rectangle.FromLTRB(movedRectangles[i].DestinationRect.Left, movedRectangles[i].DestinationRect.Top, movedRectangles[i].DestinationRect.Right, movedRectangles[i].DestinationRect.Bottom)
                    };
                }

                // Get dirty regions
                RawRectangle[] dirtyRectangles = new RawRectangle[outFrameInfo.TotalMetadataBufferSize];
                mDeskDupl.GetFrameDirtyRects(dirtyRectangles.Length, dirtyRectangles, out var dirtyRegionsLength);
                frameInfo.UpdatedRegions = new System.Drawing.Rectangle[dirtyRegionsLength / Marshal.SizeOf(typeof(Rectangle))];

                Int32 bit = 4;
                for (int i = 0; i < frameInfo.UpdatedRegions.Length; i++)
                {

                    //var sw = Stopwatch.StartNew();
                    //dirtyRectangles[i] = this.OptimizeRectangle(mapDest.Scan0, mapSource.DataPointer, dirtyRectangles[i], mapSource.RowPitch);
                    //sw.Stop();
                    //Console.WriteLine(sw.ElapsedMilliseconds);


                    var rect = Rectangle.FromLTRB(dirtyRectangles[i].Left, dirtyRectangles[i].Top, dirtyRectangles[i].Right, dirtyRectangles[i].Bottom);
                    frameInfo.UpdatedRegions[i] = rect;
                    // init base addr
                    var sourcePtr = IntPtr.Add(mapSource.DataPointer, rect.Left * bit + rect.Top * mapSource.RowPitch);
                    var destPtr = IntPtr.Add(mapDest.Scan0, rect.Left * bit + rect.Top * mapDest.Stride);
                    for (int y = 0; y < rect.Height; y++)
                    {
                        Utilities.CopyMemory(destPtr, sourcePtr, rect.Width * bit);
                        sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                        destPtr = IntPtr.Add(destPtr, mapDest.Stride);
                    }
                }
                this.bitmapBuffer.UnlockBits(mapDest);
                mDevice.ImmediateContext.UnmapSubresource(desktopImageTexture, 0);
            }
            return frameInfo;
        }

        private unsafe RawRectangle OptimizeRectangle(IntPtr oldBuffer, IntPtr newBuffer, RawRectangle rectangle, Int32 RowPitch)
        {
            var bit = 4;
            var center = (rectangle.Right + rectangle.Left) / 2;
            var width = rectangle.Right - rectangle.Left;
            var sfff = width / 2;
            var left = rectangle.Left;
            var right = rectangle.Right;
            var vLeft = true;
            var vRight = true;



            var vCenter = (rectangle.Bottom + rectangle.Top) / 2;

            var height = rectangle.Bottom - rectangle.Top;

            var vStart = height / 2;

            var top = rectangle.Top;
            var bottom = rectangle.Bottom;
            var vtop = true;
            var vbottom = true;






            for (int i = sfff; i >= 0; i--)
            {
                if (vLeft)
                {
                    left = center - i - 1;
                    var p1 = oldBuffer + left * bit + rectangle.Top * RowPitch;
                    var p2 = newBuffer + left * bit + rectangle.Top * RowPitch;
                    if (!this.CmpMemory((Int32*)p1, (Int32*)p2, RowPitch / 4, rectangle.Bottom - rectangle.Top))
                    {
                        vLeft = false;
                    }
                }
                if (vRight)
                {
                    right = center + i - 1;
                    var p1 = oldBuffer + right * bit + rectangle.Top * RowPitch;
                    var p2 = newBuffer + right * bit + rectangle.Top * RowPitch;
                    if (!this.CmpMemory((Int32*)p1, (Int32*)p2, RowPitch / 4, rectangle.Bottom - rectangle.Top))
                    {
                        vRight = false;
                    }
                }
            }

            var W = right - left;
            for (int i = vStart; i >= 0; i--)
            {
                if (vtop)
                {
                    top = vCenter - i - 1;
                    var p1 = oldBuffer + left * bit + top * RowPitch;
                    var p2 = newBuffer + left * bit + top * RowPitch;
                    if (!this.CmpMemory((Int32*)p1, (Int32*)p2, 1, W))
                    {
                        vtop = false;
                    }
                }
                if (vbottom)
                {
                    bottom = vCenter + i - 1;
                    var p1 = oldBuffer + right * bit + bottom * RowPitch;
                    var p2 = newBuffer + right * bit + bottom * RowPitch;
                    if (!this.CmpMemory((Int32*)p1, (Int32*)p2, 1, W))
                    {
                        vbottom = false;
                    }
                }
            }


            return new RawRectangle(left, top, right, bottom);







            //Console.WriteLine( "none");
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe Boolean CmpMemory(Int32* ptr1, Int32* ptr2, Int32 step, Int32 count)
        {
            for (int i = 0; i < count; i++)
            {
                if (*ptr1 != *ptr2) return false;
                ptr1 += step;
                ptr2 += step;
            }
            return true;
        }



        private CursorInfo UpdateCursor()
        {
            var info = new CursorInfo();
            OutputDuplicatePointerShapeInformation shapeInfo;
            if (lastCursorLocation.Equals(outFrameInfo.PointerPosition.Position) && outFrameInfo.PointerShapeBufferSize <= 0) return info;
            // No new shape
            if (outFrameInfo.PointerShapeBufferSize > 0)
            {
                if (outFrameInfo.PointerShapeBufferSize > PtrShapeBuffer.Length)
                {
                    PtrShapeBuffer = new byte[outFrameInfo.PointerShapeBufferSize];
                }
                unsafe
                {
                    fixed (byte* ptrShapeBufferPtr = PtrShapeBuffer)
                    {
                        mDeskDupl.GetFramePointerShape(outFrameInfo.PointerShapeBufferSize, (IntPtr)ptrShapeBufferPtr, out int bufferSize, out shapeInfo);
                    }
                }
                info.Size.Width = shapeInfo.Width;
                info.Size.Height = shapeInfo.Type == 1 ? shapeInfo.Height / 2 : shapeInfo.Height;
                info.Type = CursorChangeType.Shape;

                var pci = new CURSORINFO();
                pci.cbSize = Marshal.SizeOf(pci);
                if (GetCursorInfo(out pci) && pci.hCursor != IntPtr.Zero)
                {
                    if (pci.flags == CURSOR_SHOWING)
                    {
                        info.Icon = this.CursorIcon;
                        using (var g = Graphics.FromImage(this.CursorIcon))
                        {
                            g.Clear(Color.Transparent);
                            var hdc = g.GetHdc();
                            DrawIconEx(hdc, 0, 0, pci.hCursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL);
                            g.ReleaseHdc();
                        }
                    }
                }
            }
            if (!lastCursorLocation.Equals(outFrameInfo.PointerPosition.Position))
            {
                this.lastCursorLocation = outFrameInfo.PointerPosition.Position;
                info.Location.X = outFrameInfo.PointerPosition.Position.X;
                info.Location.Y = outFrameInfo.PointerPosition.Position.Y;
                info.Type = info.Type | CursorChangeType.Position;
            }
            return info;
        }





        private Boolean RequestFrame()
        {
            SharpDX.DXGI.Resource desktopResource = null;
            try
            {
                this.outFrameInfo = new OutputDuplicateFrameInformation();
                mDeskDupl.AcquireNextFrame(10000, out this.outFrameInfo, out desktopResource);
                using (var tempTexture = desktopResource.QueryInterface<Texture2D>())
                {
                    mDevice.ImmediateContext.CopyResource(tempTexture, desktopImageTexture);
                }
            }
            catch (SharpDXException ex)
            {
                if (ex.ResultCode.Code == SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                {
                    return true;
                }
                if (ex.ResultCode.Failure)
                {
                    throw ex;
                }
            }
            finally
            {
                if (desktopResource != null)
                {
                    desktopResource.Dispose();
                }
            }
            return false;
        }

        public void Dispose()
        {
            this.IsDispose = true;
        }



        public Boolean IsDispose { get; private set; }
    }
}
