using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Saraff.Twain.DS.BitmapSource {

    /// <summary>
    /// Provide a Data Source that controls the bitmap image acquisition device and is written by the device developer to
    /// comply with TWAIN specifications. Traditional device drivers are now included with the
    /// Source software and do not need to be shipped by applications.
    /// </summary>
    /// <seealso cref="Saraff.Twain.DS.ImageDataSource" />
    public abstract class BitmapDataSource:ImageDataSource {
        private Bitmap _currentImage;
        private BitmapReader _bitmapReader;

        /// <summary>
        /// Causes the transfer of an image’s data from the Source to the application, via the Native transfer
        /// mechanism, to begin. The resulting data is stored in main memory in a single block. The data is
        /// stored in the Operating Systems native image format. The size of the image that can be transferred
        /// is limited to the size of the memory block that can be allocated by the Source. If the image is too
        /// large to fit, the Source may resize or crop the image.
        /// </summary>
        /// <returns>
        /// A image to transfer.
        /// </returns>
        protected override Image OnImageNativeXfer() {
            return this._currentImage;
        }

        /// <summary>
        /// This operation is used to initiate the transfer of an image from the Source to the application via the
        /// Buffered Memory transfer mechanism.
        /// This operation supports the transfer of successive blocks of image data (in strips or,optionally,
        /// tiles) from the Source into one or more main memory transfer buffers. These buffers(for strips)
        /// are allocated and owned by the application. For tiled transfers, the source allocates the buffers.
        /// The application should repeatedly invoke this operation while TWRC_SUCCESS is returned by the Source.
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="isMemFile">If set to <c>true</c> that transfer a MemFile.</param>
        /// <returns>
        /// Information about transmitting data.
        /// </returns>
        /// <exception cref="System.NotSupportedException"></exception>
        protected override ImageMemXfer OnImageMemXfer(long length, bool isMemFile) {
            if(!isMemFile) {
                return new ImageMemXfer {
                    YOffset=(uint)this._BitmapReader.RowsOffset,
                    XOffset=0U,
                    BytesPerRow=(uint)this._BitmapReader.BitmapStream.BitmapData.Stride,
                    Compression=this.XferEnvironment.ImageInfo.Compression,
                    ImageData=this._BitmapReader.ReadRows((int)(length/this._BitmapReader.BitmapStream.BitmapData.Stride)),
                    Columns=(uint)this._currentImage.Width,
                    Rows=(uint)this._BitmapReader.RowsAffected,
                    IsXferDone=this._BitmapReader.RowsOffset==this._BitmapReader.BitmapStream.BitmapData.Height
                };
            }
            throw new NotSupportedException();
        }

        /// <summary>
        /// This operation is used to initiate the transfer of an image from the Source to the application via the
        /// disk-file transfer mechanism. It causes the transfer to begin.
        /// </summary>
        protected override void OnImageFileXfer() {
            using(var _stream=File.Open(this.XferEnvironment.FileXferName,FileMode.Create)) {
                this._currentImage.Save(
                    _stream,
                    new Dictionary<TwFF,ImageFormat> {
                        {TwFF.Bmp,ImageFormat.Bmp},
                        {TwFF.Exif,ImageFormat.Exif},
                        {TwFF.Jfif,ImageFormat.Jpeg},
                        {TwFF.Png,ImageFormat.Png},
                        {TwFF.Tiff,ImageFormat.Tiff},
                        {TwFF.TiffMulti,ImageFormat.Tiff}
                    }[this.XferEnvironment.FileXferFormat]);
            }
        }

        /// <summary>
        /// Invoked to indicate that the Source has data that is ready to be transferred.
        /// </summary>
        protected override void OnXferReady() {
            if(this.XferEnvironment.PendingXfers==0) {
                this.XferEnvironment.PendingXfers=(ushort)Math.Abs((short)this[TwCap.XferCount].Value);
            }
            this._Acquire();

            base.OnXferReady();
        }

        /// <summary>
        /// Invoked at the end of every transfer to signal that the application has received all the data it expected.
        /// </summary>
        protected override void OnEndXfer() {
            this._Dispose();
            if(this.XferEnvironment.PendingXfers>0) {
                this._Acquire();
            }

            base.OnEndXfer();
        }

        /// <summary>
        /// Invoked when the pending transfers discarded.
        /// </summary>
        protected override void OnResetXfer() {
            this._Dispose();

            base.OnResetXfer();
        }

        private void _Dispose() {
            if(this._currentImage!=null) {
                this._currentImage.Dispose();
                this._currentImage=null;
            }
        }

        /// <summary>
        /// Acquire bitmap image.
        /// </summary>
        /// <returns>The bitmap image.</returns>
        protected abstract Bitmap Acquire();

        private void _Acquire() {
            this._BitmapReader=null;
            this._currentImage=this.Acquire();

            #region ImageInfo

            this.XferEnvironment.ImageInfo=new ImageInfo {
                BitsPerPixel=(short)Image.GetPixelFormatSize(this._currentImage.PixelFormat),
                BitsPerSample=new Dictionary<PixelFormat, short[]> {
                    {PixelFormat.Format1bppIndexed,new short[] { 1 }},
                    {PixelFormat.Format4bppIndexed,new short[] { 4 }},
                    {PixelFormat.Format8bppIndexed,new short[] { 8 }},
                    {PixelFormat.Format24bppRgb,new short[] { 8, 8, 8 }},
                    {PixelFormat.Format48bppRgb,new short[] { 16, 16, 16 }}}[this._currentImage.PixelFormat],
                Compression=TwCompression.None,
                ImageLength=this._currentImage.Height,
                ImageWidth=this._currentImage.Width,
                PixelType=(TwPixelType)this[TwCap.IPixelType].Value,
                Planar=false,
                XResolution=this._currentImage.HorizontalResolution,
                YResolution=this._currentImage.VerticalResolution
            };

            #endregion
        }

        private BitmapReader _BitmapReader {
            get {
                return this._bitmapReader??(this._bitmapReader=this._currentImage.CreateReader(this.XferEnvironment.ImageInfo));
            }
            set {
                if(value!=null) {
                    throw new ArgumentException();
                }
                if(this._bitmapReader!=null) {
                    this._bitmapReader.Dispose();
                    this._bitmapReader=null;
                }
            }
        }
    }
}
