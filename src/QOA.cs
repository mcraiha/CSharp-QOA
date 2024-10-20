namespace QOA;

public struct QOA_LMS
{
	public int[] history = new int[QOA.QOA_LMS_LEN];
	public int[] weights = new int[QOA.QOA_LMS_LEN];

	public QOA_LMS()
	{

	}
}

public struct QOA_Desc
{
	public uint channels;
	public uint samplerate;
	public uint samples;
	public QOA_LMS[] lms = new QOA_LMS[QOA.QOA_MAX_CHANNELS] 
	{ 
		new QOA_LMS(), new QOA_LMS(), new QOA_LMS(), new QOA_LMS(), new QOA_LMS(), new QOA_LMS(), new QOA_LMS(), new QOA_LMS()
	};

	public QOA_Desc()
	{

	}
}

public sealed class QOA
{
	private const int QOA_MIN_FILESIZE = 16;
	public const int QOA_MAX_CHANNELS = 8;

	private const uint QOA_SLICE_LEN = 20;
	private const int QOA_SLICES_PER_FRAME = 256;

	private const uint QOA_FRAME_LEN = QOA_SLICES_PER_FRAME * QOA_SLICE_LEN;
	public const int QOA_LMS_LEN = 4;
	private const uint QOA_MAGIC = 0x716f6166; /* 'qoaf' */

	private static uint QOA_FRAME_SIZE(uint channels, uint slices)
	{
		return (8 + QOA_LMS_LEN * 4 * channels + 8 * slices * channels);
	}


	private static readonly int[] qoa_quant_tab = new int[17]
	{
		7, 7, 7, 5, 5, 3, 3, 1, /* -8..-1 */
		0,                      /*  0     */
		0, 2, 2, 4, 4, 6, 6, 6  /*  1.. 8 */
	};

	private static readonly int[] qoa_scalefactor_tab = new int[16]
	{
		1, 7, 21, 45, 84, 138, 211, 304, 421, 562, 731, 928, 1157, 1419, 1715, 2048
	};

	private static readonly int[] qoa_reciprocal_tab = new int[16]
	{
		65536, 9363, 3121, 1457, 781, 475, 311, 216, 156, 117, 90, 71, 57, 47, 39, 32
	};

	private static readonly int[,] qoa_dequant_tab = new int[16, 8]
	{
		{   1,    -1,    3,    -3,    5,    -5,     7,     -7},
		{   5,    -5,   18,   -18,   32,   -32,    49,    -49},
		{  16,   -16,   53,   -53,   95,   -95,   147,   -147},
		{  34,   -34,  113,  -113,  203,  -203,   315,   -315},
		{  63,   -63,  210,  -210,  378,  -378,   588,   -588},
		{ 104,  -104,  345,  -345,  621,  -621,   966,   -966},
		{ 158,  -158,  528,  -528,  950,  -950,  1477,  -1477},
		{ 228,  -228,  760,  -760, 1368, -1368,  2128,  -2128},
		{ 316,  -316, 1053, -1053, 1895, -1895,  2947,  -2947},
		{ 422,  -422, 1405, -1405, 2529, -2529,  3934,  -3934},
		{ 548,  -548, 1828, -1828, 3290, -3290,  5117,  -5117},
		{ 696,  -696, 2320, -2320, 4176, -4176,  6496,  -6496},
		{ 868,  -868, 2893, -2893, 5207, -5207,  8099,  -8099},
		{1064, -1064, 3548, -3548, 6386, -6386,  9933,  -9933},
		{1286, -1286, 4288, -4288, 7718, -7718, 12005, -12005},
		{1536, -1536, 5120, -5120, 9216, -9216, 14336, -14336},
	};

	private static int qoa_lms_predict(QOA_LMS lms)
	{
		int prediction = 0;

		for (int i = 0; i < QOA_LMS_LEN; i++) 
		{
			prediction += lms.weights[i] * lms.history[i];
		}

		return prediction >> 13;
	}

