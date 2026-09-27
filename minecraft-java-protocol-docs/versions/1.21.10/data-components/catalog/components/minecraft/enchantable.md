# Data Component `minecraft:enchantable`

- Game version: `1.21.10`
- Registry protocol ID: `27`
- Category: `item_stack`
- Ownership: ItemStack Data Component map
- Registry JSON Pointer: `#/minecraft:data_component_type/entries/minecraft:enchantable`
- Wiki revision: `3185065` (`2025-10-05T16:00:49Z`)

## Target-version facts

Registry membership, numeric IDs, default values, and reverse usage in this document come only from the target version's official Mojang reports.

The official reports do not expose the complete payload codec schema. Dynamic Wiki template output is deliberately not copied into this per-entry page because historical page revisions can render later template edits. The full recorded Wiki page remains linked below as a reference, not as the target-version schema oracle.

## Default item usage

Items whose official default component map contains this component: `70`.

| Item | Exact default value |
| --- | --- |
| `minecraft:book` | <code>{"value":1}</code> |
| `minecraft:bow` | <code>{"value":1}</code> |
| `minecraft:chainmail_boots` | <code>{"value":12}</code> |
| `minecraft:chainmail_chestplate` | <code>{"value":12}</code> |
| `minecraft:chainmail_helmet` | <code>{"value":12}</code> |
| `minecraft:chainmail_leggings` | <code>{"value":12}</code> |
| `minecraft:copper_axe` | <code>{"value":13}</code> |
| `minecraft:copper_boots` | <code>{"value":8}</code> |
| `minecraft:copper_chestplate` | <code>{"value":8}</code> |
| `minecraft:copper_helmet` | <code>{"value":8}</code> |
| `minecraft:copper_hoe` | <code>{"value":13}</code> |
| `minecraft:copper_leggings` | <code>{"value":8}</code> |
| `minecraft:copper_pickaxe` | <code>{"value":13}</code> |
| `minecraft:copper_shovel` | <code>{"value":13}</code> |
| `minecraft:copper_sword` | <code>{"value":13}</code> |
| `minecraft:crossbow` | <code>{"value":1}</code> |
| `minecraft:diamond_axe` | <code>{"value":10}</code> |
| `minecraft:diamond_boots` | <code>{"value":10}</code> |
| `minecraft:diamond_chestplate` | <code>{"value":10}</code> |
| `minecraft:diamond_helmet` | <code>{"value":10}</code> |
| `minecraft:diamond_hoe` | <code>{"value":10}</code> |
| `minecraft:diamond_leggings` | <code>{"value":10}</code> |
| `minecraft:diamond_pickaxe` | <code>{"value":10}</code> |
| `minecraft:diamond_shovel` | <code>{"value":10}</code> |
| `minecraft:diamond_sword` | <code>{"value":10}</code> |
| `minecraft:fishing_rod` | <code>{"value":1}</code> |
| `minecraft:golden_axe` | <code>{"value":22}</code> |
| `minecraft:golden_boots` | <code>{"value":25}</code> |
| `minecraft:golden_chestplate` | <code>{"value":25}</code> |
| `minecraft:golden_helmet` | <code>{"value":25}</code> |
| `minecraft:golden_hoe` | <code>{"value":22}</code> |
| `minecraft:golden_leggings` | <code>{"value":25}</code> |
| `minecraft:golden_pickaxe` | <code>{"value":22}</code> |
| `minecraft:golden_shovel` | <code>{"value":22}</code> |
| `minecraft:golden_sword` | <code>{"value":22}</code> |
| `minecraft:iron_axe` | <code>{"value":14}</code> |
| `minecraft:iron_boots` | <code>{"value":9}</code> |
| `minecraft:iron_chestplate` | <code>{"value":9}</code> |
| `minecraft:iron_helmet` | <code>{"value":9}</code> |
| `minecraft:iron_hoe` | <code>{"value":14}</code> |
| `minecraft:iron_leggings` | <code>{"value":9}</code> |
| `minecraft:iron_pickaxe` | <code>{"value":14}</code> |
| `minecraft:iron_shovel` | <code>{"value":14}</code> |
| `minecraft:iron_sword` | <code>{"value":14}</code> |
| `minecraft:leather_boots` | <code>{"value":15}</code> |
| `minecraft:leather_chestplate` | <code>{"value":15}</code> |
| `minecraft:leather_helmet` | <code>{"value":15}</code> |
| `minecraft:leather_leggings` | <code>{"value":15}</code> |
| `minecraft:mace` | <code>{"value":15}</code> |
| `minecraft:netherite_axe` | <code>{"value":15}</code> |
| `minecraft:netherite_boots` | <code>{"value":15}</code> |
| `minecraft:netherite_chestplate` | <code>{"value":15}</code> |
| `minecraft:netherite_helmet` | <code>{"value":15}</code> |
| `minecraft:netherite_hoe` | <code>{"value":15}</code> |
| `minecraft:netherite_leggings` | <code>{"value":15}</code> |
| `minecraft:netherite_pickaxe` | <code>{"value":15}</code> |
| `minecraft:netherite_shovel` | <code>{"value":15}</code> |
| `minecraft:netherite_sword` | <code>{"value":15}</code> |
| `minecraft:stone_axe` | <code>{"value":5}</code> |
| `minecraft:stone_hoe` | <code>{"value":5}</code> |
| `minecraft:stone_pickaxe` | <code>{"value":5}</code> |
| `minecraft:stone_shovel` | <code>{"value":5}</code> |
| `minecraft:stone_sword` | <code>{"value":5}</code> |
| `minecraft:trident` | <code>{"value":1}</code> |
| `minecraft:turtle_helmet` | <code>{"value":9}</code> |
| `minecraft:wooden_axe` | <code>{"value":15}</code> |
| `minecraft:wooden_hoe` | <code>{"value":15}</code> |
| `minecraft:wooden_pickaxe` | <code>{"value":15}</code> |
| `minecraft:wooden_shovel` | <code>{"value":15}</code> |
| `minecraft:wooden_sword` | <code>{"value":15}</code> |

## Related block items

No block item installs this component by default.

## Direct entity association

No entity-specific component ID is associated directly. Entities do not own an ItemStack Data Component map.

## Official registry entry

```json
{
  "protocol_id": 27
}
```

## Machine-readable sources

- [Official registries report](../../../official-reports/registries.json)
- [Component registry JSON](../../../data-component-types.json)
- [Component-to-items JSON](../../../component-to-items.json)
- [Version-pinned Wiki document](../../../../data-component-format.md)
- [Source authority and limitations](../../../SOURCE-LIMITATIONS.md)

Wiki text is attributed to Minecraft Wiki under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Registry IDs and defaults come from Mojang's official server reports.
