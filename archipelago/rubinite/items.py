"""Items for Rubinite."""

from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import Item, ItemClassification

from . import data

if TYPE_CHECKING:
    from .world import RubiniteWorld


def _build_names() -> dict[str, int]:
    names: dict[str, int] = {}
    # Sigils are the only progression items.
    sigil_index = 0
    for _key, _name, _level, sigil in data.BOSSES:
        if sigil:
            names[sigil] = data.SIGIL_ITEM_BASE + sigil_index
            sigil_index += 1
    for i, (title, _talisman_id) in enumerate(data.TALISMANS):
        names[data.talisman_item_name(title)] = data.TALISMAN_ITEM_BASE + i
    for i, (tree_id, title) in enumerate(data.SKILLS):
        names[data.skill_item_name(tree_id, title)] = data.SKILL_ITEM_BASE + i
    names[data.FILLER_ITEM] = data.FILLER_ITEM_ID
    return names


ITEM_NAME_TO_ID = _build_names()

PROGRESSION_ITEMS = {
    sigil for _key, _name, _level, sigil in data.BOSSES if sigil
}


class RubiniteItem(Item):
    game = "Rubinite"


def create_item_with_correct_classification(world: RubiniteWorld, name: str) -> RubiniteItem:
    if name in PROGRESSION_ITEMS:
        classification = ItemClassification.progression
    elif name == data.FILLER_ITEM:
        classification = ItemClassification.filler
    else:
        # Talismans and skills make the player stronger, but no boss is logically
        # gated behind a particular one.
        classification = ItemClassification.useful
    return RubiniteItem(name, classification, ITEM_NAME_TO_ID[name], world.player)


def get_random_filler_item_name(_world: RubiniteWorld) -> str:
    return data.FILLER_ITEM


def create_all_items(world: RubiniteWorld) -> None:
    itempool: list[Item] = []
    for _key, _name, _level, sigil in data.BOSSES:
        if sigil:
            itempool.append(world.create_item(sigil))
    for title, _talisman_id in data.TALISMANS:
        itempool.append(world.create_item(data.talisman_item_name(title)))
    for tree_id, title in data.SKILLS:
        itempool.append(world.create_item(data.skill_item_name(tree_id, title)))

    unfilled_locations = len(world.multiworld.get_unfilled_locations(world.player))
    missing = unfilled_locations - len(itempool)
    if missing < 0:
        raise ValueError(
            f"Rubinite has more items ({len(itempool)}) than locations ({unfilled_locations})."
        )
    itempool.extend(world.create_filler() for _ in range(missing))

    world.multiworld.itempool += itempool
