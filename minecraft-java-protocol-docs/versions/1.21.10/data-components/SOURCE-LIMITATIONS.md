# Minecraft Java 1.21.10 Data Component source boundaries

The target-version authoritative data in this directory comes from Mojang's official server `--reports` output: registry membership, protocol IDs, item default values, block definitions/states, and entity registry entries.

The archived Minecraft Wiki page records a historical page revision, but MediaWiki can expand transcluded templates using later template revisions. It is retained as a reference and attribution source, but its rendered payload schemas are not copied into the per-entry catalog and are not treated as target-version facts.

Official reports do not expose every Data Component payload codec. Where no exact schema is present in the reports, the catalog reports that limitation instead of inferring a schema from later Wiki text.
