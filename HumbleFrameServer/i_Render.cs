using HumbleFrameServer.Lib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HumbleFrameServer.Base
{
    internal class Renderer : iAudioVideoStream
    {
        public string NodeName { get { return "render"; } }

        public string NodeDescription { get { return "Main renderer"; } }

        private int _framesCount = 0;

        public NodeType Type
        {
            get { return NodeType.Input; }
        }

        private bool _hasAudio = false;
        public bool hasAudio
        {
            get { return _hasAudio; }
        }

        private bool _hasVideo = false;
        public bool hasVideo
        {
            get { return _hasVideo; }
        }

        public bool canSeek { get; private set; } = false;

        private readonly Dictionary<string, NodeParameter> _Parameters = [];
        public Dictionary<string, NodeParameter> Parameters
        {
            get { return _Parameters; }
        }

        /// <summary>
        /// Last meaningful row. Is the row that produces the output
        /// </summary>
        private iAudioVideoStream _outputRow = null;

        public void openStream()
        {
            //Per ogni riga elaborata, a partire dalla seconda, se c'è un input di tipo "video" senza valore assegnato, assegna l'output della riga precedente
            // se la riga precedente non è l'assegnazione di una variabile

            NodeParameter lastParam = null;

            foreach (NodeParameter currParam in _Parameters.Values)
            {
                switch (currParam.Type)
                {
                    case NodeParameterType.AudioVideoStream:
                        iAudioVideoStream currentRow = currParam.Value as iAudioVideoStream;
                        if (lastParam != null && !lastParam.AsVariable && currentRow != null)
                        {
                            List<KeyValuePair<string, NodeParameter>> firstAudioVideo = new(currentRow.Parameters.Where(x => x.Value.Type == NodeParameterType.AudioVideoStream && x.Value.Value == null && x.Value.IsRequired));
                            if (firstAudioVideo.Count > 0)
                            {
                                firstAudioVideo[0].Value.Value = lastParam.Value;
                            }
                        }
                        lastParam = currParam;

                        foreach (KeyValuePair<string, NodeParameter> nullParams in currentRow.Parameters.Where(x =>
                        {
                            return x.Value.IsRequired && x.Value.Value == null;
                        }))
                        {
                            throw new ArgumentNullException(string.Format("{0}: parameter \"{1}\" is required.", currentRow.NodeName, nullParams.Key));
                        }

                        break;
                    default:
                        break;
                }


            }

            //TODO: controllare se è AudioVideoStream?
            _outputRow = (_Parameters.Last().Value.Value as iAudioVideoStream);
            _outputRow.openStream();
            _hasAudio = _outputRow.hasAudio;
            _hasVideo = _outputRow.hasVideo;
            //canSeek = _outputRow.canSeek;
        }

        public void closeStream()
        {
            _outputRow.closeStream();
        }

        //public void Seek(int frame)
        //{
        //    if(canSeek)
        //    {
        //        _outputRow.Seek(frame);
        //    }
        //}

        public DataPacket getNextPacket()
        {
            _framesCount++;
            return _outputRow.getNextPacket();
        }

        public ushort BitsPerSample
        {
            get { return _outputRow.BitsPerSample; }
        }

        public uint SamplesPerSecond
        {
            get { return _outputRow.SamplesPerSecond; }
        }

        public ushort ChannelsCount
        {
            get { return _outputRow.ChannelsCount; }
        }

        public decimal FPS
        {
            get { return _outputRow.FPS; }
        }

        public uint Width
        {
            get { return _outputRow.Width; }
        }

        public uint Height
        {
            get { return _outputRow.Height; }
        }
    }
}