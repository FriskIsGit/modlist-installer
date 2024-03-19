## Mod list installer
Install mods listed in `modlist.html` </br>

<b>The project:
 - is written in .NET CORE
 - does not require or use an CF API token
 - contains 1 submodule - web scraper
 - works fully from CLI
</b>


## Cloning
```bash
git clone --recurse-submodules https://github.com/FriskIsGit/modlist-installer
```

## Files
`mod.cache` - mod_name to mod_id mappings </br>
`failed.html` - html-formatted mods that failed to download </br>
`release_win.bat` - command to release the app as standalone and trim
