# Feature Specification: Full Tailviewer Migration

**Feature Branch**: `master` (existing branch; specification path is independent)

**Created**: 2026-11-18

**Status**: Ready for planning

**Input**: Migrate the full Tailviewer project to .NET 10 + Avalonia, including all missing legacy features, settings, plugins, and updates. Keep changes small and explain in Burmese.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read and navigate existing logs (Priority: P1)

As a log reader, I can open files and folders, navigate large logs, and follow changing files without losing access to existing reading tools.

**Why this priority**: Reading and navigation are the foundation of every other workflow.

**Independent Test**: Open the shared text, Unicode, multiline, long-line and rotating-file fixtures; navigate, select, copy and follow their content.

**Acceptance Scenarios**:

1. **Given** several files or a folder, **When** I open them through the picker, command line or drag-and-drop, **Then** each source appears in the source tree with loading, empty and error states.
2. **Given** an open log, **When** I use scrolling, go-to-line, quick navigation, selection and copy, **Then** I reach the requested entry and copy the intended original text using the legacy-supported selection modes.
3. **Given** a followed or paused source, **When** it grows, rotates, is replaced or temporarily disappears, **Then** content recovers without stale rows and the chosen follow state is respected.

### User Story 2 - Search, filter and correlate sources (Priority: P1)

As an investigator, I can find events, narrow results, highlight patterns and combine sources while retaining the original source and position.

**Why this priority**: These tools distinguish the complete application from the current viewer prototype.

**Independent Test**: Run recorded search/filter/merge examples with known expected entries and ordering.

**Acceptance Scenarios**:

1. **Given** a known fixture, **When** I search forward/backward or find all with supported text/pattern options, **Then** matches, counts and navigation agree with the expected results, including after append.
2. **Given** level, text, time and expression filters, **When** I combine inclusion/exclusion and highlighting rules, **Then** visible entries and highlights follow the legacy semantics.
3. **Given** multiple timestamped sources with ties and missing timestamps, **When** I merge, include/exclude or time-adjust sources, **Then** ordering follows a documented deterministic baseline and entries retain source identity.

### User Story 3 - Resume work and export results (Priority: P1)

As a returning user, I retain my sources, preferences, bookmarks and custom formats, and can export the intended result set.

**Why this priority**: Migration must preserve existing work and avoid silent data loss.

**Independent Test**: Import a copied legacy profile, restart twice and compare all supported settings and exported text with expected fixtures.

**Acceptance Scenarios**:

1. **Given** an existing legacy profile, **When** I import it, **Then** a backup is retained and sources/groups, bookmarks, filters, highlighting, custom formats, encodings, column/view settings, window layout, export preferences and update preferences are restored where supported.
2. **Given** malformed settings or unavailable sources/plugins, **When** I start the app, **Then** usable settings load, unresolved values are preserved and actionable warnings identify affected items.
3. **Given** selected or filtered entries, **When** I export using the available legacy export choices, **Then** output matches the documented content/order/encoding, reports progress and handles cancellation or overwrite decisions without damaging existing files.

### User Story 4 - Use formats, plugins and supporting tools (Priority: P2)

As a user or plugin author, I can use custom sources, parsers, formats and analysis extensions and build/distribute supported plugins.

**Why this priority**: Plugin functionality and the bundled tooling are part of full-project parity.

**Independent Test**: Build, package, install, run, update, disable and remove representative extensions; exercise unavailable and incompatible repository cases.

**Acceptance Scenarios**:

1. **Given** supported built-in/custom formats and the bundled Serilog format, **When** I open matching logs, **Then** timestamps, levels, multiline entries, properties and columns are parsed and displayed as expected.
2. **Given** compatible plugins, **When** I install and enable them, **Then** their source, parser, format, property, issue, outline and custom-view capabilities are accessible; removal explains effects on active sources.
3. **Given** an incompatible old plugin, missing dependency or failed plugin operation, **When** it is encountered, **Then** compatibility and recovery guidance are shown, persisted configuration is retained and other sources remain usable where recovery is possible.
4. **Given** the plugin-author samples, **When** I follow the updated instructions, **Then** I can create and inspect a valid plugin archive and publish/list/download it using the migrated repository tools and service.

### User Story 5 - Install and update safely (Priority: P2)

As a desktop user, I can choose portable or installed distribution, check for updates and move to a selected release without losing my work.

**Why this priority**: Distribution and the existing updater must be migrated alongside the application.

**Independent Test**: Install, upgrade, interrupt an update, retry and uninstall in a clean test profile; repeat the portable update path.

**Acceptance Scenarios**:

