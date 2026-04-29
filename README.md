[![image](https://discordapp.com/api/guilds/1001823907193552978/embed.png?style=banner2)](https://discord.gg/Zzrcc8kmvy)

# FFXIV LazyLoot

LazyLoot is a Dalamud plugin that helps you roll on loot quickly and consistently. It can issue one-time rolls on everything in the current chest, or run fully automatic rolls (FULF) with delays and filters so it still feels like a real person at the keyboard. It also lets you define global rules and overrides per item or duty so your loot preferences are always respected.

## Quick start

* Add this Repo URL to your Dalamud Experimental settings `/xlsettings > Experimental Tab`:

```
https://love.puni.sh/ment.json
```
* Install the plugin from the `/xlplugins > All Plugins > Lazy Loot`.
* `/lazy` opens the configuration window.
* Use `/lazy need`, `/lazy greed`, or `/lazy pass` to roll on the currently available loot.
* If you want hands-free rolls, enable FULF in the config or with `/fulf`.

## Commands

### Base commands

| Command | What it does |
| --- | --- |
| `/lazyloot` | Opens the LazyLoot config window. |
| `/lazy` | Opens the config window by default. Add a roll option to roll on current items. |
| `/lazy need` | Roll need for everything. If you can’t need, it will greed, and if you can’t greed, it will pass. |
| `/lazy greed` | Roll greed for everything. If you can’t greed, it will pass. |
| `/lazy pass` | Pass on everything you haven’t rolled on yet. |
| `/lazy test <itemId>` | Prints what LazyLoot *would* do for a specific item ID, based on your current settings. |

### FULF (Fancy Ultimate Lazy Feature)

| Command | What it does |
| --- | --- |
| `/fulf` | Toggle FULF on/off. |
| `/fulf on` | Enable FULF. |
| `/fulf off` | Disable FULF. |
| `/fulf need` | Set FULF to Need mode (uses the same rules as `/lazy need`). |
| `/fulf greed` | Set FULF to Greed mode (uses the same rules as `/lazy greed`). |
| `/fulf pass` | Set FULF to Pass mode (uses the same rules as `/lazy pass`). |

## How it works

LazyLoot watches the loot roll window and applies your rules in this order:

1. **Per-item rules** override everything else.
2. **Per-duty rules** override global rules (but not per-item rules).
3. **Global rules** apply everywhere else.
4. If FULF is enabled, it will automatically roll using the same logic as `/lazy need`, `/lazy greed`, or `/lazy pass`.

You also control how quickly rolls happen with min/max delay ranges, both for manual `/lazy` rolls and for FULF.

## Configuration guide

### Rolling behavior

* **Rolling delay between items**: set minimum and maximum delay for `/lazy need/greed/pass` rolls.
* **FULF delay range**: Min/max delay for auto rolls (minimum enforced to avoid suspiciously fast rolls).

### Loot filters (global rules)

These rules apply to all loot unless overridden by per-item or per-duty settings:

* **Item level filter**: automatically pass on items below a chosen item level.
* **Unlocked collection filter**: pass on items you already have unlocked. You can also target specific categories instead of everything at once:
  * Mounts
  * Minions
  * Bardings
  * Triple Triad cards
  * Emotes/hairstyles
  * Orchestrion rolls
  * Faded copies
  * *There is also the option to only pass already obtained unlockables if they are untradeables*
* **Other-job items**: pass on gear you can’t use on your current job.
* **Weekly lockout protection**: suspend rolling in weekly lockout duties.
* **Low-ilvl gear rule**: for gear you *can* need, decide whether to greed or pass if it’s below your current job’s item level by a configurable threshold.
* **Not an upgrade rule**: for gear you *can* need, decide whether to greed or pass if your equipped gear is higher item level.
* **Grand Company seals rule**: pass on gear with expert delivery seal value below a chosen amount.
* **Never pass glamour**: always roll on glamour items (item level 1 gear) even if other rules would pass them.

### Custom item rules (overrides)

You can set explicit roll rules per item:

* Choose **Need**, **Greed**, **Pass**, or **Do nothing**.
* Enable/disable each rule individually.
* Add items by search or ID.
* Export and import your list via clipboard for easy sharing or backups.

### Custom duty rules (overrides)

The same override system exists for duties:

* Choose **Need**, **Greed**, **Pass**, or **Do nothing** per duty.
* Enable/disable each rule individually.
* Add duties by search or ID.
* Export and import duty rules via clipboard.

### Output & feedback

* **Chat message**: print a summary of the rolls to chat.
* **Toast notifications**: show results as quest/normal/error toasts.

### Diagnostics & safety

* **Diagnostics mode**: print extra “why it passed” messages to help you troubleshoot.
* **Don’t pass on items that fail to roll**: prevents emergency passes when rolls fail.

### DTR (Server Info Bar) display

LazyLoot shows a DTR entry with your current FULF mode. You can toggle the entry on/off from the config, and the DTR entry can be clicked to cycle FULF modes (left/right click) or open the config with Ctrl+click.

---

If you’re unsure how a specific item will be handled, use `/lazy test <Item ID or Item Name>` and check the result in chat.
