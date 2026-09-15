// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Linq;
using NUnit.Framework;
using SIL.LCModel.DomainServices;

namespace SIL.LCModel.DomainImpl
{
	/// <summary>
	/// LT-22575 (reopened): editing one copy of a duplicated phonological rule must not change the
	/// other copy.
	///
	/// A regular rule owns the single object in each environment slot, but as soon as a slot holds
	/// two or more items the slot owns a PhSequenceContext whose members live in
	/// PhPhonData.ContextsOS and are only referenced. CopyObject clones owned objects and keeps
	/// references to anything it did not clone, so a duplicate's sequence references the same pooled
	/// members as the original. The FieldWorks rule slice deletes an environment member by removing
	/// it from the pool (RuleFormulaControl.ProcessIndicesSeqContext), which takes it out of every
	/// sequence that referenced it.
	///
	/// The "pins current behaviour" tests document the sharing so the document describing it stays
	/// honest; the "invariant" tests state what the user expects and fail until the slice (or the
	/// clone) is fixed. If the fix chooses to deep-copy pooled members on duplicate, invert the two
	/// sharing tests.
	/// </summary>
	[TestFixture]
	public class PhRegularRuleDuplicateTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		private IPhPhonData m_phData;

		public override void TestSetup()
		{
			base.TestSetup();
			m_phData = Cache.LangProject.PhonologicalDataOA;
		}

		#region helpers that mirror the FieldWorks slice and record clerk

		private IPhRegularRule MakeRule()
		{
			var rule = Cache.ServiceLocator.GetInstance<IPhRegularRuleFactory>().Create();
			m_phData.PhonRulesOS.Add(rule);
			return rule;
		}

		private IPhSimpleContextNC MakeOwnedNC()
		{
			return Cache.ServiceLocator.GetInstance<IPhSimpleContextNCFactory>().Create();
		}

		/// <summary>
		/// A member of a multi-item environment: owned by the pool, referenced by the sequence.
		/// </summary>
		private IPhSimpleContextNC MakePooledNC()
		{
			var ctxt = MakeOwnedNC();
			m_phData.ContextsOS.Add(ctxt);
			return ctxt;
		}

		/// <summary>
		/// Builds an environment slot the way RegRuleFormulaControl.CreateSeqCtxt leaves it after the
		/// second insert: the slot owns a PhSequenceContext, the members are pooled.
		/// </summary>
		private IPhSequenceContext MakeSequenceEnvironment(Action<IPhSequenceContext> attachToSlot, params IPhPhonContext[] members)
		{
			var seq = Cache.ServiceLocator.GetInstance<IPhSequenceContextFactory>().Create();
			attachToSlot(seq);
			foreach (var member in members)
				seq.MembersRS.Add(member);
			return seq;
		}

		/// <summary>
		/// RecordClerk.OnDuplicateSelectedItem creates a new rule and calls SetCloneProperties;
		/// CopyObject.CloneLcmObject does the same for an ICloneableCmObject.
		/// </summary>
		private IPhRegularRule Duplicate(IPhRegularRule rule)
		{
			return (IPhRegularRule)CopyObject<IPhSegmentRule>.CloneLcmObject(rule, x => m_phData.PhonRulesOS.Add(x));
		}

		/// <summary>
		/// What Delete/Backspace does on a member of a multi-item environment
		/// (RuleFormulaControl.RemoveContextsFrom → ProcessIndicesSeqContext).
		/// </summary>
		private void UiDeleteSequenceMember(IPhPhonContext member)
		{
			member.PreRemovalSideEffects();
			m_phData.ContextsOS.Remove(member);
		}

		/// <summary>
		/// What Delete/Backspace does on a single-item environment
		/// (RegRuleFormulaControl.RemoveItems, non-sequence branch).
		/// </summary>
		private static void UiDeleteSingleEnvironment(IPhSegRuleRHS rhs, bool left)
		{
			var ctxt = left ? rhs.LeftContextOA : rhs.RightContextOA;
			ctxt.PreRemovalSideEffects();
			if (left)
				rhs.LeftContextOA = null;
			else
				rhs.RightContextOA = null;
		}

		#endregion

		#region pins current behaviour

		/// <summary>
		/// The duplicate's environment sequence is a new owned object, but its members are the
		/// original's pooled contexts: nothing new appears in PhPhonData.ContextsOS.
		/// </summary>
		[Test]
		public void Duplicate_SequenceEnvironmentSharesPooledMembersWithOriginal()
		{
			var rule = MakeRule();
			var rhs = rule.RightHandSidesOS[0];
			var b1 = MakePooledNC();
			var b2 = MakePooledNC();
			var seq = MakeSequenceEnvironment(s => rhs.RightContextOA = s, b1, b2);

			var clone = Duplicate(rule);
			var cloneSeq = clone.RightHandSidesOS[0].RightContextOA as IPhSequenceContext;

			Assert.That(cloneSeq, Is.Not.Null.And.Not.EqualTo(seq), "the sequence itself is owned and therefore cloned");
			Assert.That(cloneSeq.MembersRS.ToArray(), Is.EqualTo(new IPhPhonContext[] { b1, b2 }),
				"the members are pooled and therefore shared");
			Assert.That(m_phData.ContextsOS.Count, Is.EqualTo(2), "no pooled member was copied");
		}

