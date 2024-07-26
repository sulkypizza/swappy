using SharpDX.DirectWrite;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace save_switcher
{
    internal class CustomFontCollectionLoader : CallbackBase, FontCollectionLoader, FontFileEnumerator, FontFileLoader
    {
        public DataStream KeyStream;

        CustomFontStream[] fontFileStreams;
        FontFile currentFont;
        Factory factory;
        DataStream enumeratorKey;

        public CustomFontCollectionLoader(Factory factory)
        {
            this.factory = factory;

            string[] fontPaths = new string[0];

            string[] files = Directory.GetFiles("Media");

            foreach (string file in files)
            {
                if (file.ToLower().EndsWith(".ttf"))
                {
                    string[] newPaths = new string[fontPaths.Length + 1];

                    for(int i = 0; i < fontPaths.Length; i++)
                    {
                        newPaths[i] = fontPaths[i];
                    }

                    newPaths[fontPaths.Length] = file;

                    fontPaths = newPaths;
                }
            }
            files = null;

            factory.RegisterFontCollectionLoader(this);
            factory.RegisterFontFileLoader(this);

            fontFileStreams = new CustomFontStream[fontPaths.Length];
            for (int i = 0; i < fontPaths.Length; i++)
            {
                FileStream fontFile = new FileStream(fontPaths[i], FileMode.Open, FileAccess.Read);

                if (fontFile.Length > int.MaxValue)
                    throw new Exception("The given font file size is too large! (size is greater than int32)");

                byte[] readFile = new byte[fontFile.Length];
                fontFile.Read(readFile, 0, readFile.Length);


                DataStream fileStream = new DataStream((int)fontFile.Length, true, true);

                fontFile.Close();

                fileStream.Write(readFile, 0, readFile.Length);
                fileStream.Position = 0;

                fontFileStreams[i] = new CustomFontStream(fileStream);
            }

            KeyStream = new DataStream(sizeof(int) * fontPaths.Length, true, true);
            for (int i = 0; i < fontPaths.Length; i++)
            {
                KeyStream.Write((int)i);
            }
            KeyStream.Position = 0;

        }

        ~CustomFontCollectionLoader()
        {
            KeyStream.Dispose();

            for(int i = 0; i < fontFileStreams.Length; i++)
            {
                fontFileStreams[i].Dispose();
            }

            this.Dispose();
        }

        public FontFileEnumerator CreateEnumeratorFromKey(SharpDX.DirectWrite.Factory factory, DataPointer pointer)
        {
            enumeratorKey = new DataStream(pointer);
            return this;
        }

        public FontFileStream CreateStreamFromKey(DataPointer key)
        {
            int i = Utilities.Read<int>(key.Pointer);
            fontFileStreams[i].AddReference();
            return fontFileStreams[i];
        }

        public FontFile CurrentFontFile
        {
            get
            {
                ((IUnknown)currentFont).AddReference();
                return currentFont;

            }
        }

        public bool MoveNext()
        {
            if (enumeratorKey.RemainingLength > 0)
            {
                if (currentFont != null)
                    currentFont.Dispose();

                currentFont = new FontFile(factory, enumeratorKey.PositionPointer, sizeof(int), this);
                enumeratorKey.Position += sizeof(int);

                return true;
            }
            else return false;
        }
    }
}

