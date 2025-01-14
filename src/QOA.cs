using System.Buffers.Binary; // For BinaryPrimitives.ReverseEndianness

namespace QOALib;

/// <summary>
/// Structure for QOA Least mean squares (LMS)
/// </summary>
public struct QOA_LMS
{
	/// <summary>
	/// Four previous history points, most recent last
	/// </summary>
	public int[] history = new int[QOA.QOA_LMS_LEN];
	/// <summary>
	/// Four adjusting weights, most recent last
	/// </summary>
	public int[] weights = new int[QOA.QOA_LMS_LEN];

	/// <summary>
	/// Constructor without parameters
	/// </summary>
	public QOA_LMS()
	{

	}

	/// <summary>
	/// Copy constructor
	/// </summary>
	/// <param name="existing">Existing struct where values will be copied from</param>
	public QOA_LMS(QOA_LMS existing)
	{
		for (int i = 0; i < QOA.QOA_LMS_LEN; i++) 
		{
			history[i] = existing.history[i];
			weights[i] = existing.weights[i];
		}
	}
}

/// <summary>
/// Description of QOA data
/// </summary>
public struct QOA_Desc
{
	/// <summary>
	/// How many channels
	/// </summary>
	public uint channels;

	/// <summary>
	/// Samplerate
	/// </summary>
	public uint samplerate;

	/// <summary>
	/// How many samples
	/// </summary>
	public uint samples;

	/// <summary>
	/// Least mean squares (LMS) for each channel (some channels might not be used)
	/// </summary>
	public QOA_LMS[] lms = new QOA_LMS[QOA.QOA_MAX_CHANNELS] 
	{ 
		new QOA_LMS(), new QOA_LMS(), new QOA_LMS(), new QOA_LMS(), new QOA_LMS(), new QOA_LMS(), new QOA_LMS(), new QOA_LMS()
	};

	/// <summary>
	/// Constructor without paramaters
	/// </summary>
	public QOA_Desc()
	{

	}

	/// <summary>
	/// Constructor with channels, samplerate and samples given with tuple
	/// </summary>
	/// <param name="tuple">Tuple that contains channels, samplerate and samples</param>
	public QOA_Desc((uint channels, uint samplerate, uint samples) tuple)
	{
		this.channels = tuple.channels;
		this.samplerate = tuple.samplerate;
		this.samples = tuple.samples;
	}

	/// <summary>
	/// Constructor with channels, samplerate and samples
	/// </summary>
	/// <param name="channels">Channels</param>
	/// <param name="samplerate">Samplerate</param>
	/// <param name="samples">Samples</param>
	public QOA_Desc(uint channels, uint samplerate, uint samples)
	{
		this.channels = channels;
		this.samplerate = samplerate;
		this.samples = samples;
	}

	/// <summary>
	/// Verify input values, e.g. samples/samplerate/channels cannot be 0
	/// </summary>
	/// <exception cref="Exception"></exception>
	public void VerifyValues()
	{
		if (samples == 0) 
		{
			throw new Exception("Cannot process 0 samples!");
		}
		else if (samplerate == 0)
		{
			throw new Exception("Samplerate 0 is incorrect!");
		}
		else if (samplerate > 0xffffff)
		{
			throw new Exception($"Samplerate is too high ({samplerate}), max is {0xffffff}!");
		}
		else if (channels == 0)
		{
			throw new Exception("Cannot process 0 channels!");
		}
		else if (channels > QOA.QOA_MAX_CHANNELS)
		{
			throw new Exception($"Channel count is too high ({channels}), max is {QOA.QOA_MAX_CHANNELS}!");
		}
	}
}

/// <summary>
/// Class that will be used to do all encoding/decoding
/// </summary>
public sealed class QOA
{
	private const int QOA_MIN_FILESIZE = 16;

	/// <summary>
	/// Max number of channels
	/// </summary>
	public const int QOA_MAX_CHANNELS = 8;

	private const uint QOA_SLICE_LEN = 20;
	private const int QOA_SLICES_PER_FRAME = 256;

	private const uint QOA_FRAME_LEN = QOA_SLICES_PER_FRAME * QOA_SLICE_LEN;

