---
name: adding-a-property
description: Add a new persisted property to an existing class in MasterLCModel.xml, including model version bump, data migration, code regeneration, and tests. Use when the user asks to add a field, property, or attribute to an LCM model class.
---

# Adding a Property to an Existing Class

This guide covers adding a new persisted property to an existing class in the LCM model. This is one of the most common and most dangerous changes -- it touches the XML model, requires a data migration, and triggers code regeneration.

If you need a computed/derived property that is NOT persisted, use the `adding-a-virtual-property` skill instead.

## Prerequisites

- Read the Critical Rules section in AGENTS.md
- Know the target class name (e.g., `LexSense`)
- Know the property type (`basic`, `owning`, or `rel`) and signature

## Steps

### 1. Edit MasterLCModel.xml

File: `src/SIL.LCModel/MasterLCModel.xml`

Find the target class and add the property inside its `<props>` element. Choose the next available `num` for that class (check existing properties).

**Basic property example** (adding a `MultiString` field):
```xml
<basic num="37" id="NewFieldName" sig="MultiString">
  <comment>
    <para>Description of the field. No newlines inside para elements.</para>
  </comment>
</basic>
```

**Owning property example** (adding an owning sequence):
```xml
<owning num="37" id="OwnedThings" card="seq" sig="TargetClassName">
  <comment>
    <para>Description of owned objects.</para>
  </comment>
</owning>
```

**Reference property example** (adding a reference collection):
```xml
<rel num="37" id="RelatedItems" card="col" sig="CmPossibility">
  <comment>
    <para>Description of referenced objects.</para>
  </comment>
</rel>
```

Property type signatures for `<basic>`:
- `Integer`, `Boolean`, `String`, `Unicode`, `MultiString`, `MultiUnicode`
- `Time`, `GenDate`, `Binary`, `Guid`, `TextPropBinary`

Cardinality values for `<owning>` and `<rel>`:
- `atomic` -- zero or one target
- `seq` -- ordered list
- `col` -- unordered collection

### 2. Increment the Model Version

In the same file (`MasterLCModel.xml`), update the `version` attribute on the root `<EntireModel>` element:

```xml
<EntireModel version="7000073">
```

Add a change history entry just below the existing ones:

```xml
  DD Month YYYY (7000073): Added NewFieldName to ClassName. Brief description.
```

### 3. Write the Data Migration

Create: `src/SIL.LCModel/DomainServices/DataMigration/DataMigration7000073.cs`

**For new optional properties with safe defaults (most common case)**, existing data doesn't need modification. But you still need the migration class:

```csharp
// Copyright (c) YYYY SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

namespace SIL.LCModel.DomainServices.DataMigration
{
    internal class DataMigration7000073 : IDataMigration
    {
        public void PerformMigration(IDomainObjectDTORepository repoDto)
        {
            DataMigrationServices.CheckVersionNumber(repoDto, 7000072);
            // New optional property with safe default; no data changes needed.
            DataMigrationServices.IncrementVersionNumber(repoDto);
        }
    }
}
```

**IMPORTANT**: If you are adding a C# value type property (int, bool, GenDate, DateTime), you MUST add an explicit XML element with the default value to every existing instance. See the WARNING at the top of `MasterLCModel.xml`. Example migration that adds a default value:

```csharp
public void PerformMigration(IDomainObjectDTORepository repoDto)
{
    DataMigrationServices.CheckVersionNumber(repoDto, 7000072);

    foreach (var dto in repoDto.AllInstancesWithSubclasses("TargetClass"))
    {
        var element = XElement.Parse(dto.Xml);
        if (element.Element("NewBoolField") == null)
        {
            element.Add(new XElement("NewBoolField", new XAttribute("val", "False")));
            DataMigrationServices.UpdateDTO(repoDto, dto, element.ToString());
        }
    }

    DataMigrationServices.IncrementVersionNumber(repoDto);
}
```

### 4. Register the Migration

File: `src/SIL.LCModel/DomainServices/DataMigration/LcmDataMigrationManager.cs`

Add a line in the constructor, after the last existing entry:

```csharp
m_individualMigrations.Add(7000073, new DataMigration7000073());
```

If no data changes are needed, you can use `m_bumpNumberOnlyMigration` instead:
```csharp
m_individualMigrations.Add(7000073, m_bumpNumberOnlyMigration);
```
In this case you do NOT need to create a `DataMigration7000073.cs` file.

### 5. Rebuild to Regenerate Code

```
dotnet build --configuration Release
```

This triggers `GenerateModel` which regenerates all 9 `Generated*.cs` files from the updated `MasterLCModel.xml`. The new property will appear in the generated constants, interfaces, class implementations, etc.

### 6. Add Hand-Written Logic (if needed)

If the property needs custom logic beyond what the generator provides (computed side effects, validation, etc.), add it to the appropriate `Overrides*.cs` partial class in `src/SIL.LCModel/DomainImpl/`.

### 7. Write Tests

**Migration test**: Create `tests/SIL.LCModel.Tests/DomainServices/DataMigration/DataMigration7000073Tests.cs`

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
            // Parse test data XML (create a matching .xml file in the test data directory)
            var dtos = DataMigrationTestServices.ParseProjectFile("DataMigration7000073.xml");
            var mockMdc = new MockMDCForDataMigration();
            IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(
                7000072, dtos, mockMdc, null, TestDirectoryFinder.LcmDirectories);

            m_dataMigrationManager.PerformMigration(dtoRepos, 7000073, new DummyProgressDlg());

            // Assert the migration results
            Assert.AreEqual(7000073, dtoRepos.CurrentModelVersion);
            // Add specific assertions for your migration...
        }
    }
}
```

**API test**: For testing property access via the LCM API, inherit from `MemoryOnlyBackendProviderTestBase`. See the `writing-tests` skill.

## Checklist

- [ ] Property added to `MasterLCModel.xml` with correct `num`, `id`, `sig`, and (if relational) `card`
- [ ] `version` attribute incremented on `<EntireModel>`
- [ ] Change history comment added
- [ ] Migration class created OR `m_bumpNumberOnlyMigration` used
- [ ] Migration registered in `LcmDataMigrationManager` constructor
- [ ] If adding a C# value type: migration writes explicit defaults to all existing instances
- [ ] Build succeeds (`dotnet build --configuration Release`)
- [ ] Migration test written
- [ ] Generated files NOT manually edited
