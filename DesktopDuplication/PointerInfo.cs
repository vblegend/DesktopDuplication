using SharpDX.DXGI;
using System.Drawing;

namespace DesktopDuplication
{
    internal class PointerInfo
    {
        public byte[] PtrShapeBuffer = new byte[0];
        public OutputDuplicatePointerShapeInformation ShapeInfo;
        public Point Position;
        public bool Visible;
        public int WhoUpdatedPositionLast;
        public long LastTimeStamp;
    }
}
