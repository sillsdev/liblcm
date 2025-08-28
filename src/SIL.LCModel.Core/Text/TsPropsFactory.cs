// Copyright (c) 2016 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

#nullable enable

using System;
using System.Collections.Generic;
using SIL.LCModel.Core.KernelInterfaces;

namespace SIL.LCModel.Core.Text
{
	/// <summary>
	/// This class is a factory for creating <see cref="TsTextProps"/>.
	/// </summary>
	public class TsPropsFactory : ITsPropsFactory
	{
		/// <summary>
		/// Creates a text properties object using the specified writing system, old writing system, and a character
		/// style. The writing system value may be zero which means no writing system is specified. The style may be null.
		/// If the writing system is zero, then the old writing system must also be zero. An exception will be thrown otherwise.
		/// </summary>
		public ITsTextProps MakeProps(string bstrStyle, int ws, int ows)
		{
			ThrowIfWSInvalid("ws", ws);

			var intProps = new Dictionary<int, TsIntPropValue>
			{
				{(int) FwTextPropType.ktptWs, new TsIntPropValue(ows, ws)}
			};

			Dictionary<int, string>? strProps = null;
			if (!string.IsNullOrEmpty(bstrStyle))
			{
				strProps = new Dictionary<int, string>
				{
					{(int) FwTextPropType.ktptNamedStyle, bstrStyle}
				};
			}

			return TsTextProps.GetInternedTextProps(intProps, strProps);
		}

		/// <summary>
		/// Creates a text properties object using the specified writing system, old writing system, and a character
		/// style. The writing system value may be zero which means no writing system is specified. The style may be null.
		/// If the writing system is zero, then the old writing system must also be zero. An exception will be thrown otherwise.
		/// This method is only used by Views.
		/// </summary>
		public ITsTextProps MakePropsRgch(string rgchStyle, int cch, int ws, int ows)
		{
			if (cch < 0 || cch > (rgchStyle == null ? 0 : rgchStyle.Length))
				throw new ArgumentOutOfRangeException("cch");
			ThrowIfWSInvalid("ws", ws);

			return MakeProps(rgchStyle == null ? string.Empty : rgchStyle.Substring(0, cch), ws, ows);
		}

		/// <summary>
		/// Creates an empty text properties builder.
		/// </summary>
		public ITsPropsBldr GetPropsBldr()
		{
			return new TsPropsBldr();
		}

		private void ThrowIfWSInvalid(string paramName, int ws)
		{
			// TODO: should we support magic writing system codes?
			if (ws < 0)
				throw new ArgumentOutOfRangeException(paramName);
		}

		public TsTextProps FromITsTextProps(ITsTextProps ttp)
		{
			var bldr = new TsPropsBldr();
			int countIntProps = ttp.IntPropCount;
			for (int i = 0; i < countIntProps; i++)
			{
				int tpt, ttv;
				var val = ttp.GetIntProp(i, out tpt, out ttv);
				bldr.SetIntPropValues(tpt, ttv, val);
			}

			int countStrProps = ttp.StrPropCount;
			for (int i = 0; i < countStrProps; i++)
			{
				int tpt;
				var val = ttp.GetStrProp(i, out tpt);
				bldr.SetStrPropValue(tpt, val);
			}
			return (TsTextProps) bldr.GetTextProps();
		}
	}
}
