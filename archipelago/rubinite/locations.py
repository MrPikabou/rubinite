"""Locations for Rubinite."""

from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import Location

from . import data, items

if TYPE_CHECKING:
    from .world import RubiniteWorld


def _build_locations() -> dict[str, int]:
    mapping: dict[str, int] = {}
    for boss_index, (_key, boss_name, _level, _sigil) in enumerate(data.BOSSES):
        for check_index, (suffix, _slug) in enumerate(data.CHECK_TYPES):
            mapping[data.location_name(boss_name, suffix)] = (
                data.LOCATION_BASE + boss_index * len(data.CHECK_TYPES) + check_index
            )
    return mapping


LOCATION_NAME_TO_ID = _build_locations()


class RubiniteLocation(Location):
    game = "Rubinite"


def create_all_locations(world: RubiniteWorld) -> None:
    goal_key = world.get_goal_boss_key()
    goal_boss_name = next(name for key, name, _l, _s in data.BOSSES if key == goal_key)
    goal_region = world.get_region(goal_boss_name)
    goal_region.add_event(
        f"{goal_boss_name} Defeated",
        "Victory",
        location_type=RubiniteLocation,
        item_type=items.RubiniteItem,
    )
