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
    public class f_Blur : iAudioVideoStream
    {
        private iAudioVideoStream _input = null;
        private int _radius = 5;
        //private double _sigma = 10;
        //private int _boxes = 3;

        private Rectangle _imageSizedRect;

        public string NodeName { get { return "Blur"; } }
        public string NodeDescription { get { return "Blur video"; } }
        public NodeType Type { get { return NodeType.Filter; } }

        private Dictionary<string, NodeParameter> _Parameters = new Dictionary<string, NodeParameter>() { 
            {"input", new NodeParameter(){
                Type = NodeParameterType.AudioVideoStream,
                IsRequired = true,
                Value = null
            }},
            {"radius", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 5,
                Description="Blur radius (5)"
            }}
        };

        public Dictionary<string, NodeParameter> Parameters { get { return _Parameters; } }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sigma">standard deviation</param>
        /// <param name="boxes">number of boxes</param>
        /// <returns></returns>
        private double[] boxesForGauss(int sigma, int boxes)
        {
            double wIdeal = Math.Sqrt((12.0 * sigma * sigma / boxes) + 1);  // Ideal averaging filter width 
            double wl = Math.Floor(wIdeal);
            if (wl % 2 == 0)
            {
                wl--;
            }
            double wu = wl + 2;

            double mIdeal = (12.0 * sigma * sigma - boxes * wl * wl - 4 * boxes * wl - 3 * boxes) / (-4 * wl - 4);
            double m = Math.Round(mIdeal);
            // var sigmaActual = Math.sqrt( (m*wl*wl + (n-m)*wu*wu - n)/12 );

            double[] sizes = new double[boxes];
            for (int i = 0; i < boxes; i++)
            {
                sizes[i] = i < m ? wl : wu;
            }

            return sizes;
        }

        int _rs = 0;
        private void gaussianBlur(byte[] source /*scl*/, byte[] dest /*tcl*/, int width, int height)
        {
            double valR = 0;
            double valG = 0;
            double valB = 0;
            double valA = 0;
            double wsum = 0;

            for (int i = 0; i < source.Length; i += 4)
            {
                int posY = i / 4 / width;
                int posX = (i / 4) - (width * posY);

                valR = 0;
                valG = 0;
                valB = 0;
                valA = 0;
                wsum = 0;

                for (int iy = posY - _rs; iy < posY + _rs + 1; iy++)
                {
                    for (int ix = posX - _rs; ix < posX + _rs + 1; ix++)
                    {
                        int x = Math.Min(width - 1, Math.Max(0, ix));
                        int y = Math.Min(height - 1, Math.Max(0, iy));
                        int newPos = (y * width + x) * 4;

                        double dsq = Math.Pow((ix - posX), 2) + Math.Pow((iy - posY), 2);
                        double wght = Math.Exp(-dsq / (2 * _radius * _radius)) / (Math.PI * 2 * _radius * _radius);

                        valR += Math.Pow(source[newPos], 2) * wght;
                        valG += Math.Pow(source[newPos + 1], 2) * wght;
                        valB += Math.Pow(source[newPos + 2], 2) * wght;
                        valA += Math.Pow(source[newPos + 3], 2) * wght;

                        wsum += wght;
                    }
                }

                dest[i] = Convert.ToByte(Math.Round(Math.Sqrt(valR / wsum), 0));
                dest[i + 1] = Convert.ToByte(Math.Round(Math.Sqrt(valG / wsum), 0));
                dest[i + 2] = Convert.ToByte(Math.Round(Math.Sqrt(valB / wsum), 0));
                dest[i + 3] = Convert.ToByte(Math.Round(Math.Sqrt(valA / wsum), 0));
            }
        }

        private DataPacket result = null;

        public DataPacket getNextPacket()
        {
            result = _input.getNextPacket();
            if (result.Data != null && result.Type == PacketType.Video)
            {
                Bitmap origFrame = result.Data as Bitmap;
                Bitmap blurFrame = new Bitmap(origFrame.Width, origFrame.Height, origFrame.PixelFormat);

                BitmapData origLock = origFrame.LockBits(_imageSizedRect, ImageLockMode.ReadOnly, origFrame.PixelFormat);
                byte[] origRgb = new byte[origFrame.Width * origFrame.Height * Image.GetPixelFormatSize(origFrame.PixelFormat) / 8];
                Marshal.Copy(origLock.Scan0, origRgb, 0, origRgb.Length);
                origFrame.UnlockBits(origLock);

                BitmapData blurLock = blurFrame.LockBits(_imageSizedRect, ImageLockMode.ReadOnly, blurFrame.PixelFormat);
                byte[] blurRgb = new byte[blurFrame.Width * blurFrame.Height * Image.GetPixelFormatSize(blurFrame.PixelFormat) / 8];
                Marshal.Copy(blurLock.Scan0, blurRgb, 0, blurRgb.Length);

                _rs = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(_radius) * 2.57));     // significant radius
                gaussianBlur(origRgb, blurRgb, blurFrame.Width, blurFrame.Height);

                Marshal.Copy(blurRgb, 0, blurLock.Scan0, blurRgb.Length);
                blurFrame.UnlockBits(blurLock);

                return new DataPacket(PacketType.Video, blurFrame);
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
                throw new Exception("Cannot blur audio...");
            }
            else
            {
                _radius = (int)_Parameters["radius"].Value;
                _imageSizedRect = new Rectangle(0, 0, Convert.ToInt32(_input.Width), Convert.ToInt32(_input.Height));
            }
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