# Architecture self-review: MoAffixProcess.PostClone fix

Companion to `affix-process-split-sense-stale-clone.md`. Written after Phase 1
(reproduction) and Phase 2 (the minimal fix) were done and verified; see that
commit history for the actual change.

## 1. Is `PostClone`-taking-the-whole-copy-map the right API?

No. `void PostClone(Dictionary<int, ICmObject> copyMap)` (`InterfaceAdditions.cs:88`)
hands every implementer the entire batch's source→clone map — every object
cloned in that `CopyObject` pass, across every top-level source object, not
just "your" object's clone. That is exactly the shape that produced defects
(A) and (B): the pre-fix code treated `copyMap.Values` as if it were "the
clones I own" and iterated it directly. The correct operation was always the
one-line lookup `copyMap[Hvo]`; the API affords (and, here, invited) the
wrong one.

A safer signature would pass only this object's own clone:

```csharp
void PostClone(ICmObject clone);
```

`CopyObject` already computes `copyMap[source.Hvo]` at both call sites
(`CopyObject.cs:124`, `:147`) to return the top-level clone to its own
caller, so passing that same value to `PostClone` costs nothing structurally.
If some future implementation genuinely needs sibling clones (I found none
that do — see §3), it can reach them via `clone.Owner`'s owned collections,
which is a bounded, self-scoping walk instead of an unscoped shared map.

Cost to change: the interface has exactly three real implementers today
(`CmObject`'s no-op base, `MoAffixProcess`, and two
`throw new NotImplementedException()` stubs), plus one test double
(`AnalysisAdjusterTests.cs:4079`) that also just throws. Mechanically small.
The real cost is that `ICloneableCmObject`/the `PostClone` member sit on a
public interface shipped in the `SIL.LCModel` package, so narrowing the
signature is a breaking API change for any out-of-tree consumer with a custom
override — it would need a major/minor bump and a changelog note, not just a
patch release. I did not make this change: nothing in the current failing
tests requires it (the bug is fully fixed by scoping the lookup inside the
existing signature), and reshaping a public interface isn't warranted by a
bug fix alone. I recommend it as a follow-up, done deliberately with its own
compatibility review.

## 2. What can be removed or simplified?

