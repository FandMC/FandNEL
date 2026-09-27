# Data Component `minecraft:fire_resistant`

- Game version: `1.20.6`
- Registry protocol ID: `21`
- Category: `item_stack`
- Ownership: ItemStack Data Component map
- Registry JSON Pointer: `#/minecraft:data_component_type/entries/minecraft:fire_resistant`
- Wiki revision: `2595602` (`2024-06-08T11:57:48Z`)

## Target-version facts

Registry membership, numeric IDs, default values, and reverse usage in this document come only from the target version's official Mojang reports.

The official reports do not expose the complete payload codec schema. Dynamic Wiki template output is deliberately not copied into this per-entry page because historical page revisions can render later template edits. The full recorded Wiki page remains linked below as a reference, not as the target-version schema oracle.

## Default item usage

Items whose official default component map contains this component: `13`.

| Item | Exact default value |
| --- | --- |
| `minecraft:ancient_debris` | <code>{}</code> |
| `minecraft:netherite_axe` | <code>{}</code> |
| `minecraft:netherite_block` | <code>{}</code> |
| `minecraft:netherite_boots` | <code>{}</code> |
| `minecraft:netherite_chestplate` | <code>{}</code> |
| `minecraft:netherite_helmet` | <code>{}</code> |
| `minecraft:netherite_hoe` | <code>{}</code> |
| `minecraft:netherite_ingot` | <code>{}</code> |
| `minecraft:netherite_leggings` | <code>{}</code> |
| `minecraft:netherite_pickaxe` | <code>{}</code> |
| `minecraft:netherite_scrap` | <code>{}</code> |
| `minecraft:netherite_shovel` | <code>{}</code> |
| `minecraft:netherite_sword` | <code>{}</code> |

## Related block items

- `minecraft:ancient_debris`
- `minecraft:netherite_block`

## Direct entity association

No entity-specific component ID is associated directly. Entities do not own an ItemStack Data Component map.

## Official registry entry

```json
{
  "protocol_id": 21
}
```

## Machine-readable sources

- [Official registries report](../../../official-reports/registries.json)
- [Component registry JSON](../../../data-component-types.json)
- [Component-to-items JSON](../../../component-to-items.json)
- [Version-pinned Wiki document](../../../../data-component-format.md)
- [Source authority and limitations](../../../SOURCE-LIMITATIONS.md)

Wiki text is attributed to Minecraft Wiki under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Registry IDs and defaults come from Mojang's official server reports.
