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
    public class f_Trim : iAudioVideoStream
    {
        private iAudioVideoStream _input = null;
        private int _firstframe = 25;
        private int _length = -1;

        public string NodeName { get { return "Trim"; } }
        public string NodeDescription { get { return "Trims video and/or audio"; } }
        public NodeType Type { get { return NodeType.Filter; } }

        private Dictionary<string, NodeParameter> _Parameters = new Dictionary<string, NodeParameter>() { 
            {"input", new NodeParameter(){
                Type = NodeParameterType.AudioVideoStream,
                IsRequired = true,
                Value = null
            }},
            {"firstframe", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = true,
                Value = 0,
                Description="Frames to be removed from the beginning of the stream"
            }},
            {"length", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = true,
                Value = -1,
                Description="Frames to be returned. -1 if return all"
            }}
        };

        public Dictionary<string, NodeParameter> Parameters { get { return _Parameters; } }

        private DataPacket result = null;
        public DataPacket getNextPacket()
        {
            while (_firstframe > 0)
            {
                result = _input.getNextPacket();
                if (_input.hasVideo)
                {
                    if (result.Type == PacketType.Video)
                    {
                        _firstframe--;
                    }
                }
                else
                {
                    _firstframe--;
                }
            }

            if (_length > 0)
            {
                result = _input.getNextPacket();
                if (_input.hasVideo)
                {
                    if (result.Type == PacketType.Video)
                    {
                        _length--;
                    }
                }
                else
                {
                    _length--;
                }
            }
            else
            {
                result = new DataPacket(PacketType.Video, null);
            }

            return result;
        }

        public void openStream()
        {
            _input = _Parameters["input"].Value as iAudioVideoStream;
            _input.openStream();
            _firstframe = (int)_Parameters["firstframe"].Value;
            _length = (int)_Parameters["length"].Value;

            //If input is audio only, frames are translated into packet
            if (!_input.hasVideo)
            {
                _firstframe = Convert.ToInt32((_firstframe / this.FPS) * this.SamplesPerSecond);
                if (_length != -1)
                    _length = Convert.ToInt32((_length / this.FPS) * this.SamplesPerSecond);
            }
            if (_length == -1)
                _length = int.MaxValue;
        }

        public bool hasAudio { get { return _input.hasAudio; } }
        public bool hasVideo { get { return _input.hasVideo; } }
        public void closeStream() { _input.closeStream(); }
        public ushort BitsPerSample { get { return _input.BitsPerSample; } }
        public uint SamplesPerSecond { get { return _input.SamplesPerSecond; } }
        public ushort ChannelsCount { get { return _input.ChannelsCount; } }
        public decimal FPS { get { return _input.FPS; } }
        public uint Width { get { return _input.Width; } }
        public uint Height { get { return _input.Height; } }
    }
}