### **Master Unimplemented Features & Fixes List (Prioritized)**

#### **High Priority**

1. **Section 4.2.3: Mobile Workspace `[ + SCENE ]` (New Setup) Button** [ **COMPLETED** ]
* **Master Spec:** A prominent control in the mobile interface to close the current scene setup, input a new Episode-Scene combination, and initialize Shot 1, Take 1 (with continuity checks if previously shot).
* **Current Status:** `MobileTakeListView` currently provides `[ + SHOT ]` and `[ + TAKE ]` buttons under existing setup groups, but lacks a `[ + SCENE ]` button to start an entirely new Episode/Scene group from scratch.


2. **Section 4.2.2: Desktop `[ + ADD SCENE ]` Button & New Scene Dialog**
* **Master Spec:** A prominent desktop header control to initiate a new scene setup and prompt for an Episode and Scene string.
* **Current Status:** The header workspace currently only provides `+ ADD SHOT` and `+ ADD TAKE` actions without an explicit scene-creation workflow.


3. **Section 4.2.2: Row Deletion Safety Modal Confirmation**
* **Master Spec:** Deleting a take row triggers a native modal confirmation dialog (matching the fail-safe pattern used for projects and days).
* **Current Status:** Clicking the delete "✕" button on a take row immediately deletes the take via `DeleteTakeCommand` without any confirmation prompt.


4. **Section 5.4: Blooper Context Menu Option & Visual Badge**
* **Master Spec:** Triggered through the context menu of the Description/Notes cell, toggling a vertical orange **"BLOOPER"** badge on the observations field.
* **Current Status:** The `IsBlooper` property and `ToggleBlooperCommand` exist in `TakeViewModel`, but the right-click context menus in `TakeGridView.axaml` and `TakeCardView.axaml` do not include an option to toggle "Blooper".



---

#### **Medium Priority**

5. **Section 6: Interactive Continuity Prompt Dialog & Pathways**
* **Master Spec:** When entering/creating an Episode & Scene with existing history, the app halts and displays a high-visibility continuity notification presenting explicit action pathways:
1. `[ ADD A NEW SHOT ]` (Prominent option)
2. `[ Continue with SHOT X TAKE Y ]`


* **Current Status:** `ContinuityService` and `CreateTakeWithContinuity` handle continuity logic in the background, but the UI silently auto-applies it without displaying an interactive prompt dialog with these specific pathways.


6. **Section 4.1: Targeted Episode & Scene Search Bar**
* **Master Spec:** A global search bar designed **strictly for Episode (ΕΠ) and Scene (ΣΚ) inputs** to quickly filter or locate setups, bypassing shot and take numbers.
* **Current Status:** Not present in `MainView.axaml` or `AppViewModel`.



---

#### **Low Priority**

7. **Section 4.2.2: Desktop Drag-and-Drop Row Reordering**
* **Master Spec:** Drag handles on the left of each row (`⋮⋮`) allow instant manual drag-and-drop reordering of takes in the desktop grid.
* **Current Status:** The visual drag handle exists in `TakeGridView.axaml`, but `TakeGridView.axaml.cs` only contains stubbed pointer event handlers (`Row_PointerPressed`/`Moved`/`Released`) that do not perform actual collection reordering.