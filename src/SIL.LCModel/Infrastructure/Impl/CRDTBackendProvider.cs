using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using SIL.Extensions;
using SIL.LCModel.Application;
using SIL.LCModel.Core.Cellar;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainImpl;
using SIL.LCModel.DomainServices.DataMigration;
using SIL.LCModel.Utils;
using SIL.Lexicon;
using static SIL.LCModel.Infrastructure.Impl.CRDTBackendProvider;

namespace SIL.LCModel.Infrastructure.Impl
{
	/// <summary>
	/// This class persists and restores the model using CRDT operations
	/// </summary>
	internal class CRDTBackendProvider : BackendProvider
	{
		private HybridLogicalClock _clock;

		private CRDTFile CRDTLogFile { get; set; }

		public CRDTBackendProvider(LcmCache cache, IdentityMap identityMap, ICmObjectSurrogateFactory surrogateFactory, IFwMetaDataCacheManagedInternal mdc, IDataMigrationManager dataMigrationManager, ILcmUI ui, ILcmDirectories dirs, LcmSettings settings) : base(cache, identityMap, surrogateFactory, mdc, dataMigrationManager, ui, dirs, settings)
		{
		}

		protected override int StartupInternal(int currentModelVersion)
		{
			CRDTLogFile = new CRDTFile(ProjectId.Path);
			return CRDTLogFile.ModelVersion;
		}

		protected override void ShutdownInternal()
		{
		}

		protected override void CreateInternal()
		{
			_clock = new HybridLogicalClock(Guid.NewGuid().ToString(), DateTime.UtcNow.Ticks);
			// Make sure the directory exists
			if (!String.IsNullOrEmpty(ProjectId.ProjectFolder) && !Directory.Exists(ProjectId.ProjectFolder))
				Directory.CreateDirectory(ProjectId.ProjectFolder);

			if (File.Exists(ProjectId.Path))
				throw new InvalidOperationException(ProjectId.Path + " already exists.");

//			CreateSettingsStores();
			var doc = new CRDTJsonContent
			{
				ModelVersion = ModelVersion,
				Changes = new CmObjectChange[]{},
				LogicalClock = _clock
			};
			CRDTLogFile = new CRDTFile(ProjectId.Path);
			File.WriteAllText(ProjectId.Path, JsonConvert.SerializeObject(doc));
		}

		public override bool RenameDatabase(string sNewBasename)
		{
			return false;
		}

		public override ISettingsStore ProjectSettingsStore { get; }
		public override ISettingsStore UserSettingsStore { get; }
		protected override void UpdateVersionNumber()
		{
		}

		public override bool Commit(HashSet<ICmObjectOrSurrogate> newbies, HashSet<ICmObjectOrSurrogate> dirtballs, HashSet<ICmObjectId> goners)
		{
			// TODO: Implement performant streaming of changes to disc without having to deserialize the whole file
			var currentContent = JsonConvert.DeserializeObject<CRDTJsonContent>(File.ReadAllText(CRDTLogFile.Path));
			_clock++;
			currentContent.LogicalClock = _clock;
			foreach (var newObj in newbies)
			{
				currentContent.Changes.Add(CmObjectToCrdtChange.GenerateChangeFromNewObject(newObj.Object, (IFwMetaDataCacheManaged)m_cache.MetaDataCache, _clock));
			}
			foreach (var change in dirtballs)
			{
				var changeObject = change.Object;
				var existing = currentContent.Changes.First(c => c.Id == changeObject.Guid);
				currentContent.Changes.Add(CmObjectToCrdtChange.GenerateChangeDiff(existing, changeObject, (IFwMetaDataCacheManaged)m_cache.MetaDataCache, _clock));
			}
			foreach (var goner in goners)
			{
				currentContent.Changes.Add(new CmObjectChange {Id = goner.Guid, ModelVersion = ModelVersion, Deleted = true, Timestamp = _clock.ToString()});
			}

			File.WriteAllText(CRDTLogFile.Path, JsonConvert.SerializeObject(currentContent));
			return base.Commit(newbies, dirtballs, goners);
		}

		private static string EncodeTsString(ITsString tsString, int ws)
		{
			return ws + TSStringDilemeter + tsString.Text;
		}

