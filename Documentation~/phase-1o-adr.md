# ADR: Phase 1O Auto Defense Boundary

## Decision

Auto Defense is the first genre package because it is the smallest useful composition layer that turns the previously generic foundations into a playable loop: spawn enemies around a central objective, move them inward, auto-fire mounted weapons, resolve damage, and fail or complete a run.

## Boundary Points

1. Auto Defense owns central-objective genre state and orchestration.
2. It depends on lower generic packages only where it directly composes them.
3. The central objective is represented by authored definition plus mutable health/life state.
4. Perimeter channels are stable ids mapped to deterministic ring poses.
5. Weapon modules are fixed mount assignments, not inventory or progression.
6. Auto-fire delegates cadence and direct/projectile intent emission to Weapon Systems.
7. Target candidates are built from active enemy state and then handed to Attacks through Weapon Systems.
8. Enemy contact with the objective is reported as leak/objective damage.
9. Encounter metrics are updated for spawned, killed, leaked, and objective damage events.
10. Progression, saves, UI, offline rewards, research, and upgrade drafting stay outside.
11. Classic Tower Defense remains separate because lanes, grids, placement, build/sell, and path-exit leaks are different genre commitments.
12. ECS/Entities remains a future integration boundary, not a 0.1.0 runtime dependency.
13. Future templates should consume this package; templates should not be embedded in the package.
