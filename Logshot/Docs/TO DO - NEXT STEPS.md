### **Master Unimplemented Features & Fixes List (Prioritized)**

**ALL COMPLETED - JULY 21, 2026**

#### **High Priority**

1. **Section 4.2.3: Mobile Workspace `[ + SCENE ]` (New Setup) Button** [ **COMPLETED** ]
* **Master Spec:** A prominent control in the mobile interface to close the current scene setup, input a new Episode-Scene combination, and initialize Shot 1, Take 1 (with continuity checks if previously shot).
* **Current Status:** `MobileTakeListView` currently provides `[ + SHOT ]` and `[ + TAKE ]` buttons under existing setup groups, but lacks a `[ + SCENE ]` button to start an entirely new Episode/Scene group from scratch.


2. **Section 4.2.2: Desktop `[ + ADD SCENE ]` Button & New Scene Dialog** [ **COMPLETED** ]
* **Master Spec:** A prominent desktop header control to initiate a new scene setup and prompt for an Episode and Scene string.
* **Current Status:** The header workspace currently only provides `+ ADD SHOT` and `+ ADD TAKE` actions without an explicit scene-creation workflow.


3. **Section 4.2.2: Row Deletion Safety Modal Confirmation** [ **COMPLETED** ]
* **Master Spec:** Deleting a take row triggers a native modal confirmation dialog (matching the fail-safe pattern used for projects and days).
* **Current Status:** Clicking the delete "✕" button on a take row immediately deletes the take via `DeleteTakeCommand` without any confirmation prompt.


4. **Section 5.4: Blooper Context Menu Option & Visual Badge** [ **COMPLETED** ]
* **Master Spec:** Triggered through the context menu of the Description/Notes cell, toggling a vertical orange **"BLOOPER"** badge on the observations field.
* **Current Status:** The `IsBlooper` property and `ToggleBlooperCommand` exist in `TakeViewModel`, but the right-click context menus in `TakeGridView.axaml` and `TakeCardView.axaml` do not include an option to toggle "Blooper".

5. **Add No-Roll to Sound** [ **COMPLETED** ]
* **Master Spec:** A new "No-Roll" toggle in the sound column of the take row, which when enabled, visually indicates that the take wasn't rolled for sound. The No Roll option should be reachable by a context menu that opens when the user right-clicks a sound cell.

6. **Sound-only Rows** [ **COMPLETED** ]
* Add the logic to allow for sound-only rows in the take grid. This sound-only row is triggered when all camera cells are marked as no-roll and the sound cell contains written information. In this case, the row doesn't count as a take. Shot and take numbers should be ignored and are empty. The sound-only row should be able to be created, edited, and deleted like any other take row.
* This row will be used in practice when a sound recordist records sounds separately from the camera (e.g. foley, ADR etc).
* In a sound-only row, only the sound cell and the notes cell are useful. All other cells can be made non-editable - but we should be careful to still allow the user to remove the no-roll from cameras, in which case the row stops being a sound-only row and behaves as a normal row again.

---

#### **Medium Priority**

7. **Section 6: Interactive Continuity Prompt Dialog & Pathways** [ **COMPLETED** ]
* **Master Spec:** When entering/creating an Episode & Scene with existing history, the app halts and displays a high-visibility continuity notification presenting explicit action pathways:
1. `[ ADD A NEW SHOT ]` (Prominent option)
2. `[ Continue with SHOT X TAKE Y ]` (increments the last take of the preexisting shot by one)



8. **Multiple scenes intelligence:** [ **COMPLETED** ]
* **Master Spec:** Sometimes a shot may contain action from multiple scenes. The app should be able to understand that and log the shot/take(s) under all of the multiple scenes.
* Example: The director decides to shoot two continuous scenes (let's say scene 13 and 14 of episode 1) in one continuous shot. The script supervisor is obliged to write this as "Episode 1 - Scenes 13-14".
  * On the app, the script supervisor can enter multiple scenes in the scene field, separated by a line break. The app should understand that this is a single shot that belongs to multiple scenes, and log the shot/take(s) under all of the multiple scenes.
  * This comes especially handy when tracking the continuity. If the first shot of the day (let's say shot 1) contains action from both scene 13 and 14, but the second shot is logged as Episode 1 - Scene 13", the app should know to increment the shot number to 2 for the second shot, even though the first shot was logged under both scenes 13 and 14.
	* If, then, the third shot is logged as "Episode 1 - Scene 14", the app should know to increment from the shared shot number of shot 1 (first shot of the day), and increment the new shot number to 2.
	* If the fourth shot contains action from both scene 13 and 14, the app should know to increment the shot number to 3 for the fourth shot etc.
	* This way, both scenes have the correct shot numbering.
  * This way, the app will always suggest the correct number for a shot, even if the previous shot was logged under multiple scenes.
  * Always remember that a "scene" is actually a combination of episode and scene. The app shouldn't compare episode 2 scenes 13-14 to episode 5 scenes 13-14. These are two different couples of scenes.

9. **Section 4.1: Targeted Episode & Scene Search Bar** [ **COMPLETED** ]
* **Master Spec:** A global search bar designed **strictly for Episode (ΕΠ) and Scene (ΣΚ) inputs** to quickly filter or locate setups, bypassing shot and take numbers.
* This gives the ability to the user to quickly see when a scene was shot, and what takes were logged for that scene.
* The results should be presented in a clear and concise manner, allowing the user to easily navigate to the desired setup and read the information that was logged.
* We should decide if we will have separate fields for episode and scene, or if the app will have a "smart search bar" that will recognize a single input with a separator (most commonly episode/scene or episode.scene; the dash-separated or space-separated input might confuse the user because dashes and spaces are already used elsewhere in the app to mark multiple scenes instead of creating an episode-scene combination, so we must avoid dash and space separation). The app should be able to understand the separator and split the input into episode and scene. We should choose the option that makes the most sense for the code and the user experience.
* **Current Status:** Not present in `MainView.axaml` or `AppViewModel`.



---

#### **Low Priority**

10. **General Day Notes** [ **COMPLETED** ]
* **Master Spec:** A global notes field for the day, accessible from the header of the main right-side view, allowing users to log notes about a specific day, such as notes about a scene that is shot during that day.
* This field should be saved with the day, and should be accessible when the user navigates to that day in the future.
* The General Day Notes should be a unified field, tied to the day. No need for a separate Scribble field (if there is under-the-hood code for a separate Scribble field, it should be removed). The General Day Notes should be a single field that is accessible from the header of the main right-side view, and should be saved with the day.
* The General Day Notes should be printed in the PDF report (not yet implemented) for the day in the header section, above the list of shots and takes.