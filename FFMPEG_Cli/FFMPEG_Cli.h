// FFMPEG_Cli.h

#pragma once

#using <system.drawing.dll>

using namespace System;
using namespace System::Drawing;
using namespace System::Drawing::Imaging;

extern "C" {
#include "include/libavformat\avformat.h"
#include "include/libavutil\imgutils.h"
#include "include/libavutil\samplefmt.h"
#include "include/libavutil\pixfmt.h"
#include "include/libswresample\swresample.h"
#include "include/libswscale\swscale.h"
}

namespace FFMPEG_Cli {

	public ref class frame_data {
	public: bool is_audio = false;
			bool is_video = false;

			///Data size
			int audio_size = 0;
			System::Drawing::Bitmap^ video_data;
			array<int>^ audio_data;

			String^ formatName = "";
	};

	public ref class video_info {
	public: int errorcode = 0;

			bool has_audio = false;
			bool has_video = false;

			double fps = 0;
			unsigned int width = 0;
			unsigned int height = 0;

			unsigned short channels = 0;
			unsigned short sample_rate = 0;
			unsigned short nb_sample = 0;
	};

	public ref class FFMPEGWrapper
	{
	public: video_info ^ OpenVideo(System::String^ FilePath);
	public: frame_data ^ GetNextFrame();
	public: int Close();
	};
}
