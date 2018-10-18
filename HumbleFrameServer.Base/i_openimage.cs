using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using HumbleFrameServer.Lib;
using System.Reflection;

namespace HumbleFrameServer.Base
{
    public class i_OpenImage : iAudioVideoStream
    {
        public string NodeName { get { return "OpenImage"; } }
        public string NodeDescription { get { return "Loads an image and repeat it for the specified number of frames"; } }

        private int _framesLeft = -1;

        private Dictionary<string, NodeParameter> _Parameters = new Dictionary<string, NodeParameter>() { 
            {"path", new NodeParameter(){
                Type = NodeParameterType.String,
                IsRequired = true
            }},    
            {"frames", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 250,
                Description = "Duration in frames"
            }},
            {"fps", new NodeParameter(){
                Type = NodeParameterType.Decimal,
                IsRequired = false,
                Value = 25.0M
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

        public DataPacket getNextPacket()
        {
            if (_framesLeft > 0)
            {
                _framesLeft--;
                return _sourceImage;
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

        private DataPacket _sourceImage = null;
        public void openStream()
        {
            _framesLeft = (int)_Parameters["frames"].Value;
            _FPS = (decimal)_Parameters["fps"].Value;

            Bitmap loadedImage = (Bitmap)Bitmap.FromFile(_Parameters["path"].Value as string);
            loadedImage = loadedImage.Clone(new Rectangle(0, 0, loadedImage.Width, loadedImage.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            loadedImage.SetResolution(96, 96);
            _sourceImage = new DataPacket(PacketType.Video, loadedImage);

            _Width = Convert.ToUInt32(loadedImage.Width);
            _Height = Convert.ToUInt32(loadedImage.Height);
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
