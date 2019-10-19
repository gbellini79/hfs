using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HumbleFrameServer.Lib;
using MNGj;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace HumbleFrameServer
{
    public class OpenMNG : iAudioVideoStream
    {
        public string NodeName { get { return "OpenMNG"; } }
        public string NodeDescription { get { return "Adds support for MAME MNG video files"; } }

        private Dictionary<string, NodeParameter> _Parameters = new Dictionary<string, NodeParameter>() {
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

        bool _isFirstFrame = true;
        public DataPacket getNextPacket()
        {
            if (_isFirstFrame)
            {
                _isFirstFrame = false;
                return new DataPacket()
                {
                    Type = PacketType.Video,
                    Data = _firstFrame
                };
            }
            else
            {
                return new DataPacket()
                {
                    Type = PacketType.Video,
                    Data = _mngIn.GetNextFrame()
                };
            }
        }

        private FileStream _mngIndex = null;
        private MNGStream _mngIn = null;
        private Bitmap _firstFrame = null;
        private uint _width = 0;
        private uint _height = 0;

        private void IndexMNG(string path)
        {
            BinaryWriter indexOut = new BinaryWriter(File.Create(path + ".index"));
            MNGStream localMNG = new MNGj.MNGStream(File.OpenRead(path));

            int frameCounter = 0;
            long framePosition = localMNG.GetNextFrameIndex();
            while (framePosition > -1)
            {
                indexOut.Write(frameCounter);
                indexOut.Write(framePosition);

                framePosition = localMNG.GetNextFrameIndex();
                frameCounter++;
            }

            localMNG._MNGStream.Close();
            indexOut.Close();
        }

        public void openStream()
        {
            string path = _Parameters["path"].Value.ToString();
            _mngIn = new MNGj.MNGStream(File.OpenRead(path));

            _FPS = (decimal)_Parameters["fps"].Value;
            if (_FPS == 0M)
            {
                _FPS = Convert.ToDecimal(_mngIn.FrameRate);
            }

            _firstFrame = _mngIn.GetNextFrame();
            _width = Convert.ToUInt32(_firstFrame.Width);
            _height = Convert.ToUInt32(_firstFrame.Height);

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
                _mngIndex.Close();
            _mngIn._MNGStream.Close();
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
