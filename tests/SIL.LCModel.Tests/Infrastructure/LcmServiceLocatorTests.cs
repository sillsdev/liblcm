// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Linq;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;

namespace SIL.LCModel.Infrastructure
{
	/// <summary>
	/// Smoke tests for the LCM service locator wiring.
	/// </summary>
	[TestFixture]
	public class LcmServiceLocatorTests : MemoryOnlyBackendProviderTestBase
	{
		/// <summary/>
		[Test]
		public void SmokeTest_ResolvesCoreServices()
		{
			var sl = Cache.ServiceLocator;
			Assert.That(sl, Is.Not.Null);

			Assert.That(sl.GetInstance<IDataSetup>(), Is.Not.Null);
			Assert.That(sl.GetInstance<ICmObjectRepository>(), Is.Not.Null);
			Assert.That(sl.GetInstance<IFwMetaDataCacheManaged>(), Is.Not.Null);

			Assert.That(sl.DataSetup, Is.SameAs(sl.GetInstance<IDataSetup>()));
			Assert.That(sl.ObjectRepository, Is.SameAs(sl.GetInstance<ICmObjectRepository>()));
			Assert.That(sl.MetaDataCache, Is.SameAs(sl.GetInstance<IFwMetaDataCacheManaged>()));
			Assert.That(sl.ActionHandler, Is.SameAs(sl.GetInstance<IActionHandler>()));
		}

		/// <summary/>
		[Test]
		public void GetAllInstances_ReturnsRegisteredServices()
		{
			var instances = Cache.ServiceLocator.GetAllInstances<IDataSetup>().ToList();
			Assert.That(instances, Is.Not.Empty);
			Assert.That(instances[0], Is.SameAs(Cache.ServiceLocator.GetInstance<IDataSetup>()));
		}


		/// <summary/>
		[Test]
		public void GetAllInstances_ReturnsEmptyForUnregisteredType()
		{
			var instances = Cache.ServiceLocator.GetAllInstances<IUnregisteredForServiceLocatorTest>().ToList();
			Assert.That(instances, Is.Empty);
		}

		private interface IUnregisteredForServiceLocatorTest { }
	}
}
