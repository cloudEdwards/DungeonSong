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
 input → intent     selects from   slot → ability   mana/soul/      nearest IInteractable
 + buffering        attack assets        │          charges
        │              │                 │
        │       HitboxDirectory    AbilityBehaviour
        │       keyed hitboxes      ├── ProjectileAbility → Eldritch Blast
        │              │            └── EffectAbility     → Cure Wounds
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
| Abilities | `AbilityDefinition`, `AbilityBehaviour`, `AbilityRequirement`, `AbilityLoadout`, `ProjectileAbility`, `EffectAbility`, `Targeting` |
| Effects | `GameplayEffect`/`EffectContext`, `DamageEffect`, `HealEffect` |
| Resources | `ResourceDefinition`/`ResourceCost`, `ResourcePool` |
| Interaction | `IInteractable`, `PlayerInteractor` |
| Animation | `IPlayerAnimator`, `PlayerAnimatorAdapter` |
| Debug | `PlayerDebugOverlay` |

### `Assets/Scripts/World` — `DungeonSong.World`

`IRestPoint`, `RestEvents`, `Campfire`, `RequireNearRestPoint`, `CheckpointService`,
`SaveData`, `ISaveService`, `JsonSaveService`, `PlayerRespawner`

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
| **Q** | Eldritch Blast |
| **R** | Cure Wounds (only at a campfire) |
| Right mouse | Block (unchanged, still in `PlayerController`) |

Bindings live on `LegacyInputSource`. The HUD reads them, so rebinding updates prompts and
the ability bar automatically.

---

## 5. The vertical slice

| Piece | Proof |
| --- | --- |
| 7 attacks | 3-hit forward chain, up, grounded down, airborne pogo (`RecoilOnHit: 9`), wall strike — all one framework, zero attack-specific code |
| Eldritch Blast | `ProjectileAbility` + `AbilityDefinition` + `ProjectileDefinition`. Reuses the enemy projectile system |
| Cure Wounds | `EffectAbility` + `HealEffect` + `RequireNearRestPoint`. No healing-specific code anywhere |
| Campfire | `IInteractable` + `IRestPoint`: heals, restores resources and charges, sets checkpoint, saves, raises `RestEvents` |
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
5. **Save data is minimal** — checkpoint and health only. No world state, unlocks or
   inventory.
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
