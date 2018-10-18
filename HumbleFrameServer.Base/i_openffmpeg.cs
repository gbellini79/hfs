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
            while (frame_Data != null && frame_Data.audio_data == null && frame_Data.video_data == null)
            {
                frame_Data = _ffmpeg.GetNextFrame();
            }

            if (frame_Data != null)
            {
                if (frame_Data.is_video)
                {
                    result.Type = PacketType.Video;
                    result.Data = frame_Data.video_data;
                }
                else //frame data is audio
                {
                    result.Type = PacketType.Audio;
                    result.Data = frame_Data.audio_data;
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
