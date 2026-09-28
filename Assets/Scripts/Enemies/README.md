# Enemy Framework

A composition-based enemy system for DungeonSong, built to make enemy #100 cheaper to
create than enemy #10.

The rule the whole design serves: **code defines what an enemy *can* do, data and
component choice define what *this* enemy does.** Adding an enemy should be a prefab and
some assets. Adding a *kind* of behaviour should be one new class that nothing else has to
know about.

> **Making an enemy?** Follow [CREATING_AN_ENEMY.md](CREATING_AN_ENEMY.md) — a step-by-step
> recipe with archetype presets, a troubleshooting table, and how to run the tests. This
> document explains how the framework works; that one explains how to use it.

---

## 1. The shape of it

Three assemblies, one direction of dependency:

```
Assembly-CSharp            DungeonSong.Combat            DungeonSong.Enemies
(existing game code)       (damage model, shared)        (the enemy framework)
      │                            ▲                              │
      │ PlayerHealth               │                              │
      └── implements IDamageable ──┘                              │
                                   └──────── references ──────────┘
```

`DungeonSong.Combat` knows nothing about enemies. `DungeonSong.Enemies` knows nothing about
the player. Neither can reference `Assembly-CSharp`, which is deliberate: it makes it
impossible for framework code to quietly couple itself to `PlayerController`.

### Runtime structure of one enemy

```
                        ┌──────────────────────────────┐
                        │            Enemy             │  lifecycle, team, facing,
                        │  (the hub, one per prefab)   │  events, module tick loop
                        └──────────────┬───────────────┘
                                       │ ticks, in order
        ┌────────────┬─────────────┬───┴────────┬──────────────┬─────────────┐
        ▼            ▼             ▼            ▼              ▼             ▼
  EnemyActivation  EnemyHealth  EnemyPerception  EnemyStateMachine  AttackController  Movement
   dormant/active   hp, poise,   targets, LOS,    ┌──────────────┐   selects + runs    ground /
   by distance      i-frames     memory           │ states as    │   attacks           flying /
        │              │            │             │ components   │        │            surface
        │              ▼            │             └──────┬───────┘        │              │
        │      DefenseController    │                    │                │              │
        │      ┌──────┴───────┐     │                    │                │              │
        │      │ Armor Shield │     │                    │                │              │
        │      │ Spike Invuln │     │                    │                │              │
        │      └──────────────┘     │                    │                │              │
        └──────────────┬────────────┴────────────────────┴────────────────┴──────────────┘
                       │
                       ▼
                 IAnimatorAdapter  ← semantic keys only, never Animator parameter names
```

### How damage flows

```
attacker's Hitbox ──trigger──► Hurtbox ──► IDamageable.TakeDamage(in DamageInfo)
                               (zone         │
                                multiplier)  ▼
                                        DefenseController.Evaluate
                                        (Armor → Shield → Spike → Invulnerability)
                                             │  returns DefenseEvaluation
                                             ▼
                                        EnemyHealth applies it
                                             ├─► health, poise, i-frames
                                             ├─► IKnockbackReceiver (movement)
                                             ├─► reflect damage back to attacker
                                             └─► events ──► Enemy re-raises ──► HurtState /
                                                                                StaggerState /
                                                                                DeadState
```

The same path serves melee, projectiles, contact damage and environment damage. Nothing
in it knows what kind of thing is being hurt.

---

## 2. Files

### `Assets/Scripts/Combat/` — `DungeonSong.Combat`

| File | Responsibility |
| --- | --- |
| `CombatTypes.cs` | `DamageTeam`, `DamageType`, `DamageFlags`, `HitReaction` |
| `DamageInfo.cs` | One hit, as a struct passed by `in`. Zero allocation per hit |
| `DamageResult.cs` | What the receiver decided: applied, blocked, immune, staggered, killed |
| `IDamageable.cs` | The only thing a hitbox talks to |
| `IKnockbackReceiver.cs` | Lets health push an actor without knowing how it moves |
| `ITargetable.cs` | Something AI can aim at. Not "the player" |
| `TargetRegistry.cs` | Static list of live targets. Replaces `Find` calls and overlap queries |
| `Targetable.cs` | Registers a GameObject as a target. Add to the player |
| `Hurtbox.cs` | A damageable zone with its own multiplier (weak points, armoured plates) |
| `Hitbox.cs` | A damage-dealing region, off until an attack opens it |
| `ContactDamager.cs` | "Touching it hurts", with a per-target cooldown |
| `AnimatorHitboxDriver.cs` | Opens a hitbox during a slice of an Animator state (player bridge) |
| `MirrorWithSprite.cs` | Mirrors a child's local X to follow a `SpriteRenderer`'s flip |
| `Pooling/IPoolable.cs` | Reset hooks for pooled prefabs |
| `Pooling/PrefabPool.cs` | Minimal prefab pool, cleared on scene unload |
| `Projectiles/ProjectileDefinition.cs` | Projectile data: motion, speed, damage, pierce, volley |
| `Projectiles/Projectile.cs` | Pooled projectile. Override `UpdateMotion` for exotic flight |
| `Projectiles/ProjectileSpawner.cs` | The one place projectiles are fired from |

