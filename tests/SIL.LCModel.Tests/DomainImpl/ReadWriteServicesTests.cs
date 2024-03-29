// Copyright (c) 2017 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System.IO;
using System.Xml.Linq;
using NUnit.Framework;

namespace SIL.LCModel.DomainImpl
{
	/// <summary>
	/// Read write services tests.
	/// </summary>
	[TestFixture]
	public class ReadWriteServicesTests
	{
		/// <summary>
		/// Tests the LoadDateTime method
		/// </summary>
		[TestCase("2016-7-7 4:2:2", ExpectedResult = "2016-07-07T04:02:02.0000000Z")]
		[TestCase("2016-7-7 4.2.2", ExpectedResult = "2016-07-07T04:02:02.0000000Z")] // European format time (LT-20698)
		[TestCase("2016.7.7 4:2:2", ExpectedResult = "2016-07-07T04:02:02.0000000Z")] // European format date (LT-20698)
		[TestCase("2016-7-7 4:2:2.0", ExpectedResult = "2016-07-07T04:02:02.0000000Z")]
		[TestCase("2016-7-7 4:2:2.007", ExpectedResult = "2016-07-07T04:02:02.0070000Z")]
		[TestCase("2016-07-07 04:02:02.700", ExpectedResult = "2016-07-07T04:02:02.7000000Z")]
		[TestCase("2016-7-7 4:2:2.700", ExpectedResult = "2016-07-07T04:02:02.7000000Z")]
		[TestCase("2016-7-7 4:2:2.07", ExpectedResult = "2016-07-07T04:02:02.0700000Z")] // Verify that <3-digit decimals are parsed correctly
		[TestCase("2016-7-7 4:2:2.70", ExpectedResult = "2016-07-07T04:02:02.7000000Z")] // Verify that <3-digit decimals are parsed correctly
		[TestCase("2016-7-7 4:2:2.7", ExpectedResult = "2016-07-07T04:02:02.7000000Z")] // Verify that <3-digit decimals are parsed correctly
		public string LoadDateTime(string datetime)
		{
			using (var stringReader = new StringReader(string.Format("<DateModified val=\"{0}\"/>", datetime)))
			{
				var reader = XElement.Load(stringReader);

				return ReadWriteServices.LoadDateTime(reader).ToUniversalTime().ToString("O");
			}
		}
	}
}
