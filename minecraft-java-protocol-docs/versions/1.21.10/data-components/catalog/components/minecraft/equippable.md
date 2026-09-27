# Data Component `minecraft:equippable`

- Game version: `1.21.10`
- Registry protocol ID: `28`
- Category: `item_stack`
- Ownership: ItemStack Data Component map
- Registry JSON Pointer: `#/minecraft:data_component_type/entries/minecraft:equippable`
- Wiki revision: `3185065` (`2025-10-05T16:00:49Z`)

## Target-version facts

Registry membership, numeric IDs, default values, and reverse usage in this document come only from the target version's official Mojang reports.

The official reports do not expose the complete payload codec schema. Dynamic Wiki template output is deliberately not copied into this per-entry page because historical page revisions can render later template edits. The full recorded Wiki page remains linked below as a reference, not as the target-version schema oracle.

## Default item usage

Items whose official default component map contains this component: `78`.

| Item | Exact default value |
| --- | --- |
| `minecraft:black_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:black_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:black_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:black_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:blue_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:blue_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:blue_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:blue_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:brown_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:brown_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:brown_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:brown_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:carved_pumpkin` | <code>{"camera_overlay":"minecraft:misc/pumpkinblur","slot":"head","swappable":false}</code> |
| `minecraft:chainmail_boots` | <code>{"asset_id":"minecraft:chainmail","equip_sound":"minecraft:item.armor.equip_chain","slot":"feet"}</code> |
| `minecraft:chainmail_chestplate` | <code>{"asset_id":"minecraft:chainmail","equip_sound":"minecraft:item.armor.equip_chain","slot":"chest"}</code> |
| `minecraft:chainmail_helmet` | <code>{"asset_id":"minecraft:chainmail","equip_sound":"minecraft:item.armor.equip_chain","slot":"head"}</code> |
| `minecraft:chainmail_leggings` | <code>{"asset_id":"minecraft:chainmail","equip_sound":"minecraft:item.armor.equip_chain","slot":"legs"}</code> |
| `minecraft:copper_boots` | <code>{"asset_id":"minecraft:copper","equip_sound":"minecraft:item.armor.equip_copper","slot":"feet"}</code> |
| `minecraft:copper_chestplate` | <code>{"asset_id":"minecraft:copper","equip_sound":"minecraft:item.armor.equip_copper","slot":"chest"}</code> |
| `minecraft:copper_helmet` | <code>{"asset_id":"minecraft:copper","equip_sound":"minecraft:item.armor.equip_copper","slot":"head"}</code> |
| `minecraft:copper_horse_armor` | <code>{"allowed_entities":"#minecraft:can_wear_horse_armor","asset_id":"minecraft:copper","can_be_sheared":true,"damage_on_hurt":false,"equip_sound":"minecraft:entity.horse.armor","shearing_sound":"minecraft:item.horse_armor.unequip","slot":"body"}</code> |
| `minecraft:copper_leggings` | <code>{"asset_id":"minecraft:copper","equip_sound":"minecraft:item.armor.equip_copper","slot":"legs"}</code> |
| `minecraft:creeper_head` | <code>{"slot":"head","swappable":false}</code> |
| `minecraft:cyan_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:cyan_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:cyan_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:cyan_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:diamond_boots` | <code>{"asset_id":"minecraft:diamond","equip_sound":"minecraft:item.armor.equip_diamond","slot":"feet"}</code> |
| `minecraft:diamond_chestplate` | <code>{"asset_id":"minecraft:diamond","equip_sound":"minecraft:item.armor.equip_diamond","slot":"chest"}</code> |
| `minecraft:diamond_helmet` | <code>{"asset_id":"minecraft:diamond","equip_sound":"minecraft:item.armor.equip_diamond","slot":"head"}</code> |
| `minecraft:diamond_horse_armor` | <code>{"allowed_entities":"#minecraft:can_wear_horse_armor","asset_id":"minecraft:diamond","can_be_sheared":true,"damage_on_hurt":false,"equip_sound":"minecraft:entity.horse.armor","shearing_sound":"minecraft:item.horse_armor.unequip","slot":"body"}</code> |
| `minecraft:diamond_leggings` | <code>{"asset_id":"minecraft:diamond","equip_sound":"minecraft:item.armor.equip_diamond","slot":"legs"}</code> |
| `minecraft:dragon_head` | <code>{"slot":"head","swappable":false}</code> |
| `minecraft:elytra` | <code>{"asset_id":"minecraft:elytra","damage_on_hurt":false,"equip_sound":"minecraft:item.armor.equip_elytra","slot":"chest"}</code> |
| `minecraft:golden_boots` | <code>{"asset_id":"minecraft:gold","equip_sound":"minecraft:item.armor.equip_gold","slot":"feet"}</code> |
| `minecraft:golden_chestplate` | <code>{"asset_id":"minecraft:gold","equip_sound":"minecraft:item.armor.equip_gold","slot":"chest"}</code> |
| `minecraft:golden_helmet` | <code>{"asset_id":"minecraft:gold","equip_sound":"minecraft:item.armor.equip_gold","slot":"head"}</code> |
| `minecraft:golden_horse_armor` | <code>{"allowed_entities":"#minecraft:can_wear_horse_armor","asset_id":"minecraft:gold","can_be_sheared":true,"damage_on_hurt":false,"equip_sound":"minecraft:entity.horse.armor","shearing_sound":"minecraft:item.horse_armor.unequip","slot":"body"}</code> |
| `minecraft:golden_leggings` | <code>{"asset_id":"minecraft:gold","equip_sound":"minecraft:item.armor.equip_gold","slot":"legs"}</code> |
| `minecraft:gray_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:gray_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:gray_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:gray_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:green_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:green_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:green_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:green_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:iron_boots` | <code>{"asset_id":"minecraft:iron","equip_sound":"minecraft:item.armor.equip_iron","slot":"feet"}</code> |
| `minecraft:iron_chestplate` | <code>{"asset_id":"minecraft:iron","equip_sound":"minecraft:item.armor.equip_iron","slot":"chest"}</code> |
| `minecraft:iron_helmet` | <code>{"asset_id":"minecraft:iron","equip_sound":"minecraft:item.armor.equip_iron","slot":"head"}</code> |
| `minecraft:iron_horse_armor` | <code>{"allowed_entities":"#minecraft:can_wear_horse_armor","asset_id":"minecraft:iron","can_be_sheared":true,"damage_on_hurt":false,"equip_sound":"minecraft:entity.horse.armor","shearing_sound":"minecraft:item.horse_armor.unequip","slot":"body"}</code> |
| `minecraft:iron_leggings` | <code>{"asset_id":"minecraft:iron","equip_sound":"minecraft:item.armor.equip_iron","slot":"legs"}</code> |
| `minecraft:leather_boots` | <code>{"asset_id":"minecraft:leather","equip_sound":"minecraft:item.armor.equip_leather","slot":"feet"}</code> |
| `minecraft:leather_chestplate` | <code>{"asset_id":"minecraft:leather","equip_sound":"minecraft:item.armor.equip_leather","slot":"chest"}</code> |
| `minecraft:leather_helmet` | <code>{"asset_id":"minecraft:leather","equip_sound":"minecraft:item.armor.equip_leather","slot":"head"}</code> |
| `minecraft:leather_horse_armor` | <code>{"allowed_entities":"#minecraft:can_wear_horse_armor","asset_id":"minecraft:leather","can_be_sheared":true,"damage_on_hurt":false,"equip_sound":"minecraft:entity.horse.armor","shearing_sound":"minecraft:item.horse_armor.unequip","slot":"body"}</code> |
| `minecraft:leather_leggings` | <code>{"asset_id":"minecraft:leather","equip_sound":"minecraft:item.armor.equip_leather","slot":"legs"}</code> |
| `minecraft:light_blue_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:light_blue_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:light_blue_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:light_blue_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:light_gray_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:light_gray_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:light_gray_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:light_gray_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:lime_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:lime_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:lime_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:lime_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:magenta_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:magenta_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:magenta_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:magenta_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:netherite_boots` | <code>{"asset_id":"minecraft:netherite","equip_sound":"minecraft:item.armor.equip_netherite","slot":"feet"}</code> |
| `minecraft:netherite_chestplate` | <code>{"asset_id":"minecraft:netherite","equip_sound":"minecraft:item.armor.equip_netherite","slot":"chest"}</code> |
| `minecraft:netherite_helmet` | <code>{"asset_id":"minecraft:netherite","equip_sound":"minecraft:item.armor.equip_netherite","slot":"head"}</code> |
| `minecraft:netherite_leggings` | <code>{"asset_id":"minecraft:netherite","equip_sound":"minecraft:item.armor.equip_netherite","slot":"legs"}</code> |
| `minecraft:orange_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:orange_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:orange_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:orange_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:piglin_head` | <code>{"slot":"head","swappable":false}</code> |
| `minecraft:pink_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:pink_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:pink_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:pink_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:player_head` | <code>{"slot":"head","swappable":false}</code> |
| `minecraft:purple_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:purple_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:purple_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:purple_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:red_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:red_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:red_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:red_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:saddle` | <code>{"allowed_entities":"#minecraft:can_equip_saddle","asset_id":"minecraft:saddle","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.horse.saddle","shearing_sound":"minecraft:item.saddle.unequip","slot":"saddle"}</code> |
| `minecraft:shield` | <code>{"slot":"offhand","swappable":false}</code> |
| `minecraft:skeleton_skull` | <code>{"slot":"head","swappable":false}</code> |
| `minecraft:turtle_helmet` | <code>{"asset_id":"minecraft:turtle_scute","equip_sound":"minecraft:item.armor.equip_turtle","slot":"head"}</code> |
| `minecraft:white_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:white_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:white_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:white_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:wither_skeleton_skull` | <code>{"slot":"head","swappable":false}</code> |
| `minecraft:wolf_armor` | <code>{"allowed_entities":"minecraft:wolf","asset_id":"minecraft:armadillo_scute","can_be_sheared":true,"equip_sound":"minecraft:item.armor.equip_wolf","shearing_sound":"minecraft:item.armor.unequip_wolf","slot":"body"}</code> |
| `minecraft:yellow_carpet` | <code>{"allowed_entities":["minecraft:llama","minecraft:trader_llama"],"asset_id":"minecraft:yellow_carpet","can_be_sheared":true,"equip_sound":"minecraft:entity.llama.swag","shearing_sound":"minecraft:item.llama_carpet.unequip","slot":"body"}</code> |
| `minecraft:yellow_harness` | <code>{"allowed_entities":"#minecraft:can_equip_harness","asset_id":"minecraft:yellow_harness","can_be_sheared":true,"equip_on_interact":true,"equip_sound":"minecraft:entity.happy_ghast.equip","shearing_sound":"minecraft:entity.happy_ghast.unequip","slot":"body"}</code> |
| `minecraft:zombie_head` | <code>{"slot":"head","swappable":false}</code> |

