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
/// SR6 Core / Game Concepts / Edge / printed pp. 45-46 / PDF pp. 46-47 is the source for
/// every rule this type encodes:
///
/// - "Characters start a gaming session with Edge points equal to their Edge rank."
///   (printed p. 46 / PDF p. 47) -- <see cref="StartSession"/>.
/// - "No player may gain more than two bonus points of Edge in a combat round." (printed
///   p. 45 / PDF p. 46) -- the <see cref="RoundGainCap"/> enforced by
///   <see cref="GainBonusPoint"/>.
/// - "Edge can be carried over and accumulated up to a limit of 7, including the Edge
///   provided by the character's Edge attribute." (printed p. 46 / PDF p. 47) -- the
///   <see cref="HoldCap"/> enforced by <see cref="GainBonusPoint"/>.
/// - "Any Edge garnered over your base attribute goes away when you complete any ongoing
///   confrontation... If, at the end of the confrontation, your current Edge points are
///   less than your Edge attribute, you stay at the lower level." (printed p. 46 / PDF
///   p. 47) -- <see cref="EndConfrontation"/>.
/// - "Edge is spent through Edge Boosts..." (printed p. 46 / PDF p. 47) -- the pool
///   mechanics of spending are <see cref="Spend"/>; what a Boost actually buys is next
///   Issue's Edge Actions/Boosts menu, not this type.
/// - Burning Edge: "you permanently lose 1 point of Edge rank (even taking it down to
///   zero if you so choose). You also spend all accumulated Edge." (printed p. 48 / PDF
///   p. 49) -- <see cref="Burn"/>.
///
/// Two distinct units of time are named in that source and deliberately modeled as two
/// distinct, explicit state transitions rather than inferred from one another:
///
/// - "a combat round" scopes the bonus-gain cap. This packet never defines what makes one
///   combat round end and the next begin (initiative, turns -- Combat-phase machinery
///   this Issue does not build), so <see cref="BeginCombatRound"/> is an opaque boundary
///   marker a future combat system calls; this type assumes nothing about how long a
///   round lasts or what causes one to end, only that "a new one has begun" is asserted
///   to it explicitly.
/// - "any ongoing confrontation" (combat, hacking, social persuasion, "any situation
///   where bonus Edge might be accumulated") scopes the reset-to-attribute rule, and is
///   modeled the same way: <see cref="EndConfrontation"/> is called explicitly by
///   whatever future system decides a confrontation has ended. The two transitions are
///   deliberately independent -- <see cref="EndConfrontation"/> does not also reset the
///   per-round counter, because nothing in the source says ending a confrontation is the
///   same event as ending a combat round, and folding one into the other would be an
///   assumption this type is not entitled to make. A caller starting a fresh confrontation
///   is expected to call <see cref="BeginCombatRound"/> for that confrontation's first
///   round in its own right.
///
/// Deliberately not modeled here, with reasoning kept alongside the code that would
/// otherwise silently omit it:
///
/// - The Attack/Defense Rating comparison gain and the Social Edge table gain are
///   separate types -- <see cref="AttackDefenseRatingEdgeGain"/> and
///   <see cref="SocialSituationEdgeGain"/> -- because they are triggered by machinery
///   (combat, social resolution) this Issue does not build; both ultimately feed
///   <see cref="GainBonusPoint"/> once a caller has decided a point is earned.
/// - "Gear, spells, qualities, and other items provide bonus Edge or cancel Edge given to
///   other players" (printed p. 45 / PDF p. 46) names a category of Edge source, not a
///   formula: no gear/spell/quality catalog exists yet (that is
///   <c>Deckard.Data</c> content for a future Issue), and the primitive such an item
///   would eventually call is already <see cref="GainBonusPoint"/> or <see cref="Spend"/>
///   -- there is no distinct algorithm here to stub.
/// - "Preventing Edge Abuse" (printed pp. 45-46 / PDF pp. 46-47) is gamemaster guidance
///   with no formula or number attached ("the gamemaster has final discretion on when
///   Edge should be awarded and when it should be withheld") -- not a mechanic an engine
///   call site can express at all, so there is nothing here to leave unresolved.
/// </summary>
public sealed class EdgePool
{
    /// <summary>
    /// "No player may gain more than two bonus points of Edge in a combat round." SR6
    /// Core / Game Concepts / Edge / Gaining Edge / printed p. 45 / PDF p. 46.
    /// </summary>
    public const int RoundGainCap = 2;

