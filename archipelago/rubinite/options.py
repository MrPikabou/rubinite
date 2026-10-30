from dataclasses import dataclass
from Options import Choice, PerGameCommonOptions, Toggle


class BossShuffle(Choice):
    """Shuffle which boss scene loads at each level entrance."""
    display_name = "Boss Shuffle"
    option_vanilla = 0
    option_shuffle = 1
    default = 0


@dataclass
class RubiniteOptions(PerGameCommonOptions):
    boss_shuffle: BossShuffle
    # Following are placeholder for more options to be added later:
    # player_color: PlayerColor
    # world_switch: WorldSwitch