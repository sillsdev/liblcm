using System.Runtime.CompilerServices;
using SIL.WritingSystems;

[assembly: InternalsVisibleTo("SIL.LCModel.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b511304f05af0a01cbc5408cdbdf742aa1664db0e1157235bb2619e7fb5e705bd3534a7157a088a458ec3136e46ebd2b73519fb07dffd2daa40a7b9aa340675d926ab918d2e0183b8613320529b8a490028c8e1b40b980f3724928455d447d8f93d459be3c55a4e3f2ef5119c3393fd25adba301cbff8a3ffbce2e181d143788")]
namespace SIL.LCModel.Core.WritingSystems
{
	/// <summary>
	/// A file-based global writing system store.
	/// </summary>
	public class CoreGlobalWritingSystemRepository : GlobalWritingSystemRepository<CoreWritingSystemDefinition>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="CoreGlobalWritingSystemRepository"/> class.
		/// </summary>
		public CoreGlobalWritingSystemRepository()
			: this(GlobalWritingSystemRepository.DefaultBasePath)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CoreGlobalWritingSystemRepository"/> class.
		/// </summary>
		internal CoreGlobalWritingSystemRepository(string basePath)
			: base(basePath)
		{
		}

		/// <summary>
		/// Creates the writing system factory.
		/// </summary>
		/// <returns></returns>
		protected override IWritingSystemFactory<CoreWritingSystemDefinition> CreateWritingSystemFactory()
		{
			return new CoreSldrWritingSystemFactory();
		}
	}
}
