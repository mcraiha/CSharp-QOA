using QOA;

namespace tests;

public class WavHelperTests
{
	[SetUp]
	public void Setup()
	{
	}

	[Test, Description("Read data from mono 48 000 Hz WAV audio file")]
	public void ReadWavTest()
	{
		// Arrange
		string input_WAV_Filename = "samples/car_trunk_close.qoa.wav";
		uint expectedSamplerate = 48_000;
		uint expectedSampleCount = 35530;
		uint expectedChannelsCount = 1;

		// Act
		short[] sampledata = Array.Empty<short>();
		(uint channels, uint samplerate, uint samples) specs;
		using (FileStream inputStream = File.OpenRead(input_WAV_Filename))
		{
			sampledata = WavHelper.ReadWav(inputStream, out specs);
		}

		// Assert
		Assert.That(expectedSampleCount, Is.EqualTo(sampledata.Length));
		Assert.That(expectedSamplerate, Is.EqualTo(specs.samplerate));
		Assert.That(expectedSampleCount, Is.EqualTo(specs.samples));
		Assert.That(expectedChannelsCount, Is.EqualTo(specs.channels));
	}
}