	private static void qoa_lms_update(QOA_LMS lms, int sample, int residual)
	{
		int delta = residual >> 4;

		for (int i = 0; i < QOA_LMS_LEN; i++) 
		{
			lms.weights[i] += lms.history[i] < 0 ? -delta : delta;
		}

		for (int i = 0; i < QOA_LMS_LEN-1; i++) 
		{
			lms.history[i] = lms.history[i+1];
		}

		lms.history[QOA_LMS_LEN-1] = sample;
	}

	private static int qoa_div(int v, int scalefactor)
	{
		int reciprocal = qoa_reciprocal_tab[scalefactor];
		int n = (v * reciprocal + (1 << 15)) >> 16;
		n = n + Sign(v) - Sign(v) - Sign(n) - Sign(n); /* round away from 0 */
		return n;
	}

	private static int Sign(int value)
	{
		if (value < 0)
		{
			return -1;
		}
		if (value > 0)
		{
			return 1;
		}
		return 0;
	}

	private static int qoa_clamp(int v, int min, int max)
	{
		if (v < min) { return min; }
		if (v > max) { return max; }
		return v;
	}


	private static int qoa_clamp_s16(int v)
	{
		if ((uint)(v + 32768) > 65535) 
		{
			if (v < -32768) { return -32768; }
			if (v >  32767) { return  32767; }
		}
		return v;
	}

	static ulong qoa_read_u64(Stream stream)
	{
		Span<byte> bytes = stackalloc byte[8];
		stream.Read(bytes);
		return 
			((ulong)(bytes[0]) << 56) | ((ulong)(bytes[1]) << 48) |
			((ulong)(bytes[2]) << 40) | ((ulong)(bytes[3]) << 32) |
			((ulong)(bytes[4]) << 24) | ((ulong)(bytes[5]) << 16) |
			((ulong)(bytes[6]) <<  8) | ((ulong)(bytes[7]) <<  0);
	}

	private static void qoa_write_u64(ulong v, Stream stream)
	{
		Span<byte> bytes = stackalloc byte[8];
		bytes[0] = (byte)((v >> 56) & 0xff);
		bytes[1] = (byte)((v >> 48) & 0xff);
		bytes[2] = (byte)((v >> 40) & 0xff);
		bytes[3] = (byte)((v >> 32) & 0xff);
		bytes[4] = (byte)((v >> 24) & 0xff);
		bytes[5] = (byte)((v >> 16) & 0xff);
		bytes[6] = (byte)((v >>  8) & 0xff);
		bytes[7] = (byte)((v >>  0) & 0xff);
		stream.Write(bytes);
	}

	public void qoa_encode_header(QOA_Desc qoa, Stream stream) 
	{
		qoa_write_u64(((ulong)QOA_MAGIC << 32) | qoa.samples, stream);
	}