### `Assets/Scripts/Enemies/` — `DungeonSong.Enemies`

| File | Responsibility |
| --- | --- |
| `Core/Enemy.cs` | The hub: lifecycle, team, facing, target, events, module tick loop |
| `Core/EnemyModule.cs` | Base for every capability. Also `ModuleTickOrder` |
| `Core/IEnemy.cs` | Narrow surface for outside systems |
| `Core/EnemyLifecycleState.cs` | Uninitialized / Dormant / Active / Dead / Despawned |
| `Config/EnemyStats.cs` | Health, poise, i-frames, knockback resistance, despawn delay |
| `Config/EnemyDefinition.cs` | Prefab + shared stats/movement/perception. What spawners reference |
| `Health/EnemyHealth.cs` | The single damage entry point. Per-instance runtime state |
| `Defense/DefenseTypes.cs` | `DefenseEvaluation`, `IDamageModifier` |
| `Defense/DefenseController.cs` | Runs defenses as a pipeline |
| `Defense/DefenseBehaviour.cs` | Base for toggleable defenses |
| `Defense/ArmorDefense.cs` | Passive reduction, optional stagger immunity for weak hits |
| `Defense/ShieldDefense.cs` | Directional block. Get behind it |
| `Defense/SpikeDefense.cs` | Shell: resistant, and hurts the attacker |
| `Defense/InvulnerabilityDefense.cs` | Hard immunity window |
| `Movement/IMovementController.cs` | Movement contract: intent in, motion out |
| `Movement/MovementControllerBase.cs` | Body, cached probes, facing, knockback lockout |
| `Movement/MovementSettings.cs` | Abstract base tuning: speed, acceleration, turn delay |
| `Movement/GroundMovementSettings.cs` | Gravity, jump, dash tuning (own file — see note in §3) |
| `Movement/FlyingMovementSettings.cs` | Steering, bob, altitude tuning (own file) |
| `Movement/SurfaceMovementSettings.cs` | Adhesion and corner tuning (own file) |
| `Movement/GroundMovement.cs` | Walks, jumps, dashes |
| `Movement/FlyingMovement.cs` | Gravity-free steering with inertia, bob, tether, avoidance |
| `Movement/SurfaceMovement.cs` | Crawls floor → wall → ceiling around corners |
| `Perception/IPerception.cs` | What the enemy knows about its quarry |
| `Perception/PerceptionSettings.cs` | Radii, view cone, LOS, memory, tick interval |
| `Perception/EnemyPerception.cs` | Throttled detection, aggro, memory, hearing |
| `Perception/NoiseEvents.cs` | Global "something made a noise here" channel |
| `Brain/IEnemyState.cs` | One behaviour, with its own entry and interruption rules |
| `Brain/EnemyStateBehaviour.cs` | Base state, plus convenience accessors |
| `Brain/EnemyStateMachine.cs` | Priority arbitration. No transition table, no switch |
| `Brain/States/*.cs` | Idle, Patrol, Alert, Chase, MaintainDistance, Search, Attack, Defend, Flee, Hurt, Stagger, Dead |
| `Attacks/AttackDefinition.cs` | One attack, as data |
| `Attacks/IAttack.cs` | Attack contract + `AttackPhase` |
| `Attacks/AttackBehaviour.cs` | Startup/active/recovery machine, cooldown, conditions, motion |
| `Attacks/MeleeAttack.cs` | Opens a hitbox for the active frames |
| `Attacks/ProjectileAttack.cs` | Fires a projectile, with optional target leading |
| `Attacks/AttackController.cs` | Weighted selection, global pacing, combos, animation relay |
| `Animation/IAnimatorAdapter.cs` | Semantic animation calls + `NullAnimatorAdapter` |
| `Animation/AnimatorAdapter.cs` | Maps semantic keys to Animator parameters, hashed once |
| `Animation/AnimationEventRelay.cs` | Clip events → attack phases |
| `Spawning/EnemyFactory.cs` | Creates enemies from definitions or prefabs, via the pool |
| `Spawning/EnemySpawner.cs` | Placement, triggers, respawn policy, alive limits |
| `Spawning/EnemyActivation.cs` | Dormant/active by distance. The main scalability lever |
| `Debugging/EnemyDebugOverlay.cs` | State, target, health, ranges and sensors in the scene view |

