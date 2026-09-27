# Data Component `minecraft:food`

- Game version: `1.21.8`
- Registry protocol ID: `20`
- Category: `item_stack`
- Ownership: ItemStack Data Component map
- Registry JSON Pointer: `#/minecraft:data_component_type/entries/minecraft:food`
- Wiki revision: `3155973` (`2025-09-16T15:03:24Z`)

## Target-version facts

Registry membership, numeric IDs, default values, and reverse usage in this document come only from the target version's official Mojang reports.

The official reports do not expose the complete payload codec schema. Dynamic Wiki template output is deliberately not copied into this per-entry page because historical page revisions can render later template edits. The full recorded Wiki page remains linked below as a reference, not as the target-version schema oracle.

## Default item usage

Items whose official default component map contains this component: `40`.

| Item | Exact default value |
| --- | --- |
| `minecraft:apple` | <code>{"nutrition":4,"saturation":2.4}</code> |
| `minecraft:baked_potato` | <code>{"nutrition":5,"saturation":6.0}</code> |
| `minecraft:beef` | <code>{"nutrition":3,"saturation":1.8000001}</code> |
| `minecraft:beetroot` | <code>{"nutrition":1,"saturation":1.2}</code> |
| `minecraft:beetroot_soup` | <code>{"nutrition":6,"saturation":7.2000003}</code> |
| `minecraft:bread` | <code>{"nutrition":5,"saturation":6.0}</code> |
| `minecraft:carrot` | <code>{"nutrition":3,"saturation":3.6000001}</code> |
| `minecraft:chicken` | <code>{"nutrition":2,"saturation":1.2}</code> |
| `minecraft:chorus_fruit` | <code>{"can_always_eat":true,"nutrition":4,"saturation":2.4}</code> |
| `minecraft:cod` | <code>{"nutrition":2,"saturation":0.4}</code> |
| `minecraft:cooked_beef` | <code>{"nutrition":8,"saturation":12.8}</code> |
| `minecraft:cooked_chicken` | <code>{"nutrition":6,"saturation":7.2000003}</code> |
| `minecraft:cooked_cod` | <code>{"nutrition":5,"saturation":6.0}</code> |
| `minecraft:cooked_mutton` | <code>{"nutrition":6,"saturation":9.6}</code> |
| `minecraft:cooked_porkchop` | <code>{"nutrition":8,"saturation":12.8}</code> |
| `minecraft:cooked_rabbit` | <code>{"nutrition":5,"saturation":6.0}</code> |
| `minecraft:cooked_salmon` | <code>{"nutrition":6,"saturation":9.6}</code> |
| `minecraft:cookie` | <code>{"nutrition":2,"saturation":0.4}</code> |
| `minecraft:dried_kelp` | <code>{"nutrition":1,"saturation":0.6}</code> |
| `minecraft:enchanted_golden_apple` | <code>{"can_always_eat":true,"nutrition":4,"saturation":9.6}</code> |
| `minecraft:glow_berries` | <code>{"nutrition":2,"saturation":0.4}</code> |
| `minecraft:golden_apple` | <code>{"can_always_eat":true,"nutrition":4,"saturation":9.6}</code> |
| `minecraft:golden_carrot` | <code>{"nutrition":6,"saturation":14.400001}</code> |
| `minecraft:honey_bottle` | <code>{"can_always_eat":true,"nutrition":6,"saturation":1.2}</code> |
| `minecraft:melon_slice` | <code>{"nutrition":2,"saturation":1.2}</code> |
| `minecraft:mushroom_stew` | <code>{"nutrition":6,"saturation":7.2000003}</code> |
| `minecraft:mutton` | <code>{"nutrition":2,"saturation":1.2}</code> |
| `minecraft:poisonous_potato` | <code>{"nutrition":2,"saturation":1.2}</code> |
| `minecraft:porkchop` | <code>{"nutrition":3,"saturation":1.8000001}</code> |
| `minecraft:potato` | <code>{"nutrition":1,"saturation":0.6}</code> |
| `minecraft:pufferfish` | <code>{"nutrition":1,"saturation":0.2}</code> |
| `minecraft:pumpkin_pie` | <code>{"nutrition":8,"saturation":4.8}</code> |
| `minecraft:rabbit` | <code>{"nutrition":3,"saturation":1.8000001}</code> |
| `minecraft:rabbit_stew` | <code>{"nutrition":10,"saturation":12.0}</code> |
| `minecraft:rotten_flesh` | <code>{"nutrition":4,"saturation":0.8}</code> |
| `minecraft:salmon` | <code>{"nutrition":2,"saturation":0.4}</code> |
| `minecraft:spider_eye` | <code>{"nutrition":2,"saturation":3.2}</code> |
| `minecraft:suspicious_stew` | <code>{"can_always_eat":true,"nutrition":6,"saturation":7.2000003}</code> |
| `minecraft:sweet_berries` | <code>{"nutrition":2,"saturation":0.4}</code> |
| `minecraft:tropical_fish` | <code>{"nutrition":1,"saturation":0.2}</code> |

## Related block items

No block item installs this component by default.

## Direct entity association

No entity-specific component ID is associated directly. Entities do not own an ItemStack Data Component map.

## Official registry entry

```json
{
  "protocol_id": 20
}
```

## Machine-readable sources

- [Official registries report](../../../official-reports/registries.json)
- [Component registry JSON](../../../data-component-types.json)
- [Component-to-items JSON](../../../component-to-items.json)
- [Version-pinned Wiki document](../../../../data-component-format.md)
- [Source authority and limitations](../../../SOURCE-LIMITATIONS.md)

Wiki text is attributed to Minecraft Wiki under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Registry IDs and defaults come from Mojang's official server reports.
