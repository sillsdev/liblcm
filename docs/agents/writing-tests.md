# Task: Writing Tests

Tests in liblcm use NUnit. The test infrastructure provides base classes that set up an `LcmCache` with the appropriate backend provider.

## Test Project Structure

Tests live in `tests/`:
- `SIL.LCModel.Tests/` -- Main library tests (model, domain services, infrastructure)
- `SIL.LCModel.Core.Tests/` -- Core utility tests
- `SIL.LCModel.Utils.Tests/` -- Utility tests
- `SIL.LCModel.FixData.Tests/` -- FixData tests

## Base Classes

### MemoryOnlyBackendProviderTestBase

**Use for**: Testing the LCM public API (properties, factories, repositories, domain services).

Located in `tests/SIL.LCModel.Tests/LcmTestBase.cs`.

This creates a fresh in-memory `LcmCache` with a blank language project per test fixture. No file I/O. This is the most common base class.

```csharp
using NUnit.Framework;
using SIL.LCModel.Infrastructure;

namespace SIL.LCModel.SomeArea
{
    [TestFixture]
    public class MyFeatureTests : MemoryOnlyBackendProviderTestBase
    {
        [Test]
        public void MyTest()
        {
            // Cache is available via the Cache property
            var lp = Cache.LanguageProject;

            // All data changes must be in a UnitOfWork
            UndoableUnitOfWorkHelper.Do("undo", "redo", m_actionHandler, () =>
            {
                // Create objects via factories
                var entry = Cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create();

                // Set properties
                var ws = Cache.DefaultVernWs;
                entry.CitationForm.VernacularDefaultWritingSystem =
                    TsStringUtils.MakeString("test", ws);

                // Assert
                Assert.IsNotNull(entry);
            });
        }
    }
}
```

Key points:
- `Cache` property gives you the `LcmCache`
- `m_actionHandler` is the `IActionHandler` for UnitOfWork operations
- Default writing systems: `Cache.DefaultAnalWs` (English), `Cache.DefaultVernWs` (French)
- Use `UndoableUnitOfWorkHelper.Do()` or `NonUndoableUnitOfWorkHelper.Do()` for data changes

### MemoryOnlyBackendProviderRestoredForEachTestTestBase

**Use for**: Tests that need a clean state for each test method (not just each fixture).

Same as above but disposes and recreates the cache before each `[Test]`.

### DataMigrationTestsBase

**Use for**: Testing data migrations.

Located in `tests/SIL.LCModel.Tests/DomainServices/DataMigration/DataMigrationTests.cs`.

Provides `m_dataMigrationManager` (an `IDataMigrationManager` instance).

```csharp
using System.Xml.Linq;
using NUnit.Framework;

namespace SIL.LCModel.DomainServices.DataMigration
{
    [TestFixture]
    public class DataMigration7000073Tests : DataMigrationTestsBase
    {
        [Test]
        public void DataMigration7000073Test()
        {
            // 1. Parse test data
            var dtos = DataMigrationTestServices.ParseProjectFile("DataMigration7000073.xml");

            // 2. Create repository at the PREVIOUS version
            var mockMdc = new MockMDCForDataMigration();
            IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(
                7000072, dtos, mockMdc, null, TestDirectoryFinder.LcmDirectories);

            // 3. Run the migration
            m_dataMigrationManager.PerformMigration(dtoRepos, 7000073, new DummyProgressDlg());

            // 4. Verify version
            Assert.AreEqual(7000073, dtoRepos.CurrentModelVersion);

            // 5. Verify data transformations
            var dto = dtoRepos.GetDTO("some-guid-from-test-data");
            var element = XElement.Parse(dto.Xml);
            Assert.IsNotNull(element.Element("ExpectedNewElement"));
        }
    }
}
```

**Test data files**: Migration tests use XML files containing sample `<rt>` elements. These live in the test data directory and are parsed by `DataMigrationTestServices.ParseProjectFile()`. Look at existing files like `DataMigration7000072.xml` for the format. The file should contain a minimal set of `<rt>` elements that exercise the migration logic.

## Common Test Patterns

### Creating Test Objects

```csharp
UndoableUnitOfWorkHelper.Do("undo", "redo", m_actionHandler, () =>
{
    // Factories are accessed via ServiceLocator
    var entryFactory = Cache.ServiceLocator.GetInstance<ILexEntryFactory>();
    var senseFactory = Cache.ServiceLocator.GetInstance<ILexSenseFactory>();

    var entry = entryFactory.Create();
    var sense = senseFactory.Create();
    entry.SensesOS.Add(sense);
});
```

### Setting String Properties

```csharp
int vernWs = Cache.DefaultVernWs;
int analWs = Cache.DefaultAnalWs;

// MultiUnicode
entry.CitationForm.set_String(vernWs, TsStringUtils.MakeString("word", vernWs));

// MultiString
sense.Definition.set_String(analWs, TsStringUtils.MakeString("a definition", analWs));
```

### Accessing Repositories

```csharp
var entryRepo = Cache.ServiceLocator.GetInstance<ILexEntryRepository>();
var allEntries = entryRepo.AllInstances();
var count = entryRepo.Count;
```

### Testing with Custom Fields

```csharp
using (var customField = new CustomFieldForTest(
    Cache, "My Field", "MyField",
    LexEntryTags.kClassId,
    CellarPropertyType.MultiUnicode,
    Guid.Empty))
{
    // customField.Flid gives you the field ID
    // Test using the custom field...
}
// Custom field is automatically removed on Dispose
```

## Running Tests

```
dotnet test --no-restore --no-build -p:ParallelizeAssembly=false --configuration Release
```

Or run individual test classes/methods from your IDE.

Tests must NOT run in parallel (`ParallelizeAssembly=false`) due to shared state in the ICU and writing system subsystems.

## Checklist

- [ ] Test class inherits from the appropriate base class
- [ ] `[TestFixture]` attribute on the class
- [ ] `[Test]` attribute on test methods
- [ ] All data changes wrapped in `UndoableUnitOfWorkHelper.Do()` or `NonUndoableUnitOfWorkHelper.Do()`
- [ ] Test assertions verify the expected behavior
- [ ] For migration tests: test data XML file created, repository initialized at previous version
- [ ] Tests pass: `dotnet test --configuration Release`
