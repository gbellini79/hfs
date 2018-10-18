﻿using HumbleFrameServer.Lib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HumbleFrameServer.Base
{
    public static class general
    {
        public static string Right(this string Text, int Length)
        {
            return Text.Substring(Text.Length - Length);
        }

        public static Color ToRGBAColor(this string colorString)
        {
            try
            {
                return Color.FromArgb(byte.Parse(colorString.Substring(7, 2), System.Globalization.NumberStyles.AllowHexSpecifier), byte.Parse(colorString.Substring(1, 2), System.Globalization.NumberStyles.AllowHexSpecifier), byte.Parse(colorString.Substring(3, 2), System.Globalization.NumberStyles.AllowHexSpecifier), byte.Parse(colorString.Substring(5, 2), System.Globalization.NumberStyles.AllowHexSpecifier));
            }
            catch
            {
                throw new Exception(string.Format("Invalid color \"{0}\"", colorString));
            }
        }

        public static bool IsSameAudio(this iAudioVideoStream av1, iAudioVideoStream audioVideoStream)
        {
            return
                (
                    !av1.hasAudio && !audioVideoStream.hasAudio
                )
                ||
                (
                    av1.hasAudio && audioVideoStream.hasAudio
                    && av1.ChannelsCount == audioVideoStream.ChannelsCount
                    && av1.BitsPerSample == audioVideoStream.BitsPerSample
                    && av1.SamplesPerSecond == audioVideoStream.SamplesPerSecond
                );

        }
    }
}
