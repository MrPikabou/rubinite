from dataclasses import dataclass
from Options import Choice, PerGameCommonOptions, Range, Toggle, OptionGroup
from worlds.AutoWorld import WebWorld
from . import data


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
    option_darkruby = 13
    default = 12


class BossShuffle(Choice):
    """Shuffle which boss scene loads at each level entrance."""
    display_name = "Boss Shuffle"
    option_vanilla = 0
    option_shuffle = 1
    default = 0


class EnableDarkRuby(Toggle):
    """Enable the Dark Ruby Boss from Roy's Memory."""
    display_name = "Enable Dark Ruby"
    default = False


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


class BossDustMultiplier(Range):
    """Multiplies the amount of Blood Dust bosses drop."""
    display_name = "Boss Dust Multiplier"
    range_start = 1
    range_end = 10
    default = 1


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


class EnableRankClear(Toggle):
    """Enable the Rank Clear check for each boss."""
    display_name = "Enable Rank Clear"
    default = True


class RequiredRank(Choice):
    """Which rank is required to unlock the Rank Clear check (S is hardest, C is easiest)."""
    display_name = "Required Rank"
    option_s = 0
    option_a = 1
    option_b = 2
    option_c = 3
    default = 2


class ProgressiveShop(Toggle):
    """Shop slots are unlocked by progressive shop items."""
    display_name = "Progressive Shop"
    default = False


class StartingShopGroups(Range):
    """How many shop groups are unlocked from the start when ProgressiveShop is enabled."""
    display_name = "Starting Shop Groups"
    range_start = 0
    range_end = 6
    default = 1


class ShopPriceMode(Choice):
    """Use vanilla shop prices or randomize every shop price slot."""
    display_name = "Shop Price Mode"
    option_vanilla = 0
    option_randomized = 1
    default = 0


class ShopPriceMin(Range):
    """Minimum price per talisman slot when randomized (inclusive)."""
    display_name = "Shop Price Min"
    range_start = 0
    range_end = 3000
    default = 100


class ShopPriceMax(Range):
    """Maximum price per talisman slot when randomized (inclusive)."""
    display_name = "Shop Price Max"
    range_start = 0
    range_end = 3000
    default = 600


class SkillPriceMode(Choice):
    """Use vanilla skill prices or randomize every skill price slot."""
    display_name = "Skill Price Mode"
    option_vanilla = 0
    option_randomized = 1
    default = 0


class SkillPriceMin(Range):
    """Minimum price per skill slot when randomized (inclusive)."""
    display_name = "Skill Price Min"
    range_start = 0
    range_end = 5000
    default = 200


class SkillPriceMax(Range):
    """Maximum price per skill slot when randomized (inclusive)."""
    display_name = "Skill Price Max"
    range_start = 0
    range_end = 5000
    default = 700


class PlayerColor(Choice):
    """Which color/skin the player starts with."""
    display_name = "Player Color"
    option_red = 0
    option_blue = 1
    option_yellow = 2
    option_green = 3
    option_purple = 4
    option_orange = 5
    option_black = 6
    option_white = 7
    option_pink = 8
    option_turquoise = 9
    option_darkruby = 10
    default = 0


class DeathLink(Toggle):
    """Share deaths with other DeathLink players."""
    display_name = "Death Link"
    default = False


class DeathLinkAmnesty(Range):
    """Deaths forgiven before one is shared (0 = every death)."""
    display_name = "Death Link Amnesty"
    range_start = 0
    range_end = 30
    default = 3


@dataclass
class RubiniteOptions(PerGameCommonOptions):
    goal_boss: GoalBoss
    boss_shuffle: BossShuffle
    enable_dark_ruby: EnableDarkRuby
    starting_boss_count: StartingBossCount
    starting_boss_selection: StartingBossSelection
    boss_dust_multiplier: BossDustMultiplier
    enable_nohit_clear: EnableNoHitClear
    enable_notalisman_clear: EnableNoTalismanClear
    enable_flawless_clear: EnableFlawlessClear
    enable_rank_clear: EnableRankClear
    required_rank: RequiredRank
    progressive_shop: ProgressiveShop
    starting_shop_groups: StartingShopGroups
    shop_price_mode: ShopPriceMode
    shop_price_min: ShopPriceMin
    shop_price_max: ShopPriceMax
    skill_price_mode: SkillPriceMode
    skill_price_min: SkillPriceMin
    skill_price_max: SkillPriceMax
    player_color: PlayerColor
    death_link: DeathLink
    death_link_amnesty: DeathLinkAmnesty


class RubiniteWebWorld(WebWorld):
    game = "Rubinite"
    option_groups = [
        OptionGroup("Goal & Progression", [
            GoalBoss,
            BossShuffle,
            EnableDarkRuby,
            BossDustMultiplier,
            StartingBossCount,
            StartingBossSelection,
            DeathLink,
            DeathLinkAmnesty,
        ]),
        OptionGroup("Checks", [
            EnableNoHitClear,
            EnableNoTalismanClear,
            EnableFlawlessClear,
            EnableRankClear,
            RequiredRank,
        ]),
        OptionGroup("Shop & Skill Prices", [
            ProgressiveShop,
            StartingShopGroups,
            ShopPriceMode,
            ShopPriceMin,
            ShopPriceMax,
            SkillPriceMode,
            SkillPriceMin,
            SkillPriceMax,
        ]),
        OptionGroup("Cosmetic", [
            PlayerColor,
        ]),
    ]