		private const string TSStringDilemeter = "\u0000\u001D\u0000";

		private static class CmObjectToCrdtChange
		{
			public static CmObjectChange GenerateChangeFromNewObject(ICmObject obj,
				IFwMetaDataCacheManaged mdc, HybridLogicalClock clock)
			{
				var cmObj = new CmObjectChange();
				var fieldChanges = new List<FieldChange>();
				cmObj.ModelVersion = ModelVersion;
				cmObj.ObjectType = obj.ClassID;
				cmObj.Timestamp = clock.ToString();
				foreach (var field in mdc.GetFields(obj.ClassID, true,
							 (int)CellarPropertyTypeFilter.All))
				{
					var fieldType = mdc.GetFieldType(field);
					var fieldChange = new FieldChange {FieldId = field};
					var silDal = (ISilDataAccessManaged)obj.Cache.DomainDataByFlid;
					switch (fieldType)
					{
						case (int)CellarPropertyType.Guid:
							cmObj.Id = obj.Guid;
							break;
						case (int)CellarPropertyType.Boolean:
							fieldChange.FieldValue = silDal.get_BooleanProp(obj.Hvo, field).ToString();
							break;
						case (int)CellarPropertyType.Integer:
							fieldChange.FieldValue = silDal.get_IntProp(obj.Hvo, field).ToString();
							break;
						case (int)CellarPropertyType.String:
							fieldChange.FieldValue = silDal.get_StringProp(obj.Hvo, field).Text;
							break;
						case (int)CellarPropertyType.Unicode:
							fieldChange.FieldValue = silDal.get_UnicodeProp(obj.Hvo, field);
							break;
						case (int)CellarPropertyType.MultiUnicode:
						case (int)CellarPropertyType.MultiString:
							ITsMultiString tms = silDal.get_MultiStringProp(obj.Hvo, field);
							fieldChange.FieldValue = EncodeTsMultiString(tms);
							break;
						case (int) CellarPropertyType.GenDate:
							fieldChange.FieldValue = silDal.get_GenDateProp(obj.Hvo, field).ToXMLExportShortString();
							break;
						case (int)CellarPropertyType.Time:
							fieldChange.FieldValue = silDal.get_DateTime(obj.Hvo, field).ToISO8601TimeFormatWithUTCString();
							break;
						case (int)CellarPropertyType.ReferenceAtomic:
						case (int)CellarPropertyType.OwningAtomic:
							var refHvo = silDal.get_ObjectProp(obj.Hvo, field);
							if (refHvo > 0) // hvo 0 or less is not a valid object.
							{
								fieldChange.FieldValue = obj.Cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(refHvo).Guid.ToString();
							}
							break;
						case (int)CellarPropertyType.ReferenceSequence:
						case (int)CellarPropertyType.OwningSequence:
						case (int)CellarPropertyType.OwningCollection:
						case (int)CellarPropertyType.ReferenceCollection:
							var refHvos = silDal.VecProp(obj.Hvo, field);
							if (refHvos.Length > 0) // hvo 0 or less is not a valid object.
							{
								var hvoList = "";
								foreach(var hvo in refHvos)
								{
									hvoList += "," + obj.Cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(hvo).Guid;
								}

								fieldChange.FieldValue = hvoList.TrimStart(',');
							}
							break;
						default:
							fieldChange.FieldValue = mdc.GetFieldName(field) + $" unhandled type ({fieldType})";
							break;
					}
					fieldChanges.Add(fieldChange);
				}

				cmObj.FieldChanges = fieldChanges.ToArray();
				return cmObj;
			}

