// This is the main DLL file.

#include "stdafx.h"
#include <msclr/marshal.h>

#include "FFMPEG_Cli.h"


static const char *src_filename = NULL;
static int width, height;
static uint8_t *video_dst_data[4] = { NULL };
static int      video_dst_linesize[4];
static int video_dst_bufsize;
static AVFrame *frame = NULL;
static AVPacket pkt;

static AVStream *video_stream = NULL, *audio_stream = NULL;

static AVFormatContext *fmt_ctx = NULL;
static AVCodecContext *video_dec_ctx = NULL, *audio_dec_ctx;
static int refcount = 0;
static int video_stream_idx = -1, audio_stream_idx = -1;

static int open_codec_context(int *stream_idx,
	AVCodecContext **dec_ctx, AVFormatContext *fmt_ctx, enum AVMediaType type)
{
	int ret, stream_index;
	AVStream *st;
	AVCodec *dec = NULL;
	AVDictionary *opts = NULL;
	ret = av_find_best_stream(fmt_ctx, type, -1, -1, NULL, 0);
	if (ret < 0) {
		fprintf(stderr, "Could not find %s stream in input file '%s'\n",
			av_get_media_type_string(type), src_filename);
		return ret;
	}
	else {
		stream_index = ret;
		st = fmt_ctx->streams[stream_index];
		/* find decoder for the stream */
		dec = avcodec_find_decoder(st->codecpar->codec_id);
		if (!dec) {
			fprintf(stderr, "Failed to find %s codec\n",
				av_get_media_type_string(type));
			return AVERROR(EINVAL);
		}
		/* Allocate a codec context for the decoder */
		*dec_ctx = avcodec_alloc_context3(dec);
		if (!*dec_ctx) {
			fprintf(stderr, "Failed to allocate the %s codec context\n",
				av_get_media_type_string(type));
			return AVERROR(ENOMEM);
		}
		/* Copy codec parameters from input stream to output codec context */
		if ((ret = avcodec_parameters_to_context(*dec_ctx, st->codecpar)) < 0) {
			fprintf(stderr, "Failed to copy %s codec parameters to decoder context\n",
				av_get_media_type_string(type));
			return ret;
		}
		/* Init the decoders, with or without reference counting */
		av_dict_set(&opts, "refcounted_frames", refcount ? "1" : "0", 0);
		if ((ret = avcodec_open2(*dec_ctx, dec, &opts)) < 0) {
			fprintf(stderr, "Failed to open %s codec\n",
				av_get_media_type_string(type));
			return ret;
		}
		*stream_idx = stream_index;
	}
	return 0;
}

