"""Rubinite Archipelago world.

Rubinite is a boss-rush action game. This world is designed around the game's
Challenge Area: every boss has a portal, and the multiworld supplies the sigils
for the ten bosses that are not available at the start.
"""

from __future__ import annotations

from collections.abc import Mapping
from typing import Any

from worlds.AutoWorld import World

from . import data, items, locations, regions, rules


class RubiniteWorld(World):
    """Rubinite is a dark-fantasy boss rush about finding weak points and striking true."""

    game = "Rubinite"
    location_name_to_id = locations.LOCATION_NAME_TO_ID
    item_name_to_id = items.ITEM_NAME_TO_ID
    origin_region_name = "Challenge Area"

    def create_regions(self) -> None:
        regions.create_and_connect_regions(self)
        locations.create_all_locations(self)

    def set_rules(self) -> None:
        rules.set_all_rules(self)

    def create_items(self) -> None:
        items.create_all_items(self)

    def create_item(self, name: str) -> items.RubiniteItem:
        return items.create_item_with_correct_classification(self, name)

    def get_filler_item_name(self) -> str:
        return items.get_random_filler_item_name(self)

    def fill_slot_data(self) -> Mapping[str, Any]:
        boss_data = []
        for key, name, level, sigil in data.BOSSES:
            location_ids = {
                slug: locations.LOCATION_NAME_TO_ID[data.location_name(name, suffix)]
                for suffix, slug in data.CHECK_TYPES
            }
            boss_data.append(
                {
                    "key": key,
                    "name": name,
                    "level": level,
                    "sigil": sigil,
                    "start": sigil is None,
                    "locations": location_ids,
                }
            )
        return {
            "version": 1,
            "goal_boss": data.GOAL_BOSS_LEVEL,
            "item_ids": items.ITEM_NAME_TO_ID,
            "bosses": boss_data,
        }
