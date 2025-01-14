# CSharp-QOA

Managed .NET implementation of [QOA](https://github.com/phoboslab/qoa) (Quite OK Audio Format) written in C#

## Build status
![](https://github.com/mcraiha/CSharp-QOA/actions/workflows/dotnet.yml/badge.svg)

## Nuget
[![NuGet version (LibQOA)](https://img.shields.io/nuget/v/LibQOA.svg?style=flat-square)](https://www.nuget.org/packages/LibQOA/)

## Why?

Because I needed this for my personal project

## Content

[src](src) folder contains the actual library source code to do QOA encoding/decoding

[tests](tests) folder contains unit test cases

[clitool](clitool) folder contains command-line tool source code for encoding/decoding QOA files

## How to use?

If you want to decode .qoa file to .wav, then you can use following code

```cs
using QOALib;

QOA qoa = new QOA();

using (FileStream inputStream = File.OpenRead(inputQOAFilename))
{
    using (FileStream outputStream = File.Create(outputWAVFilename))
    {
        qoa.DecodeToWav(inputStream, outputStream);
    }
}
```

If you want to encode 16 bit .wav file to .qoa, then you can use following code

```cs
using QOALib;

QOA qoa = new QOA();

using (FileStream inputStream = File.OpenRead(inputWAVFilename))
{
    using (FileStream outputStream = File.Create(outputQOAFilename))
    {
        qoa.EncodeWAVToQOA(inputStream, outputStream);
    }
}
```

## License

All the code is licensed under [MIT License](LICENSE) because that is the license QOA uses.

All the audio samples in [tests/samples](tests/samples) folder are under [Creative Commons Attribution 4.0 License](https://creativecommons.org/licenses/by/4.0/) and they are from [Oculus Audio Pack 1](https://developers.meta.com/horizon/downloads/package/oculus-audio-pack-1/).
