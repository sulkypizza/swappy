using SharpDX;
using SharpDX.DirectWrite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace save_switcher
{
    internal class CustomFontStream : CallbackBase, FontFileStream
    {
        DataStream stream;

        public CustomFontStream(DataStream dataSteam)
        {
            stream = dataSteam;
        }

        public void ReadFileFragment(out IntPtr fragmentStart, long fileOffset, long fragmentSize, out IntPtr fragmentContext)
        {
            lock (this)
            {
                stream.Position = fileOffset;

                fragmentContext = IntPtr.Zero;
                fragmentStart = stream.PositionPointer;
            }
        }

        public void ReleaseFileFragment(IntPtr fragmentContext) { }

        public long GetFileSize()
        {
            return stream.Length;
        }

        public long GetLastWriteTime()
        {
            return 0;
        }
    }
}

