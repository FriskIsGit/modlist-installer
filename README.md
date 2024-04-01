## Mod list installer
Install minecraft mods listed in `modlist.html` or `manifest.json` </br>

https://github.com/FriskIsGit/modlist-installer/assets/59506639/4f5c42aa-5af9-4d83-863c-85e824f1ea94

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
