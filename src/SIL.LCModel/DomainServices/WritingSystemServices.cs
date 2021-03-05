// Copyright (c) 2014-2021 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using SIL.LCModel.Application.ApplicationServices;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainImpl;
using SIL.LCModel.Infrastructure;
using SIL.LCModel.Utils;
using SIL.WritingSystems;
using SIL.WritingSystems.Migration;
using SIL.Xml;

namespace SIL.LCModel.DomainServices
{
	/// <summary>
	///
	/// </summary>
	public static class WritingSystemServices
	{
		// These are magic writing system numbers that are not legal writing systems, but are used to signal
		// the application to get the appropriate writing system from the application. For example,
		// a language project has a list of one or more analysis encodings. kwsAnal would
		// tell the program to use the first writing system in this list.

		/// <summary>(-1) The first analysis writing system.</summary>
		public const int kwsAnal = -1;
		/// <summary>(-2) The first vernacular writing system.</summary>
		public const int kwsVern = -2;
		/// <summary>(-3) All analysis writing system.</summary>
		public const int kwsAnals = -3;
		/// <summary>(-4) All vernacular writing system.</summary>
		public const int kwsVerns = -4;
		/// <summary>(-5) All analysis then All vernacular writing system.</summary>
		public const int kwsAnalVerns = -5;
		/// <summary>(-6) All vernacular then All analysis writing system.</summary>
		public const int kwsVernAnals = -6;
		/// <summary>(-7) The first available analysis ws with data in the current sequence.</summary>
		public const int kwsFirstAnal = -7;
		/// <summary>(-8) The first available vernacular ws in the current sequence.</summary>
		public const int kwsFirstVern = -8;
		/// <summary>(-9) The first available analysis ws with data in the current sequence,
		/// or the first available vernacular ws in that sequence.</summary>
		public const int kwsFirstAnalOrVern = -9;
		/// <summary>(-10) The first available vernacular ws with data in the current sequence,
		/// or the first available analysis ws in that sequence.</summary>
		public const int kwsFirstVernOrAnal = -10;
		/// <summary>(-11) The first pronunciation writing system.</summary>
		public const int kwsPronunciation = -11;
		/// <summary>(-12) The first pronunciation writing system with data.</summary>
		public const int kwsFirstPronunciation = -12;
		/// <summary>(-13) All pronunciation writing systems.</summary>
		public const int kwsPronunciations = -13;
		/// <summary>(-14) The primary writing system for the current reversal index.</summary>
		public const int kwsReversalIndex = -14;
		/// <summary>(-15) The full list of writing systems for the current reversal index.</summary>
		public const int kwsAllReversalIndex = -15;
		/// <summary>(-16) The ws of the relevant text at an offset in its paragraph</summary>
		public const int kwsVernInParagraph = -16;
		/// <summary>(-17) The first available vern ws with data in the current sequence or else a ws named in the database. </summary>
		public const int kwsFirstVernOrNamed = -17;
		/// <summary> One beyond the last magic value.</summary>
		public const int kwsLim = -18;

		/// <summary>
		/// Somebody has to tell us the current reversal writing system, or we don't have a clue.
		/// </summary>
		/// <value>The current reversal writing system identifier.</value>
		public static int CurrentReversalWsId { get; set; }

		/// <summary>
		/// Gets text properties used to display the abbreviation of a writing system in a multi-string editor.
		/// Currently this is mainly used in detail views in Harvest, where we don't want to use the blue color
		/// that is the default in DN and perhaps elsewhere. The ControlDarkDark color is chosen to match
		/// the color of the labels used to identify slices (see Slice.DrawLabel).
		///
		/// Review team (JohnT): this is probably not the best place for this, but where else??
		/// </summary>
		public static ITsTextProps AbbreviationTextProperties
		{
			get
			{
				var tpb = TsStringUtils.MakePropsBldr();
				tpb.SetIntPropValues((int)FwTextPropType.ktptForeColor, (int)FwTextPropVar.ktpvDefault,
					(int)ColorUtil.ConvertColorToBGR(Color.FromKnownColor(KnownColor.ControlDarkDark)));
				//				// This is the formula (red + (blue * 256 + green) * 256) for a FW RGB color,
				//				// applied to the standard FW color "light blue". This is the default defn of the
				//				// "Language Code" character style in DN. We could just use this style, except
				//				// I'm not sure Oyster is yet using style sheets.
				//				tpb.SetIntPropValues((int)FwTextPropType.ktptForeColor, (int)FwTextPropVar.ktpvDefault,
				//					47 + (255 * 256 + 96) * 256);
				// And this makes it 8 point.
				tpb.SetIntPropValues((int)FwTextPropType.ktptFontSize, (int)FwTextPropVar.ktpvMilliPoint, 8000);
				tpb.SetStrPropValue((int)FwTextPropType.ktptFontFamily, MiscUtils.StandardSansSerif);//JH added to get sans serif
				tpb.SetIntPropValues((int)FwTextPropType.ktptBold,	//JH added so it's not bold on citation form
					(int)FwTextPropVar.ktpvEnum,
					(int)FwTextToggleVal.kttvOff);

				tpb.SetIntPropValues((int)FwTextPropType.ktptEditable,
					(int)FwTextPropVar.ktpvEnum, (int)TptEditable.ktptNotEditable);
				return tpb.GetTextProps();
			}
		}

		private static readonly Dictionary<int, string> MagicWsIdToWsName = new Dictionary<int, string>
		{
			{kwsAnal, "analysis"},
			{kwsVern, "vernacular"},
			{kwsVerns, "all vernacular"},
			{kwsAnals, "all analysis"},
			{kwsAnalVerns, "analysis vernacular"},
			{kwsVernAnals, "vernacular analysis"},
			{kwsFirstAnal, "best analysis"},
			{kwsFirstVern, "best vernacular"},
			{kwsFirstAnalOrVern, "best analorvern"},
			{kwsFirstVernOrAnal, "best vernoranal"},
			{kwsPronunciation, "pronunciation"},
			{kwsFirstPronunciation, "best pronunciation"},
			{kwsPronunciations, "all pronunciation"},
			{kwsReversalIndex, "reversal"},
			{kwsAllReversalIndex, "all reversal"},
			{kwsVernInParagraph, "vern in para"},
			{kwsFirstVernOrNamed, "best vernornamed"}
		};
		private static readonly Dictionary<string, int> MagicWsNameToWsId;

		static WritingSystemServices()
		{
			MagicWsNameToWsId = MagicWsIdToWsName.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
		}

		/// <summary>
		/// Returns the simple equivalent of a smart magic writing system.  If a simple magic ws is used as the argument, it should
		/// be returned unmodified.  For example, given kwsAnals (all analysis), this method will return kwsAnal (analysis).
		/// The WS returned should be safe to use in bulk edit. This means it should not be so smart as to pick one of several
		/// writing systems based on what is not empty. For example, kwsFirstAnal is simplified to kwsAnal, and so is kwsFirstAnalOrVern.
		/// </summary>
		/// <param name="wsMagic">The smart magic ws to turn into a simple equivalent</param>
		/// <returns></returns>
		public static int SmartMagicWsToSimpleMagicWs(int wsMagic)
		{
			switch (wsMagic)
			{
				case kwsAnal:
				case kwsAnals:
				case kwsFirstAnal:
				case kwsAnalVerns:
				case kwsFirstAnalOrVern:
					return kwsAnal;
				case kwsVern:
				case kwsVerns:
				case kwsFirstVern:
				case kwsVernAnals:
				case kwsFirstVernOrAnal:
					return kwsVern;
				case kwsPronunciations:
				case kwsPronunciation:
					return kwsPronunciation;
				case kwsAllReversalIndex:
				case kwsReversalIndex:
					return kwsReversalIndex;
				case kwsVernInParagraph:
					return kwsVernInParagraph;
				default:
					Debug.Assert(false, "A magic writing system ID (" + wsMagic + ") was encountered that this method does not understand.");
					return wsMagic;
			}
		}

		/// <summary>
		/// Returns the singular equivalent of a plural magic writing system.  If a singular magic ws is used as the argument, it should
		/// be returned unmodified.  For example, given kwsAnals (all analysis), this method will return kwsAnal (analysis).
		/// </summary>
		/// <param name="wsMagic">The plural magic ws to turn into a singular equivalent</param>
		/// <returns></returns>
		public static int PluralMagicWsToSingularMagicWs(int wsMagic)
		{
			switch (wsMagic)
			{
				case kwsAnal:
				case kwsAnals:
					return kwsAnal;
				case kwsVern:
				case kwsVerns:
					return kwsVern;
				case kwsPronunciations:
				case kwsPronunciation:
					return kwsPronunciation;
				case kwsAllReversalIndex:
				case kwsReversalIndex:
					return kwsReversalIndex;
				case kwsFirstAnal:
					return kwsFirstAnal;
				case kwsFirstVern:
					return kwsFirstVern;
				case kwsVernAnals:
					return kwsVernAnals;	// not singular, but handled okay elsewhere.
				case kwsFirstVernOrAnal:
					return kwsFirstVernOrAnal;
				case kwsAnalVerns:
					return kwsAnalVerns;	// not singular, but handled okay elsewhere.
				case kwsFirstAnalOrVern:
					return kwsFirstAnalOrVern;
				case kwsVernInParagraph:
					return kwsVernInParagraph;
				default:
					throw new InvalidOperationException("A magic writing system ID (" + wsMagic + ") was encountered that this method does not understand.");
			}
		}

		/// <summary>
		/// Get the writing system from the XML attributes ws, smartws or wsid, or use the supplied
		/// default if neither attribute exists, or no meaningful value exists, even if the attribute does exist.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="frag">The frag.</param>
		/// <param name="currentWS">The current WS.</param>
		/// <param name="wsDefault">The ws default.</param>
		/// <returns></returns>
		public static CoreWritingSystemDefinition GetWritingSystem(LcmCache cache, XmlNode frag, CoreWritingSystemDefinition currentWS, int wsDefault)
		{
			return GetWritingSystem(cache, frag, currentWS, 0, 0, wsDefault);
		}
		/// <summary>
		/// Get the writing system for the specified arguments, using the supplied SDA to interpret hvo and flid.
		/// </summary>
		public static CoreWritingSystemDefinition GetWritingSystem(LcmCache cache, ISilDataAccess sda, XmlNode frag, CoreWritingSystemDefinition currentWS,
			int hvo, int flid, int wsDefault)
		{
			if (wsDefault == 0)
				wsDefault = cache.DefaultUserWs;
			var wsid = wsDefault;
			var xa = frag.Attributes["ws"];
			if (xa != null)
			{
				// If it has a parameter tag, strip it off.
				string wsSpec = StringServices.GetWsSpecWithoutPrefix(xa.Value);
				int wsMagicOut; // required output arg not used.
				// LT-16301 If the user specifies a ws (like 'fr') the Get for a magic wsid crashes
				// before we get to interpret the wsSpec, so convert the default to an actual ws first.
				if (wsid < 0)
					wsid = ActualWs(cache, sda, wsid, hvo, flid);
				wsid = InterpretWsLabel(cache, sda, wsSpec, cache.ServiceLocator.WritingSystemManager.Get(wsid),
					hvo,
					flid,
					currentWS, out wsMagicOut);
			}
			// if ws is still a magic id, then convert it to something real.
			if (wsid < 0)
			{
				wsid = ActualWs(cache, sda, wsid, hvo, flid);
			}
			return cache.ServiceLocator.WritingSystemManager.Get(wsid);
		}

