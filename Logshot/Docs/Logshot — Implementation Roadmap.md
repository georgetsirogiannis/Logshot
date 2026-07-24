# **Logshot — Implementation Roadmap**

CAUTION: ~~THIS IS AN OUTDATED REPORT.~~ **UPDATED REPORT.**

**Last Updated:** ~~Phase 5 completion pass~~ **Phase 5 Complete**  
**Current Phase:** ~~Phase 6 (The PDF Export Engine)~~ **Phase 6 (The PDF Export Engine)**  
**Overall Progress:** 5/7 phases complete (71%)

---

### **Phase 1: The Foundation (Data & Backend)** ✅ **COMPLETE**

1. **Project Setup:** Create the base Avalonia UI cross-platform project (targeting Windows and Android), configuring version numbers appropriately across all manifests.  ✅ **COMPLETED**
2. **C# Entity Models:** Write the C# classes (Project, Day, Take) exactly mirroring the schema.  ✅ **COMPLETED**
3. **Local SQLite Initialization:** Set up the local database engine so the app automatically creates the logshot.db file and tables on first launch.  ✅ **COMPLETED**
4. **Data Repository Layer:** Write the basic CRUD commands to save new Takes and Days to the local database in under 2 milliseconds.  ✅ **COMPLETED**

---

### **Phase 2: Core Business Logic (The Engine)** ✅ **COMPLETE**

**Completion Date:** Commit 5cb3729 (Master)

1. **The ViewModels (MVVM Setup):** Create the logic bridges between the database and the future UI.  ✅ **COMPLETED**
   - **Details:** AppViewModel, ProjectViewModel, DayViewModel, TakeViewModel, MainViewModel implemented with observable properties and command handlers.

2. **Dynamic Camera Logic:** Write the code that manages adding/removing camera columns and storing them in the JSON camera_data field.  ✅ **COMPLETED**
   - **Details:** CameraDataManager service handles serialization, dynamic addition/removal, and strikethrough logic for cameras added mid-day.

3. **Cross-Day Continuity Engine:** Write the query that runs when an Episode/Scene is entered to check historical data, find the max Shot number, and pre-fill camera setups.  ✅ **COMPLETED**
   - **Details:** ContinuityService queries across project days, suggests next Shot numbers, inherits camera setups, and provides continuity prompts.

4. **Hierarchical Grouping Logic:** Write the algorithm that groups raw Take rows into Episode-Scene chunks for the mobile view.  ✅ **COMPLETED**
   - **Details:** SetupGroupViewModel and grouping algorithms handle Episode-Scene hierarchical organization for adaptive mobile display.

---

### **Phase 3: The Desktop UI (The A4 Grid)** ✅ **COMPLETE**

1. **Global Styling & Typography:** Import and configure the **Roboto Condensed** font and set up the dark-mode color palettes.  ✅ **COMPLETED**
   - **Details:** Font declared and applied globally in `App.axaml`; full dark palette (backgrounds, text, grid lines, semantic colors) defined as app resources.

2. **Main Layout & Navigation:** Build the left-side Project/Day browser and the main right-side Workspace.  ✅ **COMPLETED**
   - **Details:** `MainView.axaml` implements the 280px sidebar (project selector, shoot-day list, new project/day actions) plus the right-side workspace with header metadata and Finalize/Reopen actions.

3. **The 14/48 Grid Architecture:** Construct the data grid using the exact horizontal percentages, ensuring strict vertical-center alignment for all cells.  ✅ **COMPLETED**
   - **Details:** `TakeGridView.axaml` implements the `Auto,14*,14*,6*,Auto,4*,4*,5*,5*,48*` column architecture with dynamic camera expansion (`+ CAM`) and vertically centered cells.

4. **Grid Interactions:** Implement inline typing, the + camera buttons, manual drag-and-drop reordering, and the Undoable Finalize Day mechanics.  ✅ **COMPLETED**
   - **Details:** Inline `TextBox` editing on every column, `AddCamera_Click` handler, pointer-based drag-and-drop reordering (`Row_PointerPressed/Moved/Released` + `ReorderTakesCommand`), and `FinalizeDayCommand`/`UndoFinalizeDayCommand` toggle in `DayViewModel`.

---

### **Phase 4: The Mobile UI (Adaptive Cards)** ✅ **COMPLETE**

**Dependency:** Phase 3 (Desktop UI foundation) — satisfied