`SetDefaultValuesAfterInit` (`OverridesLing_MoClasses.cs:4037-4048`) seeding
every new `MoAffixProcess` with a default `PhVariable` input and
`MoCopyFromInput` output — purely so the Affix Process UI slice has a
non-empty, editable row to show for a brand-new rule (the comment cites
FWR-1619) — and then requiring a clone-time hook to strip that seam back off
is exactly the wrong seam. It couples two files (`:4037` and `:4056`) through
an unenforced shared assumption ("clones always have exactly one leaked
default pair, appended-before-real-content-follows"), with nothing but a
comment to keep them in sync. That coupling is *why* this bug was possible in
the first place, and why fixing it revealed a second, undocumented coupling
(§ below): removing the default input can itself cascade, via
`RemoveObjectSideEffectsInternal`, into removing the default output, so even
"the obvious fix" of unconditionally stripping index 0 from both lists is
wrong without checking whether the cascade already did half the job.

The genuinely categorical removal is to stop seeding defaults during
*cloning* at all — i.e., give `MoAffixProcess` an `ICloneableCmObject`
implementation (§4) so the clone path never calls
`SetDefaultValuesAfterInit` in the first place, and `PostClone`/the
strip-defaults dance disappears entirely. I evaluated this in depth (§4) but
did not implement it, so `SetDefaultValuesAfterInit` and the (now-correct)
`PostClone` both remain. I did not find any other dead code the minimal fix
could remove; the fix is a same-size rewrite of one method body.

## 3. Did I find the same defect pattern elsewhat in other `PostClone` implementations?

No. A full-repo search for `PostClone` turns up only:

- `CmObject.PostClone` (`DomainImpl/CmObject.cs:606`) — the base virtual,
  a correct no-op ("up to subclasses to override").
- `MoAffixProcess.PostClone` — the one fixed here.
- `DomainObjectServices.cs:1398` and
  `Application/ApplicationServices/SingleLexReference.cs:358` — both
  `throw new NotImplementedException()`. Neither iterates `copyMap`, so
  neither has the return-instead-of-continue or whole-map-strip defects;
  they simply never implemented the hook (their objects presumably never go
  through `CopyObject`'s generic path in practice). Left unchanged — that is
  out of this bug's scope, and I have no evidence they're ever reached.
- `AnalysisAdjusterTests.cs:4079` — a test double, also just throws.

## 4. What did I NOT fix, and why?

- **The `PostClone` signature** (§1) — recommended, not executed. No failing
  test requires it; it's a public API change that deserves its own
  versioning decision, not a rider on a bug fix.

- **`ICloneableCmObject.SetCloneProperties` for `MoAffixProcess`** — the
  "deeper, categorical" option the bug doc raised as worth considering. I
  looked hard at this and chose not to implement it:

  `PhRegularRule.SetCloneProperties` (`OverridesLing_Lex.cs:7683-7701`) is
  not a safe template to copy verbatim, because it clones two owned lists
  (`RightHandSidesOS`, `StrucDescOS`) that don't reference each other via a
  plain object reference — their only sharing is through
  `PhFeatureConstraint`, which is a deliberately-shared, deliberately
  *not*-remapped pooled reference (same object, same identity, in both the
  original and the clone; see `DuplicateRegularRule_SharedConstraintSurvivesUntilLastRuleDeleted`
  in `LingTests.cs`). `MoAffixProcess` is a harder case: `MoCopyFromInput`
  and `MoModifyFromInput` in `OutputOS` have a `ContentRA` that *must* be
  re-targeted at the clone's own `InputOS` — that's not optional sharing,
  it's the entire meaning of the rule. `SetCloneProperties` bypasses
  `CopyObject`'s own reference-remap pass entirely (short-circuited at
  `CopyObject.cs:169-170` and `:337-342`), so a correct implementation would
  have to:
  1. Clone `InputOS` itself, building a `Hvo → clone` map by hand.
  2. Clone `OutputOS`, then walk it re-targeting `ContentRA` through that
     map for exactly the two `MoRuleMapping` subclasses whose `Content`
     targets `InputOS` (`MoCopyFromInput`, `MoModifyFromInput`) — while
     leaving the other two (`MoInsertPhones.Content` → `PhTerminalUnit`,
     `MoInsertNC.Content` → `PhNaturalClass`, both shared phonological-
     inventory references, confirmed from `MasterLCModel.xml:4002-4025`)
     untouched, since those must **not** be remapped.

  That's a correct, buildable design (sketched, not written), but it
  roughly doubles this class's clone-handling code and adds edge cases
  (null `ContentRA`, `MoModifyFromInput.ModificationRA`, making sure the
  hand-rolled switch is exhaustive over `MoRuleMapping` subclasses now and
  in the future) that I have not written dedicated tests for. The task's
  own bar — implement only if tests can prove it correct — argues against
  landing it now: the minimal fix already makes all 5 reproduction tests
  and the full 1732-test suite pass with zero regressions, so nothing
  currently broken demands the larger change. Doing it without tests for
  those edge cases would be "half-doing it." I'm recommending it as a
  well-scoped follow-up, not doing it here.

- **`SetDefaultValuesAfterInit` itself** — left as-is. It's still needed for
  genuinely-new, user-created processes (FWR-1619); only the clone side
  changed.

## 5. What still needs verification in a running FLEx?

This repo's tests (including the new save/reload round trip) prove the LCM
domain data is correct in memory and after a real XML-backend reload. Two
things are outside this repo's reach:

1. **UI redraw.** Does the FieldWorks Affix Process slice actually reflect
   the corrected `InputOS`/`OutputOS` immediately after
   `LexEntry.MoveSenseToCopy`, without requiring a manual refresh? This
   repo can't exercise `Src/xWorks`/`Src/FdoUi`. Decisive evidence: in a
   FLEx build against this fix, create an affix-process rule with 2+ real
   inputs/outputs, use "Move Sense to a New Entry," and check the new
   entry's Affix Process slice (a) immediately, (b) after navigating away
   and back, and (c) after closing and reopening the project — all three
   should show identical, correct content. Phase 1 found no "right then
   wrong later" timing effect at the LCM level (the corruption, when
   present, is there from the moment of cloning, and reload merely
   persists it unchanged) — so if the live-FLEx symptom really is
   "right at first, wrong later," that timing effect must come from
   somewhere in the UI/caching layer above LCM, not from `PostClone`. That
   would be worth chasing down as a separate investigation if reproduced.
2. **Packaging.** Per the bug doc's Scope section, FieldWorks needs a
   `liblcm` package bump to pick up this fix at all; that step is outside
   this worktree.
