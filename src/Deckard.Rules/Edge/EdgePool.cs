using System;

namespace Deckard.Rules.Edge;

/// <summary>
/// A character's spendable Edge pool: the running total of Edge points a character can
/// spend, distinct from <see cref="Deckard.Data.Attributes.CharacterAttribute.Edge"/>
/// (Issue #64), which is only a vocabulary tag identifying "the Edge attribute slot" and
/// carries no rank of its own. Every operation here that needs the character's Edge rank
/// takes it as a plain <see langword="int"/> parameter -- the same posture
/// <see cref="Attributes.ConditionMonitor"/> already takes toward Body and Willpower
/// ranks (Issue #64): this type does not store, look up, or compute a rank, it only
/// consumes one where the book's Edge-economy rules require it. A future character-state
/// type is what will own "this character's <see cref="Deckard.Data.Attributes.CharacterAttribute.Edge"/>
/// rank is N" and feed that N into these methods.
///
/// SR6 Core / Game Concepts / Edge / printed pp. 45-48 / PDF pp. 46-49 is the source for
/// this type. Each member below cites the exact page it comes from rather than repeating
/// one blanket range, because the rules it encodes are split across three separate pages
/// (45, 46 and 48).
///
/// - Session start, the round cap, the hold cap and the confrontation reset are all on
///   printed p. 45 / PDF p. 46 -- see <see cref="StartSession"/>, <see cref="RoundGainCap"/>,
///   <see cref="HoldCap"/> and <see cref="EndConfrontation"/> for each one's exact wording.
/// - Spending Edge, as a generic pool operation, is printed p. 46 / PDF p. 47 -- see
///   <see cref="Spend"/>. What a specific Edge Boost costs and buys is next Issue's Edge
///   Actions/Boosts menu, not this type.
/// - Burning Edge is printed p. 48 / PDF p. 49 -- see <see cref="Burn"/>.
///
/// Two distinct units of time are named in that source and deliberately modeled as two
/// distinct, explicit state transitions rather than inferred from one another:
///
/// - "a combat round" scopes the bonus-gain cap (printed p. 45 / PDF p. 46: the cap
///   applies only "in a combat round"). This packet never defines what makes one combat
///   round end and the next begin (initiative, turns -- Combat-phase machinery this Issue
///   does not build), so <see cref="BeginCombatRound"/> and <see cref="EndCombatRound"/>
///   are opaque boundary markers a future combat system calls; this type assumes nothing
///   about how long a round lasts or what causes one to end, only that "a round is now in
///   progress" / "a round is no longer in progress" is asserted to it explicitly. See ADR
///   0008 (docs/decisions/0008-round-cap-scoped-to-combat-rounds.md) for why the cap does
///   not apply at all while no round is in progress, rather than applying unconditionally.
/// - "any ongoing confrontation" (combat, hacking, social persuasion, "any situation
///   where bonus Edge might be accumulated" -- printed p. 45 / PDF p. 46) scopes the
///   reset-to-attribute rule, and is modeled the same way: <see cref="EndConfrontation"/>
///   is called explicitly by whatever future system decides a confrontation has ended.
///   This transition is deliberately independent of <see cref="BeginCombatRound"/> /
///   <see cref="EndCombatRound"/> -- nothing in the source says ending a confrontation is
///   the same event as ending a combat round, and folding one into the other would be an
///   assumption this type is not entitled to make. A caller is expected to call
///   <see cref="EndCombatRound"/> itself once a combat round (if any) is no longer current.
///
/// Deliberately not modeled here, with reasoning kept alongside the code that would
/// otherwise silently omit it:
///
/// - The Attack/Defense Rating comparison gain and the Social Edge table gain (both
///   printed p. 45 / PDF p. 46) are separate types -- <see cref="AttackDefenseRatingEdgeGain"/>
///   and <see cref="SocialSituationEdgeGain"/> -- because they are triggered by machinery
///   (combat, social resolution) this Issue does not build; both ultimately feed
///   <see cref="GainBonusPoint"/> once a caller has decided a point is earned.
/// - Burning Edge's two named uses, Smackdown and Not Dead Yet (printed p. 48 / PDF
///   p. 49), are separate types too -- <c>Smackdown</c> and <c>NotDeadYet</c> in
///   <c>BurningEdgeUses.cs</c> -- for the same reason: what a burn *costs* is this type's
///   job (<see cref="Burn"/>); what it *does* needs test-resolution and damage/death
///   machinery this Issue does not build.
/// - Gear, spells and qualities granting or cancelling Edge (printed p. 45 / PDF p. 46)
///   name a category of Edge source, not a formula: no gear/spell/quality catalog exists
///   yet (that is <c>Deckard.Data</c> content for a future Issue), and the primitive such
///   an item would eventually call is already <see cref="GainBonusPoint"/> or
///   <see cref="Spend"/> -- there is no distinct algorithm here to stub.
/// - The gamemaster's roleplaying-award bonus point (printed p. 45 / PDF p. 46) is, once
///   awarded, an ordinary <see cref="GainBonusPoint"/> call -- the decision of *when* to
///   award it is a gamemaster judgment call with no formula, not something a type here
///   resolves.
/// - "Preventing Edge Abuse" (printed p. 45 / PDF p. 46) is gamemaster guidance with no
///   formula or number attached -- not a mechanic an engine call site can express at all,
///   so there is nothing here to leave unresolved.
/// - The same-roll spending restriction (printed p. 45 / PDF p. 46: Edge spent across
///   multiple boosts on one test must all apply to that same roll) is a constraint on
///   *which* roll a Boost's effect attaches to, not on the pool amount -- Edge Actions
///   /Boosts territory, next Issue.
/// - "Give ally 1 Edge" (a 2-Edge Boost, printed p. 46 / PDF p. 47: spend 2 of your own
///   Edge, a teammate gains 1 -- a destructive 2-for-1 transfer) composes cleanly out of
///   this type's own primitives (<see cref="Spend"/> on the giver, <see cref="GainBonusPoint"/>
///   on the receiver) but is not a dedicated method here: it is textually one of the
///   printed Edge Boosts, so deciding when it applies and to whom is Edge Actions/Boosts
///   territory like every other boost, not this Issue's pool economy.
/// - Buying burned Edge rank back with Karma (printed p. 48 / PDF p. 49) needs a Karma/
///   character-advancement system this Issue does not build.
/// </summary>
public sealed class EdgePool
{
    /// <summary>
    /// The bonus-gain cap: no more than this many bonus Edge points may be gained in a
    /// single combat round. SR6 Core / Game Concepts / Edge / printed p. 45 / PDF p. 46.
    /// </summary>
    public const int RoundGainCap = 2;

