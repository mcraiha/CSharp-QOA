using QOALib;

namespace tests;

public class WavHelperTests
{
	[SetUp]
	public void Setup()
	{
	}

	[Test, Description("Read data from mono 48 000 Hz WAV audio file")]
	public void ReadWavMono1Test()
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

	[Test, Description("Read data from mono 48 000 Hz WAV audio file")]
	public void ReadWavMono2Test()
	{
		// Arrange
		string input_WAV_Filename = "samples/car_trunk_close.wav";
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

	[Test, Description("Read data from stereo 48 000 Hz WAV audio file")]
	public void ReadWavStereo1Test()
	{
		// Arrange
		string input_WAV_Filename = "samples/sting_xp_level_up_orch_01.wav";
		uint expectedSamplerate = 48_000;
		uint expectedTotalSampleCount = 191_942;
		uint expectedSamplesPerChannelCount = expectedTotalSampleCount / 2;
		uint expectedChannelsCount = 2;

		// Act
		short[] sampledata = Array.Empty<short>();
		(uint channels, uint samplerate, uint samples) specs;
		using (FileStream inputStream = File.OpenRead(input_WAV_Filename))
		{
			sampledata = WavHelper.ReadWav(inputStream, out specs);
		}

		// Assert
		Assert.That(expectedTotalSampleCount, Is.EqualTo(sampledata.Length));
		Assert.That(expectedSamplerate, Is.EqualTo(specs.samplerate));
		Assert.That(expectedSamplesPerChannelCount, Is.EqualTo(specs.samples));
		Assert.That(expectedChannelsCount, Is.EqualTo(specs.channels));
	}
}