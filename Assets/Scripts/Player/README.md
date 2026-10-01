# Player Framework

Combat, abilities, spells, tools, resources, interaction and rest for the player character —
built so that adding the hundredth attack or the thirtieth spell never means editing
`PlayerController` or `PlayerActor`.

> **Making something?** See [CREATING_ABILITIES.md](CREATING_ABILITIES.md) — step-by-step
> recipes for attacks, spells, tools, effects, items, resources and rest points. This
> document explains how the framework works; that one explains how to use it.

---

## 1. Shape

```
Assembly-CSharp              DungeonSong.Combat         DungeonSong.Player      DungeonSong.World      DungeonSong.UI
(PlayerController,     ◄──   (damage model, shared  ◄──  (player framework) ◄──  (campfires,      ◄──   (HUD)
 PlayerHealth)               with the enemies)                                   checkpoints, save)
      │                              ▲
      └── implement IPlayerMotionContext, IDamageable, IHealth ──┘
```

Dependencies run one way. Neither `Combat` nor `Player` can reference `Assembly-CSharp`, so
the framework *cannot* couple itself to `PlayerController` even by accident. The two touch
points are interfaces the existing classes implement:

- `PlayerController : IPlayerMotionContext` — grounded? on a wall? facing? plus a movement lock.
- `PlayerHealth : IDamageable, IHealth` — the write side and the read/observe side of health.

### Runtime structure

```
                        ┌──────────────────────────────┐
                        │          PlayerActor         │  lifecycle, module tick,
                        │   (hub — never subclassed)   │  events, movement-lock counter
                        └───────────────┬──────────────┘
        ┌──────────────┬────────────────┼───────────────┬──────────────────┐
        ▼              ▼                ▼               ▼                  ▼
 PlayerInputRouter  PlayerCombat   AbilityLoadout   ResourcePool    PlayerInteractor
 input → intent     selects from   slot → ability   Loyalty, spell  nearest IInteractable
 + buffering        attack assets        │          slots (+ResourceOnHit)
        │       IAttackModifier[]        │
        │       HitboxDirectory    AbilityBehaviour
        │       keyed hitboxes      ├── ProjectileAbility  → Eldritch Blast (cantrip)
        │              │            ├── SmiteAbility       → Divine Smite
        │              │            ├── HitboxBurstAbility → Burning Hands
        │              │            └── EffectAbility      → Cure Wounds, Short Rest
        │              │                 │
        │              │           GameplayEffect[]  (DamageEffect, HealEffect, …)
        │              │           AbilityRequirement[] (RequireNearRestPoint, …)
        └──────────────┴─────────────────┴────────────────────┐
                                                              ▼
                                        DungeonSong.Combat — shared with enemies
                                   Hitbox · Hurtbox · DamageInfo · AttackPayload
                                   HitStop · HitFeedback · Projectile · PrefabPool
```

### Damage flow — identical in both directions

```
attacker Hitbox ──trigger──► Hurtbox ──► IDamageable.TakeDamage(in DamageInfo)
                                              │
                                              ├─► defenses / i-frames
                                              ├─► health, knockback (IKnockbackReceiver)
                                              ├─► HitStop.Request           (feel)
                                              ├─► HitFeedback: flash + blood (juice)
                                              └─► IHealth.Damaged event ──► HUD, death, AI
```

Player hits enemy and enemy hits player run the *same* path. Neither side has its own damage
philosophy.

---

## 2. Key separations

| Separation | Why it matters |
| --- | --- |
| **Input vs intent** | `PlayerInputRouter` turns "up held + button down" into `AttackIntent(Up)` captured *at press time*, and buffers it. No combat code polls a key, and releasing up before the swing still yields an up attack. |
| **Definition vs executor** | `PlayerAttackDefinition` / `AbilityDefinition` describe; `PlayerCombat` / `AbilityBehaviour` perform. Same data, different performers. |
| **Ability vs effect** | Abilities handle cost, cooldown, timing and targeting. `GameplayEffect` assets are the consequences, reused across abilities. |
| **Requirement vs ability** | `AbilityRequirement` assets gate activation ("only at a campfire"). The ability does not know what a campfire is. |
| **Checkpoint vs save** | `CheckpointService` knows *where* you respawn; `ISaveService` knows how to persist. |
| **Rest vs world reaction** | `Campfire.Rest()` ends at `RestEvents.RaiseRested`. Enemy respawning and world resets listen. |
| **Death vs checkpoint** | `PlayerHealth` raises `Died`; `PlayerRespawner` asks the checkpoint service where to go. |
| **HUD vs gameplay** | The HUD only reads. Gameplay has no idea it exists, so it can be replaced or switched off freely. |

---

## 3. Files

