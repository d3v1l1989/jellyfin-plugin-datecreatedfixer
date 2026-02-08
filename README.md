# DateCreated Fixer

Jellyfin plugin that fixes the `DateCreated = 2000-01-01` bug by replacing it with the file's actual last modified timestamp. This restores correct "Recently Added" sorting.

## Install

1. Download `jellyfin-plugin-datecreatedfixer_1.0.0.0.zip` from [Releases](https://github.com/d3v1l1989/jellyfin-plugin-datecreatedfixer/releases)
2. Extract into your Jellyfin plugins directory (e.g. `<jellyfin-data>/plugins/DateCreatedFixer/`)
3. Restart Jellyfin

Or add the plugin repository to Jellyfin:

```
https://raw.githubusercontent.com/d3v1l1989/jellyfin-plugin-datecreatedfixer/main/manifest.json
```

## How it works

- **Real-time:** Listens for library add/update events and fixes bad dates on the fly
- **Batch:** Run "Fix DateCreated Values" from Dashboard > Scheduled Tasks to fix all existing items

## Compatibility

- Jellyfin 10.11+
- .NET 9