1. **Given** an installed or portable copy, **When** I check manually or enable scheduled checks, **Then** current/newer-version, selected stable/preview channel, offline and failed-check states are clear.
2. **Given** an eligible update, **When** I accept it, **Then** version, platform and package integrity are validated before replacement; cancellation, insufficient space, locked files or interruption preserve a usable existing version and settings.
3. **Given** installation or upgrade completion, **When** I reopen the app, **Then** settings and plugins are preserved; uninstall removes application files without deleting user logs or profiles by default.

### User Story 6 - Complete desktop workflow and project parity (Priority: P2)

As a user or maintainer, I can use the remaining desktop panels and verify each shipped legacy capability rather than mistaking a prototype release for completion.

**Why this priority**: Less visible workflows and companion projects must not be silently omitted.

**Independent Test**: Execute the feature inventory across the source tree, including keyboard access, light/dark themes and supporting tools.

**Acceptance Scenarios**:

1. **Given** a source with metadata, issues or outline information, **When** I use its panels, bookmarks, properties, action center and notifications, **Then** navigation and relevant operations work with keyboard access and readable theme contrast.
2. **Given** an application command, **When** I invoke the menu, documented shortcut or supported startup/single-instance workflow, **Then** the intended action runs and error/help/about/version information is available.
3. **Given** a legacy capability in a shipping project, **When** migration is reviewed, **Then** it has a requirement, acceptance case and evidence; a missing capability prevents a full-migration completion claim.

### Edge Cases

- Empty/missing/locked/inaccessible files; network path interruption; rename rotation; same-size and middle-only rewrites; truncate-and-regrow; encoding changes after replacement; unfinished final lines.
- Invalid or mixed encodings, Unicode, very long lines, multiline entries, missing timestamps and equal timestamps across sources.
- Invalid patterns, no matches, large find-all/copy/export results and cancellation while indexing, filtering, merging or closing.
- Corrupt profiles, duplicate source identifiers, missing custom formats/plugins, repeated imports and failed settings writes; originals and unknown fields must survive.
- Invalid/traversal plugin archive paths, duplicate plugin identity, incompatible dependencies, unavailable repository and corrupt downloads; invalid packages must not write outside their destination.
- Offline/rate-limited update checks, wrong architecture/channel, tampered packages, interrupted downloads, disk exhaustion and install locations without write permission.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Maintain a feature parity inventory for every shipping legacy application/library/tool/service/plugin and its acceptance/system tests. Record baseline behavior, current status, acceptance evidence and corresponding requirement/story. Experimental playground work is inventoried separately and is not shipped implicitly.
- **FR-002**: Support opening/closing files, folders and plugin-provided sources, source grouping, switching, inclusion/exclusion, supported startup arguments, drag-and-drop and existing-instance handoff; verify using Story 1 and Story 6.
- **FR-003**: Preserve navigation, original-text selection/copy, follow/pause, source position and recovery behavior, including the edge cases above; verify using Story 1.
- **FR-004**: Restore search, find-all, quick filters, expression filters, levels/time ranges and highlighting with legacy option semantics; verify using Story 2 fixtures.
- **FR-005**: Restore merged-source ordering, provenance, grouping and supported timestamp adjustments; verify ties and missing timestamps using Story 2.
- **FR-006**: Restore format detection, custom formats, timestamp/level parsing, encodings, multiline handling and configurable parsed columns; verify using Stories 1 and 4.
- **FR-007**: Restore bookmarks and navigation, properties, outline/issues, quick navigation, action center, notifications, settings, help/about/changelog and all other shipping panels found by FR-001; verify using Stories 3 and 6.
- **FR-008**: Import legacy settings after preserving an original backup; save changes safely, preserve unknown/unavailable-extension settings, support repeatable imports and recover from malformed or unwritable profiles without destroying the last valid profile; verify Story 3.
- **FR-009**: Restore export choices, source/filter scope, ordering, progress, cancellation and overwrite handling; verify Story 3 with expected-output files.
- **FR-010**: Restore plugin discovery, metadata, install/update/disable/remove and every legacy extension category; label unsupported binaries with rebuild/port guidance rather than promising old binary compatibility; verify Story 4.
- **FR-011**: Migrate the plugin packaging/inspection tools, bundled plugins and creator samples, repository client/library/service and distribution metadata; retain existing repository behaviors and persisted records or provide a verified conversion; verify Story 4 end-to-end.
- **FR-012**: Offer portable EXE, portable ZIP and installer for Windows x64, correct icons/licenses/version metadata, per-user installation, upgrade and uninstall without default user-data deletion; verify Story 5.
- **FR-013**: Restore manual/automatic update checks, remembered update preferences, release-channel selection, progress/cancellation, consent before installation and recoverable failure; direct updates to this repository's releases rather than the original upstream; verify Story 5.
- **FR-014**: Validate update provenance and integrity before execution; preserve a working version on failed application and do not bypass a failed validation; verify modified, incomplete and wrong-platform packages in Story 5.
- **FR-015**: Keep local log reading usable offline. Logs/profile contents must not be uploaded by migration, update checks or diagnostics without explicit user action; verify with network observation during Stories 1, 3 and 5.
- **FR-016**: Preserve keyboard workflows, meaningful accessible names, readable light/dark states, loading/error/progress feedback and a responsive interface during background work; verify Story 6 and timed outcomes below.
- **FR-017**: Bring all shipping projects and relevant automated/acceptance/system tests into the supported migrated build; retain attribution and required licenses and document plugin-author/user migration steps. No shipping project may remain silently dependent on the retired application.
- **FR-018**: Deliver in small independently reviewable changes with Conventional Commits and Burmese progress explanations. Keep existing preview releases clearly marked as incomplete until the parity inventory and release acceptance outcomes pass.

