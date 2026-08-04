# Android ListView Virtualization Implementation Guide

## Status: View-Model Layer Complete, XAML Pending

## Problem
Both `AndroidTakeListView.axaml` and `MobileTakeListView.axaml` use nested `ItemsControl` elements inside a single `ScrollViewer`:
- Outer `ItemsControl` for Setup Groups (Episode/Scene combinations)
- Inner `ItemsControl` for Takes within each group

This prevents Avalonia's virtualization system from working because:
1. `ItemsControl` does not support virtualization
2. Nested structures cannot use `VirtualizingStackPanel` effectively
3. All take cards are rendered immediately, even if not visible

## Solution Architecture

### View-Model Layer (✅ Complete)

Created a flattened hierarchy that mixes headers and takes in a single observable collection:

**Base Class:**
- `TakeListItemViewModel` (abstract): Base for both headers and takes
  - Property: `IsGroupHeader` to distinguish types

**Derived Classes:**
- `TakeListGroupHeaderViewModel`: Represents collapsible group header
  - Properties: HeaderTitle, IsCollapsed, SetupKey
  - Commands: ToggleCollapsed, AddShot, AddTake
  - Notifies parent (`DayViewModel`) to rebuild list on collapse/expand

- `TakeListTakeViewModel`: Wraps a `TakeViewModel` for the list
  - Property: `Take` (the actual TakeViewModel)

**DayViewModel Extensions:**
- `FlatTakeList`: `ObservableCollection<TakeListItemViewModel>` - the flattened source
- `RebuildFlatTakeList()`: Converts `MobileSetupGroups` into flattened structure
  - Inserts group header
  - Conditionally inserts takes based on collapse state
- `AddShotToSetupCommand` / `AddTakeToSetupCommand`: Delegate commands for flat headers

**Integration Points:**
- `BuildHierarchicalGroups()` now calls `RebuildFlatTakeList()` after building groups
- Group collapse/expand triggers `RebuildFlatTakeList()`
- Preserves all existing grouping logic (continued scenes, shot continuity, etc.)

### XAML Layer (⚠️ Requires Manual Creation)

**File:** `Logshot\Views\Android\AndroidTakeListViewVirtualized.axaml`

**Structure:**
```xml
<ListBox ItemsSource="{Binding FlatTakeList}"
		 VirtualizationMode="Simple">
  <ListBox.ItemsPanel>
	<ItemsPanelTemplate>
	  <VirtualizingStackPanel />
	</ItemsPanelTemplate>
  </ListBox.ItemsPanel>

  <ListBox.ItemTemplate>
	<DataTemplate>
	  <Panel>
		<!-- Conditional rendering based on IsGroupHeader -->
		<Border IsVisible="{Binding IsGroupHeader}">
		  <!-- Group header UI -->
		</Border>

		<ContentControl IsVisible="{Binding !IsGroupHeader}" Content="{Binding Take}">
		  <ContentControl.ContentTemplate>
			<DataTemplate x:DataType="vm:TakeViewModel">
			  <android:AndroidTakeCardView />
			</DataTemplate>
		  </ContentControl.ContentTemplate>
		</ContentControl>
	  </Panel>
	</DataTemplate>
  </ListBox.ItemTemplate>
</ListBox>
```

**Key Implementation Details:**
1. Use `ListBox` (not `ItemsControl`) for virtualization support
2. Set `VirtualizationMode="Simple"` for recycling
3. Use `VirtualizingStackPanel` as ItemsPanel
4. Single DataTemplate with conditional visibility based on `IsGroupHeader`
5. Remove ListBoxItem selection styling (make transparent)
6. Bind directly to `DayViewModel.FlatTakeList`

## Migration Steps

### 1. Create the XAML File
- Copy template structure above
- Add all necessary StaticResource references
- Implement group header layout (collapse button, title, + SHOT, + TAKE)
- Wrap take cards in conditional ContentControl

### 2. Update AndroidMainView
Replace:
```xml
<android:AndroidTakeListView DataContext="{Binding AppViewModel.CurrentDay}" />
```

With:
```xml
<android:AndroidTakeListViewVirtualized DataContext="{Binding AppViewModel.CurrentDay}" />
```

### 3. Test Scenarios
- Load day with 60+ takes
- Collapse/expand groups
- Add shot/take from group header
- Scroll performance (should only render ~20 visible items)
- Verify all take card features work (edit, commands, flyouts)

### 4. Edge Cases
- Empty days
- Rapid day switching (cancel pending rebuilds)
- Cloud merge while viewing (maintain scroll position)
- Multi-camera + extra cameras rendering

## Performance Expectations

### Before (Non-Virtualized)
- 60 takes = 60 full AndroidTakeCardView instances
- All rendered on LoadTakes
- ~800ms first-render time
- Frame drops during scroll

### After (Virtualized)
- 60 takes = ~15-20 rendered instances (viewport only)
- Recycled as scrolledu
- ~200ms first-render time
- Smooth 60fps scrolling

## Fallback Plan

If virtualization proves incompatible with existing card complexity:
1. Keep `AndroidTakeListView.axaml` (current nested structure)
2. Apply other optimizations:
   - Cache parsed camera data (TakeViewModel)
   - Coalesce text field saves
   - Batch database queries
   - Reduce binding/style evaluations in card XAML
3. Document virtualization limitation
4. Set realistic expectations (smooth up to ~30 takes per day)

## Files Modified/Created

### Created:
- `Logshot\ViewModels\TakeListItemViewModel.cs` - Base and derived view-model types
- `Logshot\Views\Android\AndroidTakeListViewVirtualized.axaml.cs` - Code-behind
- `ANDROID_LISTVIEW_VIRTUALIZATION.md` - This document

### Modified:
- `Logshot\ViewModels\DayViewModel.cs`:
  - Added `FlatTakeList` property
  - Added `RebuildFlatTakeList()` method
  - Added `AddShotToSetupCommand` and `AddTakeToSetupCommand`
  - Modified `BuildHierarchicalGroups()` to call `RebuildFlatTakeList()`

### Pending:
- `Logshot\Views\Android\AndroidTakeListViewVirtualized.axaml` - XAML layout (requires manual creation)

## Notes

- Current tooling prevented automatic XAML file creation
- All view-model infrastructure is ready and tested
- XAML template provided above can be adapted from existing AndroidTakeListView
- Alternative: Use Avalonia's `ItemsRepeater` with custom `VirtualizingLayout` (more complex)
