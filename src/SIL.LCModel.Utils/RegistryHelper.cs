﻿// Copyright (c) 2010-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace SIL.LCModel.Utils
{
	#region RegistryHelper class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Misc. static methods for dealing with the registry.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public static class RegistryHelper
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Sets the name of the company used for registry settings.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string CompanyName { get; set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Sets the name of the product used for registry settings.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string ProductName { get; set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the registry key for the current application's company. This is 'HKCU\Software\{Application.CompanyName}'
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static RegistryKey CompanyKey
		{
			get
			{
				using (var softwareKey = Registry.CurrentUser.CreateSubKey("Software"))
				{
					Debug.Assert(softwareKey != null);
					return softwareKey.CreateSubKey(CompanyName);
				}
			}
		}

		/// <summary>
		/// Gets the registry key for the current application's company in the 32-bit space on a 64-bit machine.
		/// This is 'HKCU\Software\WOW6432Node\{Application.CompanyName}'.
		/// NOTE: This key is not opened for write access, because 64-bit apps should write to their own registry space.
		/// </summary>
		public static RegistryKey CompanyKeyOld32Bit => Registry.CurrentUser.OpenSubKey($@"Software\WOW6432Node\{CompanyName}");

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the registry key for the current application's company from the local machine
		/// settings. This is 'HKLM\Software\{Application.CompanyName}'
		/// NOTE: This key is not opened for write access because it will fail on non-administrator logins.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static RegistryKey CompanyKeyLocalMachine
		{
			get
			{
				using (var softwareKey = Registry.LocalMachine.OpenSubKey("Software"))
				{
					Debug.Assert(softwareKey != null);
					return softwareKey.OpenSubKey(CompanyName);
				}
			}
		}

		/// <summary>
		/// Gets the registry key for the current application's company from the Local Machine settings' 32-bit space on a 64-bit machine.
		/// This is 'HKLM\Software\WOW6432Node\{Application.CompanyName}'.
		/// NOTE: This key is not opened for write access, because 64-bit apps should write to their own registry space.
		/// </summary>
		public static RegistryKey CompanyKeyLocalMachineOld32Bit => Registry.LocalMachine.OpenSubKey($@"Software\WOW6432Node\{CompanyName}");

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the registry key for the current application's company from the local machine
		/// settings. This is 'HKLM\Software\{Application.CompanyName}'
		/// NOTE: This will fail on non-administrator logins.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static RegistryKey CompanyKeyLocalMachineForWriting
		{
			get
			{
				using (var softwareKey = Registry.LocalMachine.CreateSubKey("Software"))
				{
					Debug.Assert(softwareKey != null);
					return softwareKey.CreateSubKey(CompanyName);
				}
			}
		}
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Checks if a registry key exists.
		/// </summary>
		/// <param name="key">The base registry key of the key to check</param>
		/// <param name="subKey">The key to check</param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static bool KeyExists(RegistryKey key, string subKey)
		{
			if (key == null)
				throw new ArgumentNullException(nameof(key));

			return key.GetSubKeyNames().Any(s => s.Equals(subKey, StringComparison.OrdinalIgnoreCase));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Checks if a registry value exists.
		/// </summary>
		/// <param name="key">The base registry key of the key to check</param>
		/// <param name="subKey">Name of the group key, or string.Empty if there is no
		/// groupKeyName.</param>
		/// <param name="regEntry">The name of the registry entry.</param>
		/// <param name="value">[out] value of the registry entry if it exists; null otherwise.</param>
		/// <returns><c>true</c> if the registry entry exists, otherwise <c>false</c></returns>
		/// ------------------------------------------------------------------------------------
		public static bool RegEntryValueExists(RegistryKey key, string subKey, string regEntry, out object value)
		{
			if (key == null)
				throw new ArgumentNullException(nameof(key));

			value = null;

			if (string.IsNullOrEmpty(subKey))
			{
				value = key.GetValue(regEntry);
				if (value != null)
					return true;
				return false;
			}

			if (!KeyExists(key, subKey))
				return false;

			using (var regSubKey = key.OpenSubKey(subKey))
			{
				Debug.Assert(regSubKey != null, "Should have caught this in the KeyExists call above");
				if (Array.IndexOf(regSubKey.GetValueNames(), regEntry) >= 0)
				{
					value = regSubKey.GetValue(regEntry);
					return true;
				}

				return false;
			}
		}

		/// <summary>
		/// Returns a subkey of HKCU\Software using the company name (Application.CompanyName) and
		/// the product name (Application.ProductName).
		/// </summary>
		/// <param name="subKeys">Zero or more subkeys (e.g., a specific application name, project name, etc.)</param>
		public static RegistryKey SettingsKey(params string[] subKeys)
		{
			return CompanyKey.CreateSubKey(GetSubkeyName(subKeys));
		}

		/// <summary>
		/// Returns a subkey of HKCU\Software using the company name (Application.CompanyName) and
		/// the product name (Application.ProductName) in the  32-bit space on a 64-bit machine.
		/// NOTE: This key is not opened for write access, because 64-bit apps should write to their own registry space.
		/// </summary>
		/// <param name="subKeys">Zero or more subkeys (e.g., a specific application name, project name, etc.)</param>
		public static RegistryKey SettingsKeyOld32Bit(params string[] subKeys)
		{
			return CompanyKeyOld32Bit?.OpenSubKey(GetSubkeyName(subKeys));
		}

		/// <summary>
		/// Returns a subkey of HKLM\Software using the company name (Application.CompanyName) and
		/// the product name (Application.ProductName).
		/// NOTE: This key is not opened for write access because it will fail on non-administrator logins.
		/// </summary>
		/// <param name="subKeys">Zero or more subkeys (e.g., a specific application name, project name, etc.)</param>
		public static RegistryKey SettingsKeyLocalMachine(params string[] subKeys)
		{
			try
			{
				return CompanyKeyLocalMachine?.OpenSubKey(GetSubkeyName(subKeys), false);
			}
			catch (SecurityException)
			{
				return null;
			}
		}

		/// <summary>
		/// Returns a subkey of HKLM\Software using the company name (Application.CompanyName) and
		/// the product name (Application.ProductName) in the  32-bit space on a 64-bit machine.
		/// NOTE: This key is not opened for write access because it will fail on non-administrator logins.
		/// </summary>
		/// <param name="subKeys">Zero or more subkeys (e.g., a specific application name, project name, etc.)</param>
		public static RegistryKey SettingsKeyLocalMachineOld32Bit(params string[] subKeys)
		{
			try
			{
				return CompanyKeyLocalMachineOld32Bit?.OpenSubKey(GetSubkeyName(subKeys), false);
			}
			catch (SecurityException)
			{
				return null;
			}
		}

		/// <summary>
		/// Returns a subkey of HKLM\Software using the company name (Application.CompanyName) and
		/// the product name (Application.ProductName).
		/// NOTE: This will fail on non-administrator logins.
		/// </summary>
		/// <param name="subKeys">Zero or more subkeys (e.g., a specific application name, project name, etc.)</param>
		public static RegistryKey SettingsKeyLocalMachineForWriting(params string[] subKeys)
		{
			return CompanyKeyLocalMachineForWriting.CreateSubKey(GetSubkeyName(subKeys));
		}

		private static string GetSubkeyName(string[] subKeys)
		{
			var bldr = new StringBuilder();
			bldr.Append(ProductName).Append(@"\");
			foreach (var subKey in subKeys)
				bldr.Append(subKey).Append(@"\");
			return bldr.ToString();
		}
	}
	#endregion

	#region RegistrySetting (base) class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// This class manages a user setting in the registry, maintaining a cached value for quick
	/// access. (Entries are under HKCU\Software\Application.CompanyName\Application.ProductName).
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public abstract class RegistrySetting<T>: IDisposable
	{
		private T m_value;
		private readonly string m_keyName;
		private readonly RegistryKey m_subKey;
		private readonly bool m_fDisposeSubKey;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor for instantiating an object for setting\retrieving a registry value
		/// based on zero or more subkeys.
		/// </summary>
		/// <param name="defaultValue">The default value to use when retrieving the value of an
		/// uninitialized key</param>
		/// <param name="entry">The key whose value is to be stored/retrieved</param>
		/// <param name="subKeys">Zero or more subkeys</param>
		/// ------------------------------------------------------------------------------------
		protected RegistrySetting(T defaultValue, string entry, params string[] subKeys) :
			this(RegistryHelper.SettingsKey(subKeys), entry, defaultValue)
		{
			m_fDisposeSubKey = true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor for instantiating an object for setting\retrieving a registry value
		/// given a registry key.
		/// </summary>
		/// <param name="regKey">The registry key</param>
		/// <param name="entry">The key whose value is to be stored/retrieved</param>
		/// <param name="defaultValue">The default value to use when retrieving the value of an
		/// uninitialized key</param>
		/// ------------------------------------------------------------------------------------
		protected RegistrySetting(RegistryKey regKey, string entry, T defaultValue)
		{
			m_subKey = regKey;
			m_keyName = entry;
			m_value = ConvertVal(m_subKey.GetValue(entry, defaultValue));
		}

		#region Disposable stuff
		#if DEBUG
		/// <summary>
		/// Releases unmanaged resources and performs other cleanup operations before the
		/// RegistrySetting is reclaimed by garbage collection.
		/// </summary>
		~RegistrySetting()
		{
			Dispose(false);
		}
		#endif

		/// <summary>
		/// Gets a value indicating whether this instance is disposed.
		/// </summary>
		public bool IsDisposed { get; private set; }

		/// <summary/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary/>
		protected virtual void Dispose(bool fDisposing)
		{
			Debug.WriteLineIf(!fDisposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			if (fDisposing && !IsDisposed)
			{
				// dispose managed and unmanaged objects
				if (m_fDisposeSubKey)
				{
					var disposableSubKey = m_subKey as IDisposable;
					disposableSubKey?.Dispose();
				}
			}
			IsDisposed = true;
		}
		#endregion

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Converts the specified value to the desired type.
		/// </summary>
		/// <param name="value">The value.</param>
		/// ------------------------------------------------------------------------------------
		protected abstract T ConvertVal(object value);

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets a form of the given value suitable for persiting to the registry.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		protected virtual object GetPersistableForm(T value)
		{
			return value;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Get/set the value of the setting
		/// </summary>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public T Value
		{
			get { return m_value; }
			set
			{
				m_value = value;
				m_subKey?.SetValue(m_keyName, GetPersistableForm(m_value));
			}
		}
	}
	#endregion

	#region RegistryFloatSetting class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// This class manages a float user setting in the registry, maintaining a cached value for quick
	/// access. (Entries are under HKCU\Software\Application.CompanyName\Application.ProductName).
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class RegistryFloatSetting : RegistrySetting<float>
	{
		private readonly float m_defaultValue;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor for instantiating an object for setting\retrieving a float registry
		/// value based on zero or more subkeys.
		/// </summary>
		/// <param name="defaultValue">The default value to use when retrieving the value of an
		/// uninitialized key</param>
		/// <param name="entry">The key whose value is to be stored/retrieved</param>
		/// <param name="subKeys">Zero or more subkeys</param>
		/// ------------------------------------------------------------------------------------
		public RegistryFloatSetting(float defaultValue, string entry, params string[] subKeys) :
			base(defaultValue, entry, subKeys)
		{
			m_defaultValue = defaultValue;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor for instantiating an object for setting\retrieving a float registry
		/// value given a registry key.
		/// </summary>
		/// <param name="regKey">The registry key</param>
		/// <param name="entry">The key whose value is to be stored/retrieved</param>
		/// <param name="defaultValue">The default value to use when retrieving the value of an
		/// uninitialized key</param>
		/// ------------------------------------------------------------------------------------
		public RegistryFloatSetting(RegistryKey regKey, string entry, float defaultValue) :
			base(regKey, entry, defaultValue)
		{
			m_defaultValue = defaultValue;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets a form of the given value suitable for persiting to the registry.
		/// </summary>
		/// <param name="value">The value.</param>
		/// ------------------------------------------------------------------------------------
		protected override object GetPersistableForm(float value)
		{
			return value.ToString(CultureInfo.GetCultureInfo("en-US"));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Converts the specified value to the desired type.
		/// </summary>
		/// <param name="value">The value.</param>
		/// ------------------------------------------------------------------------------------
		protected override float ConvertVal(object value)
		{
			if (value is float)
				return (float)value;

			var val = (string)value;

			try
			{
				var enCi = CultureInfo.GetCultureInfo("en-US");
				var currDecSep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
				if (currDecSep != enCi.NumberFormat.NumberDecimalSeparator)
					val = val.Replace(currDecSep, enCi.NumberFormat.NumberDecimalSeparator);

				return float.Parse(val, enCi);
			}
			catch
			{
				return m_defaultValue;
			}
		}
	}
	#endregion

	#region RegistryBoolSetting class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// This class manages a boolean user setting in the registry, maintaining a cached value for quick
	/// access. (Entries are under HKCU\Software\Application.CompanyName\Application.ProductName).
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class RegistryBoolSetting : RegistrySetting<bool>
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor for instantiating an object for setting\retrieving a boolean registry
		/// value based on zero or more subkeys.
		/// </summary>
		/// <param name="defaultValue">The default value to use when retrieving the value of an
		/// uninitialized key</param>
		/// <param name="entry">The key whose value is to be stored/retrieved</param>
		/// <param name="subKeys">Zero or more subkeys</param>
		/// ------------------------------------------------------------------------------------
		public RegistryBoolSetting(bool defaultValue, string entry, params string[] subKeys) :
			base(defaultValue, entry, subKeys)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor for instantiating an object for setting\retrieving a boolean registry
		/// value given a registry key.
		/// </summary>
		/// <param name="regKey">The registry key</param>
		/// <param name="entry">The key whose value is to be stored/retrieved</param>
		/// <param name="defaultValue">The default value to use when retrieving the value of an
		/// uninitialized key</param>
		/// ------------------------------------------------------------------------------------
		public RegistryBoolSetting(RegistryKey regKey, string entry, bool defaultValue) :
			base(regKey, entry, defaultValue)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor for instantiating an object for setting\retrieving a boolean registry
		/// value for use in views where we do not want to persist settings or in tests.
		/// </summary>
		/// <param name="keyName">The key whose value is to be stored/retrieved</param>
		/// <param name="defaultValue">The default value to use when retrieving the value of an
		/// unitialized key</param>
		/// ------------------------------------------------------------------------------------
		public RegistryBoolSetting(string keyName, bool defaultValue)
			: base(null, keyName, defaultValue)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Converts the specified value to the desired type.
		/// </summary>
		/// <param name="value">The value.</param>
		/// ------------------------------------------------------------------------------------
		protected override bool ConvertVal(object value)
		{
			return Convert.ToBoolean(value);
		}
	}
	#endregion

	#region RegistryIntSetting class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// This class manages a integer (DWORD) user setting in the registry, maintaining a cached value for quick
	/// access. (Entries are under HKCU\Software\Application.CompanyName\Application.ProductName).
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class RegistryIntSetting : RegistrySetting<int>
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor for instantiating an object for setting\retrieving an integer registry
		/// value based on zero or more subkeys.
		/// </summary>
		/// <param name="defaultValue">The default value to use when retrieving the value of an
		/// uninitialized key</param>
		/// <param name="entry">The key whose value is to be stored/retrieved</param>
		/// <param name="subKeys">Zero or more subkeys</param>
		/// ------------------------------------------------------------------------------------
		public RegistryIntSetting(int defaultValue, string entry, params string[] subKeys) :
			base(defaultValue, entry, subKeys)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor for instantiating an object for setting\retrieving an integer registry
		/// value given a registry key.
		/// </summary>
		/// <param name="regKey">The registry key</param>
		/// <param name="entry">The key whose value is to be stored/retrieved</param>
		/// <param name="defaultValue">The default value to use when retrieving the value of an
		/// uninitialized key</param>
		/// ------------------------------------------------------------------------------------
		public RegistryIntSetting(RegistryKey regKey, string entry, int defaultValue) :
			base(regKey, entry, defaultValue)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Converts the specified value to the desired type.
		/// </summary>
		/// <param name="value">The value.</param>
		/// ------------------------------------------------------------------------------------
		protected override int ConvertVal(object value)
		{
			return Convert.ToInt32(value);
		}
	}
	#endregion

	#region RegistryStringSetting class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// This class manages a string user setting in the registry, maintaining a cached value for quick
	/// access. (Entries are under HKCU\Software\Application.CompanyName\Application.ProductName).
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class RegistryStringSetting : RegistrySetting<string>
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor for instantiating an object for setting\retrieving a string registry
		/// value based on zero or more subkeys.
		/// </summary>
		/// <param name="defaultValue">The default value to use when retrieving the value of an
		/// uninitialized key</param>
		/// <param name="entry">The key whose value is to be stored/retrieved</param>
		/// <param name="subKeys">Zero or more subkeys</param>
		/// ------------------------------------------------------------------------------------
		public RegistryStringSetting(string defaultValue, string entry, params string[] subKeys) :
			base(defaultValue, entry, subKeys)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor for instantiating an object for setting\retrieving a string registry
		/// value given a registry key.
		/// </summary>
		/// <param name="regKey">The registry key</param>
		/// <param name="entry">The key whose value is to be stored/retrieved</param>
		/// <param name="defaultValue">The default value to use when retrieving the value of an
		/// uninitialized key</param>
		/// ------------------------------------------------------------------------------------
		public RegistryStringSetting(RegistryKey regKey, string entry, string defaultValue) :
			base(regKey, entry, defaultValue)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Converts the specified value to the desired type.
		/// </summary>
		/// <param name="value">The value.</param>
		/// ------------------------------------------------------------------------------------
		protected override string ConvertVal(object value)
		{
			return (string)value;
		}
	}
	#endregion
}
