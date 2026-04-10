# .NET 10 Upgrade Plan - PumaLanguageCompiler

## Table of Contents
- [Executive Summary](#executive-summary)
- [Migration Strategy](#migration-strategy)
- [Implementation Timeline](#implementation-timeline)
- [Detailed Dependency Analysis](#detailed-dependency-analysis)
- [Project-by-Project Migration Plans](#project-by-project-migration-plans)
- [Package Update Reference](#package-update-reference)
- [Breaking Changes Catalog](#breaking-changes-catalog)
- [Testing & Validation Strategy](#testing--validation-strategy)
- [Risk Management](#risk-management)
- [Complexity & Effort Assessment](#complexity--effort-assessment)
- [Source Control Strategy](#source-control-strategy)
- [Success Criteria](#success-criteria)

## Executive Summary
### Scenario
Upgrade `Puma.csproj` from `net8.0` to `net10.0` using a single coordinated modernization pass.

### Discovered Metrics
- Total projects: `1`
- Total issues: `1` (mandatory: `1`, optional/potential: `0`)
- Dependency graph depth: `0` (single-node graph)
- Total NuGet packages: `4`
- Package upgrades required by assessment: `0`
- Security vulnerability issues reported: `0`
- Project LOC: `7505`

### Complexity Classification
**Simple**

Justification:
- `<= 5` projects (actual: `1`)
- No project-to-project dependencies
- No package incompatibilities
- No API incompatibility incidents reported
- Single mandatory issue is framework target update (`Project.0002`)

### Selected Strategy
**All-At-Once Strategy** — all required changes are applied in one atomic operation with no intermediate migration states.

### Critical Issues
1. `Project.0002` - Project target framework must be upgraded to selected target (`net10.0`).

### Iteration Strategy Used
- Phase 1: Skeleton + classification
- Phase 2: Foundation sections
- Phase 3: Detailed project/package/testing/source-control/success criteria fill

## Migration Strategy
### Approach Selection
**All-At-Once Migration (single-project atomic upgrade)**

Rationale:
- The solution contains one SDK-style .NET project (`Puma.csproj`).
- There are no inter-project ordering constraints.
- Assessment reports all listed packages as compatible for target upgrade context.
- Risk profile is low-to-medium and manageable in a single bounded change set.

### Strategy-Specific Considerations
- Treat framework update and validation as one coordinated operation.
- Do not split migration by project (not applicable with one project).
- Keep build-fix-rebuild verification bounded within the same upgrade operation.
- Keep testing as a separate validation phase after upgrade changes are complete.

### Ordering Principles
1. Prerequisite validation (SDK/global configuration where applicable).
2. Atomic framework update in project definition.
3. Restore/build validation and compatibility review.
4. Test execution and any test-level adjustments.

### Parallel vs Sequential Decision
- **Sequential** at workflow level (prepare -> upgrade -> validate), because there is one project.
- No project parallelization opportunities exist.

## Implementation Timeline
### Phase 0: Preparation
- Confirm branch is `upgrade-to-NET10`.
- Confirm target SDK availability for `.NET 10.0`.
- Confirm no unreviewed local changes before execution phase.

### Phase 1: Atomic Upgrade
**Operations (single coordinated batch):**
- Update `TargetFramework` in `Puma.csproj` from `net8.0` to `net10.0`.
- Re-evaluate package restore under `.NET 10` (no package version changes expected from assessment).
- Resolve any compile-time regressions caused by framework behavior or SDK changes.
- Rebuild to verify zero errors.

**Deliverable:** solution builds successfully with `0` compile errors.

### Phase 2: Test Validation
**Operations:**
- Execute tests in `Puma.csproj`.
- Address any test failures introduced by framework/runtime behavior changes.

**Deliverable:** all tests pass on upgraded target framework.

### Phase 3: Final Verification
- Confirm no unresolved vulnerabilities in dependency graph.
- Confirm upgrade branch is ready for review/merge.

## Detailed Dependency Analysis
### Dependency Graph Summary
- Single project node: `Puma.csproj`
- Dependencies: none
- Dependants: none
- Critical path length: 1

### Migration Grouping
Given all-at-once strategy and single project topology:
- **Atomic Upgrade Group A**: `Puma.csproj`

### Project Relationship Notes
- No circular dependencies.
- No cross-project blocking concerns.
- No phase-to-phase project handoff risk.

### Ordering Implication
Because the graph has one isolated node, dependency ordering constraints are trivially satisfied by upgrading `Puma.csproj` directly in the atomic phase.

## Project-by-Project Migration Plans
### Project: `Puma.csproj`

**Current State**
- Target framework: `net8.0`
- Project kind: `DotNetCoreApp` (SDK-style)
- Dependencies: `0`
- Dependants: `0`
- Approximate size: `7505` LOC
- Package set: 4 test-related packages, all compatible per assessment
- Risk level: **Low-Medium**

**Target State**
- Target framework: `net10.0`
- Package baseline remains compatible unless restore/build reveals transitive issues

**Migration Steps**
1. **Prerequisites**
   - Confirm `.NET 10 SDK` is installed and selected by environment/global SDK resolution.
   - Confirm upgrade branch is active: `upgrade-to-NET10`.
2. **Project File Update**
   - Edit `Puma.csproj`: set `<TargetFramework>net10.0</TargetFramework>`.
   - Review conditional properties/targets (if any) for framework-specific logic.
3. **Package Validation**
   - Keep existing package versions initially (assessment marks all compatible).
   - Restore dependencies and check for transitive warnings/errors.
4. **Expected Breaking Changes Review**
   - Inspect compiler warnings/errors for obsolete APIs and stricter analyzers.
   - Validate runtime behavior assumptions in parser/compiler tests.
5. **Code Modifications (if required by build/test results)**
   - Replace obsolete API usages.
   - Adjust test assertions if behavior changed by runtime/compiler updates.
   - Update configuration/build settings only when required for `.NET 10` compatibility.
6. **Validation Checklist for Project**
   - Build succeeds with zero compile errors.
   - Tests in project pass.
   - No dependency conflicts.
   - No unresolved security vulnerabilities.

## Package Update Reference
Assessment indicates no direct package version upgrades are required for target compatibility.

### Common Package Status
| Package | Current Version | Target Version | Projects Affected | Reason |
|---|---:|---:|---|---|
| `Microsoft.NET.Test.Sdk` | 17.6.0 | 17.6.0 | `Puma.csproj` | Compatible per assessment |
| `MSTest` | 3.1.1 | 3.1.1 | `Puma.csproj` | Compatible per assessment |
| `MSTest.TestAdapter` | 3.1.1 | 3.1.1 | `Puma.csproj` | Compatible per assessment |
| `MSTest.TestFramework` | 3.1.1 | 3.1.1 | `Puma.csproj` | Compatible per assessment |

### Package Governance Notes
- If restore/build uncovers transitive compatibility gaps, update only affected packages with explicit version pinning and document reason.
- Keep test stack aligned (`MSTest` + adapter/framework) if any future update becomes necessary.

## Breaking Changes Catalog
No concrete API break incidents were identified in assessment, but the upgrade plan must account for potential framework/runtime-level changes discovered at build/test time.

### Expected Categories to Validate
1. **Compiler/Analyzer strictness changes**
   - New warnings or diagnostics may surface previously tolerated patterns.
2. **Runtime behavior differences**
   - Subtle behavior changes in BCL methods, formatting, globalization, or regex behavior may impact tests.
3. **Build SDK changes**
   - Updated defaults in SDK targets may affect build output, warnings, or test execution behavior.

### Detection and Handling Plan
- Use build output as first-pass detector.
- Use test failures as behavioral detector.
- Apply minimal, targeted code/config updates and re-validate.

## Testing & Validation Strategy
### Level 1: Project Build Validation
- Restore dependencies for upgraded target.
- Build `Puma.csproj` and require zero compile errors.

### Level 2: Project Test Validation
- Execute all tests associated with `Puma.csproj`.
- Resolve failures attributable to framework/runtime changes.

### Level 3: Upgrade Acceptance Validation
- Confirm no unresolved package conflicts.
- Confirm no unresolved vulnerability findings.
- Confirm output artifacts are generated as expected for the project.

### Validation Checklist
- [ ] `TargetFramework` updated to `net10.0`
- [ ] Dependency restore succeeds
- [ ] Build succeeds with no errors
- [ ] Tests pass
- [ ] No unresolved vulnerabilities

## Risk Management
### High-Level Risk Assessment
| Area | Risk Level | Reason |
|---|---|---|
| Framework target update (`net8.0` -> `net10.0`) | Medium | New runtime/compiler behavior may expose latent assumptions |
| Package compatibility | Low | All listed packages are marked compatible |
| Dependency ordering | Low | Single project, no dependency graph complexity |
| API migration | Low | Assessment reports no API incidents |

### High-Risk Changes and Mitigation
| Item | Risk | Mitigation |
|---|---|---|
| Framework retarget in `Puma.csproj` | Medium | Keep change atomic; validate immediately with restore/build/tests |
| Runtime behavior regression in tests | Medium | Triage failing tests, patch behavior-dependent assertions/code paths, re-run full tests |

### Security Vulnerability Status
Assessment reported **no package vulnerability issues**.

### Contingency Plan
- If blocking incompatibility appears, keep changes isolated on `upgrade-to-NET10`, apply focused compatibility fixes, and re-validate.
- If unresolved blocker remains, pause merge and document exact blocker + fallback action.

## Complexity & Effort Assessment
| Project | LOC | Dependencies | Package Count | Risk | Complexity |
|---|---:|---:|---:|---|---|
| `Puma.csproj` | 7505 | 0 | 4 | Low-Medium | Medium |

### Phase Complexity
| Phase | Complexity | Notes |
|---|---|---|
| Preparation | Low | Environment/branch readiness checks |
| Atomic Upgrade | Medium | Core framework change and compile verification |
| Test Validation | Medium | Runtime/test behavior confirmation on `.NET 10` |

### Resource/Skill Considerations
- Requires .NET SDK/project configuration familiarity.
- Requires ability to diagnose compiler/test regressions.

## Source Control Strategy
### Branching
- Source branch: `main`
- Upgrade branch: `upgrade-to-NET10`

### Commit Strategy (All-At-Once aligned)
- Prefer a **single upgrade commit** containing:
  - framework retarget change(s)
  - any required compatibility fixes
  - test adjustments required by runtime changes
- Use one follow-up commit only if separation is needed for review clarity (e.g., upgrade vs test-fix split).

### Review and Merge Requirements
- PR from `upgrade-to-NET10` into `main`.
- Required evidence in PR:
  - successful build result
  - successful test result
  - confirmation of no unresolved vulnerabilities

## Success Criteria
### Technical Criteria
- `Puma.csproj` targets `net10.0`.
- All assessment-required updates are applied (framework retarget; no additional package updates required unless discovered during validation).
- Dependency restore and build complete successfully.
- All project tests pass.
- No unresolved security vulnerabilities remain.

### Quality Criteria
- No unresolved build errors.
- Compatibility fixes are minimal and documented.
- Project behavior remains functionally consistent based on automated test outcomes.

### Process Criteria
- All-at-once atomic upgrade principle followed.
- Upgrade executed on `upgrade-to-NET10` branch.
- Source control strategy and validation evidence captured in review.
