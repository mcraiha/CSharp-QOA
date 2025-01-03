using System.Text;

namespace QOALib;

/// <summary>
/// Static helper class for reading and writing WAV data
/// </summary>
public static class WavHelper
{
	/// <summary>
	/// Write sample data to stream in WAV format
	/// </summary>
	/// <param name="outputSteam">Stream for writing</param>
	/// <param name="sampleData">All the sample data that will be written</param>
	/// <param name="samples">How many samples are there per channel</param>
	/// <param name="channels">How many channels are there</param>
	/// <param name="samplerate">Samplerate</param>
	public static void Write16bitWav(Stream outputSteam, short[] sampleData, uint samples, uint channels, uint samplerate)
	{
		uint data_size = samples * channels * sizeof(short);
		const short bits_per_sample = 16;
		using (var writer = new BinaryWriter(outputSteam))
		{
			// All data will be written in little endian format
			writer.Write("RIFF"u8); // RIFF marker
			writer.Write(data_size + 44 - 8); // File size 
			writer.Write("WAVE"u8); // File Type Header
			writer.Write("fmt "u8); // Mark the format section
			writer.Write((uint)16); // Chunk size. Always 16 with these WAV files 
			writer.Write((short)1); // Type of format (PCM integer)
			writer.Write((short)channels); // Number of Channels
			writer.Write(samplerate); // Sample Rate
			writer.Write((uint)(channels * samplerate * bits_per_sample / 8)); // Bytes per second
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

	/// <summary>
	/// Read data from WAV format stream
	/// </summary>
	/// <param name="inputStream">Input stream that will be read</param>
	/// <param name="specs">Output of WAV file specs as tuple. Audio channels count, samplerate and samples per channel</param>
	/// <returns>All samples as short array</returns>
	/// <exception cref="ArgumentException"></exception>
	public static short[] ReadWav(Stream inputStream, out (uint channels, uint samplerate, uint samples) specs)
	{		
		ReadOnlySpan<byte> wantedWave = "WAVE"u8;
		bool waveFound = SeekNext(inputStream, wantedWave);
		
		if (!waveFound)
		{
			throw new ArgumentException($"Input stream does not contain 'WAVE' file header section!");
		}

		ReadOnlySpan<byte> wantedFmt = "fmt "u8;
		bool fmtHeaderFound = SeekNext(inputStream, wantedFmt);

		if (!fmtHeaderFound)
		{
			throw new ArgumentException($"Input stream does not contain 'fmt ' file format section!");
		}

		// Ignore chunk size
		inputStream.Seek(4, SeekOrigin.Current);
		ushort audioFormat = 0;
		ushort channels = 0;
		uint sampleRate = 0;
		ushort bitsPerSample = 0;
		using (var reader = new BinaryReader(inputStream, Encoding.UTF8, leaveOpen: true))
		{
			audioFormat = reader.ReadUInt16();

			if (audioFormat != 1)
			{
				throw new ArgumentException($"Audio format: {audioFormat} is not supported!");
			}

			channels = reader.ReadUInt16();
			sampleRate = reader.ReadUInt32();
			// Ignore byte rate and block align
			inputStream.Seek(6, SeekOrigin.Current);
			bitsPerSample = reader.ReadUInt16();
		}

		ReadOnlySpan<byte> wantedData = "data"u8;
		bool dataFound = SeekNext(inputStream, wantedData);

		if (!dataFound)
		{
			throw new ArgumentException($"Input stream does not contain 'data' data section!");
		}

		using (var reader = new BinaryReader(inputStream))
		{
			uint dataSizeInBytes = reader.ReadUInt32();
			uint samples = (uint)(dataSizeInBytes / (channels * (bitsPerSample/8)));
			specs = new (channels, sampleRate, samples);

			short[] returnArray = new short[dataSizeInBytes / 2];

			for (int i = 0; i < dataSizeInBytes / 2; i++)
			{
				returnArray[i] = reader.ReadInt16();
			}

			return returnArray;
		}
	}

	private static bool SeekNext(Stream inputStream, ReadOnlySpan<byte> wantedBytes)
	{
		int currentByte = inputStream.ReadByte();

		int wantedIndex = 0;
		while (currentByte > -1)
		{
			if (wantedBytes[wantedIndex] == currentByte)
			{
				wantedIndex++;
				if (wantedIndex == wantedBytes.Length)
				{
					return true;
				}
			}
			else
			{
				wantedIndex = 0;
			}
			currentByte = inputStream.ReadByte();
		}

		return false;
	}
}