	/// <summary>
	/// How long is Least mean squares (LMS) history
	/// </summary>
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

	private static int qoa_lms_predict(ref QOA_LMS lms)
	{
		int prediction = 0;

		for (int i = 0; i < QOA_LMS_LEN; i++) 
		{
			prediction += lms.weights[i] * lms.history[i];
		}

		return prediction >> 13;
	}

	private static void qoa_lms_update(ref QOA_LMS lms, int sample, int residual)
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
		n = n + (GreaterThanZero(v) - LesserThanZero(v)) - (GreaterThanZero(n) - LesserThanZero(n)); /* round away from 0 */
		return n;
	}

	private static int GreaterThanZero(int value)
	{
		if (value > 0)
		{
			return 1;
		}

		return 0;
	}

	private static int LesserThanZero(int value)
	{
		if (value < 0)
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

	/// <summary>
	/// Read 8 bytes as ulong
	/// </summary>
	/// <param name="stream">Stream for reading</param>
	/// <returns>ulong</returns>
	private static ulong qoa_read_u64(Stream stream)
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

	/// <summary>
	/// Encode header and write it to stream
	/// </summary>
	/// <param name="qoa">Given QOA_Desc</param>
	/// <param name="stream">Output stream</param>
	public void qoa_encode_header(QOA_Desc qoa, Stream stream) 
	{
		qoa_write_u64(((ulong)QOA_MAGIC << 32) | qoa.samples, stream);
	}

	/// <summary>
	/// Encode one frame
	/// </summary>
	/// <param name="sample_data">Sample data</param>
	/// <param name="qoa">Given QOA_Desc</param>
	/// <param name="frame_len">Frame length</param>
	/// <param name="outputStream">Output stream</param>
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
					QOA_LMS lms = new QOA_LMS(qoa.lms[c]);
					ulong slice = (ulong)scalefactor;
					ulong current_rank = 0;

					for (uint si = slice_start; si < slice_end; si += channels) 
					{
						int sample = sample_data[(int)si];
						int predicted = qoa_lms_predict(ref lms);

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

						qoa_lms_update(ref lms, reconstructed, dequantized);
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

	/// <summary>
	/// Encode all samples into QOA
	/// </summary>
	/// <param name="outputStream">Output stream</param>
	/// <param name="sample_data">Sample data</param>
	/// <param name="qoa">Given QOA_Desc</param>
	public void qoa_encode(Stream outputStream, short[] sample_data, QOA_Desc qoa) 
	{
		// Verify values
		qoa.VerifyValues();

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

	/// <summary>
	/// Decode QOA header
	/// </summary>
	/// <param name="inputStream">Input stream (QOA data)</param>
	/// <returns>QOA_Desc</returns>
	/// <exception cref="Exception"></exception>
	public QOA_Desc qoa_decode_header(Stream inputStream) 
	{
		/* Read the file header, verify the magic number ('qoaf') and read the 
		total number of samples. */
		ulong file_header = qoa_read_u64(inputStream);

		if ((file_header >> 32) != QOA_MAGIC)
		{
			throw new Exception($"FourCC should be '{QOA_MAGIC:X8}' but it is '{(file_header >> 32):X8}'");
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

	/// <summary>
	/// Decode one QOA frame
	/// </summary>
	/// <param name="inputStream">Input stream (QOA data)</param>
	/// <param name="qoa">QOA_Desc</param>
	/// <param name="decodedOutput">Decoded samples output buffer</param>
	/// <param name="frame_len">Lenght of decoded frame</param>
	/// <exception cref="InvalidOperationException"></exception>
	public void qoa_decode_frame(Stream inputStream, QOA_Desc qoa, Span<short> decodedOutput, out uint frame_len) 
	{
		frame_len = 0;

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
					int predicted = qoa_lms_predict(ref qoa.lms[c]);
					int quantized = (int)((slice >> 61) & 0x7);
					int dequantized = qoa_dequant_tab[scalefactor, quantized];
					int reconstructed = qoa_clamp_s16(predicted + dequantized);

					decodedOutput[(int)si] = (short)reconstructed;
					slice <<= 3;

					qoa_lms_update(ref qoa.lms[c], reconstructed, dequantized);
				}
			}
		}

		frame_len = samples;
	}

	/// <summary>
	/// Decode all samples from QOA data
	/// </summary>
	/// <param name="inputStream">Input stream (QOA data)</param>
	/// <param name="qoa">QOA_Desc</param>
	/// <returns>Samples as short array</returns>
	public short[] qoa_decode(Stream inputStream, out QOA_Desc qoa)
	{
		qoa = qoa_decode_header(inputStream);

		/* Calculate the required size of the sample buffer and allocate */
		int total_samples = (int)(qoa.samples * qoa.channels);
		short[] sample_data = new short[total_samples];

		uint sample_index = 0;

		/* Decode all frames */
		do 
		{
			int samplesPerFrame = (int)(QOA_SLICES_PER_FRAME * QOA_SLICE_LEN * qoa.channels);
			if (samplesPerFrame < total_samples)
			{
				total_samples -= samplesPerFrame;
			}
			else
			{
				samplesPerFrame = total_samples;
			}
			Span<short> decodedOutputSpan = new Span<short>(sample_data, (int)(sample_index * qoa.channels), samplesPerFrame);
			qoa_decode_frame(inputStream, qoa, decodedOutputSpan, out uint frame_len);

			sample_index += frame_len;
		} 
		while (sample_index < qoa.samples);

		qoa.samples = sample_index;
		return sample_data;
	}

	/// <summary>
	/// Encode WAV input to QOA
	/// </summary>
	/// <param name="inputStream">Input stream (WAV)</param>
	/// <param name="outputSteam">Output stream (QOA)</param>
	/// <exception cref="IOException">If streams have issues</exception>
	public void EncodeWAVToQOA(Stream inputStream, Stream outputSteam)
	{
		if (!inputStream.CanRead)
		{
			throw new IOException("Input stream must be readable!");
		}

		if (!outputSteam.CanWrite)
		{
			throw new IOException("Output stream must be writable!");
		}

		// Read needed data from WAV file
		short[] sampleData = WavHelper.ReadWav(inputStream, out (uint channels, uint samplerate, uint samples) specs);
		QOA_Desc desc = new QOA_Desc(specs);
		qoa_encode(outputSteam, sampleData, desc);
	}

	/// <summary>
	/// Decode QOA input to 16 bit WAV
	/// </summary>
	/// <param name="inputStream">Input stream (QOA)</param>
	/// <param name="outputSteam">Output stream (WAV)</param>
	/// <exception cref="IOException">If streams have issues</exception>
	public void DecodeToWav(Stream inputStream, Stream outputSteam)
	{
		if (!inputStream.CanRead)
		{
			throw new IOException("Input stream must be readable!");
		}

		if (!outputSteam.CanWrite)
		{
			throw new IOException("Output stream must be writable!");
		}

		short[] sampleData = qoa_decode(inputStream, out QOA_Desc qoa);
		WavHelper.Write16bitWav(outputSteam, sampleData, qoa.samples, qoa.channels, qoa.samplerate);
	}

	/// <summary>
	/// Decode header to text (for debugging purposes)
	/// </summary>
	/// <param name="inputStream">Input stream (QOA data)</param>
	/// <returns>String that contains channel count, samplerate and total amount of samples</returns>
	public string DecodeHeaderToText(Stream inputStream)
	{
		QOA_Desc header = qoa_decode_header(inputStream);
		return $"Channels: {header.channels} samplerate: {header.samplerate} total samples: {header.samples}";
	}

	/// <summary>
	/// Check that four first bytes are equal to QOA FourCC
	/// </summary>
	/// <param name="bytes">Byte array</param>
	/// <returns>True if they are equal; False otherwise</returns>
	public static bool CheckFourCC(byte[] bytes)
	{
		if (bytes == null || bytes.Length < 4)
		{
			return false;
		}

		return BitConverter.ToUInt32(bytes, 0) == BinaryPrimitives.ReverseEndianness(QOA_MAGIC);
	}
}