    /// <summary>
    /// The hold cap: total current Edge -- including the Edge provided by the character's
    /// Edge attribute -- may not be carried over or accumulated past this. SR6 Core /
    /// Game Concepts / Edge / printed p. 45 / PDF p. 46.
    /// </summary>
    public const int HoldCap = 7;

    /// <summary>The character's current spendable Edge points.</summary>
    public int Current { get; }

    /// <summary>
    /// Bonus Edge points gained since the last <see cref="BeginCombatRound"/> call, for
    /// enforcing <see cref="RoundGainCap"/> -- or <see langword="null"/> when no combat
    /// round is currently in progress, in which case <see cref="RoundGainCap"/> does not
    /// apply at all (ADR 0008). Not part of any printed vocabulary -- it is this type's
    /// own bookkeeping for a cap the book states but does not name a counter for.
    /// </summary>
    public int? BonusGainedThisRound { get; }

    private EdgePool(int current, int? bonusGainedThisRound)
    {
        Current = current;
        BonusGainedThisRound = bonusGainedThisRound;
    }

    /// <summary>
    /// A character starts a gaming session with Edge points equal to their Edge rank. SR6
    /// Core / Game Concepts / Edge / printed p. 45 / PDF p. 46. No combat round is in
    /// progress yet, so <see cref="BonusGainedThisRound"/> starts <see langword="null"/>.
    ///
    /// This does not clip <paramref name="edgeRank"/> to <see cref="HoldCap"/> even
    /// though that value becomes <see cref="Current"/> directly. The source states the
    /// hold cap as something Edge "can be carried over and accumulated up to" -- the
    /// accumulation process <see cref="GainBonusPoint"/> governs -- and separately states
    /// this starting assignment with no ceiling attached to it. Whether that reading also
    /// holds for a rank that is itself above 7 is not pinned by a test (see
    /// <c>EdgePoolTests</c>): a rank's own valid range is Character Creation content,
    /// outside both this Issue's packet and its scope, the same reasoning
    /// <see cref="Attributes.ConditionMonitor"/> already applies to Body and Willpower.
    /// Should a rank above 7 ever occur, the pool simply starts above <see cref="HoldCap"/>
    /// and <see cref="GainBonusPoint"/> still behaves correctly -- it already refuses to
    /// gain further once <see cref="Current"/> is at or above <see cref="HoldCap"/>,
    /// whatever put it there.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edgeRank"/> is negative.</exception>
    public static EdgePool StartSession(int edgeRank)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(edgeRank);
        return new EdgePool(edgeRank, null);
    }

    /// <summary>
    /// Attempts to gain one bonus point of Edge -- the only amount any printed gain
    /// source in this packet ever names ("a point of Edge"), so this deliberately takes
    /// no amount parameter to generalize beyond what the source states. Refused, with
    /// <see cref="Current"/> and <see cref="BonusGainedThisRound"/> unchanged, once
    /// <see cref="HoldCap"/> is already at its boundary, or once <see cref="RoundGainCap"/>
    /// is already at its boundary <i>and</i> a combat round is in progress (ADR 0008: the
    /// round cap does not apply at all outside a combat round). Both caps are checked
    /// against their exact boundary value (<c>&gt;=</c>, not <c>&gt;</c>): a point that
    /// would land exactly on <see cref="RoundGainCap"/> or <see cref="HoldCap"/> is still
    /// granted, only the next one past it is refused.
    /// </summary>
    public EdgeGainResult GainBonusPoint()
    {
        bool roundCapped = BonusGainedThisRound is int gainedThisRound && gainedThisRound >= RoundGainCap;
        bool holdCapped = Current >= HoldCap;
        bool granted = !roundCapped && !holdCapped;

        EdgePool poolAfter = granted
            ? new EdgePool(Current + 1, BonusGainedThisRound is int n ? n + 1 : null)
            : this;

        return new EdgeGainResult(granted, roundCapped, holdCapped, this, poolAfter);
    }

    /// <summary>
    /// Marks the start of a new combat round: <see cref="BonusGainedThisRound"/> becomes
    /// 0, and <see cref="RoundGainCap"/> begins applying to <see cref="GainBonusPoint"/>
    /// calls. See the type doc's "two distinct units of time" section and ADR 0008 for
    /// why this is an explicit, opaque transition rather than a round identifier this
    /// type compares, and why the cap does not apply before this is ever called.
    /// </summary>
    public EdgePool BeginCombatRound() => new(Current, 0);

    /// <summary>
    /// Marks that a combat round is no longer in progress: <see cref="BonusGainedThisRound"/>
    /// becomes <see langword="null"/>, and <see cref="RoundGainCap"/> stops applying to
    /// <see cref="GainBonusPoint"/> calls until the next <see cref="BeginCombatRound"/>.
    /// See ADR 0008: the book states no bonus-gain limit for a confrontation that is not
    /// currently in a combat round (a purely social or hacking exchange, or the gap
    /// between two combats), so this type must not keep enforcing one.
    /// </summary>
    public EdgePool EndCombatRound() => new(Current, null);

    /// <summary>
    /// Spends <paramref name="amount"/> Edge from the pool. Refused (<see cref="Current"/>
    /// unchanged) when <paramref name="amount"/> exceeds <see cref="Current"/> -- a
    /// resource pool cannot go negative; this packet never states that in so many words,
    /// but nor does it ever describe spending more Edge than a character has, and no
    /// operation here is prepared to represent a negative pool. This is the generic pool
    /// primitive spending Edge bottoms out to: what a specific Edge Boost costs, and what
    /// it buys, is next Issue's Edge Actions/Boosts menu, not this method -- including the
    /// two boosts that only move Edge between pools rather than affecting a roll ("Negate
    /// 1 Edge of a foe" and "Give ally 1 Edge", printed p. 46 / PDF p. 47), both of which
    /// this primitive already composes: reduce the spender's <see cref="Current"/> by N,
    /// and -- for "Give ally 1 Edge" specifically -- call <see cref="GainBonusPoint"/> on
    /// the receiving pool. See the type doc's "Deliberately not modeled" section for why
    /// neither gets its own dedicated method.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is zero or negative.</exception>
    public EdgeSpendResult Spend(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        bool succeeded = amount <= Current;
        EdgePool poolAfter = succeeded ? new EdgePool(Current - amount, BonusGainedThisRound) : this;

        return new EdgeSpendResult(succeeded, amount, this, poolAfter);
    }

    /// <summary>
    /// The confrontation-end reset: Edge accumulated above the character's Edge attribute
    /// is dropped back to it; Edge already below the attribute is left exactly where it
    /// is -- a depleted pool is never topped back up. SR6 Core / Game Concepts / Edge /
    /// printed p. 45 / PDF p. 46. <c>Current' = min(Current, edgeRank)</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edgeRank"/> is negative.</exception>
    public EdgePool EndConfrontation(int edgeRank)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(edgeRank);
        return new EdgePool(Math.Min(Current, edgeRank), BonusGainedThisRound);
    }

    /// <summary>
    /// Burning Edge: the character permanently loses 1 point of Edge rank and spends
    /// every point currently in the pool. SR6 Core / Game Concepts / Edge / Burning Edge
    /// / printed p. 48 / PDF p. 49.
    ///
    /// Refused (<see cref="EdgeBurnResult.Succeeded"/> <see langword="false"/>, nothing
    /// changed) when <paramref name="edgeRank"/> is already 0: the source frames burning
    /// as taking rank "down to zero", never below it, so there is no rank left to spend
    /// this way. Otherwise <paramref name="edgeRank"/> is the character's Edge rank going
    /// in, and the returned <see cref="EdgeBurnResult.RankAfter"/> is <c>edgeRank - 1</c>
    /// -- this type does not store rank, so the caller is responsible for actually
    /// applying that new rank wherever the character's attribute state lives. The pool's
    /// entire <see cref="Current"/> goes to 0 outright, not merely down to the new (lower)
    /// rank: unlike the ordinary confrontation-end reset, the source states this without
    /// an "over your base attribute" qualifier, and separately notes burning is usable
    /// even when Edge normally cannot be spent -- a harsher, last-resort mechanic
    /// throughout this section. <see cref="BonusGainedThisRound"/> is left untouched:
    /// burning is not itself a combat-round boundary, and nothing in the source says
    /// otherwise. Burning Edge's two named uses -- Smackdown and Not Dead Yet -- are what
    /// the burn is spent *on*; see the type doc's "Deliberately not modeled" section.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edgeRank"/> is negative.</exception>
    public EdgeBurnResult Burn(int edgeRank)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(edgeRank);

        if (edgeRank == 0)
        {
            return new EdgeBurnResult(succeeded: false, rankBefore: edgeRank, rankAfter: edgeRank, this, this);
        }

        int rankAfter = edgeRank - 1;
        EdgePool poolAfter = new(0, BonusGainedThisRound);

        return new EdgeBurnResult(succeeded: true, rankBefore: edgeRank, rankAfter, this, poolAfter);
    }
}

