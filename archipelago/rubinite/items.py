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
    for name, id_offset, _w, _d, _i in data.FILLER_ITEMS:
        names[name] = data.FILLER_ITEM_BASE + id_offset
    names[data.SHOP_EXPANSION_ITEM] = data.SHOP_EXPANSION_ITEM_ID
    return names


ITEM_NAME_TO_ID = _build_names()

# All sigils are progression. Shop Expansions are progression too, because the
# shop locations they unlock can themselves contain progression (further sigils
# or more Shop Expansions). Everything else is useful or filler.
PROGRESSION_ITEMS = set(SIGIL_IDS.keys()) | {data.SHOP_EXPANSION_ITEM}
FILLER_ITEM_NAMES = {name for name, _id, _w, _d, _i in data.FILLER_ITEMS}


class RubiniteItem(Item):
    game = "Rubinite"


def create_item_with_correct_classification(world: RubiniteWorld, name: str) -> RubiniteItem:
    if name in PROGRESSION_ITEMS:
        classification = ItemClassification.progression
    elif name in FILLER_ITEM_NAMES:
        classification = ItemClassification.filler
    else:
        classification = ItemClassification.useful
    return RubiniteItem(name, classification, ITEM_NAME_TO_ID[name], world.player)


def get_random_filler_item_name(world: RubiniteWorld) -> str:
    names = [n for n, _id, _w, _d, _i in data.FILLER_ITEMS]
    weights = [w for _n, _id, w, _d, _i in data.FILLER_ITEMS]
    return world.random.choices(names, weights=weights, k=1)[0]


def create_all_items(world: RubiniteWorld) -> None:
    itempool: list[Item] = []
    for key, _name, _level, _vanilla_sigil in data.BOSSES:
        if key in world._starting_boss_keys:
            continue
        sigil = data.sigil_for_boss_key_or_fallback(key)
        if sigil:
            itempool.append(world.create_item(sigil))

    # Skills before talismans: if trimming is needed, permanent skill-tree
    # upgrades are more impactful than single-slot talisman perks. A trimmed
    # talisman can still be bought in the shop (its location check still fires,
    # granting an AP item) — it just can't be equipped.
    for tree_id, title in data.SKILLS:
        itempool.append(world.create_item(data.skill_item_name(tree_id, title)))
    for title, _talisman_id in data.TALISMANS:
        itempool.append(world.create_item(data.talisman_item_name(title)))

    if world.options.progressive_shop.value:
        starting = world.options.starting_shop_groups.value
        expansions_needed = max(0, data.SHOP_GROUPS - starting)
        for _ in range(expansions_needed):
            itempool.append(world.create_item(data.SHOP_EXPANSION_ITEM))

    unfilled_locations = len(world.multiworld.get_unfilled_locations(world.player))

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