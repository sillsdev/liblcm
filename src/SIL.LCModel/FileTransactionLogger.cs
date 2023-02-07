using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SIL.LCModel
{
   internal class FileTransactionLogger : ITransactionLogger, IDisposable
   {
	   private object m_lock = new object();
	   private FileStream m_stream;
	   
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
		   lock (m_lock)
		   {
			   m_stream.WriteAsync(Encoding.UTF8.GetBytes((description + Environment.NewLine).ToCharArray()), 0,
				   description.Length + 1);
		   }
	   }

	   protected virtual void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType() + ". *******");
			lock (m_stream)
			{
				m_stream?.Flush();
				m_stream?.Dispose();
			}
		}

		public void Dispose()
	   {
		   Dispose(true);
		   GC.SuppressFinalize(this);
		}
   }
}
