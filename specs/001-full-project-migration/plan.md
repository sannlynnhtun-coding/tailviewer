# Implementation Plan: Full Tailviewer Migration

**Branch**: `master` (actual Git branch; feature resolver identifier: `001-full-project-migration`) | **Date**: 2026-11-19 | **Spec**: [spec.md](spec.md)

**Input**: Clarified full-project migration, five accepted decisions. This is a plan/design deliverable; phases below are not implemented or passed by this document.

## Summary

မူရင်း shipping features အားလုံးကို Windows x64 ပေါ်တွင် ပြန်လည်ရရှိစေရန် လက်ရှိ Avalonia prototype ကို အဆင့်သေးများဖြင့် တိုးချဲ့မည်။ API/Core logic နှင့် legacy business/settings logic ကို ပြန်သုံးပြီး WPF drawing၊ Metrolib view helpers နှင့် platform integrations ကိုသာ သက်ဆိုင်ရာ boundary တွင် ပြောင်းမည်။ Plugins ကို rebuild/port လုပ်မည်၊ မူရင်း settings မပြောင်းဘဲ import copy/backup ထားမည်။ Updates သည် check/notify သာ automatic ဖြစ်မည်။ Public plugin hosting နှင့် Linux/macOS ကို နောက် milestone ထားမည်။

## Technical Context

**Language/Version**: C# / .NET 10; existing SDK pin 10.0.302, self-contained release runtime pin 10.0.15. Keep pins initially; security/service updates are separate validated changes.

**Primary Dependencies**: Avalonia Desktop/Fluent/Headless 12.1.6 and modern log4net 3.6.0 already used. Legacy dependency declarations and every project edge: [dependency-inventory.md](dependency-inventory.md). Retarget libraries one at a time, then replace blocked integrations as documented in [research.md](research.md).

**Storage**: Existing XML profile/custom-format/plugin configuration; immutable import backup plus separate migrated profile. Repository uses IsabelDb; preserve a verified export/conversion path before any storage replacement. Local logs remain read-only.

**Testing**: Reuse NUnit/Core suites and Avalonia headless/Skia tests. Port legacy application/Archiver/repository/Serilog tests with their owning slice. Native Windows checks cover dialogs, single-instance, service lifecycle and install/update. Do not carry WpfUnit runtime patching into Avalonia.

**Target Platform**: Windows x64 for first acceptance; service integration on loopback test instance. Linux/macOS, ARM64 and public hosting are outside this release gate.

**Project Type**: Desktop app + API/Core libraries + archive/author/repository CLI tools + repository Windows service + plugins/examples + installer/update flow.

**Performance Goals**: SC-004–006: 100 MB first page ≤3 s; 1 GB ≤10 s; p95 navigation ≤200 ms; 1 GB indexing <1 GB app memory; 1,000 lines/s for 5 min, p95 display ≤1 s; recovery ≤3 s after final write. Record reference hardware/cache/fixture details.

**Constraints**: Small logical commits; no silent data deletion; offline local reading; no log uploads; user consent for import/download/install/restart; all extension categories supported after port; no old-binary compatibility promise.

**Scale/Scope**: 28 projects inventoried, including tests/playgrounds/examples; 35 direct package/version declarations. 29 feature families with leaf source checklist, 18 functional requirements and 10 completion outcomes. Inventory totals are a source snapshot, not a fixed feature ceiling.

## Constitution Check

**Before research: PASS.** `.specify/memory/constitution.md` is the generated unratified template, explicitly not policy under the spec. No new constitution or approval gate is invented. Existing user rules apply: reuse code, protect local work/data, small changes, Conventional Commits, Burmese progress.

**After design: PASS.** Reuse existing projects; no new framework/library solely for convenience. Preserve profile/plugin/repository data and validate archive/update trust boundaries. Public hosting and cross-platform acceptance remain deferred as agreed. No unresolved product clarification remains; dependency compatibility is a technical probe with a chosen fallback, not a user-blocking question.

## Project Structure

### Documentation (this feature)

```text
specs/001-full-project-migration/
  spec.md                    # clarified user outcomes
  plan.md                    # this plan and phase gates
  research.md                # migration decisions and alternatives
  feature-inventory.md       # capability families and desktop source checklist
  dependency-inventory.md    # every project edge and direct package declaration
  data-model.md              # identities, persistence and state transitions
  contracts/migration.md     # profile, plugin, CLI/service and update boundaries
  quickstart.md              # validation commands and future acceptance recipes
  checklists/requirements.md # spec-quality checklist, not test results
```

The ordered implementation checklist is now available in [tasks.md](tasks.md), grouped by user story with explicit test and dependency gates.

