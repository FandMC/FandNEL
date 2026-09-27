# Data Component `minecraft:bucket_entity_data`

- Game version: `1.21.8`
- Registry protocol ID: `50`
- Category: `item_stack`
- Ownership: ItemStack Data Component map
- Registry JSON Pointer: `#/minecraft:data_component_type/entries/minecraft:bucket_entity_data`
- Wiki revision: `3155973` (`2025-09-16T15:03:24Z`)

## Target-version facts

Registry membership, numeric IDs, default values, and reverse usage in this document come only from the target version's official Mojang reports.

The official reports do not expose the complete payload codec schema. Dynamic Wiki template output is deliberately not copied into this per-entry page because historical page revisions can render later template edits. The full recorded Wiki page remains linked below as a reference, not as the target-version schema oracle.

## Default item usage

Items whose official default component map contains this component: `6`.

| Item | Exact default value |
| --- | --- |
| `minecraft:axolotl_bucket` | <code>{}</code> |
| `minecraft:cod_bucket` | <code>{}</code> |
| `minecraft:pufferfish_bucket` | <code>{}</code> |
| `minecraft:salmon_bucket` | <code>{}</code> |
| `minecraft:tadpole_bucket` | <code>{}</code> |
| `minecraft:tropical_fish_bucket` | <code>{}</code> |

## Related block items

No block item installs this component by default.

## Direct entity association

No entity-specific component ID is associated directly. Entities do not own an ItemStack Data Component map.

## Official registry entry

```json
{
  "protocol_id": 50
}
```

## Machine-readable sources

- [Official registries report](../../../official-reports/registries.json)
- [Component registry JSON](../../../data-component-types.json)
- [Component-to-items JSON](../../../component-to-items.json)
- [Version-pinned Wiki document](../../../../data-component-format.md)
- [Source authority and limitations](../../../SOURCE-LIMITATIONS.md)

Wiki text is attributed to Minecraft Wiki under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Registry IDs and defaults come from Mojang's official server reports.
