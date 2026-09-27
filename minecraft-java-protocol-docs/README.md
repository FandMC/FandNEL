# Minecraft Java protocol documentation archive

本归档包含指定 Java Edition 版本的完整协议文档快照。每一页都固定到历史 revision，
并记录来源、时间、许可和 SHA-256；未在当时存在的后续格式会明确标记为不适用。

| Game version | Protocol | Downloaded | Unavailable | Failed |
| --- | ---: | ---: | ---: | ---: |
| [1.8.9](versions/1.8.9/README.md) | 47 | 27 | 17 | 0 |
| [1.12.2](versions/1.12.2/README.md) | 340 | 30 | 14 | 0 |
| [1.20](versions/1.20/README.md) | 763 | 37 | 7 | 0 |
| [1.20.6](versions/1.20.6/README.md) | 766 | 42 | 2 | 0 |
| [1.21.8](versions/1.21.8/README.md) | 772 | 44 | 0 | 0 |
| [1.21.10](versions/1.21.10/README.md) | 773 | 44 | 0 | 0 |

## Layout

- `versions/<version>/`: version-pinned Markdown documents and per-version index.
- `manifest.json`: machine-readable source, revision, hash and availability data.
- `versions/<version>/data-components/`: for 1.20.5+ snapshots, per-entry Data Component Markdown, official reports, and aggregate component JSON.
- `SHA256SUMS.txt`: hashes for every file in the archive except itself.

Content license: [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/).
