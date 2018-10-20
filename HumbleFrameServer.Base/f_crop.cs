using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using HumbleFrameServer.Lib;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HumbleFrameServer.Base
{
    public class f_crop : iAudioVideoStream
    {
        private iAudioVideoStream _input = null;
        private int _newWidth = -1;
        private int _newHeight = -1;
        private int _left = -1;
        private int _top = -1;

        public string NodeName { get { return "Crop"; } }
        public string NodeDescription { get { return "Crop video"; } }
        public NodeType Type { get { return NodeType.Filter; } }

        private Dictionary<string, NodeParameter> _Parameters = new Dictionary<string, NodeParameter>() {
            {"input", new NodeParameter(){
                Type = NodeParameterType.AudioVideoStream,
                IsRequired = true,
                Value = null
            }},
            {"left", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = true,
                Value = -1,
                Description="Left margin"
            }},
            {"top", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = true,
                Value = -1,
                Description="Top margin"
            }},
            {"width", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = true,
                Value = -1,
                Description="New width"
            }},
            {"height", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = true,
                Value = -1,
                Description="New height"
            }}
        };

        public Dictionary<string, NodeParameter> Parameters { get { return _Parameters; } }

        private DataPacket result = null;
        public DataPacket getNextPacket()
        {
            result = _input.getNextPacket();
            if (result.Data != null && result.Type == PacketType.Video)
            {
                Bitmap originalFrame = result.Data as Bitmap;
                Bitmap croppedFrame = new Bitmap(_newWidth, _newHeight, originalFrame.PixelFormat);

                //Crop
                Graphics cropper = Graphics.FromImage(croppedFrame);
                cropper.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                cropper.DrawImage(originalFrame, 0, 0, new Rectangle(_left, _top, _newWidth, _newHeight), GraphicsUnit.Pixel);

                return new DataPacket(PacketType.Video, croppedFrame);
            }
            else
            {
                return result;
            }
        }

        public void openStream()
        {
            _input = _Parameters["input"].Value as iAudioVideoStream;
            _input.openStream();
            if (!_input.hasVideo)
            {
                throw new Exception("Cannot crop audio...");
            }
            else
            {
                _newWidth = (int)_Parameters["width"].Value;
                _newHeight = (int)_Parameters["height"].Value;
                _left = (int)_Parameters["left"].Value;
                _top = (int)_Parameters["top"].Value;
            }
        }

        public bool hasAudio { get { return _input.hasAudio; } }
        public bool hasVideo { get { return _input.hasVideo; } }
        public void closeStream() { _input.closeStream(); }
        public ushort BitsPerSample { get { return _input.BitsPerSample; } }
        public uint SamplesPerSecond { get { return _input.SamplesPerSecond; } }
        public ushort ChannelsCount { get { return _input.ChannelsCount; } }
        public decimal FPS { get { return _input.FPS; } }
        public uint Width { get { return Convert.ToUInt32(_newWidth); } }
        public uint Height { get { return Convert.ToUInt32(_newHeight); } }
    }
}