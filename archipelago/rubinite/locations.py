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
            
    for shop_index, (title, _) in enumerate(data.TALISMANS):
        mapping[data.shop_location_name(title)] = data.SHOP_LOCATION_BASE + shop_index

    for skill_index, (tree_id, title) in enumerate(data.SKILLS):
        mapping[data.skill_tree_location_name(tree_id, title)] = (
            data.SKILL_TREE_LOCATION_BASE + skill_index
        )
        
    return mapping


LOCATION_NAME_TO_ID = _build_locations()


class RubiniteLocation(Location):
    game = "Rubinite"


def create_all_locations(world: RubiniteWorld) -> None:
    enabled_checks = world.get_enabled_check_types()
    for _key, boss_name, _level, _sigil in world.active_bosses:
        boss_checks = enabled_checks
        if _key in data.RANKLESS_BOSSES:
            boss_checks = [c for c in enabled_checks if c[1] != "rank"]

        region = world.get_region(boss_name)
        for suffix, _slug in boss_checks:
            name = data.location_name(boss_name, suffix)
            region.add_locations({name: LOCATION_NAME_TO_ID[name]}, RubiniteLocation)

    shop_region = world.get_region("Wichiwi Shop")
    for title, _ in data.TALISMANS:
        name = data.shop_location_name(title)
        shop_region.add_locations({name: LOCATION_NAME_TO_ID[name]}, RubiniteLocation)

    # Skill tree purchases are reachable from the Challenge Area (the
    # skill tree UI is always accessible, no portal needed).
    challenge_area = world.get_region("Challenge Area")
    for tree_id, title in data.SKILLS:
        name = data.skill_tree_location_name(tree_id, title)
        challenge_area.add_locations(
            {name: LOCATION_NAME_TO_ID[name]}, RubiniteLocation
        )

    goal_key = world.get_goal_boss_key()
    goal_boss_name = next(name for key, name, _l, _s in world.active_bosses if key == goal_key)
    goal_region = world.get_region(goal_boss_name)
    goal_region.add_event(
        f"{goal_boss_name} Defeated",
        "Victory",
        location_type=RubiniteLocation,
        item_type=items.RubiniteItem,
    )
