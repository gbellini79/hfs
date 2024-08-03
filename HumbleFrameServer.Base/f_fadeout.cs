using HumbleFrameServer.Lib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace HumbleFrameServer.Base
{
    public class f_FadeOut : iAudioVideoStream
    {
        public string NodeName { get { return "FadeOut"; } }
        public string NodeDescription { get { return "FadeOut"; } }

        private iAudioVideoStream _from = null;
        private int _frames = 0;
        private bool _squared = true;

        private readonly Dictionary<string, NodeParameter> _Parameters = new() {
            {"from", new NodeParameter(){
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
            {"color", new NodeParameter(){
                Type = NodeParameterType.String,
                IsRequired = false,
                Value = "#000000ff",
                Description = "RGBa Color of the fade (#000000ff)"
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

        private object Dissolve(Bitmap bmpFrom, decimal Percentage)
        {
            double dPercentage = Convert.ToDouble(Percentage);

            Bitmap bmpResult = new(bmpFrom.Width, bmpFrom.Height, bmpFrom.PixelFormat);

            BitmapData lockFrom = bmpFrom.LockBits(new Rectangle(0, 0, bmpResult.Width, bmpResult.Height), ImageLockMode.ReadOnly, bmpResult.PixelFormat);
            byte[] rgbFrom = new byte[bmpResult.Width * bmpResult.Height * Image.GetPixelFormatSize(bmpResult.PixelFormat) / 8];
            Marshal.Copy(lockFrom.Scan0, rgbFrom, 0, rgbFrom.Length);
            bmpFrom.UnlockBits(lockFrom);

            BitmapData lockResult = bmpResult.LockBits(new Rectangle(0, 0, bmpResult.Width, bmpResult.Height), ImageLockMode.ReadWrite, bmpResult.PixelFormat);
            byte[] rgbResult = new byte[bmpResult.Width * bmpResult.Height * Image.GetPixelFormatSize(bmpResult.PixelFormat) / 8];
            Marshal.Copy(lockResult.Scan0, rgbResult, 0, rgbResult.Length);

            if (_squared)
            {
                Parallel.For(0, rgbResult.Length, new Action<int>(v =>
                {
                    rgbResult[v] = Convert.ToByte(Math.Round(Math.Sqrt(Math.Pow(rgbFrom[v], 2) + ((Math.Pow(_rgbTo[v], 2) - Math.Pow(rgbFrom[v], 2)) * dPercentage)), 0));
                }));
            }
            else
            {
                Parallel.For(0, rgbResult.Length, new Action<int>(v =>
                {
                    rgbResult[v] = Convert.ToByte(Math.Round(rgbFrom[v] + ((_rgbTo[v] - rgbFrom[v]) * Percentage), 0));
                }));
            }

            Marshal.Copy(rgbResult, 0, lockResult.Scan0, rgbResult.Length);
            bmpResult.UnlockBits(lockResult);

            return bmpResult;
        }

        private int[] Fade(int[] sampleFrom, decimal Percentage)
        {
            Parallel.For(0, sampleFrom.Length, new Action<int>(b =>
            {
                sampleFrom[b] = Convert.ToInt32(Math.Round(sampleFrom[b] + ((_baseSilence - sampleFrom[b]) * Percentage), 0));
            }));

            return sampleFrom;
        }


        private DataPacket result;
        private int _videoFramesDone = 0;
        private int _videoFrameBuffered = 0;
        private Queue<DataPacket> _fromBuffer;
        private bool _fromEOF = false;
        public DataPacket getNextPacket()
        {
            while (!_fromEOF && _videoFrameBuffered <= _frames)
            {
                DataPacket newPacket = _from.getNextPacket();
                if (newPacket.Data != null)
                {
                    if (newPacket.Type == PacketType.Video)
                    {
                        _videoFrameBuffered++;
                    }
                    _fromBuffer.Enqueue(newPacket);
                }
                else
                {
                    _fromEOF = true;
                }
            }

            if (!_fromEOF && _videoFrameBuffered > _frames)
            {
                result = _fromBuffer.Dequeue();
                if (result.Type == PacketType.Video)
                {
                    _videoFrameBuffered--;
                }
            }
            else if (_videoFramesDone < _frames)
            {
                switch (_fromBuffer.Peek().Type)
                {
                    case PacketType.Video:
                        _videoFramesDone++;
                        result = new DataPacket(PacketType.Video, Dissolve(_fromBuffer.Dequeue().Data as Bitmap, (Convert.ToDecimal(_videoFramesDone)) / (_frames)));
                        break;
                    case PacketType.Audio:
                        result = new DataPacket(PacketType.Audio, Fade(_fromBuffer.Dequeue().Data as int[], (Convert.ToDecimal(_videoFramesDone)) / (_frames)));
                        break;
                }
            }
            else
            {
                result = new DataPacket(PacketType.Video, null);
            }

            return result;
        }


        private Bitmap _baseImg;
        private byte[] _rgbTo;
        private int _baseSilence;
        public void openStream()
        {
            _from = _Parameters["from"].Value as iAudioVideoStream;
            _from.openStream();

            _frames = (int)_Parameters["frames"].Value;
            _squared = (bool)_Parameters["squared"].Value;

            _fromBuffer = new Queue<DataPacket>(_frames);

            Color colorColor = (_Parameters["color"].Value as string).ToRGBAColor();

            _baseImg = new Bitmap(Convert.ToInt32(_from.Width), Convert.ToInt32(_from.Height), PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(_baseImg);
            Brush colorBrush = new SolidBrush(colorColor);
            g.FillRectangle(colorBrush, new Rectangle(0, 0, _baseImg.Width, _baseImg.Height));

            BitmapData lockTo = _baseImg.LockBits(new Rectangle(0, 0, _baseImg.Width, _baseImg.Height), ImageLockMode.ReadOnly, _baseImg.PixelFormat);
            _rgbTo = new byte[_baseImg.Width * _baseImg.Height * Image.GetPixelFormatSize(_baseImg.PixelFormat) / 8];
            Marshal.Copy(lockTo.Scan0, _rgbTo, 0, _rgbTo.Length);
            _baseImg.UnlockBits(lockTo);

            if (_from.hasAudio)
            {
                switch (this.BitsPerSample)
                {
                    case 8:
                        _baseSilence = 0x80;
                        break;
                    case 16:
                        _baseSilence = 0;
                        break;
                    default:
                        throw new NotSupportedException(string.Format("{0}bits audio not supported.", this.BitsPerSample));
                        break;
                }
            }
        }

        public void closeStream()
        {
            _from.closeStream();
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
