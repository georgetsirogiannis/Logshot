# Logshot Architecture & Current State

**Last Updated:** Phase 2, Steps 1-3 Complete  
**Project Target:** .NET 10  
**UI Framework:** Avalonia (cross-platform desktop, Android, iOS, browser)

---

## Table of Contents

1. [High-Level Architecture](#high-level-architecture)
2. [Data Layer (Models)](#data-layer-models)
3. [Service Layer](#service-layer)
4. [ViewModel Layer (MVVM)](#viewmodel-layer-mvvm)
5. [How Data Flows Through the App](#how-data-flows-through-the-app)
6. [Camera Data Management](#camera-data-management)
7. [Cross-Day Continuity](#cross-day-continuity)
8. [Current Limitations & Next Steps](#current-limitations--next-steps)

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    UI Layer (Views)                     │
│         MainView.axaml, MainWindow.axaml, etc.         │
└──────────────────┬──────────────────────────────────────┘
				   │
┌──────────────────▼──────────────────────────────────────┐
│              ViewModel Layer (MVVM)                     │
│  AppViewModel, ProjectViewModel, DayViewModel,         │
│  TakeViewModel, MainViewModel                          │
└──────────────────┬──────────────────────────────────────┘
				   │
┌──────────────────▼──────────────────────────────────────┐
│              Service Layer                              │
│  DatabaseService, CameraDataManager,                   │
│  ContinuityService, SupabaseService                    │
└──────────────────┬──────────────────────────────────────┘
				   │
┌──────────────────▼──────────────────────────────────────┐
│           Data Layer (Models & Storage)                 │
│  Project, Day, Take (SQLite database)                  │
└─────────────────────────────────────────────────────────┘
```

The app follows **MVVM (Model-View-ViewModel)** architecture:
- **Models** define the database schema and data structures
- **Services** handle business logic and data access
- **ViewModels** bridge the services and UI, exposing observable properties
- **Views** bind to ViewModels and present the UI

---

## Data Layer (Models)

### **Project Model** (`Models/Project.cs`)

Represents a single film/TV project within Logshot.

```csharp
public class Project
{
	public string Id { get; set; }                    // Unique identifier (GUID)
	public string Title { get; set; }                 // Project name (e.g., "Season 1 - Episode 5")
	public string ProductionCompany { get; set; }     // Production house name
	public string Director { get; set; }              // Director's name
	public string DP { get; set; }                    // Director of Photography
	public string Producer { get; set; }              // Producer's name
	public string GeneralNotes { get; set; }          // Project-wide notes/metadata
	public DateTime CreatedAt { get; set; }           // Timestamp of project creation
}
```

**Purpose:** Container for all Days and Takes within a single production.

---

### **Day Model** (`Models/Day.cs`)

Represents a single shoot day within a Project.

```csharp
public class Day
{
	public string Id { get; set; }                    // Unique identifier (GUID)
	public string ProjectId { get; set; }             // Foreign key to Project
	public string ShootDayNumber { get; set; }        // "Day 1", "Day 2", etc.
	public DateTime CalendarDate { get; set; }        // The actual calendar date (e.g., 2024-01-15)
	public string GeneralNotes { get; set; }          // Day-level production notes
	public string TopScribbleNotes { get; set; }      // Quick handwritten-style notes
	public bool IsFinalized { get; set; }             // True when the day's work is locked
	public DateTime CreatedAt { get; set; }           // Timestamp of day creation
}
```

**Purpose:** A container for all Takes shot on a particular day. When finalized, the day's data is locked and cannot be edited.

---

### **Take Model** (`Models/Take.cs`)

Represents a single take (one attempt to shoot a scene).

```csharp
public class Take
{
	public string Id { get; set; }                    // Unique identifier (GUID)
	public string DayId { get; set; }                 // Foreign key to Day

	// Hierarchy
	public int SequenceOrder { get; set; }            // Order within the day (for drag-and-drop)
	public string Episode { get; set; }               // Episode number or identifier
	public string Scene { get; set; }                 // Scene identifier
	public int Shot { get; set; }                     // Shot number within the scene
	public int TakeNumber { get; set; }               // Take 1, Take 2, etc.

	// Camera Data (stored as JSON)
	public string CameraData { get; set; } = "{}";    // Dynamic camera metadata (see Camera Data section below)
	public string SoundNotes { get; set; }            // Sound department notes

	// Annotations
	public string TakeNotes { get; set; }             // General take notes
	public int FalseStartCount { get; set; }          // Number of false starts
	public bool IsLongStart { get; set; }             // LS marker
	public bool IsCircled { get; set; }               // Circled (good take)
	public bool IsFailed { get; set; }                // Failed/unusable
	public bool IsPickup { get; set; }                // Pickup shot
	public bool IsBlooper { get; set; }               // Blooper/outtake

	// False Clip Tracking
	public string VoidCameraLabels { get; set; } = "[]"; // JSON array of cameras with void/no-roll

	public DateTime CreatedAt { get; set; }           // Timestamp of take creation
}
```

**Purpose:** Stores detailed information about a single take, including:
- Hierarchical identifiers (Episode, Scene, Shot, Take Number)
- Dynamic camera assignments (stored as JSON in `CameraData`)
- Multiple boolean flags for shorthand annotations (Circled, Failed, Blooper, etc.)
- Track which cameras had "no roll" or "void" status

---

## Service Layer

### **DatabaseService** (`Services/DatabaseService.cs`)

Handles all SQLite database operations. Uses the `SQLite-net-pcl` library with async support.

#### **Key Responsibilities:**

1. **Initialization:**
   - `InitAsync()` - Ensures DB is initialized on first app launch
   - Creates tables for Project, Day, Take automatically

2. **CRUD Operations:**
   - `SaveTakeAsync(Take)` - Insert or update a take
   - `SaveDayAsync(Day)` - Insert or update a day
   - `SaveProjectAsync(Project)` - Insert or update a project
   - `DeleteTakeAsync(Take)` - Remove a take

3. **Query Operations (Phase 2 Step 3):**
   - `GetTakesForDayAsync(dayId)` - Retrieve all takes for a specific day
   - `GetTakesForProjectAsync(projectId)` - Retrieve **all takes across all days** in a project
   - `GetTakesForEpisodeSceneAsync(projectId, episode, scene)` - Find all takes matching Episode/Scene
   - `GetDayAsync(dayId)` - Retrieve a specific day
   - `GetDaysForProjectAsync(projectId)` - Retrieve all days in a project

#### **Key Features:**

- **Async/Await Pattern:** All operations are async to prevent UI blocking
- **Cross-Platform:** SQLite database stored in `Environment.SpecialFolder.LocalApplicationData`
- **Automatic Schema:** Uses C# attributes to auto-generate SQL tables from models

#### **Example Usage:**

```csharp
var dbService = new DatabaseService();
await dbService.InitAsync();

// Save a take
var newTake = new Take { Episode = "1", Scene = "5", Shot = 3, ... };
await dbService.SaveTakeAsync(newTake);

// Query historical takes for Episode 1, Scene 5
var previousTakes = await dbService.GetTakesForEpisodeSceneAsync(projectId, "1", "5");
```

---

### **CameraDataManager** (`Services/CameraDataManager.cs`)

Manages the serialization/deserialization of camera metadata stored in `Take.CameraData` (JSON).

#### **Data Structure:**

```csharp
public class CameraState
{
	public string Label { get; set; } = string.Empty;      // e.g., "Main", "B-Cam", "Steadicam"
	public bool IsActive { get; set; } = true;
	public bool IsStrikethrough { get; set; } = false;     // Grayed out if camera wasn't used at take time
	public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public class CameraDataStructure
{
	public List<CameraState> Cameras { get; set; } = new();
}
```

The `CameraData` JSON in a Take looks like:
```json
{
  "cameras": [
	{ "label": "Main", "isActive": true, "isStrikethrough": false, "addedAt": "2024-01-15T10:30:00Z" },
	{ "label": "B-Cam", "isActive": true, "isStrikethrough": true, "addedAt": "2024-01-15T09:00:00Z" },
	{ "label": "Steadicam", "isActive": false, "isStrikethrough": false, "addedAt": "2024-01-16T08:00:00Z" }
  ]
}
```

#### **Key Methods:**

- `ParseCameraData(string json)` - Deserialize JSON into `CameraDataStructure`
- `SerializeCameraData(CameraDataStructure)` - Serialize structure back to JSON
- `InitializeDefaultCameras()` - Create default camera set (Main, B-Cam, Steadicam)
- `AddCamera(cameraLabel, cameraData)` - Add a new camera to the structure
- `RemoveCamera(cameraLabel, cameraData)` - Remove a camera from the structure
- `MarkCameraStrikethrough(addedAt, takeCreatedAt)` - Mark cameras as strikethrough based on when they were added
- `GetActiveCameraLabels(cameraData)` - Return list of active camera labels
- `IsActive(cameraLabel, cameraData)` - Check if a specific camera is active
- `IsCameraStrikethrough(cameraLabel, cameraData)` - Check strikethrough status

#### **Why This Exists:**

Different shoot days may add/remove cameras during production. By storing camera assignments as JSON, we:
- Support **dynamic camera columns** (no fixed schema)
- Track **when cameras were added** (for strikethrough logic)
- Inherit camera setups across days (continuity)
- Keep the database schema simple (just one `CameraData` text field)

#### **Example Usage:**

```csharp
var cameraManager = new CameraDataManager();

// Initialize with defaults for a new day
var cameraData = cameraManager.InitializeDefaultCameras();
var json = cameraManager.SerializeCameraData(cameraData);

// Add a new camera mid-day
var updated = cameraManager.AddCamera("Jib Arm", cameraData);

// Mark older cameras as strikethrough (added before this take's time)
var struck = cameraManager.MarkCameraStrikethrough(DateTime.UtcNow.AddHours(-2), take.CreatedAt);
```

---

### **ContinuityService** (`Services/ContinuityService.cs`)

Looks up historical Episode/Scene data to enable **cross-day continuity** — automatically suggesting the next shot number and inheriting camera setups.

#### **Core Data Class:**

```csharp
public class ContinuityData
{
	public int NextShotNumber { get; set; } = 1;           // Suggested Shot # for this Episode/Scene
	public string InheritedCameraData { get; set; } = "{}"; // Camera setup from last occurrence
	public Take? LastReferenceTake { get; set; }            // The most recent take for context
	public bool HasHistory { get; set; } = false;           // Whether any history exists
}
```

#### **Key Methods:**

- `GetContinuityDataAsync(projectId, episode, scene)` - Main method; returns `ContinuityData` with shot suggestion and inherited cameras
- `GetNextShotNumberAsync(projectId, episode, scene)` - Returns just the next shot number
- `GetInheritedCameraDataAsync(projectId, episode, scene)` - Returns just the camera JSON
- `GetUniqueEpisodeScenesAsync(projectId)` - List all Episode/Scene combos in a project (useful for UI autocomplete)
- `GetEpisodeSceneStatsAsync(projectId, episode, scene)` - Return statistics (total takes, date range, days used)
- `GetRecentTakesAsync(projectId, limit)` - Get the N most recent takes globally
- `GetTakesFromPreviousDayAsync(projectId, currentDayId)` - Retrieve all takes from the previous day

#### **How It Works:**

1. User enters Episode and Scene for a new take
2. `ApplyContinuity(episode, scene)` is called on DayViewModel
3. ContinuityService queries all previous takes with that Episode/Scene
4. If history exists:
   - Find the **maximum Shot number** already used
   - Suggest **Shot N+1** as the next shot
   - **Inherit the camera setup** from the most recent take with that Episode/Scene
5. Pre-fill the new take with these values

#### **Example:**

```
Scenario: Resuming Episode 1, Scene 5 the next day

Previous Day's Takes:
  - Ep1 Sc5 Shot 1 Take 1 (cameras: Main, B-Cam)
  - Ep1 Sc5 Shot 2 Take 1 (cameras: Main, B-Cam)
  - Ep1 Sc5 Shot 2 Take 2 (cameras: Main, B-Cam, Steadicam) ← MOST RECENT

New Day - User Creates Take with Ep1 Sc5:
  ContinuityService.GetContinuityDataAsync(projectId, "1", "5") returns:
  {
	NextShotNumber: 3,                        // 2 + 1
	InheritedCameraData: "{ cameras: [Main, B-Cam, Steadicam] }",
	LastReferenceTake: <Ep1 Sc5 Shot 2 Take 2>,
	HasHistory: true
  }

Result: New take is pre-filled with Shot 3 and the three cameras
```

---

### **SupabaseService** (`Services/SupabaseService.cs`)

*(Incomplete in Phase 2; stub for future cloud sync)*

Intended to handle synchronization with Supabase backend. Currently not fully implemented.

---

## ViewModel Layer (MVVM)

ViewModels use **CommunityToolkit.Mvvm** for automatic property and command generation via attributes.

### **ViewModelBase** (`ViewModels/ViewModelBase.cs`)

Base class for all ViewModels. Inherits from `ObservableObject` (provides `INotifyPropertyChanged` support).

```csharp
public abstract class ViewModelBase : ObservableObject
{
	// Provides INotifyPropertyChanged and [ObservableProperty] support
}
```

---

### **AppViewModel** (`ViewModels/AppViewModel.cs`)

**Top-level orchestrator** for the entire app. Manages Projects collection, current selections, and high-level workflows.

#### **Observable Properties:**

- `Projects` (ObservableCollection<ProjectViewModel>) - All projects
- `CurrentProject` (ProjectViewModel) - Currently selected project
- `CurrentDay` (DayViewModel) - Currently selected day
- `IsProjectSelected` (bool) - Computed/derived state

#### **Methods & Commands:**

- `InitAsync()` - Load projects from database on app startup
- `SelectProjectCommand` - Switch to a different project
- `SelectDayCommand` - Switch to a different day
- `CreateProjectAsync(title, ...)` - Create a new project
- `CreateDayAsync(shootDayNumber, calendarDate)` - Add a day to current project
- `CreateTakeAsync(episode, scene, shot, ...)` - Add a take to current day
- `DeleteProjectAsync(project)` - Remove a project
- `DeleteDayAsync(day)` - Remove a day
- `ExportDayToPdfCommand` - *(Stub)* Export current day as PDF
- `SyncToSupabaseCommand` - *(Stub)* Push data to Supabase

#### **Role:**

Acts as the "router" for the app — tracks top-level selections and orchestrates navigation.

---

### **ProjectViewModel** (`ViewModels/ProjectViewModel.cs`)

Wraps a `Project` model and manages its Days collection.

#### **Observable Properties:**

- `Id`, `Title`, `ProductionCompany`, `Director`, `DP`, `Producer`, `GeneralNotes` - Direct Project fields
- `Days` (ObservableCollection<DayViewModel>) - All days in this project

#### **Methods:**

- `LoadFromModel(Project)` - Populate ViewModel from database model
- `ToModel()` - Convert ViewModel back to database model
- `SaveProjectCommand` - Persist changes to the database
- `LoadDaysCommand` - Load all days for this project from the database
- `AddDayCommand` - Create a new day
- `DeleteDayCommand` - Remove a day

#### **Role:**

Bridges the UI (displaying project details) and the database (persisting project data and managing its days).

---

### **DayViewModel** (`ViewModels/DayViewModel.cs`)

Wraps a `Day` model and manages its Takes collection, camera operations, and continuity.

#### **Observable Properties:**

- `Id`, `ProjectId`, `ShootDayNumber`, `CalendarDate`, `GeneralNotes`, `TopScribbleNotes`, `IsFinalized`, `CreatedAt` - Direct Day fields
- `Takes` (ObservableCollection<TakeViewModel>) - All takes for this day
- `TotalTakes` (int) - Count of takes
- `CurrentShot` (int) - Current/next shot number (UI feedback)
- `ActiveCameras` (ObservableCollection<string>) - Cameras active on this day

#### **Key Methods & Commands:**

**Camera Management:**
- `AddCameraCommand(cameraLabel)` - Add a camera to all takes in this day
- `RemoveCameraCommand(cameraLabel)` - Remove a camera from all takes
- `InitializeCamerasCommand()` - Initialize default cameras for all takes
- `RefreshActiveCamerasCommand()` - Refresh UI list of active cameras

**Take Management:**
- `ReorderTakesCommand()` - Resort takes by sequence order
- `UpdateTotalTakesCommand()` - Update the take count display
- `FinalizeDayCommand()` - Lock the day (mark as finalized)

**Continuity (Phase 2 Step 3):**
- `ApplyContinuity(episode, scene)` - Query continuity data and update UI hints
- `CreateTakeWithContinuity(episode, scene)` - Create a pre-filled take
- `GetContinuityInfoAsync(episode, scene)` - Fetch history info for UI display

#### **Role:**

Central hub for day-level operations. Bridges:
- Day data persistence (database ↔ UI)
- Take collection management
- Camera operations across all takes
- Continuity queries

---

### **TakeViewModel** (`ViewModels/TakeViewModel.cs`)

Wraps a `Take` model and exposes its properties in a bindable way.

#### **Observable Properties:**

- `Id`, `DayId`, `SequenceOrder` - Day reference
- `Episode`, `Scene`, `Shot`, `TakeNumber` - Hierarchy
- `CameraData`, `SoundNotes`, `TakeNotes` - Metadata
- `FalseStartCount`, `IsLongStart`, `IsCircled`, `IsFailed`, `IsPickup`, `IsBlooper` - Annotations
- `VoidCameraLabels` - Struck cameras (JSON)
- `CreatedAt` - Timestamp
- `ActiveCameras` (ObservableCollection<string>) - Current camera list
- `StrikethroughCameras` (Dictionary<string, bool>) - Which cameras are struck
- **`IsFromContinuity` (bool)** - *(Phase 2 Step 5)* Whether this take was pre-filled
- **`ContinuityContext` (string)** - *(Phase 2 Step 5)* Context info (e.g., "Inherited from Shot X")

#### **Methods & Commands:**

- `LoadFromModel(Take)` - Populate from database model
- `ToModel()` - Convert back to database model
- `SaveTakeCommand` - Persist to database
- `RefreshCameraDataCommand()` - Refresh active/strikethrough camera lists
- `ToggleCameraStrikethroughCommand()` - Mark/unmark camera as struck
- Gesture handlers for annotations (circle, fail, false start, etc.)

#### **Role:**

Represents a single take row in the UI grid. Each take displays:
- The take's Episode, Scene, Shot, Take# hierarchy
- Active cameras with their status (active vs. strikethrough)
- Shorthand annotations (Circled, Failed, LS, Blooper, etc.)
- Quick-action buttons and gestures

---

### **MainViewModel** (`ViewModels/MainViewModel.cs`)

Entry point ViewModel; orchestrates app startup and delegates to AppViewModel.

#### **Role:**

1. Accepts `DatabaseService` from App startup
2. Initializes the database
3. Initializes `AppViewModel`
4. Provides `AppViewModel` to the main UI

```csharp
public class MainViewModel : ViewModelBase
{
	public AppViewModel AppViewModel { get; }

	public MainViewModel(DatabaseService databaseService)
	{
		// Initialize AppViewModel and load initial data
	}
}
```

---

## How Data Flows Through the App

### **Scenario 1: Creating a New Take**

```
User clicks "Add Take" button in UI
	↓
MainView.axaml binds to DayViewModel
	↓
CreateTakeWithContinuityCommand called with Episode="1", Scene="5"
	↓
DayViewModel.CreateTakeWithContinuity() executes:
	• Calls ContinuityService.GetContinuityDataAsync()
	• ContinuityService queries DatabaseService for historical takes
	• Returns NextShotNumber and InheritedCameraData
	• Creates new Take model with prefilled values
	• Wraps in TakeViewModel
	• Calls TakeViewModel.SaveTakeCommand
	↓
TakeViewModel.SaveTake() executes:
	• Calls DatabaseService.SaveTakeAsync(take)
	• Database inserts the take to SQLite
	↓
DayViewModel.Takes collection updated
	↓
UI automatically refreshes via binding
	↓
New take row appears in grid
```

### **Scenario 2: Adding a Camera to a Day**

```
User clicks "+ Camera" button, enters "Jib Arm"
	↓
DayViewModel.AddCameraCommand("Jib Arm") executes:
	• For each take in Takes:
		◦ Calls CameraDataManager.ParseCameraData(take.CameraData)
		◦ Calls CameraDataManager.AddCamera("Jib Arm", parsed)
		◦ Calls CameraDataManager.MarkCameraStrikethrough() to gray out old takes
		◦ Calls take.SaveTakeCommand to persist
	↓
All takes updated in database
	↓
UI refreshes to show new "Jib Arm" column
	↓
Old takes show "Jib Arm" as strikethrough (grayed out)
```

### **Scenario 3: Resuming an Episode/Scene the Next Day**

```
New shoot day; user creates a take with Episode="1", Scene="5"
	↓
DayViewModel.CreateTakeWithContinuity("1", "5") executes:
	• Calls ContinuityService.GetContinuityDataAsync(projectId, "1", "5")
	↓
ContinuityService.GetContinuityDataAsync() executes:
	• Calls DatabaseService.GetTakesForEpisodeSceneAsync(projectId, "1", "5")
	• Database queries: SELECT * FROM Takes WHERE Episode="1" AND Scene="5"
	  across all days, ordered by CreatedAt desc
	• Most recent take found: Shot=2, Cameras=[Main, B-Cam, Steadicam]
	• Returns ContinuityData:
		NextShotNumber: 3
		InheritedCameraData: "{ cameras: [Main, B-Cam, Steadicam] }"
		LastReferenceTake: <Take Shot 2>
		HasHistory: true
	↓
DayViewModel receives result, creates Take with:
	Shot: 3                                    (auto-filled)
	CameraData: "{ cameras: [Main, B-Cam, Steadicam] }"  (inherited)
	IsFromContinuity: true
	ContinuityContext: "Inherited from Shot 2"
	↓
Take saved; UI shows "🔗 Continuity" badge on the take row
```

---

## Camera Data Management

### **Why Camera Data is Stored as JSON**

Instead of fixed database columns, Logshot allows **dynamic cameras**:

| Day 1 Cameras | Day 2 Cameras | Day 3 Cameras |
|---|---|---|
| Main, B-Cam | Main, B-Cam, Steadicam | Main, B-Cam, Steadicam, Jib |

If we used fixed columns, the schema would break. By storing cameras as JSON:
- **Flexible:** Add/remove cameras anytime
- **Historical:** Old days keep their camera setup
- **Reusable:** Future days can inherit camera setups via continuity

### **The Strikethrough Logic**

When a camera is **added mid-day**:
1. New takes get the camera (active)
2. **Older takes get the camera marked as strikethrough** (grayed out)

Example:
```
Take 1, 2, 3:     Created with cameras [Main, B-Cam]
				  (Steadicam added)
Take 4, 5, 6:     Have [Main, B-Cam, Steadicam]
				  - Take 4: Steadicam active
				  - Takes 1-3: Steadicam strikethrough (shown grayed out)
```

The `MarkCameraStrikethrough()` method marks a camera as struck if it was added **after** the take was created.

---

## Cross-Day Continuity

### **What It Solves**

Without continuity:
- Resume Episode 1, Scene 5 the next day
- User manually checks: "What was the last shot number?"
- User manually sets camera lineup

With continuity:
- Episode 1, Scene 5 → Auto-suggests Shot #3
- Camera setup → Automatically inherited from last occurrence
- Workflow is **faster and less error-prone**

### **How It Works**

1. **Historical Query:** ContinuityService queries the database for all takes with a specific Episode/Scene
2. **Find Max Shot:** Determines the highest Shot # used so far
3. **Suggest Next Shot:** NextShot = MaxShot + 1
4. **Inherit Cameras:** Fetch CameraData from the most recent take
5. **Pre-fill New Take:** Create the take with these values pre-filled
6. **Mark as Continuation:** Set `IsFromContinuity = true` for UI tracking

### **Example Data Flow**

```
DatabaseService.GetTakesForEpisodeSceneAsync(projectId, "1", "5")
	↓ Queries DB ↓
SELECT * FROM Takes 
WHERE DayId IN (SELECT Id FROM Days WHERE ProjectId = @projectId)
  AND Episode = "1" AND Scene = "5"
ORDER BY CreatedAt DESC
	↓ Returns ↓
[
  { Id: "xyz5", Shot: 2, TakeNumber: 2, CameraData: "...", CreatedAt: "2024-01-16T10:00" },
  { Id: "xyz4", Shot: 2, TakeNumber: 1, CameraData: "...", CreatedAt: "2024-01-16T09:45" },
  { Id: "xyz3", Shot: 1, TakeNumber: 3, CameraData: "...", CreatedAt: "2024-01-15T16:30" },
  { Id: "xyz2", Shot: 1, TakeNumber: 2, CameraData: "...", CreatedAt: "2024-01-15T15:20" },
  { Id: "xyz1", Shot: 1, TakeNumber: 1, CameraData: "...", CreatedAt: "2024-01-15T14:50" }
]
	↓ ContinuityService processes ↓
MaxShot = 2
NextShotNumber = 3
InheritedCameraData = "..." (from xyz5, the most recent)
LastReferenceTake = xyz5
```

---

## Current Limitations & Next Steps

### **What's Currently Implemented**

✅ **Phase 1 - Foundation:**
- Database models (Project, Day, Take)
- SQLite integration
- Basic CRUD operations

✅ **Phase 2.1 - The ViewModels:**
- MVVM architecture with AppViewModel, ProjectViewModel, DayViewModel, TakeViewModel
- Observable properties and command binding

✅ **Phase 2.2 - Dynamic Camera Logic:**
- CameraDataManager for JSON serialization
- Add/remove cameras dynamically
- Strikethrough logic for old cameras

✅ **Phase 2.3 - Cross-Day Continuity Engine:**
- ContinuityService for historical queries
- Auto-suggest next Shot number
- Pre-fill camera setups from previous occurrences
- Mark takes as "from continuity"

### **What's Not Yet Implemented**

❌ **Phase 2.4 - Hierarchical Grouping Logic:**
- Algorithm to group raw Takes into Episode-Scene chunks (for mobile view)
- *(Next step)*

❌ **Phase 3 - Desktop UI:**
- The A4 Grid layout
- Avalonia XAML bindings
- Column drag-and-drop reordering
- Inline editing of cells

❌ **Phase 4 - Mobile UI:**
- Responsive rules for screens < 720px
- Collapsible Episode-Scene headers
- Swipe gestures for quick actions

❌ **Phase 5 - Gestures & Shorthands:**
- Tap/double-tap logic (Circled, Failed)
- Quick-increment buttons (FS, LS)
- Diagonal slashes for "no roll"

❌ **Phase 6 - PDF Export:**
- QuestPDF document generation
- Custom rendering for shorthand notation
- "END DAY" filler block

❌ **Phase 7 - Polish & Production:**
- Supabase synchronization worker
- Emergency backup/export
- Version control audit
- Performance testing with 100+ takes

### **Key Architectural Decisions**

1. **JSON for Cameras:** Flexible, supports dynamic columns, enables inheritance
2. **ContinuityService Separate:** Decoupled continuity logic from DatabaseService makes it testable and reusable
3. **Observable Collections:** MVVM binding makes UI updates automatic and reactive
4. **Async/Await Throughout:** Prevents UI blocking, scales to large data sets
5. **ViewModels as Wrappers:** Clean separation between data (models) and presentation (viewmodels)

---

## How to Extend

### **Adding a New Service**

1. Create `Services/NewService.cs` inheriting from appropriate base
2. Inject into ViewModels via constructor
3. Add observable properties for UI binding
4. Call from ViewModel methods/commands

### **Adding a New ViewModel**

1. Create `ViewModels/NewViewModel.cs` inheriting from `ViewModelBase`
2. Define `[ObservableProperty]` fields (auto-generates properties)
3. Define `[RelayCommand]` methods (auto-generates ICommand)
4. Inject required services via constructor
5. Bind in XAML views

### **Adding UI Views**

1. Create `.axaml` file
2. Bind to ViewModel via `DataContext`
3. Bind controls to observable properties
4. Subscribe to commands with `Button Command="{Binding ...Command}"`

---

## Summary

Logshot is a **MVVM-based film production assistant** managing Takes, Days, and Projects:

- **Models** define the database schema
- **DatabaseService** provides async query/persist operations
- **CameraDataManager** handles dynamic camera JSON serialization
- **ContinuityService** enables cross-day shot/camera pre-filling
- **ViewModels** wrap models and expose observable properties for binding
- **Views** bind to ViewModels and present the UI

The architecture is **modular, testable, and extensible** — each service has a single responsibility, and ViewModels act as the glue between services and UI.

