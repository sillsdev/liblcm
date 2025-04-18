using System.Collections.ObjectModel;

namespace SIL.LCModel.Build.Tasks
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Collection which can also be accessed by Name.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	internal class StringKeyCollection<T> : KeyedCollection<string, T> where T : notnull
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// When implemented in a derived class, extracts the key from the specified element.
		/// </summary>
		/// <param name="item">The element from which to extract the key.</param>
		/// <returns>The key for the specified element.</returns>
		/// ------------------------------------------------------------------------------------
		protected override string GetKeyForItem(T item)
		{
			return item.ToString();
		}
	}
}