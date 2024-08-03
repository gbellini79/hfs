using HumbleFrameServer.Lib;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace HumbleFrameServer.Base
{
    public class f_Resize : iAudioVideoStream
    {
        private iAudioVideoStream _input = null;
        private int _newWidth = -1;
        private int _newHeight = -1;
        private string _resizeMode = "resize";

        public string NodeName { get { return "Resize"; } }
        public string NodeDescription { get { return "Resize video"; } }
        public NodeType Type { get { return NodeType.Filter; } }

        private readonly Dictionary<string, NodeParameter> _Parameters = new() {
            {"input", new NodeParameter(){
                Type = NodeParameterType.AudioVideoStream,
                IsRequired = true,
                Value = null
            }},
            {"mode", new NodeParameter(){
                Type = NodeParameterType.String,
                IsRequired = false,
                Value = "resize",
                Description="resize|lanczos"
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
                if (originalFrame.Height != _newHeight || originalFrame.Width != _newWidth)
                {

                    Bitmap scaledFrame = new(_newWidth, _newHeight, originalFrame.PixelFormat);

                    //Resize
                    Graphics rescaler = Graphics.FromImage(scaledFrame);
                    rescaler.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    switch (_resizeMode)
                    {
                        case "resize":
                            rescaler.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                            break;
                        case "lanczos":
                            rescaler.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            break;
                        default:
                            throw new Exception("Resize: unknown resize mode \"" + _resizeMode + "\"");
                            break;
                    }

                    rescaler.DrawImage(originalFrame, 0, 0, _newWidth, _newHeight);

                    return new DataPacket(PacketType.Video, scaledFrame);
                }
                else
                {
                    return result;
                }
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
                throw new Exception("Cannot resize audio...");
            }
            else
            {
                _newWidth = (int)_Parameters["width"].Value;
                _newHeight = (int)_Parameters["height"].Value;
                _resizeMode = _Parameters["mode"].Value as string;
            }
        }

        public bool hasAudio { get { return _input.hasAudio; } }
        public bool hasVideo { get { return _input.hasVideo; } }
        public void closeStream() { _input.closeStream(); }
        public ushort BitsPerSample { get { return _input.BitsPerSample; } }
        public uint SamplesPerSecond { get { return _input.SamplesPerSecond; } }
        public ushort ChannelsCount { get { return _input.ChannelsCount; } }
        public decimal FPS
        {
            get
            {
                return _input.FPS;
            }
        }
        public uint Width { get { return Convert.ToUInt32(_newWidth); } }
        public uint Height { get { return Convert.ToUInt32(_newHeight); } }
    }
}