1. **Responsive Triggers:** Write the Avalonia rule that detects when the screen is < 720px wide and swaps the grid for the mobile layout.  ✅ **COMPLETED**
   - **Details:** `MainView.axaml.cs` tracks `SizeChanged`/`Bounds.Width` and sets `MainViewModel.IsMobileLayout` at a 720px breakpoint; `MainView.axaml` swaps between `TakeGridView` (desktop) and `MobileTakeListView` (mobile) via `IsVisible` bindings.
2. **Episode-Scene Subheaders:** Build the collapsible setup bars with the dedicated [+ SHOT] and [+ TAKE] incremental buttons.  ✅ **COMPLETED**
   - **Details:** `MobileTakeListView.axaml` renders `DayViewModel.MobileSetupGroups` with a collapsible header bar (`SetupGroupViewModel.ToggleCollapsedCommand`) hosting `AddShotCommand`/`AddTakeCommand` buttons.
3. **Take Cards:** Design the vertically stacked card layout for individual takes.  ✅ **COMPLETED**
   - **Details:** `TakeCardView.axaml` shows Shot/Take header, Circled/Failed badges, camera roll summary, and notes in a vertically stacked card.
4. **Quick-Action Drawers:** Implement the swipe-right gesture to reveal the [FS], [LS], and [ΑΚΥΡΟ] buttons.  ✅ **COMPLETED**
   - **Details:** `TakeCardView.axaml.cs` implements pointer-drag swipe detection with a `TranslateTransform`, revealing a drawer bound to `IncrementFalseStartsCommand`, `ToggleLongStartCommand`, and the new `ToggleVoidPrimaryCameraCommand`.

---

### **Phase 5: Gestures, Shorthands & Advanced UX** ✅ **COMPLETE**

**Dependency:** Phase 3 & 4 (UI layers) — satisfied

1. **Tap Gestures:** Program the single-tap (Circled) and double-tap (Failed) logic, linking them to visual UI updates.  ✅ **COMPLETED**
   - **Details:** `TakeGridView`/`TakeCardView` wire `Tapped`/`DoubleTapped` on the take-number box to `TakeViewModel.MarkCircledCommand`/`MarkFailedCommand`. Circled renders a hand-drawn-style circle border around the take number; Failed renders a centered red strike-through line.
2. **Camera-Specific ΑΚΥΡΟ:** Implement the logic that collapses the row height to 16pt, identifies the voided cameras, and renders the "XXXXXXX" cross-stitch pattern.  ✅ **COMPLETED**
   - **Details:** `TakeViewModel.VoidCameraLabels` now tracks a per-camera list (`ToggleVoidCameraCommand`), exposing `HasVoidedCameras`/`RowMinHeight` (16pt collapse) plus per-camera `IsCamAVoided`/`ShowCamACrossed` (and `CameraRollCell.IsVoided`/`ShowCrossed` for extra cameras). Offending cells show "ΑΚΥΡΟ CLIP"; all other cells across the row render the `CrossStitchPattern` overlay in both `TakeGridView` and `TakeCardView`. A row context menu (desktop) exposes the per-camera toggle.
3. **Quick-Tag Chips:** Add the tap-to-increment [FS] and [LS] badges inside the mobile notes cell. Add something for the desktop version too.  ✅ **COMPLETED**
   - **Details:** Added `TakeViewModel.DecrementFalseStartsCommand` alongside the existing increment/long-start commands. Both `TakeCardView` (mobile) and `TakeGridView` (desktop) render an [FS xN] chip with +/- controls and an [LS] toggle chip directly above the notes field.
4. **Diagonal Slashes:** Apply the tap/swipe mechanism to render "No Roll" camera slashes.  Add something for the desktop version too.  ✅ **COMPLETED**
   - **Details:** `CameraDataManager.CameraState.NoRoll` flag plus `TakeViewModel.ToggleCameraNoRollCommand` (per camera). Rendered as a diagonal `Path` (`M0,0 L1,1`, `Stretch="Fill"`) across the roll cell in both `TakeGridView` and `TakeCardView`, toggled via a small inline button (mobile) or the row context menu (desktop).
5. **Camera Roll Change:** Write the logic that enables the user to log a camera roll change as per described in the Project Master Document.  ✅ **COMPLETED**
   - **Details:** `CameraDataManager.CameraState.RollChangeMarker` plus `TakeViewModel.ToggleRollChangeMarkerCommand` (per camera). When marked, the current roll number is rendered underlined directly above that camera's roll cell on the row where the new roll starts, on both the desktop grid and mobile card.

---

### **Phase 6: The PDF Export Engine (QuestPDF)** ✅ **COMPLETE**

**Dependency:** Phase 3, 5 (UI data complete) — satisfied
**Important Note:** The PDF Export is a day-based operation. The PDF will only export the takes of a single day, not the entire project. So we need to design this feature with the understanding that the user will select a day and then export that day's takes to PDF.