### Elsewhere

| Path | Note |
| --- | --- |
| `Assets/Enemies/Config/*.asset` | 23 configuration assets for the five proof-of-concept enemies |
| `Assets/Enemies/Prefabs/POC_*.prefab` | The five enemies plus a projectile |
| `Assets/Tests/EditMode/*` | 66 edit-mode tests |
| `Assets/Scripts/PlayerHealth.cs` | **Modified:** now implements `IDamageable` (additive; `Damage(float)` untouched) |

---

## 3. How to do things

### Create a new enemy (no code)

1. Duplicate the nearest `POC_*` prefab, or start from an empty GameObject with
   `SpriteRenderer`, `Rigidbody2D`, `Collider2D`.
2. Add `Enemy`, `EnemyHealth`, and a movement component (`GroundMovement`,
   `FlyingMovement`, `SurfaceMovement`).
3. Add `EnemyStateMachine` and the state components this enemy should have. Patrols get
   `PatrolState`; hunters add `EnemyPerception`, `AlertState`, `ChaseState`, `SearchState`.
   Always add `HurtState`, `StaggerState`, `DeadState` unless you mean not to react.
4. Add a `Hurtbox` child with a trigger collider, pointed at the `EnemyHealth`.
5. For attacks: add `AttackController`, `AttackState`, and one attack component per attack,
   plus a `Hitbox` child for melee ones.
6. Create an `EnemyDefinition` (Create ▸ Dungeon ▸ Enemies) and give it stats, movement and
   perception assets. Assign it to the `Enemy` component and point it back at the prefab.

Nothing above requires touching `Enemy.cs`. That is the test the design has to keep passing.

### Create a new attack

Usually no code: make an `AttackDefinition` asset and add a `MeleeAttack` or
`ProjectileAttack` component pointed at it, plus a `Hitbox` whose key matches. Timings,
damage, range, knockback, motion, combo follow-up and animation key are all on the asset.

Write a class only for genuinely new *mechanics* — a beam that persists, an attack that
spawns terrain. Subclass `AttackBehaviour` and implement `OnActiveBegin` / `OnActiveEnd`;
phases, cooldown and conditions come free.

### Create a new movement behaviour

Subclass `MovementControllerBase`, implement `ConfigureBody` and `ApplyMotion`, and
override `UpdateProbes` if your probes differ (as `SurfaceMovement` does). Every state that
says "go that way" works with it immediately, because they all speak `IMovementController`.

> **Rule for any new ScriptableObject: one class, one file, named after the class.** Unity
> creates a `MonoScript` only for the type whose name matches the file name. A settings
> subclass sharing a file with another type serializes with a null script reference and
> loads as `null`, with no error — the enemy simply never moves. This bit the three movement
> settings classes once; they now live in `GroundMovementSettings.cs`,
> `FlyingMovementSettings.cs` and `SurfaceMovementSettings.cs` for exactly this reason.

### Create a new AI behaviour

Subclass `EnemyStateBehaviour`, set `DefaultPriority`, override `WantsControl` with the
condition under which it applies, and do the work in `OnStateTick`. Add the component to a
prefab. The machine picks it up; no registration, no enum entry, no switch case.

- Event-driven states (a reaction to something) set a pending flag in a handler and call
  `RequestSelf()`. See `HurtState`.
- Committed states return `false` from `IsInterruptible`. See `StaggerState`.
- States needing timers while *not* running use `OnBackgroundTick`. See `DefendState`.

### Damage and defense

Attackers build a `DamageInfo` and open a `Hitbox`. Receivers own the decision: `Hurtbox`
scales by zone, `DefenseController` runs every `IDamageModifier`, `EnemyHealth` applies the
verdict. To add a defense, implement `IDamageModifier` (usually by subclassing
`DefenseBehaviour`) and narrow the `DefenseEvaluation`. `ModifierOrder` decides where it
sits in the pipeline.

---

## 3b. The proof-of-concept enemies

Five prefabs in `Assets/Enemies/Prefabs/`, built only to prove the architecture holds. Each
is the same `Enemy` component with a different set of capabilities — no subclass of `Enemy`
exists anywhere, and no enemy-specific code was written for any of them.

