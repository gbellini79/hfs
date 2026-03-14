using System;
using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace MNGj
{
    internal struct Simplicity_profile_Elements_Struct
    {
        internal uint Profile_Validity;
        internal uint Simple_MNG_features;
        internal uint Complex_MNG_features;
        internal uint Internal_transparency;
        internal uint JNG;
        internal uint Delta_PNG;
        internal uint Validity_flag_bits789;
        internal uint Background_transparency;
        internal uint Semi_transparency;
        internal uint Stored_objectbuffers;
    }

    internal struct MHDR_Chunk_Struct
    {
        internal uint Frame_width;
        internal uint Frame_height;
        internal uint Ticks_per_second;
        internal uint Nominal_layer_count;
        internal uint Nominal_frame_coun;
        internal uint Nominal_play_time;
        internal uint Simplicity_profile;
        internal Simplicity_profile_Elements_Struct Simplicity_profile_Elements;
    }

    internal enum MNGChunkTypes
    {
        MHDR, //MNG file header
        MEND, //MNG end of file
        IHDR, //PNG image header, which is the first chunk in a PNG datastream
        IDAT, //PNG image data chunks
        IEND, //PNG image trailer, which is the last chunk in a PNG datastream
        Error
    }

    internal enum TransitionsType
    {
        Cut,
        Crossfade
    }

    internal class MNGStream : IDisposable
    {
        internal byte[] PNGHeader = new byte[8] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        internal byte[] MNGHeader = new byte[8] { 0x8a, 0x4d, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };

        /// <summary>
        /// Internal binary stream
        /// </summary>
        internal BinaryReader _MNGStream;

        private bool _disposed;

        private const uint MHDR_NAME = 0x4D484452;
        private const uint MEND_NAME = 0x4D454E44;
        private const uint IHDR_NAME = 0x49484452;
        private const uint IDAT_NAME = 0x49444154;
        private const uint IEND_NAME = 0x49454E44;

        internal uint curr_chunk_Name_Uint;

        private readonly MemoryStream _frameStream = new MemoryStream();
        private readonly byte[] _chunkHeaderBuffer = new byte[8];
        private readonly byte[] _nameBuffer = new byte[4];
        private readonly byte[] _crcBuffer = new byte[4];

        /// <summary>
        /// Current chunk len (in byte)
        /// </summary>
        internal uint curr_chunk_Len;

        /// <summary>
        /// Current chunk name (MHDR, IHDR or MEND)
        /// </summary>
        internal string curr_chunk_Name;

        /// <summary>
        /// Current chunk CRC
        /// </summary>
        internal uint curr_chunk_CRC;

        /// <summary>
        /// Current chunk
        /// </summary>
        internal byte[] curr_chunk_Data;

        /// <summary>
        /// Struct to store MHDR chunk information about the MNG file structure
        /// </summary>
        internal MHDR_Chunk_Struct MHDR_Chunk = new();

        //// <summary>
        //// Index of the MNGFile - Store position of every frame
        //// </summary>
        //internal List<int> MNGIndex = new List<int>(500);

        #region Properties
        /// <summary>
        /// Get the video framerate
        /// </summary>
        public double FrameRate
        {
            get { return MHDR_Chunk.Ticks_per_second; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Check file header and load MHDR chunk
        /// </summary>
        /// <param name="MNGFileStream"></param>
        public MNGStream(FileStream MNGFileStream)
        {
            _MNGStream = new BinaryReader(MNGFileStream, Encoding.Default);

            Span<byte> header = stackalloc byte[8];
            _MNGStream.BaseStream.ReadExactly(header);

            if (!header.SequenceEqual(MNGHeader))
                throw new Exception("Invalid file header");

            if (ReadChunk() != MNGChunkTypes.MHDR)
            {
                throw new Exception("MHDR chunk not found");
            }
            else
            {
                ParseMHDR();
            }
        }

        /// <summary>
        /// Returns the next frame position in the file without decode it and advace to next frame
        /// </summary>
        /// <returns></returns>
        public long GetNextFrameIndex()
        {
            long result = _MNGStream.BaseStream.Position;

            do
            {
                ReadChunk();
            }
            while (curr_chunk_Name_Uint != IEND_NAME && curr_chunk_Name_Uint != MEND_NAME);

            if (curr_chunk_Name_Uint == MEND_NAME)
            {
                result = -1;
            }

            return result;
        }

        /// <summary>
        /// Returns the next decoded frame and advance to next frame
        /// </summary>
        /// <returns></returns>
        public Bitmap GetNextFrame()
        {
            Bitmap result = null;
            _frameStream.SetLength(0);
            _frameStream.Write(PNGHeader);

            Span<byte> u32Buffer = stackalloc byte[4];

            do
            {
                if (ReadChunk() != MNGChunkTypes.MEND)
                {
                    BinaryPrimitives.WriteUInt32BigEndian(u32Buffer, curr_chunk_Len);
                    _frameStream.Write(u32Buffer);
                    _frameStream.Write(_chunkHeaderBuffer.AsSpan(4, 4));
                    _frameStream.Write(curr_chunk_Data);
                    BinaryPrimitives.WriteUInt32BigEndian(u32Buffer, curr_chunk_CRC);
                    _frameStream.Write(u32Buffer);
                }
            }
            while (curr_chunk_Name_Uint != IEND_NAME && curr_chunk_Name_Uint != MEND_NAME);

            if (curr_chunk_Name_Uint != MEND_NAME)
            {
                _frameStream.Position = 0;
                using Bitmap newImage = (Bitmap)Bitmap.FromStream(_frameStream, false, false);
                result = ConvertBitmapTo32bppArgb(newImage);
            }

            return result;
        }

        private Bitmap ConvertBitmapTo32bppArgb(Bitmap src)
        {
            int width = src.Width;
            int height = src.Height;
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            bool useFallback = true;
            PixelFormat srcFormat = src.PixelFormat;

            if (srcFormat == PixelFormat.Format32bppArgb || srcFormat == PixelFormat.Format24bppRgb || srcFormat == PixelFormat.Format32bppRgb || srcFormat == PixelFormat.Format8bppIndexed)
            {
                BitmapData srcData = src.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, srcFormat);
                BitmapData dstData = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                try
                {
                    unsafe
                    {
                        if (srcFormat == PixelFormat.Format32bppArgb || srcFormat == PixelFormat.Format32bppRgb)
                        {
                            Buffer.MemoryCopy((void*)srcData.Scan0, (void*)dstData.Scan0, (long)dstData.Stride * (long)height, (long)srcData.Stride * (long)height);
                            useFallback = false;
                        }
                        else if (srcFormat == PixelFormat.Format24bppRgb)
                        {
                            byte* sBase = (byte*)srcData.Scan0;
                            byte* dBase = (byte*)dstData.Scan0;
                            for (int y = 0; y < height; y++)
                            {
                                byte* s = sBase + (y * (long)srcData.Stride);
                                uint* d = (uint*)(dBase + (y * (long)dstData.Stride));
                                for (int x = 0; x < width; x++)
                                {
                                    *d = (uint)(s[0] | (s[1] << 8) | (s[2] << 16) | 0xFF000000);
                                    s += 3;
                                    d++;
                                }
                            }
                            useFallback = false;
                        }
                        else if (srcFormat == PixelFormat.Format8bppIndexed)
                        {
                            Color[] entries = src.Palette.Entries;
                            int palCount = entries.Length;
                            uint* pal = stackalloc uint[256];
                            for (int i = 0; i < palCount; i++) pal[i] = (uint)entries[i].ToArgb();

                            byte* sBase = (byte*)srcData.Scan0;
                            byte* dBase = (byte*)dstData.Scan0;
                            for (int y = 0; y < height; y++)
                            {
                                byte* s = sBase + (y * (long)srcData.Stride);
                                uint* d = (uint*)(dBase + (y * (long)dstData.Stride));
                                for (int x = 0; x < width; x++)
                                {
                                    *d = pal[*s];
                                    s++;
                                    d++;
                                }
                            }
                            useFallback = false;
                        }
                    }
                }
                finally
                {
                    src.UnlockBits(srcData);
                    result.UnlockBits(dstData);
                }
            }

            if (useFallback)
            {
                using Graphics grp = Graphics.FromImage(result);
                grp.CompositingMode = CompositingMode.SourceCopy;
                grp.CompositingQuality = CompositingQuality.HighSpeed;
                grp.InterpolationMode = InterpolationMode.NearestNeighbor;
                grp.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                grp.DrawImage(src, 0, 0);
            }

            return result;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _MNGStream?.Dispose();
                    _frameStream?.Dispose();
                }

                _disposed = true;
            }
        }

        #endregion

        #region Private Methods
        /// <summary>
        /// Read a complete chunk
        /// </summary>
        /// <returns>Chunk type</returns>
        internal MNGChunkTypes ReadChunk()
        {
            _MNGStream.BaseStream.ReadExactly(_chunkHeaderBuffer, 0, 8);
            curr_chunk_Len = BinaryPrimitives.ReadUInt32BigEndian(_chunkHeaderBuffer.AsSpan(0, 4));

            curr_chunk_Name_Uint = BinaryPrimitives.ReadUInt32BigEndian(_chunkHeaderBuffer.AsSpan(4, 4));
            
            // Map common names to strings to avoid allocations for known chunks
            curr_chunk_Name = curr_chunk_Name_Uint switch
            {
                MHDR_NAME => "MHDR",
                IHDR_NAME => "IHDR",
                IDAT_NAME => "IDAT",
                IEND_NAME => "IEND",
                MEND_NAME => "MEND",
                _ => Encoding.ASCII.GetString(_chunkHeaderBuffer, 4, 4)
            };

            curr_chunk_Data = _MNGStream.ReadBytes((int)curr_chunk_Len);

            _MNGStream.BaseStream.ReadExactly(_crcBuffer, 0, 4);
            curr_chunk_CRC = BinaryPrimitives.ReadUInt32BigEndian(_crcBuffer);

            return curr_chunk_Name_Uint switch
            {
                MHDR_NAME => MNGChunkTypes.MHDR,
                IHDR_NAME => MNGChunkTypes.IHDR,
                IDAT_NAME => MNGChunkTypes.IDAT,
                IEND_NAME => MNGChunkTypes.IEND,
                MEND_NAME => MNGChunkTypes.MEND,
                _ => MNGChunkTypes.Error,
            };
        }

        /// <summary>
        /// Parse the MHDR chunk and fill MHDR_Chunk_Struct MHDR_Chunk
        /// </summary>
        internal void ParseMHDR()
        {
            ReadOnlySpan<byte> data = curr_chunk_Data;
            MHDR_Chunk.Frame_width = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(0, 4));
            MHDR_Chunk.Frame_height = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
            MHDR_Chunk.Ticks_per_second = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8, 4));
            MHDR_Chunk.Nominal_layer_count = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(12, 4));
            MHDR_Chunk.Nominal_frame_coun = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(16, 4));
            MHDR_Chunk.Nominal_play_time = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20, 4));
            MHDR_Chunk.Simplicity_profile = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(24, 4));

            MHDR_Chunk.Simplicity_profile_Elements.Profile_Validity = MHDR_Chunk.Simplicity_profile & 1;
            MHDR_Chunk.Simplicity_profile_Elements.Simple_MNG_features = MHDR_Chunk.Simplicity_profile & 2;
            MHDR_Chunk.Simplicity_profile_Elements.Complex_MNG_features = MHDR_Chunk.Simplicity_profile & 4;
            MHDR_Chunk.Simplicity_profile_Elements.Internal_transparency = MHDR_Chunk.Simplicity_profile & 8;
            MHDR_Chunk.Simplicity_profile_Elements.JNG = MHDR_Chunk.Simplicity_profile & 16;
            MHDR_Chunk.Simplicity_profile_Elements.Delta_PNG = MHDR_Chunk.Simplicity_profile & 32;
            MHDR_Chunk.Simplicity_profile_Elements.Validity_flag_bits789 = MHDR_Chunk.Simplicity_profile & 64;
            MHDR_Chunk.Simplicity_profile_Elements.Background_transparency = MHDR_Chunk.Simplicity_profile & 128;
            MHDR_Chunk.Simplicity_profile_Elements.Semi_transparency = MHDR_Chunk.Simplicity_profile & 256;
            MHDR_Chunk.Simplicity_profile_Elements.Stored_objectbuffers = MHDR_Chunk.Simplicity_profile & 512;
        }
        #endregion


    }

    internal class Utils
    {
        public static bool ConfrontArrays<T>(T[] first, T[] second)
        {
            if (first.Length != second.Length)
            {
                return false;
            }
            else
            {
                for (int nI = 0; nI < first.Length; nI++)
                {
                    if (!first[nI].Equals(second[nI]))
                        return false;
                }
            }

            return true;
        }

        public static UInt32 ReadUInt32NBO(byte[] intBuffer)
        {
            return BinaryPrimitives.ReadUInt32BigEndian(intBuffer);
        }

        public static byte[] WriteUInt32NBO(uint intNumber)
        {
            byte[] buffer = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, intNumber);
            return buffer;
        }

        public static byte[] GetBytes(byte[] byteArray, int startFrom, int Len)
        {
            return byteArray[startFrom..(startFrom + Len)];
        }
    }

}

