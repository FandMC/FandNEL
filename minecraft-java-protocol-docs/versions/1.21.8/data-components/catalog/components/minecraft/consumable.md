# Data Component `minecraft:consumable`

- Game version: `1.21.8`
- Registry protocol ID: `21`
- Category: `item_stack`
- Ownership: ItemStack Data Component map
- Registry JSON Pointer: `#/minecraft:data_component_type/entries/minecraft:consumable`
- Wiki revision: `3155973` (`2025-09-16T15:03:24Z`)

## Target-version facts

Registry membership, numeric IDs, default values, and reverse usage in this document come only from the target version's official Mojang reports.

The official reports do not expose the complete payload codec schema. Dynamic Wiki template output is deliberately not copied into this per-entry page because historical page revisions can render later template edits. The full recorded Wiki page remains linked below as a reference, not as the target-version schema oracle.

## Default item usage

Items whose official default component map contains this component: `43`.

| Item | Exact default value |
| --- | --- |
| `minecraft:apple` | <code>{}</code> |
| `minecraft:baked_potato` | <code>{}</code> |
| `minecraft:beef` | <code>{}</code> |
| `minecraft:beetroot` | <code>{}</code> |
| `minecraft:beetroot_soup` | <code>{}</code> |
| `minecraft:bread` | <code>{}</code> |
| `minecraft:carrot` | <code>{}</code> |
| `minecraft:chicken` | <code>{"on_consume_effects":[{"effects":[{"duration":600,"id":"minecraft:hunger","show_icon":true}],"probability":0.3,"type":"minecraft:apply_effects"}]}</code> |
| `minecraft:chorus_fruit` | <code>{"on_consume_effects":[{"type":"minecraft:teleport_randomly"}]}</code> |
| `minecraft:cod` | <code>{}</code> |
| `minecraft:cooked_beef` | <code>{}</code> |
| `minecraft:cooked_chicken` | <code>{}</code> |
| `minecraft:cooked_cod` | <code>{}</code> |
| `minecraft:cooked_mutton` | <code>{}</code> |
| `minecraft:cooked_porkchop` | <code>{}</code> |
| `minecraft:cooked_rabbit` | <code>{}</code> |
| `minecraft:cooked_salmon` | <code>{}</code> |
| `minecraft:cookie` | <code>{}</code> |
| `minecraft:dried_kelp` | <code>{"consume_seconds":0.8}</code> |
| `minecraft:enchanted_golden_apple` | <code>{"on_consume_effects":[{"effects":[{"amplifier":1,"duration":400,"id":"minecraft:regeneration","show_icon":true},{"duration":6000,"id":"minecraft:resistance","show_icon":true},{"duration":6000,"id":"minecraft:fire_resistance","show_icon":true},{"amplifier":3,"duration":2400,"id":"minecraft:absorption","show_icon":true}],"type":"minecraft:apply_effects"}]}</code> |
| `minecraft:glow_berries` | <code>{}</code> |
| `minecraft:golden_apple` | <code>{"on_consume_effects":[{"effects":[{"amplifier":1,"duration":100,"id":"minecraft:regeneration","show_icon":true},{"duration":2400,"id":"minecraft:absorption","show_icon":true}],"type":"minecraft:apply_effects"}]}</code> |
| `minecraft:golden_carrot` | <code>{}</code> |
| `minecraft:honey_bottle` | <code>{"animation":"drink","consume_seconds":2.0,"has_consume_particles":false,"on_consume_effects":[{"effects":"minecraft:poison","type":"minecraft:remove_effects"}],"sound":"minecraft:item.honey_bottle.drink"}</code> |
| `minecraft:melon_slice` | <code>{}</code> |
| `minecraft:milk_bucket` | <code>{"animation":"drink","has_consume_particles":false,"on_consume_effects":[{"type":"minecraft:clear_all_effects"}],"sound":"minecraft:entity.generic.drink"}</code> |
| `minecraft:mushroom_stew` | <code>{}</code> |
| `minecraft:mutton` | <code>{}</code> |
| `minecraft:ominous_bottle` | <code>{"animation":"drink","has_consume_particles":false,"on_consume_effects":[{"sound":"minecraft:item.ominous_bottle.dispose","type":"minecraft:play_sound"}],"sound":"minecraft:entity.generic.drink"}</code> |
| `minecraft:poisonous_potato` | <code>{"on_consume_effects":[{"effects":[{"duration":100,"id":"minecraft:poison","show_icon":true}],"probability":0.6,"type":"minecraft:apply_effects"}]}</code> |
| `minecraft:porkchop` | <code>{}</code> |
| `minecraft:potato` | <code>{}</code> |
| `minecraft:potion` | <code>{"animation":"drink","has_consume_particles":false,"sound":"minecraft:entity.generic.drink"}</code> |
| `minecraft:pufferfish` | <code>{"on_consume_effects":[{"effects":[{"amplifier":1,"duration":1200,"id":"minecraft:poison","show_icon":true},{"amplifier":2,"duration":300,"id":"minecraft:hunger","show_icon":true},{"duration":300,"id":"minecraft:nausea","show_icon":true}],"type":"minecraft:apply_effects"}]}</code> |
| `minecraft:pumpkin_pie` | <code>{}</code> |
| `minecraft:rabbit` | <code>{}</code> |
| `minecraft:rabbit_stew` | <code>{}</code> |
| `minecraft:rotten_flesh` | <code>{"on_consume_effects":[{"effects":[{"duration":600,"id":"minecraft:hunger","show_icon":true}],"probability":0.8,"type":"minecraft:apply_effects"}]}</code> |
| `minecraft:salmon` | <code>{}</code> |
| `minecraft:spider_eye` | <code>{"on_consume_effects":[{"effects":[{"duration":100,"id":"minecraft:poison","show_icon":true}],"type":"minecraft:apply_effects"}]}</code> |
| `minecraft:suspicious_stew` | <code>{}</code> |
| `minecraft:sweet_berries` | <code>{}</code> |
| `minecraft:tropical_fish` | <code>{}</code> |

## Related block items

No block item installs this component by default.

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
