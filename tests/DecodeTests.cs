using QOA;

namespace tests;

public class DecodeTests
{
	[SetUp]
	public void Setup()
	{
	}

	[Test, Description("Decode an existing mono 48 000 Hz QOA audio file")]
	public void Test1()
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
}