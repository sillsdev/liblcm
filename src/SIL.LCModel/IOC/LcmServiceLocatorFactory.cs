// Copyright (c) 2009-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CommonServiceLocator;
using Microsoft.Extensions.DependencyInjection;
using SIL.LCModel.Application;
using SIL.LCModel.Application.Impl;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainImpl;
using SIL.LCModel.DomainServices;
using SIL.LCModel.DomainServices.DataMigration;
using SIL.LCModel.Infrastructure;
using SIL.LCModel.Infrastructure.Impl;

namespace SIL.LCModel.IOC
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Factory for hard-wired LCM Common Service Locator.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	internal sealed partial class LcmServiceLocatorFactory
	{
		private readonly BackendProviderType m_backendProviderType;
		private readonly ILcmUI m_ui;
		private readonly ILcmDirectories m_dirs;
		private readonly LcmSettings m_settings;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="backendProviderType">Type of backend provider to create.</param>
		/// <param name="ui">The UI service.</param>
		/// <param name="dirs">The directories service.</param>
		/// <param name="settings">The LCM settings.</param>
		internal LcmServiceLocatorFactory(BackendProviderType backendProviderType, ILcmUI ui, ILcmDirectories dirs, LcmSettings settings)
		{
			m_backendProviderType = backendProviderType;
			m_ui = ui;
			m_dirs = dirs;
			m_settings = settings;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Create an IServiceLocator instance.
		/// </summary>
		/// <returns>An IServiceLocator instance.</returns>
		/// ------------------------------------------------------------------------------------
		public IServiceProvider CreateServiceLocator()
		{
			ITransactionLogger logger = null;

			var logPath = Environment.GetEnvironmentVariable("LCM_TransactionLogPath");
			if (!string.IsNullOrEmpty(logPath))
			{
				logger = new FileTransactionLogger(Path.Combine(logPath, $"lcm_transaction.{DateTime.Now.Ticks}.log"));
			}

			// All registrations are explicit factory lambdas that call the (often internal)
			// constructors directly. This is legal because all registration code lives inside
			// SIL.LCModel, and it makes the whole object graph compile-time checked: a changed
			// constructor breaks the build here instead of failing at runtime resolution.
			var services = new ServiceCollection();

			// Add data migration manager. (new one per request)
			services.AddTransient<IDataMigrationManager>(sp => new LcmDataMigrationManager());

			// Add HomographConfiguration
			services.AddSingleton<HomographConfiguration>(sp => new HomographConfiguration());

			// Add LcmCache
			services.AddSingleton<LcmCache>(sp => new LcmCache());

			// Add IParagraphCounterRepository
			services.AddSingleton<IParagraphCounterRepository, ParagraphCounterRepository>();

			// Add MDC
			services.AddSingleton<IFwMetaDataCacheManaged>(sp => new LcmMetaDataCache());
			// Register its other interface.
			services.AddTransient<IFwMetaDataCacheManagedInternal>(sp =>
				(IFwMetaDataCacheManagedInternal)sp.GetRequiredService<IFwMetaDataCacheManaged>());

			// Add Virtuals
			services.AddSingleton<Virtuals>(sp =>
				new Virtuals(sp.GetRequiredService<IFwMetaDataCacheManaged>()));

			// Add IdentityMap
			services.AddSingleton<IdentityMap>();
			// Register IdentityMap's other interface.
			services.AddTransient<ICmObjectIdFactory>(sp =>
				(ICmObjectIdFactory)sp.GetRequiredService<IdentityMap>());
			services.AddTransient<ICmObjectRepositoryInternal>(sp =>
				(ICmObjectRepositoryInternal)sp.GetRequiredService<ICmObjectRepository>());

			// Add surrogate factory (internal);
			services.AddSingleton<ICmObjectSurrogateFactory, CmObjectSurrogateFactory>();

			// Add surrogate repository (internal);
			services.AddSingleton<ICmObjectSurrogateRepository, CmObjectSurrogateRepository>();

			// Add BEP.
			switch (m_backendProviderType)
			{
				default:
					throw new InvalidOperationException(Strings.ksInvalidBackendProviderType);
				case BackendProviderType.kXML:
					services.AddSingleton<IDataSetup, XMLBackendProvider>();
					break;
				case BackendProviderType.kMemoryOnly:
					services.AddSingleton<IDataSetup, MemoryOnlyBackendProvider>();
					break;
				case BackendProviderType.kSharedXML:
					services.AddSingleton<IDataSetup, SharedXMLBackendProvider>();
					break;
			}
			// Register two additional interfaces of the BEP, which are injected into other services.
			services.AddTransient<IDataStorer>(sp => (IDataStorer)sp.GetRequiredService<IDataSetup>());
			services.AddTransient<IDataReader>(sp => (IDataReader)sp.GetRequiredService<IDataSetup>());

			// Add Mediator
			services.AddSingleton<IUnitOfWorkService, UnitOfWorkService>();
			// Register additional interfaces for the UnitOfWorkService.
			services.AddTransient<ISilDataAccessHelperInternal>(sp =>
				(ISilDataAccessHelperInternal)sp.GetRequiredService<IUnitOfWorkService>());
			// IActionHandler is deliberately transient: it returns the current ActiveUndoStack,
			// which changes over the life of the UnitOfWorkService, so it must be re-evaluated
			// on every resolution.
			services.AddTransient<IActionHandler>(sp =>
				((UnitOfWorkService)sp.GetRequiredService<IUnitOfWorkService>()).ActiveUndoStack);
			services.AddTransient<IWorkerThreadReadHandler>(sp =>
				(IWorkerThreadReadHandler)sp.GetRequiredService<IUnitOfWorkService>());
			services.AddTransient<IUndoStackManager>(sp =>
				(IUndoStackManager)sp.GetRequiredService<IUnitOfWorkService>());
			if (logger != null)
				services.AddSingleton<ITransactionLogger>(logger);

			// Add generated factories.
			AddFactories(services);

			// Add generated Repositories
			AddRepositories(services);

			// Add IAnalysisRepository
			services.AddSingleton<IAnalysisRepository, AnalysisRepository>();

			// Add ReferenceAdjusterService
			services.AddSingleton<IReferenceAdjuster>(sp => new ReferenceAdjusterService());

			// Add SDA
			services.AddSingleton<ISilDataAccessManaged, DomainDataByFlid>();

			// Add loader helper
			services.AddSingleton<LoadingServices>();

			// StTxtParaBldr is a stateful builder resolved by its concrete type. StructureMap
			// auto-built it per request; register it transient to preserve that behavior.
			services.AddTransient<StTxtParaBldr>();

			// Add writing system manager
			services.AddSingleton<WritingSystemManager>(sp =>
				new WritingSystemManager {TemplateFolder = m_dirs.TemplateDirectory});
			services.AddTransient<ILgWritingSystemFactory>(sp =>
				(ILgWritingSystemFactory)sp.GetRequiredService<WritingSystemManager>());

			services.AddTransient<IWritingSystemContainer>(sp =>
				sp.GetRequiredService<ILangProjectRepository>().Singleton);

			services.AddSingleton<ILcmUI>(m_ui);

			services.AddSingleton<ILcmDirectories>(m_dirs);

			services.AddSingleton<LcmSettings>(m_settings);

			// =================================================================================
			// Don't add COM objects to the container. The container does not properly release
			// COM objects when it is disposed; it will crash when the container is disposed.
			// =================================================================================

			var serviceProvider = services.BuildServiceProvider();

			return new MediServiceLocator(serviceProvider);
		}
	}

	/// <summary>
	/// Service locator backed by Microsoft.Extensions.DependencyInjection, exposing the extra
	/// methods of ILcmServiceLocator. It continues to implement CommonServiceLocator's
	/// <see cref="ServiceLocatorImplBase"/> so downstream code that binds GetInstance&lt;T&gt;
	/// to the interface method keeps working, source- and binary-compatible.
	/// </summary>
	internal sealed class MediServiceLocator : ServiceLocatorImplBase,
		ILcmServiceLocator, IServiceLocatorInternal, IDisposable
	{
		private ServiceProvider m_serviceProvider;

		/// <summary>
		/// Constructor
		/// </summary>
		internal MediServiceLocator(ServiceProvider serviceProvider)
		{
			m_serviceProvider = serviceProvider;
		}

		#region Disposable stuff
		#if DEBUG
		/// <summary/>
		~MediServiceLocator()
		{
			Dispose(false);
		}
		#endif

		/// <summary/>
		public bool IsDisposed
		{
			get;
			private set;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Releases all resources
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary/>
		private void Dispose(bool fDisposing)
		{
			Debug.WriteLineIf(!fDisposing, "****** Missing Dispose() call for " + GetType() + " *******");
			if (fDisposing && !IsDisposed)
			{
				// dispose managed and unmanaged objects
				if (m_serviceProvider != null)
				{
					try
					{
						m_serviceProvider.Dispose();
					}
					catch (InvalidComObjectException e) // Intermittantly the dispose of the container fails because a COM object has become invalid
					{
						// Display an indication of the failure, but don't crash, we made a good faith effort to dispose all our COM objects
						// and they probably were disposed. Also at this point we are probably shutting down, or wrapping up a unit test.
						Debug.WriteLine(String.Format(@"COM problem when disposing container in MediServiceLocator: {0}", e.Message));
					}
				}
			}
			m_serviceProvider = null;
			IsDisposed = true;
		}
		#endregion

		#region Implementation of abstract methods
		/// <summary>
		///             When implemented by inheriting classes, this method will do the actual work of resolving
		///             the requested service instance.
		/// </summary>
		/// <param name="serviceType">Type of instance requested.</param>
		/// <param name="key">Name of registered service you want. May be null.</param>
		/// <returns>
		/// The requested service instance.
		/// </returns>
		protected override object DoGetInstance(Type serviceType, string key)
		{
			// LCM does not use named instances; resolve strictly by type. GetRequiredService
			// preserves the throw-on-missing semantics of the previous StructureMap container.
			return m_serviceProvider.GetRequiredService(serviceType);
		}

		/// <summary>
		///             When implemented by inheriting classes, this method will do the actual work of
		///             resolving all the requested service instances.
		/// </summary>
		/// <param name="serviceType">Type of service requested.</param>
		/// <returns>
		/// Sequence of service instance objects.
		/// </returns>
		protected override IEnumerable<object> DoGetAllInstances(Type serviceType)
		{
			var enumerableType = typeof(IEnumerable<>).MakeGenericType(serviceType);
			var instances = (IEnumerable<object>?)m_serviceProvider.GetServices(enumerableType);
			return instances ?? Enumerable.Empty<object>();
		}

		#endregion

		#region Overrides of IServiceLocator implementation

		/// <summary>
		/// Get an instance of the given <typeparamref name="TService"/>.
		/// </summary>
		/// <typeparam name="TService">Type of object requested.</typeparam><exception cref="T:Microsoft.Practices.ServiceLocation.ActivationException">if there is are errors resolving
		///			 the service instance.</exception>
		/// <returns>
		/// The requested service instance.
		/// </returns>
		public override TService GetInstance<TService>()
		{
			// IActionHandler is special - want to return the current one in use.
			if (typeof(TService) == typeof(IActionHandler))
				return (TService)ActionHandler;
			return base.GetInstance<TService>();
		}

		#endregion

		#region Implementation of ILcmServiceLocator

		/// <summary>
		/// Shortcut. Don't try to cache this locally, it can change from one call to another!
		/// </summary>
		public IActionHandler ActionHandler
		{
			get { return ((UnitOfWorkService) UnitOfWorkService).ActiveUndoStack; }
		}

		/// <summary>
		/// Shortcut
		/// </summary>
		public IUnitOfWorkService UnitOfWorkService
		{
			get
			{
				return GetInstance<IUnitOfWorkService>();
			}
		}

		/// <summary>
		/// Shortcut
		/// </summary>
		public ICmObjectIdFactory CmObjectIdFactory
		{
			get
			{
				return GetInstance<ICmObjectIdFactory>();
			}
		}

		/// <summary>
		/// Shortcut
		/// </summary>
		public IDataSetup DataSetup
		{
			get
			{
				return GetInstance<IDataSetup>();
			}
		}

		public IDataReader DataReader
		{
			get
			{
				return GetInstance<IDataReader>();
			}
		}

		/// <summary>
		/// Get the specified object instance; short for getting ICmObjectRepository and asking it to GetObject.
		/// </summary>
		public ICmObject GetObject(int hvo)
		{
			return GetInstance<ICmObjectRepository>().GetObject(hvo);
		}

		/// <summary>
		/// Answers true iff GetObject(hvo) will succeed; useful to avoid throwing and catching exceptions
		/// when possibly working with fake objects.
		/// </summary>
		public bool IsValidObjectId(int hvo)
		{
			return GetInstance<ICmObjectRepository>().IsValidObjectId(hvo);

		}


		/// <summary>
		/// Get the specified object instance; short for getting ICmObjectRepository and asking it to GetObject.
		/// </summary>
		public ICmObject GetObject(Guid guid)
		{
			return GetInstance<ICmObjectRepository>().GetObject(guid);
		}

		/// <summary>
		/// Get the specified object instance; short for getting ICmObjectRepository and asking it to GetObject.
		/// </summary>
		public ICmObject GetObject(ICmObjectId id)
		{
			return GetInstance<ICmObjectRepository>().GetObject(id);
		}

		/// <summary>
		/// Shortcut to the WS manager.
		/// </summary>
		public WritingSystemManager WritingSystemManager
		{
			get
			{
				return GetInstance<WritingSystemManager>();
			}
		}

		/// <summary>
		/// Shortcut to the WS container.
		/// </summary>
		public IWritingSystemContainer WritingSystems
		{
			get
			{
				return GetInstance<IWritingSystemContainer>();
			}
		}

		/// <summary>
		/// Shortcut.
		/// </summary>
		public ICmObjectRepository ObjectRepository
		{
			get
			{
				return GetInstance<ICmObjectRepository>();
			}
		}

		/// <summary>
		/// Shortcut.
		/// </summary>
		public ICmObjectIdFactory ObjectIdFactory
		{
			get
			{
				return GetInstance<ICmObjectIdFactory>();
			}
		}

		/// <summary>
		/// Shortcut.
		/// </summary>
		public IFwMetaDataCacheManaged MetaDataCache
		{
			get
			{
				return GetInstance<IFwMetaDataCacheManaged>();
			}
		}

		/// <summary>
		/// Shortcut to the writing system factory that gives meaning to writing systems.
		/// </summary>
		public ILgWritingSystemFactory WritingSystemFactory
		{
			get
			{
				return GetInstance<ILgWritingSystemFactory>();
			}
		}

		/// <summary>
		/// Shortcut to a service used in fluffing up surrogates.
		/// </summary>
		public LoadingServices LoadingServices
		{
			get
			{
				return GetInstance<LoadingServices>();
			}
		}

		/// <summary>
		/// Shortcut to the map used to find the one and only instance of LCM object for any given id.
		/// </summary>
		public IdentityMap IdentityMap
		{
			get
			{
				return GetInstance<IdentityMap>();
			}
		}
		#endregion
	}
}
