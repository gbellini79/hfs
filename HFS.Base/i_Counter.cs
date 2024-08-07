using HFS.Lib;
using System.Collections.Generic;
using System.Drawing;

namespace HFS.Base
{
    public class f_Counter : iAudioVideoStream
    {
        public string NodeName { get { return "Counter"; } }
        public string NodeDescription { get { return "Adds a frame counter in the top left corner"; } }

        private iAudioVideoStream _input = null;

        private readonly Dictionary<string, NodeParameter> _Parameters = new() {
            {"input", new NodeParameter(){
                Type = NodeParameterType.AudioVideoStream,
                IsRequired = true,
                Value = null
            }}
        };
        public Dictionary<string, NodeParameter> Parameters { get { return _Parameters; } }

        public decimal FPS
        {
            get { return _input.FPS; }
        }

        public uint Width
        {
            get { return _input.Width; }
        }

        public uint Height
        {
            get { return _input.Height; }
        }


        private int _framesCounter = 0;

        public DataPacket getNextPacket()
        {
            DataPacket newFrame = _input.getNextPacket();
            if (newFrame.Data == null || newFrame.Type == PacketType.Audio)
            {
                return newFrame;
            }
            else
            {
                Bitmap returnFrame = new(newFrame.Data as Bitmap);
                Graphics newGraphics = Graphics.FromImage(returnFrame);

                newGraphics.DrawString(string.Format("{0:#,#}", _framesCounter), new Font("Arial", 10), Brushes.White, new PointF(1, 4));

                _framesCounter++;

                return new DataPacket()
                {
                    Type = PacketType.Video,
                    Data = returnFrame
                };
            }
        }

        public void openStream()
        {
            _input = _Parameters["input"].Value as iAudioVideoStream;
            _input.openStream();
        }

        public void closeStream()
        {
            _input.closeStream();
        }


        public NodeType Type
        {
            get { return NodeType.Filter; }
        }

        public bool hasAudio
        {
            get { return _input.hasAudio; }
        }

        public bool hasVideo
        {
            get { return _input.hasVideo; }
        }

        public ushort BitsPerSample
        {
            get { return _input.BitsPerSample; }
        }

        public uint SamplesPerSecond
        {
            get { return _input.SamplesPerSecond; }
        }

        public ushort ChannelsCount
        {
            get { return _input.ChannelsCount; }
        }
    }
}
