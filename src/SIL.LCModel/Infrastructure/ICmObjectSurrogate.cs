// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Xml.Linq;

namespace SIL.LCModel.Infrastructure
{
	/// <summary>
	/// Internal interface for the CmObjectSurrogate.
	/// </summary>
	internal interface ICmObjectSurrogate : ICmObjectOrSurrogate
	{
		/// <summary>
		/// Connect an object with a surrogate, during bootstrap of extant system.
		/// </summary>
		/// <param name="obj"></param>
		void AttachObject(ICmObject obj);
		
		/// <summary>
		/// Get the Object's Guid.
		/// </summary>
		Guid Guid { get; }

		/// <summary>
		/// Reset the class and xml, after a data migration (and before reconstitution).
		/// </summary>
		/// <param name="className">Class name. (May be the same).</param>
		/// <param name="xml">New XML.</param>
		void Reset(string className, string xml);

		/// <summary>
		/// Reset the class and xml, after a data migration (and before reconstitution).
		/// </summary>
		/// <param name="className">Class name. (May be the same).</param>
		/// <param name="xmlBytes">New XML.</param>
		void Reset(string className, byte[] xmlBytes);
	}

	/// <summary>
	/// This interface encapsulates the behaviors needed in sets of objects that might be either CmObjects
	/// or surrogates, as used in persistence and Unit of Work.
	/// </summary>
	internal interface ICmObjectOrSurrogate
	{
		ICmObjectId Id { get; }

		/// <summary>
		/// Get the Object's classname.
		/// </summary>
		string Classname { get; }

		/// <summary>
		/// Get the CmObject.
		/// </summary>
		ICmObject Object { get; }

		/// <summary>
		/// Find out if the surrogate has the actual object.
		/// </summary>
		bool HasObject { get; }

		/// <summary>
		/// Return the DTO for the object
		/// </summary>
		ICmObjectDTO DTO { get; }
	}

	/// <summary>
	/// ICmObjectSurrogate factory.
	/// </summary>
	internal interface ICmObjectSurrogateFactory
	{
		/// <summary/>
		/// Create a surrogate from the data store.
		ICmObjectSurrogate Create(ICmObjectDTO dto);

		/// <summary>
		/// Create a surrogate from the data store.
		/// </summary>
		ICmObjectSurrogate Create(Guid guid, string classname, ICmObjectDTO data);
		
		/// <summary>
		/// Create a surrogate from the data store.
		/// This gets the full XML string of the object from the BEP.
		/// </summary>
		ICmObjectSurrogate Create(ICmObjectId objId, string classname, ICmObjectDTO xmlData);

		/// <summary>
		/// Create a surrogate from the data store.
		/// This gets the full XML string of the object from the BEP.
		/// </summary>
		ICmObjectSurrogate Create(ICmObjectOrSurrogate sourceSurrogate);

		/// <summary>
		/// Create one from an existing object; set its XML to the current state of the object.
		/// </summary>
		ICmObjectSurrogate Create(ICmObject obj);
	}

	/// <summary>
	/// ICmObjectSurrogate repository.
	/// </summary>
	internal interface ICmObjectSurrogateRepository
	{
		/// <summary>
		/// Get an id from the Guid in an XElement.
		/// Enhance JohnT: this belongs in some other interface now it no longer returns a surrogate.
		/// </summary>
		ICmObjectId GetId(XElement reader);

		/// <summary>
		/// Get a surrogate of the ICmObject.
		/// </summary>
		/// <param name="obj"></param>
		/// <returns>The surrogate of the ICmObject.</returns>
		ICmObjectSurrogate GetSurrogate(ICmObject obj);
	}
}