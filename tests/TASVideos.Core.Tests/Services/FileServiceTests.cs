﻿////using System.IO;
////using System.IO.Compression;
////using System.Linq;
////using System.Threading.Tasks;
////using Microsoft.VisualStudio.TestTools.UnitTesting;
////using TASVideos.Core.Services;
////using TASVideos.Data.Entity;
////using TASVideos.Test.MovieParsers;

////namespace TASVideos.Core.Tests.Services
////{
////	[TestClass]
////	public class FileServiceTests : BaseParserTests
////	{
////		private readonly FileService _fileService = new ();

////		public override string ResourcesPath { get; } = "TASVideos.Test.MovieParsers.Bk2SampleFiles.";

////		[TestMethod]
////		public async Task CopyZip_BasicTest()
////		{
////			// Arrange
////			const string newName = "1M";
////			var stream = Embedded("2Frames.zip");
////			await using var ms = new MemoryStream();
////			await stream.CopyToAsync(ms);
////			var bytes = ms.ToArray();

////			// Act
////			var result = await _fileService.CopyZip(bytes, newName);

////			// Assert
////			Assert.IsNotNull(result);
////			Assert.IsTrue(result.Any());

////			await using var resultStream = new MemoryStream(result);
////			using var resultZipArchive = new ZipArchive(resultStream, ZipArchiveMode.Read);

////			Assert.AreEqual(1, resultZipArchive.Entries.Count);
////			var entry = resultZipArchive.Entries.Single();
////			Assert.AreEqual(newName, entry.Name);
////		}

////		[TestMethod]
////		public async Task Compress_ReturnsGzipIfSmaller()
////		{
////			// Arrange
////			var bytes = Enumerable.Repeat(0, 100).Select(i => (byte)i).ToArray();

////			// Act
////			var result = await _fileService.Compress(bytes);

////			// Assert
////			Assert.IsNotNull(result);
////			Assert.AreEqual(bytes.Length, result.OriginalSize);
////			Assert.IsTrue(result.CompressedSize < result.OriginalSize);
////			Assert.AreEqual(Compression.Gzip, result.Type);
////		}

////		[TestMethod]
////		public async Task Compress_ReturnsUncompressedIfNotSmaller()
////		{
////			// Arrange
////			var bytes = new byte[] { 31, 139, 8, 0, 0, 0, 0, 0, 0, 10 };

////			// Act
////			var result = await _fileService.Compress(bytes);

////			// Assert
////			Assert.IsNotNull(result);
////			Assert.AreEqual(bytes.Length, result.OriginalSize);
////			Assert.IsTrue(result.CompressedSize >= result.OriginalSize);
////			Assert.AreEqual(Compression.None, result.Type);
////		}
////	}
////}