## Mod list installer
Install minecraft mods listed in `modlist.html` or `manifest.json` </br>

https://github.com/FriskIsGit/modlist-installer/assets/59506639/a52f4215-aaa4-4c10-9bfb-087b92bc5f44

<b>The project:
 - is written in .NET CORE
 - does not require or use a CF API token
 - contains 1 submodule - web scraper
 - works fully from CLI
</b>


## Cloning
```bash[mod.cache](mod.cache)
git clone --recurse-submodules https://github.com/FriskIsGit/modlist-installer
```

## Files
`mod.cache` - mod names to ids mappings </br>
`failed.html` - html-formatted mods that failed to download </br>
`diff.html` - html-formatted mods that are the difference of two mod lists </br>
`diff.json` - json manifest mods that are the difference of two manifests </br>
`release_win.bat` - command to release the app as standalone and trim