### `Assets/Scripts/Combat` — shared with enemies

| File | Responsibility |
| --- | --- |
| `AttackPayload.cs` | Damage half of an attack, as reusable serializable data |
| `IHealth.cs` | Read/observe side of a health pool. Implemented by player *and* enemies |
| `HitStop.cs` | Brief global freeze on impact. Longest request wins |
| `HitFeedback.cs` | White flash + particle burst, driven by `IHealth.Damaged` |
| `Hitbox` / `Hurtbox` / `DamageInfo` / `DamageResult` | The damage model both sides share |
| `Projectiles/*` | Pooled projectiles, used by enemy spits and Eldritch Blast alike |

### `Assets/Scripts/Player` — `DungeonSong.Player`

| Area | Files |
| --- | --- |
| Core | `PlayerActor`, `PlayerModule`, `IPlayerMotionContext` |
| Input | `PlayerIntent` (+`AttackDirection`), `IPlayerInputSource`, `LegacyInputSource`, `PlayerInputRouter` |
| Combat | `PlayerAttackDefinition`, `HitboxDirectory`, `PlayerCombat` |
| Abilities | `AbilityDefinition`, `AbilityBehaviour`, `AbilityRequirement`, `AbilityLoadout`, `ProjectileAbility`, `EffectAbility`, `SmiteAbility`, `HitboxBurstAbility`, `Targeting` |
| Effects | `GameplayEffect`/`EffectContext`, `DamageEffect`, `HealEffect`, `RestoreResourcesEffect` |
| Resources | `ResourceDefinition`/`ResourceCost`/`RestType`, `ResourcePool`, `ResourceOnHit` |
| Interaction | `IInteractable`, `PlayerInteractor` |
| Animation | `IPlayerAnimator`, `PlayerAnimatorAdapter` |
| Debug | `PlayerDebugOverlay` |

### `Assets/Scripts/World` — `DungeonSong.World`

`IRestPoint`, `RestEvents`, `Campfire`, `RequireNearRestPoint`, `CheckpointService`,
`SaveData`, `ISaveService`, `JsonSaveService`, `SaveLoader`, `PlayerRespawner`;
Editor: `SaveMenu` (Dungeon ▸ Save)

### `Assets/Scripts/UI` — `DungeonSong.UI`

`PlayerHud`, `HudView`, `HealthView`, `ResourceView`, `AbilityBarView`, `AbilitySlotView`,
`InteractionPromptView`

### Modified existing files

| File | Change |
| --- | --- |
| `PlayerController.cs` | Implements `IPlayerMotionContext`; attack/heal/test-key input removed; movement lock added. Movement only |
| `PlayerHealth.cs` | Implements `IHealth`; honours `DamageFlags`, applies knockback, raises events, `RestoreToFull` |
| `EnemyHealth.cs` | Also implements `IHealth`, so healing and hit feedback work on any actor |

---

## 4. Controls

| Input | Action |
| --- | --- |
| Left mouse | Attack. Direction comes from movement input: neutral = forward, up, down, diagonals in the air, wall attack while wall-sliding |
| **F** | Interact — "Press F to take a Long Rest" at a campfire |
| **Q** | Eldritch Blast — warlock cantrip, cooldown only |
| **E** | Divine Smite — 1 Paladin slot; next 3 swings deal bonus Holy damage |
| **R** | Burning Hands — 1 Warlock slot; short cone of fire |
| **C** | Cure Wounds — 1 Paladin slot; heal anywhere |
| **Tab** | Short Rest — needs a full Loyalty bar; ~1s channel, broken by damage |
| Right mouse | Block (unchanged, still in `PlayerController`) |

Bindings live on `LegacyInputSource`. The HUD reads them, so rebinding updates prompts and
the ability bar automatically.

---

## 5. The vertical slice

| Piece | Proof |
| --- | --- |
| 7 attacks | 3-hit forward chain, up, grounded down, airborne pogo (`RecoilOnHit: 9`), wall strike — all one framework, zero attack-specific code |
| Eldritch Blast | `ProjectileAbility` + `AbilityDefinition` + `ProjectileDefinition`. Reuses the enemy projectile system |
| Cure Wounds | `EffectAbility` + `HealEffect`, costing a Paladin slot. No healing-specific code anywhere |
| Campfire | `IInteractable` + `IRestPoint`: the long rest. Full heal, `RestoreFor(RestType.Long)`, sets checkpoint, saves, raises `RestEvents` |

### Spell slots, Loyalty and resting

D&D-style casting, all built on `ResourceDefinition` — a spell slot is a resource with a
max of 2 and a cost of 1, so no casting code knows slots exist.

