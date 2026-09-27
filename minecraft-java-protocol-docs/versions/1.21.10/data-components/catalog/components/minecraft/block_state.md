# Data Component `minecraft:block_state`

- Game version: `1.21.10`
- Registry protocol ID: `67`
- Category: `item_stack`
- Ownership: ItemStack Data Component map
- Registry JSON Pointer: `#/minecraft:data_component_type/entries/minecraft:block_state`
- Wiki revision: `3185065` (`2025-10-05T16:00:49Z`)

## Target-version facts

Registry membership, numeric IDs, default values, and reverse usage in this document come only from the target version's official Mojang reports.

The official reports do not expose the complete payload codec schema. Dynamic Wiki template output is deliberately not copied into this per-entry page because historical page revisions can render later template edits. The full recorded Wiki page remains linked below as a reference, not as the target-version schema oracle.

## Default item usage

Items whose official default component map contains this component: `12`.

| Item | Exact default value |
| --- | --- |
| `minecraft:bee_nest` | <code>{"honey_level":"0"}</code> |
| `minecraft:beehive` | <code>{"honey_level":"0"}</code> |
| `minecraft:copper_golem_statue` | <code>{"copper_golem_pose":"standing"}</code> |
| `minecraft:exposed_copper_golem_statue` | <code>{"copper_golem_pose":"standing"}</code> |
| `minecraft:light` | <code>{"level":"15"}</code> |
| `minecraft:oxidized_copper_golem_statue` | <code>{"copper_golem_pose":"standing"}</code> |
| `minecraft:test_block` | <code>{"mode":"start"}</code> |
| `minecraft:waxed_copper_golem_statue` | <code>{"copper_golem_pose":"standing"}</code> |
| `minecraft:waxed_exposed_copper_golem_statue` | <code>{"copper_golem_pose":"standing"}</code> |
| `minecraft:waxed_oxidized_copper_golem_statue` | <code>{"copper_golem_pose":"standing"}</code> |
| `minecraft:waxed_weathered_copper_golem_statue` | <code>{"copper_golem_pose":"standing"}</code> |
| `minecraft:weathered_copper_golem_statue` | <code>{"copper_golem_pose":"standing"}</code> |

## Related block items

- `minecraft:bee_nest`
- `minecraft:beehive`
- `minecraft:copper_golem_statue`
- `minecraft:exposed_copper_golem_statue`
- `minecraft:light`
- `minecraft:oxidized_copper_golem_statue`
- `minecraft:test_block`
- `minecraft:waxed_copper_golem_statue`
- `minecraft:waxed_exposed_copper_golem_statue`
- `minecraft:waxed_oxidized_copper_golem_statue`
- `minecraft:waxed_weathered_copper_golem_statue`
- `minecraft:weathered_copper_golem_statue`

## Direct entity association

No entity-specific component ID is associated directly. Entities do not own an ItemStack Data Component map.

## Official registry entry

```json
{
  "protocol_id": 67
}
```

## Machine-readable sources

- [Official registries report](../../../official-reports/registries.json)
- [Component registry JSON](../../../data-component-types.json)
- [Component-to-items JSON](../../../component-to-items.json)
- [Version-pinned Wiki document](../../../../data-component-format.md)
- [Source authority and limitations](../../../SOURCE-LIMITATIONS.md)

Wiki text is attributed to Minecraft Wiki under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Registry IDs and defaults come from Mojang's official server reports.
