from dataclasses import dataclass
from Options import Choice, PerGameCommonOptions, Range, Toggle

from . import data


class BossShuffle(Choice):
    """Shuffle which boss scene loads at each level entrance."""
    display_name = "Boss Shuffle"
    option_vanilla = 0
    option_shuffle = 1
    default = 0


class GoalBoss(Choice):
    """Which boss must be defeated to complete the game."""
    display_name = "Goal Boss"
    option_beast = 0
    option_guardian = 1
    option_cult = 2
    option_gravekeeper = 3
    option_plagueroot = 4
    option_nestling = 5
    option_guardian2 = 6
    option_ghost = 7
    option_master = 8
    option_eyestree = 9
    option_queen = 10
    option_witch = 11
    option_witch2 = 12
    default = 12


class BossShuffle(Choice):
    """Shuffle which boss scene loads at each level entrance."""
    display_name = "Boss Shuffle"
    option_vanilla = 0
    option_shuffle = 1
    default = 0


class StartingBossCount(Range):
    """How many boss portals are unlocked at the start."""
    display_name = "Starting Boss Count"
    range_start = 1
    range_end = 12
    default = 4


class StartingBossSelection(Choice):
    """Which portals are unlocked at the start."""
    display_name = "Starting Boss Selection"
    option_vanilla = 0
    option_random = 1
    default = 0


class EnableNoHitClear(Toggle):
    """Enable the No-Hit Clear check for each boss."""
    display_name = "Enable No-Hit Clear"
    default = True


class EnableNoTalismanClear(Toggle):
    """Enable the No-Talisman Clear check for each boss."""
    display_name = "Enable No-Talisman Clear"
    default = False


class EnableFlawlessClear(Toggle):
    """Enable the Flawless Clear check (No-Hit + No-Talisman) for each boss."""
    display_name = "Enable Flawless Clear"
    default = False


class EnableSRankClear(Toggle):
    """Enable the S-Rank Clear check for each boss."""
    display_name = "Enable S-Rank Clear"
    default = False


@dataclass
class RubiniteOptions(PerGameCommonOptions):
    boss_shuffle: BossShuffle
    goal_boss: GoalBoss
    starting_boss_count: StartingBossCount
    starting_boss_selection: StartingBossSelection
    enable_nohit_clear: EnableNoHitClear
    enable_notalisman_clear: EnableNoTalismanClear
    enable_flawless_clear: EnableFlawlessClear
    enable_srank_clear: EnableSRankClear

    # Placeholders for more options to be added later:
    # player_color: PlayerColor
    # world_switch: WorldSwitch