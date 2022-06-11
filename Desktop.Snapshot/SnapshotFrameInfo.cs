using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Desktop.Snapshot
{
    public struct SnapshotFrameInfo
    {
        public Bitmap Image { get; internal set; }

        public MovedRegion[] MovedRegions { get; internal set; }

        public Rectangle[] UpdatedRegions { get; internal set; }

        public int AccumulatedFrames { get; internal set; }
        /// <summary>
        /// Gets whether the desktop image contains protected content that was already blacked out in the desktop image.
        /// </summary>
        public bool ProtectedContentMaskedOut { get; internal set; }

        /// <summary>
        /// Gets whether the operating system accumulated updates by coalescing updated regions. If so, the updated regions might contain unmodified pixels.
        /// </summary>
        public bool RectanglesCoalesced { get; internal set; }
    }
}
