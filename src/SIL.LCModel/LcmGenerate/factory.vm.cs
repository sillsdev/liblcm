## --------------------------------------------------------------------------------------------
## --------------------------------------------------------------------------------------------
## Copyright (c) 2006-2013 SIL International
## This software is licensed under the LGPL, version 2.1 or later
## (http://www.gnu.org/licenses/lgpl-2.1.html)
##
## NVelocity template file
## This file is used by the LcmGenerate task to generate the source code from the XMI
## database model.
## --------------------------------------------------------------------------------------------
#set( $className = $class.Name )
#set( $baseClassName = $class.BaseClass.Name )
#if ($class.Name == "LgWritingSystem")
#set( $classSfx = "FactoryLcm" )
#else
#set( $classSfx = "Factory" )
#end

	#region ${className}$classSfx
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Generated implementation of: I${className}$classSfx
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	internal partial class ${className}$classSfx : I${className}$classSfx, ILcmFactoryInternal
	{
		private readonly LcmCache m_cache;

		/// <summary>
		/// Construction.
		/// </summary>
		/// <param name="cache"></param>
		internal ${className}$classSfx(LcmCache cache)
		{
			if (cache == null) throw new ArgumentNullException("cache");
			m_cache = cache;
		}

#if ($class.GenerateFullCreateMethod)
		/// <summary>
		/// Basic creation method for an $className.
		/// </summary>
		/// <returns>A new, unowned, $className.</returns>
		public I$className Create()
		{
#if ($class.IsSingleton)
			if (m_cache.ServiceLocator.GetInstance<I${className}Repository>().Singleton != null)
				throw new InvalidOperationException("Can not create more than one ${className}");
#end
			var newby = new $className();
			if (newby.OwnershipStatus != ClassOwnershipStatus.kOwnerRequired)
				((ICmObjectInternal)newby).InitializeNewOwnerlessCmObject(m_cache);
			return newby;
		}
#end

		/// <summary>
		/// Basic creation method for an ICmObject
		/// </summary>
		/// <returns>A new, unowned, ICmObject with a Guid, but no Hvo.</returns>
		public ICmObject CreateInternal()
		{
#if ($class.GenerateFullCreateMethod)
			return Create();
#else
			var newby = new $className();
			Debug.Assert(newby.OwnershipStatus != ClassOwnershipStatus.kOwnerProhibited);
			return newby;
#end
		}
	}
	#endregion ${className}$classSfx
