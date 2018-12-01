using System;
using System.IO;
using System.Reflection;
using System.Resources;
using BenchmarkDotNet.Attributes;
using liblistfile.NodeTree;
using Moq;
using Warcraft.MPQ;
using Warcraft.MPQ.FileInfo;
using FileAttributes = Warcraft.MPQ.Attributes.FileAttributes;

namespace liblistfile.Benchmark
{
    public class Benchmark
    {
        private IPackage SamplePackage { get; set; }

        private ListfileDictionary Dictionary { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            string[] fileList;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("liblistfile.Benchmark.Data.sample-data.txt"))
            {
                using (var sr = new StreamReader(stream))
                {
                    fileList = sr.ReadToEnd().Split('\n');
                }
            }

            var mockedPackage = new Mock<IPackage>();
            mockedPackage.Setup(p => p.HasFileList()).Returns(true);
            mockedPackage.Setup(p => p.GetFileList()).Returns(fileList);

            this.SamplePackage = mockedPackage.Object;

            var dictionaryData = File.ReadAllBytes(Path.Combine("Dictionary", "dictionary.dic"));
            this.Dictionary = new ListfileDictionary(dictionaryData);
        }

        [Benchmark]
        public void OldAlgorithm()
        {
            var builder = new MultiPackageNodeTreeBuilder(this.Dictionary);
            builder.ConsumePackage("sample", this.SamplePackage);

            builder.Build();
        }
    }
}
