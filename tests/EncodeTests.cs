using QOALib;

namespace tests;

public class EncodeTests
{
	[SetUp]
	public void Setup()
	{
	}

	[Test, Description("Encode an existing mono 48 000 Hz WAV audio file to QOA")]
	public void Encode_48000_Hz_Mono_QOA()
	{
		// Arrange
		string input_WAV_Filename = "samples/car_trunk_close.wav";

		string input_QOA_Filename = "samples/car_trunk_close.qoa";
		byte[] expected = File.ReadAllBytes(input_QOA_Filename);

		QOA qoa = new QOA();

		// Act
		byte[]? actual = null;
		using (FileStream inputStream = File.OpenRead(input_WAV_Filename))
		{
			using (MemoryStream encodedStream = new MemoryStream())
			{
				qoa.EncodeWAVToQOA(inputStream, encodedStream);
				actual = encodedStream.ToArray();
			}
		}

		// Assert
		Assert.That(actual, Is.EqualTo(expected).AsCollection);
	}

	[Test, Description("Encode an existing stereo 48 000 Hz WAV audio file to QOA")]
	public void Encode_48000_Hz_Stereo_QOA()
	{
		// Arrange
		string input_WAV_Filename = "samples/sting_xp_level_up_orch_01.wav";

		string input_QOA_Filename = "samples/sting_xp_level_up_orch_01.qoa";
		byte[] expected = File.ReadAllBytes(input_QOA_Filename);

		QOA qoa = new QOA();

		// Act
		byte[]? actual = null;
		using (FileStream inputStream = File.OpenRead(input_WAV_Filename))
		{
			using (MemoryStream encodedStream = new MemoryStream())
			{
				qoa.EncodeWAVToQOA(inputStream, encodedStream);
				actual = encodedStream.ToArray();
			}
		}

		// Assert
		Assert.That(actual, Is.EqualTo(expected).AsCollection);
	}
}