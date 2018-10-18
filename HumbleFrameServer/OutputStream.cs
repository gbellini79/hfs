using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using HumbleFrameServer.WAVELib;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;


namespace HumbleFrameServer
{
    internal enum WriteType
    {
        Video,
        Audio,
        Undefined
    }

    class OutputStream
    {
        private BinaryWriter _outStream = null;

        private bool _hasVideo;
        private bool _hasAudio;
        private WriteType _lastWriteType = WriteType.Undefined;

        //Video
        private decimal _fps;
        private uint _width;
        private uint _height;

        //Audio
        private uint _samplerate;
        private uint _channels;
        private uint _bitpersample;

        /// <summary>
        /// samplerate / _fps
        /// </summary>
        private decimal _samplesPerFrame;

        private bool _isReady = false;
        /// <summary>
        /// True if the outstream is ready to accept data
        /// </summary>
        public bool IsReady { get { return _isReady; } }

        public OutputStream(Stream outStream, bool HasVideo, bool HasAudio)
        {
            _outStream = new BinaryWriter(outStream);

            _hasVideo = HasVideo;
            _hasAudio = HasAudio;
        }

        public void initStream(decimal fps = 0, uint width = 0, uint height = 0, uint samplerate = 0, uint channels = 0, uint bitpersample = 0)
        {
            //video
            _fps = fps;
            _width = width;
            _height = height;

            //Audio
            _samplerate = samplerate;
            _channels = channels;
            _bitpersample = bitpersample;
            _audioBuffer = new List<int[]>(Convert.ToInt32(_samplerate));

            _samplesPerFrame = _samplerate / _fps;

            //Write RIFF
            _outStream.WriteChars("RIFF");
            _outStream.Write(0xffffffffu);
            _outStream.WriteChars("AVIX");

            //Write LIST hdrl
            _outStream.WriteChars("LIST");
            _outStream.Write(68u); //calcolare dimensione header?
            _outStream.WriteChars("hdrl");

            //Write avih
            _outStream.WriteChars("avih");
            _outStream.Write(56u); //Size in byte
            _outStream.Write(Convert.ToUInt32(1000000 / _fps)); //dwMicroSecPerFrame
            _outStream.Write(0xbb80u); //dwMaxBytesPerSec
            _outStream.Write(0u); //dwPaddingGranularity
            _outStream.Write(0u); //dwFlags
            _outStream.Write(0u); //dwTotalFrames
            _outStream.Write(0u); //dwInitialFrames
            _outStream.Write(2u); //dwStreams
            _outStream.Write(0x100000u); //dwSuggestedBufferSize
            _outStream.Write(_width); //dwWidth
            _outStream.Write(_height); //dwWidth
            _outStream.Write(0u); //dwReserved[0]
            _outStream.Write(0u); //dwReserved[1]
            _outStream.Write(0u); //dwReserved[2]
            _outStream.Write(0u); //dwReserved[3]

            if (_hasVideo)
            {
                //Write LIST strl
                _outStream.WriteChars("LIST");
                _outStream.Write(116u); //calcolare?
                _outStream.WriteChars("strl");

                //Write strh
                _outStream.WriteChars("strh");
                _outStream.Write(56u); //Size in byte
                _outStream.WriteChars("vids");
                _outStream.Write(0u); //fccHandler (rawvideo)
                _outStream.Write(0u); //dwFlags
                _outStream.Write((short)0); //wPriority
                _outStream.Write((short)0); //wLanguage
                _outStream.Write(0u); //dwInitialFrames
                _outStream.Write(1u * 1000000); //dwScale
                _outStream.Write(Convert.ToUInt32(_fps * 1000000)); //dwRate (fps)
                _outStream.Write(0u); //dwStart
                _outStream.Write(0x40000000u); //dwLength
                _outStream.Write(0x100000u); //dwSuggestedBufferSize
                _outStream.Write(0xffffffffu); //dwQuality
                _outStream.Write(0u); //dwSampleSize
                _outStream.Write((short)0); //rcFrame left
                _outStream.Write((short)0); //rcFrame top
                _outStream.Write(Convert.ToUInt16(_width)); //rcFrame width
                _outStream.Write(Convert.ToUInt16(_height)); //rcFrame height

                //Write strf + BITMAPINFOHEADER
                _outStream.WriteChars("strf");
                _outStream.Write(40u); //Size in byte (strf)
                _outStream.Write(40u); //Size in byte (BITMAPINFOHEADER)
                _outStream.Write(Convert.ToInt32(_width)); //signed long Width
                _outStream.Write(0 - Convert.ToInt32(_height)); //signed long negative Height
                _outStream.Write((ushort)1); //wPlanes (always 1)
                _outStream.Write((ushort)32); //wBitCount (always 24)
                _outStream.Write(0u); //dwCompression (0 = BI_RGB = uncompressed)
                _outStream.Write(0u); //dwSizeImage (w*h*3byte) (can be zero for BI_RGB)
                _outStream.Write(0); //long XPelsPerMeter
                _outStream.Write(0); //long YPelsPerMeter
                _outStream.Write(0u); //dwClrUsed
                _outStream.Write(0u); //dwClrImportant
            }

            if (_hasAudio)
            {
                //Write LIST strl
                _outStream.WriteChars("LIST");
                _outStream.Write(92u); //calcolare?
                _outStream.WriteChars("strl");

                //Write strh
                _outStream.WriteChars("strh");
                _outStream.Write(56u); //Size in byte
                _outStream.WriteChars("auds");
                _outStream.Write(1u); //fccHandler (uncompressed?)
                _outStream.Write(0u); //dwFlags
                _outStream.Write((short)0); //wPriority
                _outStream.Write((short)0); //wLanguage
                _outStream.Write(0u); //dwInitialFrames
                _outStream.Write(1u); //dwScale
                _outStream.Write(_samplerate); //dwRate (Hz)
                _outStream.Write(0u); //dwStart
                _outStream.Write(0x238530u); //dwLength 1GByte
                _outStream.Write(0x1000u); //dwSuggestedBufferSize
                _outStream.Write(0xffffffffu); //dwQuality
                _outStream.Write(_bitpersample / 8u * _channels); //dwSampleSize
                _outStream.Write((short)0); //rcFrame left
                _outStream.Write((short)0); //rcFrame top
                _outStream.Write((short)0); //rcFrame width
                _outStream.Write((short)0); //rcFrame height

                _outStream.WriteChars("strf");
                _outStream.Write(16u); //Size in byte (strf)
                _outStream.Write((short)1); //wFormatTag (1 = uncompressed?)
                _outStream.Write(Convert.ToInt16(_channels)); //wChannels
                _outStream.Write(_samplerate); //dwSamplePerSec
                _outStream.Write(_bitpersample / 8u * _channels * _samplerate); //dwAvgBytesPerSec (dwSamplePerSec * dwSampleSize)
                _outStream.Write(Convert.ToUInt16(_bitpersample / 8u * _channels)); //wBlockAlign = dwSampleSize?

                _outStream.Write(Convert.ToUInt16(_bitpersample)); //wcbSize (Extra info size) //Required for some reason...
                //_outStream.WriteChars("JUNK"); //Required for some reason...
                //_outStream.Write(new byte[_bitpersample - 4]); //Required for some reason...
            }

            //Write movi chunk
            _outStream.WriteChars("LIST");
            _outStream.Write(0xffffffffu);
            _outStream.WriteChars("movi");

            _isReady = true;
        }

