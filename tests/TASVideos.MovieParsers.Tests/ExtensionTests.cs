﻿using TASVideos.MovieParsers.Extensions;

namespace TASVideos.MovieParsers.Tests;

[TestClass]
public class ExtensionTests
{
	[TestMethod]
	[DataRow(null, null)]
	[DataRow("", null)]
	[DataRow(" ", null)]
	[DataRow("1", 1)]
	[DataRow("0", 0)]
	[DataRow("-1", null)]
	[DataRow("1.1", null)]
	[DataRow("99999999999", null)]
	[DataRow("NotANumber", null)]
	public void ToPositiveInt(string val, int? expected)
	{
		var actual = val.ToPositiveInt();
		Assert.AreEqual(expected, actual);
	}

	[TestMethod]
	[DataRow(null, null)]
	[DataRow("", null)]
	[DataRow(" ", null)]
	[DataRow("1", 1L)]
	[DataRow("0", 0L)]
	[DataRow("-1", null)]
	[DataRow("1.1", null)]
	[DataRow("99999999999", 99999999999L)]
	[DataRow("9999999999999999999", null)]
	[DataRow("NotANumber", null)]
	public void ToPositiveLong(string val, long? expected)
	{
		var actual = val.ToPositiveLong();
		Assert.AreEqual(expected, actual);
	}

	[TestMethod]
	[DataRow(null, false)]
	[DataRow("", false)]
	[DataRow(" ", false)]
	[DataRow("1", true)]
	[DataRow("0", false)]
	[DataRow("true", true)]
	[DataRow("True", true)]
	[DataRow("truE", true)]
	[DataRow("false", false)]
	[DataRow("False", false)]
	[DataRow("falsE", false)]
	public void ToBool(string val, bool expected)
	{
		var actual = val.ToBool();
		Assert.AreEqual(expected, actual);
	}
}