### Source Code (repository root)

```text
src/Tailviewer.Api/                  # UI-neutral plugin/data contracts
src/Tailviewer.Core/                 # streaming/parser/filter/merge logic
src/Tailviewer/                      # legacy behavior/settings baseline
src/Tailviewer.Avalonia/             # only modern desktop entrypoint
src/Tailviewer.Archiver/             # packaging, metadata and repository client
src/Tailviewer.PluginRepository/     # repository persistence/server/admin CLI
src/Tailviewer.PluginRepository.Service/ # Windows service adapter
src/Plugins/                        # Serilog and plugin-author creator
examples/Tailviewer.LogLevelPlugin/  # author example
src/*Tests/                         # existing suites, migrated by owner slice
src/Installer/                      # legacy installer baseline, retire executable
installer/Tailviewer.Avalonia.iss    # modern per-user Windows installer
scripts/                            # existing exports/build support
Tailviewer.Avalonia.slnx             # expand to every modern shipping/test project
```

**Structure Decision**: Keep current project boundaries. Port reusable business/settings classes into the existing Core project only when UI-independent; viewmodels/control adapters belong in Avalonia. Change each caller/test together. Keep legacy source as reference until parity evidence closes; do not leave it as a required runtime or second installer after cutover.

## Phase 0 — Research and baseline

P00 captures each legacy command/setting/extension operation and current test baseline; generated inventories below seed the work. P01 validates modern dependency edges. Decisions/fallbacks are recorded in research; runtime probes and baseline execution remain implementation work. There is no claim that old binaries or package transports currently run on .NET 10.

## Phase 1 — Design and contracts

[data-model.md](data-model.md) defines stable source/group/bookmark/plugin identities, profile backups and update state. [contracts/migration.md](contracts/migration.md) specifies compatibility/data validation; [quickstart.md](quickstart.md) separates today's commands from future acceptance recipes.

## Small implementation phases

Each phase is multiple small reviewable commits: one behavior or project conversion plus its callers/tests, normally one family at a time. Keep buildable increments; feature branches are optional and never inferred from the virtual feature ID. Do not upgrade unrelated dependencies or replace working Core algorithms just for architecture.

| Phase | Dependencies | Small change sequence | Verification / exit gate | Requirements |
|---|---|---|---|---|
| P00 Baseline | none | Capture clean modern test results; attempt legacy build in isolated environment; expand inventory to exact menu/CLI/settings options; add deterministic fixture manifest | Each family has baseline fixture/test reference; unavailable legacy environment explicitly recorded; no inferred pass | FR-001,017,018 |
| P01 Project edges | P00 | Modernize Archiver metadata/contracts first; remove WPF images/Metrolib at boundary; port its tests; probe SharpRemote/IsabelDb independently; add each converted project to modern solution | Modern assets contain no WPF/net45-only dependency; archive metadata round-trip; selected dependency strategy proven by smoke test | FR-010,011,017 |
| P02 Profiles | P00 | Port XML settings helpers; separate profile path; backup/atomic save; optional first-run import; later import/conflict choice; retain unknown plugin data | Copied legacy profile compares field-by-field after two restarts; malformed/read-only/interrupted/repeated import fixtures; original hash unchanged | FR-008,015 |
| P03 Desktop shell/sources | P02 | Extract one source command at a time; tree/files/folders/groups; menus/drop/startup; single-instance handoff and accessible loading/error states | Open/switch/close groups and missing paths; native picker/drop/handoff keyboard checks | FR-002,016 |
| P04 Reading/navigation | P03 | Preserve prototype streaming/cache; add encoding selector/reopen; go-to-line; legacy text selection/copy; strengthen replacement/rotation convergence | Unicode/long line/multiline/unfinished lines; middle rewrite and encoding-change fixtures; viewport/memory and close-during-read tests | FR-003,006,016 |
| P05 Investigation | P04 | Search then find-all; text/level filters then time/expression filters; highlights; persist each feature via P02 | Known-match/order fixtures; invalid regex; cancellation; combined rules and append refresh; keyboard next/previous | FR-004,016 |
| P06 Parsing/merge | P04; P05 for filter composition | Wire existing parser/format detection; custom format editor; parsed column presenters; merged sources/provenance/offsets | Expected parse/column values, equal/missing timestamps, filter+merge order, live append/rotate under merge | FR-005,006 |
| P07 Workflow panels/export | P02-P06 | Bookmarks; properties/issues/outline host panels; export options/progress; action center; help/about/changelog/explorer | Bookmark restoration/navigation; output fixtures; cancel/overwrite failure; keyboard/theme checks per panel | FR-007,009,016 |
| P08 Plugin foundation | P01,P02 | Version modern contract; safe archive validation/metadata/CLI; host shared API load context; dependency compatibility checks; local install/remove/update with rollback | Traversal/duplicate/incompatible/missing-dependency packages; archive author CLI roundtrip; reject before execute; retained config | FR-010,011,014,015 |
| P09 Extension parity | P06-P08 | One extension family per commit: source/config; source parser; entry parser; matcher/format creator; issues; outline; property/custom UI; Serilog/creator/example | One representative per public extension contract, bundled plugin suite, old-package guidance, settings reuse/conversion and disable/remove lifecycle | FR-010,011,016 |
| P10 Controlled repository | P01,P08; P09 sample package | Retarget persistence/admin CLI with export bridge; implement chosen transport boundary; Windows service adapter; desktop client discovery/publish/list/download | Loopback service + temporary DB; admin token/auth errors; records/package hashes retained; offline client and local install succeed; service start/stop | FR-011,015,017 |
| P11 Update check/download | P02,P07 | Replace original upstream URLs; stable/preview ordering; new/imported preferences; check-only timer; consented download; integrity/provenance validation | Fake-server/offline/rate-limit/channel/wrong-platform/hash/redirect fixtures; no package download during automatic check; no real updater execution in unit tests | FR-013,014,015 |
| P12 Install/update recovery | P08-P11 | Reuse Inno/profile packaging; staged install/portable swap helper only if needed; consented restart; rollback journal; preserve settings/plugins; notices/checksums | Clean per-user install, vN→vN+1 both distribution modes, locked files/disk-full/interruption/cancel/recovery, uninstall keeps profile/logs | FR-012,013,014 |
| P13 Full acceptance/cutover | P00-P12 | Run complete solution/tests and native journeys; performance workload; final inventory audit; retire required legacy runtime/installer references; update docs/release CI | All SC-001–010 evidence attached, documented existing skips, release binaries verified, no family without passing acceptance | FR-001–018 |

