"""Static game data for the Rubinite Archipelago world.

Everything in this module is derived from the retail game's own ScriptableObjects
(LevelData, TalismanData, SkillTreeData) so IDs and display names match the client.
"""

from __future__ import annotations

# The challenge area exposes one portal per boss. The first three are always open;
# the rest require their sigil item from the multiworld.
BOSSES = [
    # key, display name, level scene, required sigil (None = available from the start)
    ("Beast", "The Beast", "BeastLevel", None),
    ("Guardian", "Faye the Guardian", "GuardianLevel", None),
    ("EyesTree", "The Red-Eyed Tree", "EyesTreeLevel", None),
    ("Cult", "The Priest", "CultLevel", "Cult Sigil"),
    ("PlagueBird", "The Gravekeeper", "PlagueLevel", "Gravekeeper Sigil"),
    ("PlagueRoot", "The Plaguebearer", "PlagueRootLevel", "Plaguebearer Sigil"),
    ("Nestling", "The Martyred Angel", "NestlingLevel", "Nestling Sigil"),
    ("Guardian2", "Faye II", "GuardianLevel2", "Faye II Sigil"),
    ("Ghost", "The Ghost", "GhostLevel", "Ghost Sigil"),
    ("Master", "Roy the Hunter", "MasterLevel", "Hunter Sigil"),
    ("Queen", "The Puppet Master", "QueenLevel", "Puppet Master Sigil"),
    ("Witch", "Alcina the Witch", "Witch_FirstRunLevel", "Witch Sigil"),
    ("Witch2", "Alcina, True Form", "WitchLevel", "Witch II Sigil"),
]

GOAL_BOSS_LEVEL = "WitchLevel"

# Five checks per boss. Only the first clear may hold progression items; the four
# challenge clears are optional skill checks and are kept free of progression.
CHECK_TYPES = [
    ("First Clear", "first"),
    ("No-Hit Clear", "nohit"),
    ("No-Talisman Clear", "notalisman"),
    ("Flawless Clear", "flawless"),
    ("S-Rank Clear", "srank"),
]

OPTIONAL_CHECKS = ("nohit", "notalisman", "flawless", "srank")

# All 30 real talismans (the two test entries and the exhibition dummy are excluded).
TALISMANS = [
    ("Grounded Lunge", "01_AddStrikeDistance"),
    ("Flutterstep", "02_AgileBody"),
    ("Sharp Blade, Sharp Eye", "03_Attack3Focus"),
    ("Scorched Trail", "04_BloodFire"),
    ("Bloodwings", "05_BloodFly"),
    ("Direct Hit", "06_CriticalStrikeATKUp"),
    ("Mira's Shadow", "07_MiraTalisman"),
    ("Pocket Bombs", "08_DodgeBomb"),
    ("Killer Instinct", "09_SpecialStrikeAngle"),
    ("Thorny Rose", "10_FocusFlower"),
    ("Pensive Eyes", "11_FocusReduceSpeedDecrease"),
    ("Keen Gaze", "12_FocusSpeedUp"),
    ("Greed", "13_Greed"),
    ("Bloodsucker", "14_HPDrain"),
    ("Hatchiwi", "15_WichiHat"),
    ("Mark Meister", "16_MarkMaster"),
    ("Paralyzing Strike", "17_Paralysis"),
    ("Bracelet of Blessing", "18_Resistance"),
    ("Perfect Strike", "19_InvincibleStrike"),
    ("Earthen Barrier", "20_StrongBody"),
    ("True Ranger", "21_IncreaseShootingATK"),
    ("Roy's Dagger", "22_RoyKnife"),
    ("True Swordfighter", "23_IncreaseSlashATK"),
    ("Sharpshooter", "24_ReduceShootMoveScale"),
    ("True Hunter", "25_IncreaseATKBasedOnMarks"),
    ("Undying Blood", "26_DimonTalisman"),
    ("Strike Meister", "27_StrikeSkillMaster"),
    ("Mark of Courage", "28_CourageMark"),
    ("Rubinite", "29_Rubinite"),
    ("Talisman Toss", "X27_TalismanShooter"),
]

# The 20 randomized skill-tree upgrades. The four always-known basics (Focus,
# Critical Strike, Slash, HP Recovery) and the four first-tier skills stay free.
SKILLS = [
    # The first tier stays unlocked as the baseline kit; the rest are items.
    ("2-1", "Dart Charge Accelerator"),
    ("2-2", "Rolling Recovery"),
    ("2-3", "Explosive Strike"),
    ("2-4", "Pureblood Heart"),
    ("2-5", "Sunder Slash - Enhanced"),
    ("2-6", "Enhanced Strike"),
    ("2-7", "Enhanced Heal"),
    ("3-1", "Enhanced Darts"),
    ("3-2", "Blood-Tipped Strike"),
    ("3-3", "Pureblood Heart"),
    ("3-4", "Enhanced Strike"),
    ("3-5", "Enhanced Heal"),
    ("3-6", "Critical Strike - Enhanced"),
    ("3-7", "Enhanced Slash"),
    ("4-1", "Pureblood Heart"),
    ("4-2", "Blood-Tipped Strike - Enhanced"),
    ("4-3", "Enhanced Heal"),
    ("4-4", "Explosive Strike - Enhanced"),
    ("4-5", "Bloodblade Strike - Enhanced"),
    ("4-6", "Pureblood Heart"),
]

FILLER_ITEM = "Blood Dust"

# Base IDs. Location and item ranges are intentionally separate.
LOCATION_BASE = 71000
SIGIL_ITEM_BASE = 72000
TALISMAN_ITEM_BASE = 72100
SKILL_ITEM_BASE = 72200
FILLER_ITEM_ID = 72300


def location_name(boss_name: str, suffix: str) -> str:
    return f"{boss_name}: {suffix}"


def talisman_item_name(title: str) -> str:
    return f"Talisman: {title}"


def skill_item_name(tree_id: str, title: str) -> str:
    return f"Skill {tree_id}: {title}"
