// Copyright (c) 2002-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// <remarks>
// Implements helper methods for marshaling of COM objects: ArrayPtr, a wrapper for a pointer
// that stores an unmanaged array, and ArrayPtrMarshaler, a custom marshaler for marshalling
// ArrayPtrs.
//
// Classes implemented in this file:
//		ArrayPtr
//		ArrayPtrMarshaler
// </remarks>

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SIL.LCModel.Utils
{
	#region ArrayPtr class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Wrapper for a IntPtr that has allocated memory with Marshal.AllocCoTaskMem
	/// </summary>
	/// <remarks>Use this class for passing unmanaged arrays to and from a COM object.</remarks>
	/// ----------------------------------------------------------------------------------------
	public class ArrayPtr : IDisposable
	{
		private IntPtr m_ptr;
		private int m_Size;
		private static ArrayPtr? s_Null;
		/// <summary>If we are in charge of the memory(true)
		/// or if native code owns it(false)</summary>
		private bool m_ownMemory;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Default constructor.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public ArrayPtr() : this(IntPtr.Zero)
		{
			m_ownMemory = true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor. Takes an IntPtr that has memory allocated with Marshal.AllocCoTaskMem
		/// </summary>
		/// <param name="intPtr">Pointer</param>
		/// ------------------------------------------------------------------------------------
		public ArrayPtr(IntPtr intPtr)
		{
			m_ptr = intPtr;
			m_Size = -1;
			m_ownMemory = false;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor. Allocates memory with Marshal.AllocCoTaskMem
		/// </summary>
		/// <param name="nSize">Amount of bytes to allocate</param>
		/// ------------------------------------------------------------------------------------
		public ArrayPtr(int nSize)
		{
			m_ownMemory = true;
			Resize(nSize);
		}

		#region IDisposable & Co. implementation
		// Region last reviewed: Oct 16, 2005, RandyR

		/// <summary>
		/// Check to see if the object has been disposed.
		/// All public Properties and Methods should call this
		/// before doing anything else.
		/// </summary>
		public void CheckDisposed()
		{
			if (IsDisposed)
				throw new ObjectDisposedException(String.Format("'{0}' in use after being disposed.", GetType().Name));
		}

		/// <summary>
		/// True, if the object has been disposed.
		/// </summary>
		private bool m_isDisposed;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Checks to see if the object has been disposed.
		/// </summary>
		/// <value>
		/// 	<c>true</c> if this instance is disposed; otherwise, <c>false</c>.
		/// </value>
		/// ------------------------------------------------------------------------------------
		public bool IsDisposed
		{
			get { return m_isDisposed; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Finalizer, in case client doesn't dispose it.
		/// Force Dispose(false) if not already called (i.e. m_isDisposed is true)
		/// </summary>
		/// <developernote>We need a Finalizer in this class because we have to free the
		/// unmanaged memory we allocated.</developernote>
		/// ------------------------------------------------------------------------------------
		~ArrayPtr()
		{
			//if (this != s_null) // TODO (Hasso) 2016.11: is this the only place this ugly hack is needed?
			Dispose(false);
			// The base class finalizer is called automatically.
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Free memory used by pointer
		/// </summary>
		/// <remarks>Must not be virtual.</remarks>
		/// ------------------------------------------------------------------------------------
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Executes in two distinct scenarios.
		/// 1. If disposing is true, the method has been called directly
		/// or indirectly by a user's code via the Dispose method.
		/// Both managed and unmanaged resources can be disposed.
		/// 2. If disposing is false, the method has been called by the
		/// runtime from inside the finalizer and you should not reference (access)
		/// other managed objects, as they already have been garbage collected.
		/// Only unmanaged resources can be disposed.
		/// </summary>
		/// <param name="disposing">if set to <c>true</c> [disposing].</param>
		/// <remarks>
		/// If any exceptions are thrown, that is fine.
		/// If the method is being done in a finalizer, it will be ignored.
		/// If it is thrown by client code calling Dispose,
		/// it needs to be handled by fixing the bug.
		/// If subclasses override this method, they should call the base implementation.
		/// </remarks>
		/// ------------------------------------------------------------------------------------
		protected virtual void Dispose(bool disposing)
		{
			// If you're getting this line it means that you forgot to call Dispose().
			Debug.WriteLineIf(!disposing,// && this != s_Null, // TODO (Hasso) 2016.11: is this the only place this ugly hack is needed?
				"****** Missing Dispose() call for " + GetType().Name + ". ****** ");

			// Must not be run more than once.
			if (m_isDisposed)
				return;

			if (disposing)
			{
				// Dispose managed resources.
			}

			// Dispose unmanaged resources, whether disposing is true or false.
			// If m_ptr is null, this will throw an exception.
			// If m_ptr == IntPtr.Zero, it will not throw an exception.
			// The exception is fine, since it indicates a programming error of some sort.

			// only free the pointer if we allocated it
			if (OwnMemory)
				Marshal.FreeCoTaskMem(m_ptr);
			m_ptr = IntPtr.Zero;

			m_isDisposed = true;
		}

		#endregion IDisposable & Co. implementation

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes unmanaged memory with 0.
		/// </summary>
		/// <param name="nSize">Size of allocated unmanaged data</param>
		/// <remarks>Strangely enough .NET doesn't provide a way to do this.
		/// We have to initialize with 0 so that passing an ArrayPtr as [in] parameter works
		/// (however, that's probably a flaw in the IDL file).</remarks>
		/// ------------------------------------------------------------------------------------
		private void InitWithNull(int nSize)
		{
			byte[] b = new byte[nSize]; // elements are automatically initialized to 0
			Marshal.Copy(b, 0, m_ptr, nSize);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Resizes the memory allocated by the IntPtr, if OwnMemory.
		/// </summary>
		/// <param name="nNewSize">New size</param>
		/// <exception cref="System.ApplicationException">if attempting to resize non-owned memory this
		/// ArrayPtr has not allocated, such as memory allocated by native code. Can check that
		/// OwnMemory is true before attempting to Resize.</exception>
		/// ------------------------------------------------------------------------------------
		public void Resize(int nNewSize)
		{
			CheckDisposed();

			if (!OwnMemory)
				throw new ApplicationException("ArrayPtr not resizing memory it does not own and has not allocated");

			m_Size = nNewSize;
			if (nNewSize <= IntPtr.Size)
				nNewSize = IntPtr.Size;
			if (m_ptr == IntPtr.Zero)
				m_ptr = Marshal.AllocCoTaskMem(nNewSize);
			else
				m_ptr = Marshal.ReAllocCoTaskMem(m_ptr, nNewSize);

			InitWithNull(nNewSize);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns the IntPtr
		/// </summary>
		/// <value>A <see cref="IntPtr"/> that represents the unmanaged array</value>
		/// ------------------------------------------------------------------------------------
		public IntPtr IntPtr
		{
			get
			{
				CheckDisposed();

				return m_ptr;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the size of the allocated memory, or -1 if no memory was allocated by this
		/// instance.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public uint Size
		{
			get
			{
				CheckDisposed();

				return (uint)m_Size;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns a pointer suitable as a NULL argument
		/// </summary>
		/// <value>Pointer suitable as a <c>NULL</c> argument</value>
		/// ------------------------------------------------------------------------------------
		public static ArrayPtr Null
		{
			get
			{
				s_Null ??= new ArrayPtr(IntPtr.Zero);
				return s_Null;
			}
		}

		/// <returns>true if this ArrayPtr has allocated its own memory. false if
		/// other code is responsible for its memory allocation and deallocation,
		/// such as native code.</returns>
		public bool OwnMemory
		{
			get
			{
				CheckDisposed();
				return m_ownMemory;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Convert from an IntPtr to an ArrayPtr by creating a new ArrayPtr
		/// </summary>
		/// <param name="intPtr">original IntPtr</param>
		/// <returns>New ArrayPtr</returns>
		/// ------------------------------------------------------------------------------------
		public static explicit operator ArrayPtr(IntPtr intPtr)
		{
			return new ArrayPtr(intPtr);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Converts from an ArrayPtr to an IntPtr
		/// </summary>
		/// <param name="arrayPtr">Original ArrayPtr</param>
		/// <returns>IntPtr of the ArrayPtr</returns>
		/// ------------------------------------------------------------------------------------
		public static explicit operator IntPtr(ArrayPtr arrayPtr)
		{
			if (arrayPtr != null)
				return arrayPtr.m_ptr;
			else
				return IntPtr.Zero;
		}
	}
	#endregion

	#region ArrayPtrMarshaler
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Custom marshaler for <see cref="ArrayPtr"/> pointers
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class ArrayPtrMarshaler : ICustomMarshaler
	{
		private ArrayPtrMarshaler()
		{
		}

		private ArrayPtrMarshaler(string strCookie)
		{
		}

		private static ArrayPtrMarshaler? m_Marshaler;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns (and creates if necessary) an instance of our custom marshaler
		/// </summary>
		/// <param name="strCookie">Cookie that can be used to customized the
		/// returned custom marshaler</param>
		/// <returns>Instance of our custom marshaler</returns>
		/// ------------------------------------------------------------------------------------
		public static ICustomMarshaler GetInstance(string strCookie)
		{
			m_Marshaler ??= new ArrayPtrMarshaler(strCookie);

			return m_Marshaler;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Performs necessary cleanup of the managed data when it is no longer needed
		/// </summary>
		/// <param name="managedObj">The managed object to be destroyed</param>
		/// ------------------------------------------------------------------------------------
		public void CleanUpManagedData(object managedObj)
		{
			if (managedObj is IDisposable disposable)
				disposable.Dispose();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Performs necessary cleanup of the unmanaged data when it is no longer needed
		/// </summary>
		/// <param name="pNativeData">A pointer to the unmanaged data to be destroyed</param>
		/// ------------------------------------------------------------------------------------
		public void CleanUpNativeData(IntPtr pNativeData)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns the size of the native data to be marshaled
		/// </summary>
		/// <returns>The size in bytes of the native data</returns>
		/// ------------------------------------------------------------------------------------
		public int GetNativeDataSize()
		{
			return Marshal.SizeOf(typeof(IntPtr));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Converts the managed data to unmanaged data
		/// </summary>
		/// <param name="managedObj">The managed object to be converted</param>
		/// <returns>Returns the COM view of the managed object</returns>
		/// ------------------------------------------------------------------------------------
		public IntPtr MarshalManagedToNative(object managedObj)
		{
			return ((ArrayPtr)managedObj).IntPtr;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Converts the unmanaged data to managed data
		/// </summary>
		/// <param name="pNativeData">A pointer to the unmanaged data to be wrapped</param>
		/// <returns>Returns the managed view of the COM data</returns>
		/// ------------------------------------------------------------------------------------
		public object? MarshalNativeToManaged(IntPtr pNativeData)
		{
			return (ArrayPtr)pNativeData;
		}
	}
	#endregion
}
