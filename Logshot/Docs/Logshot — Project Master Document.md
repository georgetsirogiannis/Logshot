# **Logshot — Project Master Document**

**Target Platforms:** Windows Desktop (Avalonia UI / .NET) & Android Mobile (Avalonia UI / Android SDK)  
**.NET Version:** 10.0  
**Avalonia Version:** 12.1.0  
**Architecture:** Local-First, Offline-Capable, Outbox Syncing Pattern  
**Backend & Database:** SQLite (Local) & Supabase PostgreSQL (Cloud Sync)  
**Export Engine:** QuestPDF (C\# Portrait Fluent Layout)  
**Primary Typography:** Roboto Condensed (Bundled for Greek Unicode and space-efficiency)  
**Default Theme:** Dark-Mode-First System Theme

## **1\. Project Vision & Core Goals**

The purpose of **Logshot** is to digitize and replace the heavy, handwritten daily paper logs used by script supervisors on professional film and television sets.  
The software is strictly built around the European production hierarchy: **Episode → Scene → Shot → Take**. It utilizes a responsive, dual-layout design. On desktop screens, it mirrors the exact column layout and geometry of a physical A4 paper log to give a "What You See Is What You Get" (WYSIWYG) sheet view. On mobile phones, it shifts seamlessly into an adaptive, grouped list of cards designed for quick, one-handed thumb entry in fast-paced environments.  
The system ensures absolute data safety with instant local writes (\<2ms), battery-saving debounced cloud synchronization, smart cross-day scene continuity checks, and an advanced PDF export engine that precisely reproduces the handwritten shortcuts, slashes, stamps, and layout constraints of original paper sheets.

## **2\. Global Visual & Layout Standards**

### **2.1 The Golden Rule: Vertical Center Alignment**

Every cell in the data grid—both within the desktop editing workspace, and the final exported PDF—must have its content **vertically centered**. Whether a row contains a single-digit tracking number or four lines of dense Greek text, all cell contents must remain perfectly anchored to the vertical middle of their respective rows. This eliminates "floating" text and ensures clean scanability.

### **2.2 Portrait Proportions & Default Sizing**

* **Standard Page Geometry:** The final output and desktop canvas are oriented to **A4 Portrait** (595 x 842 points layout budget).  
* **Strict 12-Row Target Budget:** Standard table rows are locked to a baseline height of **42 points**. This vertical spacing is mathematically balanced to fit exactly **12 rows of takes** per page while leaving perfect margins for metadata headers, top margin scribbles, and footers.  
* **Text Wrapping & Auto-Expansion:** Text wrapping is globally enabled on all text columns. If observations in the notes column exceed the default row height budget, the row height will automatically expand downward to prevent any data cropping.  
* **Roboto Condensed Typeface:** This specific condensed sans-serif is bundled into the application's assets. Its narrow character width maximizes horizontal cell limits, allowing more text on a single line before wrapping.  
* 

## **3\. Relational Database Schema (The Foundation)**

To maintain absolute data integrity across local SQLite files and the Supabase cloud backend, the database uses a strict relational table structure reflecting the Episode → Scene → Shot → Take hierarchy.  
Each project must be self-contained, i.e. we need to make sure that searching for an episode-scene combination only returns results for the currently active project.

### **3.1 Projects**

* id (TEXT, Primary Key / UUID)  
* name (VARCHAR, e.g., *"Ghosts"*)  
* director (VARCHAR)  
* dop (VARCHAR)  
* production\_company (VARCHAR)  
* created\_at (TIMESTAMP)

### **3.2 Days**

* id (TEXT, Primary Key / UUID)  
* project\_id (TEXT, Foreign Key referencing Projects.id with CASCADE delete)  
* shoot\_day\_number (VARCHAR, e.g., "54", "54B". String type allows manual corrections, pickup days, and split-day alphanumeric entries)  
* calendar\_date (DATE)  
* general\_notes (TEXT, daily high-level notes box)  
* top\_scribble\_notes (TEXT, top margin notes for script changes)  
* is\_finalized (BOOLEAN, defaults to false. When locked, edits are disabled, and the "END DAY" space filler is generated on the PDF. This state can be reversed)  
* created\_at (TIMESTAMP)

### **3.3 Takes**

* id (TEXT, Primary Key / UUID)  
* day\_id (TEXT, Foreign Key referencing Days.id with CASCADE delete)  
* sequence\_order (INTEGER, used for manual drag-and-drop reordering)  
* episode (VARCHAR, e.g., "7", "10-11". String type allows continuous multi-scene or multi-episode setups)  
* scene (VARCHAR, e.g., "32", "28-29". String type allows split scenes, pickup slates, and continuous takes)  
* shot (INTEGER, ΠΛΑΝΟ)  
* take (INTEGER, ΛΗΨΗ)  
* camera\_data (TEXT, JSON mapping of active camera labels to setup text, e.g., {"A": "KONTINO LOUKA", "B": "-//-"})  
* sound\_notes (VARCHAR, e.g., "MUTE", "OFF ΧΑΡΗ 9/53")  
* take\_notes (TEXT, raw observations)  
* false\_start\_count (INTEGER, default 0. Increments: 0 \= none, 1 \= FS, 2 \= FS x2)  
* is\_long\_start (BOOLEAN, default false)  
* is\_circled (BOOLEAN, default false — preferred takes)  
* is\_failed (BOOLEAN, default false — bad takes)  
* is\_pickup (BOOLEAN, default false — flags the take as a Pickup / PU)  
* is\_blooper (BOOLEAN, default false — toggles vertical blooper badge)  
* void\_camera\_labels (TEXT, JSON array of strings, e.g., \["A"\]. Stores camera column labels that suffered a false clip on that take)  
* created\_at (TIMESTAMP)


## **4\. UI/UX Workflow & Responsive Layouts**

### **4.1 Screen A: Project Dashboard & History (Unified)**

* **Project Selector:** Active project dropdown.  
* **Day Browser:** A chronological vertical listing of shooting days.  
* **Targeted Search Functionality:** A global search bar designed **strictly for Episode (ΕΠ) and Scene (ΣΚ) inputs**. It entirely bypasses shot and take numbers to eliminate search noise.  
* **Start New Day Button:** Stamps today's date and auto-increments the day number identifier string.


### **4.2 Screen B: The Shot Report Workspace (Current Day)**

#### **4.2.1 Header Area (Interactive Metadata)**

* Displays Project Name, Date, Director, and DoP.  
* Includes an expandable text area for daily **General Notes**.  
* **Interactive Day Number:** The shoot day identifier is an interactive touch target to correct typos or enter custom configurations.  
* **Top-Margin Scribble Box:** Input for notes that must sit at the absolute top margin of the daily report PDF.

#### **4.2.2 Desktop Workspace (WYSIWYG A4 Portrait Grid)**

* **Exact Paper Column Layout & Widths:**  
  * CAM A ROLL **(14%)**  
  * CAM B ROLL **(14%)**  
  * SOUND ROLL **(6%)**  
  * ΕΠ **(4%)**  
  * ΣΚ **(4%)**  
  * ΠΛΑΝΟ **(5%)**  
  * ΛΗΨΗ **(5%)**  
  * ΠΑΡΑΤΗΡΗΣΕΙΣ **(48%)**  
* **Dynamic Camera Expansion:** App starts with CAM A and CAM B columns by default. A \+ button in the header adds columns dynamically. If a camera is added when a day’s report has already started and is filled with previous takes, all the new camera’s cells for previously recorded takes are striked-through, meaning the newly added camera wasn’t recording until now.  
* **Continuous Visual Flow:** The workspace displays a clean, continuous grid without simulated physical page separators. It allows you to scroll freely and edit takes inline.  
* **Drag-and-Drop Reordering:** Drag handles on the left of each row allow instant manual reordering of takes.  
* **Deletion Safety:** Deleting a row triggers a native modal confirmation.  
* **Two-Way Finalize / Reopen System:**  
  * When the day is complete, tap **"Finalize Day & Sign Off"**. This locks standard row inputs and automatically triggers the cross-hatched "END DAY" pattern in the PDF generator.  
  * An **"Undo Finalize / Reopen Day"** button is immediately available to restore edit access.

#### **4.2.3 Mobile Workspace (Adaptive Grouped Card Feed)**

When the screen width drops below 720 pixels, the viewport adapts into a vertical, touch-friendly structure enforcing the exact hierarchy:

* **Episode-Scene Subheaders (Top Level):** Takes are grouped under high-contrast, collapsible subheader bars representing the active setup (e.g., ΕΠ 10 \- ΣΚ 40 (3 Takes) \[Collapse\]).  
* **Dual Setup-Incremental Controls:** To respect the Shot → Take hierarchy, the subheader card features two explicit quick-action buttons:  
  1. \[ \+ SHOT \] **Button:** Closes the current shot setup. Automatically queries the highest shot number recorded within this Episode-Scene group, increments the Shot (ΠΛΑΝΟ) number by 1, and initializes **Take 1** for this new setup.  
  2. \[ \+ TAKE \] **Button:** Logs a subsequent take for the active setup. Duplicates the active Shot (ΠΛΑΝΟ) number and increments the Take (ΛΗΨΗ) number by 1\.  
* **Floating Setup Button:** A prominent \[ \+ New EP-Scene \] button floating in the bottom corner prompts you for a new Episode and Scene string to begin an entirely fresh setup group.  
* 

## **5\. Core Logging Gestures & Visual Shorthands**

### **5.1 Single-Tap on Take (ΛΗΨΗ)**

Toggles **Circled Take** status. The take number is rendered inside a prominent, hand-drawn-style black circle.

### **5.2 Double-Tap on Take (ΛΗΨΗ)**

Toggles **Failed/Bad Take** status. The row text opacity drops to 50%, and a clean, centered red strike-through line is drawn across the entire take record.

### **5.3 Pickup (PU) Modifier**

A fast, dedicated \[PU\] mini-toggle sits immediately adjacent to the Take number field (on both desktop inline edit and mobile card view). Tapping it flags the record as a Pickup, rendering a clean superscript "PU" next to the take digit.

### **5.4 Blooper Checkbox**

When checked, a narrow, vertical orange badge reading **"BLOOPER"** is appended directly inside the far-right edge of that take's observations cell.

### **5.5 ΑΚΥΡΟ CLIP Toggle (Camera Specific)**

Triggered from a row or cell options menu. Prompts you to choose which active cameras suffered a false clip.

* **Height Adjustment:** The row height collapses to a shallow **16 points**.  
* **Offending Cell(s):** Selected camera cells display **"ΑΚΥΡΟ CLIP".** All other cells are overlaid with a tight, repeating **"XXXXXXX"** cross-stitch pattern.  
* **Whole-Row Safety Constraint:** All other cells across the entire row are automatically filled with the same tight **"XXXXXXX"** cross-stitch pattern, completely crossing out the line to explicitly mark it as unusable media.

### **5.6 Diagonal Camera Slashes**

If a camera did not roll for a take, swiping or tapping "No Roll" on that camera cell draws a clean diagonal line across it.

### **5.7 Quick-Tag Chips (False & Long Starts)**

When typing observations inside the notes cell, a quick-bar of tap-and-go buttons is available:

* \[FS\] **Chip:** Tap once to prepend \[FS\] to your notes. Once prepended, tap a small \+ icon to increment to \[FS x2\], or tap a \- icon to remove or decrease by one.  
* \[LS\] **Chip:** Tap once to prepend \[LS\] (Long Start).  
* **Row Swiping (Mobile Quick Drawer):** Swiping right on any take card slides out a quick-action drawer containing thumb-friendly buttons for \[FS\], \[LS\], and \[ΑΚΥΡΟ\].  
* 

### **5.8 Change Camera Roll**

During a shooting day, camera assistants may swap out camera rolls. A dedicated **"Change Roll"** button is available in the row options menu. Tapping it prompts you to select a new roll number for that camera, and the change is reflected immediately in the database and UI. The new camera roll number shows directly above its first clip in a separate short row. For example, when the camera roll for CAM A changes from A110 to A111, a small "A111" underlined text must appear on the CAM A column, directly above the first take's row of the new roll.

IMPORTANT ΝΟΤΕ/UPDATE: The camera-specific and take-specific actions (Void Clip, No Roll, Change Camera Roll, Circled Take, Crossed-out/bad take) have now been moved to a context menu, accessible on desktop via right-click. Mobile implementation TBD, but likely a long-press gesture on the cell.


## **6\. Cross-Day Scene Continuity & Shot Memory**

1. **Automatic Database Query:** The moment you enter or create an Episode and Scene combination within the active project, the app queries the global database for previous takes of that exact setup across all days of the active project.  
2. **Continuity Prompt:** If historical takes are found, the app halts and displays a high-visibility continuity notification:  
   * *"Ep 10 \- Scene 40 was previously shot on Day 53\. Last recorded setup was SHOT 3 TAKE 2."*  
3. **Action Pathways:**  
   * \[ Continue with SHOT 4 \] **(Default/Recommended):** Automatically sets the new record to Shot 4, copies the active camera roll numbers, camera descriptions, and sound rolls from the last known take of that scene.  
   * \[ Start over from SHOT 1 \]**:** Overrides continuity logic and initializes a fresh sequence.  
   * \[ Custom Shot Number... \]**:** Allows manual alphanumeric entry.

## **7\. Local-First Syncing & Battery Optimization**

1. **Instant Write Performance:** Every single keypress, tap, or gesture writes exclusively to the local SQLite database in under **2 milliseconds**.  
2. **The Sync Queue Outbox:** Every local write simultaneously logs a lightweight instruction command inside a local SQLite SyncQueue table.  
3. **Debounced Network Requests:** The background sync engine utilizes a **3-second debounce timer**. The app waits until you pause typing for 3 seconds before bundling changes and executing a single compressed sync request.  
4. **Event-Driven & Network-Aware Sync Worker:**  
   * The sync engine stays in a deep sleep mode until a change is committed to the local queue.  
   * If the device is offline, the worker immediately returns to sleep rather than continuously polling.  
   * If the device enters native OS "Battery Saver Mode," the background sync shifts to a manual "pull-to-sync" mode.

## **8\. Emergency AirDrop / Local Share Backup**

* A prominent **"Local Database Backup"** button sits in the mobile app settings.  
* Bundles your active project's local SQLite database file (logshot.db) and lets you share it directly via Bluetooth, Android Quick Share, or local USB transfer to your laptop.  
* Ensures 100% data safety regardless of cloud connectivity on deep soundstages or remote locations.

## **9\. PDF Export Engine (QuestPDF Specifications)**

* **Strict White-Label Output:** The PDF contains **zero app branding or watermarks**. It is a professional deliverable belonging entirely to the production.  
* **Header Title:** The main document header reads dynamically as: \[Project Name\] • ΔΕΛΤΙΟ ΛΗΨΕΩΝ.  
* **Orientation:** Locked to **A4 Portrait**.  
* **Page Budget Layout:** Balanced to fit exactly **12 rows of takes** per physical page, reserving perfect spacing for headers and footers.  
* **Header Repetition:** For multi-page reports, automatically repeats the top-margin scribble, main metadata header block, and table column headers. If a scene continues to the next page, add the episode and scene numbers again in the first row in their respective cells.  
* **Adaptive Column Sizing:** Dynamically recalculates and scales column widths for dynamic multi-camera setups (CAM C, CAM D).  
* **"X / Y" Dynamic Page Numbering:** Standard footer rendering outputting Σελίδα X / Y (e.g., Σελίδα 2 / 4).  
* **Shorthand and Symbol Precision:**  
  * **Pickup (PU):** Rendered as a clean, small superscript "PU" directly trailing the Take digit.  
  * **Circled Takes:** Rendered as a clean, solid black circle around the take number. We must account for the occasional PU indicator, so that a circle around a simple take, and the circle around a pickup take, are the same size. Takes never exceed two digits, so the circle size should be designed to fit two digits and a PU indicator at maximum.  
  * **Failed Takes:** Rendered with a centered red strike-through line on the take number.  
  * **ΑΚΥΡΟ CLIP (False Roll Layout):** Row height drops to **16 points**. Offending camera cells display **"ΑΚΥΡΟ CLIP"**. All remaining cells in the row are filled entirely with the repeating **"XXXXXXX"** pattern.  
  * **Bloopers:** Rendered as a vertical box reading “BLOOPER” aligned to the far right of the notes cell.  
  * **Quick-Tags:** Rendered as clean, thin-bordered rectangular badges (\[FS\], \[LS\]) at the start of the notes text.  
  * **Diagonal No-Roll Slashes:** Rendered as a clean diagonal line crossing out any unused camera setup cells from top-right to bottom-left.  
  * **END DAY Unused Space Filler:** On the final page of a finalized day, calculates remaining unused row slots and replaces them with a massive repeating diagonal cross-hatch pattern, stamping **"END DAY \[Day Identifier\]"** cleanly in the center.

