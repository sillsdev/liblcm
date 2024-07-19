// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using CommonServiceLocator;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.Infrastructure;
using SIL.LCModel.Infrastructure.Impl;
using SIL.LCModel.IOC;

namespace SIL.LCModel
{
	/// <summary>
	/// This interface defines LCM extensions to IServiceLocator, mainly shortcuts for particular
	/// GetService() calls.
	/// </summary>
	public interface ILcmServiceLocator : IServiceProvider
	{
		/// <summary>
		/// Shortcut to the IActionHandler instance.
		/// </summary>
		IActionHandler ActionHandler { get; }

		/// <summary>
		/// Shortcut to the ICmObjectIdFactory instance.
		/// </summary>
		ICmObjectIdFactory CmObjectIdFactory { get; }

		/// <summary>
		/// Shortcut to the IDataSetup instance.
		/// </summary>
		IDataSetup DataSetup { get; }

		/// <summary>
		/// Get the specified object instance; short for getting ICmObjectRepository and asking it to GetObject.
		/// </summary>
		ICmObject GetObject(int hvo);

		/// <summary>
		/// Answers true iff GetObject(hvo) will succeed; useful to avoid throwing and catching exceptions
		/// when possibly working with fake objects.
		/// </summary>
		bool IsValidObjectId(int hvo);

		/// <summary>
		/// Get the specified object instance; short for getting ICmObjectRepository and asking it to GetObject.
		/// </summary>
		ICmObject GetObject(Guid guid);

		/// <summary>
		/// Get the specified object instance; short for getting ICmObjectRepository and asking it to GetObject.
		/// </summary>
		ICmObject GetObject(ICmObjectId id);

		/// <summary>
		/// Shortcut to the WS manager.
		/// </summary>
		WritingSystemManager WritingSystemManager { get; }

		/// <summary>
		/// Gets the writing system container.
		/// </summary>
		/// <value>The writing system container.</value>
		IWritingSystemContainer WritingSystems { get; }

		/// <summary>
		/// The place to get CmObjects.
		/// </summary>
		ICmObjectRepository ObjectRepository { get; }

		/// <summary>
		/// The thing that knows how to make ICmObjectIds.
		/// </summary>
		ICmObjectIdFactory ObjectIdFactory { get; }

		/// <summary>
		/// Shortcut to the meta data cache that gives information about the properties of objects.
		/// </summary>
		IFwMetaDataCacheManaged MetaDataCache { get;  }

		/// <summary>
		/// Shortcut to the writing system factory that gives meaning to writing systems.
		/// </summary>
		ILgWritingSystemFactory WritingSystemFactory { get; }
	}

	/// <summary>
	/// A further interface typically implemented by service locator, for services that should stay
	/// internal to LCM.
	/// </summary>
	internal interface IServiceLocatorInternal
	{
		IdentityMap IdentityMap { get; }
		LoadingServices LoadingServices { get; }
		IUnitOfWorkService UnitOfWorkService { get; }
	}

	/// <summary>
	/// Helpers to provide drop in methods that match the api of IServiceLocator, but use IServiceProvider instead.
	/// </summary>
	public static class IocHelpers
	{
		public static object GetInstance(this IServiceProvider provider, Type serviceType)
		{
			//todo how to handle null? Should we throw an exception?
			return provider.GetService(serviceType);
		}

		public static TService GetInstance<TService>(this IServiceProvider provider)
		{
			return (TService)provider.GetService(typeof(TService));
		}

		public static IEnumerable<TService> GetAllInstances<TService>(this IServiceProvider provider)
		{
			//structure map might not work the same way as the standard service provider, so we need to handle it separately.
			if (provider is IServiceLocator serviceLocator) return serviceLocator.GetAllInstances<TService>();
			//the standard service provider handles listing all services like this, however that might not work the same in structure map if an IEnumerable is explicitly registered.
			return (IEnumerable<TService>) provider.GetService(typeof(IEnumerable<TService>));
		}
	}
}
