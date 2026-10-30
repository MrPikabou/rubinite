"""Regions and entrances for Rubinite."""

from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import Region
from rule_builder.rules import Has

from . import data

if TYPE_CHECKING:
    from .world import RubiniteWorld


def create_and_connect_regions(world: RubiniteWorld) -> None:
    challenge_area = Region("Challenge Area", world.player, world.multiworld)
    regions = [challenge_area]
    for _key, boss_name, _level, _sigil in data.BOSSES:
        regions.append(Region(boss_name, world.player, world.multiworld))
    world.multiworld.regions += regions

    for key, boss_name, _level, _vanilla_sigil in data.BOSSES:
    boss_region = world.get_region(boss_name)
    sigil = world.sigil_for_boss_region(key)
    if sigil:
        challenge_area.connect(
            boss_region, f"Challenge Area to {boss_name}", rule=Has(sigil)
        )
    else:
        challenge_area.connect(boss_region, f"Challenge Area to {boss_name}")
