﻿using SIL.WritingSystems;

namespace SIL.LCModel.Core.WritingSystems
{
	internal class CoreLdmlInFolderWritingSystemFactory : LdmlInFolderWritingSystemFactory<CoreWritingSystemDefinition>
	{
		public CoreLdmlInFolderWritingSystemFactory(CoreLdmlInFolderWritingSystemRepository writingSystemRepository)
			: base(writingSystemRepository)
		{
		}

		protected override CoreWritingSystemDefinition ConstructDefinition()
		{
			return new CoreWritingSystemDefinition();
		}

		protected override CoreWritingSystemDefinition ConstructDefinition(string ietfLanguageTag)
		{
			return new CoreWritingSystemDefinition(ietfLanguageTag);
		}

		protected override CoreWritingSystemDefinition ConstructDefinition(CoreWritingSystemDefinition ws, bool cloneId = false)
		{
			return new CoreWritingSystemDefinition(ws, cloneId);
		}
	}
}
