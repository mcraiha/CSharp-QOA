using System;
using System.IO;

using QOALib;

namespace CliToolQOA;

internal class Program
{
	static void Main(string[] args)
	{
		if (args.Length == 0)
		{
			Console.WriteLine("More parameters are needed!");
			Console.WriteLine();
			Console.WriteLine("1. Get QOA info: input.qoa");
			Console.WriteLine("2. Convert WAV to QOA (lossy operation): input.wav output.qoa");
			Console.WriteLine("3. Convert QOA to WAV: input.qoa output.wav");
			return;
		}

		string inputFilename = args[0];

		if (!File.Exists(inputFilename))
		{
			Console.Error.WriteLine($"Input file: {inputFilename} does not exist!");
			return;
		}

		// If only one parameter is given, assume it is .qoa file
		if (args.Length == 1)
		{
			DecodeHeader(inputFilename);
			
			return;
		}

		byte[] firstFourBytes = new byte[4];

		using (FileStream fs = File.OpenRead(inputFilename))
		{
			int readAmount = fs.Read(firstFourBytes, 0, 4);
			if (readAmount != 4)
			{
				Console.WriteLine($"Could not read 4 bytes from {inputFilename}"!);
				return;
			}
		}

		bool inputQOA = QOA.CheckFourCC(firstFourBytes);
		
		string outputFilename = args[1];

		if (File.Exists(outputFilename))
		{
			Console.Error.WriteLine($"Output file: {outputFilename} does already exist. This tool does NOT overwrite files!");
			return;
		}

		if (inputQOA)
		{
			// Do QOA -> WAV decoding
			DecodeQOAtoWAV(inputFilename, outputFilename);
		}
		else
		{
			// Do WAV -> QOA encoding (lossy operation)
			EncodeWAVtoQOA(inputFilename, outputFilename);
		}

		Console.WriteLine($"Wrote {outputFilename}");
	}

	private static void DecodeHeader(string inputFilename)
	{
		Console.WriteLine($"Decoding header from {inputFilename}");

		QOA qoa = new QOA();

		using (FileStream inputStream = File.OpenRead(inputFilename))
		{
			Console.WriteLine(qoa.DecodeHeaderToText(inputStream));
		}

		Console.WriteLine($"Trying to decode the file");
		using (FileStream inputStream = File.OpenRead(inputFilename))
		{
			using (MemoryStream nullStream = new MemoryStream())
			{
				qoa.DecodeToWav(inputStream, nullStream);
			}
		}

		Console.WriteLine("Decoding succesful");
	}

	private static void DecodeQOAtoWAV(string inputQOAFilename, string outputWAVFilename)
	{
		Console.WriteLine($"Trying to decode {inputQOAFilename} to {outputWAVFilename}");

		QOA qoa = new QOA();

		using (FileStream inputStream = File.OpenRead(inputQOAFilename))
		{
			using (FileStream outputStream = File.Create(outputWAVFilename))
			{
				qoa.DecodeToWav(inputStream, outputStream);
			}
		}

		Console.WriteLine("Decoding completed succesfully");
	}

	private static void EncodeWAVtoQOA(string inputWAVFilename, string outputQOAFilename)
	{
		Console.WriteLine($"Trying to encode {inputWAVFilename} to {outputQOAFilename} (lossy operation)");

		QOA qoa = new QOA();

		using (FileStream inputStream = File.OpenRead(inputWAVFilename))
		{
			using (FileStream outputStream = File.Create(outputQOAFilename))
			{
				qoa.EncodeWAVToQOA(inputStream, outputStream);
			}
		}

		Console.WriteLine("Encoding completed succesfully");
	}
}