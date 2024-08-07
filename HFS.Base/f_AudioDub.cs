using HFS.Lib;
using System;
using System.Collections.Generic;

namespace HFS.Base
{
    public class f_AudioDub : iAudioVideoStream
    {
        public string NodeName { get { return "AudioDub"; } }
        public string NodeDescription { get { return "Adds audio to a video stream"; } }

        private readonly Dictionary<string, NodeParameter> _Parameters = new() {
            {"video", new NodeParameter(){
                Type = NodeParameterType.AudioVideoStream,
                IsRequired = true
            }},
            {"audio", new NodeParameter(){
                Type = NodeParameterType.AudioVideoStream,
                IsRequired = true
            }}
        };
        public Dictionary<string, NodeParameter> Parameters { get { return _Parameters; } }

        private decimal _fps;
        public decimal FPS
        {
            get { return _fps; }
        }

        private uint _width;
        public uint Width
        {
            get { return _width; }
        }

        private uint _height;
        public uint Height
        {
            get { return _height; }
        }


        private bool EndOfAudioStream = false;
        private bool WriteVideo = false;
        private int _audioSamples = 0;
        private DataPacket tempPacket = null;
        public DataPacket getNextPacket()
        {
            if (!WriteVideo && !EndOfAudioStream && _audioSamples < _samplesPerFrame)
            {
                _audioSamples++;
                tempPacket = _audio.getNextPacket();
                EndOfAudioStream = tempPacket.Data == null;
            }
            else
            {
                WriteVideo = true;
            }

            if (EndOfAudioStream || WriteVideo)
            {
                _audioSamples = 0;
                WriteVideo = false;
                tempPacket = _video.getNextPacket();
            }

            return tempPacket;
        }


        private iAudioVideoStream _video;
        private iAudioVideoStream _audio;
        private decimal _samplesPerFrame;
        public void openStream()
        {
            _video = (iAudioVideoStream)_Parameters["video"].Value;
            _audio = (iAudioVideoStream)_Parameters["audio"].Value;

            if (_video != null && _audio != null)
            {
                _video.openStream();
                _audio.openStream();

                _fps = _video.FPS;
                _width = _video.Width;
                _height = _video.Height;

                _BitsPerSample = _audio.BitsPerSample;
                _SamplesPerSecond = _audio.SamplesPerSecond;
                _ChannelsCount = _audio.ChannelsCount;

                _samplesPerFrame = _audio.SamplesPerSecond / _video.FPS;
            }
            else
            {
                throw new Exception("Audiodub: video and audio parameters cannot be null");
            }
        }

        public void closeStream()
        {
            _video.closeStream();
            _audio.closeStream();
        }


        public NodeType Type
        {
            get { return NodeType.Filter; }
        }

        public bool hasAudio
        {
            get { return true; }
        }

        public bool hasVideo
        {
            get { return true; }
        }

        private ushort _BitsPerSample;
        public ushort BitsPerSample
        {
            get { return _BitsPerSample; }
        }

        private uint _SamplesPerSecond;
        public uint SamplesPerSecond
        {
            get { return _SamplesPerSecond; }
        }

        private ushort _ChannelsCount;
        public ushort ChannelsCount
        {
            get { return _ChannelsCount; }
        }
    }
}