    /// <summary>
    /// "Edge can be carried over and accumulated up to a limit of 7, including the Edge
    /// provided by the character's Edge attribute." SR6 Core / Game Concepts / Edge /
    /// Spending Edge / printed p. 46 / PDF p. 47.
    /// </summary>
    public const int HoldCap = 7;

    /// <summary>The character's current spendable Edge points.</summary>
    public int Current { get; }

    /// <summary>
    /// Bonus Edge points gained since the last <see cref="BeginCombatRound"/> call, for
    /// enforcing <see cref="RoundGainCap"/>. Not part of any printed vocabulary -- it is
    /// this type's own bookkeeping for a cap the book states but does not name a counter
    /// for.
    /// </summary>
    public int BonusGainedThisRound { get; }

    private EdgePool(int current, int bonusGainedThisRound)
    {
        Current = current;
        BonusGainedThisRound = bonusGainedThisRound;
    }

    /// <summary>
    /// "Characters start a gaming session with Edge points equal to their Edge rank."
    /// SR6 Core / Game Concepts / Edge / Spending Edge / printed p. 46 / PDF p. 47.
    ///
    /// This does not clip <paramref name="edgeRank"/> to <see cref="HoldCap"/> even
    /// though that value becomes <see cref="Current"/> directly. The cited sentence
    /// stating "up to a limit of 7" is scoped to what the session "can be carried over
    /// and accumulated" to -- the accumulation process <see cref="GainBonusPoint"/>
    /// governs -- not to this unconditional starting assignment; nothing in the source
    /// revisits or qualifies "equal to their Edge rank" with a ceiling. A rank above 7 is
    /// not itself validated against any upper bound here, for the same reason
    /// <see cref="Attributes.ConditionMonitor"/> encodes none for Body or Willpower: the
    /// valid range of an attribute rank is Character Creation content, outside both this
    /// Issue's packet and its scope. Should a rank above 7 ever occur, the pool simply
    /// starts above <see cref="HoldCap"/> and <see cref="GainBonusPoint"/> still behaves
    /// correctly -- it already refuses to gain further once <see cref="Current"/> is at
    /// or above <see cref="HoldCap"/>, whatever put it there.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edgeRank"/> is negative.</exception>
    public static EdgePool StartSession(int edgeRank)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(edgeRank);
        return new EdgePool(edgeRank, 0);
    }

    /// <summary>
    /// Attempts to gain one bonus point of Edge -- the only amount any printed gain
    /// source in this packet ever names ("a point of Edge", "a bonus point of Edge"),
    /// so this deliberately takes no amount parameter to generalize beyond what the
    /// source states. Refused, with <see cref="Current"/> and
    /// <see cref="BonusGainedThisRound"/> unchanged, once either printed cap is already
    /// at its boundary; granted otherwise. Both caps are checked against their exact
    /// boundary value (<c>&gt;=</c>, not <c>&gt;</c>): a point that would land exactly on
    /// <see cref="RoundGainCap"/> or <see cref="HoldCap"/> is still granted, only the
    /// next one past it is refused.
    /// </summary>
    public EdgeGainResult GainBonusPoint()
    {
        bool roundCapped = BonusGainedThisRound >= RoundGainCap;
        bool holdCapped = Current >= HoldCap;
        bool granted = !roundCapped && !holdCapped;

        EdgePool poolAfter = granted
            ? new EdgePool(Current + 1, BonusGainedThisRound + 1)
            : this;

        return new EdgeGainResult(granted, roundCapped, holdCapped, this, poolAfter);
    }

    /// <summary>
    /// Marks the start of a new combat round, resetting <see cref="BonusGainedThisRound"/>
    /// so <see cref="RoundGainCap"/> applies to the new round rather than accumulating
    /// forever. See the type doc's "two distinct units of time" section for why this is
    /// an explicit, opaque transition rather than a round identifier this type compares.
    /// </summary>
    public EdgePool BeginCombatRound() => new(Current, bonusGainedThisRound: 0);

    /// <summary>
    /// Spends <paramref name="amount"/> Edge from the pool. Refused (<see cref="Current"/>
    /// unchanged) when <paramref name="amount"/> exceeds <see cref="Current"/> -- a
    /// resource pool cannot go negative; this packet never states that in so many words,
    /// but nor does it ever describe spending more Edge than a character has, and no
    /// operation here is prepared to represent a negative pool. This is the generic pool
    /// primitive "spending Edge" bottoms out to: what a specific Edge Boost costs, and
    /// what it buys, is next Issue's Edge Actions/Boosts menu, not this method. The same
    /// primitive also covers an item or Boost that reduces an opponent's Edge (e.g. the
    /// 2-Edge Boost "Negate 1 Edge of a foe", printed p. 47 / PDF p. 48) -- from this
    /// pool's point of view that is still "reduce Current by N", regardless of whose
    /// choice caused it; assigning that meaning is the future caller's job.
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
    /// "Any Edge garnered over your base attribute goes away when you complete any
    /// ongoing confrontation; this includes combat, hacking, social persuasion, and any
    /// situation where bonus Edge might be accumulated. If, at the end of the
    /// confrontation, your current Edge points are less than your Edge attribute, you
    /// stay at the lower level." SR6 Core / Game Concepts / Edge / Spending Edge /
    /// printed p. 46 / PDF p. 47 -- <c>Current' = min(Current, edgeRank)</c>: Edge above
    /// <paramref name="edgeRank"/> is dropped to it; Edge already below
    /// <paramref name="edgeRank"/> is left exactly where it is, per "you stay at the
    /// lower level. If you want more Edge, you have to earn it" -- this never tops a
    /// depleted pool back up to the attribute value.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edgeRank"/> is negative.</exception>
    public EdgePool EndConfrontation(int edgeRank)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(edgeRank);
        return new EdgePool(Math.Min(Current, edgeRank), BonusGainedThisRound);
    }

    /// <summary>
    /// Burning Edge: "you permanently lose 1 point of Edge rank (even taking it down to
    /// zero if you so choose). You also spend all accumulated Edge." SR6 Core / Game
    /// Concepts / Edge / Burning Edge / printed p. 48 / PDF p. 49.
    ///
    /// <paramref name="edgeRank"/> is the character's Edge rank going in; the returned
    /// <see cref="EdgeBurnResult.RankAfter"/> is <c>max(edgeRank - 1, 0)</c> -- this type
    /// does not store rank, so the caller is responsible for actually applying that new
    /// rank wherever the character's attribute state lives. "You also spend all
    /// accumulated Edge" sets <see cref="Current"/> to 0 outright, not merely down to the
    /// new (lower) rank: unlike the ordinary confrontation-end reset, this sentence
    /// carries no "over your base attribute" qualifier, and Burning Edge is described as
    /// usable even "at times when Edge normally cannot be spent" -- a harsher, last-resort
    /// mechanic the book distinguishes from ordinary spending throughout this section.
    /// <see cref="BonusGainedThisRound"/> is left untouched: burning is not itself a
    /// combat-round or confrontation boundary, and nothing in the source says otherwise.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edgeRank"/> is negative.</exception>
    public EdgeBurnResult Burn(int edgeRank)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(edgeRank);

        int rankAfter = Math.Max(edgeRank - 1, 0);
        EdgePool poolAfter = new(0, BonusGainedThisRound);

        return new EdgeBurnResult(edgeRank, rankAfter, this, poolAfter);
    }
}

