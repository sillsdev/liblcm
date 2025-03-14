// Copyright (c) 2019-2022 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SIL.ObjectModel;
using SIL.TestUtilities;
using SIL.WritingSystems;

namespace SIL.LCModel.Core.WritingSystems
{
	[TestFixture]
	public class CoreWritingSystemDefinitionTests : CloneableTests<CoreWritingSystemDefinition, WritingSystemDefinition>
	{
		[OneTimeSetUp]
		public void OneTimeSetup()
		{
			FieldComparer = ValueEquatableComparer.Instance;
		}

		[TestCase("en", "English")]
		[TestCase("en-Arab", "English \\(Arabic\\)")]
		[TestCase("en-Latn", "English \\(Latin\\)")]
		[TestCase("en-Latn-US", "English \\(Latin, United States\\)")]
		[TestCase("en-Latn-US-x-special", "English \\(Latin, United States, special\\)")]
		[TestCase("en-fonipa", "English \\(International Phonetic Alphabet\\)")]
		[TestCase("en-Latn-fonipa", "English \\(Latin, International Phonetic Alphabet\\)")]
		[TestCase("en-Zxxx-x-audio", "English \\(Audio\\)")]
		public void CoreWritingDefinition_DisplayLabel(string langTag, string expectedLabel)
		{
			var ws = new CoreWritingSystemDefinition(langTag);
			Assert.That(ws.DisplayLabel, Does.Match(expectedLabel));
		}

		[Test]
		public void CopyCopiesAllNeededMembers()
		{
			CoreWritingSystemDefinition copyable = CreateNewCloneable();
			MethodInfo getAllFields = null;
			var type = GetType().BaseType;
			while(getAllFields == null)
			{
				getAllFields = type.GetMethod("GetAllFields", BindingFlags.NonPublic | BindingFlags.Static);
				type = type.BaseType;
			}
			Assert.That(getAllFields, Is.Not.Null, "Has the signature for GetAllFields changed?");
			var fieldInfos = (IEnumerable<FieldInfo>) getAllFields.Invoke(this, new object[] {copyable});

			foreach (var fieldInfo in fieldInfos)
			{
				var fieldName = fieldInfo.Name;
				var fieldType = fieldInfo.FieldType;
				if (fieldInfo.Name.Contains("<"))
				{
					var splitResult = fieldInfo.Name.Split('<', '>');
					fieldName = splitResult[1];
				}
				if (ExceptionList.Contains($"|{fieldName}|"))
				{
					continue;
				}
				object defaultValue = null;
				try
				{
					defaultValue = DefaultValuesForTypes.Single(dv => dv.TypeOfValueToSet == fieldType).ValueToSet;
				}
				catch (InvalidOperationException)
				{
					Assert.Fail("Unhandled field type - please update the test to handle type \"{0}\". The field that uses this type is \"{1}\".", fieldType.Name, fieldName);
				}

				fieldInfo.SetValue(copyable, defaultValue);

				var theCopy = CreateNewCloneable();
				theCopy.Copy(copyable);
				// strings are special in .NET, so we won't worry about checking them here.
				if (fieldType != typeof(string))
				{
					Assert.AreNotSame(fieldInfo.GetValue(copyable), fieldInfo.GetValue(theCopy),
						"The field \"{0}\" refers to the same object; it was not copied.", fieldName);
				}
				Assert.That(fieldInfo.GetValue(theCopy), Is.EqualTo(defaultValue).Using(FieldComparer), "Field \"{0}\" not copied on Copy()", fieldName);
			}
		}

		#region CloneableTests
		public override CoreWritingSystemDefinition CreateNewCloneable()
		{
			return new CoreWritingSystemDefinition();
		}

		protected override bool Equals(CoreWritingSystemDefinition x, CoreWritingSystemDefinition y)
		{
			return x?.ValueEquals(y) ?? y == null;
		}

		public override string ExceptionList =>
			// This list must be longer than ExceptionsList in Palaso's WritingSystemDefinitionCloneableTests
			// because we don't have access to set backing fields to bypass side effects of setting
			"|Handle|Id|IsChanged|MarkedForDeletion|PropertyChanged|PropertyChanging|Template|WritingSystemFactory" +
			"|_characterSets|_collations|_defaultCollation|_defaultFont|_fonts|_ignoreVariantChanges|_knownKeyboards|_language" +
			"|_languageTag|_localKeyboard|_matchedPairs|_punctuationPatterns|_quotationMarks|_region|_script|_spellCheckDictionaries|";

		protected override List<ValuesToSet> DefaultValuesForTypes => new List<ValuesToSet>
		{
			new ValuesToSet(3.14f, 2.72f),
			new ValuesToSet(true, false),
			new ValuesToSet("to be", "!(to be)"),
			new ValuesToSet(DateTime.Now, DateTime.MinValue),
			new ValuesToSet(QuotationParagraphContinueType.All, QuotationParagraphContinueType.None),
			new ValuesToSet(NumberingSystemDefinition.CreateCustomSystem("9876543210"), NumberingSystemDefinition.Default),
			new ValuesToSet(
				new BulkObservableList<VariantSubtag>(new VariantSubtag[] {"1901", "bisque"}),
				new BulkObservableList<VariantSubtag>(new VariantSubtag[] {"foo", "bar"})),
			new ValuesToSet(new CharacterSetDefinition("test"), new CharacterSetDefinition("type")),
			new ValuesToSet(new HashSet<int> {1}, new HashSet<int> {2, 3})
		};

		#endregion CloneableTests
	}
}