| Prefab | Composition | What it proves |
| --- | --- | --- |
| `POC_Walker` | GroundMovement + Patrol + ContactDamager | Works with **no** perception and **no** attack modules |
| `POC_Chaser` | + Perception, Alert, Chase, Search, AttackState, MeleeAttack | Detection with LOS, a tell, a committed attack with a punish window |
| `POC_FlyingShooter` | FlyingMovement + MaintainDistance + ProjectileAttack | A different movement and attack pairing, same brain |
| `POC_Defender` | + DefenseController, ShieldDefense, DefendState | Directional defense driven by a state, not a special enemy type |
| `POC_WallCrawler` | SurfaceMovement + Patrol + ContactDamager | Floor→wall→ceiling travel with no separate framework |

To try one, drag it into a scene near the player and press play. They need no wiring: the
player prefab already carries `Targetable`, a `Hurtbox`, and an `AttackHitbox` that opens
during its `Attack1/2/3` animation states.

## 4. Performance

Decisions made for scenes with many enemies:

- **Two engine callbacks per enemy, not a dozen.** Modules never implement `Update`;
  `Enemy` ticks them in a sorted array. Ordering is explicit and deterministic.
- **Perception is throttled and registry-based.** Ten evaluations a second, not 60, and
  targets come from `TargetRegistry` rather than a physics overlap — one distance check in
  the common case. The line-of-sight raycast runs only for a candidate that already passed
  distance and view-cone tests. First tick is randomly offset so enemies spawned together
  don't all evaluate on the same frame.
- **Probes are computed once per physics step and cached.** `IsGrounded`, `IsEdgeAhead` and
  friends are field reads, not queries.
- **Dormancy.** `EnemyActivation` runs four checks a second and suspends perception, brain
  and movement for anything far away.
- **No per-frame `GetComponent`.** All role lookups happen once in `Enemy.Initialize`.
- **No allocation in steady state.** `DamageInfo` is a struct passed by `in`; hit dedupe
  reuses a `HashSet`; attack selection reuses a buffer; no LINQ anywhere in the runtime.
- **Pooling** for projectiles and, optionally, enemies.
- **Animator parameters hashed once** at bind time.

The one deliberate cost: `Hitbox` uses trigger callbacks, so a hitbox opened mid-frame
registers on the next physics step (up to 0.02s). That is invisible in play and avoids a
per-frame overlap query per live hitbox.

Attack attachments (melee hitboxes, projectile muzzles) are mirrored to the owner's facing
at attack time by `AttackBehaviour.ResolveAttachment`, so flipping a sprite — which does not
move child transforms — never leaves an enemy swinging or firing out of its back. Author
attachments on the positive-X side; the framework handles the rest. Enemies using
`FacingMode.ScaleX` already mirror their children, and are skipped.

---

## 5. Intentionally left open

- `Projectile.UpdateMotion` is `virtual` — new flight paths need no changes to damage or pooling.
- `EnemyModule` is the extension point for whole new capability classes (loot, status
  effects, dialogue, phase controllers) with no edit to `Enemy`.
- `EnemyLifecycleState` already distinguishes Dormant from Active, so off-screen simulation
  levels can be added later without touching callers.
- `ITargetable` never assumes one player: decoys, summons and co-op all fit.
- `AttackDefinition.FollowUp` gives combos; boss phase scripts can call
  `AttackController.ForceAttack` directly.
- Attack timing has two sources (numeric and animation events) and attacks can mix.

## 6. Known limitations

1. **No pathfinding.** Chase is direct steering with ledge stopping. Fine for Hollow-Knight-
   style rooms; a navigation solution would slot in behind `IMovementController`.
2. **`SurfaceMovement` handles convex and concave corners but not moving platforms or
   one-way colliders**, and it can lose a surface on very thin geometry.
3. **No loot or drops.** The `Died` event is the hook; a `LootDropper` module is a
   half-hour's work once there is a pickup system.
4. **No status effects.** `DamageType` is a flags enum ready for it, but nothing consumes it
   beyond `ArmorDefense`.
5. **Boss phases** have no dedicated controller yet. A phase module driving state priorities
   and swapping defenses is the natural next addition.
6. **`AnimatorHitboxDriver` is a bridge, not a destination** for the player. When the player
   gets its combat pass, authored clip events plus the attack framework are the better path.
7. **Existing scene geometry is on the `Default` layer**, so terrain masks include `Default`.
   Moving level geometry onto the new `Terrain` layer would tighten every probe and sight
   test.
8. **`EnemyScript` / `OozlingEnemy` (the old enemies) are untouched** and still work. They
   are not part of this framework; migrate or delete them when convenient.
