# Data Component `minecraft:provides_banner_patterns`

- Game version: `1.21.8`
- Registry protocol ID: `56`
- Category: `item_stack`
- Ownership: ItemStack Data Component map
- Registry JSON Pointer: `#/minecraft:data_component_type/entries/minecraft:provides_banner_patterns`
- Wiki revision: `3155973` (`2025-09-16T15:03:24Z`)

## Target-version facts

Registry membership, numeric IDs, default values, and reverse usage in this document come only from the target version's official Mojang reports.

The official reports do not expose the complete payload codec schema. Dynamic Wiki template output is deliberately not copied into this per-entry page because historical page revisions can render later template edits. The full recorded Wiki page remains linked below as a reference, not as the target-version schema oracle.

## Default item usage

Items whose official default component map contains this component: `10`.

| Item | Exact default value |
| --- | --- |
| `minecraft:bordure_indented_banner_pattern` | <code>"#minecraft:pattern_item/bordure_indented"</code> |
| `minecraft:creeper_banner_pattern` | <code>"#minecraft:pattern_item/creeper"</code> |
| `minecraft:field_masoned_banner_pattern` | <code>"#minecraft:pattern_item/field_masoned"</code> |
| `minecraft:flow_banner_pattern` | <code>"#minecraft:pattern_item/flow"</code> |
| `minecraft:flower_banner_pattern` | <code>"#minecraft:pattern_item/flower"</code> |
| `minecraft:globe_banner_pattern` | <code>"#minecraft:pattern_item/globe"</code> |
| `minecraft:guster_banner_pattern` | <code>"#minecraft:pattern_item/guster"</code> |
| `minecraft:mojang_banner_pattern` | <code>"#minecraft:pattern_item/mojang"</code> |
| `minecraft:piglin_banner_pattern` | <code>"#minecraft:pattern_item/piglin"</code> |
| `minecraft:skull_banner_pattern` | <code>"#minecraft:pattern_item/skull"</code> |

## Related block items

No block item installs this component by default.

## Direct entity association

No entity-specific component ID is associated directly. Entities do not own an ItemStack Data Component map.

## Official registry entry

```json
{
  "protocol_id": 56
}
```

## Machine-readable sources

- [Official registries report](../../../official-reports/registries.json)
- [Component registry JSON](../../../data-component-types.json)
- [Component-to-items JSON](../../../component-to-items.json)
- [Version-pinned Wiki document](../../../../data-component-format.md)
- [Source authority and limitations](../../../SOURCE-LIMITATIONS.md)

Wiki text is attributed to Minecraft Wiki under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Registry IDs and defaults come from Mojang's official server reports.
