// Copyright (c) 2010-2013 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace SIL.LCModel.Utils
{
	/// <summary>
	/// Tests class ArrayPtr
	/// </summary>
	[TestFixture]
	public class ArrayPtrTests
	{
		/// <summary></summary>
		[Test]
		public void Basic()
		{
			using (var array = new ArrayPtr())
			{
				Assert.IsNotNull(array);
			}
		}

		/// <summary></summary>
		[Test]
		public void DisposingArrayPtrOfNativeOwnedMemoryDoesNotFree()
		{
			var intptr = Marshal.AllocCoTaskMem(10);
			using (var array = new ArrayPtr(intptr))
			{
				Marshal.WriteByte(intptr, (byte)123);
			}

			byte b = Marshal.ReadByte(intptr);
			Assert.AreEqual((byte)123, b, "native-owned memory should not have been freed");
			Marshal.FreeCoTaskMem(intptr);
		}

		/// <remarks>Reading memory after it has been freed is not guaranteed to have
		/// consistent results.</remarks>
		[Test]
		[Ignore("By design this test doesn't produce consistent results")]
		public void DisposingArrayPtrOfOwnMemoryDoesFree_1()
		{
			IntPtr arrayIntPtr;
			using (var array = new ArrayPtr(10))
			{
				arrayIntPtr = array.IntPtr;
				Marshal.WriteByte(arrayIntPtr, (byte)123);
			}
			byte b = Marshal.ReadByte(arrayIntPtr);
			Assert.AreEqual((byte)0, b, "Owned memory should have been freed");
		}

		/// <remarks>Reading memory after it has been freed is not guaranteed to have
		/// consistent results.</remarks>
		[Test]
		[Ignore("By design this test doesn't produce consistent results")]
		public void DisposingArrayPtrOfOwnMemoryDoesFree_2()
		{
			IntPtr arrayIntPtr;
			using (var array = new ArrayPtr())
			{
				array.Resize(10);

				arrayIntPtr = array.IntPtr;
				Marshal.WriteByte(arrayIntPtr, (byte)123);
			}
			byte b = Marshal.ReadByte(arrayIntPtr);
			Assert.AreEqual((byte)0, b, "Owned memory should have been freed");
		}

		/// <summary></summary>
		[Test]
		public void CannotResizeIfExternalMemory()
		{
			var intptr = Marshal.AllocCoTaskMem(10);
			using (var array = new ArrayPtr(intptr))
			{
				Assert.That(() => array.Resize(12), Throws.TypeOf<ApplicationException>());
			}
		}

		/// <summary></summary>
		[Test]
		public void CanResizeIfOwnMemory()
		{
			int originalSize = 10;
			using (var array = new ArrayPtr(originalSize))
			{
				Assert.AreEqual(originalSize, array.Size);

				int newSize = 12;
				array.Resize(newSize);
				Assert.AreEqual(newSize, array.Size);
			}
		}

		/// <summary></summary>
		[Test]
		public void DoNotOwnExternalMemory()
		{
			var intptr = Marshal.AllocCoTaskMem(10);
			using (var array = new ArrayPtr(intptr))
			{
				try
				{
					Assert.AreEqual(false, array.OwnMemory, "An ArrayPtr with externally allocated memory does not own its memory");
				}
				finally
				{
					Marshal.FreeCoTaskMem(intptr);
				}
			}
		}

		/// <summary></summary>
		[Test]
		public void DoOwnMemory_1()
		{
			using (var array = new ArrayPtr(10))
				Assert.AreEqual(true, array.OwnMemory, "Should own memory");
		}

		/// <summary></summary>
		[Test]
		public void DoOwnMemory_2()
		{
			using (var array = new ArrayPtr())
			{
				Assert.AreEqual(true, array.OwnMemory, "Should own memory");
				array.Resize(10);
				Assert.AreEqual(true, array.OwnMemory, "Should still own memory after resize");
			}
		}
	}
}
