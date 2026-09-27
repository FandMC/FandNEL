# Minecraft Java 1.21.10 Data Components

Source: Mojang's official server data generator (`--reports`).

- Data component types: `96`
- Individual Data Component documents: `96`
- [`catalog/components/`](catalog/components/README.md): one document per registered Data Component type.
- `documentation-manifest.json`: ID-to-document mapping with source pointers and SHA-256.
- [`SOURCE-LIMITATIONS.md`](SOURCE-LIMITATIONS.md): exact authority boundary for reports and Wiki prose.
- `data-component-types.json`: protocol IDs for every component type.
- `item-default-components.json`: normalized default components for every item.
- `component-to-items.json`: reverse lookup from component type to item/value.
- `block-item-default-components.json`: defaults for items that place blocks.
- `game-registries.json`: item, block, block-entity, entity, and component registries.
- `block-entity-related-components.json`: convenience index for block/entity-carrying components.
- `official-reports/`: byte-exact reports emitted by the official server JAR.

Data Components are attached to ItemStack values. Blocks are represented here through their block items and components such as `minecraft:block_state` or `minecraft:block_entity_data`. Entities do not own a Data Component map; item stacks can carry entity data through components such as `minecraft:entity_data` and `minecraft:bucket_entity_data`. All original aggregate JSON files remain present.
