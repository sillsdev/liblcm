// Copyright (c) 2022 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Globalization;

namespace SIL.LCModel.Utils
{
	public static class SilUtilsExtensions
	{
		#region DateTime extensions
		public const string LCMTimeFormatWithMillis = "yyyy-MM-dd HH:mm:ss.fff";

		public static string ToLCMTimeFormatWithMillisString(this DateTime when)
		{
			return when.ToUniversalTime().ToString(LCMTimeFormatWithMillis, CultureInfo.InvariantCulture);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets a datetime value with the seconds and milliseconds stripped off (does not
		/// actually round to the nearest minute).
		/// </summary>
		/// <param name="value">The value.</param>
		/// ------------------------------------------------------------------------------------
		public static DateTime ToTheMinute(this DateTime value)
		{
			return (value.Second != 0 || value.Millisecond != 0) ?
				new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0) : value;
		}
		#endregion
	}
}
