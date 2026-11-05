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
    enabled_checks = world.get_enabled_check_types()
    for key, boss_name, _level, _sigil in world.active_bosses:
        boss_checks = enabled_checks
        if key in data.RANKLESS_BOSSES:
            boss_checks = [c for c in enabled_checks if c[1] != "rank"]

        for suffix, slug in boss_checks:
            if slug in data.OPTIONAL_CHECKS:
                location = world.get_location(data.location_name(boss_name, suffix))
                location.item_rule = _not_progression

    if world.options.progressive_shop.value:
        id_to_title = {tid: t for t, tid in data.TALISMANS}
        for group_index, group in enumerate(world._shop_groups):
            required = group_index + 1
            for talisman_id in group:
                title = id_to_title[talisman_id]
                loc = world.get_location(data.shop_location_name(title))
                loc.access_rule = (
                    lambda state, c=required: state.has(
                        data.SHOP_EXPANSION_ITEM, world.player, c
                    )
                )

    def _goal_condition(state, world=world):
        goal_key = world.get_goal_boss_key()
        goal_name = next(
            name for key, name, _l, _s in world.active_bosses if key == goal_key
        )

        if not state.can_reach(goal_name, "Region", world.player):
            return False

        required = world.options.goal_boss_kills_required.value
        if required <= 0:
            return True

        reachable = 0
        for key, name, _l, _s in world.active_bosses:
            if key == goal_key:
                continue
            if state.can_reach(name, "Region", world.player):
                reachable += 1
        return reachable >= required


    world.set_completion_rule(_goal_condition)
