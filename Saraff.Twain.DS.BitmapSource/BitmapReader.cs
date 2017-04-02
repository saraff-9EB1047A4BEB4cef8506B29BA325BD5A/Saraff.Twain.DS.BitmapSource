using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Saraff.Twain.DS.BitmapSource {

    internal sealed class BitmapReader:BinaryReader {
        private int? _sumBitsPerSample=null;

        public BitmapReader(BitmapStream stream) : base(stream) {
        }

        public byte[] ReadRow() {
            return this.ReadRows(1);
        }

        public byte[] ReadRows(int count) {
            var _result=this.ReadBytes(count*this.BitmapStream.BitmapData.Stride);
            this.RowsOffset+=this.RowsAffected=_result.Length/this.BitmapStream.BitmapData.Stride;

            if(this.BitmapStream.ImageInfo.PixelType==TwPixelType.RGB) {
                using(var _stream=new MemoryStream(_result, true)) {
                    var _reader=new BinaryReader(_stream);
                    var _writer=new BinaryWriter(_stream);

                    for(var _row=0; _row<this.RowsAffected; _row++, _stream.Seek(this.BitmapStream.BitmapData.Stride*_row, SeekOrigin.Begin)) {
                        for(var _pixel=0; _pixel<this.BitmapStream.BitmapData.Width; _pixel++) {
                            var _buf=0UL;
                            var _bytesPerPixel=this.BitmapStream.ImageInfo.BitsPerPixel>>3;
                            for(var i=0; i<_bytesPerPixel; i++) {
                                _buf|=(ulong)_reader.ReadByte()<<(i<<3);
                            }
                            _stream.Seek(-_bytesPerPixel, SeekOrigin.Current);
                            _buf=this._Transform(_buf);
                            for(var i=0; i<_bytesPerPixel; i++) {
                                _writer.Write((byte)((_buf>>(i<<3))&0xff));
                            }
                        }
                    }
                }
            }

            return _result;
        }

        public BitmapStream BitmapStream {
            get {
                return this.BaseStream as BitmapStream;
            }
        }

        public int RowsAffected {
            get;
            private set;
        }

        public int RowsOffset {
            get;
            private set;
        }

        private int _SumBitsPerSample {
            get {
                if(this._sumBitsPerSample==null) {
                    this._sumBitsPerSample=this.BitmapStream.ImageInfo.BitsPerSample.Sum(x => x);
                }
                return this._sumBitsPerSample.Value;
            }
        }

        private ulong _Transform(ulong pixel) {
            var _result=pixel&(0xffffffffffffffff<<this._SumBitsPerSample);
            pixel<<=this.BitmapStream.ImageInfo.BitsPerPixel-this._SumBitsPerSample;
            foreach(var _bitsPerSample in this.BitmapStream.ImageInfo.BitsPerSample) {
                _result>>=_bitsPerSample;
                _result|=pixel&(((1UL<<_bitsPerSample)-1)<<this.BitmapStream.ImageInfo.BitsPerPixel-_bitsPerSample);
                pixel<<=_bitsPerSample;
            }
            return _result;
        }
    }

    internal sealed class BitmapStream:MemoryStream {
        private readonly bool _canWrite=true;

        public BitmapStream(Bitmap bitmap, ImageInfo info) {
            this.ImageInfo=info;
            this.BitmapData=bitmap.LockBits(new Rectangle(new Point(0, 0), bitmap.Size), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try {
                var _buf=new byte[this.BitmapData.Stride*this.BitmapData.Height];
                Marshal.Copy(this.BitmapData.Scan0, _buf, 0, _buf.Length);
                this.Write(_buf, 0, _buf.Length);
                this.Seek(0, SeekOrigin.Begin);
                this._canWrite=false;
            } finally {
                bitmap.UnlockBits(this.BitmapData);
            }
        }

        public override bool CanWrite {
            get {
                return this._canWrite;
            }
        }

        public BitmapData BitmapData {
            get;
            private set;
        }

        public ImageInfo ImageInfo {
            get;
            private set;
        }
    }

    internal static class BitmapAux {

        public static BitmapReader CreateReader(this Bitmap bitmap, ImageInfo info) {
            return new BitmapReader(new BitmapStream(bitmap, info));
        }
    }
}