		/// <summary>
		/// Get the writing system for the specified arguments, using the default cache to interpret hvo and flid.
		/// Consider using the overload that takes an SDA if hvo or flid might be decorator objects/properties.
		/// </summary>
		public static CoreWritingSystemDefinition GetWritingSystem(LcmCache cache, XmlNode frag, CoreWritingSystemDefinition currentWS, int hvo, int flid, int wsDefault)
		{
			return GetWritingSystem(cache, cache.DomainDataByFlid, frag, currentWS, hvo, flid, wsDefault);
		}

		/// <summary>
		/// Return the possible writing systems that we might want to preload for the given fragment.
		/// Note that currently this is for optimization and preloading; it is not (yet) guaranteed to
		/// return EVERY writing system that might be displayed by the given fragment.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="frag">The frag.</param>
		/// <returns></returns>
		public static HashSet<int> GetWritingSystems(LcmCache cache, XmlNode frag)
		{
			var xa = frag.Attributes["ws"];
			if (xa == null)
				return new HashSet<int>();
			// If it has a parameter tag, strip it off.
			string wsSpec = StringServices.GetWsSpecWithoutPrefix(xa.Value);
			var result = new HashSet<int>();
			switch (wsSpec)
			{
				case "vernacular":
					result.Add(cache.LanguageProject.DefaultVernacularWritingSystem.Handle);
					break;
				case "analysis":
					result.Add(cache.LanguageProject.DefaultAnalysisWritingSystem.Handle);
					break;
				case "pronunciation":
				case "all pronunciation":
					result.Add(cache.LanguageProject.DefaultPronunciationWritingSystem.Handle);
					break;
				case "current":
					break;
				case "reversal":
					foreach (var ri in cache.LanguageProject.LexDbOA.CurrentReversalIndices)
						result.Add(cache.ServiceLocator.WritingSystemManager.GetWsFromStr(ri.WritingSystem));
					break;
				case "best analorvern":
				case "best vernoranal":
					result.UnionWith(cache.LanguageProject.CurrentAnalysisWritingSystems.Handles());
					result.UnionWith(cache.LanguageProject.CurrentVernacularWritingSystems.Handles());
					break;
				case "analysis vernacular":
				case "av":
				case "vernacular analysis":
				case "va":
					result.Add(cache.DefaultVernWs);
					result.Add(cache.DefaultAnalWs);
					break;
				case "user":
					result.Add(cache.DefaultUserWs);
					break;
				case "best analysis":
				case "all analysis":
					result.UnionWith(cache.LanguageProject.CurrentAnalysisWritingSystems.Handles());
					break;
				case "best vernacular":
				case "all vernacular":
					result.UnionWith(cache.LanguageProject.CurrentVernacularWritingSystems.Handles());
					break;
				default:
					// See if we can get anywhere by treating it as an ICU locale.
					// Note that it is important to do this in a way that won't create a new writing system for
					// an invalid locale name, for example, if 'all analysis' is mistakenly passed to this routine.
					// Note however that the behavior of recognizing an ICU locale name for an existing writing system
					// definitely IS needed, e.g., when the user configures a Browse view to show an explicit writing system.
					var wsT = cache.WritingSystemFactory.GetWsFromStr(wsSpec);
					if (wsT != 0)
						result.Add(wsT);
					break;
			}
			return result;
		}

		/// <summary>
		/// Get a Set of zero or more actual writing system ID from the given xml fragment.
		/// The contents of the mandatory 'ws' attribute may be a magic ws specification,
		/// or one of several pseudo magic writing system spcifications.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="frag">The frag.</param>
		/// <param name="currentWS">The current WS.</param>
		/// <param name="hvo">object to use in determining 'best' names</param>
		/// <param name="flid">flid to use in determining 'best' names</param>
		/// <returns></returns>
		public static HashSet<int> GetAllWritingSystems(LcmCache cache, XmlNode frag, CoreWritingSystemDefinition currentWS, int hvo, int flid)
		{
			var sWs = XmlUtils.GetOptionalAttributeValue(frag, "ws");
			return GetAllWritingSystems(cache, sWs, currentWS, hvo, flid);
		}

		/// <summary>
		/// Get a Set of zero or more actual writing system IDs for the given ws identifier.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="sWs">One of our magic strings that signifies one or more writing systems</param>
		/// <param name="currentWS">The current WS.</param>
		/// <param name="hvo">object to use in determining 'best' names</param>
		/// <param name="flid">flid to use in determining 'best' names</param>
		/// <returns></returns>
		public static HashSet<int> GetAllWritingSystems(LcmCache cache, string sWs, CoreWritingSystemDefinition currentWS, int hvo, int flid)
		{
			var allWsIds = new HashSet<int>();
			if (sWs != null)
			{
				IWritingSystemContainer wsContainer = cache.ServiceLocator.WritingSystems;
				switch (sWs)
				{
					case "all analysis":
						allWsIds.UnionWith(wsContainer.CurrentAnalysisWritingSystems.Handles());
						break;
					case "all vernacular":
						allWsIds.UnionWith(wsContainer.CurrentVernacularWritingSystems.Handles());
						break;
					case "analysis vernacular":
						allWsIds.UnionWith(wsContainer.CurrentAnalysisWritingSystems.Handles());
						allWsIds.UnionWith(wsContainer.CurrentVernacularWritingSystems.Handles());
						break;
					case "vernacular analysis":
						allWsIds.UnionWith(wsContainer.CurrentVernacularWritingSystems.Handles());
						allWsIds.UnionWith(wsContainer.CurrentAnalysisWritingSystems.Handles());
						break;
					case "all pronunciation":
						allWsIds.UnionWith(wsContainer.CurrentPronunciationWritingSystems.Handles());
						//if (allWsIds.Count == 0)
						//	allWsIds.Add(cache.LangProject.DefaultPronunciationWritingSystem);
						break;
					default:
						sWs = StringServices.GetWsSpecWithoutPrefix(sWs);
						var rgsWs = sWs.Split(new[] { ',' });
						for (var i = 0; i < rgsWs.Length; ++i)
						{
							var ws = InterpretWsLabel(cache, rgsWs[i], null, hvo, flid, currentWS);
							if (ws != 0)
								allWsIds.Add(ws);
						}
						break;
				}
			}

			return allWsIds;
		}

		/// <summary>
		/// Try to get an actual writing system id from some ws string specification.
		/// If it does not recognize the ws spec string, it returns 0.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="wsSpec">The ws spec.</param>
		/// <param name="wsDefault">The ws default.</param>
		/// <param name="hvoObj">The hvo obj.</param>
		/// <param name="flid">The flid.</param>
		/// <param name="currentWS">The current WS.</param>
		/// <returns>
		/// An actual writing system id, or 0, if it can't recognize the wsSpec parameter.
		/// </returns>
		public static int InterpretWsLabel(LcmCache cache, string wsSpec, CoreWritingSystemDefinition wsDefault,
										   int hvoObj, int flid, CoreWritingSystemDefinition currentWS)
		{
			int wsMagic;
			return InterpretWsLabel(cache, wsSpec, wsDefault, hvoObj, flid, currentWS, out wsMagic);
		}

		/// <summary>
		/// Try to get an actual writing system id from some ws string specification.
		/// If it does not recognize the ws spec string, it returns 0.
		/// Note that the HVO may be a fake object, understood only by the sda passed in.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="sda">May be a decorated SDA that understands hvo/flid.</param>
		/// <param name="wsSpec">The ws spec.</param>
		/// <param name="wsDefault">The ws default.</param>
		/// <param name="hvoObj">The hvo obj.</param>
		/// <param name="flid">The flid.</param>
		/// <param name="currentWS">The current WS.</param>
		/// <param name="wsMagic">returns the equivalent magic ws value</param>
		/// <returns>
		/// An actual writing system id, or 0, if it can't recognize the wsSpec parameter.
		/// </returns>
		public static int InterpretWsLabel(LcmCache cache, ISilDataAccess sda, string wsSpec, CoreWritingSystemDefinition wsDefault,
			int hvoObj, int flid, CoreWritingSystemDefinition currentWS, out int wsMagic)
		{
			wsMagic = GetMagicWsIdFromName(wsSpec);	// note: doesn't cover "va" and "av".
			var defAnalWs = cache.ServiceLocator.WritingSystems.DefaultAnalysisWritingSystem.Handle;
			var defVernWs = cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem.Handle;
			var defUserWs = cache.ServiceLocator.WritingSystemManager.UserWs;
			int actualWS;
			switch (wsSpec)
			{
				case "vernacular":
					actualWS = ActualWs(cache, sda, kwsVern, hvoObj, flid);
					break;
				case "analysis":
					actualWS = ActualWs(cache, sda, kwsAnal, hvoObj, flid);
					break;
				case "best analysis":
					actualWS = ActualWs(cache, sda, kwsFirstAnal, hvoObj, flid);
					if (actualWS == 0)
						actualWS = wsDefault == null ? defAnalWs : wsDefault.Handle;
					break;
				case "best vernacular":
					actualWS = ActualWs(cache, sda, kwsFirstVern, hvoObj, flid);
					if (actualWS == 0)
						actualWS = wsDefault == null ? defVernWs : wsDefault.Handle;
					break;
				case "best analorvern":
					actualWS = ActualWs(cache, sda, kwsFirstAnalOrVern, hvoObj, flid);
					if (actualWS == 0)
						actualWS = wsDefault == null ? defAnalWs : wsDefault.Handle;
					break;
				case "best vernoranal":
					actualWS = ActualWs(cache, sda, kwsFirstVernOrAnal, hvoObj, flid);
					if (actualWS == 0)
						actualWS = wsDefault == null ? defVernWs : wsDefault.Handle;
					break;
				case "pronunciation":
				case "all pronunciation":	// fixes LT-6665.
					actualWS = cache.ServiceLocator.WritingSystems.DefaultPronunciationWritingSystem.Handle;
					break;
				case "current":
					if (currentWS != null)
						actualWS = currentWS.Handle;
					else
						actualWS = defUserWs;
					break;
				case "reversal":
					if (CurrentReversalWsId > 0)
					{
						actualWS = CurrentReversalWsId;
					}
					else
					{
						var reversalIndexEntryWritingSystem = GetReversalIndexEntryWritingSystem(cache, hvoObj, wsDefault);
						actualWS = reversalIndexEntryWritingSystem == null ? 0 : reversalIndexEntryWritingSystem.Handle;
					}
					break;
				case "analysis vernacular":
				case "av":
					// Sometimes this is done, e.g., to figure out something about overall behavior of a column,
					// and we don't have a specific HVO. Since we prefer the analysis one, answer it when we don't
					// have a specific HVO.
					if (hvoObj == 0)
						actualWS = defAnalWs;
					else if (sda.get_MultiStringAlt(hvoObj, flid, defAnalWs).Length > 0)
						actualWS = defAnalWs;
					else if (sda.get_MultiStringAlt(hvoObj, flid, defVernWs).Length > 0)
						actualWS = defVernWs;
					else
						actualWS = defAnalWs;
					break;
				case "vernacular analysis":
				case "va":
					if (hvoObj == 0)
						actualWS = defVernWs;
					else if (sda.get_MultiStringAlt(hvoObj, flid, defVernWs).Length > 0)
						actualWS = defVernWs;
					else if (sda.get_MultiStringAlt(hvoObj, flid, defAnalWs).Length > 0)
						actualWS = defAnalWs;
					else
						actualWS = defVernWs;
					break;
				case "user":
					actualWS = defUserWs;
					break;
				default:
					// See if we can get anywhere by treating it as an ICU locale.
					// Note that it is important to do this in a way that won't create a new writing system for
					// an invalid locale name, for example, if 'all analysis' is mistakenly passed to this routine.
					// Note however that the behavior of recognizing an ICU locale name for an existing writing system
					// definitely IS needed, e.g., when the user configures a Browse view to show an explicit writing system.
					var wsT = cache.ServiceLocator.WritingSystemManager.GetWsFromStr(wsSpec);
					if (wsT == 0 && wsDefault != null)
						wsT = wsDefault.Handle;
					if (wsT == 0)
						wsT = cache.DefaultAnalWs; // last desperate default, better than throwing with 0 WS.
					actualWS = wsT;
					break;
			}
			return actualWS;
		}

