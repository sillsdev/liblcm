// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace SIL.LCModel
{
	[TestFixture]
	public class FileTransactionLoggerTests
	{
		private string m_tempFile;

		[SetUp]
		public void Setup()
		{
			m_tempFile = Path.GetTempFileName();
		}

		[TearDown]
		public void Teardown()
		{
			try
			{
				if (File.Exists(m_tempFile))
					File.Delete(m_tempFile);
			}
			catch
			{
				// best-effort cleanup
			}
		}

		/// <summary>
		/// After Dispose(), calling AddBreadCrumb must not throw ObjectDisposedException.
		/// In production the timer thread can race against Dispose.
		/// </summary>
		[Test]
		public void AddBreadCrumb_AfterDispose_DoesNotThrow()
		{
			var logger = new FileTransactionLogger(m_tempFile);
			logger.Dispose();

			Assert.DoesNotThrow(() => logger.AddBreadCrumb("late write"));
		}

		/// <summary>
		/// Passing a null description must not throw NullReferenceException.
		/// </summary>
		[Test]
		public void AddBreadCrumb_NullDescription_DoesNotThrow()
		{
			using (var logger = new FileTransactionLogger(m_tempFile))
			{
				Assert.DoesNotThrow(() => logger.AddBreadCrumb(null));
			}
		}

		/// <summary>
		/// The full message including newline must be written, not truncated.
		/// On Windows Environment.NewLine is "\r\n" (2 chars) but old code used
		/// description.Length + 1, losing the final byte.
		/// </summary>
		[Test]
		public void AddBreadCrumb_WritesFullMessageIncludingNewline()
		{
			const string message = "hello";
			using (var logger = new FileTransactionLogger(m_tempFile))
			{
				logger.AddBreadCrumb(message);
			}

			// Dispose flushes the stream, so the file is readable immediately.
			string content = File.ReadAllText(m_tempFile, Encoding.UTF8);
			string expected = message + Environment.NewLine;
			Assert.AreEqual(expected, content,
				"AddBreadCrumb should write the full message plus Environment.NewLine");
		}
	}
}