P02/P03 do not wait for plugin repository hosting; P10 uses loopback only. P07 host panels can precede P09 plugin-powered content. Linux/macOS assessment notes are retained throughout, but binaries/public hosting are subsequent milestones.

## Validation and release evidence

Maintain result records under ignored `artifacts/migration/` with fixture sizes/hashes, hardware, command, commit, outcome and known limitations; link those records from inventory closure notes when producing review artifacts. Never count a planning checklist, skipped fixture or unavailable service as a passing feature. Run targeted tests after each slice; run broader suites when dependencies/shared contracts change and once at final gate.

Start from existing modern commands in quickstart; phase-owned legacy test projects join the modern solution as converted. Keep service/package/install tests in temporary profiles and loopback endpoints. No production logs, real plugin tokens, live user profiles or published releases are mutated by validation.

## Complexity Tracking

No constitution violations. Two justified compatibility seams: a versioned modern plugin contract because rebuild/port was accepted, and an explicit repository data/transport converter only if probes prove existing libraries cannot run. Neither permits silently dropping capabilities. No microservices, new state framework or generic abstraction layer is proposed.

## Requirement traceability

| Requirement | Primary phase | Inventory | Acceptance |
|---|---|---|---|
| FR-001 | P00/P13 | F29 | SC-001,010 |
| FR-002 | P03 | F01-F02 | Story 1/6, SC-009 |
| FR-003 | P04 | F03-F04 | Story 1, SC-003-006 |
| FR-004 | P05 | F05-F07 | Story 2, SC-003 |
| FR-005 | P06 | F08 | Story 2, SC-003 |
| FR-006 | P04/P06 | F09-F10 | Story 1/4, SC-003 |
| FR-007 | P07/P09 | F11-F13 | Story 6, SC-009 |
| FR-008 | P02 | F14 | Story 3, SC-002 |
| FR-009 | P07 | F15 | Story 3, SC-003 |
| FR-010 | P08/P09 | F16-F20 | Story 4, SC-007 |
| FR-011 | P08-P10 | F21-F24 | Story 4, SC-007,010 |
| FR-012 | P12 | F25 | Story 5, SC-008 |
| FR-013 | P11/P12 | F26 | Story 5, SC-008 |
| FR-014 | P08/P11/P12 | F27 | Story 4/5, SC-007/008 |
| FR-015 | P02/P08/P10/P11 | F27 | Privacy/offline boundary cases |
| FR-016 | P03-P13 | F28 | Story 6, SC-005,009 |
| FR-017 | P01/P08-P10/P13 | F29 | SC-001,010 |
| FR-018 | Every phase | F29 | Commit/release evidence and Burmese progress |
