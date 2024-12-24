using SharpDX.Direct2D1;
using SharpDX.IO;
using SharpDX.WIC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace save_switcher.Elements
{
    internal class BitmapImage
    {
        public SharpDX.Direct2D1.Bitmap Image;

        private ImagingFactory imagingFactory;
        private NativeFileStream fileStream;
        private BitmapDecoder bitmapDecoder;
        private BitmapFrameDecode frame;
        private FormatConverter converter;

        public BitmapImage(string fileLocation, DeviceContext d2dDeviceContext, ImagingFactory imageFactory)
        {
            imagingFactory = new ImagingFactory();

            try
            {
                fileStream = new NativeFileStream(fileLocation, NativeFileMode.Open, NativeFileAccess.Read);
                bitmapDecoder = new BitmapDecoder(imagingFactory, fileStream, DecodeOptions.CacheOnDemand);
                frame = bitmapDecoder.GetFrame(0);
            }
            catch 
            {
                fileStream?.Close();
                fileStream?.Dispose();

                fileStream = new NativeFileStream("Media/image_error.png", NativeFileMode.Open, NativeFileAccess.Read);
                bitmapDecoder = new BitmapDecoder(imagingFactory, fileStream, DecodeOptions.CacheOnDemand);
                frame = bitmapDecoder.GetFrame(0);
            }

            converter = new FormatConverter(imagingFactory);
            converter.Initialize(frame, SharpDX.WIC.PixelFormat.Format32bppPRGBA);

            Image = SharpDX.Direct2D1.Bitmap.FromWicBitmap(d2dDeviceContext, converter);

            frame.Dispose();
            converter.Dispose();
            bitmapDecoder.Dispose();
            fileStream.Close();
            fileStream.Dispose();
        }
    }
}
