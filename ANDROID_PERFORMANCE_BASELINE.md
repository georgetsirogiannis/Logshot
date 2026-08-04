# Android Performance Baseline Measurement Guide

## Overview
This document provides instructions for measuring Android performance metrics on a Pixel 8 device. Complete these measurements before and after optimization to validate improvements.

## Prerequisites
- Pixel 8 device connected via USB with Developer Options enabled
- Android Debug Bridge (adb) installed
- APK built in Release configuration

## Baseline Metrics to Collect

### 1. Cold Startup Time
**What to measure:** Time from app launch to "Ready" status display

**How to measure:**
1. Force-stop the app: `adb shell am force-stop com.georgetsirogiannis.Logshot`
2. Clear app data (optional, for true cold start): `adb shell pm clear com.georgetsirogiannis.Logshot`
3. Launch with timing: `adb shell am start-activity -W com.georgetsirogiannis.Logshot/.MainActivity`
4. Look for "TotalTime" in the output (or use logcat for the [PERF] log)
5. Record the time
6. Repeat 5 times and calculate average

**Automated logcat monitoring:**
```powershell
adb logcat -c
adb logcat | Select-String "\[PERF\] Startup"
```

### 2. Project/Day Navigation Time
**What to measure:** Time to switch from one day to another

**Steps:**
1. Open app and select a project with multiple days
2. Monitor logcat: `adb logcat | Select-String "\[PERF\] Loaded"`
3. Tap different days in the navigation drawer
4. Record the "Loaded X takes in Yms" messages
5. Test with both small (5-10 takes) and large (50+ takes) days

### 3. Scrolling Performance
**What to measure:** Frame drops and jank during scroll

**Tools:**
- Enable "Profile GPU Rendering" in Developer Options → set to "On screen as bars"
- Green bars should stay below the horizontal line (16.67ms for 60fps)

**Steps:**
1. Open a day with 30+ takes
2. Scroll rapidly up and down
3. Observe GPU rendering bars
4. Count visible spikes above the line (frame drops)
5. Use `adb shell dumpsys gfxinfo com.georgetsirogiannis.Logshot` for detailed stats

### 4. Text Edit Responsiveness  
**What to measure:** Database save count during editing session

**Steps:**
1. Open a day and start editing a take field
2. Type continuously for 10 seconds
3. Check logcat for save counts: `adb logcat | Select-String "Take.SaveCount"`
4. Or add this debug command to view current counter after editing:
   - In code, print `PerformanceDiagnostics.Instance.GetCount("Take.SaveCount")`
5. Record the number of database writes triggered

### 5. Memory Usage
**What to measure:** Memory consumption during normal use

**Command:**
```powershell
adb shell dumpsys meminfo com.georgetsirogiannis.Logshot
```

**Record:**
- Total PSS (column 1, rightmost value)
- Native Heap
- Dalvik Heap

### 6. Startup CPU/Memory Profile
**Advanced measurement using Android Studio:**
1. Open Android Studio
2. Run → Profile 'Logshot.Android'
3. Wait for app launch
4. Stop profiling after "Ready" status appears
5. Export CPU/Memory flamegraph

## Test Data Setup

Create test scenarios:
- **Small Day:** 5-10 takes, 2 cameras
- **Medium Day:** 25-35 takes, 3 cameras, mixed scenes
- **Large Day:** 60+ takes, 4 cameras, many scenes

## Baseline Results Template

```
=== BASELINE RESULTS ===
Date: [DATE]
Device: Pixel 8
Android Version: [VERSION]
Build: Release APK

Cold Startup (5 runs):
  Run 1: ___ ms
  Run 2: ___ ms
  Run 3: ___ ms
  Run 4: ___ ms
  Run 5: ___ ms
  Average: ___ ms

Day Load Times:
  Small day (10 takes): ___ ms
  Medium day (30 takes): ___ ms
  Large day (60 takes): ___ ms

Scrolling (Large day):
  Frame drops observed: ___
  Janky frames %: ___

Text Edit (10 second session):
  Database saves triggered: ___

Memory (after loading large day):
  Total PSS: ___ MB
  Native Heap: ___ MB
  Dalvik Heap: ___ MB

Notes:
[Any relevant observations]
```

## Log Extraction Commands

```powershell
# Clear logs before test
adb logcat -c

# Capture performance logs during test
adb logcat -s "Logshot:*" "PERF:*" > baseline-logs.txt

# View metrics summary (if implemented in app)
adb logcat | Select-String "Performance Metrics"
```

## Automated Baseline Script

```powershell
# baseline-test.ps1
Write-Host "Starting baseline measurement..."

# Cold start test
for ($i = 1; $i -le 5; $i++) {
	Write-Host "Cold start run $i..."
	adb shell am force-stop com.georgetsirogiannis.Logshot
	Start-Sleep -Seconds 2
	adb shell am start-activity -W com.georgetsirogiannis.Logshot/.MainActivity
	Start-Sleep -Seconds 5
}

# Memory snapshot
Write-Host "Capturing memory usage..."
adb shell dumpsys meminfo com.georgetsirogiannis.Logshot | Out-File -Append baseline-results.txt

# GPU stats
Write-Host "Capturing GPU rendering stats..."
adb shell dumpsys gfxinfo com.georgetsirogiannis.Logshot | Out-File -Append baseline-results.txt

Write-Host "Baseline measurement complete. Check baseline-results.txt"
```

## After Optimization

### Build Configuration Validation
- `AndroidEnableProfiledAot=true` was validated with a .NET 10 Android Release build.
- Validation result: succeeded with exit code 0 on the supported `android-arm64` and `android-x64` runtime identifiers.
- The build emitted the existing AndroidX `NU1608` dependency-constraint warnings; no new build errors were introduced.
- Device startup and APK-size measurements still need to be collected on the Pixel 8.

Run the same measurements and compare:
- Startup time improvement %
- Day load time improvement %
- Database save reduction %
- Frame drop reduction
- Memory usage change

Target improvements:
- 20-30% faster cold startup
- 30-50% faster day loading
- 80-90% fewer database writes during text editing
- <5% frame drops during scrolling
- Stable or reduced memory footprint