        public void Close()
        {
            _outStream.Close();
        }

        private List<int[]> _audioBuffer;

        public void WriteAudioSample(int[] sample)
        {
            if (_hasAudio)
            {
                _audioBuffer.Add(sample);
                _lastWriteType = WriteType.Audio;
            }
        }

        public void FlushAudioBuffer()
        {
            if (_hasAudio)
            {
                _outStream.WriteChars("01wb");

                switch (_bitpersample)
                {
                    case 8:
                        _outStream.Write(_audioBuffer.Count * _audioBuffer[0].Length);
                        for (int b = 0; b < _audioBuffer.Count; b++)
                        {
                            for (int s = 0; s < _audioBuffer[b].Length; s++)
                            {
                                _outStream.Write((byte)(_audioBuffer[b][s]));
                            }
                        }
                        break;
                    case 16:
                        //_outStream.Write(_audioBuffer.Count * _audioBuffer[0].Length * 2);
                        _outStream.Write((int)_audioBuffer.Select(x => x.Length).Sum() * 2);
                        for (int b = 0; b < _audioBuffer.Count; b++)
                        {
                            for (int s = 0; s < _audioBuffer[b].Length; s++)
                            {
                                _outStream.Write((short)(_audioBuffer[b][s]));
                            }
                        }
                        break;
                    case 24:
                        throw new NotImplementedException(string.Format("{0}bit audio is unsupported", _bitpersample));
                    //break;
                    default:
                        throw new NotImplementedException(string.Format("{0}bit audio is unsupported", _bitpersample));
                        //break;
                }
                _audioBuffer.Clear();
            }
        }

        public void WriteVideoFrame(Bitmap frame)
        {
            if (_hasVideo)
            {
                if (_lastWriteType == WriteType.Audio)
                {
                    FlushAudioBuffer();
                }
                _outStream.Write(frame);
                _lastWriteType = WriteType.Video;
            }
        }
    }

    public class NullStream : Stream
    {

        public override bool CanRead
        {
            get { throw new NotImplementedException(); }
        }

        public override bool CanSeek
        {
            get { throw new NotImplementedException(); }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {

        }
    }

    public static class Utils
    {
        public static void WriteChars(this BinaryWriter binaryWriter, string chars)
        {
            foreach (char c in chars)
            {
                binaryWriter.Write(c);
            }
        }

        public static void Write(this BinaryWriter binaryWriter, Bitmap frame)
        {
            byte[] output = new byte[frame.Width * frame.Height * Bitmap.GetPixelFormatSize(frame.PixelFormat) / 8];
            BitmapData bmpData = frame.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.ReadOnly, frame.PixelFormat);
            Marshal.Copy(bmpData.Scan0, output, 0, output.Length);
            frame.UnlockBits(bmpData);

            binaryWriter.WriteChars("00dc");
            binaryWriter.Write(output.Length);
            binaryWriter.Write(output);

            //            frame.Save(binaryWriter.BaseStream, ImageFormat.Png);
        }
    }
}