		/// <summary>
		/// Try to get an actual writing system id from some ws string specification.
		/// If it does not recognize the ws spec string, it returns 0.
		/// Note: if the HVO/flid combination might be a fake, use the overload which takes the SDA that
		/// understands them.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="wsSpec">The ws spec.</param>
		/// <param name="wsDefault">The ws default.</param>
		/// <param name="hvoObj">The hvo obj.</param>
		/// <param name="flid">The flid.</param>
		/// <param name="currentWS">The current WS.</param>
		/// <param name="wsMagic">returns the equivalent magic ws value</param>
		/// <returns>
		/// An actual writing system id, or 0, if it can't recognize the wsSpec parameter.
		/// </returns>
		public static int InterpretWsLabel(LcmCache cache, string wsSpec, CoreWritingSystemDefinition wsDefault,
			int hvoObj, int flid, CoreWritingSystemDefinition currentWS, out int wsMagic)
		{
			return InterpretWsLabel(cache, cache.DomainDataByFlid, wsSpec, wsDefault, hvoObj, flid,
				currentWS, out wsMagic);

		}

		/// <summary>
		/// Convert a writing system (could be magic) to a real writing system.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="magicName">Writing system to convert, which may be a 'magic'</param>
		/// <param name="hvo">Optional hvo that owns the string.</param>
		/// <param name="flid">Optional flid for the owned string.</param>
		/// <returns></returns>
		/// <remarks>
		/// The hvo and flid parameters are only used for the four 'magic' WSes
		/// that try to get data from a preferred list of WSes.
		/// </remarks>
		public static int ActualWs(LcmCache cache, string magicName, int hvo, int flid)
		{
			int retWs = GetMagicWsIdFromName(magicName);
			if (retWs != 0)
				retWs = ActualWs(cache, retWs, hvo, flid);
			return retWs;
		}

		/// <summary>
		/// Convert a writing system (could be magic) to a real writing system.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="ws">Writing system to convert, which may be a 'magic'</param>
		/// <param name="hvo">Optional hvo that owns the string.</param>
		/// <param name="flid">Optional flid for the owned string.</param>
		/// <returns></returns>
		/// <remarks>
		/// The hvo and flid parameters are only used for the four 'magic' WSes
		/// that try to get data from a preferred list of WSes.
		/// </remarks>
		public static int ActualWs(LcmCache cache, int ws, int hvo, int flid)
		{
			int actualWs;
			GetMagicStringAlt(cache, ws, hvo, flid, false, out actualWs);
			return actualWs;
		}

		/// <summary>
		/// Convert a writing system (could be magic) to a real writing system.
		/// The hvo may be a fake object known only to the provided SDA.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="sda">Possibly a decorator which understands the hvo/flid combination.</param>
		/// <param name="ws">Writing system to convert, which may be a 'magic'</param>
		/// <param name="hvo">Optional hvo that owns the string.</param>
		/// <param name="flid">Optional flid for the owned string.</param>
		/// <returns></returns>
		/// <remarks>
		/// The hvo and flid parameters are only used for the four 'magic' WSes
		/// that try to get data from a preferred list of WSes.
		/// </remarks>
		public static int ActualWs(LcmCache cache, ISilDataAccess sda, int ws, int hvo, int flid)
		{
			int actualWs;
			GetMagicStringAlt(cache, sda, ws, hvo, flid, false, out actualWs);
			return actualWs;
		}

		/// <summary>
		/// Get the magic WS id for the given string, or 0, if not a magic WS id.
		/// </summary>
		/// <param name="wsSpec"></param>
		/// <returns></returns>
		public static int GetMagicWsIdFromName(string wsSpec)
		{
			if (wsSpec == null)
				return 0;

			int wsMagic;
			if (MagicWsNameToWsId.TryGetValue(wsSpec, out wsMagic))
				return wsMagic;
			// JohnT: took this out, because ConfigureFieldDlg wants to pass names of specific writing systems
			// and get zero back, as indicated in the method comment.
			//Debug.Assert(wsMagic != 0, "Method encountered a Magic Ws string that it did not understand");
			return 0;
		}

		/// <summary>
		/// Get the writing system for the given ReversalIndexEntry.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="hvoObj">The hvo obj.</param>
		/// <param name="wsDefault">The ws default.</param>
		/// <returns></returns>
		public static CoreWritingSystemDefinition GetReversalIndexEntryWritingSystem(LcmCache cache, int hvoObj, CoreWritingSystemDefinition wsDefault)
		{
			IReversalIndex ri = null;

			if (hvoObj != 0)
			{
				var obj = cache.ServiceLocator.GetObject(hvoObj);
				var clid = obj.ClassID;
				switch (clid)
				{
					case ReversalIndexTags.kClassId:
						ri = obj as IReversalIndex;
						break;
					case ReversalIndexEntryTags.kClassId:
						ri = ((IReversalIndexEntry)obj).ReversalIndex;
						break;
					case PartOfSpeechTags.kClassId:
						// It may be nested, but we need the owner (index) of the list,
						// no matter how high up.
						ri = (IReversalIndex)obj.OwnerOfClass(ReversalIndexTags.kClassId);
						break;
				}
			}

			// This combines the former "default" case from the above switch (no specific ClassId)
			// with the case where hvoObj is zero, which happened in LT-12956 in the Form column
			// of Bulk Edit Reversal Entries:
			if (ri == null)
			{
				var rgriCurrent = cache.LanguageProject.LexDbOA.CurrentReversalIndices;
				if (rgriCurrent.Count > 0)
				{
					ri = rgriCurrent[0];
				}
				else
				{
					if (cache.LanguageProject.LexDbOA.ReversalIndexesOC.Count > 0)
						ri = cache.LanguageProject.LexDbOA.ReversalIndexesOC.ToArray()[0];
				}
			}

			if (ri != null)
				return cache.ServiceLocator.WritingSystemManager.Get(ri.WritingSystem);

			return wsDefault;
		}

		/// <summary>
		/// Extract a string and writing system from an object, flid, and 'magic' writing
		/// system code.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="ws">Writing system to convert, which may be a 'magic'</param>
		/// <param name="hvo">Hvo that owns the string.</param>
		/// <param name="flid">Flid for the owned string.</param>
		/// <returns></returns>
		public static ITsString GetMagicStringAlt(LcmCache cache, int ws, int hvo, int flid)
		{
			int actualWs;
			return GetMagicStringAlt(cache, ws, hvo, flid, true, out actualWs);
		}

		/// <summary>
		/// Extract a string and writing system from an object, flid, and 'magic' writing system code.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="ws">Writing system to convert, which may be a 'magic'</param>
		/// <param name="hvo">Hvo that owns the string.</param>
		/// <param name="flid">Flid for the owned string.</param>
		/// <param name="fWantString">false if we don't really care about the string. This allows clients to avoid retrieving it at all.</param>
		/// <param name="retWs">Retrieves the actual ws that the returned string belongs to.</param>
		public static ITsString GetMagicStringAlt(LcmCache cache, int ws, int hvo, int flid, bool fWantString, out int retWs)
		{
			return GetMagicStringAlt(cache, cache.DomainDataByFlid, ws, hvo, flid, fWantString, out retWs);
		}

