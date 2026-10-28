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
    for _key, boss_name, _level, _sigil in data.BOSSES:
        region = world.get_region(boss_name)
        for suffix, _slug in data.CHECK_TYPES:
            name = data.location_name(boss_name, suffix)
            region.add_locations({name: LOCATION_NAME_TO_ID[name]}, RubiniteLocation)

    # Victory is an event inside the goal boss's region. Reaching the region already
    # requires the goal boss's sigil, so the event only needs to exist.
    goal_region = world.get_region("Alcina, True Form")
    goal_region.add_event(
        "Alcina, True Form Defeated",
        "Victory",
        location_type=RubiniteLocation,
        item_type=items.RubiniteItem,
    )
