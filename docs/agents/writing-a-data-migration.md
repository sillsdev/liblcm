# Task: Writing a Data Migration

Data migrations transform existing persisted data when the model version changes. They operate on raw XML via `DomainObjectDTO` objects -- live `CmObject` instances are NOT available during migration.

## When a Migration is Needed

- Adding a C# value-type property (int, bool, GenDate, DateTime) that needs explicit defaults
- Removing a property or class (must clean up existing XML)
- Renaming a property or class
- Changing a property's type or cardinality
- Restructuring ownership or references
- Any change that requires existing persisted data to be transformed

If the model change is purely additive (new optional reference/string property with no data to transform), you can use `m_bumpNumberOnlyMigration` in the manager instead. See step 4 in [adding-a-property.md](adding-a-property.md).

## Steps

### 1. Determine the Next Version Number

Check `src/SIL.LCModel/MasterLCModel.xml` for the current version in `<EntireModel version="...">`. The current version is **7000072**. Your migration file number is the next integer: **7000073**.

### 2. Create the Migration Class

Create: `src/SIL.LCModel/DomainServices/DataMigration/DataMigration7000073.cs`

```csharp
// Copyright (c) YYYY SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Xml.Linq;

namespace SIL.LCModel.DomainServices.DataMigration
{
    internal class DataMigration7000073 : IDataMigration
    {
        /// <summary>
        /// Brief description of what this migration does.
        /// </summary>
        public void PerformMigration(IDomainObjectDTORepository repoDto)
        {
            DataMigrationServices.CheckVersionNumber(repoDto, 7000072);

            // --- Your migration logic here ---

            DataMigrationServices.IncrementVersionNumber(repoDto);
        }
    }
}
```

**Mandatory structure:**
1. First line: `DataMigrationServices.CheckVersionNumber(repoDto, previousVersion)`
2. Middle: your data transformation
3. Last line: `DataMigrationServices.IncrementVersionNumber(repoDto)`

### 3. Common Migration Operations

**Finding objects:**
```csharp
// All instances of a class (including subclasses)
var entries = repoDto.AllInstancesWithSubclasses("LexEntry");

// All instances of exact class (no subclasses)
var entries = repoDto.AllInstancesSansSubclasses("LexEntry");

// Single object by GUID
var dto = repoDto.GetDTO("guid-string-here");

// Owner of an object
var ownerDto = repoDto.GetOwningDTO(dto);

// Directly owned children
var children = repoDto.GetDirectlyOwnedDTOs(dto.Guid);
```

**Modifying XML:**
```csharp
var element = XElement.Parse(dto.Xml);

// Add an element
element.Add(new XElement("NewProperty", new XAttribute("val", "False")));

// Remove an element
element.Element("OldProperty")?.Remove();

// Add an objsur (owning reference)
var container = new XElement("OwnedThings");
container.Add(new XElement("objsur",
    new XAttribute("guid", targetGuid),
    new XAttribute("t", "o")));  // "o" for owning, "r" for reference
element.Add(container);

// Save changes
DataMigrationServices.UpdateDTO(repoDto, dto, element.ToString());
```

**Creating new objects:**
```csharp
var newGuid = Guid.NewGuid().ToString().ToLowerInvariant();
var sb = new StringBuilder();
sb.AppendFormat("<rt class=\"ClassName\" guid=\"{0}\" ownerguid=\"{1}\">", newGuid, ownerGuid);
sb.Append("<PropertyName>");
sb.Append("<AUni ws=\"en\">value</AUni>");
sb.Append("</PropertyName>");
sb.Append("</rt>");
repoDto.Add(new DomainObjectDTO(newGuid, "ClassName", sb.ToString()));
```

**Removing objects:**
```csharp
// Remove object, its owned children, and clean up owner's objsur
DataMigrationServices.RemoveIncludingOwnedObjects(repoDto, dto, removeFromOwner: true);
```

**Creating possibility lists** (use the helper):
```csharp
DataMigrationServices.CreatePossibilityList(repoDto, listGuid, ownerGuid,
    new[] { Tuple.Create("en", "Abbr", "List Name") },
    DateTime.Now, WritingSystemServices.kwsAnals);
```

### 4. Register the Migration

File: `src/SIL.LCModel/DomainServices/DataMigration/LcmDataMigrationManager.cs`

Add at the end of the constructor's registration block:

```csharp
m_individualMigrations.Add(7000073, new DataMigration7000073());
```

### 5. Update MasterLCModel.xml Version

File: `src/SIL.LCModel/MasterLCModel.xml`

Update the version attribute and add a change history entry:
```xml
<EntireModel version="7000073">
```

### 6. Write Tests

Create: `tests/SIL.LCModel.Tests/DomainServices/DataMigration/DataMigration7000073Tests.cs`

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
            var dtos = DataMigrationTestServices.ParseProjectFile("DataMigration7000073.xml");
            var mockMdc = new MockMDCForDataMigration();
            IDomainObjectDTORepository dtoRepos = new DomainObjectDtoRepository(
                7000072, dtos, mockMdc, null, TestDirectoryFinder.LcmDirectories);

            m_dataMigrationManager.PerformMigration(dtoRepos, 7000073, new DummyProgressDlg());

            Assert.AreEqual(7000073, dtoRepos.CurrentModelVersion);
            // Add assertions verifying data was transformed correctly
        }
    }
}
```

**Test data file**: Create a minimal `.xml` file with sample `<rt>` elements in the test data directory used by `DataMigrationTestServices.ParseProjectFile()`. Look at existing test data files like `DataMigration7000072.xml` for the expected format.

## Key Rules

- Migrations operate on raw XML strings via `DomainObjectDTO`. You cannot use `ICmObject` or any live LCM API.
- Use `XElement.Parse()` / `.ToString()` for XML manipulation. Do not use string replacement on XML.
- Always call `CheckVersionNumber` first and `IncrementVersionNumber` last.
- Handle null checks: optional elements may not exist in all objects.
- The repository tracks changes automatically. `UpdateDTO` marks as modified. `Add` marks as new. `Remove` marks as deleted.
- Use `AllInstancesWithSubclasses` when the property could be on subclasses too.
- GUIDs in the data are lowercase. Use `.ToLowerInvariant()` when comparing.
- For large migrations, organize logic into private helper methods (see `DataMigration7000072.cs` for this pattern).

## Checklist

- [ ] Migration class created with correct version number
- [ ] `CheckVersionNumber` called with previous version (N-1)
- [ ] `IncrementVersionNumber` called at the end
- [ ] Migration registered in `LcmDataMigrationManager` constructor
- [ ] `MasterLCModel.xml` version attribute updated
- [ ] Change history entry added to `MasterLCModel.xml`
- [ ] Test class created extending `DataMigrationTestsBase`
- [ ] Test data XML file created
- [ ] Build succeeds
