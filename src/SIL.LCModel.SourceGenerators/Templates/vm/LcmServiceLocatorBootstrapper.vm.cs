## --------------------------------------------------------------------------------------------
## Copyright (c) 2009-2013 SIL International
## This software is licensed under the LGPL, version 2.1 or later
## (http://www.gnu.org/licenses/lgpl-2.1.html)
##
## NVelocity template file
## This file is used by the LcmGenerate task to generate the source code from the XMI
## database model.
## --------------------------------------------------------------------------------------------
using Microsoft.Extensions.DependencyInjection;
using SIL.LCModel.DomainImpl;
using SIL.LCModel.Infrastructure;
using SIL.LCModel.Infrastructure.Impl;

namespace SIL.LCModel.IOC
{
	/// <summary>
	/// Bootstrapper for LCM Common Service Locator.
	/// </summary>
	internal partial class LcmServiceLocatorFactory
	{
		// Each type is registered by its concrete type (the actual singleton) with the
		// interface registered as an alias resolving to that same singleton. This mirrors the
		// previous StructureMap container, which allowed resolving a registered service by
		// either its interface or its concrete implementation type.
		private static void AddFactories(IServiceCollection services)
		{
#foreach($module in $lcmgenerate.Modules)
#foreach($class in $module.Classes)
#if ($class.Name == "LgWritingSystem")
#set( $classSfx = "FactoryLcm" )
#else
#set( $classSfx = "Factory" )
#end
#if(!$class.IsAbstract)
			services.AddSingleton<${class.Name}$classSfx>(sp => new ${class.Name}$classSfx(sp.GetRequiredService<LcmCache>()));
			services.AddSingleton<I${class.Name}$classSfx>(sp => sp.GetRequiredService<${class.Name}$classSfx>());
#end
#end
#end
		}

		private static void AddRepositories(IServiceCollection services)
		{
#foreach($module in $lcmgenerate.Modules)
#foreach($class in $module.Classes)
			services.AddSingleton<${class.Name}Repository>(sp => new ${class.Name}Repository(
				sp.GetRequiredService<LcmCache>(), sp.GetRequiredService<IDataReader>()));
			services.AddSingleton<I${class.Name}Repository>(sp => sp.GetRequiredService<${class.Name}Repository>());
#end
#end
		}
	}
}
