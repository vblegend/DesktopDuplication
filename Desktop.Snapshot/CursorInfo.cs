using System;
using System.Drawing;


namespace Desktop.Snapshot
{
    [Flags]
    public enum CursorChangeType
    {
        None = 0,
        Position = 1,
        Shape = 2,
        PositionAndShape =3
    }




    public struct CursorInfo
    {
        public Size Size;
        public Point Location;
        public CursorChangeType Type;
        public Bitmap Icon;
    }
}
