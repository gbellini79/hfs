using System;

namespace HumbleFrameServer.WAVELib
{
    public class WAVEStream
    {
        #region Private properties

        internal short _wFormatTag = 1;
        internal ushort _wChannels = 2;
        internal uint _dwSamplesPerSec = 48000;
        internal ushort _wBitsPerSample = 16;
        internal ushort _wBlockAlign = 2;
        internal ulong _dwAvgBytesPerSec = 96000;

        internal long _waveChunkPosition = -1;
        internal long _waveChunkSize = -1;

        #endregion

        /// <summary>
        /// Data format (1 = uncompressed)
        /// </summary>
        public short wFormatTag { get { return _wFormatTag; } }

        /// <summary>
        /// Update FMT Chunk
        /// </summary>
        internal void updateFMT()
        {
            _wBlockAlign = Convert.ToUInt16(_wChannels * (_wBitsPerSample / 8));
            _dwAvgBytesPerSec = _wBlockAlign * _dwSamplesPerSec;
        }
    }
}
