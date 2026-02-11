using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SIL.LCModel
{
   internal class FileTransactionLogger : ITransactionLogger, IDisposable
   {
	   private readonly object m_lock = new object();
	   private FileStream m_stream;
	   private bool m_disposed;
	   
	   internal FileTransactionLogger(string filePath)
	   {
		   m_stream = File.Open(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
	   }

	   ~FileTransactionLogger()
	   {
		   Dispose(false);
	   }

	   public void AddBreadCrumb(string description)
	   {
		   if (description == null || m_disposed)
			   return;
		   lock (m_lock)
		   {
			   if (m_disposed)
				   return;
			   var bytes = Encoding.UTF8.GetBytes(description + Environment.NewLine);
			   m_stream.Write(bytes, 0, bytes.Length);
			   m_stream.Flush();
		   }
	   }

	   protected virtual void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType() + ". *******");
			lock (m_lock)
			{
				if (!m_disposed)
				{
					m_stream?.Flush();
					m_stream?.Dispose();
					m_stream = null;
					m_disposed = true;
				}
			}
		}

		public void Dispose()
	   {
		   Dispose(true);
		   GC.SuppressFinalize(this);
		}
   }
}
