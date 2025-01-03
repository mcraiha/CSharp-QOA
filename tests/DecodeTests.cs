using QOA;

namespace tests;

public class DecodeTests
{
	[SetUp]
	public void Setup()
	{
	}

	[Test, Description("Decode an existing mono 48 000 Hz QOA audio file")]
	public void Decode_48000_Hz_Mono_QOA()
	{
		// Arrange
		string input_WAV_Filename = "samples/car_trunk_close.qoa.wav";
		byte[] expected = File.ReadAllBytes(input_WAV_Filename);

		string input_QOA_Filename = "samples/car_trunk_close.qoa";

		QOA.QOA qoa = new QOA.QOA();

		// Act
		byte[]? actual = null;
		using (FileStream inputStream = File.OpenRead(input_QOA_Filename))
		{
			using (MemoryStream decodedStream = new MemoryStream())
			{
				qoa.DecodeToWav(inputStream, decodedStream);
				actual = decodedStream.ToArray();
			}
		}

		// Assert
		Assert.That(actual, Is.EqualTo(expected).AsCollection);
	}

	[Test, Description("Decode an existing stereo 48 000 Hz QOA audio file")]
	public void Decode_48000_Hz_Stereo_QOA()
	{
		// Arrange
		string input_WAV_Filename = "samples/sting_xp_level_up_orch_01.qoa.wav";
		byte[] expected = File.ReadAllBytes(input_WAV_Filename);

		string input_QOA_Filename = "samples/sting_xp_level_up_orch_01.qoa";

		QOA.QOA qoa = new QOA.QOA();

		// Act
		byte[]? actual = null;
		using (FileStream inputStream = File.OpenRead(input_QOA_Filename))
		{
			using (MemoryStream decodedStream = new MemoryStream())
			{
				qoa.DecodeToWav(inputStream, decodedStream);
				actual = decodedStream.ToArray();
			}
		}

		// Assert
		Assert.That(actual, Is.EqualTo(expected).AsCollection);
	}

	[Test, Description("Test that check fourCC works correctly")]
	public void CheckFourCCTest()
	{
		// Arrange
		string input_QOA_Filename = "samples/car_trunk_close.qoa";
		byte[] validBytes = File.ReadAllBytes(input_QOA_Filename);
		byte[] incorrectBytes1 = new byte[] {};
		byte[] incorrectBytes2 = new byte[] { 1, 2, 3, 4 };

		// Act
		bool checkResult1 = QOA.QOA.CheckFourCC(validBytes);

		bool checkResult2 = QOA.QOA.CheckFourCC(null!);
		bool checkResult3 = QOA.QOA.CheckFourCC(incorrectBytes1);
		bool checkResult4 = QOA.QOA.CheckFourCC(incorrectBytes2);

		// Assert
		Assert.That(checkResult1, Is.True);

		Assert.That(checkResult2, Is.False);
		Assert.That(checkResult3, Is.False);
		Assert.That(checkResult4, Is.False);
	}
}