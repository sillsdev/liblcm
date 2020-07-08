// Copyright (c) 2016 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;

namespace SIL.LCModel.Core.Text
{
	[TestFixture]
	public class TsPropsFactoryTests
	{
		[Test]
		public void MakeProps_NonNullStyle_CreatesTextProps()
		{
			var tpf = new TsPropsFactory();
			ITsTextProps tps = tpf.MakeProps("Style", 2, 1);
			Assert.That(tps.IntPropCount, Is.EqualTo(1));
			int var;
			Assert.That(tps.GetIntPropValues((int) FwTextPropType.ktptWs, out var), Is.EqualTo(2));
			Assert.That(var, Is.EqualTo(1));
			Assert.That(tps.StrPropCount, Is.EqualTo(1));
			Assert.That(tps.GetStrPropValue((int) FwTextPropType.ktptNamedStyle), Is.EqualTo("Style"));
		}

		[Test]
		public void MakeProps_NullStyle_CreatesTextPropsWithoutStyle()
		{
			var tpf = new TsPropsFactory();
			ITsTextProps tps = tpf.MakeProps(null, 2, 1);
			Assert.That(tps.IntPropCount, Is.EqualTo(1));
			int var;
			Assert.That(tps.GetIntPropValues((int) FwTextPropType.ktptWs, out var), Is.EqualTo(2));
			Assert.That(var, Is.EqualTo(1));
			Assert.That(tps.StrPropCount, Is.EqualTo(0));
		}

		[Test]
		public void MakeProps_InvalidWS_Throws()
		{
			var tpf = new TsPropsFactory();
			Assert.That(() => tpf.MakeProps("Style", -1, -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		[Test]
		public void FromITsTextProps_IntAndStringProps_Copies()
		{
			var tpf = new TsPropsFactory();
			var original = tpf.MakeProps("someStyle", 43, 57) as ITsTextProps;

			var copy = tpf.FromITsTextProps(original);
			string diff;
			Assert.That(TsTextPropsHelper.PropsAreEqual(original, copy, out diff));
		}
	}
}