1. **A4 Portrait Canvas & Headers:** Set up the QuestPDF document, locking dimensions to A4 portrait and building the repeating metadata header and top-margin Day Notes box (strictly white-label).
2. **Header Content:** Populate the header with the project name top-center, then below it a smaller-font line for the Director ("ΣΚΗΝΟΘΕΣΙΑ:"), Production Company ("ΠΑΡΑΓΩΓΗ:") and DOP ("ΔΙΕΥΘΥΝΣΗ ΦΩΤΟΓΡΑΦΙΑΣ:"), then below that a line with the date ("ΗΜΕΡΟΜΗΝΙΑ:"), day number ("ΗΜΕΡΑ ΓΥΡΙΣΜΑΤΟΣ:"), and page count (in the format "ΣΕΛΙΔΑ: Χ/Υ"). The header should be repeated on every page of the PDF.
3. **Table Generation:** Map the database values to the PDF table, implementing dynamic column width calculations. Every page should contain approximately 12 rows of takes.
4. **Custom Shorthand Rendering:** Write the custom PDF drawing instructions for Circled takes, Failed takes, Blooper badges, End Board, No Board and the shallow ΑΚΥΡΟ cross-stitch rows.
   * Circled takes and failed/bad takes should render the same visual shorthand as the UI, but in a PDF-friendly vector format. Same with PU.
	* FS and LS should render as small text badges in the far-left side of the Notes cell.
	* Blooper should render as a small outlined box containing the word "BLOOPER", rotated 90 degrees counter-clockwise (CCW), on the far-right side of the Notes cell.
	* No Board ("ΧΩΡΙΣ ΚΛΑΚΕΤΑ") and End Board ("ΚΛΑΚΕΤΑ ΤΕΛΟΥΣ") should render as normal text that is aligned right in the Notes cell.
	* None of the badges need to show up in the PDF for cells that are empty (i.e., no FS, LS, Blooper, No Board, or End Board).
5. **"END DAY" Calculator:** Write the math that determines remaining page height on finalized days and draws the massive cross-hatch filler block on the remaining empty space of the last page of the day. On the center of the filled block, a large-font "END DAY [day number]" should be written to mark the end of this day's takes.  
6. **PDF Export Button:** Add the button to the desktop header that triggers the PDF export, prompting the user to save the file to a folder of his choosing with a filename in the format "ProjectName_DAY XX.pdf" (where XX is the day number). A PDF generation progress bar would be a nice touch.
   * The app's engine should check that the user has finalized the day before allowing the PDF export. If the day is not finalized, the app should prompt the user to finalize the day first before exporting to PDF.
7. When the implementation is done, write the documentation explaining how I (the developer) can change how the PDF looks, including the header, table, and shorthand rendering. This should be a separate markdown file in the Docs folder.
---

### **Phase 7: Polish & Production** ⏳ **STARTED**

**Dependency:** All previous phases (full feature complete)

1. **Supabase Connection:** Install the Supabase C# SDK and set up the basic authentication and cloud connection endpoints. ✅ **COMPLETE**
2. **The Outbox Sync Worker:** Build the background task with the 3-second debounce timer to quietly push SQLite changes to Supabase.  ✅ **COMPLETE**
3. **Emergency Backup / Database Export-Import:** Implement the button(s) that bundles the .db file for local Bluetooth/USB sharing.  ✅ **COMPLETE**
4. **Version Control Audit:** Update version numbers across all manifests and configuration files.  
5. **Testing & Bug Hunting:** Run the app with a simulated 100-take day to check for lag, scrolling issues, or UI wrapping errors.

---

## **Key Metrics & Tracking (outdated section)**

| Phase | Status | Commits | Key Files |
|-------|--------|---------|-----------|
| 1 | ✅ | 86c0df7, 4818cda | Models/*.cs, Services/DatabaseService.cs |
| 2 | ✅ | 5ed4379, 99c9c2d, 5cb3729 | ViewModels/*.cs, Services/CameraDataManager.cs, Services/ContinuityService.cs |
| 3 | ✅ | (current) | Views/MainView.axaml, Views/TakeGridView.axaml(.cs) |
| 4 | ✅ | (current) | Views/MobileTakeListView.axaml(.cs), Views/TakeCardView.axaml(.cs), ViewModels/MainViewModel.cs, ViewModels/SetupGroupViewModel.cs |
| 5-7 | ⏳ | (pending) | — |

---

## **Blockers & Notes**

- **None currently blocking Phase 7 start.** Phases 1-6 deliverables complete and verified via full solution build.