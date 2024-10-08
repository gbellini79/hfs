﻿using HFS.Lib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace HFS.Base
{
    public class f_Text : iAudioVideoStream
    {
        private iAudioVideoStream _bgvideo = null;
        private string _text = "";
        private Font _font = null;
        private int _firstframe = 0;
        private int _posx = 0;
        private int _posy = 0;
        private Color _color = Color.FromArgb(255, 0, 0, 0);

        private bool _istimer = false;
        private int _timerstartframe = 0;
        private int _timerstopframe = -1;
        private bool _showHH = true;

        public string NodeName { get { return "Text"; } }
        public string NodeDescription { get { return "Write text"; } }
        public NodeType Type { get { return NodeType.Filter; } }

        private readonly Dictionary<string, NodeParameter> _Parameters = new() {
            {"video", new NodeParameter(){
                Type = NodeParameterType.AudioVideoStream,
                IsRequired = true,
                Value = null,
                Description="Background video"
            }},
            {"text", new NodeParameter(){
                Type = NodeParameterType.String,
                IsRequired = true,
                Value = "",
                Description="Text to be written"
            }},
            {"font", new NodeParameter(){
                Type = NodeParameterType.String,
                IsRequired = false,
                Value = "Arial",
                Description="Font family (Arial)"
            }},
            {"size", new NodeParameter(){
                Type = NodeParameterType.Decimal,
                IsRequired = false,
                Value = 8,
                Description="Font size (8)"
            }},
            {"bold", new NodeParameter(){
                Type = NodeParameterType.Bool,
                IsRequired = false,
                Value = false,
                Description="Font is bold (false)"
            }},
            {"italic", new NodeParameter(){
                Type = NodeParameterType.Bool,
                IsRequired = false,
                Value = false,
                Description="Font is italic (false)"
            }},
            {"strikeout", new NodeParameter(){
                Type = NodeParameterType.Bool,
                IsRequired = false,
                Value = false,
                Description="Font is strikeouted (false)"
            }},
            {"underline", new NodeParameter(){
                Type = NodeParameterType.Bool,
                IsRequired = false,
                Value = false,
                Description="Font is underlined (false)"
            }},
            {"color", new NodeParameter(){
                Type = NodeParameterType.String,
                IsRequired = false,
                Value = "#000000ff",
                Description="Text color (#000000ff)"
            }},
            {"firstframe", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 0,
                Description="First frame of the bgvideo in which the text appears (0)"
            }},
            {"posx", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 0,
                Description="Text horizontal position (0)"
            }},
            {"posy", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 0,
                Description="Text vertical position (0)"
            }},
            {"istimer", new NodeParameter(){
                Type = NodeParameterType.Bool,
                IsRequired = false,
                Value = false,
                Description="Show a timer (false)"
            }},
            {"timerstartframe", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 0,
                Description="Time start at frame (0)"
            }},
            {"timerstopframe", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = -1,
                Description="Time stop at frame (-1)"
            }},
            {"showHH", new NodeParameter(){
                Type = NodeParameterType.Bool,
                IsRequired = false,
                Value = true,
                Description="Shows hours"
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

                if (_frameCount >= _firstframe)
                {
                    Bitmap resultBitmap = new(Convert.ToInt32(_bgvideo.Width), Convert.ToInt32(_bgvideo.Height), PixelFormat.Format32bppArgb);
                    Bitmap bgFrame = result.Data as Bitmap;

                    Graphics texter = Graphics.FromImage(resultBitmap);
                    texter.DrawImage(bgFrame, 0, 0);

                    if (_istimer)
                    {
                        if (_frameCount >= _timerstartframe && _frameCount <= _timerstopframe)
                        {
                            int currentMilli = Math.Max(0, Convert.ToInt32(Math.Floor(((_frameCount - this._timerstartframe) * 1000) / _bgvideo.FPS * 1.0m)));
                            TimeSpan timeSpan = new(0, 0, 0, 0, currentMilli);
                            _text = _showHH ? $"{timeSpan:hh\\:mm\\:ss\\.fff}" : $"{timeSpan:mm\\:ss\\.fff}";
                        }
                    }

                    texter.DrawString(_text, _font, new SolidBrush(_color), _posx, _posy);

                    return new DataPacket(PacketType.Video, resultBitmap);
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
            _bgvideo = _Parameters["video"].Value as iAudioVideoStream;
            _bgvideo.openStream();

            if (!_bgvideo.hasVideo)
            {
                throw new Exception("Text: cannot write on audio (yet)");
            }
            else
            {
                _text = (string)_Parameters["text"].Value;
                _posx = (int)_Parameters["posx"].Value;
                _posy = (int)_Parameters["posy"].Value;
                _firstframe = (int)_Parameters["firstframe"].Value;
                _color = ((string)_Parameters["color"].Value).ToRGBAColor();
                _istimer = (bool)_Parameters["istimer"].Value;
                _timerstartframe = (int)_Parameters["timerstartframe"].Value;
                _timerstopframe = (int)_Parameters["timerstopframe"].Value;
                _showHH = (bool)_Parameters["showHH"].Value;

                string fontFamily = (string)_Parameters["font"].Value;
                float fontSize = Convert.ToSingle(_Parameters["size"].Value);
                bool fontBold = (bool)_Parameters["bold"].Value;
                bool fontItalic = (bool)_Parameters["italic"].Value;
                bool fontStrikeout = (bool)_Parameters["strikeout"].Value;
                bool fontUnderline = (bool)_Parameters["underline"].Value;

                FontStyle newFS = FontStyle.Regular;
                if (fontBold) newFS |= FontStyle.Bold;
                if (fontItalic) newFS |= FontStyle.Italic;
                if (fontStrikeout) newFS |= FontStyle.Strikeout;
                if (fontUnderline) newFS |= FontStyle.Underline;

                if (FontFamily.Families.Count(x => x.Name == fontFamily) > 0)
                {
                    _font = new Font(new FontFamily(fontFamily), fontSize, newFS);
                }
                else
                {
                    throw new Exception(string.Format("Text: Unknown font family {0}", (string)_Parameters["font"].Value));
                }

                if (_istimer)
                {
                    _text = _showHH ? "00:00:00.000" : "00:00.000";
                }
            }
        }

        public bool hasAudio { get { return _bgvideo.hasAudio; } }
        public bool hasVideo { get { return _bgvideo.hasVideo; } }
        public void closeStream() { _bgvideo.closeStream(); }
        public ushort BitsPerSample { get { return _bgvideo.BitsPerSample; } }
        public uint SamplesPerSecond { get { return _bgvideo.SamplesPerSecond; } }
        public ushort ChannelsCount { get { return _bgvideo.ChannelsCount; } }
        public decimal FPS { get { return _bgvideo.FPS; } }
        public uint Width { get { return _bgvideo.Width; } }
        public uint Height { get { return _bgvideo.Height; } }
    }
}