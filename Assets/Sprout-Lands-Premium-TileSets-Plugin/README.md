# Sprout Lands Premium TileSets
For Godot 4.3. Made in collaboration with [Cup Nooble](https://cupnooble.itch.io/).

[Example on itch.io](https://maaack.itch.io/harvest-hill-gwj-62-edition)  
![Example Screenshot](/addons/sprout_lands_premium_tilesets/media/Example_Screenshot_1.png)  

## Use Case
Start building worlds with the premium [Sprout Lands Asset Pack](https://cupnooble.itch.io/sprout-lands-asset-pack) right away.

## Requirements
1. Download and run [Godot](https://godotengine.org/).
2. Start a new project.
3. Purchase the premium version of the [Sprout Lands Asset Pack](https://cupnooble.itch.io/sprout-lands-asset-pack).
  
## Installation
1.  Copy the `Objects` and `Tilesets` folders from your premium sprite pack folder into `addons/sprout_lands_premium_tilesets/assets/`.
	1.  Select the option to overwrite or replace the existing placeholder files.
2.  Move the `addons/sprout_lands_premium_tilesets` folder into your project's `addons/` folder.
	1.  Add the `addons/` (case-sensitive) folder if it does not exist.
3.  Open/Reload the project.  
4.  Enable the plugin from the Project Settings > Plugins tab.  
	If it's enabled for the first time,
	1.  A dialogue window will appear asking to copy the example scenes out of `addons/`.
	2.  The example test scene shows some tiles. It doesn't have any player controller.

## Usage

The example scenes copied over during installation be worked in directly.


1.  Open `blank_sprout_lands_tile_map.tscn` or `example_world.tscn`.  
![Open Examples](/addons/sprout_lands_premium_tilesets/media/Usage_Screenshot_1.png)
2.  Right-click the root node and select "Clear Inheritance".  
![Clear Inheritance](/addons/sprout_lands_premium_tilesets/media/Usage_Screenshot_2.png)
3.  Select one of the layers that are children of the root node (ex. Grass).  
![Select Layer](/addons/sprout_lands_premium_tilesets/media/Usage_Screenshot_3.png)
4.  Open the TileMap editor, (if it is not open already).  
![Selecting TileMap Editor](/addons/sprout_lands_premium_tilesets/media/Usage_Screenshot_6.png)
5.  Select to draw terrains.  
![Selecting to draw terrains](/addons/sprout_lands_premium_tilesets/media/Usage_Screenshot_7.png)
6.  Select a terrain to draw (ex. Tiles)  
![Selecting Grass Terrain](/addons/sprout_lands_premium_tilesets/media/Usage_Screenshot_8.png)
7.  Draw the terrain (ex. Grass) in the scene view.  
![Drawing Grass](/addons/sprout_lands_premium_tilesets/media/Usage_Screenshot_9.png)

## Links
[Attribution](/addons/sprout_lands_tilemap/ATTRIBUTION.md)  
[Code License](/addons/sprout_lands_tilemap/LICENSE.txt)  
