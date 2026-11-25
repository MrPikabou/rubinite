"""Logic rules for Rubinite."""

from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import ItemClassification

from . import data

if TYPE_CHECKING:
    from .world import RubiniteWorld


def _not_progression(item) -> bool:
    return not item.advancement


def _boss_clear_count(state, world: RubiniteWorld, exclude_goal: bool = True) -> int:
    return sum(
        state.has(data.boss_clear_item_name(key), world.player)
        for key, _name, _level, _sigil in world.active_bosses
        if not exclude_goal or key != world.get_goal_boss_key()
    )


def _has_boss_clear(state, world: RubiniteWorld) -> bool:
    return _boss_clear_count(state, world, exclude_goal=False) > 0


def set_all_rules(world: RubiniteWorld) -> None:
    enabled_checks = world.get_enabled_check_types()
    for key, boss_name, _level, _sigil in world.active_bosses:
        boss_checks = enabled_checks
        if key in data.RANKLESS_BOSSES:
            boss_checks = [c for c in enabled_checks if c[1] != "rank"]

        for suffix, slug in boss_checks:
            location = world.get_location(data.location_name(boss_name, suffix))
            if slug in data.OPTIONAL_CHECKS:
                location.item_rule = _not_progression

    has_boss_clear = lambda state: _has_boss_clear(state, world)
    if not world.options.progressive_shop.value:
        for title, _talisman_id in data.TALISMANS:
            location = world.get_location(data.shop_location_name(title))
            location.access_rule = has_boss_clear

    if world.options.progressive_shop.value:
        id_to_title = {tid: t for t, tid in data.TALISMANS}
        for group_index, group in enumerate(world._shop_groups):
            required = max(
                0, group_index + 1 - world.options.starting_shop_groups.value
            )
            for talisman_id in group:
                title = id_to_title[talisman_id]
                loc = world.get_location(data.shop_location_name(title))
                loc.access_rule = (
                    lambda state, c=required: (
                        c == 0
                        or state.has(data.SHOP_EXPANSION_ITEM, world.player, c)
                    )
                )

    for shop_index in range(len(data.TALISMANS), world.max_shop_slots):
        location = world.get_location(f"Wichiwi Shop: Slot {shop_index + 1}")
        shop_number = shop_index + 1
        base_slots = world.options.base_shop_slots.value

        if world.options.progressive_shop.value:
            slots_per_expansion = world.options.slots_per_shop_expansion.value
            slots_above_base = max(0, shop_number - base_slots)
            required_expansions = (
                (slots_above_base + slots_per_expansion - 1) // slots_per_expansion
                if slots_per_expansion > 0
                else 0
            )
            location.access_rule = (
                lambda state, c=required_expansions: (
                    c == 0
                    or state.has(data.SHOP_EXPANSION_ITEM, world.player, c)
                )
            )
        else:
            slots_per_boss = world.options.slots_per_boss_kill.value
            slots_above_base = max(0, shop_number - base_slots)
            required_boss_clears = (
                (slots_above_base + slots_per_boss - 1) // slots_per_boss
                if slots_per_boss > 0
                else 0
            )
            location.access_rule = (
                lambda state, c=required_boss_clears: (
                    has_boss_clear(state)
                    and _boss_clear_count(state, world) >= c
                )
            )

    for tree_id, title in data.SKILLS:
        loc = world.get_location(data.skill_tree_location_name(tree_id, title))
        loc.access_rule = has_boss_clear

    def _goal_condition(state):
        goal_key = world.get_goal_boss_key()
        goal_name = next(
            name for key, name, _l, _s in world.active_bosses if key == goal_key
        )

        if not state.can_reach(goal_name, "Region", world.player):
            return False

        required = world.options.goal_boss_kills_required.value
        if required <= 0:
            return True

        return _boss_clear_count(state, world) >= required

    goal_key = world.get_goal_boss_key()
    goal_name = next(name for key, name, _l, _s in world.active_bosses if key == goal_key)
    
    victory_loc = world.get_location(f"{goal_name} Defeated")
    victory_loc.access_rule = _goal_condition
    
    if victory_loc.item:
        victory_loc.item.classification = ItemClassification.progression

    world.multiworld.completion_condition[world.player] = (
        lambda state: state.has("Victory", world.player)
    )