---
name: adding-a-virtual-property
description: Add a computed or derived virtual property to an LCM class using the VirtualProperty attribute. No model version change, no migration, and no XML editing required. Use when the user asks to add a computed, derived, or virtual property that is not persisted.
---

# Adding a Virtual Property

Virtual properties are computed/derived properties that are NOT persisted in the data store. They are discovered automatically via reflection from the `[VirtualProperty]` attribute. No model version change, no migration, and no XML editing is required.

Use this when you need a property that:
- Computes a value from other persisted data
- Provides a back-reference (e.g., "all senses that reference this semantic domain")
- Exposes a convenience accessor

If the property needs to be persisted, use the `adding-a-property` skill instead.

## Steps

### 1. Choose the Target File

Virtual properties are added to partial class definitions in `src/SIL.LCModel/DomainImpl/`. Find the appropriate `Overrides*.cs` file:

| File | Classes |
|------|---------|
| `OverridesLing_Lex.cs` | LexDb, LexEntry, LexSense, LexEntryRef, LexExampleSentence, etc. |
| `OverridesCellar.cs` | CmObject, CmPossibility, CmSemanticDomain, StText, StPara, etc. |
| `OverridesLing_Wfi.cs` | WfiWordform, WfiAnalysis, WfiGloss, WfiMorphBundle |
| `OverridesLing_MoClasses.cs` | MoForm, MoStemAllomorph, MoAffixAllomorph, MoMorphSynAnalysis, etc. |
| `OverridesLangProj.cs` | LangProject |
| `OverridesLing_Disc.cs` | DsConstChart, ConstChartRow, etc. |
| `OverridesNotebk.cs` | RnGenericRec |

If the class doesn't have a partial class in any of these files yet, add a new `partial class` block to the appropriate file.

### 2. Add the Property

Add a public property with the `[VirtualProperty]` attribute inside the partial class.

**Required imports:**
```csharp
using SIL.LCModel.Core.Cellar;        // CellarPropertyType
using SIL.LCModel.Infrastructure;      // VirtualPropertyAttribute
```

### 3. Choose the Right Pattern

**Simple value type** (Integer, Boolean):
```csharp
[VirtualProperty(CellarPropertyType.Boolean)]
public bool IsSpecialCase
{
    get { return /* computed boolean expression */; }
}
```

**Reference collection** (back-references or computed lists):
```csharp
[VirtualProperty(CellarPropertyType.ReferenceCollection, "LexSense")]
public IEnumerable<ILexSense> RelatedSenses
{
    get
    {
        // Compute and return the collection
        return Services.GetInstance<ILexSenseRepository>()
            .AllInstances()
            .Where(s => /* filter condition */);
    }
}
```

The second parameter to `VirtualProperty` is the **signature** -- the unqualified class name of the target type. Required for all object-type properties (Reference*, Owning*).

**Reference sequence** (ordered list):
```csharp
[VirtualProperty(CellarPropertyType.ReferenceSequence, "LexEntry")]
public IEnumerable<ILexEntry> OrderedEntries
{
    get { return /* computed ordered sequence */; }
}
```

**MultiUnicode** (computed multi-writing-system string):
```csharp
[VirtualProperty(CellarPropertyType.MultiUnicode)]
public IMultiAccessorBase ComputedTitle
{
    get
    {
        if (m_titleFlid == 0)
            m_titleFlid = Cache.MetaDataCache.GetFieldId("ClassName", "ComputedTitle", false);
        return new VirtualStringAccessor(this, m_titleFlid, ComputedTitleForWs);
    }
}
private int m_titleFlid;

private ITsString ComputedTitleForWs(int ws)
{
    // Return a TsString for the given writing system
    return TsStringUtils.MakeString("computed value", ws);
}
```

**Reference atomic** (single computed reference):
```csharp
[VirtualProperty(CellarPropertyType.ReferenceAtomic, "CmPossibility")]
public ICmPossibility ComputedCategory
{
    get { return /* single object or null */; }
}
```

### 4. Property Registration

No registration is needed. The `LcmMetaDataCache` automatically discovers properties with `[VirtualProperty]` via reflection during initialization. FLIDs are auto-assigned starting at 20,000,000.

### 5. Accessing Virtual Properties

**From C# code** -- use the property directly:
```csharp
var senses = semanticDomain.ReferringSenses;
```

**From the SilDataAccess layer** (for views/UI integration):
```csharp
int flid = cache.MetaDataCache.GetFieldId("ClassName", "PropertyName", false);
var value = cache.DomainDataByFlid.get_Prop(obj.Hvo, flid);
```

### 6. Optional: Expose on the Interface

If the virtual property should be accessible via the public interface (e.g., `ILexEntry`), add it to the hand-written partial interface. The generated interfaces are partial, so you can extend them:

```csharp
// In a file like src/SIL.LCModel/ILexEntryExtensions.cs or similar
namespace SIL.LCModel
{
    public partial interface ILexEntry
    {
        IEnumerable<ILexEntry> ComputedProperty { get; }
    }
}
```

Check existing patterns -- many interfaces already have partial extensions.

### 7. Write Tests

Test virtual properties using `MemoryOnlyBackendProviderTestBase`. See the `writing-tests` skill.

## Checklist

- [ ] Property added to the correct partial class in `DomainImpl/Overrides*.cs`
- [ ] `[VirtualProperty]` attribute applied with correct `CellarPropertyType`
- [ ] Signature parameter provided for object-type properties
- [ ] Property is public and read-only (getter only)
- [ ] Interface extended if the property needs to be part of the public API
- [ ] No changes to `MasterLCModel.xml` (virtual properties are not persisted)
- [ ] No data migration needed
- [ ] Tests written
