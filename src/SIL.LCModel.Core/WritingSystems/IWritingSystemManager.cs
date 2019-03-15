using System.Collections.Generic;
using SIL.WritingSystems;

namespace SIL.LCModel.Core.WritingSystems
{
	public interface IWritingSystemManager
	{
		bool CanSave(CoreWritingSystemDefinition ws);
		CoreWritingSystemDefinition Create(LanguageSubtag languageSubtag, ScriptSubtag scriptSubtag, RegionSubtag regionSubtag, IEnumerable<VariantSubtag> variantSubtags);
		CoreWritingSystemDefinition Create(string ietfLanguageTag);
		CoreWritingSystemDefinition CreateFrom(CoreWritingSystemDefinition ws);
		bool Exists(int handle);
		bool Exists(string identifier);
		CoreWritingSystemDefinition Get(int handle);
		CoreWritingSystemDefinition Get(string identifier);
		void Replace(CoreWritingSystemDefinition ws);
		void Save();
		void Set(CoreWritingSystemDefinition ws);
		CoreWritingSystemDefinition Set(string identifier);
		bool TryGet(string identifier, out CoreWritingSystemDefinition ws);
	}
}