		/// <summary>
		/// Get a magic writing system alternative, for an object which may possibly be a fake known only to the
		/// sda passed in.
		/// </summary>
		public static ITsString GetMagicStringAlt(LcmCache cache, ISilDataAccess sda, int ws, int hvo, int flid, bool fWantString, out int retWs)
		{
			Debug.Assert(ws != 0);
			retWs = 0; // Start on the pessimistic side.
			ITsString retTss = null;
			int defaultUserWs = cache.DefaultUserWs;
			int defaultAnalWs = cache.DefaultAnalWs;
			int defaultVernWs = cache.DefaultVernWs;
			// Default reversals to the default analysis writing system (change if insufficient)
			// TODO: default the reversalWs to "Current reversal writing system"
			int defaultReversalWs = cache.DefaultAnalWs;
			int fallbackUserWs = FallbackUserWs(cache);
			int englishWs = cache.ServiceLocator.WritingSystemManager.GetWsFromStr("en");
			var writingSystems = cache.ServiceLocator.WritingSystems;

			switch (ws)
			{
				case kwsVernInParagraph:
					// Even if we don't pass in a twfic, we can guess the ws in general for a text's paragraph
					// is the ws of the first character in its string.
					IStTxtPara para = null;
					switch (sda.get_IntProp(hvo, CmObjectTags.kflidClass))
					{
						case CmBaseAnnotationTags.kClassId:
							var ann = (ICmBaseAnnotation) cache.ServiceLocator.GetObject(hvo);
							retWs = TsStringUtils.GetWsAtOffset(((IStTxtPara) ann.BeginObjectRA).Contents, ann.BeginOffset);
							break;
						case StTxtParaTags.kClassId:
						case ScrTxtParaTags.kClassId:
							para = cache.ServiceLocator.GetInstance<IStTxtParaRepository>().GetObject(hvo);
							break;
						case StTextTags.kClassId:
							// get the first paragraph. REVIEW (Hasso) 2018.01: the first occurence of a Vern WS could be in a following paragraph.
							var stText = (IStText) cache.ServiceLocator.GetObject(hvo);
							para = stText.ParagraphsOS.FirstOrDefault() as IStTxtPara;
							break;
					}
					if (retWs != 0)
					{
						break;
					}
					if (para == null)
					{
						retWs = defaultVernWs;
						break;
					}

					// Find the first vernacular WS in the paragraph
					var allVerns = new HashSet<int>(cache.LanguageProject.CurrentVernacularWritingSystems.Handles());
					foreach (var run in para.Contents.Runs())
					{
						var runWs = run.Props.GetWs();
						if (allVerns.Contains(runWs))
						{
							retWs = runWs;
							break;
						}
					}
					if (retWs == 0)
					{
						retWs = defaultVernWs;
					}
					break;
				case kwsAnals:
				case kwsAnal:
				case kwsAnalVerns:
					retWs = defaultAnalWs;
					break;
				case kwsVerns:
				case kwsVern:
				case kwsVernAnals:
					retWs = defaultVernWs;
					break;
				case kwsFirstAnal:
					if (flid == 0) // sometimes used this way, just trying for a ws...make robust
					{
						retWs = defaultAnalWs;
						return null;
					}
					retWs = GetStringFromWsCollection(out retTss, writingSystems.CurrentAnalysisWritingSystems, hvo, flid, sda);
					if (retWs == 0)
					{
						// Try non-current analysis WSes.
						retWs = GetStringFromWsCollection(out retTss, writingSystems.AnalysisWritingSystems, hvo, flid, sda);
					}
					if (retWs == 0)
					{
						// Now try the default user ws.
						retTss = sda.get_MultiStringAlt(hvo, flid, defaultUserWs);
						if (retTss.Length > 0)
						{
							retWs = defaultUserWs;
							break;
						}
					}
					if (retWs == 0)
					{
						// Now try the fallback WS.
						retTss = sda.get_MultiStringAlt(hvo, flid, fallbackUserWs);
						if (retTss.Length > 0)
						{
							retWs = fallbackUserWs;
							break;
						}
					}
					if (retWs == 0)
					{
						// Now try English.
						retTss = sda.get_MultiStringAlt(hvo, flid, englishWs);
						if (retTss.Length > 0)
						{
							retWs = englishWs;
							break;
						}
					}
					break;
				case kwsFirstVernOrNamed:
				case kwsFirstVern:
					if (flid == 0) // sometimes used this way, just trying for a ws...make robust
					{
						retWs = defaultVernWs;
						return null;
					}
					var triedWsList = new HashSet<int>();
					// try the current vernacular writing systems
					if (TryFirstWsInList(sda, hvo, flid, cache.LanguageProject.CurrentVernacularWritingSystems.Handles(),
						ref triedWsList, out retWs, out retTss))
					{
						break;
					}
					// Try non-current vernacular WSes.
					if (TryFirstWsInList(sda, hvo, flid, cache.LanguageProject.VernacularWritingSystems.Handles(),
						ref triedWsList, out retWs, out retTss))
					{
						break;
					}
					// Now try the default user ws.
					if (TryFirstWsInList(sda, hvo, flid, new[] { defaultUserWs },
						ref triedWsList, out retWs, out retTss))
					{
						break;
					}
					// Now try the fallback ws.
					if (TryFirstWsInList(sda, hvo, flid, new[] { FallbackUserWs(cache) },
						ref triedWsList, out retWs, out retTss))
					{
						break;
					}
					// Now try English.
					if (TryFirstWsInList(sda, hvo, flid, new[] { cache.ServiceLocator.WritingSystemManager.GetWsFromStr("en") },
						ref triedWsList, out retWs, out retTss))
					{
						break;
					}
					if (ws == kwsFirstVernOrNamed)
					{
						// try to get a ws in the named writing systems that we haven't already tried.
						if (TryFirstWsInList(sda, hvo, flid,
							cache.ServiceLocator.WritingSystems.AllWritingSystems.Handles().ToArray(),
							ref triedWsList, out retWs, out retTss))
						{
							break;
						}
					}
					break;
				case kwsFirstAnalOrVern:
					if (flid == 0) // sometimes used this way, just trying for a ws...make robust
					{
						retWs = defaultAnalWs;
						return null;
					}
					retWs = GetStringFromWsCollection(out retTss, writingSystems.CurrentAnalysisWritingSystems, hvo, flid, sda);
					if (retWs == 0)
					{
						retWs = GetStringFromWsCollection(out retTss, writingSystems.CurrentVernacularWritingSystems, hvo, flid, sda);
					}
					if (retWs == 0)
					{
						// Try non-current analysis WSes.
						retWs = GetStringFromWsCollection(out retTss, writingSystems.AnalysisWritingSystems, hvo, flid, sda);
					}
					if (retWs == 0)
					{
						// Try non-current vernacular WSes.
						retWs = GetStringFromWsCollection(out retTss, writingSystems.VernacularWritingSystems, hvo, flid, sda);
					}
					if (retWs == 0)
					{
						// Now try the default user ws.
						retTss = sda.get_MultiStringAlt(hvo, flid, defaultUserWs);
						if (retTss != null && retTss.Length > 0)
						{
							retWs = defaultUserWs;
							break;
						}
					}
					if (retWs == 0)
					{
						// Now try the fallback WS.
						retTss = sda.get_MultiStringAlt(hvo, flid, fallbackUserWs);
						if (retTss != null && retTss.Length > 0)
						{
							retWs = fallbackUserWs;
							break;
						}
					}
					if (retWs == 0)
					{
						// Now try English.
						retTss = sda.get_MultiStringAlt(hvo, flid, englishWs);
						if (retTss != null && retTss.Length > 0)
						{
							retWs = englishWs;
							break;
						}
					}
					break;
				case kwsFirstVernOrAnal:
					if (flid == 0) // sometimes used this way, just trying for a ws...make robust
					{
						retWs = defaultVernWs;
						return null;
					}
					retWs = GetStringFromWsCollection(out retTss, writingSystems.CurrentVernacularWritingSystems, hvo, flid, sda);
					if (retWs == 0)
					{
						retWs = GetStringFromWsCollection(out retTss, writingSystems.CurrentAnalysisWritingSystems, hvo, flid, sda);
					}
					if (retWs == 0)
					{
						// Try non-current vernacular WSes.
						retWs = GetStringFromWsCollection(out retTss, writingSystems.VernacularWritingSystems, hvo, flid, sda);
					}
					if (retWs == 0)
					{
						// Try non-current analysis WSes.
						retWs = GetStringFromWsCollection(out retTss, writingSystems.AnalysisWritingSystems, hvo, flid, sda);
					}
					if (retWs == 0)
					{
						// Now try the default user ws.
						retTss = sda.get_MultiStringAlt(hvo, flid, defaultUserWs);
						if (retTss != null && retTss.Length > 0)
						{
							retWs = defaultUserWs;
							break;
						}
					}
					if (retWs == 0)
					{
						// Now try the fallback WS.
						retTss = sda.get_MultiStringAlt(hvo, flid, fallbackUserWs);
						if (retTss != null && retTss.Length > 0)
						{
							retWs = fallbackUserWs;
							break;
						}
					}
					if (retWs == 0)
					{
						// Now try English.
						retTss = sda.get_MultiStringAlt(hvo, flid, englishWs);
						if (retTss != null && retTss.Length > 0)
						{
							retWs = englishWs;
							break;
						}
					}
					break;
				case(kwsPronunciation):
				case(kwsFirstPronunciation):
				{
					if (flid == 0) // sometimes used this way, just trying for a ws...make robust
					{
						retWs = writingSystems.DefaultPronunciationWritingSystem.Handle;
						return null;
					}
					retWs = GetStringFromWsCollection(out retTss, writingSystems.CurrentPronunciationWritingSystems, hvo, flid, sda);
					break;
				}
				case(kwsReversalIndex):
				{
					// We need the current reversal writing system, not the default one! (see LT-16851)
					if (CurrentReversalWsId > 0)
						retWs = CurrentReversalWsId;
					else if (flid != 0 && hvo != 0)
						retWs = GetStringFromWsCollection(out retTss, writingSystems.CurrentAnalysisWritingSystems, hvo, flid, sda);
					if (retWs == 0)
						retWs = defaultReversalWs;
					break;
				}
				case(kwsAllReversalIndex):
					retWs = defaultReversalWs;
					break;
				default:
					retWs = ws;
					break;
			}
			if (retWs != 0 && fWantString && retTss == null)
			{
				retTss = sda.get_MultiStringAlt(hvo, flid, retWs);
			}
			return retTss;
		}

		private static int GetStringFromWsCollection(out ITsString retTss, ICollection<CoreWritingSystemDefinition> wsList, int hvo, int flid, ISilDataAccess sda)
		{
			foreach(var ws in wsList)
			{
				retTss = sda.get_MultiStringAlt(hvo, flid, ws.Handle);
				if(retTss != null && retTss.Length > 0)
				{
					return ws.Handle;
				}
			}
			retTss = null;
			return 0;
		}

		/// <summary>
		/// first try wsPreferred, then wsSecondary (both can be a magic).
		/// </summary>
		/// <returns>
		/// true if ws is real and tssResult has content (length greater than 0)
		/// </returns>
		public static bool TryWs(LcmCache cache, int wsPreferred, int wsSecondary, int hvoOwner, int flidOwning, out int actualWs, out ITsString tssResult)
		{
			return TryWs(cache, wsPreferred, hvoOwner, flidOwning, out actualWs, out tssResult)
				|| TryWs(cache, wsSecondary, hvoOwner, flidOwning, out actualWs, out tssResult);
		}


