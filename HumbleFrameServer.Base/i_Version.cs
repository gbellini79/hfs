using HumbleFrameServer.Lib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace HumbleFrameServer.Base
{
    public class i_Version : iAudioVideoStream
    {
        public string NodeName { get { return "Version"; } }
        public string NodeDescription { get { return "Returns a 400x100 video showing current version"; } }

        private int _framesLeft = -1;

        private readonly Dictionary<string, NodeParameter> _Parameters = [];
        public Dictionary<string, NodeParameter> Parameters { get { return _Parameters; } }

        public decimal FPS
        {
            get { return 25M; }
        }

        private readonly int _width = 400;
        public uint Width
        {
            get { return Convert.ToUInt32(_width); }
        }

        private readonly int _height = 100;
        public uint Height
        {
            get { return Convert.ToUInt32(_height); }
        }


        private int _framesCounter = 0;

        public void Seek(int frame)
        {
            throw new NotImplementedException();
        }

        public DataPacket getNextPacket()
        {
            if (_framesLeft > 0)
            {
                _framesLeft--;
                _framesCounter++;

                Bitmap _returnFrame = new(_width, _height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                Graphics newGraphics = Graphics.FromImage(_returnFrame);

                newGraphics.DrawString(_versionString + "\r\nFrame number: " + _framesCounter, new Font("Arial", 10), Brushes.White, new PointF(1, 4));

                return new DataPacket()
                {
                    Type = PacketType.Video,
                    Data = _returnFrame
                };
            }
            else
            {
                return new DataPacket()
                {
                    Type = PacketType.Video,
                    Data = null
                };
            }
        }

        private string _versionString;
        public void openStream()
        {
            //10 seconds video
            _framesLeft = Convert.ToInt32(this.FPS * 10);

            Assembly current = Assembly.GetExecutingAssembly();
            _versionString = string.Format("HumbleFrameServer v{0} {1}",
                        current.GetName().Version.ToString(3),
                        current.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
        }

        public void closeStream()
        {
            //Do nothing
        }


        public NodeType Type
        {
            get { return NodeType.Input; }
        }

        public bool hasAudio
        {
            get { return false; }
        }

        public bool hasVideo
        {
            get { return true; }
        }

        public bool canSeek { get { return false; } }

        public ushort BitsPerSample
        {
            get { return 0; }
        }

        public uint SamplesPerSecond
        {
            get { return 0; }
        }

        public ushort ChannelsCount
        {
            get { return 0; }
        }
    }
}
