using HumbleFrameServer.Lib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace HumbleFrameServer.Base
{
    public class i_Blank : iAudioVideoStream
    {
        public string NodeName { get { return "Blank"; } }
        public string NodeDescription { get { return "Returns a \"blank\" video"; } }

        private int _framesLeft = -1;
        private DataPacket _blankFrame = null;
        private int _width = 0;
        private int _height = 0;

        private readonly Dictionary<string, NodeParameter> _Parameters = new() {
            {"fps", new NodeParameter(){
                Type = NodeParameterType.Decimal,
                IsRequired = false,
                Value = 25.0M
            }},
            {"width", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 320
            }},
            {"height", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 240
            }},
            {"frames", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 150,
                Description = "Duration in frames"
            }},
            {"color", new NodeParameter(){
                Type = NodeParameterType.String,
                IsRequired = false,
                Value = "#000000ff",
                Description = "RGBa background color (#000000ff)"
            }}
        };


        public Dictionary<string, NodeParameter> Parameters { get { return _Parameters; } }

        public decimal FPS
        {
            get { return (decimal)_Parameters["fps"].Value; }
        }

        public uint Width
        {
            get { return Convert.ToUInt32(_width); }
        }

        public uint Height
        {
            get { return Convert.ToUInt32(_height); }
        }

        public DataPacket getNextPacket()
        {
            if (_framesLeft > 0)
            {
                _framesLeft--;
                return _blankFrame;
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


        public void openStream()
        {
            _width = (int)_Parameters["width"].Value;
            _height = (int)_Parameters["height"].Value;

            Color colorColor = (_Parameters["color"].Value as string).ToRGBAColor();

            Bitmap newFrame = new(_width, _height, PixelFormat.Format32bppArgb);
            Graphics gr = Graphics.FromImage(newFrame);
            gr.FillRectangle(new SolidBrush(colorColor), 0, 0, _width, _height);

            _blankFrame = new DataPacket(PacketType.Video, newFrame);

            _framesLeft = (int)_Parameters["frames"].Value;
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
