**An updated UnityEditor based on Balrond's Location Tools.**

Features
* .blueprint and .vbuild are recognized by Unity as TextAssets now, and simply drag the desired blueprint to the import panel.
* Import path settings are easily defined now, so you can import blueprints with 3rd party prefabs, assuming you already have said prefabs in your project. 
* Select all by component is supported.  Manual selections are tracked and can be modified (select parent(s) of current selection(s) by component).
* Grouping and Stripping require a selection, and will detect all scene components to group or strip.
* Mesh Combine and Set WearNTear are virtually unchanged from original versions.
* Installable from Unity Package Manager with *Add from git URL*, `https://github.com/probablykory/location-tools.git`