static int decode_packet(int *got_frame, int cached)
{
	int ret = 0;
	int decoded = pkt.size;
	*got_frame = 0;
	if (pkt.stream_index == video_stream_idx) {
		/* decode video frame */
		ret = avcodec_decode_video2(video_dec_ctx, frame, got_frame, &pkt);
		if (ret < 0) {
			//fprintf(stderr, "Error decoding video frame (%s)\n", av_err2str(ret));
			return ret;
		}
		if (*got_frame) {
			if (frame->width != width || frame->height != height ||
				frame->format != video_dec_ctx->pix_fmt) {
				/* To handle this change, one could call av_image_alloc again and
				* decode the following frames into another rawvideo file. */
				/*fprintf(stderr, "Error: Width, height and pixel format have to be "
				"constant in a rawvideo file, but the width, height or "
				"pixel format of the input video changed:\n"
				"old: width = %d, height = %d, format = %s\n"
				"new: width = %d, height = %d, format = %s\n",
				width, height, av_get_pix_fmt_name(pix_fmt),
				frame->width, frame->height,
				av_get_pix_fmt_name(frame->format));*/
				return -1;
			}
			//printf("video_frame%s n:%d coded_n:%d\n", cached ? "(cached)" : "", video_frame_count++, frame->coded_picture_number);
			/* copy decoded frame to destination buffer:
			* this is required since rawvideo expects non aligned data */
			av_image_copy(video_dst_data, video_dst_linesize,
				(const uint8_t **)(frame->data), frame->linesize,
				video_dec_ctx->pix_fmt, width, height);
			/* write to rawvideo file */
			//fwrite(video_dst_data[0], 1, video_dst_bufsize, video_dst_file);
		}
	}
	else if (pkt.stream_index == audio_stream_idx) {
		/* decode audio frame */
		ret = avcodec_decode_audio4(audio_dec_ctx, frame, got_frame, &pkt);
		if (ret < 0) {
			//fprintf(stderr, "Error decoding audio frame (%s)\n", av_err2str(ret));
			return ret;
		}
		/* Some audio decoders decode only part of the packet, and have to be
		* called again with the remainder of the packet data.
		* Sample: fate-suite/lossless-audio/luckynight-partial.shn
		* Also, some decoders might over-read the packet. */
		decoded = FFMIN(ret, pkt.size);
		//if (*got_frame) {
			//size_t unpadded_linesize = frame->nb_samples * av_get_bytes_per_sample((AVSampleFormat)frame->format);
			//printf("audio_frame%s n:%d nb_samples:%d pts:%s\n", cached ? "(cached)" : "", audio_frame_count++, frame->nb_samples, av_ts2timestr(frame->pts, &audio_dec_ctx->time_base));		
			/* Write the raw audio data samples of the first plane. This works
			* fine for packed formats (e.g. AV_SAMPLE_FMT_S16). However,
			* most audio decoders output planar audio, which uses a separate
			* plane of audio samples for each channel (e.g. AV_SAMPLE_FMT_S16P).
			* In other words, this code will write only the first audio channel
			* in these cases.
			* You should use libswresample or libavfilter to convert the frame
			* to packed data. */
			//fwrite(frame->extended_data[0], 1, unpadded_linesize, audio_dst_file);
		//}
	}
	/* If we use frame reference counting, we own the data and need
	* to de-reference it when we don't use it anymore */
	if (*got_frame && refcount)
		av_frame_unref(frame);
	return decoded;
}




FFMPEG_Cli::video_info^ FFMPEG_Cli::FFMPEGWrapper::OpenVideo(String^ FilePath)
{
	FFMPEG_Cli::video_info^ result = gcnew video_info();

	msclr::interop::marshal_context ctx;
	src_filename = ctx.marshal_as<const char*>(FilePath);

	//av_register_all();

	result->errorcode = avformat_open_input(&fmt_ctx, src_filename, NULL, NULL);
	if (result->errorcode == 0)
	{
		result->errorcode = avformat_find_stream_info(fmt_ctx, NULL);
	}

	if (result->errorcode == 0)
	{
		if (open_codec_context(&video_stream_idx, &video_dec_ctx, fmt_ctx, AVMEDIA_TYPE_VIDEO) >= 0)
		{
			video_stream = fmt_ctx->streams[video_stream_idx];

			result->has_video = true;
			result->width = video_dec_ctx->width;
			result->height = video_dec_ctx->height;
			result->fps = video_stream->r_frame_rate.num / (video_stream->r_frame_rate.den * 1.0);

			if (open_codec_context(&audio_stream_idx, &audio_dec_ctx, fmt_ctx, AVMEDIA_TYPE_AUDIO) >= 0) {
				audio_stream = fmt_ctx->streams[audio_stream_idx];
				result->has_audio = true;
				result->channels = audio_dec_ctx->channels;
				result->sample_rate = audio_dec_ctx->sample_rate;
				result->nb_sample = 16; // audio_dec_ctx->bits_per_raw_sample;
			}


		}
		else
		{
			result->errorcode = -1;
		}
	}

	return result;
	//		AVCodecID video = fmt_ctx->streams[0]->codec->codec_id;
	//		AVCodecID audio = fmt_ctx->streams[1]->codec->codec_id;
}

struct SwsContext *videoConverterCtx;
struct SwrContext *audioConvertCtx;

