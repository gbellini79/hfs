﻿using HFS.Lib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace HFS.Base
{
    public class f_Overlay : iAudioVideoStream
    {
        private iAudioVideoStream _bgvideo = null;
        private iAudioVideoStream _fgvideo = null;
        private int _start = 0;
        private int _posx = 0;
        private int _posy = 0;
        private int _mixaudio = 0;

        public string NodeName { get { return "Overlay"; } }
        public string NodeDescription { get { return "Overlays two videos"; } }
        public NodeType Type { get { return NodeType.Filter; } }

        private readonly Dictionary<string, NodeParameter> _Parameters = new() {
            {"bgvideo", new NodeParameter(){
                Type = NodeParameterType.AudioVideoStream,
                IsRequired = true,
                Value = null,
                Description="Background video"
            }},
            {"fgvideo", new NodeParameter(){
                Type = NodeParameterType.AudioVideoStream,
                IsRequired = true,
                Value = null,
                Description="Foreground video"
            }},
            {"start", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 0,
                Description="First frame of the bgvideo in which the fgvideo appear"
            }},
            {"posx", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 0,
                Description="Foreground image horizontal position"
            }},
            {"posy", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 0,
                Description="Foreground image vertical position"
            }},
            {"mixaudio", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 0,
                Description="Mix audio from -100 (full background) to 100 (full foreground)"
            }}
        };

        public Dictionary<string, NodeParameter> Parameters { get { return _Parameters; } }

        private int _frameCount = -1;
        private DataPacket result = null;
        public DataPacket getNextPacket()
        {
            result = _bgvideo.getNextPacket();


            if (result.Data != null && result.Type == PacketType.Video)
            {
                _frameCount++;
                if (_frameCount >= _start)
                {
                    DataPacket fgData = _fgvideo.getNextPacket();
                    while (fgData.Data != null && fgData.Type != PacketType.Video)
                    {
                        fgData = _fgvideo.getNextPacket();
                    }

                    if (fgData.Data != null && fgData.Type == PacketType.Video)
                    {
                        Bitmap resultBitmap = new(Convert.ToInt32(_bgvideo.Width), Convert.ToInt32(_bgvideo.Height), PixelFormat.Format32bppArgb);
                        Bitmap bgFrame = result.Data as Bitmap;
                        Bitmap fgFrame = fgData.Data as Bitmap;

                        Graphics overlayer = Graphics.FromImage(resultBitmap);
                        overlayer.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        overlayer.DrawImage(bgFrame, 0, 0);
                        overlayer.DrawImage(fgFrame, _posx, _posy);

                        return new DataPacket(PacketType.Video, resultBitmap);
                    }
                    if (fgData.Data != null && fgData.Type == PacketType.Audio)
                    {
                        return result;
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
            else
            {
                return result;
            }
        }

        public void openStream()
        {
            _bgvideo = _Parameters["bgvideo"].Value as iAudioVideoStream;
            _fgvideo = _Parameters["fgvideo"].Value as iAudioVideoStream;

            _bgvideo.openStream();
            _fgvideo.openStream();

            if (!_bgvideo.hasVideo || !_fgvideo.hasVideo)
            {
                throw new Exception("Overlay: Both inputs must have video");
            }
            else if (_bgvideo.FPS != _fgvideo.FPS)
            {
                throw new Exception("Overlay: Inputs must have same FPS");
            }
            else if (_bgvideo.hasAudio && _fgvideo.hasAudio && !_bgvideo.IsSameAudio(_fgvideo))
            {
                throw new Exception("Overlay: Inputs must have same audio properties");
            }
            else
            {
                _posx = (int)_Parameters["posx"].Value;
                _posy = (int)_Parameters["posy"].Value;
                _start = (int)_Parameters["start"].Value;
                _mixaudio = (int)_Parameters["mixaudio"].Value;
            }
        }

        public bool hasAudio { get { return _bgvideo.hasAudio; } }
        public bool hasVideo { get { return _bgvideo.hasVideo; } }
        public void closeStream() { _bgvideo.closeStream(); _fgvideo.closeStream(); }
        public ushort BitsPerSample { get { return _bgvideo.BitsPerSample; } }
        public uint SamplesPerSecond { get { return _bgvideo.SamplesPerSecond; } }
        public ushort ChannelsCount { get { return _bgvideo.ChannelsCount; } }
        public decimal FPS { get { return _bgvideo.FPS; } }
        public uint Width { get { return _bgvideo.Width; } }
        public uint Height { get { return _bgvideo.Height; } }
    }
}