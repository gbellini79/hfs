using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using HumbleFrameServer.Lib;

namespace HumbleFrameServer.WAVELib
{
    public class Silence : WAVEStream, iAudioVideoStream
    {
        public string NodeName { get { return "Silence"; } }
        public string NodeDescription { get { return "Returns an endless silent audio stream"; } }

        public ushort BitsPerSample { get { return _wBitsPerSample; } }
        public uint SamplesPerSecond { get { return _dwSamplesPerSec; } }
        public ushort ChannelsCount { get { return _wChannels; } }


        private Dictionary<string, NodeParameter> _Parameters = new Dictionary<string, NodeParameter>() { 
            {"samplepersecond", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 48000
            }},    
            {"bitpersample", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 16
            }},
            {"channels", new NodeParameter(){
                Type = NodeParameterType.Int,
                IsRequired = false,
                Value = 2
            }}
        };



        public Dictionary<string, NodeParameter> Parameters { get { return _Parameters; } }


        public DataPacket getNextPacket()
        {
            DataPacket result;

            result = new DataPacket(PacketType.Audio, _silentSample.ToArray());

            return result;
        }

        private int[] _silentSample;
        public void openStream()
        {
            _wBitsPerSample = Convert.ToUInt16(_Parameters["bitpersample"].Value);
            _dwSamplesPerSec = Convert.ToUInt32(_Parameters["samplepersecond"].Value);
            _wChannels = Convert.ToUInt16(_Parameters["channels"].Value);
            updateFMT();

            if (this.BitsPerSample == 8)
            {
                _silentSample = new int[_wBlockAlign];
                for (int x = 0; x < _silentSample.Length; x++)
                {
                    _silentSample[x] = 0x80;
                }
            }
            else if (this.BitsPerSample == 16)
            {
                _silentSample = new int[_wBlockAlign / 2];
            }
            else
            {
                throw new NotImplementedException(string.Format("{0}bits audio not supported.", this.BitsPerSample));
            }

            _waveChunkSize = Convert.ToInt64(_wBlockAlign * _dwSamplesPerSec);
            if (_waveChunkSize <= 0)
            {
                throw new Exception("Invalid chunk size.");
            }
        }

        public void closeStream()
        {
            //Do nothing
            _waveChunkSize = -1;
        }


        public NodeType Type
        {
            get { return NodeType.Input; }
        }

        public bool hasAudio
        {
            get { return true; }
        }

        public bool hasVideo
        {
            get { return false; }
        }

        public decimal FPS
        {
            get { return 0; }
        }

        public uint Width
        {
            get { return 0; }
        }

        public uint Height
        {
            get { return 0; }
        }
    }
}