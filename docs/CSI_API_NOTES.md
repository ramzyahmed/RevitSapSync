# CSI API notes (ETABS and SAP2000)

Hard-won findings from live testing. They cannot be rediscovered in a cloud session (no ETABS/SAP2000 there), so read them before touching any code that talks to ETABS or SAP2000. Verified on ETABS 21, 22.0, 22.7 and 23 and SAP2000 25, unless a note says otherwise.

## Never do this
- **Never attach a probe or test script to the user's open ETABS/SAP2000 windows.** Two read-only probes closed the user's ETABS (possible lost unsaved work). Test only against a scratch instance you start yourself, track its PID, and kill only that PID.
- **Do not trust API help text for geometry.** Check it against the program's own picture or ask for a screenshot (see rebar orientation below).
- **Check the product's own API reference listing before relying on a method.** A method that exists in the compiled interop DLL can still be missing or at a different position in the other product.

## One compiled DLL, two products: vtable-order hazard
Code compiled against one `CSiAPIv1.dll` (for example SAP2000 25's) also runs on ETABS. COM dispatch is by interface *position*, so a method declared at a different position in ETABS's interface than in SAP2000's calls the **wrong method** on ETABS.
- Example: `LoadCases.GetTypeOAPI(name, ref type, ref subType)` threw an uncatchable `AccessViolationException` on ETABS (SAP2000 declares `GetTypeOAPI_1` 5th and `GetTypeOAPI` 7th, ETABS the opposite order). Related symptoms: `GetPresentUnits_2` and `GridSys.GetNameList` returning -100, `GetTube_1` returning 1.
- Before using any `*_N`-suffixed or recently added method (especially in cLoadCases, cCombo, cPropFrame), compare the method order of that interface in the ETABS and SAP2000 API references. If positions differ, derive the value another way (for example use the type implied by `GetNameList(caseType)` instead of `GetTypeOAPI`).
- A try/fallback only protects methods that return an error code, not an access violation.
- `PointObj.GetAllPoints` is ETABS-only (returns -100 on SAP2000). For both products use `PointObj.GetNameList` plus `GetCoordCartesian` per joint.

## Attaching to ETABS
- ETABS 22.0: the compiled-in SAP2000-25 `Helper.GetObject` builds wrapper objects that fetch about 80 sub-objects eagerly, and ETABS 22.0 dies during the attach (`AccessViolationException` in `SysFreeString`). ETABS 21 and 23 survive it. Fix used: look the object up in the Running Object Table (`!CSI.ETABS.API.ETABSObject:<pid>`) and cast to `cOAPI`. SAP2000 can still use `Helper.GetObjectProcess`.
- ETABS 22.0 `LoadCases` sub-objects (StaticLinear, StaticNonlinear, ModalEigen and so on) sit at different interface positions and give `InvalidCastException` or crash ETABS. `LoadCases.GetNameList` and the other sub-objects are fine. Skip static-case load lists on ETABS 22.0.x.
- Revit add-ins: SAP2000 attach uses `Helper.GetObject` (net48) or a reflection-built `wOAPI` (Revit 2025 / .NET 8). A direct `cOAPI` cast fails when SAP2000 27 owns the COM registration while SAP2000 25 runs. Add-in Manager reload keeps stale same-identity CSI DLLs in the Revit process, so tell the user to restart Revit after DLL changes.

## Units
- **ETABS returns database units, not present units.** On ETABS 22.7 a model whose `GetPresentUnits()` said `kN_m_C` still returned coordinates and section dimensions in mm (database units `kN_mm_C`). Do not read units with `GetPresentUnits()` on ETABS; use the database units. SAP2000 follows the present units.
- On the ETABS 22.0 scratch model the values followed the present units instead. If lengths ever come out 1000x off, suspect present-versus-database units.
- **ETABS 22.0 story elevations are 1000x** the present length unit (`Story.GetElevation`, `GetStories_2`), while frames, points and sections follow the present unit. Calibrate the scale (1, 0.001 or 1000) by matching story elevations to real frame-end elevations. `GetStories_2` also returns a `BaseElevation` (the unnamed "Base" level, not in the story list).
- SAP2000 `Results.JointReact` returns kN,m-style present units. ETABS results units are not verified live.

## Design overwrites
- Steel design overwrite **item numbers differ by product** for the same code. AISC 360-16 on ETABS 22: 26/27/28 Unbraced Length Ratio Major/Minor/LTB, 29-33 K1 Major, K1 Minor, K2 Major, K2 Minor, K LTB, 34/35 Cm Major/Minor, 36 Cb (18-22 are absolute deflection limits). SAP2000 numbers the same items 18-28. Always pick the table by product, and block copying overwrites between ETABS and SAP2000.
- A frame that is not a steel member answers `GetOverwrite` with value 0 and `ProgDet=false`. 0 means "program determined" for these items, so ignore zero values when listing user assignments.
- Only read design overwrites for the frame's matching material (steel overwrites on a concrete frame fail).

## Editing and geometry
- **ETABS has no replicate.** `EditGeneral.ReplicateLinear/Mirror/Radial` exist in the SAP2000-25 interop but ETABS answers -99; ETABS's API only has `cEditGeneral.Move`. Build copies with `AddByCoord` / `AddCartesian` plus the original's assignments.
- **Concrete column rebar orientation:** `NumberR3Bars` is the bars on the faces of the section **width (T2)**; `NumberR2Bars` is the **depth (T3)**. The API help text suggests the opposite.
- ETABS reports a never-configured concrete rectangle as a Column (`GetTypeRebar=1`), so set beams to Beam explicitly.

## Testing tips (local only)
- On a scratch ETABS 22, `File.NewBlank` / `File.NewGridOnly` can throw `NullReferenceException`; `InitializeNewModel(kN_mm_C)` alone gives default stories Story1-4 (3 m), enough for tests.
- Do not test `Get-Process -Id X` for "has exited" by `-not $null`: it errors when the process is gone. Test `$null -ne (Get-Process -Id X -ErrorAction SilentlyContinue)`.
- PowerShell 5.1 cannot read `init`/`required` members of .NET 8 DLLs and its COM adapter cannot call methods on a raw RCW. Use a small throwaway net48 console project as the live-test harness.
- PowerShell 5.1 `Get-Content -Raw` plus `Set-Content` corrupts UTF-8 in source files. Use an editor, or read and write with explicit UTF-8.