		/// <summary>
		/// Try the given ws (can be magic) and return the resulting tss and actualWs.
		/// </summary>
		/// <returns>
		/// true if ws is real and tssResult has content (length greater than 0)
		/// </returns>
		public static bool TryWs(LcmCache cache, int ws, int hvoOwner, int flidOwning, out int actualWs, out ITsString tssResult)
		{
			tssResult = GetMagicStringAlt(cache, ws, hvoOwner, flidOwning, true, out actualWs);
			return actualWs > 0 && tssResult != null &&  tssResult.Length > 0;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the fallback user writing system.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static int FallbackUserWs(LcmCache cache)
		{
			return cache.WritingSystemFactory.GetWsFromStr(FallbackUserWsId);
		}

		private static bool TryFirstWsInList(ISilDataAccess sda, int hvo, int flid,
			IEnumerable<int> wssToTry, ref HashSet<int> wssTried, out int retWs, out ITsString retTss)
		{
			retTss = null;
			retWs = 0;
			foreach (var wsLoop in wssToTry)
			{
				if (wssTried.Contains(wsLoop))
					continue;
				wssTried.Add(wsLoop);
				retTss = sda.get_MultiStringAlt(hvo, flid, wsLoop);
				if (retTss == null || retTss.Length <= 0) continue;

				retWs = wsLoop;
				return true;
			}
			return false;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the fallback user writing system identifier as a string (e.g. "en").
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string FallbackUserWsId
		{
			get { return "en"; }
		}

		/// <summary>
		/// Get the magic WS name for the given id, or "", if not a magic WS name.
		/// </summary>
		public static string GetMagicWsNameFromId(int wsMagic)
		{
			string wsName;
			if (MagicWsIdToWsName.TryGetValue(wsMagic, out wsName))
				return wsName;

			Debug.Fail("Method encountered a Magic Ws ID that it did not understand");
			return string.Empty;
		}

		/// <summary>
		/// Return the list of writing system for the language represented by the writing system of
		/// the given object).
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="hvoObj">id of a reversal index entry, reversal index, PartOfSpeech or LexSense</param>
		/// <param name="forceIncludeEnglish">True, if it is to include English, no matter what.</param>
		/// <returns></returns>
		public static List<CoreWritingSystemDefinition> GetReversalIndexWritingSystems(LcmCache cache, int hvoObj, bool forceIncludeEnglish)
		{
			// This method actually handles reversal index, reversal index entry, other classes, and even hvo 0.
			CoreWritingSystemDefinition wsPrimary = GetReversalIndexEntryWritingSystem(cache, hvoObj, cache.LanguageProject.DefaultAnalysisWritingSystem);
			var rgwsWanted = new List<CoreWritingSystemDefinition>(4) { wsPrimary };
			foreach (CoreWritingSystemDefinition ws in cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems)
			{
				if (ws == wsPrimary)
					continue;

				if (ws.Language == wsPrimary.Language)
					rgwsWanted.Add(ws);
			}

			if (forceIncludeEnglish)
			{
				CoreWritingSystemDefinition english = cache.ServiceLocator.WritingSystemManager.Get("en");
				if (!rgwsWanted.Contains(english))
					rgwsWanted.Add(english);
			}

			return rgwsWanted;
		}

		/// <summary>
		/// Return an array of writing systems given an array of their HVOs.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="hvosWss">Array of LgWritingSystem Hvos.</param>
		/// <returns></returns>
		public static List<CoreWritingSystemDefinition> WssFromHvos(LcmCache cache, int[] hvosWss)
		{
			var result = new List<CoreWritingSystemDefinition>(hvosWss.Length);
			for (var i = 0; i < hvosWss.Length; i++)
				result.Add(cache.ServiceLocator.WritingSystemManager.Get(hvosWss[i]));

			return result;
		}

		/// <summary>
		/// Gets the writing system list.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="wsMagic">The ws magic.</param>
		/// <param name="forceIncludeEnglish">if set to <c>true</c> [force include english].</param>
		/// <returns></returns>
		public static List<CoreWritingSystemDefinition> GetWritingSystemList(LcmCache cache, int wsMagic, bool forceIncludeEnglish)
		{
			return GetWritingSystemList(cache, wsMagic, 0, forceIncludeEnglish);
		}

		/// <summary>
		/// Gets the writing system list.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="wsMagic">The ws magic.</param>
		/// <param name="hvoObj">The hvo obj.</param>
		/// <param name="forceIncludeEnglish">if set to <c>true</c> [force include english].</param>
		/// <returns></returns>
		public static List<CoreWritingSystemDefinition> GetWritingSystemList(LcmCache cache, int wsMagic, int hvoObj, bool forceIncludeEnglish)
		{
			// add only current writing systems (not all active), by default
			return GetWritingSystemList(cache, wsMagic, hvoObj, forceIncludeEnglish, false);
		}

		/// <summary>
		/// Gets the writing system list.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="wsMagic">The ws magic.</param>
		/// <param name="hvoObj">The hvo obj.</param>
		/// <param name="forceIncludeEnglish">if set to <c>true</c> [force include english].</param>
		/// <param name="fIncludeUncheckedActiveWss">if true, add appropriate non-current but active writing systems.</param>
		/// <returns></returns>
		public static List<CoreWritingSystemDefinition> GetWritingSystemList(LcmCache cache, int wsMagic, int hvoObj,
			bool forceIncludeEnglish, bool fIncludeUncheckedActiveWss)
		{
			switch (wsMagic)
			{
				case kwsAnals:
				case kwsAnal:
					return AnalysisWss(cache, fIncludeUncheckedActiveWss);
				case kwsVerns:
				case kwsVern:
					return VernWss(cache, fIncludeUncheckedActiveWss);
				case kwsAnalVerns:
					return AnalysisVernacularWss(cache, fIncludeUncheckedActiveWss);
				case kwsVernAnals:
					return VernacularAnalysisWss(cache, fIncludeUncheckedActiveWss);
				case kwsPronunciations:
					return cache.ServiceLocator.WritingSystems.CurrentPronunciationWritingSystems.ToList();
				case kwsAllReversalIndex:
					return GetReversalIndexWritingSystems(cache, hvoObj, forceIncludeEnglish);
				default: // for now some sort of default.
					return AnalysisWss(cache, fIncludeUncheckedActiveWss);
			}
		}

		private static List<CoreWritingSystemDefinition> VernWss(LcmCache cache, bool fIncludeUncheckedActiveWss)
		{
			return GetCurrentThenRemainingActiveWss(cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems,
				cache.ServiceLocator.WritingSystems.VernacularWritingSystems, !fIncludeUncheckedActiveWss);
		}


		private static List<CoreWritingSystemDefinition> AnalysisWss(LcmCache cache, bool fIncludeUncheckedActiveWss)
		{
			return GetCurrentThenRemainingActiveWss(cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems,
				cache.ServiceLocator.WritingSystems.AnalysisWritingSystems, !fIncludeUncheckedActiveWss);
		}

		/// <summary>
		/// gets a list of ws hvos, starting with the current wss, followed by remaining (non-current) active ones
		/// </summary>
		/// <param name="currentWss"></param>
		/// <param name="activeWss"></param>
		/// <param name="fAddOnlyCurrent">if true, only add the current wss, ignoring remaining active wss.</param>
		/// <returns></returns>
		public static List<CoreWritingSystemDefinition> GetCurrentThenRemainingActiveWss(IEnumerable<CoreWritingSystemDefinition> currentWss, ICollection<CoreWritingSystemDefinition> activeWss,
			bool fAddOnlyCurrent)
		{
			List<CoreWritingSystemDefinition> wss = currentWss.ToList();

			if (fAddOnlyCurrent)
				return wss; // finished adding current wss, so return;

			// Now add the unchecked (or not current) writing systems to the list.
			foreach (CoreWritingSystemDefinition ws in activeWss)
			{
				if (!wss.Contains(ws))
					wss.Add(ws);
			}
			return wss;
		}

		private static List<CoreWritingSystemDefinition> VernacularAnalysisWss(LcmCache cache, bool fIncludeUncheckedActiveWss)
		{
			var mergedSet = new HashSet<CoreWritingSystemDefinition>();
			mergedSet.UnionWith(GetCurrentThenRemainingActiveWss(cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems,
				cache.ServiceLocator.WritingSystems.VernacularWritingSystems, !fIncludeUncheckedActiveWss));
			mergedSet.UnionWith(GetCurrentThenRemainingActiveWss(cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems,
				cache.ServiceLocator.WritingSystems.AnalysisWritingSystems, !fIncludeUncheckedActiveWss));
			return mergedSet.ToList();
		}

		private static List<CoreWritingSystemDefinition> AnalysisVernacularWss(LcmCache cache, bool fIncludeUncheckedActiveWss)
		{
			var mergedSet = new HashSet<CoreWritingSystemDefinition>();
			mergedSet.UnionWith(GetCurrentThenRemainingActiveWss(cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems,
				cache.ServiceLocator.WritingSystems.VernacularWritingSystems, !fIncludeUncheckedActiveWss));
			mergedSet.UnionWith(GetCurrentThenRemainingActiveWss(cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems,
				cache.ServiceLocator.WritingSystems.AnalysisWritingSystems, !fIncludeUncheckedActiveWss));
			return mergedSet.ToList();
		}

		/// <summary>
		/// Finds or creates a writing system based on the specified identifier. If it doesn't find an existing one, it will add it
		/// to the requested collection(s).
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="templateDir">The template directory.</param>
		/// <param name="identifier">The identifier.</param>
		/// <param name="addAnalWss">if set to <c>true</c> ***new*** writing systems will be added to the list of analysis writing systems.</param>
		/// <param name="addVernWss">if set to <c>true</c> ***new*** writing systems will be added to the list of vernacular writing systems.</param>
		/// <param name="ws">The writing system.</param>
		/// <returns>true if the writing system already exists, false if it had to be created</returns>
		public static bool FindOrCreateWritingSystem(LcmCache cache, string templateDir, string identifier, bool addAnalWss, bool addVernWss, out CoreWritingSystemDefinition ws)
		{
			if (cache.ServiceLocator.WritingSystemManager.GetOrSet(identifier, out ws))
				return true;

			if (addAnalWss)
				cache.ServiceLocator.WritingSystems.AddToCurrentAnalysisWritingSystems(ws);
			if (addVernWss)
				cache.ServiceLocator.WritingSystems.AddToCurrentVernacularWritingSystems(ws);
			// If the new writing system is one for which we have localized versions of lists, import them.
			// We can't easily put up a progress dialog here, because the code is in a project we can't reference.
			// However this routine is used in relatively long operations anyway so there should already be some kind of progress bar.
			if (templateDir != null)
				XmlTranslatedLists.ImportTranslatedListsForWs(identifier, cache, templateDir, null);
			return false;
		}

		/// <summary>
		/// Find or creates a writing system corresponding as best we can to identifier. This routine will always come up with
		/// something, even if it involves drastically mangling identifier to make something valid. It is primarily used in
		/// imports where we must make something. If a new WS must be created, it will be added to analysis or vernacular writing
		/// systems if the appropriate flag is set.
		/// </summary>
		/// <returns>true if the writing system already exists, false if it had to be created</returns>
		public static bool FindOrCreateSomeWritingSystem(LcmCache cache, string templateDir, string identifier, bool addAnalWss, bool addVernWss, out CoreWritingSystemDefinition ws)
		{
			if (cache.ServiceLocator.WritingSystemManager.TryGet(identifier, out ws))
				return true;

			// Check if this is a valid language tag
			if (IetfLanguageTag.IsValid(identifier))
				return FindOrCreateWritingSystem(cache, templateDir, identifier, addAnalWss, addVernWss, out ws);

			// Try cleaning up an old style language tag
			var langTagCleaner = new IetfLanguageTagCleaner(identifier);
			langTagCleaner.Clean();
			string newIdentifier = langTagCleaner.GetCompleteTag();
			if (IetfLanguageTag.IsValid(newIdentifier))
				return FindOrCreateWritingSystem(cache, templateDir, newIdentifier, addAnalWss, addVernWss, out ws);

			// Try converting an ICU locale to a valid language tag.
			newIdentifier = IetfLanguageTag.ToLanguageTag(identifier);
			if (IetfLanguageTag.IsValid(newIdentifier))
				return FindOrCreateWritingSystem(cache, templateDir, newIdentifier, addAnalWss, addVernWss, out ws);

			// No, it's nothing we know how to deal with. Get drastic.
			// First strip out everything invalid.
			var badChars = new Regex("[^-a-zA-Z0-9]");
			newIdentifier = badChars.Replace(identifier, "");
			// Get the hyphen-separated parts, each truncated to max 40 characters, skipping empty ones.
			var parts = from s in newIdentifier.Split('-') where s.Length > 0 select s.Length <= 40 ? s : s.Substring(0, 40);
			// Ensure that the first part is "x" or "X" to make it private use.
			if (parts.First().ToLower() != "x")
				parts = new[] {"x"}.Concat(parts);
			// If the result now has nothing left except the x, add something arbitrary.
			if (parts.Count() == 1)
				parts = new[] {"qaa", "x", "qaa"};
			else
				parts = new [] {"qaa"}.Concat(parts); // Can't start with x, we always use qaa for unknown WS.
			newIdentifier = parts.Aggregate((first, second) => first + "-" + second);
			// This should now qualify as a private-use writing system identifier.
			return FindOrCreateWritingSystem(cache, templateDir, newIdentifier, addAnalWss, addVernWss, out ws);
		}

		/// <summary>
		/// Updates all of the writing system fields in the specified LCM cache.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="origWsId">The original writing system identifier.</param>
		/// <param name="newWsId">The new writing system identifier.</param>
		public static void UpdateWritingSystemFields(LcmCache cache, string origWsId, string newWsId)
		{
			ILcmServiceLocator servLocator = cache.ServiceLocator;

			if (newWsId != null)
			{
				// When writing sytem code is changed, reversal index entries are preserved. (See LT-18256)
				var reversalsToUpdate = servLocator.GetInstance<IReversalIndexRepository>().AllInstances().Where(reversalIndex =>
					reversalIndex.WritingSystem == origWsId).ToList();
				foreach (var reversal in reversalsToUpdate)
				{
					reversal.WritingSystem = newWsId;
					var wsName = servLocator.WritingSystemManager.WritingSystemStore.AllWritingSystems
						.First(x => x.LanguageTag == newWsId).DisplayLabel;
					reversal.Name.SetAnalysisDefaultWritingSystem(wsName);
				}
			}
			else
			{
				var condemnedReversals = servLocator.GetInstance<IReversalIndexRepository>().AllInstances().Where(reversalIndex =>
					reversalIndex.WritingSystem == origWsId).ToList();
				foreach (var condemnedReversal in condemnedReversals)
				{
					((ILexDb)condemnedReversal.Owner).ReversalIndexesOC.Remove(condemnedReversal);
				}
			}
			if (cache.LangProject.HomographWs == origWsId)
			{
				var homographConfig = cache.ServiceLocator.GetInstance<HomographConfiguration>();

				if (newWsId == null)
				{
					homographConfig.WritingSystem = string.Empty;
					homographConfig.CustomHomographNumbers = new List<string>();
				}
				else
				{
					homographConfig.WritingSystem = newWsId;
				}
				cache.LangProject.HomographWs = newWsId;
			}

			UpdateWritingSystemField(cache, servLocator.GetInstance<IWordformLookupListRepository>().AllInstances(),
				WordformLookupListTags.kflidWritingSystem, origWsId, newWsId);

			UpdateWritingSystemField(cache, servLocator.GetInstance<ICmPossibilityListRepository>().AllInstances(),
				CmPossibilityListTags.kflidWritingSystem, origWsId, newWsId);

			UpdateWritingSystemField(cache, servLocator.GetInstance<ICmBaseAnnotationRepository>().AllInstances(),
				CmBaseAnnotationTags.kflidWritingSystem, origWsId, newWsId);

			UpdateWritingSystemField(cache, servLocator.GetInstance<IFsOpenFeatureRepository>().AllInstances(),
				FsOpenFeatureTags.kflidWritingSystem, origWsId, newWsId);

			UpdateWritingSystemField(cache, servLocator.GetInstance<IScrMarkerMappingRepository>().AllInstances(),
				ScrMarkerMappingTags.kflidWritingSystem, origWsId, newWsId);

			UpdateWritingSystemField(cache, servLocator.GetInstance<IScrImportSourceRepository>().AllInstances(),
				ScrImportSourceTags.kflidWritingSystem, origWsId, newWsId);

			UpdateWritingSystemListField(cache, cache.LanguageProject, LangProjectTags.kflidVernWss, origWsId, newWsId);
			UpdateWritingSystemListField(cache, cache.LanguageProject, LangProjectTags.kflidAnalysisWss, origWsId, newWsId);
			UpdateWritingSystemListField(cache, cache.LanguageProject, LangProjectTags.kflidCurVernWss, origWsId, newWsId);
			UpdateWritingSystemListField(cache, cache.LanguageProject, LangProjectTags.kflidCurAnalysisWss, origWsId, newWsId);
			UpdateWritingSystemListField(cache, cache.LanguageProject, LangProjectTags.kflidCurPronunWss, origWsId, newWsId);
			UpdateLiftResidue(origWsId, newWsId, cache.ServiceLocator.GetInstance<ILexEntryRepository>().AllInstances(),
				item => item.LiftResidue, (item, val) => item.LiftResidue = val);
			UpdateLiftResidue(origWsId, newWsId, cache.ServiceLocator.GetInstance<ILexExampleSentenceRepository>().AllInstances(),
				item => item.LiftResidue, (item, val) => item.LiftResidue = val);
			UpdateLiftResidue(origWsId, newWsId, cache.ServiceLocator.GetInstance<ILexPronunciationRepository>().AllInstances(),
				item => item.LiftResidue, (item, val) => item.LiftResidue = val);
			UpdateLiftResidue(origWsId, newWsId, cache.ServiceLocator.GetInstance<ILexSenseRepository>().AllInstances(),
				item => item.LiftResidue, (item, val) => item.LiftResidue = val);
			UpdateLiftResidue(origWsId, newWsId, cache.ServiceLocator.GetInstance<IMoFormRepository>().AllInstances(),
				item => item.LiftResidue, (item, val) => item.LiftResidue = val);
			UpdateLiftResidue(origWsId, newWsId, cache.ServiceLocator.GetInstance<IMoMorphSynAnalysisRepository>().AllInstances(),
				item => item.LiftResidue, (item, val) => item.LiftResidue = val);
			UpdateLiftResidue(origWsId, newWsId, cache.ServiceLocator.GetInstance<ILexEtymologyRepository>().AllInstances(),
				item => item.LiftResidue, (item, val) => item.LiftResidue = val);
			UpdateLiftResidue(origWsId, newWsId, cache.ServiceLocator.GetInstance<ILexReferenceRepository>().AllInstances(),
				item => item.LiftResidue, (item, val) => item.LiftResidue = val);
			UpdateLiftResidue(origWsId, newWsId, cache.ServiceLocator.GetInstance<ILexEntryRefRepository>().AllInstances(),
				item => item.LiftResidue, (item, val) => item.LiftResidue = val);
		}

		private static void UpdateLiftResidue<T>(string origWsId, string newWsId, IEnumerable<T> instances,
			Func<T, string> getter, Action<T, string> setter)
		{
			foreach (var item in instances)
			{
				var oldVal = getter(item);
				if (String.IsNullOrEmpty(oldVal))
					continue;
				// We may have more than one root element which .Parse can't handle.
				var contentElt = XElement.Parse("<x>" + oldVal + "</x>");
				bool changedResidue = false;
				foreach (var elt in contentElt.XPathSelectElements("//*[@lang]"))
				{
					var attr = elt.Attribute("lang");
					if (attr == null)
						continue; // pathological, but let's try to survive
					if (attr.Value != origWsId)
						continue;
					changedResidue = true;
					attr.Value = newWsId;
				}
				if (changedResidue)
				{
					string s = "";
					foreach (var node in contentElt.Nodes())
						s += node.ToString();
					setter(item, s);
				}
			}
		}

		private static void UpdateWritingSystemField(LcmCache cache, IEnumerable<ICmObject> objs, int flid, string origWsId, string newWsId)
		{
			foreach (ICmObject obj in objs)
			{
				string wsId = cache.DomainDataByFlid.get_UnicodeProp(obj.Hvo, flid);
				if (wsId == origWsId)
					cache.DomainDataByFlid.set_UnicodeProp(obj.Hvo, flid, newWsId);
			}
		}

		internal static void UpdateWritingSystemListField(LcmCache cache, ICmObject obj, int flid, string origWsId, string newWsId)
		{
			string wsIdsStr = cache.DomainDataByFlid.get_UnicodeProp(obj.Hvo, flid);
			if (string.IsNullOrEmpty(wsIdsStr))
				return;

			string[] wsIds = wsIdsStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (wsIds.Contains(newWsId, StringComparer.OrdinalIgnoreCase))
				wsIds = wsIds.Where(item => !item.Equals(origWsId, StringComparison.OrdinalIgnoreCase)).ToArray();
			else
				wsIds = (from item in wsIds select item.Equals(origWsId, StringComparison.OrdinalIgnoreCase) ? newWsId : item).ToArray();
			var newVal = string.Join(" ", wsIds.Where(x => x != null));
			cache.DomainDataByFlid.set_UnicodeProp(obj.Hvo, flid, newVal.Length == 0 ? null : newVal);
		}

		/// <returns>
		/// the handles of all Writing Systems that have text in the project, including text embedded in other writing systems' strings
		/// </returns>
		public static ISet<int> FindAllWritingSystemsWithText(LcmCache cache)
		{
			var allHandles = new HashSet<int>();
			StringServices.CrawlStrings(cache, str => FindAllWritingSystemsInTsString(str, allHandles), multiStr =>
			{
				for (var i = 0; i < multiStr.StringCount; i++)
				{
					var strAtI = multiStr.GetStringFromIndex(i, out var ws);
					if (strAtI.Length > 0)
					{
						FindAllWritingSystemsInTsString(strAtI, allHandles);
						allHandles.Add(ws);
					}
				}
			});
			return allHandles;
		}

		private static ITsString FindAllWritingSystemsInTsString(ITsString str, ISet<int> outWsHandles)
		{
			if (str.Length == 0)
			{
				return str;
			}
			return StringServices.CrawlRuns(str, run =>
			{
				outWsHandles.Add(run.get_WritingSystemAt(0));
				return run;
			});
		}

		/// <summary>
		/// Deletes the writing system from the specified LCM cache.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="ws">The writing system.</param>
		public static void DeleteWritingSystem(LcmCache cache, CoreWritingSystemDefinition ws)
		{
			StringServices.CrawlStrings(cache, str => DeleteRuns(ws, str), multiStr => DeleteMultiString(ws, multiStr));

			UpdateWritingSystemFields(cache, ws.Id, null);
			ws.MarkedForDeletion = true;
		}

		private static ITsString DeleteRuns(CoreWritingSystemDefinition ws, ITsString str)
		{
			return StringServices.CrawlRuns(str, run => run.get_WritingSystemAt(0) == ws.Handle ? null : run);
		}

		private static void DeleteMultiString(CoreWritingSystemDefinition ws, ITsMultiString multiStr)
		{
			multiStr.set_String(ws.Handle, null);

			var changes = new Dictionary<int, ITsString>(multiStr.StringCount);
			for (int i = 0; i < multiStr.StringCount; i++)
			{
				int wsHandle;
				ITsString str = multiStr.GetStringFromIndex(i, out wsHandle);
				ITsString newStr = DeleteRuns(ws, str);
				// just to be safe, we don't want to modify the multi-string while we are iterating thru it
				if (str != newStr)
					changes[wsHandle] = newStr;
			}

			foreach (KeyValuePair<int, ITsString> change in changes)
				multiStr.set_String(change.Key, change.Value);
		}

		/// <summary>
		/// Merges the writing systems.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="fromWs">The writing system to merge from.</param>
		/// <param name="toWs">The writing system to merge into.</param>
		public static void MergeWritingSystems(LcmCache cache, CoreWritingSystemDefinition fromWs, CoreWritingSystemDefinition toWs)
		{
			StringServices.CrawlStrings(cache, str => StringServices.CrawlRuns(str,
				run => MergeRuns(fromWs.Handle, toWs.Handle, run)),
				multiStr => MergeMultiString(fromWs.Handle, toWs.Handle, multiStr));

			foreach (var para in cache.ServiceLocator.GetInstance<IStTxtParaRepository>().AllInstances())
			{
				if(para.StyleRules == null)
					continue;
				int variant;
				//if the paragraph has a spurious writing system that matches the one we are getting rid of
				if(para.StyleRules.GetIntPropValues((int)FwTextPropType.ktptWs, out variant) == fromWs.Handle)
				{
					var builder = para.StyleRules.GetBldr();
					//get rid of it
					builder.SetIntPropValues((int)FwTextPropType.ktptWs, -1, -1);
					para.StyleRules = builder.GetTextProps();
				}
			}

			UpdateWritingSystemFields(cache, fromWs.Id, toWs.Id);
			foreach (var style in cache.ServiceLocator.GetInstance<IStStyleRepository>().AllInstances())
			{
				var oldProps = style.Rules;
				if (oldProps == null)
					continue;
				var oldOverrides = oldProps.GetStrPropValue((int) FwTextPropType.ktptWsStyle);
				if (string.IsNullOrEmpty(oldOverrides))
					continue;
				var styleInfo = new BaseStyleInfo(style, fromWs).FontInfoOverrides;
				if (!styleInfo.ContainsKey(fromWs.Handle))
					continue;
				// If toWs already has explicit overrides, don't change them, just delete the obsolete one.
				FontInfo oldInfo;
				if (!(styleInfo.TryGetValue(toWs.Handle, out oldInfo) && oldInfo.IsAnyExplicit))
					styleInfo[toWs.Handle] = styleInfo[fromWs.Handle];
				styleInfo.Remove(fromWs.Handle);
				var bldr = oldProps.GetBldr();
				bldr.SetStrPropValue((int)FwTextPropType.ktptWsStyle, BaseStyleInfo.GetOverridesString(styleInfo));
				style.Rules = bldr.GetTextProps();
			}
			fromWs.MarkedForDeletion = true;
		}

		/// <summary>
		/// This is called when a writing system that may be in use has its ID changed (but not merged with another).
		/// Every object that uses the WS in its representation must be marked dirty so a new representation with
		/// the new ID can be written out. Also fields which store a string representation of the ID must be updated.
		/// </summary>
		public static void UpdateWritingSystemId(LcmCache cache, CoreWritingSystemDefinition changedWs, int oldWsHandle, string oldWsId)
		{
			UpdateWritingSystemFields(cache, oldWsId, changedWs.Id);
			StringServices.CrawlStrings(cache, (obj, str) => MergeRuns(oldWsHandle, changedWs.Handle, str), (obj, ms) => MergeMultiString(oldWsHandle, changedWs.Handle, ms));
		}

		/// <summary>
		/// If the string contains text in the specified ws, mark the object as dirty.
		/// </summary>
		private static ITsString MergeRuns(int fromWsHandle, int toWsHandle, ITsString tsString)
		{
			var tsb = tsString.GetBldr();
			var noRunsChanged = true;
			foreach (var run in tsString.Runs())
			{
				int runWs = run.Props.GetIntPropValues((int)FwTextPropType.ktptWs, out _);
				if (runWs != fromWsHandle)
					continue;
				tsb.SetIntPropValues(run.IchMin, run.IchLim, (int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, toWsHandle);
				noRunsChanged = false;
			}
			return noRunsChanged ? tsString : tsb.GetString();
		}

		/// <summary>
		/// If the string contains text in the specified ws, mark the object as dirty.
		/// </summary>
		private static void MergeMultiString(int fromWs, int toWs, ITsMultiString multiStr)
		{
			var changes = new Dictionary<int, ITsString>(multiStr.StringCount);
			ITsString toWsStr = null;
			ITsString fromWsStr = null;
			for (int i = 0; i < multiStr.StringCount; i++)
			{
				ITsString str = multiStr.GetStringFromIndex(i, out var wsHandle);
				ITsString newStr = StringServices.CrawlRuns(str, run => MergeRuns(fromWs, toWs, run));
				// just to be safe, we don't want to modify the multi-string while we are iterating thru it
				if (fromWs == wsHandle)
				{
					fromWsStr = newStr;
					// delete this writing system string
					changes[wsHandle] = null;
				}
				else
				{
					if (toWs == wsHandle)
						toWsStr = newStr;
					if (newStr != str)
						changes[wsHandle] = newStr;
				}
			}

			if (fromWsStr != null)
			{
				if (toWsStr != null)
				{
					if (!fromWsStr.Equals(toWsStr))
					{
						ITsIncStrBldr tisb = toWsStr.GetIncBldr();
						tisb.Append(";");
						tisb.AppendTsString(fromWsStr);
						changes[toWs] = tisb.GetString();
					}
				}
				else
				{
					changes[toWs] = fromWsStr;
				}
			}

			foreach (KeyValuePair<int, ITsString> change in changes)
				multiStr.set_String(change.Key, change.Value);
		}

		/// <summary>
		/// If there are no pronunciation writing systems selected, make a default set, with phonetic variants
		/// coming before phonemic variants (if either of those exist).  If neither exists, the primary
		/// vernacular writing system is selected.
		/// </summary>
		public static void InitializePronunciationWritingSystems(LcmCache cache)
		{
			IWritingSystemContainer wsContainer = cache.ServiceLocator.WritingSystems;
			if (wsContainer.CurrentPronunciationWritingSystems.Count > 0)
				return;

			NonUndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW(cache.ActionHandlerAccessor, () =>
			{
				CoreWritingSystemDefinition defVernWs = wsContainer.DefaultVernacularWritingSystem;
				IEnumerable<CoreWritingSystemDefinition> relatedWss = wsContainer.AllWritingSystems.Related(defVernWs).ToArray();

				foreach (CoreWritingSystemDefinition ws in relatedWss.Where(ws => ws.Variants.Contains(WellKnownSubtags.IpaVariant)))
					wsContainer.CurrentPronunciationWritingSystems.Add(ws);

				// Add the primary vernacular writing system if nothing else fits.
				if (wsContainer.CurrentPronunciationWritingSystems.Count == 0)
					wsContainer.CurrentPronunciationWritingSystems.Add(defVernWs);
			});
		}

		/// <summary>
		/// Try to get an actual writing system id from some ws string specification.
		/// If it does not recognize the ws spec string, it returns 0.
		/// </summary>
		/// <param name="cache"></param>
		/// <param name="wsSpec"></param>
		/// <param name="wsDefault"></param>
		/// <param name="hvoObj"></param>
		/// <param name="flid"></param>
		/// <param name="currentWS"></param>
		/// <returns>A Set of writing system ids, or an empty Set, if it can't recognize the wsSpec parameter.</returns>
		public static IEnumerable<int> GetWritingSystemIdsFromLabel(LcmCache cache,
															  string wsSpec, CoreWritingSystemDefinition wsDefault,
															  int hvoObj, int flid,
															  CoreWritingSystemDefinition currentWS)
		{
			var writingSystemIds = new HashSet<int>();

			switch (wsSpec.Trim().ToLowerInvariant())
			{
				case "all analysis":
					{
						writingSystemIds.UnionWith(cache.LanguageProject.CurrentAnalysisWritingSystems.Handles());
						break;
					}
				case "all vernacular":
					{
						writingSystemIds.UnionWith(cache.LanguageProject.CurrentVernacularWritingSystems.Handles());
						break;
					}
				case "analysis vernacular":
					{
						writingSystemIds.UnionWith(cache.LanguageProject.CurrentAnalysisWritingSystems.Handles());
						writingSystemIds.UnionWith(cache.LanguageProject.CurrentVernacularWritingSystems.Handles());
						break;
					}
				case "vernacular analysis":
					{
						writingSystemIds.UnionWith(cache.LanguageProject.CurrentVernacularWritingSystems.Handles());
						writingSystemIds.UnionWith(cache.LanguageProject.CurrentAnalysisWritingSystems.Handles());
						break;
					}
				default:
					writingSystemIds.Add(InterpretWsLabel(cache, wsSpec, wsDefault, hvoObj, flid, currentWS));
					break;
			}

			return writingSystemIds;
		}
	}

	/// <summary>
	/// A memory-based implementation of the IWritingSystemContainer interface.
	/// </summary>
	public class MemoryWritingSystemContainer : IWritingSystemContainer
	{
		private readonly HashSet<CoreWritingSystemDefinition> m_analWss;
		private readonly HashSet<CoreWritingSystemDefinition> m_vernWss;
		private readonly List<CoreWritingSystemDefinition> m_curAnalWss;
		private readonly List<CoreWritingSystemDefinition> m_curVernWss;
		private readonly List<CoreWritingSystemDefinition> m_curPronWss;

		/// <summary>
		/// Initializes a new instance of the <see cref="MemoryWritingSystemContainer"/> class.
		/// </summary>
		/// <param name="analWss">The analysis writing systems.</param>
		/// <param name="vernWss">The vernacular writing systems.</param>
		/// <param name="curAnalWss">The current analysis writing systems.</param>
		/// <param name="curVernWss">The current vernacular writing systems.</param>
		/// <param name="curPronWss">The current pronunciation writing systems.</param>
		public MemoryWritingSystemContainer(IEnumerable<CoreWritingSystemDefinition> analWss, IEnumerable<CoreWritingSystemDefinition> vernWss,
			IEnumerable<CoreWritingSystemDefinition> curAnalWss, IEnumerable<CoreWritingSystemDefinition> curVernWss, IEnumerable<CoreWritingSystemDefinition> curPronWss)
		{
			m_analWss = new HashSet<CoreWritingSystemDefinition>(analWss);
			m_vernWss = new HashSet<CoreWritingSystemDefinition>(vernWss);
			m_curAnalWss = new List<CoreWritingSystemDefinition>(curAnalWss);
			m_curVernWss = new List<CoreWritingSystemDefinition>(curVernWss);
			m_curPronWss = new List<CoreWritingSystemDefinition>(curPronWss);
		}

		/// <summary>
		/// Gets all writing systems.
		/// </summary>
		/// <value>All writing systems.</value>
		public IEnumerable<CoreWritingSystemDefinition> AllWritingSystems
		{
			get
			{
				return m_analWss.Union(m_vernWss);
			}
		}

		/// <summary>
		/// Gets the analysis writing systems.
		/// </summary>
		/// <value>The analysis writing systems.</value>
		public ICollection<CoreWritingSystemDefinition> AnalysisWritingSystems
		{
			get
			{
				return m_analWss;
			}
		}

		/// <summary>
		/// Gets the vernacular writing systems.
		/// </summary>
		/// <value>The vernacular writing systems.</value>
		public ICollection<CoreWritingSystemDefinition> VernacularWritingSystems
		{
			get
			{
				return m_vernWss;
			}
		}

		/// <summary>
		/// Gets the current analysis writing systems.
		/// </summary>
		/// <value>The current analysis writing systems.</value>
		public IList<CoreWritingSystemDefinition> CurrentAnalysisWritingSystems
		{
			get
			{
				return m_curAnalWss;
			}
		}

		/// <summary>
		/// Gets the current vernacular writing systems.
		/// </summary>
		/// <value>The current vernacular writing systems.</value>
		public IList<CoreWritingSystemDefinition> CurrentVernacularWritingSystems
		{
			get
			{
				return m_curVernWss;
			}
		}

		/// <summary>
		/// Gets the current pronunciation writing systems.
		/// </summary>
		/// <value>The current pronunciation writing systems.</value>
		public IList<CoreWritingSystemDefinition> CurrentPronunciationWritingSystems
		{
			get
			{
				return m_curPronWss;
			}
		}

		/// <summary>
		/// Gets the default analysis writing system.
		/// </summary>
		/// <value>The default analysis writing system.</value>
		public CoreWritingSystemDefinition DefaultAnalysisWritingSystem
		{
			get
			{
				return m_curAnalWss.FirstOrDefault();
			}
			set
			{
				if (DefaultAnalysisWritingSystem == value)
					return;
				if (!m_analWss.Contains(value))
					m_analWss.Add(value);
				m_curAnalWss.Remove(value);
				m_curAnalWss.Insert(0, value);
			}
		}

		/// <summary>
		/// Gets the default vernacular writing system.
		/// </summary>
		/// <value>The default vernacular writing system.</value>
		public CoreWritingSystemDefinition DefaultVernacularWritingSystem
		{
			get
			{
				return m_curVernWss.FirstOrDefault();
			}
			set
			{
				if (DefaultVernacularWritingSystem == value)
					return;
				if (!m_vernWss.Contains(value))
					m_vernWss.Add(value);
				m_curVernWss.Remove(value);
				m_curVernWss.Insert(0, value);
			}
		}

		/// <summary>
		/// Gets the default pronunciation writing system.
		/// </summary>
		/// <value>The default pronunciation writing system.</value>
		public CoreWritingSystemDefinition DefaultPronunciationWritingSystem
		{
			get
			{
				return m_curPronWss.FirstOrDefault();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds the given writing system to the current analysis writing systems
		/// and also to the collection of all analysis writing systems if necessary.
		/// </summary>
		/// <param name="ws">The writing system to add.</param>
		/// ------------------------------------------------------------------------------------
		public void AddToCurrentAnalysisWritingSystems(CoreWritingSystemDefinition ws)
		{
			if (!m_analWss.Contains(ws))
				m_analWss.Add(ws);
			m_curAnalWss.Add(ws);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds the given writing system to the current vernacular writing systems
		/// and also to the collection of all vernacular writing systems if necessary.
		/// </summary>
		/// <param name="ws">The writing system to add.</param>
		/// ------------------------------------------------------------------------------------
		public void AddToCurrentVernacularWritingSystems(CoreWritingSystemDefinition ws)
		{
			if (!m_vernWss.Contains(ws))
				m_vernWss.Add(ws);
			m_curVernWss.Add(ws);
		}
	}

	/// <summary>
	/// A collection of writing systems backed by a space-delimited list of writing system IDs
	/// contained in a string property of an LCM object.
	/// </summary>
	public class WritingSystemCollection : ICollection<CoreWritingSystemDefinition>
	{
		/// <summary>
		/// The LCM object
		/// </summary>
		protected readonly ICmObject m_obj;
		private readonly int m_flid;

		/// <summary>
		/// Event fired when the contents of the list changes.
		/// </summary>
		public event EventHandler<EventArgs> Changed;

		internal void RaiseChanged()
		{
			if (Changed != null)
				Changed(this, new EventArgs());
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WritingSystemCollection"/> class.
		/// </summary>
		/// <param name="obj">The obj.</param>
		/// <param name="flid">The flid.</param>
		public WritingSystemCollection(ICmObject obj, int flid)
		{
			m_obj = obj;
			m_flid = flid;
		}

		#region Implementation of IEnumerable

		/// <summary>
		/// Returns an enumerator that iterates through the collection.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
		/// </returns>
		/// <filterpriority>1</filterpriority>
		public IEnumerator<CoreWritingSystemDefinition> GetEnumerator()
		{
			foreach (string wsId in WsIds)
			{
				CoreWritingSystemDefinition ret;
				m_obj.Services.WritingSystemManager.TryGet(wsId, out ret);
				yield return ret;
			}
		}

		/// <summary>
		/// Returns an enumerator that iterates through a collection.
		/// </summary>
		/// <returns>
		/// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
		/// </returns>
		/// <filterpriority>2</filterpriority>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion

		#region Implementation of ICollection<WritingSystem>

		/// <summary>
		/// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1"/>.
		/// </summary>
		/// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
		public virtual void Add(CoreWritingSystemDefinition item)
		{
			var wsIds = new List<string>(WsIds);
			if (!wsIds.Contains(item.Id))
			{
				wsIds.Add(item.Id);
				WsIds = wsIds;
			}
			RaiseChanged();
		}

		/// <summary>
		/// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
		/// </summary>
		public void Clear()
		{
			Wss = string.Empty;
			RaiseChanged();
		}

		/// <summary>
		/// Determines whether the <see cref="T:System.Collections.Generic.ICollection`1"/> contains a specific value.
		/// </summary>
		/// <returns>
		/// true if <paramref name="item"/> is found in the <see cref="T:System.Collections.Generic.ICollection`1"/>; otherwise, false.
		/// </returns>
		/// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
		public bool Contains(CoreWritingSystemDefinition item)
		{
			return WsIds.Contains(item.Id);
		}

		/// <summary>
		/// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
		/// </summary>
		/// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1"/>. The <see cref="T:System.Array"/> must have zero-based indexing.</param>
		/// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null.</exception>
		/// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.</exception>
		/// <exception cref="T:System.ArgumentException"><paramref name="array"/> is multidimensional.
		///                     -or-
		///                 <paramref name="arrayIndex"/> is equal to or greater than the length of <paramref name="array"/>.
		///                     -or-
		///                     The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.
		///                 </exception>
		public void CopyTo(CoreWritingSystemDefinition[] array, int arrayIndex)
		{
			if (array == null)
				throw new ArgumentNullException("array");
			if (arrayIndex < 0)
				throw new ArgumentOutOfRangeException("arrayIndex");
			IList<string> wsIds = WsIds;
			if ((wsIds.Count > 0 && arrayIndex >= array.Length) || wsIds.Count > array.Length - arrayIndex)
				throw new ArgumentException("arrayIndex");
			if (array.Rank > 1)
				throw new ArgumentException("array");

			foreach (string wsId in wsIds)
				array[arrayIndex++] = m_obj.Services.WritingSystemManager.Get(wsId);
		}

		/// <summary>
		/// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
		/// </summary>
		/// <returns>
		/// true if <paramref name="item"/> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1"/>; otherwise, false. This method also returns false if <paramref name="item"/> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1"/>.
		/// </returns>
		/// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
		public bool Remove(CoreWritingSystemDefinition item)
		{
			bool removed = false;
			var newWsIds = new List<string>();
			foreach (string wsId in WsIds)
			{
				if (wsId == item.Id)
					removed = true;
				else
					newWsIds.Add(wsId);
			}
			WsIds = newWsIds;
			RaiseChanged();
			return removed;
		}

		/// <summary>
		/// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"/>.
		/// </summary>
		/// <returns>
		/// The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"/>.
		/// </returns>
		public int Count
		{
			get { return WsIds.Count; }
		}

		/// <summary>
		/// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
		/// </summary>
		/// <returns>
		/// true if the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only; otherwise, false.
		/// </returns>
		public bool IsReadOnly
		{
			get { return false; }
		}

		#endregion

		/// <summary>
		/// Gets or sets the writing systems string.
		/// </summary>
		/// <value>The writing systems string.</value>
		protected string Wss
		{
			get
			{
				return m_obj.Cache.DomainDataByFlid.get_UnicodeProp(m_obj.Hvo, m_flid);
			}

			set
			{
				m_obj.Cache.DomainDataByFlid.SetUnicode(m_obj.Hvo, m_flid, value, value.Length);
				RaiseChanged();
			}
		}

		/// <summary>
		/// Gets or sets the writing system IDs.
		/// </summary>
		/// <value>The writing system IDs.</value>
		protected IList<string> WsIds
		{
			get
			{
				string wss = Wss;
				if (string.IsNullOrEmpty(wss))
					return new string[0];
				return Wss.Split(new [] {' '}, StringSplitOptions.RemoveEmptyEntries);
			}

			set
			{
				var sb = new StringBuilder();
				bool first = true;
				foreach (string wsId in value)
				{
					if (!first)
						sb.Append(' ');
					sb.Append(wsId);
					first = false;
				}
				Wss = sb.ToString();
				RaiseChanged();
			}
		}
	}

	/// <summary>
	/// A list of writing systems backed by a space-delimited list of writing system IDs
	/// contained in a string property of an LCM object.
	/// </summary>
	public class WritingSystemList : WritingSystemCollection, IList<CoreWritingSystemDefinition>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="WritingSystemList"/> class.
		/// </summary>
		/// <param name="obj">The obj.</param>
		/// <param name="flid">The flid.</param>
		public WritingSystemList(ICmObject obj, int flid)
			: base(obj, flid)
		{
		}

		/// <summary>
		/// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1"/>.
		/// </summary>
		/// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
		public override void Add(CoreWritingSystemDefinition item)
		{
			string wss = Wss;
			if (string.IsNullOrEmpty(wss))
				wss = item.Id;
			else
				wss += " " + item.Id;
			Wss = wss;
			RaiseChanged();
		}

		#region Implementation of IList<WritingSystem>

		/// <summary>
		/// Determines the index of a specific item in the <see cref="T:System.Collections.Generic.IList`1"/>.
		/// </summary>
		/// <returns>
		/// The index of <paramref name="item"/> if found in the list; otherwise, -1.
		/// </returns>
		/// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.IList`1"/>.</param>
		public int IndexOf(CoreWritingSystemDefinition item)
		{
			return WsIds.IndexOf(item.Id);
		}

		/// <summary>
		/// Inserts an item to the <see cref="T:System.Collections.Generic.IList`1"/> at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
		/// <param name="item">The object to insert into the <see cref="T:System.Collections.Generic.IList`1"/>.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.</exception>
		public void Insert(int index, CoreWritingSystemDefinition item)
		{
			IList<string> wsIds = WsIds;
			if (index < 0 || index > wsIds.Count)
				throw new ArgumentOutOfRangeException("index");

			var newWsIds = new List<string>();
			for (int i = 0; i < wsIds.Count; i++)
			{
				if (i == index)
					newWsIds.Add(item.Id);
				newWsIds.Add(wsIds[i]);
			}
			if (index == wsIds.Count)
				newWsIds.Add(item.Id);
			WsIds = newWsIds;
			RaiseChanged();
		}

		/// <summary>
		/// Removes the <see cref="T:System.Collections.Generic.IList`1"/> item at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index of the item to remove.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.</exception>
		public void RemoveAt(int index)
		{
			IList<string> wsIds = WsIds;
			if (index < 0 || index >= wsIds.Count)
				throw new ArgumentOutOfRangeException("index");

			var newWsIds = new List<string>();
			for (int i = 0; i < wsIds.Count; i++)
			{
				if (i != index)
					newWsIds.Add(wsIds[i]);
			}
			WsIds = newWsIds;
			RaiseChanged();
		}

		/// <summary>
		/// Gets or sets the element at the specified index.
		/// </summary>
		/// <returns>
		/// The element at the specified index.
		/// </returns>
		/// <param name="index">The zero-based index of the element to get or set.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.</exception>
		public CoreWritingSystemDefinition this[int index]
		{
			get
			{
				IList<string> wsIds = WsIds;
				if (index < 0 || index >= wsIds.Count)
					throw new ArgumentOutOfRangeException("index");
				return m_obj.Services.WritingSystemManager.Get(wsIds[index]);
			}

			set
			{
				IList<string> wsIds = WsIds;
				if (index < 0 || index >= wsIds.Count)
					throw new ArgumentOutOfRangeException("index");
				var newWsIds = new List<string>(wsIds);
				newWsIds[index] = value.Id;
				WsIds = newWsIds;
				RaiseChanged();
			}
		}

		#endregion
	}
}
