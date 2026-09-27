# Data Component `minecraft:chicken/variant`

- Game version: `1.21.8`
- Registry protocol ID: `86`
- Category: `entity_variant`
- Ownership: ItemStack Data Component map
- Registry JSON Pointer: `#/minecraft:data_component_type/entries/minecraft:chicken~1variant`
- Wiki revision: `3155973` (`2025-09-16T15:03:24Z`)

## Target-version facts

Registry membership, numeric IDs, default values, and reverse usage in this document come only from the target version's official Mojang reports.

The official reports do not expose the complete payload codec schema. Dynamic Wiki template output is deliberately not copied into this per-entry page because historical page revisions can render later template edits. The full recorded Wiki page remains linked below as a reference, not as the target-version schema oracle.

## Default item usage

Items whose official default component map contains this component: `3`.

| Item | Exact default value |
| --- | --- |
| `minecraft:blue_egg` | <code>"minecraft:cold"</code> |
| `minecraft:brown_egg` | <code>"minecraft:warm"</code> |
| `minecraft:egg` | <code>"minecraft:temperate"</code> |

## Related block items

No block item installs this component by default.

## Direct entity association

- `minecraft:chicken`

## Official registry entry

```json
{
  "protocol_id": 86
}
```

## Machine-readable sources

- [Official registries report](../../../../official-reports/registries.json)
- [Component registry JSON](../../../../data-component-types.json)
- [Component-to-items JSON](../../../../component-to-items.json)
- [Version-pinned Wiki document](../../../../../data-component-format.md)
- [Source authority and limitations](../../../../SOURCE-LIMITATIONS.md)

Wiki text is attributed to Minecraft Wiki under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Registry IDs and defaults come from Mojang's official server reports.