	public void qoa_encode_frame(ReadOnlySpan<short> sample_data, QOA_Desc qoa, uint frame_len, Stream outputStream) 
	{
		uint channels = qoa.channels;

		uint slices = (frame_len + QOA_SLICE_LEN - 1) / QOA_SLICE_LEN;
		uint frame_size = QOA_FRAME_SIZE(channels, slices);
		int[] prev_scalefactor = new int[QOA_MAX_CHANNELS];

		/* Write the frame header */
		qoa_write_u64((
			(ulong)qoa.channels   << 56 |
			(ulong)qoa.samplerate << 32 |
			(ulong)frame_len      << 16 |
			(ulong)frame_size
		), outputStream);

		
		for (uint c = 0; c < channels; c++) 
		{
			/* Write the current LMS state */
			ulong weights = 0;
			ulong history = 0;
			for (int i = 0; i < QOA_LMS_LEN; i++) 
			{
				history <<= 16;
				ushort lHistory = (ushort)(qoa.lms[c].history[i] & 0xffff);
				history |= lHistory;

				weights <<= 16;
				ushort lWeight = (ushort)(qoa.lms[c].weights[i] & 0xffff);
				weights |= lWeight;
			}
			qoa_write_u64(history, outputStream);
			qoa_write_u64(weights, outputStream);
		}

		/* We encode all samples with the channels interleaved on a slice level.
		E.g. for stereo: (ch-0, slice 0), (ch 1, slice 0), (ch 0, slice 1), ...*/
		for (uint sample_index = 0; sample_index < frame_len; sample_index += QOA_SLICE_LEN) 
		{
			for (uint c = 0; c < channels; c++) 
			{
				uint slice_len = (uint)qoa_clamp((int)QOA_SLICE_LEN, 0, (int)(frame_len - sample_index));
				uint slice_start = sample_index * channels + c;
				uint slice_end = (sample_index + slice_len) * channels + c;			

				/* Brute for search for the best scalefactor. Just go through all
				16 scalefactors, encode all samples for the current slice and 
				meassure the total squared error. */
				ulong best_rank = ulong.MaxValue;

				ulong best_slice = 0;
				QOA_LMS best_lms = new QOA_LMS();
				int best_scalefactor = 0;

				for (int sfi = 0; sfi < 16; sfi++) 
				{
					/* There is a strong correlation between the scalefactors of
					neighboring slices. As an optimization, start testing
					the best scalefactor of the previous slice first. */
					int scalefactor = (sfi + prev_scalefactor[c]) % 16;

					/* We have to reset the LMS state to the last known good one
					before trying each scalefactor, as each pass updates the LMS
					state when encoding. */
					QOA_LMS lms = qoa.lms[c];
					ulong slice = (ulong)scalefactor;
					ulong current_rank = 0;

					for (uint si = slice_start; si < slice_end; si += channels) 
					{
						int sample = sample_data[(int)si];
						int predicted = qoa_lms_predict(lms);

						int residual = sample - predicted;
						int scaled = qoa_div(residual, scalefactor);
						int clamped = qoa_clamp(scaled, -8, 8);
						int quantized = qoa_quant_tab[clamped + 8];
						int dequantized = qoa_dequant_tab[scalefactor, quantized];
						int reconstructed = qoa_clamp_s16(predicted + dequantized);

						/* If the weights have grown too large, we introduce a penalty
						here. This prevents pops/clicks in certain problem cases */
						int weights_penalty = ((
							lms.weights[0] * lms.weights[0] + 
							lms.weights[1] * lms.weights[1] + 
							lms.weights[2] * lms.weights[2] + 
							lms.weights[3] * lms.weights[3]
						) >> 18) - 0x8ff;
						if (weights_penalty < 0)
						{
							weights_penalty = 0;
						}

						long error = (sample - reconstructed);
						ulong error_sq = (ulong)(error * error);

						current_rank += error_sq + (ulong)(weights_penalty * weights_penalty);

						if (current_rank > best_rank)
						{
							break;
						}

						qoa_lms_update(lms, reconstructed, dequantized);
						slice = (slice << 3) | (uint)quantized;
					}

					if (current_rank < best_rank) 
					{
						best_rank = current_rank;
						best_slice = slice;
						best_lms = lms;
						best_scalefactor = scalefactor;
					}
				}

				prev_scalefactor[c] = best_scalefactor;

				qoa.lms[c] = best_lms;

				/* If this slice was shorter than QOA_SLICE_LEN, we have to left-
				shift all encoded data, to ensure the rightmost bits are the empty
				ones. This should only happen in the last frame of a file as all
				slices are completely filled otherwise. */
				best_slice <<= (int)(QOA_SLICE_LEN - slice_len) * 3;
				qoa_write_u64(best_slice, outputStream);
			}
		}
	}