			public static CmObjectChange GenerateChangeDiff(CmObjectChange lastChange, ICmObject obj, IFwMetaDataCacheManaged mdc, HybridLogicalClock clock)
			{
				var cmObj = new CmObjectChange();
				var fieldChanges = new List<FieldChange>();
				cmObj.ModelVersion = ModelVersion;
				cmObj.ObjectType = obj.ClassID;
				cmObj.Id = obj.Guid;
				cmObj.Timestamp = clock.ToString();
				foreach (var field in mdc.GetFields(obj.ClassID, true, (int)CellarPropertyTypeFilter.AllOwning | (int)CellarPropertyTypeFilter.AllBasic))
				{
					var lastFieldChange = lastChange.FieldChanges.FirstOrDefault(fc => fc.FieldId == field);
					var fieldType = mdc.GetFieldType(field);
					var fieldChange = new FieldChange { FieldId = field };
					var silDal = (ISilDataAccessManaged)obj.Cache.DomainDataByFlid;
					switch (fieldType)
					{
						case (int)CellarPropertyType.Boolean:
							var propVal = silDal.get_BooleanProp(obj.Hvo, field).ToString();
							if (lastFieldChange == null || lastFieldChange.FieldValue != propVal)
							{
								fieldChange.FieldValue = propVal;
							}
							break;
						case (int)CellarPropertyType.Integer:
							propVal = silDal.get_IntProp(obj.Hvo, field).ToString();
							if (lastFieldChange == null || lastFieldChange.FieldValue != propVal)
							{
								fieldChange.FieldValue = propVal;
							}
							break;
						case (int)CellarPropertyType.String:
							propVal = EncodeTsString(silDal.get_StringProp(obj.Hvo, field), 0); // Fix me: get the right writing system
							if (lastFieldChange == null || lastFieldChange.FieldValue != propVal)
							{
								fieldChange.FieldValue = propVal;
							}
							break;
						case (int)CellarPropertyType.Unicode:
							propVal = silDal.get_UnicodeProp(obj.Hvo, field);
							if (lastFieldChange == null || lastFieldChange.FieldValue != propVal)
							{
								fieldChange.FieldValue = propVal;
							}
							break;
						case (int)CellarPropertyType.MultiUnicode:
						case (int)CellarPropertyType.MultiString:
							propVal = EncodeTsMultiString(silDal.get_MultiStringProp(obj.Hvo, field));
							if (lastFieldChange == null || lastFieldChange.FieldValue != propVal)
							{
								fieldChange.FieldValue = propVal;
							}
							break;
						case (int)CellarPropertyType.GenDate:
							propVal = silDal.get_GenDateProp(obj.Hvo, field).ToXMLExportShortString();
							if (lastFieldChange == null || lastFieldChange.FieldValue != propVal)
							{
								fieldChange.FieldValue = propVal;
							}
							break;
						case (int)CellarPropertyType.ReferenceAtomic:
							propVal = obj.Cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(silDal.get_ObjectProp(obj.Hvo, field)).Guid.ToString();
							if (lastFieldChange == null || lastFieldChange.FieldValue != propVal)
							{
								fieldChange.FieldValue = propVal;
							}
							break;
					}
					fieldChanges.Add(fieldChange);
				}

				cmObj.FieldChanges = fieldChanges.ToArray();
				return cmObj;
			}

			private static string EncodeTsMultiString(ITsMultiString tms)
			{
				var encodedString = "";
				for (int i = 0; i < tms.StringCount; ++i)
				{
					int ws;
					var tss = tms.GetStringFromIndex(i, out ws);
					if (tss != null && tss.Length > 0)
					{
						encodedString += EncodeTsString(tss, ws) + TSStringDilemeter;
					}
				}

				return encodedString;
			}
		}

		private class CRDTFile
		{
			public readonly string Path;

			public CRDTFile(string path)
			{
				Path = path;
			}

			public int ModelVersion => JsonConvert.DeserializeObject<CRDTJsonContent>(File.ReadAllText(Path)).ModelVersion;
		}

		internal class CRDTJsonContent
		{
			[JsonProperty("modelVersion")]
			public int ModelVersion;

			public ICollection<CmObjectChange> Changes;

			public HybridLogicalClock LogicalClock;
		}

		internal class CmObjectChange
		{
			public Guid Id { get; set; }
			[JsonProperty("v")]
			public int ModelVersion;
			[JsonProperty("ts")]
			public string Timestamp;
			[JsonProperty("ot")]
			public int ObjectType;
			[JsonProperty("fc")]
			public FieldChange[] FieldChanges;
			[JsonProperty("d")]
			public bool Deleted = false;
		}
		internal class FieldChange
		{
			[JsonProperty("id")]
			public int FieldId;
			[JsonProperty("data")]
			public string FieldValue;
		}
	}
}
