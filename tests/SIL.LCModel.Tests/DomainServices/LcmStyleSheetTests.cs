// Copyright (c) 2007-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Text;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Scripture;
using SIL.LCModel.Core.Text;

namespace SIL.LCModel.DomainServices
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Testing subclass of FwStyleSheet, that can access the protected method "ComputeDerivedStyles()"
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	internal class DummyLcmStyleSheet : LcmStyleSheet
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Set a font face name for a style.
		/// </summary>
		/// <param name="styleName"></param>
		/// <param name="fontName"></param>
		/// ------------------------------------------------------------------------------------
		internal void SetStyleFont(string styleName, string fontName)
		{
			var style = FindStyle(styleName);
			var ttpBldr = style.Rules.GetBldr();
			ttpBldr.SetStrPropValue((int)FwTextPropType.ktptFontFamily, fontName);
			style.Rules = ttpBldr.GetTextProps();
			ComputeDerivedStyles();
		}

		internal void TestLoadStyles()
		{
			LoadStyles();
		}
	}

	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Tests for the FwStyleSheet class.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class LcmStyleSheetTests : ScrInMemoryLcmTestBase
	{
		private DummyLcmStyleSheet m_styleSheet;

		/// <summary>
		/// Clear out test data.
		/// </summary>
		public override void FixtureTeardown()
		{
			m_styleSheet = null;

			base.FixtureTeardown();
		}

		/// -----------------------------------------------------------------------------------
		/// <summary>
		/// </summary>
		/// -----------------------------------------------------------------------------------
		public override void TestSetup()
		{
			base.TestSetup();

			m_styleSheet = new DummyLcmStyleSheet();
			m_styleSheet.Init(Cache, Cache.LangProject.Hvo, LangProjectTags.kflidStyles);
		}

		/// <summary/>
		[Test]
		public void LoadStyles_LoadedStylesHaveBasedOnMemberSet()
		{
			var parentStyle = GenerateEmptyParagraphStyle("parent", null);
			GenerateEmptyParagraphStyle("child", parentStyle);
			m_styleSheet.TestLoadStyles();
			var loadedChildStyle = m_styleSheet.Styles["child"];
			Assert.That(loadedChildStyle.m_basedOnStyleName, Is.EqualTo("parent"), "Based on name set");
			Assert.That(loadedChildStyle.m_basedOnStyle, Is.Not.Null, "Based on member not set after styles were loaded");
			Assert.That(loadedChildStyle.m_basedOnStyle.Name, Is.EqualTo("parent"), "Based on member not set to the parent style");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Verify that adding a style and deleting a style work.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void AddDeleteStyle()
		{
			var tsPropsBldr = TsStringUtils.MakePropsBldr();
			var ttpFormattingProps = tsPropsBldr.GetTextProps(); // default properties
			var nStylesOrig = m_styleSheet.CStyles;

			// get an hvo for the new style
			var hvoStyle = m_styleSheet.MakeNewStyle();
			var style = Cache.ServiceLocator.GetInstance<IStStyleRepository>().GetObject(hvoStyle);

			// PutStyle() adds the style to the stylesheet
			m_styleSheet.PutStyle("MyNewStyle", "bla", hvoStyle, 0,
				hvoStyle, 0, false, false, ttpFormattingProps);

			Assert.AreEqual(nStylesOrig + 1, m_styleSheet.CStyles);
			Assert.AreEqual(ttpFormattingProps, m_styleSheet.GetStyleRgch(0, "MyNewStyle"),
				"Should get correct format props for the style added");

			// Make style be based on section head and check context
			var baseOnStyle = Cache.LangProject.FindStyle(ScrStyleNames.SectionHead);
			m_styleSheet.PutStyle("MyNewStyle", "bla", hvoStyle, baseOnStyle.Hvo,
				hvoStyle, 0, false, false, ttpFormattingProps);
			Assert.AreEqual(baseOnStyle.Context, style.Context);

			// Now delete the new style
			m_styleSheet.Delete(hvoStyle);

			// Verfiy the deletion
			Assert.AreEqual(nStylesOrig, m_styleSheet.CStyles);
			Assert.IsNull(m_styleSheet.GetStyleRgch(0, "MyNewStyle"),
				"Should get null because style is not there");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure attempting to delete a built-in style throws an exception
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void DeleteBuiltInStyle()
		{
			// get the hvo of the Verse Number style
			var hvoStyle = -1;
			for (var i = 0; i < m_styleSheet.CStyles; i++)
			{
				if (m_styleSheet.get_NthStyleName(i) == "Verse Number")
					hvoStyle = m_styleSheet.get_NthStyle(i);
			}
			Assert.IsTrue(hvoStyle != -1, "Style 'Verse Number' should exist in DB");

			// attempting to delete this built-in style should throw an exception
			Assert.That(() => m_styleSheet.Delete(hvoStyle), Throws.TypeOf<ArgumentException>());
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Test that the StyleCollection class works correct with composed and decomposed
		/// form of strings (TE-6090)
		/// </summary>
		///--------------------------------------------------------------------------------------
		[Test]
		public void StyleCollectionWorksWithUpperAscii()
		{
			const string styleName = "\u00e1bc";
			var style = Cache.ServiceLocator.GetInstance<IStStyleFactory>().Create();
			Cache.LanguageProject.StylesOC.Add(style);
			style.Name = styleName;

			var styleCollection = new LcmStyleSheet.StyleInfoCollection {new BaseStyleInfo(style)};

			Assert.IsTrue(styleCollection.Contains(styleName.Normalize(NormalizationForm.FormC)));
			Assert.IsTrue(styleCollection.Contains(styleName.Normalize(NormalizationForm.FormD)));
			Assert.IsTrue(styleCollection.Contains(styleName.Normalize(NormalizationForm.FormKC)));
			Assert.IsTrue(styleCollection.Contains(styleName.Normalize(NormalizationForm.FormKD)));
		}

		private IStStyle GenerateEmptyParagraphStyle(string name, IStStyle basedOnStyle)
		{
			var styleFactory = Cache.ServiceLocator.GetInstance<IStStyleFactory>();
			var newStyle = styleFactory.Create();
			Cache.LangProject.StylesOC.Add(newStyle);
			newStyle.Name = name;
			newStyle.BasedOnRA = basedOnStyle;
			return newStyle;
		}
	}
}
