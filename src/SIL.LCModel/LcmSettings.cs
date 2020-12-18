// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using SIL.LCModel.Infrastructure.Impl;
using SIL.Lexicon;

namespace SIL.LCModel
{
	/// <summary>
	/// This class captures all of the configurable settings for LCM.
	/// </summary>
	public sealed class LcmSettings
	{
		private bool m_frozen;
		private bool m_disableDataMigration;
		private int m_sharedXMLBackendCommitLogSize;

		/// <summary>
		/// LcmSettings include project sharing and data migration related settings.
		/// </summary>
		public LcmSettings()
		{
			m_sharedXMLBackendCommitLogSize = 100000 * SharedXMLBackendProvider.PageSize;
		}

		internal void Freeze()
		{
			m_frozen = true;
		}

		private void CheckFrozen()
		{
			if (m_frozen)
				throw new InvalidOperationException("The LCM settings cannot be changed.");
		}

		/// <summary>
		/// Gets or sets a value indicating whether data migration is disabled.
		/// </summary>
		public bool DisableDataMigration
		{
			get { return m_disableDataMigration; }
			set
			{
				CheckFrozen();
				m_disableDataMigration = value;
			}
		}

		/// <summary>
		/// Gets or sets the size of the shared XML backend commit log.
		/// </summary>
		public int SharedXMLBackendCommitLogSize
		{
			get { return m_sharedXMLBackendCommitLogSize; }
			set
			{
				CheckFrozen();
				m_sharedXMLBackendCommitLogSize = value;
			}
		}

		/// <summary/>
		public static bool IsProjectSharingEnabled(string projectFolder)
		{
			ISettingsStore settingStore =
				new FileSettingsStore(LexiconSettingsFileHelper.GetProjectLexiconSettingsPath(projectFolder));
			var settingsMapper = new ProjectLexiconSettingsDataMapper(settingStore);
			var settings = new ProjectLexiconSettings();
			settingsMapper.Read(settings);
			return settings.ProjectSharing;
		}
	}
}
