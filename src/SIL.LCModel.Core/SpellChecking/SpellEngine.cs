// Copyright (c) 2015-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Icu;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Utils;
#if !__MonoCS__
using NHunspell;
#endif

namespace SIL.LCModel.Core.SpellChecking
{
	internal class SpellEngine : ISpellEngine, IDisposable
	{
		/// <summary>
		/// File of exceptions (for a vernacular dictionary, may be the whole dictionary).
		/// Words are added by appearing, each on a line
		/// Words beginning * are known bad words.
		/// </summary>
		internal string ExceptionPath { get; set; }
		#if __MonoCS__
		private IntPtr m_hunspellHandle;
		private NativeLibhunspell _libhunspell = new NativeLibhunspell();
		#else
		private Hunspell m_hunspellHandle;
		#endif

		internal SpellEngine(string affixPath, string dictPath, string exceptionPath)
		{
			try
			{
				#if __MonoCS__
				m_hunspellHandle = _libhunspell.Hunspell_initialize(MarshallAsUtf8Bytes(affixPath), MarshallAsUtf8Bytes(dictPath));
				#else
				m_hunspellHandle = new Hunspell(affixPath, dictPath);
				#endif
				ExceptionPath = exceptionPath;

				if (File.Exists(ExceptionPath))
				{
					using (var reader = new StreamReader(ExceptionPath, Encoding.UTF8))
					{
						string line;
						while ((line = reader.ReadLine()) != null)
						{
							var item = line;
							bool correct = true;
							if (item.Length > 0 && item[0] == '*')
							{
								correct = false;
								item = item.Substring(1);
							}
							SetInternalStatus(item, correct);
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.WriteLine("Initializing Hunspell: {0} exception: {1} ", e.GetType(), e.Message);
#if __MonoCS__
				if (m_hunspellHandle != IntPtr.Zero)
				{
					_libhunspell.Hunspell_uninitialize(m_hunspellHandle);
					m_hunspellHandle = IntPtr.Zero;
				}
#else
				m_hunspellHandle?.Dispose();
#endif
				throw;
			}
		}

		public bool Check(string word)
		{
			#if __MonoCS__
				return _libhunspell.Hunspell_spell(m_hunspellHandle, MarshallAsUtf8Bytes(word)) != 0;
			#else
				return m_hunspellHandle.Spell(MarshallAsUtf8Bytes(word));
			#endif
		}

		/// <summary>
		/// We can't declare these arguments (char * in C++) as [MarshalAs(UnmanagedType.LPStr)] string, because that
		/// unconditionally coverts the string to bytes using the current system code page, which is never what we want.
		/// So we declare them as byte[] and marshal like this. The C++ code requires null termination so add a null
		/// before converting. (This doesn't seem to be necessary, but better safe than sorry.)
		/// </summary>
		/// <param name="word"></param>
		/// <returns></returns>
#if __MonoCS__
		private static byte[] MarshallAsUtf8Bytes(string word)
		{
			return Encoding.UTF8.GetBytes(Normalizer.Normalize(word, Normalizer.UNormalizationMode.UNORM_NFC) + "\0");
		}
#else
		private static string MarshallAsUtf8Bytes(string word)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(Normalizer.Normalize(word,
				Normalizer.UNormalizationMode.UNORM_NFC) + "\0");
			return Encoding.UTF8.GetString(bytes);
		}
#endif

		private bool m_isVernacular;
		private bool m_fGotIsVernacular;
		/// <summary>
		/// A dictionary is considered vernacular if it contains our special word. That is, we presume it is one
		/// we created and can rewrite if we choose; and it should not be used for any dictionary ID that is
		/// not an exact match.
		/// </summary>
		public bool IsVernacular
		{
			get
			{
				if (!m_fGotIsVernacular)
				{
					#if __MonoCS__
					m_isVernacular = Check(SpellingHelper.PrototypeWord);
					#else
					m_isVernacular = Check(MarshallAsUtf8Bytes(SpellingHelper.PrototypeWord));
					#endif
					m_fGotIsVernacular = true;
				}
				return m_isVernacular;
			}
		}

		/// <summary>
		/// Get a list of suggestions for alternate words to use in place of the mis-spelled one.
		/// </summary>
		/// <param name="badWord"></param>
		/// <returns></returns>
		public ICollection<string> Suggest(string badWord)
		{
			#if __MonoCS__
			IntPtr pointerToAddressStringArray;
			int resultCount = _libhunspell.Hunspell_suggest(m_hunspellHandle, MarshallAsUtf8Bytes(badWord), out pointerToAddressStringArray);
			if (pointerToAddressStringArray == IntPtr.Zero)
				return new string[0];
			var results = MarshalUnmananagedStrArray2ManagedStrArray(pointerToAddressStringArray, resultCount);
			_libhunspell.Hunspell_free_list(m_hunspellHandle, ref pointerToAddressStringArray, resultCount);
			return results;
			#else
			return m_hunspellHandle.Suggest(MarshallAsUtf8Bytes(badWord));
			#endif
		}

		public void SetStatus(string word1, bool isCorrect)
		{
			var word = Normalizer.Normalize(word1, Normalizer.UNormalizationMode.UNORM_NFC);
			if (Check(word) == isCorrect)
				return; // nothing to do.
			// Review: any IO exceptions we should handle? How??
			SetInternalStatus(word, isCorrect);
			var builder = new StringBuilder();
			bool insertedLineForWord = false;
			if (File.Exists(ExceptionPath))
			{
				using (var reader = new StreamReader(ExceptionPath, Encoding.UTF8))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						var item = line;
						if (item.Length > 0 && item[0] == '*')
							item = item.Substring(1);
						// If we already got it, or the current line is before the word, just copy the line to the output.
						if (insertedLineForWord || string.Compare(item, word, StringComparison.Ordinal) < 0)
						{
							builder.AppendLine(line);
							continue;
						}
						// We've come to the right place to insert our word.
						if (!isCorrect)
							builder.Append("*");
						builder.AppendLine(word);
						insertedLineForWord = true;
						if (word != item) // then current line must be a pre-existing word that comes after ours.
							builder.AppendLine(line); // so add it in after item
					}
				}
			}
			if (!insertedLineForWord) // no input file, or the word comes after any existing one
			{
				// The very first exception!
				if (!isCorrect)
					builder.Append("*");
				builder.AppendLine(word);
			}
			// Write the new file over the old one.
			File.WriteAllText(ExceptionPath, builder.ToString(), Encoding.UTF8);
		}

