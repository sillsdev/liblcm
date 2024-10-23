using System;
using System.Linq;
using NUnit.Framework;

namespace SIL.LCModel.Infrastructure.Impl
{
	public class LexEntryTypeTests: MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		[Test]
		public void CreateTypeWithGuid_CanAddToList()
		{
			var guid = Guid.NewGuid();
			var lexEntryType = Cache.ServiceLocator.GetInstance<ILexEntryTypeFactory>().Create(guid);

			Cache.LangProject.LexDbOA.ComplexEntryTypesOA.PossibilitiesOS.Add(lexEntryType);

			Assert.That(Cache.ServiceLocator.ObjectRepository.GetObject(guid), Is.EqualTo(lexEntryType));
		}
	}
}