/// <summary>The structured, explainable outcome of an <see cref="EdgePool.GainBonusPoint"/> attempt.</summary>
public sealed class EdgeGainResult
{
    /// <summary><see langword="true"/> when the point was actually added to the pool.</summary>
    public bool Granted { get; }

    /// <summary>
    /// <see langword="true"/> when <see cref="EdgePool.RoundGainCap"/> is the reason (or
    /// one of the reasons) the point was refused. Always <see langword="false"/> when no
    /// combat round is in progress (<c>PoolBefore.BonusGainedThisRound</c> is
    /// <see langword="null"/>) -- see ADR 0008.
    /// </summary>
    public bool BlockedByRoundCap { get; }

    /// <summary>
    /// <see langword="true"/> when <see cref="EdgePool.HoldCap"/> is the reason (or one
    /// of the reasons) the point was refused. Both flags can be true at once, when the
    /// pool is simultaneously at both boundaries.
    /// </summary>
    public bool BlockedByHoldCap { get; }

    /// <summary>The pool state before this attempt.</summary>
    public EdgePool PoolBefore { get; }

    /// <summary>The pool state after this attempt -- identical to <see cref="PoolBefore"/> when refused.</summary>
    public EdgePool PoolAfter { get; }

    internal EdgeGainResult(
        bool granted, bool blockedByRoundCap, bool blockedByHoldCap, EdgePool poolBefore, EdgePool poolAfter)
    {
        Granted = granted;
        BlockedByRoundCap = blockedByRoundCap;
        BlockedByHoldCap = blockedByHoldCap;
        PoolBefore = poolBefore;
        PoolAfter = poolAfter;
    }
}

