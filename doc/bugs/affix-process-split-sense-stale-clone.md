# Bug 3 — Affix process rule is wrong in the copy after "Move Sense to a New Entry"

**Area:** Lexicon → Move Sense to a New Entry; LCM `CopyObject` / `MoAffixProcess` cloning
**Type:** Data corruption on clone
**Repos:** `FieldWorks` (command wiring) and `liblcm` (the defect)

## Symptom as reported

Edit an affix process rule on an entry, then split a sense off into a new entry. Immediately afterwards both entries appear to hold the updated rule. Returning to them later, one holds the old version. The reporter's read is a save / copy / live-state problem.

## The path

1. `CmdDataTree-Split-Sense` (`DistFiles/Language Explorer/Configuration/Lexicon/DataTreeInclude.xml:165`), message `DataTreeSplit`.
2. `DTMenuHandler.OnDataTreeSplit` (`Src/xWorks/DTMenuHandler.cs:1052-1058`) → `Slice.HandleSplitCommand()`.
3. `LexSenseUi.MoveUnderlyingObjectToCopyOfOwner` (`Src/FdoUi/FdoUiCore.cs:2093-2106`) → `ILexEntry.MoveSenseToCopy`.
4. `LexEntry.MoveSenseToCopy` (`liblcm/src/SIL.LCModel/DomainImpl/OverridesLing_Lex.cs:1652`) creates the new entry and deep-copies the allomorphs:
   - `OverridesLing_Lex.cs:1670` — `CopyObject<IMoForm>.CloneLcmObject(LexemeFormOA, ...)`
   - `OverridesLing_Lex.cs:1672` — `CopyObject<IMoForm>.CloneLcmObjects(AlternateFormsOS, ...)`

An affix process rule is a `MoAffixProcess`, a `MoForm` subclass, so it is cloned by step 4 through the generic reflection-based `CopyObject`.

## Root cause: `MoAffixProcess.PostClone` is broken

`MoAffixProcess` does **not** implement `ICloneableCmObject` — unlike `PhRegularRule` and `PhMetathesisRule`, which have hand-written `SetCloneProperties` implementations (`OverridesLing_Lex.cs:7683` and `:8176`). So it goes through generic reflection cloning and then relies on a `PostClone` hook to repair the result.

The hook exists because `MoAffixProcess.SetDefaultValuesAfterInit` (`OverridesLing_MoClasses.cs:4037-4048`) seeds every newly created affix process with a default `PhVariable` in `InputOS` and a default `MoCopyFromInput` in `OutputOS`. `CopyObject` creates the clone through the normal factory (`CopyObject.cs:301-373`), so the clone gets those defaults, and then `HandleObjFlid` (`CopyObject.cs:582-595`) appends the cloned real inputs and outputs after them. `PostClone` is supposed to strip the two defaults back off:

```csharp
public override void PostClone(Dictionary<int, ICmObject> copyMap)
{
    foreach (var cmObject in copyMap.Values)
    {
        var clonedProcess = cmObject as IMoAffixProcess;
        if (clonedProcess == null)
            return;                                  // <-- (A)
        if (clonedProcess.InputOS.Count > 1)
            clonedProcess.InputOS.RemoveAt(0);        // <-- (B)
        if (clonedProcess.OutputOS.Count > 1)
            clonedProcess.OutputOS.RemoveAt(0);
    }
}
```
`liblcm/src/SIL.LCModel/DomainImpl/OverridesLing_MoClasses.cs:4056-4068`

Three defects, all CONFIRMED by reading:

### (A) `return` where `continue` was meant — line 4061-4062

`copyMap` is `CopyObject.m_sourceToCopyMap` (`CopyObject.cs:44`), which holds **every** object cloned in the pass: the affix process, all of its `PhVariable` / `PhSimpleContext*` inputs, all of its `MoRuleMapping` outputs, and — because `MoveSenseToCopy` clones the whole of `AlternateFormsOS` in one batch (`CopyObject.cs:108-128`) — every object cloned from every sibling allomorph too.

The loop bails out entirely at the first value that is not an `IMoAffixProcess`. Unless the affix process happens to be the very first entry, **the cleanup never runs**, and the clone keeps the default `PhVariable` input and default `MoCopyFromInput` output prepended to the real content. A `PhVariable` + `MoCopyFromInput` pair is precisely what an untouched, freshly created affix process rule looks like — which is a strong candidate for what the reporter is seeing as "the old version".

### (B) Repeated `RemoveAt(0)` deletes real content

`CopyObject` calls `PostClone` once per top-level source object (`CopyObject.cs:123-124`). If the entry has two or more affix-process allomorphs, `PostClone` fires once per affix process — and because it iterates all of `copyMap`, **each call strips index 0 from every cloned affix process**. The first call removes the defaults; the second call removes the first *real* input and output. With N affix processes on an entry, N-1 real leading input/output pairs are silently destroyed.

### (C) The map is not scoped to the object being repaired