		private void SetInternalStatus(string word, bool isCorrect)
		{
			if (isCorrect)
			{
				if (IsVernacular)
				{
					// Custom vernacular-only dictionary.
					// want it 'affixed' like the prototype, which has been marked to suppress other-case matches
					#if __MonoCS__
					_libhunspell.Hunspell_add_with_affix(m_hunspellHandle, MarshallAsUtf8Bytes(word),
							MarshallAsUtf8Bytes(SpellingHelper.PrototypeWord));
					#else
					m_hunspellHandle.AddWithAffix(MarshallAsUtf8Bytes(word), MarshallAsUtf8Bytes(SpellingHelper.PrototypeWord));
					#endif
				}
				else
				{
					// not our custom dictionary, some majority language, we can't (and probably don't want)
					// to be restrictive about case.
					#if __MonoCS__
					_libhunspell.Hunspell_add(m_hunspellHandle, MarshallAsUtf8Bytes(word));
					#else
					m_hunspellHandle.Add(MarshallAsUtf8Bytes(word));
					#endif
				}
			}
			else
			{
				#if __MonoCS__
				_libhunspell.Hunspell_remove(m_hunspellHandle, MarshallAsUtf8Bytes(word));
				#else
				m_hunspellHandle.Remove(MarshallAsUtf8Bytes(word));
				#endif
			}
		}

		~SpellEngine()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SupressFinalize to
			// take this object off the finalization queue
			// and prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			#if __MonoCS__
			if (m_hunspellHandle != IntPtr.Zero)
			{
				_libhunspell.Hunspell_uninitialize(m_hunspellHandle);
				m_hunspellHandle = IntPtr.Zero;
			}
			#else
			m_hunspellHandle?.Dispose();
			#endif
		}

		#if __MonoCS__
		// This method transforms an array of unmanaged character pointers (pointed to by pUnmanagedStringArray)
		// into an array of managed strings.
		// Adapted with thanks from http://limbioliong.wordpress.com/2011/08/14/returning-an-array-of-strings-from-c-to-c-part-1/
		static string[] MarshalUnmananagedStrArray2ManagedStrArray(IntPtr pUnmanagedStringArray, int stringCount)
		{
			IntPtr[] pIntPtrArray = new IntPtr[stringCount];
			var managedStringArray = new string[stringCount];

			Marshal.Copy(pUnmanagedStringArray, pIntPtrArray, 0, stringCount);

			for (int i = 0; i < stringCount; i++)
			{
				var data = new List<byte>();
				var ptr = pIntPtrArray[i];
				int offset = 0;
				while (true)
				{
					var ch = Marshal.ReadByte(ptr, offset++);
					if (ch == 0)
					{
						break;
					}
					data.Add(ch);
				}
				managedStringArray[i] = Encoding.UTF8.GetString(data.ToArray());
			}
			return managedStringArray;
		}
		#endif
	}

	/// <summary>
	/// Access point to native hunspell methods, in an available libhunspell version.
	/// </summary>
	internal class NativeLibhunspell
	{
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

		public IntPtr Hunspell_initialize(byte[] affFile, byte[] dictFile)
		{
			return Library.Hunspell_initialize(affFile, dictFile);
		}

		public void Hunspell_uninitialize(IntPtr handle)
		{
			Library.Hunspell_uninitialize(handle);
		}

		public int Hunspell_spell(IntPtr handle, byte[] word)
		{
			return Library.Hunspell_spell(handle, word);
		}

		public int Hunspell_add(IntPtr handle, byte[] word)
		{
			return Library.Hunspell_add(handle, word);
		}

		public int Hunspell_add_with_affix(IntPtr handle, byte[] word, byte[] example)
		{
			return Library.Hunspell_add_with_affix(handle, word, example);
		}

		public int Hunspell_remove(IntPtr handle, byte[] word)
		{
			return Library.Hunspell_remove(handle, word);
		}

		public int Hunspell_suggest_unix(IntPtr handle, out IntPtr suggestions, byte[] word)
		{
			return Library.Hunspell_suggest_unix(handle, out suggestions, word);
		}

		public int Hunspell_suggest(IntPtr handle, byte[] word, out IntPtr suggestions)
		{
			return Library.Hunspell_suggest_unix(handle, out suggestions, word);
		}

		public void Hunspell_free_list(IntPtr handle, ref IntPtr list, int count)
		{
			Library.Hunspell_free_list(handle, ref list, count);
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
