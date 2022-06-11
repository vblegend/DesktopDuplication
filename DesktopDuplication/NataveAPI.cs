using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopDuplication
{

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

    public class NataveAPI
    {

        [DllImport("user32.dll", EntryPoint = "GetCursorInfo")]
        public static extern bool GetCursorInfo(out CURSORINFO pci);







        public static Cursor GetCursor()
        {
            var ci = new CURSORINFO();
            ci.cbSize = Marshal.SizeOf(ci);
            if (GetCursorInfo(out ci))
            {
                if (ci.hCursor == IntPtr.Zero) return null; 
                return new Cursor(ci.hCursor);
            }
            return null;
        }




    }
}
