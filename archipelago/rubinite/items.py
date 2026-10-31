"""Items for Rubinite."""

from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import Item, ItemClassification

from . import data

if TYPE_CHECKING:
    from .world import RubiniteWorld


# All 13 possible sigils, explicitly assigned fixed IDs.
# This mapping must NEVER change; otherwise, existing seeds will become incompatible.
SIGIL_IDS = {
    "Beast Sigil":         data.SIGIL_ITEM_BASE + 0,   # Beast (Fallback)
    "Guardian Sigil":      data.SIGIL_ITEM_BASE + 1,   # Guardian (Fallback)
    "Cult Sigil":          data.SIGIL_ITEM_BASE + 2,   # Cult (Fallback)
    "Gravekeeper Sigil":   data.SIGIL_ITEM_BASE + 3,   # Gravekeeper (Fallback)
    "Plaguebearer Sigil":  data.SIGIL_ITEM_BASE + 4,   # PlagueRoot
    "Nestling Sigil":      data.SIGIL_ITEM_BASE + 5,   # Nestling
    "Faye II Sigil":       data.SIGIL_ITEM_BASE + 6,   # Guardian2
    "Ghost Sigil":         data.SIGIL_ITEM_BASE + 7,   # Ghost
    "Hunter Sigil":        data.SIGIL_ITEM_BASE + 8,   # Master
    "EyesTree Sigil":      data.SIGIL_ITEM_BASE + 9,   # EyesTree
    "Puppet Master Sigil": data.SIGIL_ITEM_BASE + 10,  # Queen
    "Witch Sigil":         data.SIGIL_ITEM_BASE + 11,  # Witch
    "Witch II Sigil":      data.SIGIL_ITEM_BASE + 12,  # Witch2
}


def _build_names() -> dict[str, int]:
    names: dict[str, int] = dict(SIGIL_IDS)
    for i, (title, _talisman_id) in enumerate(data.TALISMANS):
        names[data.talisman_item_name(title)] = data.TALISMAN_ITEM_BASE + i
    for i, (tree_id, title) in enumerate(data.SKILLS):
        names[data.skill_item_name(tree_id, title)] = data.SKILL_ITEM_BASE + i
    names[data.FILLER_ITEM] = data.FILLER_ITEM_ID
    return names


ITEM_NAME_TO_ID = _build_names()

# ALL sigils can be upgraded—including the fallback sigils, once they're in the pool.
PROGRESSION_ITEMS = set(SIGIL_IDS.keys())


class RubiniteItem(Item):
    game = "Rubinite"


def create_item_with_correct_classification(world: RubiniteWorld, name: str) -> RubiniteItem:
    if name in PROGRESSION_ITEMS:
        classification = ItemClassification.progression
    elif name == data.FILLER_ITEM:
        classification = ItemClassification.filler
    else:
        classification = ItemClassification.useful
    return RubiniteItem(name, classification, ITEM_NAME_TO_ID[name], world.player)


def get_random_filler_item_name(_world: RubiniteWorld) -> str:
    return data.FILLER_ITEM


def create_all_items(world: RubiniteWorld) -> None:
    itempool: list[Item] = []
    for key, _name, _level, _vanilla_sigil in data.BOSSES:
        if key in world._starting_boss_keys:
            continue
        sigil = data.sigil_for_boss_key_or_fallback(key)
        if sigil:
            itempool.append(world.create_item(sigil))
    for title, _talisman_id in data.TALISMANS:
        itempool.append(world.create_item(data.talisman_item_name(title)))
    for tree_id, title in data.SKILLS:
        itempool.append(world.create_item(data.skill_item_name(tree_id, title)))

    unfilled_locations = len(world.multiworld.get_unfilled_locations(world.player))

    # Trim useful items if the pool exceeds available locations.
    # Sigils are progression and must never be dropped. Talismans and skills
    # are useful, so trimming them keeps the seed solvable.
    if len(itempool) > unfilled_locations:
        progression = [i for i in itempool if i.advancement]
        useful = [i for i in itempool if not i.advancement]
        keep = unfilled_locations - len(progression)
        if keep < 0:
            raise ValueError(
                f"Rubinite has more progression items ({len(progression)}) "
                f"than locations ({unfilled_locations}). "
                f"Enable more checks or reduce the starting boss count."
            )
        itempool = progression + useful[:keep]

    missing = unfilled_locations - len(itempool)
    itempool.extend(world.create_filler() for _ in range(missing))

    world.multiworld.itempool += itempool