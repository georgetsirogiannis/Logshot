# **Logshot — Implementation Roadmap**

**Last Updated:** Master Commit 5cb3729  
**Current Phase:** Phase 3 (Desktop UI)  
**Overall Progress:** 2/7 phases complete (29%)

---

### **Phase 1: The Foundation (Data & Backend)** ✅ **COMPLETE**

1. **Project Setup:** Create the base Avalonia UI cross-platform project (targeting Windows and Android), configuring version numbers appropriately across all manifests.  ✅ **COMPLETED**
2. **C# Entity Models:** Write the C# classes (Project, Day, Take) exactly mirroring the schema.  ✅ **COMPLETED**
3. **Local SQLite Initialization:** Set up the local database engine so the app automatically creates the logshot.db file and tables on first launch.  ✅ **COMPLETED**
4. **Data Repository Layer:** Write the basic CRUD commands to save new Takes and Days to the local database in under 2 milliseconds.  ✅ **COMPLETED**
5. **Supabase Connection:** Install the Supabase C# SDK and set up the basic authentication and cloud connection endpoints.  ⏳ **DEFERRED** (scheduled for Phase 7)

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

### **Phase 3: The Desktop UI (The A4 Grid)** 🔄 **IN PROGRESS**

**Target Completion:** TBD

1. **Global Styling & Typography:** Import and configure the **Roboto Condensed** font and set up the dark-mode color palettes.
   - Status: ⏳ Not started

2. **Main Layout & Navigation:** Build the left-side Project/Day browser and the main right-side Workspace.
   - Status: ⏳ Not started
   - Notes: MainView.axaml exists (referenced in workspace) but minimal UI implementation currently present

3. **The 14/48 Grid Architecture:** Construct the data grid using the exact horizontal percentages, ensuring strict vertical-center alignment for all cells.
   - Status: ⏳ Not started
   - Requirements: 
	 - CAM A ROLL (14%) | CAM B ROLL (14%) | SOUND ROLL (6%) | ΕΠ (4%) | ΣΚ (4%) | ΠΛΑΝΟ (5%) | ΛΗΨΗ (5%) | ΠΑΡΑΤΗΡΗΣΕΙΣ (48%)
	 - Dynamic camera expansion with + buttons
	 - Vertical center alignment throughout

4. **Grid Interactions:** Implement inline typing, the + camera buttons, manual drag-and-drop reordering, and the Undoable Finalize Day mechanics.
   - Status: ⏳ Not started
   - Features needed: Inline editing, drag-and-drop, camera management UI, Finalize/Reopen toggle

---

### **Phase 4: The Mobile UI (Adaptive Cards)** ⏳ **PENDING**

**Dependency:** Phase 3 (Desktop UI foundation)

1. **Responsive Triggers:** Write the Avalonia rule that detects when the screen is < 720px wide and swaps the grid for the mobile layout.  
2. **Episode-Scene Subheaders:** Build the collapsible setup bars with the dedicated [+ SHOT] and [+ TAKE] incremental buttons.  
3. **Take Cards:** Design the vertically stacked card layout for individual takes.  
4. **Quick-Action Drawers:** Implement the swipe-right gesture to reveal the [FS], [LS], and [ΑΚΥΡΟ] buttons.  

---

### **Phase 5: Gestures, Shorthands & Advanced UX** ⏳ **PENDING**

**Dependency:** Phase 3 & 4 (UI layers)

1. **Tap Gestures:** Program the single-tap (Circled) and double-tap (Failed) logic, linking them to visual UI updates.  
2. **Camera-Specific ΑΚΥΡΟ:** Implement the logic that collapses the row height to 16pt, identifies the voided cameras, and renders the "XXXXXXX" cross-stitch pattern.  
3. **Quick-Tag Chips:** Add the tap-to-increment [FS] and [LS] badges inside the mobile notes cell.  
4. **Diagonal Slashes:** Apply the tap/swipe mechanism to render "No Roll" camera slashes.  

---

### **Phase 6: The PDF Export Engine (QuestPDF)** ⏳ **PENDING**

**Dependency:** Phase 3, 5 (UI data complete)

1. **A4 Portrait Canvas & Headers:** Set up the QuestPDF document, locking dimensions to A4 and building the repeating metadata header and top-margin scribble box (strictly white-label).  
2. **Table Generation:** Map the database values to the PDF table, implementing dynamic column width calculations.  
3. **Custom Shorthand Rendering:** Write the custom PDF drawing instructions for Circled takes, Failed takes, Blooper badges, and the shallow ΑΚΥΡΟ cross-stitch rows.  
4. **"END DAY" Calculator:** Write the math that determines remaining page height on finalized days and draws the massive cross-hatch filler block.  

---

### **Phase 7: Polish & Production** ⏳ **PENDING**

**Dependency:** All previous phases (full feature complete)

1. **The Outbox Sync Worker:** Build the background task with the 3-second debounce timer to quietly push SQLite changes to Supabase.  
   - *Note: Includes Supabase connection setup deferred from Phase 1*

2. **Emergency Backup:** Implement the button that bundles the .db file for local Bluetooth/USB sharing.  
3. **Version Control Audit:** Update version numbers across all manifests and configuration files.  
4. **Testing & Bug Hunting:** Run the app with a simulated 100-take day to check for lag, scrolling issues, or UI wrapping errors.

---

## **Key Metrics & Tracking**

| Phase | Status | Commits | Key Files |
|-------|--------|---------|-----------|
| 1 | ✅ | 86c0df7, 4818cda | Models/*.cs, Services/DatabaseService.cs |
| 2 | ✅ | 5ed4379, 99c9c2d, 5cb3729 | ViewModels/*.cs, Services/CameraDataManager.cs, Services/ContinuityService.cs |
| 3 | 🔄 | (upcoming) | Views/MainView.axaml, Views/** |
| 4-7 | ⏳ | (pending) | — |

---

## **Blockers & Notes**

- **None currently blocking Phase 3 start.** All Phase 2 deliverables complete.
- MainView.axaml exists but requires full desktop layout and grid implementation.
- Roboto Condensed font integration is the recommended first task for Phase 3 Step 1.