### Key Entities

- **Log source and entry**: Source identity/location/type, availability, encoding, source position, text and parsed fields; merged entries preserve provenance.
- **Workspace/profile**: Open sources/groups, view/layout preferences, bookmarks, filters/highlights, custom formats, plugin configuration, export and update preferences; includes migration provenance and retained unknown settings.
- **Plugin and package**: Identity/version, author/license, supported host/platform, dependencies, capabilities, installation state and repository metadata.
- **Release/update**: Version, channel, supported platform, package provenance/integrity, progress and recoverable installation state.
- **Parity inventory entry**: Legacy capability/project, baseline fixture, requirement, migration status and acceptance evidence.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of shipping legacy capabilities/projects have inventory entries and passing mapped acceptance evidence before full migration is declared; no unexplained omissions or regressions remain.
- **SC-002**: All representative legacy profiles import and survive two restarts without loss of supported settings; original files remain unchanged, and corrupt/unavailable-extension cases retain recoverable data.
- **SC-003**: Search/filter/merge/format/export fixtures produce 100% of expected entries, ordering, parsed values and original text; all file-recovery scenarios show the final current contents without stale entries.
- **SC-004**: On a recorded Windows x64 reference machine (at least 4 CPU cores, 16 GB RAM and local SSD), users see the first page of a 100 MB log within 3 seconds and a 1 GB log within 10 seconds in three warm-cache runs; these are acceptance targets, not current results.
- **SC-005**: On that machine, 95% of scroll/navigation/pause interactions respond within 200 ms during indexing; complete indexing of the 1 GB plain-text fixture peaks below 1 GB application memory, measured separately from explicitly requested large copy/export results.
- **SC-006**: During a five-minute 1,000-lines/second append workload, new output becomes visible within 1 second for 95% of samples and no entries are lost; rotation/replacement fixtures converge within 3 seconds after the final write.
- **SC-007**: Each supported plugin category has a passing representative integration case, all bundled plugins pass, incompatible packages show guidance, and invalid archives write zero files outside their installation destination.
- **SC-008**: Portable/install/upgrade/uninstall and interrupted/tampered/offline update cases pass with zero user-log/profile loss and no execution of failed-integrity packages.
- **SC-009**: All mapped reading, investigation, settings, plugin and update journeys can be completed with keyboard access in both light and dark themes; every failed operation presents an actionable outcome.
- **SC-010**: Every shipping project builds under the supported migration toolchain and relevant automated tests pass; existing skips are individually documented and cannot hide a missing acceptance capability.

## Assumptions

- The user-selected implementation constraint is .NET 10 with Avalonia. This specification defines user outcomes; implementation choices and dependency decisions belong in the plan.
- The baseline is the shipping legacy source at commit `6bc9c575`, compared with the current Avalonia preview. Source/tests define parity where a runnable legacy baseline is unavailable; that validation limitation must be recorded.
- Full project includes API/Core, desktop, Archiver, PluginRepository library/service, bundled plugins/creator, installer/updater and relevant tests. Playground prototypes are reviewed for shipping dependencies but not promoted into new product features.
- Windows x64 is the first complete release acceptance platform, matching existing distributions. Linux/macOS portability must be assessed and platform limitations recorded; validated packages for those platforms are a subsequent milestone, not a claim of this release.
- Legacy plugin authors may need to rebuild or port plugins and UI integrations. Behavioral capability parity is required; unchanged legacy binary compatibility is not assumed.
- The existing public repository is the release source. A runnable legacy plugin repository or a controlled equivalent is needed for service compatibility checks; unavailable services must be reported, not counted as passing.
- No existing log or profile may be deleted to simplify migration. Source files remain read-only unless a user explicitly chooses an existing supported write/export action.
- The generated constitution is an unratified template and supplies no additional project policy. Existing session instructions and repository rules apply; no new approval requirements are inferred from its examples.
