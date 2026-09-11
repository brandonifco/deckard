namespace Deckard.Data.Skills;

/// <summary>
/// The nineteen active skills SR6 Core / Skills / printed pp. 92-97 / PDF pp. 93-98 lists
/// in its "Skill List" summary box, in the order printed there: Astral, Athletics,
/// Biotech, Close Combat, Con, Conjuring, Cracking, Electronics, Enchanting, Engineering,
/// Exotic Weapons, Firearms, Influence, Outdoors, Perception, Piloting, Sorcery, Stealth,
/// Tasking. That summary box's own "Untrained?" column is asserted against each skill's
/// own printed entry in <see cref="SkillCatalog"/> -- both agree, so no invented value.
///
/// This is the closed vocabulary itself: no skill outside this list exists in the packet,
/// and none of these nineteen is omitted. See <see cref="SkillCatalog"/> for each skill's
/// linked attribute and untrained flag.
///
/// Knowledge skills are mentioned at the end of this packet ("Knowledge skills do not
/// have ranks, because they are not used directly in skill tests...") but are a distinct,
/// separate category the book itself says are not used in skill tests -- so they carry no
/// linked attribute or untrained flag, and the packet does not enumerate their names
/// before the excerpt's page range ends. Out of scope here, not a missed row.
/// </summary>
public enum Skill
{
    Astral,
    Athletics,
    Biotech,
    CloseCombat,
    Con,
    Conjuring,
    Cracking,
    Electronics,
    Enchanting,
    Engineering,
    ExoticWeapons,
    Firearms,
    Influence,
    Outdoors,
    Perception,
    Piloting,
    Sorcery,
    Stealth,
    Tasking,
}
