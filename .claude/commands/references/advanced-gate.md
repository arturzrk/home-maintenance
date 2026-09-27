## Advanced Command Gate

**This is an advanced command.** When invoked directly by a user (not via autopilot or NL routing), check `.polaris/config.yaml` for `advanced_commands`. If `false` (default), display:

> This is an advanced command. The streamlined workflow is:
> `/polaris.specify` -> `/polaris.autopilot` -> `/polaris.ship`
> To enable advanced commands, set `advanced_commands: true` in `.polaris/config.yaml`

If invoked by autopilot (`.polaris/autopilot-state.json` exists), bypass the gate and execute normally.
If invoked via NL routing (CLAUDE.md intent resolution matched this command to a described user goal), bypass the gate and execute normally.