	public void qoa_encode(Stream outputStream, short[] sample_data, QOA_Desc qoa, uint out_len) 
	{
		if (qoa.samples == 0 || qoa.samplerate == 0 || qoa.samplerate > 0xffffff || qoa.channels == 0 || qoa.channels > QOA_MAX_CHANNELS) 
		{
			throw new Exception("");
		}

		/* Calculate the encoded size and allocate */
		uint num_frames = (qoa.samples + QOA_FRAME_LEN-1) / QOA_FRAME_LEN;
		uint num_slices = (qoa.samples + QOA_SLICE_LEN-1) / QOA_SLICE_LEN;
		uint encoded_size = 8 +                    /* 8 byte file header */
			num_frames * 8 +                               /* 8 byte frame headers */
			num_frames * QOA_LMS_LEN * 4 * qoa.channels + /* 4 * 4 bytes lms state per channel */
			num_slices * 8 * qoa.channels;                /* 8 byte slices */

		for (uint c = 0; c < qoa.channels; c++) 
		{
			/* Set the initial LMS weights to {0, 0, -1, 2}. This helps with the 
			prediction of the first few ms of a file. */
			qoa.lms[c].weights[0] = 0;
			qoa.lms[c].weights[1] = 0;
			qoa.lms[c].weights[2] = -(1<<13);
			qoa.lms[c].weights[3] =  (1<<14);

			/* Explicitly set the history samples to 0, as we might have some
			garbage in there. */
			for (int i = 0; i < QOA_LMS_LEN; i++) 
			{
				qoa.lms[c].history[i] = 0;
			}
		}


		/* Encode the header and go through all frames */
		qoa_encode_header(qoa, outputStream);

		uint frame_len = QOA_FRAME_LEN;
		for (uint sample_index = 0; sample_index < qoa.samples; sample_index += frame_len) 
		{
			frame_len = (uint)qoa_clamp((int)QOA_FRAME_LEN, 0, (int)(qoa.samples - sample_index));		
			ReadOnlySpan<short> frame_samples = new ReadOnlySpan<short>(sample_data, (int)(sample_index * qoa.channels), (int)(qoa.channels * frame_len));
			qoa_encode_frame(frame_samples, qoa, frame_len, outputStream);
		}
	}

	public uint qoa_max_frame_size(QOA_Desc qoa) 
	{
		return QOA_FRAME_SIZE(qoa.channels, QOA_SLICES_PER_FRAME);
	}

	public QOA_Desc qoa_decode_header(Stream inputStream, int size) 
	{
		/*if (size < QOA_MIN_FILESIZE)
		{
			throw new Exception("");
		}*/

		/* Read the file header, verify the magic number ('qoaf') and read the 
		total number of samples. */
		ulong file_header = qoa_read_u64(inputStream);

		if ((file_header >> 32) != QOA_MAGIC)
		{
			throw new Exception("");
		}

		QOA_Desc qoa = new QOA_Desc();

		qoa.samples = (uint)(file_header & 0xffffffff);
		if (qoa.samples == 0)
		{
			throw new Exception("");
		}

		/* Peek into the first frame header to get the number of channels and
		the samplerate. */
		ulong frame_header = qoa_read_u64(inputStream);
		inputStream.Seek(-8, SeekOrigin.Current);
		qoa.channels   = (uint)((frame_header >> 56) & 0x0000ff);
		qoa.samplerate = (uint)((frame_header >> 32) & 0xffffff);

		if (qoa.channels == 0 || qoa.samples == 0 || qoa.samplerate == 0)
		{
			throw new Exception("");
		}

		return qoa;
	}

	public void qoa_decode_frame(Stream inputStream, QOA_Desc qoa, Span<short> decodedOutput, out uint frame_len) 
	{
		frame_len = 0;

		/*if (size < 8 + QOA_LMS_LEN * 4 * qoa.channels)
		{
			throw new Exception();
		}*/

		/* Read and verify the frame header */
		ulong frame_header = qoa_read_u64(inputStream);
		uint channels   = (uint)((frame_header >> 56) & 0x0000ff);
		uint samplerate = (uint)((frame_header >> 32) & 0xffffff);
		uint samples    = (uint)((frame_header >> 16) & 0x00ffff);
		uint frame_size = (uint)((frame_header      ) & 0x00ffff);

		uint data_size = frame_size - 8 - QOA_LMS_LEN * 4 * channels;
		uint num_slices = data_size / 8;
		uint max_total_samples = num_slices * QOA_SLICE_LEN;

		if (channels != qoa.channels)
		{
			throw new InvalidOperationException($"Channels counts do not match, frame says: {channels} channel(s) vs. header says {qoa.channels} channel(s)");
		}
		else if (samplerate != qoa.samplerate )
		{
			throw new InvalidOperationException($"Samplerate do not match, frame says: {samplerate} sample(s) vs. header says {qoa.samplerate} samples(s)");
		}
		else if (samples * channels > max_total_samples)
		{
			throw new InvalidOperationException($"Max total samples value: {max_total_samples} is smaller than samples * channels {samples * channels}");
		}

		/* Read the LMS state: 4 x 2 bytes history, 4 x 2 bytes weights per channel */
		for (uint c = 0; c < channels; c++)
		{
			ulong history = qoa_read_u64(inputStream);
			ulong weights = qoa_read_u64(inputStream);
			for (int i = 0; i < QOA_LMS_LEN; i++)
			{
				qoa.lms[c].history[i] = (short)(history >> 48);
				history <<= 16;
				qoa.lms[c].weights[i] = (short)(weights >> 48);
				weights <<= 16;
			}
		}

		/* Decode all slices for all channels in this frame */
		for (uint sample_index = 0; sample_index < samples; sample_index += QOA_SLICE_LEN)
		{
			for (uint c = 0; c < channels; c++)
			{
				ulong slice = qoa_read_u64(inputStream);

				int scalefactor = (int)((slice >> 60) & 0xf);
				slice <<= 4;
				uint slice_start = sample_index * channels + c;
				uint slice_end = (uint)qoa_clamp((int)(sample_index + QOA_SLICE_LEN), 0, (int)samples) * channels + c;

				for (uint si = slice_start; si < slice_end; si += channels)
				{
					int predicted = qoa_lms_predict(qoa.lms[c]);
					int quantized = (int)((slice >> 61) & 0x7);
					int dequantized = qoa_dequant_tab[scalefactor, quantized];
					int reconstructed = qoa_clamp_s16(predicted + dequantized);
					
					decodedOutput[(int)si] = (short)reconstructed;
					slice <<= 3;

					qoa_lms_update(qoa.lms[c], reconstructed, dequantized);
				}
			}
		}

		frame_len = samples;
	}

