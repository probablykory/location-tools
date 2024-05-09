**An updated UnityEditor based on Balrond's Location Tools.**

## Features
* Installable from Unity Package Manager with *Add from git URL*, `https://github.com/probablykory/location-tools.git`
* .blueprint and .vbuild are recognized by Unity as TextAssets now, and simply drag the desired blueprint to the import panel.
* Import path settings are easily defined now, so you can import blueprints with 3rd party prefabs, assuming you already have said prefabs in your project. 
* Select all by component is supported.  Manual selections are tracked and can be modified (select parent(s) of current selection(s) by component).
* Grouping and Stripping require a selection, and will detect all scene components to group or strip.
* Adds a UI to take advantage of Simple Mesh Combine component functionality
* Older Mesh Combine tool is available but virtually unchanged from original versions.

## How to use
1. Open your ripped version of Valheim in Unity. Ensure that the prefabs used by the Blueprint are present in the Unity project. For instructions on creating a ripped version of Valheim, see [this guide](https://github.com/Valheim-Modding/Wiki/wiki/Valheim-Unity-Project-Guide).
2. Import the package using git by navigating to Windows > Package Manager > "+" > "Add Package from git URL..."
3. Download and import the package. Once successfully installed, you will have a new "Tools" tab in your task bar.
4. Click "Tools" > "Location Tools"
5. A new Unity window will open with the locaiton tools options. 
6. Add a Blueprint file into your Unity project and drag it onto the "Blueprint" field in Location Tools.
7. Create a new Game Object in the scene, by right-clicking in the Heirarchy menu > "Create Empty"
8. Name the Game Object, this will be the prefab name.
8. Drag the Game Object from Heirarchy menu into the "Import Target" field in Location Tools. 
9. Click import. 
10. You should now have a Game Object that reflects the Blueprint. 
11. Save the Game Object by dragging it from the Heirarchy menu into a project folder.

## Changes

### v1.6.0
* Wrote a new utility to act as a UI for valheim's SimpleMeshCombine component.  Should be friendlier & more reliable, but kept the old Combine tool just in case.
* Bugfix for Group tool: resolve occasional NRE when selection change yields a similar but not identical set of availble components.
* Commented out the WearNTear tool for now, some reports of people unable to use any of the tools due to an error with it.