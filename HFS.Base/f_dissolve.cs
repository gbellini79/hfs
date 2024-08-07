using HFS.Lib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace HFS.Base
{
    public class f_Dissolve : iAudioVideoStream
    {
        public string NodeName { get { return "Dissolve"; } }
        public string NodeDescription { get { return "Dissolves two given video stream"; } }

        private iAudioVideoStream _from = null;
        private iAudioVideoStream _to = null;
        private int _frames = 0;
        private bool _squared = true;

        private readonly Dictionary<string, NodeParameter> _Parameters = new() {
            {"from", new NodeParameter(){
                Type = NodeParameterType.AudioVideoStream,
                IsRequired = true,
                Value = null
            }},
            {"to", new NodeParameter(){
                Type = NodeParameterType.AudioVideoStream,
                IsRequired = true,
                Value = null
            }},
            {"frames", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 25,
                Description = "Durantion of the transition in frames"
            }},
            {"squared", new NodeParameter(){
                Type = NodeParameterType.Bool,
                IsRequired = false,
                Value = true,
                Description = "Corrected squared color conversion (true)"
            }}
        };


        public Dictionary<string, NodeParameter> Parameters { get { return _Parameters; } }

        public decimal FPS
        {
            get { return _from.FPS; }
        }

        public uint Width
        {
            get { return _from.Width; }
        }

        public uint Height
        {
            get { return _from.Height; }
        }

        private object Dissolve(Bitmap bmpFrom, Bitmap bmpTo, decimal Percentage)
        {
            double dPercentace = Convert.ToDouble(Percentage);

            Bitmap bmpResult = new(bmpFrom.Width, bmpFrom.Height, bmpFrom.PixelFormat);

            BitmapData lockFrom = bmpFrom.LockBits(new Rectangle(0, 0, bmpFrom.Width, bmpFrom.Height), ImageLockMode.ReadOnly, bmpFrom.PixelFormat);
            byte[] rgbFrom = new byte[bmpFrom.Width * bmpFrom.Height * Image.GetPixelFormatSize(bmpFrom.PixelFormat) / 8];
            Marshal.Copy(lockFrom.Scan0, rgbFrom, 0, rgbFrom.Length);
            bmpFrom.UnlockBits(lockFrom);

            BitmapData lockTo = bmpTo.LockBits(new Rectangle(0, 0, bmpTo.Width, bmpTo.Height), ImageLockMode.ReadOnly, bmpTo.PixelFormat);
            byte[] rgbTo = new byte[bmpTo.Width * bmpTo.Height * Image.GetPixelFormatSize(bmpTo.PixelFormat) / 8];
            Marshal.Copy(lockTo.Scan0, rgbTo, 0, rgbTo.Length);
            bmpTo.UnlockBits(lockTo);

            BitmapData lockResult = bmpResult.LockBits(new Rectangle(0, 0, bmpResult.Width, bmpResult.Height), ImageLockMode.ReadWrite, bmpResult.PixelFormat);
            byte[] rgbResult = new byte[bmpResult.Width * bmpResult.Height * Image.GetPixelFormatSize(bmpResult.PixelFormat) / 8];
            Marshal.Copy(lockResult.Scan0, rgbResult, 0, rgbResult.Length);

            if (_squared)
            {
                Parallel.For(0, rgbResult.Length, new Action<int>(v =>
                {
                    rgbResult[v] = Convert.ToByte(Math.Round(Math.Sqrt(Math.Pow(rgbFrom[v], 2) + ((Math.Pow(rgbTo[v], 2) - Math.Pow(rgbFrom[v], 2)) * dPercentace)), 0));
                }));
            }
            else
            {
                Parallel.For(0, rgbResult.Length, new Action<int>(v =>
                {
                    rgbResult[v] = Convert.ToByte(Math.Round(rgbFrom[v] + ((rgbTo[v] - rgbFrom[v]) * Percentage), 0));
                }));
            }

            Marshal.Copy(rgbResult, 0, lockResult.Scan0, rgbResult.Length);
            bmpResult.UnlockBits(lockResult);

            return bmpResult;
        }

        private int[] Fade(int[] sampleFrom, int[] sampleTo, decimal Percentage)
        {
            Parallel.For(0, sampleFrom.Length, new Action<int>(b =>
            {
                sampleFrom[b] = Convert.ToInt32(Math.Round(sampleFrom[b] + ((sampleTo[b] - sampleFrom[b]) * Percentage), 0));
            }));

            return sampleFrom;
        }


        private DataPacket _result = null;
        private DataPacket _nextToPacket = null;
        private int _videoFramesBuffered = 0;
        private int _videoFramesDone = 0;
        private Queue<DataPacket> _fromBuffer;
        private Queue<DataPacket> _toBufferVideo;
        private Queue<DataPacket> _toBufferAudio;
        bool _fromEOF = false;
        int debugCounter = 0;
        public DataPacket getNextPacket()
        {
            while (!_fromEOF && _videoFramesBuffered <= _frames)
            {
                DataPacket newPacket = _from.getNextPacket();
                if (newPacket.Data != null)
                {
                    if (newPacket.Type == PacketType.Video)
                    {
                        _videoFramesBuffered++;
                        debugCounter++;
                    }
                    _fromBuffer.Enqueue(newPacket);
                }
                else
                {
                    _fromEOF = true;
                }
            }

            if (!_fromEOF)
            {
                _result = _fromBuffer.Dequeue();
                if (_result.Type == PacketType.Video)
                {
                    _videoFramesBuffered--;
                }
            }
            else if (_fromBuffer.Count > 0)
            {
                if (_fromBuffer.Peek().Type == PacketType.Audio)
                {
                    while (_toBufferAudio.Count == 0)
                    {
                        _nextToPacket = _to.getNextPacket();
                        if (_nextToPacket.Type == PacketType.Audio)
                        {
                            _toBufferAudio.Enqueue(_nextToPacket);
                        }
                        else
                        {
                            _toBufferVideo.Enqueue(_nextToPacket);
                        }
                    }

                    _result = new DataPacket(PacketType.Audio, Fade(_fromBuffer.Dequeue().Data as int[], _toBufferAudio.Dequeue().Data as int[], (Convert.ToDecimal(_videoFramesDone)) / (_frames)));
                }
                else
                {
                    while (_toBufferVideo.Count == 0)
                    {
                        _nextToPacket = _to.getNextPacket();
                        if (_nextToPacket.Type == PacketType.Audio)
                        {
                            _toBufferAudio.Enqueue(_nextToPacket);
                        }
                        else
                        {
                            _toBufferVideo.Enqueue(_nextToPacket);
                        }
                    }

                    _result = new DataPacket(PacketType.Video, Dissolve(_fromBuffer.Dequeue().Data as Bitmap, _toBufferVideo.Dequeue().Data as Bitmap, (Convert.ToDecimal(_videoFramesDone)) / (_frames)));
                    _videoFramesDone++;
                }
            }
            else
            {
                //Empties queues
                if (_toBufferAudio.Count > 0)
                    _result = _toBufferAudio.Dequeue();
                else if (_toBufferVideo.Count > 0)
                    _result = _toBufferVideo.Dequeue();
                else
                    _result = _to.getNextPacket();
            }

            return _result;

        }


        public void openStream()
        {
            _from = _Parameters["from"].Value as iAudioVideoStream;
            _to = _Parameters["to"].Value as iAudioVideoStream;
            _frames = (int)_Parameters["frames"].Value;
            _squared = (bool)_Parameters["squared"].Value;

            _fromBuffer = new Queue<DataPacket>(_frames);
            _toBufferAudio = new Queue<DataPacket>(_frames);
            _toBufferVideo = new Queue<DataPacket>(_frames);

            _from.openStream();
            _to.openStream();
            //TODO: dettagliare errori



            bool result = _from.Width == _to.Width && _from.Height == _to.Height && _from.FPS == _to.FPS &&
                _from.hasAudio == _to.hasAudio &&
                _from.BitsPerSample == _to.BitsPerSample &&
                _from.SamplesPerSecond == _to.SamplesPerSecond &&
                _from.ChannelsCount == _to.ChannelsCount;

            if (!result)
            {
                throw new Exception("Both streams must have the same audio and video properties.");
            }
        }

        public void closeStream()
        {
            _from.closeStream();
            _to.closeStream();
        }


        public NodeType Type
        {
            get { return NodeType.Filter; }
        }

        public bool hasAudio
        {
            get { return _from.hasAudio; }
        }

        public bool hasVideo
        {
            get { return _from.hasVideo; }
        }

        public ushort BitsPerSample
        {
            get { return _from.BitsPerSample; }
        }

        public uint SamplesPerSecond
        {
            get { return _from.SamplesPerSecond; }
        }

        public ushort ChannelsCount
        {
            get { return _from.ChannelsCount; }
        }
    }
}