| Resource | Max | Refilled by | Notes |
| --- | --- | --- | --- |
| `Resource_WarlockSlots` | 2 | Short + Long rest | Burning Hands |
| `Resource_PaladinSlots` | 2 | Long rest | Divine Smite, Cure Wounds |
| `Resource_Loyalty` | 100 | never — earned | +10 per landed hit (`ResourceOnHit`), emptied on death. Formerly mana; same GUID |

- **Cantrip** = an ability with an empty **Cost**. Cooldown is its only limit.
- **Short rest** (`Ability_ShortRest`) is an `EffectAbility` costing `{Loyalty, 100}` with
  **Spend Cost On Resolve** on, so an interrupted rest keeps the Loyalty, and **Suspend In
  Air** on, so resting mid-jump hangs in place like Silksong's bind. Its effects are
  `HealEffect` (30% of max) + `RestoreResourcesEffect(Short)`.
- **Long rest** is the campfire. Loyalty is untouched.
- **Death** empties Loyalty; respawning refills what a long rest would, without saving. If the
  checkpoint is in another scene, `CheckpointTravel` loads it first — the same trip a save resume takes.
- **Save and load.** The campfire writes the save. `SaveLoader` reads it once when a play
  session starts: it loads the save's scene if needed, puts the player at that campfire via
  `RespawnAt`, and writes the saved Loyalty and slots back. In the Editor,
  **Dungeon ▸ Save ▸ Load Save On Play** turns this off so any scene can be tested directly;
  **Delete Save** and **Reveal Save File** sit beside it.
- Pool values live for the whole session in `ResourcePool`, keyed by resource **Id**, because
  the player object is rebuilt in every scene. Slot counts are fixed data on the assets
  today; a levelling system raises **Max Amount**.
- Loyalty gain listens to `CombatEvents.DamageDealt`, raised by `Hurtbox.Receive`, so every
  attack — melee, projectile, cone, smite — counts without opting in.
| Hit feedback | `DungeonSong/SpriteFlash` shader + green blood particles, via `MaterialPropertyBlock` |

---

## 6. Performance

- **Two engine callbacks for the whole player.** Modules never implement `Update`;
  `PlayerActor` ticks them in order, so input is always read before combat consumes it.
- **No per-frame `GetComponent`.** All roles resolved once in `PlayerActor.Initialize`.
- **No allocation in steady state.** `DamageInfo`, `AttackPayload`, `EffectContext` and the
  intent structs are all value types passed by `in`; attack and cooldown bookkeeping reuses
  buffers; no LINQ in any runtime path.
- **Flash via `MaterialPropertyBlock`.** Any number of actors flash at once on one shared
  material, adding no draw calls and leaking no material instances.
- **Pooled projectiles and VFX.** `PrefabPool` for both; particle bursts self-release
  through `PooledLifetime`.
- **HUD refreshes on events**, except cooldown sweeps which tick at 20 Hz, in unscaled time
  so they keep moving through hit-stop.

---

## 7. Known limitations

1. **Cure Wounds is press-to-heal, not hold-to-channel.** The original `HealHold` coroutine
   is still in `PlayerHealth` but unused by abilities. A `ChannelledAbility` executor is the
   natural addition if the hold feel is wanted.
2. **No item or inventory system.** `AbilityDefinition.MaxCharges` and `AbilityCategory.Tool`
   are the hooks; an item that grants or unlocks an ability has nothing to plug into yet.
3. **Animation keys reuse the Hero Knight rig.** `attack_up` and `cast` are mapped onto
   `Attack2` because no dedicated clips exist. Rebinding is data on `PlayerAnimatorAdapter`.
4. **Attack timing is numeric, not animation-driven.** The enemy framework supports clip
   events; the player does not yet. Worth adding when real attack animations land.
5. **Save data is minimal** — checkpoint, health and resource pools. No world state,
   unlocks or inventory. One save file, no slots.
6. **`SpriteFlash` is unlit.** Fine today (no `Light2D` in any scene); needs a URP 2D lit
   variant if lights are added.
7. **Two enums named `AttackPhase`** existed briefly; the player's is `PlayerAttackPhase`.
   Watch for that when working across both frameworks.
8. **No hold/charge attacks yet.** `IPlayerInputSource` exposes `AttackHeld`/`AttackReleased`
   and nothing consumes them.

---

## 8. Next architectural steps

1. `ChannelledAbility` — hold-to-channel, for the original heal feel and future beams.
2. Animation-event timing for player attacks, matching the enemy framework.
3. An item layer that grants abilities, feeding `AbilityLoadout.Equip`.
4. Status effects: `DamageType` is already a flags enum ready to carry them.
5. A `StatusEffect` variant of `GameplayEffect` with duration, applied by the same abilities.