## Related block items

- `minecraft:black_carpet`
- `minecraft:blue_carpet`
- `minecraft:brown_carpet`
- `minecraft:carved_pumpkin`
- `minecraft:creeper_head`
- `minecraft:cyan_carpet`
- `minecraft:dragon_head`
- `minecraft:gray_carpet`
- `minecraft:green_carpet`
- `minecraft:light_blue_carpet`
- `minecraft:light_gray_carpet`
- `minecraft:lime_carpet`
- `minecraft:magenta_carpet`
- `minecraft:orange_carpet`
- `minecraft:piglin_head`
- `minecraft:pink_carpet`
- `minecraft:player_head`
- `minecraft:purple_carpet`
- `minecraft:red_carpet`
- `minecraft:skeleton_skull`
- `minecraft:white_carpet`
- `minecraft:wither_skeleton_skull`
- `minecraft:yellow_carpet`
- `minecraft:zombie_head`

## Direct entity association

No entity-specific component ID is associated directly. Entities do not own an ItemStack Data Component map.

## Official registry entry

```json
{
  "protocol_id": 28
}
```

## Machine-readable sources

- [Official registries report](../../../official-reports/registries.json)
- [Component registry JSON](../../../data-component-types.json)
- [Component-to-items JSON](../../../component-to-items.json)
- [Version-pinned Wiki document](../../../../data-component-format.md)
- [Source authority and limitations](../../../SOURCE-LIMITATIONS.md)

Wiki text is attributed to Minecraft Wiki under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Registry IDs and defaults come from Mojang's official server reports.
