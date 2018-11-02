// Copyright (c) 2015-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SIL.LCModel.DomainServices.DataMigration
{
	/// <summary>
	/// Basic data migration tests
	/// </summary>
	[TestFixture]
	public sealed class DataMigrationBasicTests : DataMigrationTestsBase
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure a null DTO repository throws.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void NullRepositoryTest()
		{
			Assert.That(() => m_dataMigrationManager.PerformMigration(null, Int32.MaxValue, null),
				Throws.TypeOf<DataMigrationException>());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure an attempt to do a downgrade migration throws.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void DowngradeMigrationTest()
		{
			var mockMDC = new MockMDCForDataMigration();
			IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(7100000,
				new HashSet<DomainObjectDTO>(), mockMDC, null, TestDirectoryFinder.LcmDirectories);
			Assert.That(() => m_dataMigrationManager.PerformMigration(dtoRepos, 7000000, null),
				Throws.TypeOf<DataMigrationException>());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure a too low starting version number throws.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void StartingVersionNumberTooLowTest()
		{
			Assert.That(() => m_dataMigrationManager.NeedsRealMigration(1, 7000001),
				Throws.TypeOf<DataMigrationException>());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure a too high ending version number throws.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void EndingVersionNumberTooHighTest()
		{
			Assert.That(() => m_dataMigrationManager.NeedsRealMigration(7000000, Int32.MaxValue),
				Throws.TypeOf<DataMigrationException>());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure a starting number that is higher than the ending version number throws.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void StartingVersionNumberHigherThanEndingVersionNumberTest()
		{
			Assert.That(() => m_dataMigrationManager.NeedsRealMigration(7000002, 7000001),
				Throws.TypeOf<DataMigrationException>());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure a real migration says it needs a migration.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void NeedsARealMigrationTest()
		{
			Assert.IsTrue(m_dataMigrationManager.NeedsRealMigration(7000000, 7000001),
				"7000000->7000001 does require a real migration.");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure migration to the same ending number says it can skip a migration.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void MigrationToSameVersionDoesNotNeedARealMigrationTest()
		{
			Assert.IsFalse(m_dataMigrationManager.NeedsRealMigration(7000001, 7000001),
				"7000001->7000001 shouldn't require a real migration.");
		}

		// Use this test, as soon as we add a do-nothing migration to the manager.
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure a do-nothing migration says it can skip a migration.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void DoesNotNeedARealMigrationTest()
		{
			Assert.IsFalse(m_dataMigrationManager.NeedsRealMigration(7000003, 7000004),
				"7000003->7000004 shouldn't require a real migration.");
		}
	}
}