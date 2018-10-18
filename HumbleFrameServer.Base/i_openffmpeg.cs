using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using HumbleFrameServer.Lib;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Drawing.Imaging;
using FFMPEG_Cli;
using System.Runtime.InteropServices;

namespace HumbleFrameServer.Base
{
    public class i_OpenFFMPEG : iAudioVideoStream
    {
        public string NodeName { get { return "OpenFFMPEG"; } }
        public string NodeDescription { get { return "Loads a video using FFMPEG"; } }

        //ffmpeg -i a.mp4 -f rawvideo -c:v png -


        private int _framesLeft = 0;
        private string _path = "";

        private Dictionary<string, NodeParameter> _Parameters = new Dictionary<string, NodeParameter>() {
            {"path", new NodeParameter(){
                Type = NodeParameterType.String,
                IsRequired = true
            }},
            {"frames", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = -1,
                Description = "Duration in frames (0 = full video)"
            }},
            {"fps", new NodeParameter(){
                Type = NodeParameterType.Decimal,
                IsRequired = false,
                Value = 25.0M,
                Description = "FPS (25)"
            }}
        };


        public Dictionary<string, NodeParameter> Parameters { get { return _Parameters; } }

        private decimal _FPS;
        public decimal FPS
        {
            get { return _FPS; }
        }

        private uint _Width;
        public uint Width
        {
            get { return _Width; }
        }

        private uint _Height;
        public uint Height
        {
            get { return _Height; }
        }

        private FFMPEGWrapper _ffmpeg = null;
        private video_info _video_Info = null;

        private Queue<byte> _buffer = new Queue<byte>(8);

        public DataPacket getNextPacket()
        {
            DataPacket result = new DataPacket() { Data = null };

            frame_data frame_Data = _ffmpeg.GetNextFrame();
            while (frame_Data != null && frame_Data.data == null)
            {
                frame_Data = _ffmpeg.GetNextFrame();
            }

            if (frame_Data != null)
            {
                if (frame_Data.is_video)
                {
                    result.Type = PacketType.Video;
                    result.Data = new Bitmap((int)_Width, (int)_Height);
                    BitmapData bitmapData = ((Bitmap)(result.Data)).LockBits(new Rectangle(0, 0, (int)_Width, (int)_Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                    Marshal.Copy(frame_Data.data, 0, bitmapData.Scan0, frame_Data.data.Length);
                    ((Bitmap)(result.Data)).UnlockBits(bitmapData);
                }
                else //frame data is audio
                {
                    result.Type = PacketType.Audio;
                    int[] tempData = new int[frame_Data.data.Length / 2];

                    Parallel.For(0, frame_Data.data.Length / 2, i =>
                    {
                        int b = i * 2;
                        tempData[i] = BitConverter.ToInt16(new byte[] { frame_Data.data[b], frame_Data.data[b + 1] }, 0);
                    });

                    result.Data = tempData;
                }
            }
            else
            {
                return new DataPacket()
                {
                    Type = PacketType.Video,
                    Data = null
                };
            }

            return result;


            //if (_framesLeft != 0)
            //{
            //    if (_isFirstFrame)
            //    {
            //        _isFirstFrame = false;
            //        return _firstFrame;
            //    }
            //    else
            //    {
            //        bool frameComplete = false;
            //        MemoryStream newFrame = new MemoryStream();

            //        while (!frameComplete)
            //        {
            //            //if (!_ffmpeg.StandardOutput.EndOfStream)
            //            int b = _ffmpeg.StandardOutput.BaseStream.ReadByte();
            //            if (b > -1)
            //            {
            //                _buffer.Enqueue(Convert.ToByte(b));
            //                if (_buffer.Count == 8)
            //                {
            //                    if (isEndOfPNG(_buffer.ToArray()))
            //                    {
            //                        newFrame.Write(_buffer.ToArray(), 0, 8);
            //                        frameComplete = true;
            //                        _buffer.Clear();
            //                    }
            //                    else
            //                    {
            //                        newFrame.WriteByte(_buffer.Dequeue());
            //                    }
            //                }
            //            }
            //            else
            //            {
            //                break;
            //            }
            //        }

            //        if (frameComplete)
            //        {
            //            _framesLeft--;

            //            Image sourceFrame = Bitmap.FromStream(newFrame);
            //            if (sourceFrame.PixelFormat != PixelFormat.Format32bppArgb)
            //            {
            //                Bitmap outputFrame = new Bitmap(sourceFrame.Width, sourceFrame.Height, PixelFormat.Format32bppArgb);
            //                Graphics outputGraphics = Graphics.FromImage(outputFrame);
            //                outputGraphics.DrawImage(sourceFrame, 0, 0);

            //                return new DataPacket()
            //                {
            //                    Type = PacketType.Video,
            //                    Data = outputFrame
            //                };
            //            }
            //            else
            //            {
            //                return new DataPacket()
            //                {
            //                    Type = PacketType.Video,
            //                    Data = (Bitmap)sourceFrame
            //                };
            //            }


            //        }
            //        else
            //        {
            //            return new DataPacket()
            //            {
            //                Type = PacketType.Video,
            //                Data = null
            //            };
            //        }
            //    }
            //}
            //else
            //{
            //    return new DataPacket()
            //    {
            //        Type = PacketType.Video,
            //        Data = null
            //    };
            //}
        }



        public void openStream()
        {
            _framesLeft = (int)_Parameters["frames"].Value;
            if (_framesLeft == 0) _framesLeft = -1;
            //_FPS = (decimal)_Parameters["fps"].Value;
            _path = (string)_Parameters["path"].Value;

            _ffmpeg = new FFMPEGWrapper();
            _video_Info = _ffmpeg.OpenVideo((string)_Parameters["path"].Value);

            if (_video_Info.errorcode >= 0)
            {
                _Width = _video_Info.width;
                _Height = _video_Info.height;
                _FPS = Convert.ToDecimal(_video_Info.fps);
            }
            else
            {
                throw new Exception("OpenFFMPEG: cannot open input file");
            }
        }

        public void closeStream()
        {
            _ffmpeg.Close();
        }


        public NodeType Type
        {
            get { return NodeType.Input; }
        }

        public bool hasAudio
        {
            get
            {
                return _video_Info.has_audio;
            }
        }

        public bool hasVideo
        {
            get { return _video_Info.has_video; }
        }

        public ushort BitsPerSample
        {
            get { return 16; } // _video_Info.nb_sample; }
        }

        public uint SamplesPerSecond
        {
            get { return _video_Info.sample_rate; }
        }

        public ushort ChannelsCount
        {
            get { return _video_Info.channels; }
        }
    }
}
