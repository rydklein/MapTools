# MapTools
V1.1.0

## **[Download](https://github.com/BadPylot/MapTools/releases)**
Click the link above to see the available releases. It is recommended that you download the latest version. Legacy builds are available, but are not recommended for use.


## What is it?
MapTools is a Procon plugin designed to simplify changing certain game parameters based on the next game mode, and optionally, next map. Gone are the days of needlessly complicated Insane Limits scripts, or ineffective ProconRulz rules. Simply drop the plugin on your server, define your Mode Settings and Mode/Map overrides, and let MapTools do the work for you.
## Why should I use it?
### Why it's better than Ultimate Map Manager
Your first thought would likely be to use Ultimate Map Manager. Unfortuantely, UMM sends its in-game commands too late for them to be useful for this purpose.
### Why it's better than Insane Limits
The first issue is that Insane Limits is hard to expand. You'll find yourself writing or copying a lot of repetitive, bulky code that's not easy to read nor modify. The latter issue is rather niche, but it can be a problem. When a xVotemap vote runs through the end of a round, it sets the new map *after* the round ends. If you change your mode settings right at the round end event, you may set your server variables for the wrong map and mode -- a fatal mistake. MapTools accounts for this by running your commands a few seconds after the round ends, to give xVotemap time to run.
### Why it's better than ProconRulz
ProconRulz doesn't have an OnRoundEnd event. That means that you have to wait for the next map to load, and then run your commands. This isn't pretty, isn't elegant, and increases loading times for players. Also, for one reason or another, it tends to occassionally fail.
## How do I use it?
### Installation
Download MapTools.cs and put it in your Procon/Plugins/BF4/ directory. Restart your Procon layer.
### Configuration
**Presets Enabled** - Controls whether the plugin is enabled. Yes, I know it's redundant. It's there for/if I add more features in the future.


#### Default Mode Settings
- Controls the default mode settings that are set at the end of each round. 

**Example**: `ConquestLarge0,true,100,400` - For Conquest Large games, enable vehicles, set tickets to 100%, and set game duration to 400%.

Key 1 - Mode Name (string)

Key 2 - Vehicle Spawning Enabled (boolean)

Key 3 - Ticket % (Integer)

Key 4 - Round Length % (Integer)


#### Overrides - Controls additional game setings that may be specified based on map and mode.

**Example**: `ConquestLarge0,MP_Flooded,false,75,400` - On CQL Flood Zone, disable vehicles, set tickets to 75%, and set game duration to 400%.

Key 1 - Mode Name (string)

Key 2 - Map Name (string)

Key 3 - Vehicle Spawning Enabled (boolean)

Key 4 - Ticket % (Integer)

Key 5 - Round Length % (Integer)

**Notes**

Ticket % and Round Length % are percentage multipliers. 400% tickets on Rush is 400 tickets. 400% tickets on CQL is 3200 tickets.
## Known Issues / Shortcomings
- Once you set a Defualt Mode Setting or Override, you won't be able to remove all of them -- you'll have to leave at least one in place.
- For whatever reason, Procon tends to not save changes to Default Mode Settings and Overrides if you change them through the dropdown. Make sure you select the setting, and click the three dots on the right to edit them. Otherwise, your settings may not save properly.
- If your server changes to a mode that is not specified in Default Mode Settings, no commands will be executed, and you'll see a nasty error pop up in the plugin console.
## New Issues / Contact Me
If you find a problem with the plugin or have a suggestion, please open an [issue](https://github.com/BadPylot/maptools/issues/new). If you just need help figuring something out or setting up the plugin, feel free to contact me on Discord: BadPylot#0001.