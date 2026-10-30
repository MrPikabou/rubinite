"""Logic rules for Rubinite."""

from __future__ import annotations

from typing import TYPE_CHECKING

from rule_builder.rules import CanReachRegion

from . import data

if TYPE_CHECKING:
    from .world import RubiniteWorld


def _not_progression(item) -> bool:
    return not item.advancement


def set_all_rules(world: RubiniteWorld) -> None:
    # Optional challenge clears are never allowed to hold progression, so a player
    # can skip a no-hit/no-talisman/flawless/S-rank run without breaking logic.
    for _key, boss_name, _level, _sigil in data.BOSSES:
        for suffix, slug in data.CHECK_TYPES:
            if slug in data.OPTIONAL_CHECKS:
                location = world.get_location(data.location_name(boss_name, suffix))
                location.item_rule = _not_progression

    goal_key = world.get_goal_boss_key()
    goal_name = next(name for key, name, _l, _s in data.BOSSES if key == goal_key)
    world.set_completion_rule(CanReachRegion(goal_name))
