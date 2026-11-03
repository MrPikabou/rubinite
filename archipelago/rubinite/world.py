"""Rubinite Archipelago world.

Rubinite is a boss-rush action game. This world is designed around the game's
Challenge Area: every boss has a portal, and the multiworld supplies the sigils
for the ten bosses that are not available at the start.
"""

from __future__ import annotations

from collections.abc import Mapping
from typing import Any

from worlds.AutoWorld import World
from .options import RubiniteOptions, RubiniteWebWorld

from . import data, items, locations, regions, rules


class RubiniteWorld(World):
    """Rubinite is a dark-fantasy boss rush about finding weak points and striking true."""

    game = "Rubinite"
    location_name_to_id = locations.LOCATION_NAME_TO_ID
    item_name_to_id = items.ITEM_NAME_TO_ID
    origin_region_name = "Challenge Area"
    options_dataclass = RubiniteOptions
    options: RubiniteOptions
    web = RubiniteWebWorld()

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
        enabled_checks = self.get_enabled_check_types()
        enabled_slugs = [slug for _suffix, slug in enabled_checks]

        boss_data = []
        for key, name, level, _sigil in data.BOSSES:
            location_ids = {
                slug: locations.LOCATION_NAME_TO_ID[data.location_name(name, suffix)]
                for suffix, slug in enabled_checks
            }
            is_starting = key in self._starting_boss_keys
            boss_data.append(
                {
                    "key": key,
                    "name": name,
                    "level": level,
                    "sigil": None if is_starting else data.sigil_for_boss_key_or_fallback(key),
                    "start": is_starting,
                    "locations": location_ids,
                }
            )
        
        shop_location_ids = {
            talisman_id: locations.LOCATION_NAME_TO_ID[data.shop_location_name(title)]
            for title, talisman_id in data.TALISMANS
        }

        return {
            "version": 2,
            "goal_boss": self.get_goal_boss_key(),
            "item_ids": items.ITEM_NAME_TO_ID,
            "bosses": boss_data,
            "boss_scene_map": self._boss_scene_map,
            "required_rank": self.options.required_rank.value,
            "player_color": self.options.player_color.current_key,
            "shop_locations": shop_location_ids,
            "shop_groups": self._shop_groups,
            "progressive_shop": self.options.progressive_shop.value,
            "starting_shop_groups": self.options.starting_shop_groups.value,
            "shop_price_mode": self.options.shop_price_mode.current_key,
            "shop_prices": self._shop_prices,
            "skill_tree_locations": {
                data.skill_tree_location_name(tree_id, title):
                    locations.LOCATION_NAME_TO_ID[data.skill_tree_location_name(tree_id, title)]
                for tree_id, title in data.SKILLS
            },
            "skill_price_mode": self.options.skill_price_mode.current_key,
            "skill_prices": self._skill_prices,
            "filler_items": [
                {"name": name, "dust": dust, "icon": icon}
                for name, _id, _w, dust, icon in data.FILLER_ITEMS
            ],
            "enabled_checks": enabled_slugs,
            "death_link": self.options.death_link.value,
            "death_link_amnesty": self.options.death_link_amnesty.value,
            "dust_multiplier": self.options.boss_dust_multiplier.value,
        }
    
    def generate_early(self) -> None:
        self._boss_scene_map = self._compute_boss_scene_map()
        self._starting_boss_keys = self._compute_starting_bosses()
        self._shop_groups = self._compute_shop_groups()
        self._shop_prices = self._compute_shop_prices()
        self._skill_prices = self._compute_skill_prices() 
        self._validate_item_location_balance()

    def _validate_item_location_balance(self) -> None:
        """Ensure progression items fit into available locations.

        Right now this can never trigger (worst case is ~18 progression items
        against ~43 locations), but it fails fast with a clear message if a
        future change adds more progression-classified items or option
        combinations that would otherwise crash inside the filler.
        """
        enabled_checks = self.get_enabled_check_types()
        boss_locations = len(data.BOSSES) * len(enabled_checks)
        shop_locations = len(data.TALISMANS)
        skill_tree_locations = len(data.SKILLS)
        total_locations = boss_locations + shop_locations + skill_tree_locations

        sigil_items = sum(
            1 for boss in data.BOSSES
            if boss[0] not in self._starting_boss_keys
            and data.sigil_for_boss_key_or_fallback(boss[0]) is not None
        )
        shop_expansion_items = max(0, len(self._shop_groups) - self.options.starting_shop_groups.value)
        total_progression = sigil_items + shop_expansion_items

        if total_progression > total_locations:
            raise ValueError(
                f"Rubinite progression items ({total_progression}) exceed "
                f"available locations ({total_locations}). "
                f"Reduce starting_boss_count or enable more check types."
            )

    def _compute_shop_groups(self) -> list[list[str]]:
        if not self.options.progressive_shop.value:
            return []
        talisman_ids = [tid for _title, tid in data.TALISMANS]
        self.random.shuffle(talisman_ids)
        # Shuffle talismans into SHOP_GROUPS groups; the first N groups are
        # unlocked from the start (starting_shop_groups), so we only put
        # SHOP_GROUPS - N expansion items into the pool.
        return [
            talisman_ids[i * data.SHOP_SLOTS_PER_GROUP:(i + 1) * data.SHOP_SLOTS_PER_GROUP]
            for i in range(data.SHOP_GROUPS)
        ]
    
    def _compute_shop_prices(self) -> dict[str, int]:
        if self.options.shop_price_mode.value != self.options.shop_price_mode.option_randomized:
            return {}
        min_price = self.options.shop_price_min.value
        max_price = self.options.shop_price_max.value
        if min_price > max_price:
            min_price, max_price = max_price, min_price
        return {
            talisman_id: self.random.randint(min_price, max_price)
            for _title, talisman_id in data.TALISMANS
        }

    def _compute_skill_prices(self) -> dict[str, int]:
        if self.options.skill_price_mode.value != self.options.skill_price_mode.option_randomized:
            return {}
        min_p = self.options.skill_price_min.value
        max_p = self.options.skill_price_max.value
        if min_p > max_p: min_p, max_p = max_p, min_p
        return {
            tree_id: self.random.randint(min_p, max_p)
            for tree_id, _title in data.SKILLS
        }
    
    def _compute_starting_bosses(self) -> set[str]:
        count = self.options.starting_boss_count.value
        all_keys = [key for key, _n, _l, _s in data.BOSSES]
        goal_key = self.get_goal_boss_key()
        goal_scene = data.level_for_boss_key(goal_key)
        goal_portals = {
            key for key in all_keys
            if self._boss_scene_map.get(data.level_for_boss_key(key)) == goal_scene
        }
        candidates = [key for key in all_keys if key not in goal_portals]

        count = max(1, min(count, len(candidates)))
        if self.options.starting_boss_selection.value == self.options.starting_boss_selection.option_random:
            return set(self.random.sample(candidates, count))
        return set(candidates[:count])

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
        """Sigil that is needed for reaching boss_key region. (None = free)."""
        portal = self._portal_level_for_boss(boss_key)
        if portal is None:
            return None
        portal_boss = data.boss_for_level(portal)
        if portal_boss is None:
            return None
        portal_key = portal_boss[0]
        if portal_key in self._starting_boss_keys:
            return None
        return data.sigil_for_boss_key_or_fallback(portal_key)

    def get_goal_boss_key(self) -> str:
        """Provides the boss key that must be defeated."""
        value = self.options.goal_boss.value
        # Convert choice value in Key Value: 0 -> "Beast", 12 -> "Witch2"
        keys = [key for key, _n, _l, _s in data.BOSSES]
        if 0 <= value < len(keys):
            return keys[value]
        return "Witch2"
    
    def get_enabled_check_types(self) -> list[tuple[str, str]]:
        """Return the check types enabled for this seed. 'First Clear' is always on."""
        enabled: list[tuple[str, str]] = [("First Clear", "first")]
        if self.options.enable_nohit_clear.value:
            enabled.append(("No-Hit Clear", "nohit"))
        if self.options.enable_notalisman_clear.value:
            enabled.append(("No-Talisman Clear", "notalisman"))
        if self.options.enable_flawless_clear.value:
            enabled.append(("Flawless Clear", "flawless"))
        if self.options.enable_rank_clear.value:
            enabled.append(("Target Rank Clear", "rank"))
        return enabled