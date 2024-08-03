using HumbleFrameServer.Lib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace HumbleFrameServer.Base
{
    public class i_FadeIn : iAudioVideoStream
    {
        public string NodeName { get { return "FadeIn"; } }
        public string NodeDescription { get { return "FadeIn"; } }

        private iAudioVideoStream _to = null;
        private int _frames = 0;
        private bool _squared = true;

        private readonly Dictionary<string, NodeParameter> _Parameters = new() {
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
            {"color", new NodeParameter(){
                Type = NodeParameterType.String,
                IsRequired = false,
                Value = "#000000ff",
                Description = "rgba color of the fade (#000000ff)"
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
            get { return _to.FPS; }
        }

        public uint Width
        {
            get { return _to.Width; }
        }

        public uint Height
        {
            get { return _to.Height; }
        }

        private object Dissolve(Bitmap bmpTo, decimal Percentage)
        {
            double dPercentage = Convert.ToDouble(Percentage);
            Bitmap bmpResult = new(bmpTo.Width, bmpTo.Height, bmpTo.PixelFormat);

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
                    rgbResult[v] = Convert.ToByte(Math.Round(Math.Sqrt(Math.Pow(_rgbFrom[v], 2) + ((Math.Pow(rgbTo[v], 2) - Math.Pow(_rgbFrom[v], 2)) * dPercentage)), 0));
                }));
            }
            else
            {
                Parallel.For(0, rgbResult.Length, new Action<int>(v =>
                {
                    rgbResult[v] = Convert.ToByte(Math.Round(_rgbFrom[v] + ((rgbTo[v] - _rgbFrom[v]) * Percentage), 0));
                }));
            }

            Marshal.Copy(rgbResult, 0, lockResult.Scan0, rgbResult.Length);
            bmpResult.UnlockBits(lockResult);

            return bmpResult;
        }

        private int[] Fade(int[] sampleTo, decimal Percentage)
        {
            Parallel.For(0, sampleTo.Length, new Action<int>(b =>
            {
                sampleTo[b] = Convert.ToInt32(Math.Round(_baseSilence + ((sampleTo[b] - _baseSilence) * Percentage), 0));
            }));

            return sampleTo;
        }

        private DataPacket result = null;
        private decimal framesDone = 0M;
        public DataPacket getNextPacket()
        {
            if (framesDone < _frames)
            {
                DataPacket newPacket = _to.getNextPacket();
                switch (newPacket.Type)
                {
                    case PacketType.Video:
                        result = new DataPacket(PacketType.Video, Dissolve(newPacket.Data as Bitmap, (framesDone) / (_frames)));
                        framesDone++;
                        break;
                    case PacketType.Audio:
                        result = new DataPacket(PacketType.Audio, Fade(newPacket.Data as int[], (framesDone) / (_frames)));
                        break;
                }
            }
            else
            {
                result = _to.getNextPacket();
            }

            return result;
        }


        private byte[] _rgbFrom;
        private int _baseSilence;
        public void openStream()
        {
            _to = _Parameters["to"].Value as iAudioVideoStream;
            _to.openStream();
            _frames = (int)_Parameters["frames"].Value;
            _squared = (bool)_Parameters["squared"].Value;

            Color colorColor = (_Parameters["color"].Value as string).ToRGBAColor();

            Bitmap _baseImg = new(Convert.ToInt32(_to.Width), Convert.ToInt32(_to.Height), PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(_baseImg);
            Brush colorBrush = new SolidBrush(colorColor);
            g.FillRectangle(colorBrush, new Rectangle(0, 0, _baseImg.Width, _baseImg.Height));

            BitmapData lockFrom = _baseImg.LockBits(new Rectangle(0, 0, _baseImg.Width, _baseImg.Height), ImageLockMode.ReadOnly, _baseImg.PixelFormat);
            _rgbFrom = new byte[_baseImg.Width * _baseImg.Height * Image.GetPixelFormatSize(_baseImg.PixelFormat) / 8];
            Marshal.Copy(lockFrom.Scan0, _rgbFrom, 0, _rgbFrom.Length);
            _baseImg.UnlockBits(lockFrom);

            if (_to.hasAudio)
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
            _to.closeStream();
        }


        public NodeType Type
        {
            get { return NodeType.Filter; }
        }

        public bool hasAudio
        {
            get { return _to.hasAudio; }
        }

        public bool hasVideo
        {
            get { return _to.hasVideo; }
        }

        public ushort BitsPerSample
        {
            get { return _to.BitsPerSample; }
        }

        public uint SamplesPerSecond
        {
            get { return _to.SamplesPerSecond; }
        }

        public ushort ChannelsCount
        {
            get { return _to.ChannelsCount; }
        }
    }
}
