# Data Component `minecraft:weapon`

- Game version: `1.21.10`
- Registry protocol ID: `26`
- Category: `item_stack`
- Ownership: ItemStack Data Component map
- Registry JSON Pointer: `#/minecraft:data_component_type/entries/minecraft:weapon`
- Wiki revision: `3185065` (`2025-10-05T16:00:49Z`)

## Target-version facts

Registry membership, numeric IDs, default values, and reverse usage in this document come only from the target version's official Mojang reports.

The official reports do not expose the complete payload codec schema. Dynamic Wiki template output is deliberately not copied into this per-entry page because historical page revisions can render later template edits. The full recorded Wiki page remains linked below as a reference, not as the target-version schema oracle.

## Default item usage

Items whose official default component map contains this component: `37`.

| Item | Exact default value |
| --- | --- |
| `minecraft:copper_axe` | <code>{"disable_blocking_for_seconds":5.0,"item_damage_per_attack":2}</code> |
| `minecraft:copper_hoe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:copper_pickaxe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:copper_shovel` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:copper_sword` | <code>{}</code> |
| `minecraft:diamond_axe` | <code>{"disable_blocking_for_seconds":5.0,"item_damage_per_attack":2}</code> |
| `minecraft:diamond_hoe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:diamond_pickaxe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:diamond_shovel` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:diamond_sword` | <code>{}</code> |
| `minecraft:golden_axe` | <code>{"disable_blocking_for_seconds":5.0,"item_damage_per_attack":2}</code> |
| `minecraft:golden_hoe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:golden_pickaxe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:golden_shovel` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:golden_sword` | <code>{}</code> |
| `minecraft:iron_axe` | <code>{"disable_blocking_for_seconds":5.0,"item_damage_per_attack":2}</code> |
| `minecraft:iron_hoe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:iron_pickaxe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:iron_shovel` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:iron_sword` | <code>{}</code> |
| `minecraft:mace` | <code>{}</code> |
| `minecraft:netherite_axe` | <code>{"disable_blocking_for_seconds":5.0,"item_damage_per_attack":2}</code> |
| `minecraft:netherite_hoe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:netherite_pickaxe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:netherite_shovel` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:netherite_sword` | <code>{}</code> |
| `minecraft:stone_axe` | <code>{"disable_blocking_for_seconds":5.0,"item_damage_per_attack":2}</code> |
| `minecraft:stone_hoe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:stone_pickaxe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:stone_shovel` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:stone_sword` | <code>{}</code> |
| `minecraft:trident` | <code>{}</code> |
| `minecraft:wooden_axe` | <code>{"disable_blocking_for_seconds":5.0,"item_damage_per_attack":2}</code> |
| `minecraft:wooden_hoe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:wooden_pickaxe` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:wooden_shovel` | <code>{"item_damage_per_attack":2}</code> |
| `minecraft:wooden_sword` | <code>{}</code> |

## Related block items

No block item installs this component by default.

## Direct entity association

No entity-specific component ID is associated directly. Entities do not own an ItemStack Data Component map.

## Official registry entry

```json
{
  "protocol_id": 26
}
```

## Machine-readable sources

- [Official registries report](../../../official-reports/registries.json)
- [Component registry JSON](../../../data-component-types.json)
- [Component-to-items JSON](../../../component-to-items.json)
- [Version-pinned Wiki document](../../../../data-component-format.md)
- [Source authority and limitations](../../../SOURCE-LIMITATIONS.md)

Wiki text is attributed to Minecraft Wiki under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Registry IDs and defaults come from Mojang's official server reports.
