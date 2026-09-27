# Data Component `minecraft:entity_data`

- Game version: `1.21.10`
- Registry protocol ID: `49`
- Category: `item_stack`
- Ownership: ItemStack Data Component map
- Registry JSON Pointer: `#/minecraft:data_component_type/entries/minecraft:entity_data`
- Wiki revision: `3185065` (`2025-10-05T16:00:49Z`)

## Target-version facts

Registry membership, numeric IDs, default values, and reverse usage in this document come only from the target version's official Mojang reports.

The official reports do not expose the complete payload codec schema. Dynamic Wiki template output is deliberately not copied into this per-entry page because historical page revisions can render later template edits. The full recorded Wiki page remains linked below as a reference, not as the target-version schema oracle.

## Default item usage

Items whose official default component map contains this component: `83`.

| Item | Exact default value |
| --- | --- |
| `minecraft:allay_spawn_egg` | <code>{"id":"minecraft:allay"}</code> |
| `minecraft:armadillo_spawn_egg` | <code>{"id":"minecraft:armadillo"}</code> |
| `minecraft:axolotl_spawn_egg` | <code>{"id":"minecraft:axolotl"}</code> |
| `minecraft:bat_spawn_egg` | <code>{"id":"minecraft:bat"}</code> |
| `minecraft:bee_spawn_egg` | <code>{"id":"minecraft:bee"}</code> |
| `minecraft:blaze_spawn_egg` | <code>{"id":"minecraft:blaze"}</code> |
| `minecraft:bogged_spawn_egg` | <code>{"id":"minecraft:bogged"}</code> |
| `minecraft:breeze_spawn_egg` | <code>{"id":"minecraft:breeze"}</code> |
| `minecraft:camel_spawn_egg` | <code>{"id":"minecraft:camel"}</code> |
| `minecraft:cat_spawn_egg` | <code>{"id":"minecraft:cat"}</code> |
| `minecraft:cave_spider_spawn_egg` | <code>{"id":"minecraft:cave_spider"}</code> |
| `minecraft:chicken_spawn_egg` | <code>{"id":"minecraft:chicken"}</code> |
| `minecraft:cod_spawn_egg` | <code>{"id":"minecraft:cod"}</code> |
| `minecraft:copper_golem_spawn_egg` | <code>{"id":"minecraft:copper_golem"}</code> |
| `minecraft:cow_spawn_egg` | <code>{"id":"minecraft:cow"}</code> |
| `minecraft:creaking_spawn_egg` | <code>{"id":"minecraft:creaking"}</code> |
| `minecraft:creeper_spawn_egg` | <code>{"id":"minecraft:creeper"}</code> |
| `minecraft:dolphin_spawn_egg` | <code>{"id":"minecraft:dolphin"}</code> |
| `minecraft:donkey_spawn_egg` | <code>{"id":"minecraft:donkey"}</code> |
| `minecraft:drowned_spawn_egg` | <code>{"id":"minecraft:drowned"}</code> |
| `minecraft:elder_guardian_spawn_egg` | <code>{"id":"minecraft:elder_guardian"}</code> |
| `minecraft:ender_dragon_spawn_egg` | <code>{"id":"minecraft:ender_dragon"}</code> |
| `minecraft:enderman_spawn_egg` | <code>{"id":"minecraft:enderman"}</code> |
| `minecraft:endermite_spawn_egg` | <code>{"id":"minecraft:endermite"}</code> |
| `minecraft:evoker_spawn_egg` | <code>{"id":"minecraft:evoker"}</code> |
| `minecraft:fox_spawn_egg` | <code>{"id":"minecraft:fox"}</code> |
| `minecraft:frog_spawn_egg` | <code>{"id":"minecraft:frog"}</code> |
| `minecraft:ghast_spawn_egg` | <code>{"id":"minecraft:ghast"}</code> |
| `minecraft:glow_squid_spawn_egg` | <code>{"id":"minecraft:glow_squid"}</code> |
| `minecraft:goat_spawn_egg` | <code>{"id":"minecraft:goat"}</code> |
| `minecraft:guardian_spawn_egg` | <code>{"id":"minecraft:guardian"}</code> |
| `minecraft:happy_ghast_spawn_egg` | <code>{"id":"minecraft:happy_ghast"}</code> |
| `minecraft:hoglin_spawn_egg` | <code>{"id":"minecraft:hoglin"}</code> |
| `minecraft:horse_spawn_egg` | <code>{"id":"minecraft:horse"}</code> |
| `minecraft:husk_spawn_egg` | <code>{"id":"minecraft:husk"}</code> |
| `minecraft:iron_golem_spawn_egg` | <code>{"id":"minecraft:iron_golem"}</code> |
| `minecraft:llama_spawn_egg` | <code>{"id":"minecraft:llama"}</code> |
| `minecraft:magma_cube_spawn_egg` | <code>{"id":"minecraft:magma_cube"}</code> |
| `minecraft:mooshroom_spawn_egg` | <code>{"id":"minecraft:mooshroom"}</code> |
| `minecraft:mule_spawn_egg` | <code>{"id":"minecraft:mule"}</code> |
| `minecraft:ocelot_spawn_egg` | <code>{"id":"minecraft:ocelot"}</code> |
| `minecraft:panda_spawn_egg` | <code>{"id":"minecraft:panda"}</code> |
| `minecraft:parrot_spawn_egg` | <code>{"id":"minecraft:parrot"}</code> |
| `minecraft:phantom_spawn_egg` | <code>{"id":"minecraft:phantom"}</code> |
| `minecraft:pig_spawn_egg` | <code>{"id":"minecraft:pig"}</code> |
| `minecraft:piglin_brute_spawn_egg` | <code>{"id":"minecraft:piglin_brute"}</code> |
| `minecraft:piglin_spawn_egg` | <code>{"id":"minecraft:piglin"}</code> |
| `minecraft:pillager_spawn_egg` | <code>{"id":"minecraft:pillager"}</code> |
| `minecraft:polar_bear_spawn_egg` | <code>{"id":"minecraft:polar_bear"}</code> |
| `minecraft:pufferfish_spawn_egg` | <code>{"id":"minecraft:pufferfish"}</code> |
| `minecraft:rabbit_spawn_egg` | <code>{"id":"minecraft:rabbit"}</code> |
| `minecraft:ravager_spawn_egg` | <code>{"id":"minecraft:ravager"}</code> |
| `minecraft:salmon_spawn_egg` | <code>{"id":"minecraft:salmon"}</code> |
| `minecraft:sheep_spawn_egg` | <code>{"id":"minecraft:sheep"}</code> |
| `minecraft:shulker_spawn_egg` | <code>{"id":"minecraft:shulker"}</code> |
| `minecraft:silverfish_spawn_egg` | <code>{"id":"minecraft:silverfish"}</code> |
| `minecraft:skeleton_horse_spawn_egg` | <code>{"id":"minecraft:skeleton_horse"}</code> |
| `minecraft:skeleton_spawn_egg` | <code>{"id":"minecraft:skeleton"}</code> |
| `minecraft:slime_spawn_egg` | <code>{"id":"minecraft:slime"}</code> |
| `minecraft:sniffer_spawn_egg` | <code>{"id":"minecraft:sniffer"}</code> |
| `minecraft:snow_golem_spawn_egg` | <code>{"id":"minecraft:snow_golem"}</code> |
| `minecraft:spider_spawn_egg` | <code>{"id":"minecraft:spider"}</code> |
| `minecraft:squid_spawn_egg` | <code>{"id":"minecraft:squid"}</code> |
| `minecraft:stray_spawn_egg` | <code>{"id":"minecraft:stray"}</code> |
| `minecraft:strider_spawn_egg` | <code>{"id":"minecraft:strider"}</code> |
| `minecraft:tadpole_spawn_egg` | <code>{"id":"minecraft:tadpole"}</code> |
| `minecraft:trader_llama_spawn_egg` | <code>{"id":"minecraft:trader_llama"}</code> |
| `minecraft:tropical_fish_spawn_egg` | <code>{"id":"minecraft:tropical_fish"}</code> |
| `minecraft:turtle_spawn_egg` | <code>{"id":"minecraft:turtle"}</code> |
| `minecraft:vex_spawn_egg` | <code>{"id":"minecraft:vex"}</code> |
| `minecraft:villager_spawn_egg` | <code>{"id":"minecraft:villager"}</code> |
| `minecraft:vindicator_spawn_egg` | <code>{"id":"minecraft:vindicator"}</code> |
| `minecraft:wandering_trader_spawn_egg` | <code>{"id":"minecraft:wandering_trader"}</code> |
| `minecraft:warden_spawn_egg` | <code>{"id":"minecraft:warden"}</code> |
| `minecraft:witch_spawn_egg` | <code>{"id":"minecraft:witch"}</code> |
| `minecraft:wither_skeleton_spawn_egg` | <code>{"id":"minecraft:wither_skeleton"}</code> |
| `minecraft:wither_spawn_egg` | <code>{"id":"minecraft:wither"}</code> |
| `minecraft:wolf_spawn_egg` | <code>{"id":"minecraft:wolf"}</code> |
| `minecraft:zoglin_spawn_egg` | <code>{"id":"minecraft:zoglin"}</code> |
| `minecraft:zombie_horse_spawn_egg` | <code>{"id":"minecraft:zombie_horse"}</code> |
| `minecraft:zombie_spawn_egg` | <code>{"id":"minecraft:zombie"}</code> |
| `minecraft:zombie_villager_spawn_egg` | <code>{"id":"minecraft:zombie_villager"}</code> |
| `minecraft:zombified_piglin_spawn_egg` | <code>{"id":"minecraft:zombified_piglin"}</code> |

## Related block items

No block item installs this component by default.

## Direct entity association

No entity-specific component ID is associated directly. Entities do not own an ItemStack Data Component map.

## Official registry entry

```json
{
  "protocol_id": 49
}
```

## Machine-readable sources

- [Official registries report](../../../official-reports/registries.json)
- [Component registry JSON](../../../data-component-types.json)
- [Component-to-items JSON](../../../component-to-items.json)
- [Version-pinned Wiki document](../../../../data-component-format.md)
- [Source authority and limitations](../../../SOURCE-LIMITATIONS.md)

Wiki text is attributed to Minecraft Wiki under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Registry IDs and defaults come from Mojang's official server reports.