		/// <summary>
		/// The counterpart: a single-item environment is owned by the RHS, so it is cloned.
		/// This is why deleting a one-item environment in a duplicate is safe today.
		/// </summary>
		[Test]
		public void Duplicate_SingleItemEnvironmentIsCopied()
		{
			var rule = MakeRule();
			var rhs = rule.RightHandSidesOS[0];
			rhs.LeftContextOA = MakeOwnedNC();

			var clone = Duplicate(rule);
			var cloneRhs = clone.RightHandSidesOS[0];

			Assert.That(cloneRhs.LeftContextOA, Is.Not.Null.And.Not.EqualTo(rhs.LeftContextOA));
			Assert.That(cloneRhs.LeftContextOA.Owner, Is.EqualTo(cloneRhs));
			Assert.That(rhs.LeftContextOA.Owner, Is.EqualTo(rhs));
		}

		#endregion

		#region invariants the user expects

		/// <summary>
		/// The reopened LT-22575 report: in a duplicate of "x → y / a _ b", deleting b removes it from
		/// the original as well, when b is one item of a multi-item right environment.
		/// </summary>
		[Test]
		public void DeletingMemberOfDuplicateRightEnvironment_LeavesOriginalIntact()
		{
			var rule = MakeRule();
			var rhs = rule.RightHandSidesOS[0];
			var b1 = MakePooledNC();
			var b2 = MakePooledNC();
			MakeSequenceEnvironment(s => rhs.RightContextOA = s, b1, b2);

			var clone = Duplicate(rule);
			var cloneSeq = (IPhSequenceContext)clone.RightHandSidesOS[0].RightContextOA;

			UiDeleteSequenceMember(cloneSeq.MembersRS[1]);

			Assert.That(cloneSeq.MembersRS.Count, Is.EqualTo(1), "the duplicate lost the deleted member");
			var originalSeq = rhs.RightContextOA as IPhSequenceContext;
			Assert.That(originalSeq, Is.Not.Null, "the original's right environment must still be a sequence");
			Assert.That(originalSeq.MembersRS.Count, Is.EqualTo(2), "the original's right environment must keep both members");
			Assert.That(b1.IsValidObject && b2.IsValidObject, Is.True, "the original's members must still exist");
		}

		/// <summary>
		/// Same defect on the left environment.
		/// </summary>
		[Test]
		public void DeletingMemberOfDuplicateLeftEnvironment_LeavesOriginalIntact()
		{
			var rule = MakeRule();
			var rhs = rule.RightHandSidesOS[0];
			var a1 = MakePooledNC();
			var a2 = MakePooledNC();
			MakeSequenceEnvironment(s => rhs.LeftContextOA = s, a1, a2);

			var clone = Duplicate(rule);
			var cloneSeq = (IPhSequenceContext)clone.RightHandSidesOS[0].LeftContextOA;

			UiDeleteSequenceMember(cloneSeq.MembersRS[0]);

			var originalSeq = rhs.LeftContextOA as IPhSequenceContext;
			Assert.That(originalSeq, Is.Not.Null, "the original's left environment must still be a sequence");
			Assert.That(originalSeq.MembersRS.Count, Is.EqualTo(2), "the original's left environment must keep both members");
		}

		/// <summary>
		/// Clearing a duplicate's environment one item at a time must not empty the original's
		/// sequence; an emptied sequence nulls the original's slot
		/// (PhSequenceContext.RemoveObjectSideEffectsInternal), which is the whole environment gone.
		/// </summary>
		[Test]
		public void DeletingAllMembersOfDuplicateEnvironment_DoesNotEmptyOriginalEnvironment()
		{
			var rule = MakeRule();
			var rhs = rule.RightHandSidesOS[0];
			var b1 = MakePooledNC();
			var b2 = MakePooledNC();
			MakeSequenceEnvironment(s => rhs.RightContextOA = s, b1, b2);

			var clone = Duplicate(rule);
			var cloneRhs = clone.RightHandSidesOS[0];

			UiDeleteSequenceMember(((IPhSequenceContext)cloneRhs.RightContextOA).MembersRS[1]);
			UiDeleteSequenceMember(((IPhSequenceContext)cloneRhs.RightContextOA).MembersRS[0]);

			Assert.That(cloneRhs.RightContextOA, Is.Null, "the duplicate's environment is empty, as the user asked");
			Assert.That(rhs.RightContextOA, Is.Not.Null, "the original's right environment must survive");
			Assert.That(((IPhSequenceContext)rhs.RightContextOA).MembersRS.Count, Is.EqualTo(2));
		}

		/// <summary>
		/// The case the reporter said still worked: a one-item environment is owned, so deleting it
		/// in the duplicate is local to the duplicate.
		/// </summary>
		[Test]
		public void DeletingSingleItemEnvironmentOfDuplicate_LeavesOriginalIntact()
		{
			var rule = MakeRule();
			var rhs = rule.RightHandSidesOS[0];
			rhs.LeftContextOA = MakeOwnedNC();
			var original = rhs.LeftContextOA;

			var clone = Duplicate(rule);
			UiDeleteSingleEnvironment(clone.RightHandSidesOS[0], left: true);

			Assert.That(clone.RightHandSidesOS[0].LeftContextOA, Is.Null);
			Assert.That(rhs.LeftContextOA, Is.EqualTo(original));
			Assert.That(original.IsValidObject, Is.True);
		}

		#endregion
	}
}
