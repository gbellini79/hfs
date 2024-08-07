using System;
using System.Drawing;
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

    internal class MNGStream
    {
        internal byte[] PNGHeader = new byte[8] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        internal byte[] MNGHeader = new byte[8] { 0x8a, 0x4d, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };

        /// <summary>
        /// Internal binary stream
        /// </summary>
        internal BinaryReader _MNGStream;

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

            if (!Utils.ConfrontArrays<byte>(_MNGStream.ReadBytes(8), MNGHeader))
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
            long result = -1;

            result = _MNGStream.BaseStream.Position;
            do
            {
                ReadChunk();
            }
            while (curr_chunk_Name != "IEND" && curr_chunk_Name != "MEND");

            if (curr_chunk_Name != "MEND")
            {

            }
            else
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
            MemoryStream newImageStream = new();
            BinaryWriter newImageStreamW = new(newImageStream);

            newImageStreamW.Write(PNGHeader, 0, PNGHeader.Length);

            do
            {
                if (ReadChunk() != MNGChunkTypes.MEND)
                {
                    newImageStreamW.Write(Utils.WriteUInt32NBO(curr_chunk_Len));
                    newImageStreamW.Write(curr_chunk_Name[0]);
                    newImageStreamW.Write(curr_chunk_Name[1]);
                    newImageStreamW.Write(curr_chunk_Name[2]);
                    newImageStreamW.Write(curr_chunk_Name[3]);
                    newImageStreamW.Write(curr_chunk_Data);
                    newImageStreamW.Write(Utils.WriteUInt32NBO(curr_chunk_CRC));
                }
            }
            while (curr_chunk_Name != "IEND" && curr_chunk_Name != "MEND");

            if (curr_chunk_Name != "MEND")
            {
                Bitmap newImage = (Bitmap)Bitmap.FromStream(newImageStream);
                //Debug.Write(newImage.PixelFormat.ToString() + " - ");

                /*
                BitmapData dataSource = newImage.LockBits(new Rectangle(0, 0, newImage.Width, newImage.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                byte[] rgbSource = new byte[newImage.Width * newImage.Height * 3];
                Marshal.Copy(dataSource.Scan0, rgbSource, 0, rgbSource.Length);
                newImage.UnlockBits(dataSource);

                Bitmap newImage32 = new Bitmap(newImage.Width, newImage.Height, PixelFormat.Format24bppRgb);
                BitmapData dataTarget = newImage32.LockBits(new Rectangle(0, 0, newImage.Width, newImage.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                Marshal.Copy(rgbSource, 0, dataTarget.Scan0, rgbSource.Length);
                newImage32.UnlockBits(dataTarget);
                 */

                Bitmap newImage32 = new(newImage.Width, newImage.Height, PixelFormat.Format32bppArgb);
                Graphics grp = Graphics.FromImage(newImage32);
                grp.DrawImage(newImage, 0, 0);

                //Debug.WriteLine(newImage32.PixelFormat.ToString());
#if DEBUG
                // Graphics debugIMGWrite = Graphics.FromImage(newImage32);
                // debugIMGWrite.DrawString(newImage.PixelFormat.ToString(), new Font("Arial", 10), Brushes.White, new PointF(1, 4));
#endif

                return newImage32;
            }
            else
                return null;
        }

        #endregion

        #region Private Methods
        /// <summary>
        /// Read a complete chunk
        /// </summary>
        /// <returns>Chunk type</returns>
        internal MNGChunkTypes ReadChunk()
        {
            curr_chunk_Len = Utils.ReadUInt32NBO(_MNGStream.ReadBytes(4));
            curr_chunk_Name = new string(_MNGStream.ReadChars(4));
            curr_chunk_Data = _MNGStream.ReadBytes((int)curr_chunk_Len);
            curr_chunk_CRC = Utils.ReadUInt32NBO(_MNGStream.ReadBytes(4));

            return curr_chunk_Name switch
            {
                "MHDR" => MNGChunkTypes.MHDR,
                "IHDR" => MNGChunkTypes.IHDR,
                "IDAT" => MNGChunkTypes.IDAT,
                "IEND" => MNGChunkTypes.IEND,
                "MEND" => MNGChunkTypes.MEND,
                _ => MNGChunkTypes.Error,
            };
        }

        /// <summary>
        /// Parse the MHDR chunk and fill MHDR_Chunk_Struct MHDR_Chunk
        /// </summary>
        internal void ParseMHDR()
        {
            MHDR_Chunk.Frame_width = Utils.ReadUInt32NBO(Utils.GetBytes(curr_chunk_Data, 0, 4));
            MHDR_Chunk.Frame_height = Utils.ReadUInt32NBO(Utils.GetBytes(curr_chunk_Data, 4, 4));
            MHDR_Chunk.Ticks_per_second = Utils.ReadUInt32NBO(Utils.GetBytes(curr_chunk_Data, 8, 4));
            MHDR_Chunk.Nominal_layer_count = Utils.ReadUInt32NBO(Utils.GetBytes(curr_chunk_Data, 12, 4));
            MHDR_Chunk.Nominal_frame_coun = Utils.ReadUInt32NBO(Utils.GetBytes(curr_chunk_Data, 16, 4));
            MHDR_Chunk.Nominal_play_time = Utils.ReadUInt32NBO(Utils.GetBytes(curr_chunk_Data, 20, 4));
            MHDR_Chunk.Simplicity_profile = Utils.ReadUInt32NBO(Utils.GetBytes(curr_chunk_Data, 24, 4));

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
            if (intBuffer.Length == 4)
            {
                //return intBuffer[3] + (intBuffer[2] * 256) + (intBuffer[1] * 4096) + (intBuffer[0] * 65536);

                return uint.Parse(string.Format("{0,2:x}{1,2:x}{2,2:x}{3,2:x}", intBuffer[0], intBuffer[1], intBuffer[2], intBuffer[3]).Replace(' ', '0'), System.Globalization.NumberStyles.HexNumber);
            }
            else
            {
                throw new Exception("Invalid buffer lenght");
            }
        }

        public static byte[] WriteUInt32NBO(uint intNumber)
        {
            byte[] newArray = new byte[4];

            string newHex = string.Format("{0,8:x}", intNumber).Replace(' ', '0');

            newArray[0] = byte.Parse(newHex[..2], System.Globalization.NumberStyles.HexNumber);
            newArray[1] = byte.Parse(newHex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            newArray[2] = byte.Parse(newHex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            newArray[3] = byte.Parse(newHex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);

            return newArray;
        }

        public static byte[] GetBytes(byte[] byteArray, int startFrom, int Len)
        {
            byte[] newArray = new byte[Len];
            for (int nI = 0; nI < Len; nI++)
                newArray[nI] = byteArray[nI + startFrom];

            return newArray;
        }
    }

}

