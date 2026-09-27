# Data Component `minecraft:tool`

- Game version: `1.21.10`
- Registry protocol ID: `25`
- Category: `item_stack`
- Ownership: ItemStack Data Component map
- Registry JSON Pointer: `#/minecraft:data_component_type/entries/minecraft:tool`
- Wiki revision: `3185065` (`2025-10-05T16:00:49Z`)

## Target-version facts

Registry membership, numeric IDs, default values, and reverse usage in this document come only from the target version's official Mojang reports.

The official reports do not expose the complete payload codec schema. Dynamic Wiki template output is deliberately not copied into this per-entry page because historical page revisions can render later template edits. The full recorded Wiki page remains linked below as a reference, not as the target-version schema oracle.

## Default item usage

Items whose official default component map contains this component: `38`.

| Item | Exact default value |
| --- | --- |
| `minecraft:copper_axe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_copper_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/axe","correct_for_drops":true,"speed":5.0}]}</code> |
| `minecraft:copper_hoe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_copper_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/hoe","correct_for_drops":true,"speed":5.0}]}</code> |
| `minecraft:copper_pickaxe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_copper_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/pickaxe","correct_for_drops":true,"speed":5.0}]}</code> |
| `minecraft:copper_shovel` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_copper_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/shovel","correct_for_drops":true,"speed":5.0}]}</code> |
| `minecraft:copper_sword` | <code>{"can_destroy_blocks_in_creative":false,"damage_per_block":2,"rules":[{"blocks":"minecraft:cobweb","correct_for_drops":true,"speed":15.0},{"blocks":"#minecraft:sword_instantly_mines","speed":3.4028235e+38},{"blocks":"#minecraft:sword_efficient","speed":1.5}]}</code> |
| `minecraft:diamond_axe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_diamond_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/axe","correct_for_drops":true,"speed":8.0}]}</code> |
| `minecraft:diamond_hoe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_diamond_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/hoe","correct_for_drops":true,"speed":8.0}]}</code> |
| `minecraft:diamond_pickaxe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_diamond_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/pickaxe","correct_for_drops":true,"speed":8.0}]}</code> |
| `minecraft:diamond_shovel` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_diamond_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/shovel","correct_for_drops":true,"speed":8.0}]}</code> |
| `minecraft:diamond_sword` | <code>{"can_destroy_blocks_in_creative":false,"damage_per_block":2,"rules":[{"blocks":"minecraft:cobweb","correct_for_drops":true,"speed":15.0},{"blocks":"#minecraft:sword_instantly_mines","speed":3.4028235e+38},{"blocks":"#minecraft:sword_efficient","speed":1.5}]}</code> |
| `minecraft:golden_axe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_gold_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/axe","correct_for_drops":true,"speed":12.0}]}</code> |
| `minecraft:golden_hoe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_gold_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/hoe","correct_for_drops":true,"speed":12.0}]}</code> |
| `minecraft:golden_pickaxe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_gold_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/pickaxe","correct_for_drops":true,"speed":12.0}]}</code> |
| `minecraft:golden_shovel` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_gold_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/shovel","correct_for_drops":true,"speed":12.0}]}</code> |
| `minecraft:golden_sword` | <code>{"can_destroy_blocks_in_creative":false,"damage_per_block":2,"rules":[{"blocks":"minecraft:cobweb","correct_for_drops":true,"speed":15.0},{"blocks":"#minecraft:sword_instantly_mines","speed":3.4028235e+38},{"blocks":"#minecraft:sword_efficient","speed":1.5}]}</code> |
| `minecraft:iron_axe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_iron_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/axe","correct_for_drops":true,"speed":6.0}]}</code> |
| `minecraft:iron_hoe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_iron_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/hoe","correct_for_drops":true,"speed":6.0}]}</code> |
| `minecraft:iron_pickaxe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_iron_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/pickaxe","correct_for_drops":true,"speed":6.0}]}</code> |
| `minecraft:iron_shovel` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_iron_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/shovel","correct_for_drops":true,"speed":6.0}]}</code> |
| `minecraft:iron_sword` | <code>{"can_destroy_blocks_in_creative":false,"damage_per_block":2,"rules":[{"blocks":"minecraft:cobweb","correct_for_drops":true,"speed":15.0},{"blocks":"#minecraft:sword_instantly_mines","speed":3.4028235e+38},{"blocks":"#minecraft:sword_efficient","speed":1.5}]}</code> |
| `minecraft:mace` | <code>{"can_destroy_blocks_in_creative":false,"damage_per_block":2,"rules":[]}</code> |
| `minecraft:netherite_axe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_netherite_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/axe","correct_for_drops":true,"speed":9.0}]}</code> |
| `minecraft:netherite_hoe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_netherite_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/hoe","correct_for_drops":true,"speed":9.0}]}</code> |
| `minecraft:netherite_pickaxe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_netherite_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/pickaxe","correct_for_drops":true,"speed":9.0}]}</code> |
| `minecraft:netherite_shovel` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_netherite_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/shovel","correct_for_drops":true,"speed":9.0}]}</code> |
| `minecraft:netherite_sword` | <code>{"can_destroy_blocks_in_creative":false,"damage_per_block":2,"rules":[{"blocks":"minecraft:cobweb","correct_for_drops":true,"speed":15.0},{"blocks":"#minecraft:sword_instantly_mines","speed":3.4028235e+38},{"blocks":"#minecraft:sword_efficient","speed":1.5}]}</code> |
| `minecraft:shears` | <code>{"rules":[{"blocks":"minecraft:cobweb","correct_for_drops":true,"speed":15.0},{"blocks":"#minecraft:leaves","speed":15.0},{"blocks":"#minecraft:wool","speed":5.0},{"blocks":["minecraft:vine","minecraft:glow_lichen"],"speed":2.0}]}</code> |
| `minecraft:stone_axe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_stone_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/axe","correct_for_drops":true,"speed":4.0}]}</code> |
| `minecraft:stone_hoe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_stone_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/hoe","correct_for_drops":true,"speed":4.0}]}</code> |
| `minecraft:stone_pickaxe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_stone_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/pickaxe","correct_for_drops":true,"speed":4.0}]}</code> |
| `minecraft:stone_shovel` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_stone_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/shovel","correct_for_drops":true,"speed":4.0}]}</code> |
| `minecraft:stone_sword` | <code>{"can_destroy_blocks_in_creative":false,"damage_per_block":2,"rules":[{"blocks":"minecraft:cobweb","correct_for_drops":true,"speed":15.0},{"blocks":"#minecraft:sword_instantly_mines","speed":3.4028235e+38},{"blocks":"#minecraft:sword_efficient","speed":1.5}]}</code> |
| `minecraft:trident` | <code>{"can_destroy_blocks_in_creative":false,"damage_per_block":2,"rules":[]}</code> |
| `minecraft:wooden_axe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_wooden_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/axe","correct_for_drops":true,"speed":2.0}]}</code> |
| `minecraft:wooden_hoe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_wooden_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/hoe","correct_for_drops":true,"speed":2.0}]}</code> |
| `minecraft:wooden_pickaxe` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_wooden_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/pickaxe","correct_for_drops":true,"speed":2.0}]}</code> |
| `minecraft:wooden_shovel` | <code>{"rules":[{"blocks":"#minecraft:incorrect_for_wooden_tool","correct_for_drops":false},{"blocks":"#minecraft:mineable/shovel","correct_for_drops":true,"speed":2.0}]}</code> |
| `minecraft:wooden_sword` | <code>{"can_destroy_blocks_in_creative":false,"damage_per_block":2,"rules":[{"blocks":"minecraft:cobweb","correct_for_drops":true,"speed":15.0},{"blocks":"#minecraft:sword_instantly_mines","speed":3.4028235e+38},{"blocks":"#minecraft:sword_efficient","speed":1.5}]}</code> |

## Related block items

No block item installs this component by default.

## Direct entity association

No entity-specific component ID is associated directly. Entities do not own an ItemStack Data Component map.

## Official registry entry

```json
{
  "protocol_id": 25
}
```

## Machine-readable sources

- [Official registries report](../../../official-reports/registries.json)
- [Component registry JSON](../../../data-component-types.json)
- [Component-to-items JSON](../../../component-to-items.json)
- [Version-pinned Wiki document](../../../../data-component-format.md)
- [Source authority and limitations](../../../SOURCE-LIMITATIONS.md)

Wiki text is attributed to Minecraft Wiki under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Registry IDs and defaults come from Mojang's official server reports.
