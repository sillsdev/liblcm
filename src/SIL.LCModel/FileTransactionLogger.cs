using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SIL.LCModel
{
   internal class FileTransactionLogger : ITransactionLogger, IDisposable
   {
	   private readonly object m_lock = new object();
	   private FileStream m_stream;
	   private Task m_lastWrite = Task.CompletedTask;
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
			   m_lastWrite = m_lastWrite.ContinueWith(_ =>
				   m_stream.WriteAsync(bytes, 0, bytes.Length),
				   TaskContinuationOptions.ExecuteSynchronously).Unwrap();
		   }
	   }

	   protected virtual void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType() + ". *******");
			lock (m_lock)
			{
				if (!m_disposed)
				{
					try { m_lastWrite?.GetAwaiter().GetResult(); }
					catch { /* best-effort: don't let a failed write prevent disposal */ }
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
