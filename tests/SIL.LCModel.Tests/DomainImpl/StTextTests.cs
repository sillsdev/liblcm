// Copyright (c) 2003-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Reflection;
using NUnit.Framework;
using SIL.LCModel.Core.Text;

namespace SIL.LCModel.DomainImpl
{
	#region StTextTests class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Tests the StText class
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class StTextTests: MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		private IStText m_stText;
		private IText m_text;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Create test data for tests.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected override void CreateTestData()
		{
			m_text = AddInterlinearTextToLangProj("My Interlinear Text");
			AddParaToInterlinearTextContents(m_text, "Here is a sentence I can chart.");
			m_stText = m_text.ContentsOA;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests the basic operation of deleting a text by creating an empty chart and
		/// deleting it.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void DeleteText_emptyChart()
		{
			IDsChart chart = AddChartToLangProj("My Discourse Chart", m_stText);

			// SUT
			Cache.DomainDataByFlid.DeleteObj(m_text.Hvo);

			Assert.AreEqual((int)SpecialHVOValues.kHvoObjectDeleted, chart.Hvo, "The chart should be deleted.");
			Assert.AreEqual((int)SpecialHVOValues.kHvoObjectDeleted, m_stText.Hvo, "The contained StText should be deleted.");
			Assert.AreEqual((int)SpecialHVOValues.kHvoObjectDeleted, m_text.Hvo, "The containing Text should be deleted.");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that attempting to delete a StText directly without deleting its owner fails
		/// unless the StText is in a collection (which I don't think it ever is in our model,
		/// except in the case of footnotes, which are a subclass of StText).
		/// deleting it.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void DeleteStTextWithoutDeletingOwner()
		{
			Assert.That(() => Cache.DomainDataByFlid.DeleteObj(m_stText.Hvo),
				Throws.TypeOf<InvalidOperationException>()
					.Or.TypeOf<TargetInvocationException>().With.InnerException.TypeOf<InvalidOperationException>());
		}

		#region Helper methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds an empty chart on the specified text.
		/// </summary>
		/// <param name="name">Chart name.</param>
		/// <param name="stText">Chart is BasedOn this text.</param>
		/// ------------------------------------------------------------------------------------
		private IDsConstChart AddChartToLangProj(string name, IStText stText)
		{
			ILcmServiceLocator servloc = Cache.ServiceLocator;
			IDsConstChart chart = servloc.GetInstance<IDsConstChartFactory>().Create();
			if (Cache.LangProject.DiscourseDataOA == null)
				Cache.LangProject.DiscourseDataOA = servloc.GetInstance<IDsDiscourseDataFactory>().Create();

			Cache.LangProject.DiscourseDataOA.ChartsOC.Add(chart);

			// Setup the new chart
			chart.Name.AnalysisDefaultWritingSystem = TsStringUtils.MakeString(name, Cache.DefaultAnalWs);
			chart.BasedOnRA = stText;

			return chart; // This chart has no template or rows, so far!!
		}
		#endregion
	}
	#endregion
}
