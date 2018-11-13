// // Copyright (c) 2018 SIL International
// // This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Icu;

namespace SIL.LCModel.Core.SpellChecking
{
	internal sealed class SpellEngineLinux: SpellEngine
	{
		private IntPtr _hunspellHandle;
		private bool _isVernacular;
		private bool _gotIsVernacular;

		internal SpellEngineLinux(string affixPath, string dictPath, string exceptionPath)
			: base(exceptionPath)
		{
			_hunspellHandle = Hunspell_initialize(MarshallAsUtf8Bytes(affixPath), MarshallAsUtf8Bytes(dictPath));
		}

		#region Disposable
		protected override void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			if (_hunspellHandle != IntPtr.Zero)
			{
				Hunspell_uninitialize(_hunspellHandle);
				_hunspellHandle = IntPtr.Zero;
			}

			base.Dispose(disposing);
		}
		#endregion

		/// <summary>
		/// We can't declare these arguments (char * in C++) as [MarshalAs(UnmanagedType.LPStr)] string, because that
		/// unconditionally coverts the string to bytes using the current system code page, which is never what we want.
		/// So we declare them as byte[] and marshal like this. The C++ code requires null termination so add a null
		/// before converting. (This doesn't seem to be necessary, but better safe than sorry.)
		/// </summary>
		/// <param name="word"></param>
		/// <returns></returns>
		private static byte[] MarshallAsUtf8Bytes(string word)
		{
			return Encoding.UTF8.GetBytes(Normalizer.Normalize(word, Normalizer.UNormalizationMode.UNORM_NFC) + "\0");
		}

		// This method transforms an array of unmanaged character pointers (pointed to by pUnmanagedStringArray)
		// into an array of managed strings.
		// Adapted with thanks from http://limbioliong.wordpress.com/2011/08/14/returning-an-array-of-strings-from-c-to-c-part-1/
		private static string[] MarshalUnmanagedStrArray2ManagedStrArray(IntPtr pUnmanagedStringArray, int stringCount)
		{
			var pIntPtrArray = new IntPtr[stringCount];
			var managedStringArray = new string[stringCount];

			Marshal.Copy(pUnmanagedStringArray, pIntPtrArray, 0, stringCount);

			for (var i = 0; i < stringCount; i++)
			{
				var data = new List<byte>();
				var ptr = pIntPtrArray[i];
				var offset = 0;
				while (true)
				{
					var ch = Marshal.ReadByte(ptr, offset++);
					if (ch == 0)
						break;

					data.Add(ch);
				}
				managedStringArray[i] = Encoding.UTF8.GetString(data.ToArray());
			}
			return managedStringArray;
		}

		#region Methods to access LibHunspell
		private const int RTLD_NOW = 2;

