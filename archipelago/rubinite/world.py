"""Rubinite Archipelago world.

Rubinite is a boss-rush action game. This world is designed around the game's
Challenge Area: every boss has a portal, and the multiworld supplies the sigils
for the ten bosses that are not available at the start.
"""

from __future__ import annotations

from collections.abc import Mapping
from typing import Any

from worlds.AutoWorld import World
from .options import RubiniteOptions

from . import data, items, locations, regions, rules


class RubiniteWorld(World):
    """Rubinite is a dark-fantasy boss rush about finding weak points and striking true."""

    game = "Rubinite"
    location_name_to_id = locations.LOCATION_NAME_TO_ID
    item_name_to_id = items.ITEM_NAME_TO_ID
    origin_region_name = "Challenge Area"
    options_dataclass = RubiniteOptions
    options: RubiniteOptions

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
            "boss_scene_map": self._boss_scene_map,
        }
    
    def __init__(self, multiworld, player):
        super().__init__(multiworld, player)
        self._boss_scene_map: dict[str, str] = self._compute_boss_scene_map()

    def _compute_boss_scene_map(self) -> dict[str, str]:
        arenas = [level for _k, _n, level, _s in data.BOSSES]
        if self.options.boss_shuffle.value == self.options.boss_shuffle.option_vanilla:
            return {a: a for a in arenas}
        shuffled = arenas.copy()
        self.random.shuffle(shuffled)
        return dict(zip(arenas, shuffled))

    def _portal_level_for_boss(self, boss_key: str) -> str | None:
        """Portal scene that loads scene from boss_key (after Shuffle)."""
        target = data.level_for_boss_key(boss_key)
        if target is None:
            return None
        for arena_level, shuffled_level in self._boss_scene_map.items():
            if shuffled_level == target:
                return arena_level
        return None

    def sigil_for_boss_region(self, boss_key: str) -> str | None:
        """Sigil that is needed for reaching boss_key region."""
        portal = self._portal_level_for_boss(boss_key)
        if portal is None:
            return None
        portal_boss = data.boss_for_level(portal)
        if portal_boss is None:
            return None
        return portal_boss[3]  # Sigil of Boss' Portal

    