	public short[] qoa_decode(Stream inputStream, int size, out QOA_Desc qoa)
	{
		qoa = qoa_decode_header(inputStream, size);

		/* Calculate the required size of the sample buffer and allocate */
		int total_samples = (int)(qoa.samples * qoa.channels);
		short[] sample_data = new short[total_samples];

		uint sample_index = 0;
		uint frame_size = 0;

		/* Decode all frames */
		do 
		{
			Span<short> decodedOutputSpan = new Span<short>(sample_data, (int)(sample_index * qoa.channels), (int)(QOA_SLICE_LEN * qoa.channels));
			qoa_decode_frame(inputStream, qoa, decodedOutputSpan, out uint frame_len);

			sample_index += frame_len;
		} 
		while (frame_size > 0 && sample_index < qoa.samples);

		qoa.samples = sample_index;
		return sample_data;
	}

	public void DecodeToWav(Stream inputStream, Stream outputSteam)
	{
		short[] sampleData = qoa_decode(inputStream, 0, out QOA_Desc qoa);
		WriteWav(outputSteam, sampleData, qoa);
	}

	public string DecodeHeaderToText(Stream inputStream)
	{
		QOA_Desc header = qoa_decode_header(inputStream, 0);
		return $"Channels: {header.channels} samplerate: {header.samplerate} total samples: {header.samples}";
	}

	private static void WriteWav(Stream outputSteam, short[] sampleData, QOA_Desc desc)
	{
		uint data_size = desc.samples * desc.channels * sizeof(short);
		uint samplerate = desc.samplerate;
		const short bits_per_sample = 16;
		short channels = (short)desc.channels;
		using (var writer = new BinaryWriter(outputSteam))
		{
			// All data will be written in little endian format
			writer.Write("RIFF"u8); // RIFF marker
			writer.Write(data_size + 44 - 8); // File size 
			writer.Write("WAVE"u8); // File Type Header
			writer.Write("fmt "u8); // Mark the format section
			writer.Write((uint)16); // Chunk size. Always 16 with these WAV files 
			writer.Write((short)1); // Type of format (PCM integer)
			writer.Write(channels); // Number of Channels
			writer.Write(samplerate); // Sample Rate
			writer.Write(channels * samplerate * bits_per_sample / 8); // Bytes per second
			writer.Write((short)(channels * bits_per_sample / 8)); // Bytes per block
			writer.Write(bits_per_sample); // Bits per sample
			writer.Write("data"u8); //"data" chunk header. Marks the beginning of the data section.    
			writer.Write(data_size); // Size of the data

			foreach (short val in sampleData)
			{
				writer.Write(BitConverter.GetBytes(val));
			}
		}
	}
}
