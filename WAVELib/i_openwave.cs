using HFS.Lib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HFS.WAVELib
{
    public class OpenWAVE : WAVEStream, iAudioVideoStream
    {
        public string NodeName { get { return "OpenWAVE"; } }
        public string NodeDescription { get { return "Adds support for WAVE audio files"; } }

        private BinaryReader _waveStreamReader = null;

        #region Properties

        /// <summary>
        /// Number of channels (default = 2)
        /// </summary>
        public ushort ChannelsCount { get { return _wChannels; } }


        /// <summary>
        /// Samples per seconds (Hertz) (Default 48000)
        /// </summary>
        public uint SamplesPerSecond { get { return _dwSamplesPerSec; } }


        /// <summary>
        /// Bits per samples (Default 16)
        /// </summary>
        public ushort BitsPerSample { get { return _wBitsPerSample; } }


        /// <summary>
        /// Size of a sample frame
        /// </summary>
        public ushort wBlockAlign { get { return _wBlockAlign; } }

        /// <summary>
        /// Avarage byte per seconds
        /// </summary>
        public ulong dwAvgBytesPerSec { get { return _dwAvgBytesPerSec; } }

        /// <summary>
        /// If true ingnores lenght of the wav file and keeps reading 'till the end of time
        /// </summary>
        private bool ignoreLenght = false;

        #endregion

        #region Private Methods

        private bool readHeader()
        {
            return _waveStreamReader.ReadChar() == 'R' &&
                _waveStreamReader.ReadChar() == 'I' &&
                _waveStreamReader.ReadChar() == 'F' &&
                _waveStreamReader.ReadChar() == 'F' &&
                _waveStreamReader.ReadInt32() != 0 &&
                _waveStreamReader.ReadChar() == 'W' &&
                _waveStreamReader.ReadChar() == 'A' &&
                _waveStreamReader.ReadChar() == 'V' &&
                _waveStreamReader.ReadChar() == 'E';
        }

        private bool readFMT()
        {
            bool result = false;
            try
            {
                while (result == false)
                {
                    //Find the "FMT " chunck
                    while (_waveStreamReader.ReadByte() != 0x66) //f
                    {
                    }

                    if (_waveStreamReader.ReadByte() == 0x6d && //m
                        _waveStreamReader.ReadByte() == 0x74 && //t
                        _waveStreamReader.ReadByte() == 0x20) //white space
                    {
                        int fmtLength = _waveStreamReader.ReadInt32();
                        _wFormatTag = _waveStreamReader.ReadInt16();
                        _wChannels = _waveStreamReader.ReadUInt16();
                        _dwSamplesPerSec = _waveStreamReader.ReadUInt32();
                        //_dwAvgBytesPerSec
                        _waveStreamReader.ReadUInt32();
                        //_wBlockAlign
                        _waveStreamReader.ReadUInt16();
                        _wBitsPerSample = _waveStreamReader.ReadUInt16();

                        //TODO: check congruence of FMT?

                        updateFMT();

                        result = true;
                    }
                }
            }
            catch (EndOfStreamException)
            {
            }

            return result;
        }


        private bool goToWAVEChunk()
        {
            try
            {
                _waveStreamReader.BaseStream.Seek(12, SeekOrigin.Begin);

                while (_waveChunkPosition < 0)
                {
                    while (_waveStreamReader.ReadByte() != 0x64) //d
                    {
                    }

                    if (_waveStreamReader.ReadByte() == 0x61 && //a
                        _waveStreamReader.ReadByte() == 0x74 && //t
                        _waveStreamReader.ReadByte() == 0x61) //a
                    {
                        _waveChunkSize = _waveStreamReader.ReadUInt32();
                        _waveChunkPosition = _waveStreamReader.BaseStream.Position;
                    }
                }
            }
            catch (EndOfStreamException)
            {
            }

            return _waveChunkPosition >= 0;
        }

        #endregion


        #region Public Methods

        public void openStream()
        {
            this.ignoreLenght = (bool)this._Parameters["ignore_length"].Value;

            _waveStreamReader = new BinaryReader(new FileStream((string)this._Parameters["path"].Value, FileMode.Open, FileAccess.Read, FileShare.Read));
            bool result = this.readHeader() && this.readFMT() && this.goToWAVEChunk();
            if (!result)
                throw new Exception("Error opening wave file");
        }

        public void closeStream()
        {
            _waveStreamReader.Close();
        }


        /// <summary>
        /// Return 
        /// </summary>
        /// <param name="sample"></param>
        /// <returns></returns>
        private int[] parseSample(byte[] inSample)
        {
            int[] result;

            switch (_wBitsPerSample)
            {
                case 8:
                    //Unsigned values from 0x00 to 0xFF
                    result = new int[inSample.Length];
                    inSample.CopyTo(result, 0);
                    break;
                case 16:
                    //Signed 2'S values from 0x8000 to 0xEFFF
                    result = new int[inSample.Length / 2];

                    Parallel.For(0, inSample.Length / 2, i =>
                    {
                        int b = i * 2;
                        result[i] = (short)(inSample[b + 1] * 256 + inSample[b]);
                    });

                    //for (int b = 0; b < inSample.Length; b += 2)
                    //{
                    //    result[b / 2] = (short)(inSample[b + 1] * 256 + inSample[b]);
                    //}
                    break;
                case 24:
                    //Signed 2'S values from 0xFF800000 to 0x007FFFFF
                    result = new int[inSample.Length / 4];
                    throw new NotImplementedException(string.Format("{0}bit audio is unsupported", _wBitsPerSample));
                    break;
                default:
                    throw new NotImplementedException(string.Format("{0}bit audio is unsupported", _wBitsPerSample));
                    break;
            }

            return result;
        }


        private DataPacket _nextPacket = new()
        {
            Type = PacketType.Audio
        };

        public DataPacket getNextPacket()
        {
            if (ignoreLenght || _waveChunkSize > 0)
            {
                _waveChunkSize -= _wBlockAlign;
                _nextPacket = new DataPacket(PacketType.Audio, parseSample(_waveStreamReader.ReadBytes(_wBlockAlign)));

                return _nextPacket;
            }
            else
            {
                return new DataPacket()
                {
                    Type = PacketType.Audio,
                    Data = null
                };
            }
        }

        #endregion


        private readonly Dictionary<string, NodeParameter> _Parameters = new() {
            { "path",
                new NodeParameter(){
                    Type = NodeParameterType.String,
                    IsRequired = true
                }
            },
            { "ignore_length",
                new NodeParameter(){
                    Type = NodeParameterType.Bool,
                    IsRequired = false,
                    Value = false
                }
            }
        };

        public Dictionary<string, NodeParameter> Parameters { get { return _Parameters; } }

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
            //Assume 25FPS
            get { return 25M; }
        }

        public uint Width
        {
            get { throw new NotImplementedException(); }
        }

        public uint Height
        {
            get { throw new NotImplementedException(); }
        }
    }
}