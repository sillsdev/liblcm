---
name: adding-a-new-class
description: Add an entirely new class to MasterLCModel.xml, including module placement, class numbering, owner property, version bump, data migration, code regeneration, and tests. Use when the user asks to add a new class, entity, or object type to the LCM data model.
---

# Adding a New Class to the Model

This guide covers adding an entirely new class to `MasterLCModel.xml`. This is less common than adding properties and more involved -- it touches the model, requires a migration, may need an owner property on an existing class, and may need hand-written partial class logic.

## Prerequisites

- Read the Critical Rules section in AGENTS.md
- Know the parent (base) class (typically `CmObject` for simple classes)
- Know whether the class requires an owner and which class will own it
- Know the properties the new class needs

## Steps

### 1. Determine Class Placement

Classes live inside `<CellarModule>` elements in `MasterLCModel.xml`. The main modules are:

| Module | id | num | Contains |
|--------|----|-----|----------|
| Cellar | CellarModule | 0 | Core classes (CmObject, CmPossibility, StText, etc.) |
| Scripture | Scripture | 3 | Scripture classes |
| LangProj | LangProj | 6 | LangProject and related |
| Ling | Ling | 5 | Linguistic classes (LexEntry, LexSense, Morph*, Wfi*, etc.) |
| Notebook | Notebook | 24 | Notebook classes |

Choose the module that best fits your class. Most new classes go in `Ling` (module 5).

### 2. Determine the Class Number

Within the module, find the highest existing `num` attribute on `<class>` elements and use the next integer.

The class ID (used in code as `kClassId`) is formed by combining the module number and the class number. For example, in module `5` (Ling), class number `134` would have class ID `5134`.

### 3. Add the Class to MasterLCModel.xml

File: `src/SIL.LCModel/MasterLCModel.xml`

```xml
<class num="134" id="NewClassName" abstract="false" abbr="ncn" base="CmObject" depth="0">
  <comment>
    <para>Description of the new class. No newlines inside para elements.</para>
  </comment>
  <props>
    <basic num="1" id="Name" sig="MultiUnicode"/>
    <basic num="2" id="Description" sig="MultiString"/>
    <!-- Add more properties as needed -->
  </props>
</class>
```

Key attributes:
- `abstract`: Set to `true` if only subclasses should be instantiated
- `base`: Parent class. Use `CmObject` unless inheriting from something more specific
- `depth`: Depth in the inheritance tree from `CmObject` (0 = direct child of CmObject)
- `abbr`: Short abbreviation for the class
- `owner`: `required` (default), `optional`, or `none`. Use `none` for unowned classes like `LexEntry`

### 4. Add an Owning Property to the Owner Class

Unless `owner="none"`, you need a property on the owning class that references the new class. Find the owner class in the XML and add an owning property:

```xml
<owning num="NEXT_NUM" id="NewClassInstances" card="seq" sig="NewClassName">
  <comment>
    <para>Owns instances of NewClassName.</para>
  </comment>
</owning>
```

### 5. Increment the Model Version

Update the `version` attribute on `<EntireModel>` and add a change history entry.

### 6. Write the Data Migration

Create: `src/SIL.LCModel/DomainServices/DataMigration/DataMigration7000073.cs`

For a new class that doesn't exist in any data yet, a minimal migration suffices:

```csharp
using System.Xml.Linq;

namespace SIL.LCModel.DomainServices.DataMigration
{
    internal class DataMigration7000073 : IDataMigration
    {
        public void PerformMigration(IDomainObjectDTORepository repoDto)
        {
            DataMigrationServices.CheckVersionNumber(repoDto, 7000072);
            // New class added to model. No existing data to migrate.
            DataMigrationServices.IncrementVersionNumber(repoDto);
        }
    }
}
```

If the new class needs default instances created (e.g., a new possibility list), create them in the migration using `DataMigrationServices.CreatePossibilityList()` or raw XML construction. See `DataMigration7000069.cs` for examples of creating new lists and objects.

### 7. Register the Migration

File: `src/SIL.LCModel/DomainServices/DataMigration/LcmDataMigrationManager.cs`

```csharp
m_individualMigrations.Add(7000073, new DataMigration7000073());
```

### 8. Rebuild

```
dotnet build --configuration Release
```

The code generator will produce:
- A `NewClassNameTags` constants class (class ID, field IDs)
- An `INewClassName` interface
- An `INewClassNameFactory` factory interface and implementation
- An `INewClassNameRepository` repository interface and implementation
- A concrete `NewClassName` class in `DomainImpl/GeneratedClasses.cs`
- StructureMap registrations in `GeneratedServiceLocatorBootstrapper.cs`

### 9. Add Hand-Written Extensions (if needed)

If the class needs business logic, create a partial class in `src/SIL.LCModel/DomainImpl/`:

```csharp
namespace SIL.LCModel.DomainImpl
{
    internal partial class NewClassName
    {
        // Virtual properties, convenience methods, overrides, etc.
    }
}
```

Place it in the appropriate `Overrides*.cs` file or create a new one if it doesn't fit existing files.

### 10. Update BootstrapNewLanguageProject (if needed)

If the new class needs default instances in every new project, update `src/SIL.LCModel/DomainServices/BootstrapNewLanguageProject.cs` to create them.

### 11. Write Tests

Create migration tests (use the `writing-a-data-migration` skill) and API tests using `MemoryOnlyBackendProviderTestBase` (use the `writing-tests` skill).

## Checklist

- [ ] Class added to correct `<CellarModule>` in `MasterLCModel.xml`
- [ ] Unique `num` within the module
- [ ] `base` class set correctly
- [ ] `owner` attribute set (or left as default `required`)
- [ ] Owning property added to the owner class (unless `owner="none"`)
- [ ] `version` attribute incremented on `<EntireModel>`
- [ ] Change history entry added
- [ ] Migration class created and registered in `LcmDataMigrationManager`
- [ ] Build succeeds and code regenerates correctly
- [ ] Hand-written partial class added if business logic needed
- [ ] `BootstrapNewLanguageProject` updated if default instances needed
- [ ] Tests written
- [ ] Generated files NOT manually edited
