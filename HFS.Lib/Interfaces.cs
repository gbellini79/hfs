﻿using System.Collections.Generic;

namespace HFS.Lib
{
    public enum NodeType
    {
        Input,
        Filter,
        Scalar
    }


    public enum NodeParameterType
    {
        AudioVideoStream,
        String,
        Int,
        Decimal,
        Bool
    }

    public enum PacketType
    {
        Audio,
        Video
        //,Subtitle
    }


    public class DataPacket
    {
        public PacketType Type { get; set; }

        /// <summary>
        /// if video Data will be a Bitmap
        /// if audio Data will be a int[]
        /// </summary>
        public object Data { get; set; }

        public DataPacket() { }

        public DataPacket(PacketType TypeOfPacket, object DataObject)
        {
            Type = TypeOfPacket;
            Data = DataObject;
        }
    }


    public class NodeParameter
    {
        private bool _IsRequired = true;
        private object _Value = null;
        private string _Description = "";
        private bool _AsVariable = false;

        public NodeParameterType Type { get; set; }
        public bool IsRequired { get { return _IsRequired; } set { _IsRequired = value; } }
        public object Value { get { return _Value; } set { _Value = value; } }
        public string Description { get { return _Description; } set { _Description = value; } }
        public bool AsVariable { get { return _AsVariable; } set { _AsVariable = value; } }
    }


    public interface iAudioVideoStream
    {
        #region Common

        /// <summary>
        /// Gets the function name to be use in scripts
        /// </summary>
        string NodeName { get; }

        /// <summary>
        /// Gets the function description
        /// </summary>
        string NodeDescription { get; }

        /// <summary>
        /// Gets the last error generated by the plugin
        /// </summary>
        //string? LastError { get; }

        /// <summary>
        /// Type of the class
        /// </summary>
        NodeType Type { get; }

        bool hasAudio { get; }
        bool hasVideo { get; }
        //bool canSeek { get; }

        /// <summary>
        /// Gets the parameter for the function
        /// </summary>
        Dictionary<string, NodeParameter> Parameters { get; }

        /// <summary>
        /// Opens and intialises stream(s)
        /// </summary>
        void openStream();

        /// <summary>
        /// Closes stream(s)
        /// </summary>
        void closeStream();

        /// <summary>
        /// Gets next uncompressed packet of audio (byte[]) or video (bitmap) data
        /// </summary>
        DataPacket getNextPacket();

        /// <summary>
        /// Moves to the specified frame
        /// </summary>
        //void Seek(int frame);

        #endregion

        #region Audio

        /// <summary>
        /// Gets bits per sample
        /// </summary>
        ushort BitsPerSample { get; }

        /// <summary>
        /// Gets samples per seconds
        /// </summary>
        uint SamplesPerSecond { get; }

        /// <summary>
        /// Gets channels count
        /// </summary>
        ushort ChannelsCount { get; }

        #endregion

        #region Video

        decimal FPS { get; }
        uint Width { get; }
        uint Height { get; }

        #endregion
    }
}
