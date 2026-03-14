using HFS.Lib;
using MNGj;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace HFS
{
    public class OpenMNG : iAudioVideoStream
    {
        public string NodeName { get { return "OpenMNG"; } }
        public string NodeDescription { get { return "Adds support for MAME MNG video files"; } }

        private readonly Dictionary<string, NodeParameter> _Parameters = new() {
            {"path", new NodeParameter(){
                Type = NodeParameterType.String,
                IsRequired = true
            }},
            {"fps", new NodeParameter(){
                Type = NodeParameterType.Decimal,
                IsRequired = false,
                Value = 0M
            }},
        };

        public Dictionary<string, NodeParameter> Parameters { get { return _Parameters; } }

        private decimal _FPS = 0M;
        public decimal FPS
        {
            get { return _FPS; }
        }

        public uint Width
        {
            get { return _width; }
        }

        public uint Height
        {
            get { return _height; }
        }

        public DataPacket getNextPacket()
        {
            DataPacket packet = new DataPacket()
            {
                Type = PacketType.Video,
                Data = _mngIn.GetNextFrame()
            };
            return packet;
        }

        private FileStream _mngIndex = null;
        private MNGStream _mngIn = null;
        private uint _width = 0;
        private uint _height = 0;

        private void IndexMNG(string path)
        {
            using FileStream fs = File.OpenRead(path);
            using MNGStream localMNG = new(fs);
            using FileStream os = File.Create(path + ".index");
            using BinaryWriter indexOut = new(os);
            int frameCounter = 0;
            long framePosition = localMNG.GetNextFrameIndex();
            while (framePosition > -1)
            {
                indexOut.Write(frameCounter);
                indexOut.Write(framePosition);

                framePosition = localMNG.GetNextFrameIndex();
                frameCounter++;
            }
        }

        public void openStream()
        {
            string path = _Parameters["path"].Value.ToString();
            FileStream fs = File.OpenRead(path);
            _mngIn = new MNGj.MNGStream(fs);

            _FPS = (decimal)_Parameters["fps"].Value;
            if (_FPS == 0M)
            {
                _FPS = Convert.ToDecimal(_mngIn.FrameRate);
            }

            _width = _mngIn.MHDR_Chunk.Frame_width;
            _height = _mngIn.MHDR_Chunk.Frame_height;

            //Indexing
            //if (File.Exists(path + ".index"))
            //{
            //    _mngIndex = File.OpenRead(path + ".index");
            //}
            //else
            //{
            //    if (DialogResult.Yes == MessageBox.Show(string.Format("\"{0}\" should be indexed. Do you want to do it now?", path), "OpenMNG - Indexing", MessageBoxButtons.YesNo, MessageBoxIcon.Question))
            //    {
            //        IndexMNG(path);
            //        _mngIndex = File.OpenRead(path + ".index");
            //    }
            //}
        }

        public void closeStream()
        {
            if (_mngIndex != null)
            {
                _mngIndex.Close();
                _mngIndex = null;
            }

            if (_mngIn != null)
            {
                _mngIn.Dispose();
                _mngIn = null;
            }
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