FFMPEG_Cli::frame_data^ FFMPEG_Cli::FFMPEGWrapper::GetNextFrame()
{
	int errorCode = 0, got_frame;
	FFMPEG_Cli::frame_data^ result;

	msclr::interop::marshal_context ctx;

	frame = av_frame_alloc();
	if (frame) {
		/* initialize packet, set data to NULL, let the demuxer fill it */
		av_init_packet(&pkt);
		pkt.data = NULL;
		pkt.size = 0;

		/* read frames from the file */
		if (av_read_frame(fmt_ctx, &pkt) >= 0) {
			AVPacket orig_pkt = pkt;
			do {
				errorCode = decode_packet(&got_frame, 0);
				if (errorCode < 0)
					break;
				pkt.data += errorCode;
				pkt.size -= errorCode;
			} while (pkt.size > 0);
			av_packet_unref(&orig_pkt);

			result = gcnew frame_data();

			if (frame->pict_type > 0)
			{
				result->is_video = true;

				//convert pixel format to argb
				if (!videoConverterCtx)
				{
					videoConverterCtx =
						sws_getContext(video_dec_ctx->width, video_dec_ctx->height, video_dec_ctx->pix_fmt,
							video_dec_ctx->width, video_dec_ctx->height, AV_PIX_FMT_BGRA,
							SWS_BILINEAR, NULL, NULL, NULL);
				}

				if (videoConverterCtx)
				{
					uint8_t *dst_data[4];
					int dst_linesize[4];

					int ret;
					if ((ret = av_image_alloc(dst_data, dst_linesize,
						frame->width, frame->height, AV_PIX_FMT_BGRA, 1)) < 0) {
						fprintf(stderr, "Could not allocate destination image\n");
					}

					sws_scale(videoConverterCtx, frame->data,
						frame->linesize, 0, frame->height, dst_data, dst_linesize);

					result->data = gcnew array<byte>(dst_linesize[0] * video_dec_ctx->height);
					System::Runtime::InteropServices::Marshal::Copy((IntPtr)dst_data[0], result->data, 0, result->data->Length);

					av_freep(&dst_data);
				}
			}
			else if (frame->nb_samples > 0)
			{
				if (audio_dec_ctx->sample_fmt != AV_SAMPLE_FMT_S16)
				{
					if (!audioConvertCtx)
					{
						audioConvertCtx = swr_alloc_set_opts(NULL,  // we're allocating a new context
							audio_dec_ctx->channel_layout,  // out_ch_layout
							AV_SAMPLE_FMT_S16,    // out_sample_fmt
							audio_dec_ctx->sample_rate, // out_sample_rate
							audio_dec_ctx->channel_layout, // in_ch_layout
							audio_dec_ctx->sample_fmt,   // in_sample_fmt
							audio_dec_ctx->sample_rate,  // in_sample_rate
							0,                    // log_offset
							NULL);                // log_ctx

						swr_init(audioConvertCtx);
					}

					if (audioConvertCtx)
					{

						int outSamples = swr_get_out_samples(audioConvertCtx, frame->nb_samples);
						uint8_t * outAudio[1];
						outAudio[0] = (uint8_t *)malloc(outSamples * 4);
						swr_convert(audioConvertCtx, outAudio, outSamples, (const uint8_t**)frame->data, frame->nb_samples);

						result->is_audio = true;
						result->audio_size = outSamples * 8;// frame->linesize[0];
						result->data = gcnew array<byte>(outSamples * 4);
						System::Runtime::InteropServices::Marshal::Copy((IntPtr)outAudio[0], result->data, 0, result->data->Length);

						result->formatName = ctx.marshal_as<String^>(av_get_sample_fmt_name((AVSampleFormat)frame->format));

						free(outAudio[0]);

						swr_free(&audioConvertCtx);
					}
				}
				else
				{
					result->is_audio = true;
					result->audio_size = frame->linesize[0];
					result->data = gcnew array<byte>(frame->linesize[0]);
					System::Runtime::InteropServices::Marshal::Copy((IntPtr)frame->data[0], result->data, 0, result->data->Length);

					result->formatName = ctx.marshal_as<String^>(av_get_sample_fmt_name((AVSampleFormat)frame->format));

				}
			}
		}

		av_freep(&frame);
	}

	return result;
}



int FFMPEG_Cli::FFMPEGWrapper::Close()
{
	return 0;
}