/// <summary>The structured, explainable outcome of an <see cref="EdgePool.GainBonusPoint"/> attempt.</summary>
public sealed class EdgeGainResult
{
    /// <summary><see langword="true"/> when the point was actually added to the pool.</summary>
    public bool Granted { get; }

    /// <summary>
    /// <see langword="true"/> when <see cref="EdgePool.RoundGainCap"/> is the reason (or
    /// one of the reasons) the point was refused.
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

/// <summary>The structured, explainable outcome of an <see cref="EdgePool.Burn"/>.</summary>
public sealed class EdgeBurnResult
{
    /// <summary>The Edge rank going into the burn.</summary>
    public int RankBefore { get; }

    /// <summary>The Edge rank after the burn: <c>max(RankBefore - 1, 0)</c>.</summary>
    public int RankAfter { get; }

    /// <summary>The pool state before this burn.</summary>
    public EdgePool PoolBefore { get; }

    /// <summary>The pool state after this burn -- always <see cref="EdgePool.Current"/> of 0.</summary>
    public EdgePool PoolAfter { get; }

    internal EdgeBurnResult(int rankBefore, int rankAfter, EdgePool poolBefore, EdgePool poolAfter)
    {
        RankBefore = rankBefore;
        RankAfter = rankAfter;
        PoolBefore = poolBefore;
        PoolAfter = poolAfter;
    }
}
