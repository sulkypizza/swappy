using SharpDX;
using SharpDX.DirectWrite;
using System;

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

        public void CloseStream()
        {
            stream.Close();
        }

        public long GetLastWriteTime()
        {
            return 0;
        }

        ~CustomFontStream()
        {
            stream?.Dispose();
        }
    }
}