/// <summary>The structured, explainable outcome of an <see cref="EdgePool.Spend"/> attempt.</summary>
public sealed class EdgeSpendResult
{
    /// <summary><see langword="true"/> when the pool held at least <see cref="Amount"/> and the spend applied.</summary>
    public bool Succeeded { get; }

    /// <summary>The amount requested to spend.</summary>
    public int Amount { get; }

    /// <summary>The pool state before this attempt.</summary>
    public EdgePool PoolBefore { get; }

    /// <summary>The pool state after this attempt -- identical to <see cref="PoolBefore"/> when refused.</summary>
    public EdgePool PoolAfter { get; }

    internal EdgeSpendResult(bool succeeded, int amount, EdgePool poolBefore, EdgePool poolAfter)
    {
        Succeeded = succeeded;
        Amount = amount;
        PoolBefore = poolBefore;
        PoolAfter = poolAfter;
    }
}

/// <summary>The structured, explainable outcome of an <see cref="EdgePool.Burn"/> attempt.</summary>
public sealed class EdgeBurnResult
{
    /// <summary>
    /// <see langword="true"/> when there was a rank point to burn (<see cref="RankBefore"/>
    /// was greater than 0) and the burn applied.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>The Edge rank going into the burn attempt.</summary>
    public int RankBefore { get; }

    /// <summary>
    /// The Edge rank after the attempt: <c>RankBefore - 1</c> when <see cref="Succeeded"/>,
    /// otherwise unchanged from <see cref="RankBefore"/>.
    /// </summary>
    public int RankAfter { get; }

    /// <summary>The pool state before this attempt.</summary>
    public EdgePool PoolBefore { get; }

    /// <summary>
    /// The pool state after this attempt: <see cref="EdgePool.Current"/> of 0 when
    /// <see cref="Succeeded"/>, otherwise identical to <see cref="PoolBefore"/>.
    /// </summary>
    public EdgePool PoolAfter { get; }

    internal EdgeBurnResult(bool succeeded, int rankBefore, int rankAfter, EdgePool poolBefore, EdgePool poolAfter)
    {
        Succeeded = succeeded;
        RankBefore = rankBefore;
        RankAfter = rankAfter;
        PoolBefore = poolBefore;
        PoolAfter = poolAfter;
    }
}
