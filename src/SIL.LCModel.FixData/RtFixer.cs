// Copyright (c) 2011-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace SIL.LCModel.FixData
{
	/// <summary>
	/// This abstract class provides the interface for fixing problems on an element\row\CmObject level.
	/// The members m_guids and m_owners can be used by fixes which need global information.
	/// </summary>
	internal abstract class RtFixer
	{
		protected HashSet<Guid>                       m_guids               = new HashSet<Guid>();
		protected Dictionary<Guid, Guid>              m_owners              = new Dictionary<Guid, Guid>();
		protected Dictionary<string, HashSet<string>> m_parentToOwnedObjsur = new Dictionary<string, HashSet<string>>();
		protected HashSet<string>                     m_rtElementsToDelete  = new HashSet<string>();

		internal virtual void FinalFixerInitialization(Dictionary<Guid, Guid> owners,              HashSet<Guid>   guids,
			Dictionary<string, HashSet<string>>                               parentToOwnedObjsur, HashSet<string> rtElementsToDelete)
		{
			m_owners = owners;
			m_guids = guids;
			m_parentToOwnedObjsur = parentToOwnedObjsur;
			m_rtElementsToDelete = rtElementsToDelete;
		}

		/// <summary>
		/// This method gives each fixer the opportunity to look at any custom fields the db might have.
		/// </summary>
		/// <param name="additionalFieldsElem">CustomField elements are direct children of this XElement.</param>
		internal virtual void InspectAdditionalFieldsElement(XElement additionalFieldsElem)
		{
		}

		/// <summary>
		/// Do any fixes to this particular root element here.
		/// Return true if we are done fixing this element and can write it out.
		/// Return false if we need to delete this root element.
		/// </summary>
		/// <param name="rt"></param>
		/// <param name="logger"></param>
		/// <returns></returns>
		internal abstract bool FixElement(XElement rt, FwDataFixer.ErrorLogger logger);

		/// <summary>
		/// Override this method if a Fixer needs to gather information on one pass in FwDataFixer.ProcessDocument().
		/// in order to fix everything on another pass (with FixElement). Try hard to limit your time in here to a
		/// small subset of the available rt elements!
		/// </summary>
		/// <param name="rt"></param>
		internal virtual void InspectElement(XElement rt)
		{
			// Base class does nothing.
		}

		//Delete an object and recursively find and delete all owned decendants of the object.
		internal void MarkObjForDeletionAndDecendants(string rtElementGuid)
		{
			m_rtElementsToDelete.Add(rtElementGuid);

			//If the element has any owned decendants then mark those for deletion also.
			HashSet<string> ownedObjsurs;
			m_parentToOwnedObjsur.TryGetValue(rtElementGuid, out ownedObjsurs);
			if (ownedObjsurs != null)
			{
				foreach (var ownedObj in ownedObjsurs)
				{
					MarkObjForDeletionAndDecendants(ownedObj);
				}
			}
		}

		/// <summary>
		/// Reset this instance to its original state.  Overrides should call the base method.
		/// </summary>
		internal virtual void Reset()
		{
			m_owners.Clear();
			m_guids.Clear();
			m_parentToOwnedObjsur.Clear();
			m_rtElementsToDelete.Clear();
		}
	}
}