		[DllImport("libdl.so", SetLastError = true)]
		private static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPTStr)] string file, int mode);

		[DllImport("libdl.so", SetLastError = true)]
		private static extern int dlclose(IntPtr handle);

		private ILibHunspell nativeLibrary;

		private ILibHunspell Library
		{
			get
			{
				if (nativeLibrary != null)
					return nativeLibrary;

				// Try dlopen'ing libhunspell .so files until we find one.
				var hunspellHandle = dlopen(LibHunspell160.LibraryFilename, RTLD_NOW);
				if (hunspellHandle != IntPtr.Zero)
				{
					dlclose(hunspellHandle);
					nativeLibrary = new LibHunspell160();
					return nativeLibrary;
				}

				hunspellHandle = dlopen(LibHunspell130.LibraryFilename, RTLD_NOW);
				if (hunspellHandle != IntPtr.Zero)
				{
					dlclose(hunspellHandle);
					nativeLibrary = new LibHunspell130();
					return nativeLibrary;
				}

				hunspellHandle = dlopen(LibHunspell.LibraryFilename, RTLD_NOW);
				if (hunspellHandle != IntPtr.Zero)
				{
					dlclose(hunspellHandle);
					nativeLibrary = new LibHunspell();
					return nativeLibrary;
				}

				throw new Exception("Unable to find and load libhunspell.");
			}
		}

		private IntPtr Hunspell_initialize(byte[] affFile, byte[] dictFile)
		{
			return Library.Hunspell_initialize(affFile, dictFile);
		}

		private void Hunspell_uninitialize(IntPtr handle)
		{
			Library.Hunspell_uninitialize(handle);
		}

		private int Hunspell_spell(IntPtr handle, byte[] word)
		{
			return Library.Hunspell_spell(handle, word);
		}

		private int Hunspell_add(IntPtr handle, byte[] word)
		{
			return Library.Hunspell_add(handle, word);
		}

		private int Hunspell_add_with_affix(IntPtr handle, byte[] word, byte[] example)
		{
			return Library.Hunspell_add_with_affix(handle, word, example);
		}

		private int Hunspell_remove(IntPtr handle, byte[] word)
		{
			return Library.Hunspell_remove(handle, word);
		}

		private int Hunspell_suggest_unix(IntPtr handle, out IntPtr suggestions, byte[] word)
		{
			return Library.Hunspell_suggest_unix(handle, out suggestions, word);
		}

		private int Hunspell_suggest(IntPtr handle, byte[] word, out IntPtr suggestions)
		{
			return Library.Hunspell_suggest_unix(handle, out suggestions, word);
		}

		private void Hunspell_free_list(IntPtr handle, ref IntPtr list, int count)
		{
			Library.Hunspell_free_list(handle, ref list, count);
		}
		#endregion

		public override bool Check(string word)
		{
			return Hunspell_spell(_hunspellHandle, MarshallAsUtf8Bytes(word)) != 0;
		}

		protected override void SetStatusInternal(string word, bool isCorrect)
		{
			if (isCorrect)
			{
				if (IsVernacular)
				{
					// Custom vernacular-only dictionary.
					// want it 'affixed' like the prototype, which has been marked to suppress other-case matches
					Hunspell_add_with_affix(_hunspellHandle, MarshallAsUtf8Bytes(word),
							MarshallAsUtf8Bytes(SpellingHelper.PrototypeWord));
				}
				else
				{
					// not our custom dictionary, some majority language, we can't (and probably don't want)
					// to be restrictive about case.
					Hunspell_add(_hunspellHandle, MarshallAsUtf8Bytes(word));
				}
			}
			else
			{
				Hunspell_remove(_hunspellHandle, MarshallAsUtf8Bytes(word));
			}
		}

		/// <inheritdoc />
		public override ICollection<string> Suggest(string badWord)
		{
			var resultCount = Hunspell_suggest(_hunspellHandle, MarshallAsUtf8Bytes(badWord), out var pointerToAddressStringArray);
			if (pointerToAddressStringArray == IntPtr.Zero)
				return new string[0];
			var results = MarshalUnmanagedStrArray2ManagedStrArray(pointerToAddressStringArray, resultCount);
			Hunspell_free_list(_hunspellHandle, ref pointerToAddressStringArray, resultCount);
			return results;
		}

		/// <inheritdoc />
		public override bool IsVernacular
		{
			get
			{
				if (_gotIsVernacular)
					return _isVernacular;

				_isVernacular = Check(SpellingHelper.PrototypeWord);
				_gotIsVernacular = true;
				return _isVernacular;
			}
		}
	}

	/// <summary>
	/// Interface to native methods in libhunspell libraries.
	/// </summary>
	interface ILibHunspell
	{
		IntPtr Hunspell_initialize(byte[] affFile, byte[] dictFile);

		void Hunspell_uninitialize(IntPtr handle);

		int Hunspell_spell(IntPtr handle, byte[] word);

		int Hunspell_add(IntPtr handle, byte[] word);

		int Hunspell_add_with_affix(IntPtr handle, byte[] word, byte[] example);

		int Hunspell_remove(IntPtr handle, byte[] word);

		int Hunspell_suggest_unix(IntPtr handle, out IntPtr suggestions, byte[] word);

		int Hunspell_suggest(IntPtr handle, byte[] word, out IntPtr suggestions);

		void Hunspell_free_list(IntPtr handle, ref IntPtr list, int count);
	}

	#region LibHunspell libraries
	/// <summary>
	/// libhunspell in Ubuntu 14.04 and 16.04
	/// </summary>
	internal class LibHunspell130 : ILibHunspell
	{
		public static string LibraryFilename
		{
			get
			{
				return NativeLibhunspell_1_3_0.LibHunspell;
			}
		}

		public IntPtr Hunspell_initialize(byte[] affFile, byte[] dictFile)
		{
			return NativeLibhunspell_1_3_0.Hunspell_initialize(affFile, dictFile);
		}

		public void Hunspell_uninitialize(IntPtr handle)
		{
			NativeLibhunspell_1_3_0.Hunspell_uninitialize(handle);
		}

		public int Hunspell_spell(IntPtr handle, byte[] word)
		{
			return NativeLibhunspell_1_3_0.Hunspell_spell(handle, word);
		}

		public int Hunspell_add(IntPtr handle, byte[] word)
		{
			return NativeLibhunspell_1_3_0.Hunspell_add(handle, word);
		}

		public int Hunspell_add_with_affix(IntPtr handle, byte[] word, byte[] example)
		{
			return NativeLibhunspell_1_3_0.Hunspell_add_with_affix(handle, word, example);
		}

		public int Hunspell_remove(IntPtr handle, byte[] word)
		{
			return NativeLibhunspell_1_3_0.Hunspell_remove(handle, word);
		}

		public int Hunspell_suggest_unix(IntPtr handle, out IntPtr suggestions, byte[] word)
		{
			return NativeLibhunspell_1_3_0.Hunspell_suggest_unix(handle, out suggestions, word);
		}

		public int Hunspell_suggest(IntPtr handle, byte[] word, out IntPtr suggestions)
		{
			return NativeLibhunspell_1_3_0.Hunspell_suggest_unix(handle, out suggestions, word);
		}

		public void Hunspell_free_list(IntPtr handle, ref IntPtr list, int count)
		{
			NativeLibhunspell_1_3_0.Hunspell_free_list(handle, ref list, count);
		}
	}

	/// <summary>Hunspell functions in libhunspell 1.3.0</summary>
	internal class NativeLibhunspell_1_3_0
	{
		public const string LibHunspell = "libhunspell-1.3.so.0";
		public const string LibHunspellPrefix = "Hunspell_";

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "create",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern IntPtr Hunspell_initialize(byte[] affFile, byte[] dictFile);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "destroy",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern void Hunspell_uninitialize(IntPtr handle);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "spell",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_spell(IntPtr handle, byte[] word);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "add",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_add(IntPtr handle, byte[] word);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "add_with_affix",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_add_with_affix(IntPtr handle, byte[] word, byte[] example);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "remove",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_remove(IntPtr handle, byte[] word);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "suggest",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_suggest_unix(IntPtr handle, out IntPtr suggestions, byte[] word);

		public static int Hunspell_suggest(IntPtr handle, byte[] word, out IntPtr suggestions)
		{
			return Hunspell_suggest_unix(handle, out suggestions, word);
		}

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "free_list",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern void Hunspell_free_list(IntPtr handle, ref IntPtr list, int count);
	}

	/// <summary>
	/// libhunspell in Ubuntu 18.04
	/// </summary>
	internal class LibHunspell160 : ILibHunspell
	{
		public static string LibraryFilename
		{
			get
			{
				return NativeLibhunspell_1_6_0.LibHunspell;
			}
		}

		public IntPtr Hunspell_initialize(byte[] affFile, byte[] dictFile)
		{
			return NativeLibhunspell_1_6_0.Hunspell_initialize(affFile, dictFile);
		}

		public void Hunspell_uninitialize(IntPtr handle)
		{
			NativeLibhunspell_1_6_0.Hunspell_uninitialize(handle);
		}

		public int Hunspell_spell(IntPtr handle, byte[] word)
		{
			return NativeLibhunspell_1_6_0.Hunspell_spell(handle, word);
		}

		public int Hunspell_add(IntPtr handle, byte[] word)
		{
			return NativeLibhunspell_1_6_0.Hunspell_add(handle, word);
		}

		public int Hunspell_add_with_affix(IntPtr handle, byte[] word, byte[] example)
		{
			return NativeLibhunspell_1_6_0.Hunspell_add_with_affix(handle, word, example);
		}

		public int Hunspell_remove(IntPtr handle, byte[] word)
		{
			return NativeLibhunspell_1_6_0.Hunspell_remove(handle, word);
		}

		public int Hunspell_suggest_unix(IntPtr handle, out IntPtr suggestions, byte[] word)
		{
			return NativeLibhunspell_1_6_0.Hunspell_suggest_unix(handle, out suggestions, word);
		}

		public int Hunspell_suggest(IntPtr handle, byte[] word, out IntPtr suggestions)
		{
			return NativeLibhunspell_1_6_0.Hunspell_suggest_unix(handle, out suggestions, word);
		}

		public void Hunspell_free_list(IntPtr handle, ref IntPtr list, int count)
		{
			NativeLibhunspell_1_6_0.Hunspell_free_list(handle, ref list, count);
		}
	}

	/// <summary>Hunspell functions in libhunspell 1.6.0</summary>
	internal class NativeLibhunspell_1_6_0
	{
		public const string LibHunspell = "libhunspell-1.6.so.0";
		public const string LibHunspellPrefix = "Hunspell_";

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "create",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern IntPtr Hunspell_initialize(byte[] affFile, byte[] dictFile);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "destroy",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern void Hunspell_uninitialize(IntPtr handle);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "spell",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_spell(IntPtr handle, byte[] word);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "add",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_add(IntPtr handle, byte[] word);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "add_with_affix",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_add_with_affix(IntPtr handle, byte[] word, byte[] example);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "remove",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_remove(IntPtr handle, byte[] word);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "suggest",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_suggest_unix(IntPtr handle, out IntPtr suggestions, byte[] word);

		public static int Hunspell_suggest(IntPtr handle, byte[] word, out IntPtr suggestions)
		{
			return Hunspell_suggest_unix(handle, out suggestions, word);
		}

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "free_list",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern void Hunspell_free_list(IntPtr handle, ref IntPtr list, int count);
	}

	/// <summary>
	/// libhunspell.so from libhunspell-dev package.
	/// </summary>
	internal class LibHunspell : ILibHunspell
	{
		public static string LibraryFilename
		{
			get
			{
				return NativeLibhunspellSo.LibHunspell;
			}
		}

		public IntPtr Hunspell_initialize(byte[] affFile, byte[] dictFile)
		{
			return NativeLibhunspellSo.Hunspell_initialize(affFile, dictFile);
		}

		public void Hunspell_uninitialize(IntPtr handle)
		{
			NativeLibhunspellSo.Hunspell_uninitialize(handle);
		}

		public int Hunspell_spell(IntPtr handle, byte[] word)
		{
			return NativeLibhunspellSo.Hunspell_spell(handle, word);
		}

		public int Hunspell_add(IntPtr handle, byte[] word)
		{
			return NativeLibhunspellSo.Hunspell_add(handle, word);
		}

		public int Hunspell_add_with_affix(IntPtr handle, byte[] word, byte[] example)
		{
			return NativeLibhunspellSo.Hunspell_add_with_affix(handle, word, example);
		}

		public int Hunspell_remove(IntPtr handle, byte[] word)
		{
			return NativeLibhunspellSo.Hunspell_remove(handle, word);
		}

		public int Hunspell_suggest_unix(IntPtr handle, out IntPtr suggestions, byte[] word)
		{
			return NativeLibhunspellSo.Hunspell_suggest_unix(handle, out suggestions, word);
		}

		public int Hunspell_suggest(IntPtr handle, byte[] word, out IntPtr suggestions)
		{
			return NativeLibhunspellSo.Hunspell_suggest_unix(handle, out suggestions, word);
		}

		public void Hunspell_free_list(IntPtr handle, ref IntPtr list, int count)
		{
			NativeLibhunspellSo.Hunspell_free_list(handle, ref list, count);
		}
	}

	/// <summary>Hunspell functions in libhunspell.so</summary>
	internal class NativeLibhunspellSo
	{
		public const string LibHunspell = "libhunspell.so";
		public const string LibHunspellPrefix = "Hunspell_";

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "create",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern IntPtr Hunspell_initialize(byte[] affFile, byte[] dictFile);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "destroy",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern void Hunspell_uninitialize(IntPtr handle);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "spell",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_spell(IntPtr handle, byte[] word);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "add",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_add(IntPtr handle, byte[] word);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "add_with_affix",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_add_with_affix(IntPtr handle, byte[] word, byte[] example);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "remove",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_remove(IntPtr handle, byte[] word);

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "suggest",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern int Hunspell_suggest_unix(IntPtr handle, out IntPtr suggestions, byte[] word);

		public static int Hunspell_suggest(IntPtr handle, byte[] word, out IntPtr suggestions)
		{
			return Hunspell_suggest_unix(handle, out suggestions, word);
		}

		[DllImport(LibHunspell, EntryPoint = LibHunspellPrefix + "free_list",
			CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static extern void Hunspell_free_list(IntPtr handle, ref IntPtr list, int count);
	}
	#endregion
}