Even if (A) and (B) are fixed, iterating the whole shared copy map is the wrong shape for this hook. `PostClone` should operate on this object's own clone, looked up as `copyMap[this.Hvo]`.

## Confidence and what is not yet proven

The three defects above are read directly from the code and are not in doubt.

What is **not** proven is that they produce the exact reported sequence — right in both entries at first, wrong in one on return. A prepended default input/output would normally be visible immediately. Two mechanisms could explain the delay, and neither has been verified:

- The affix process slice may render a leading `PhVariable` / `MoCopyFromInput` pair invisibly or identically to the correct display until the view is rebuilt from a reload.
- `MoCopyFromInput.ContentRA` / `MoModifyFromInput.ContentRA` reference a `PhContextOrVar` owned by the same rule's `InputOS`. `CopyObject` remaps such intra-copy references in pass 2 (`CopyObject.cs:203-238`), but if defect (B) has removed the referenced input, the surviving mapping points at a deleted or wrong context — which can render plausibly in a warm cache and differently after reload.

**The decisive next step is a repro plus a diff of the `.fwdata` XML before and after the split**, comparing the source and cloned `MoAffixProcess` element trees. That will show immediately whether the clone carries an extra leading input/output, is missing one, or has a mis-targeted `ContentRA`.

## Proposed fix

In `liblcm`, `OverridesLing_MoClasses.cs:4056-4068`:

```csharp
public override void PostClone(Dictionary<int, ICmObject> copyMap)
{
    if (!copyMap.TryGetValue(Hvo, out var clone) || !(clone is IMoAffixProcess clonedProcess))
        return;
    if (clonedProcess.InputOS.Count > 1)
        clonedProcess.InputOS.RemoveAt(0);
    if (clonedProcess.OutputOS.Count > 1)
        clonedProcess.OutputOS.RemoveAt(0);
}
```

This fixes (A), (B) and (C) together: each source affix process repairs exactly its own clone, exactly once.

Worth considering as a follow-up, not required for the fix: give `MoAffixProcess` a proper `ICloneableCmObject.SetCloneProperties` implementation, matching `PhRegularRule` (`OverridesLing_Lex.cs:7683`). That removes the create-defaults-then-strip-them dance entirely, at the cost of hand-maintaining the property copy. `SetCloneProperties` short-circuits both clone passes (`CopyObject.cs:169-170` and `:337-342`), so any such implementation must handle the `InputOS` → `OutputOS` `ContentRA` remapping itself.

## Test plan

Unit tests in `liblcm/tests/SIL.LCModel.Tests/DomainImpl/LexEntryTests.cs` (which already covers `MoveSenseToCopy`):

1. Entry with one affix-process allomorph with a non-trivial rule → split a sense → assert the clone's `InputOS` and `OutputOS` match the source exactly, with no leading default `PhVariable` / `MoCopyFromInput`.
2. Entry with a stem allomorph **ordered before** an affix-process allomorph in `AlternateFormsOS` → split → same assertion. This is the case defect (A) breaks.
3. Entry with **two** affix-process allomorphs → split → assert neither clone has lost its first real input/output. This is the case defect (B) breaks.
4. Assert every cloned `MoCopyFromInput` / `MoModifyFromInput` `ContentRA` points into the **clone's** `InputOS`, never the source's.
5. A round-trip test: split, save, reload, re-read the clone. This is the one that speaks to the reported "comes back wrong later" symptom, and should be written even if the in-memory assertions pass.

## Scope

Independent of Bugs 1 and 2. Those are FieldWorks UI defects in the rule formula editor; this is an LCM domain-layer defect in the clone path. The fix lands in `liblcm` and will need a package bump in FieldWorks to ship.

## Key files

| Path:line | Role |
|---|---|
| `liblcm/.../DomainImpl/OverridesLing_MoClasses.cs:4056-4068` | The broken `PostClone` |
| `liblcm/.../DomainImpl/OverridesLing_MoClasses.cs:4037-4048` | `SetDefaultValuesAfterInit` — the defaults that need stripping |
| `liblcm/.../DomainImpl/OverridesLing_Lex.cs:1652-1675` | `MoveSenseToCopy`, allomorph cloning |
| `liblcm/.../DomainServices/CopyObject.cs:108-128` | Batch clone; `PostClone` invoked once per top-level source |
| `liblcm/.../DomainServices/CopyObject.cs:301-373` | Pass 1, owned clone; `ICloneableCmObject` short-circuit |
| `liblcm/.../DomainServices/CopyObject.cs:203-238` | Pass 2, reference remapping |
| `liblcm/.../DomainImpl/OverridesLing_Lex.cs:7683`, `:8176` | `PhRegularRule` / `PhMetathesisRule` — the pattern `MoAffixProcess` lacks |
| `FieldWorks/Src/FdoUi/FdoUiCore.cs:2093-2106` | UI entry point |
| `FieldWorks/Src/xWorks/DTMenuHandler.cs:1052-1058` | Command handler |
