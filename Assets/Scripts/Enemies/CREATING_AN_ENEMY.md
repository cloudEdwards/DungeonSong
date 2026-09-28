# Creating a New Enemy

A step-by-step recipe. No C# required — everything here is assets and components.

Worked example throughout: **"Spitter"**, a ground enemy that patrols, notices the player,
keeps its distance, and spits at them.

> **Faster route:** duplicate the closest `Assets/Enemies/Prefabs/POC_*.prefab`, retune its
> config assets, and skip to step 10. The long form below is for understanding what the
> pieces are and for enemies that don't resemble an existing one.

---

## Before you start: the three ideas

1. **`Enemy` is a hub, not a base class to inherit from.** You never subclass it. It owns
   the lifecycle and holds references to whatever capability components are on the prefab.
2. **Capabilities are components.** Movement, perception, brain, attacks, defenses. Add the
   ones this enemy needs; leave off the ones it doesn't. An enemy with no perception simply
   never acquires targets — nothing breaks.
3. **Tuning lives in ScriptableObjects.** Stats, movement, perception and attacks are
   assets, so several enemies can share them and a balance pass is one edit.

---

## Step 1 — Create the tuning assets

In the Project window, right-click in a folder (e.g. `Assets/Enemies/Config`) →
**Create ▸ Dungeon ▸ Enemies ▸ …**

| Asset | Menu path | Needed for |
| --- | --- | --- |
| Enemy Stats | Enemies ▸ Enemy Stats | Every enemy |
| Movement (Ground) | Enemies ▸ Movement ▸ Ground | Walkers |
| Movement (Flying) | Enemies ▸ Movement ▸ Flying | Flyers |
| Movement (Surface) | Enemies ▸ Movement ▸ Surface | Wall/ceiling crawlers |
| Perception Settings | Enemies ▸ Perception Settings | Anything that reacts to the player |
| Attack Definition | Enemies ▸ Attack Definition | One per attack |
| Projectile Definition | Combat ▸ Projectile Definition | Ranged attacks |

**For the Spitter**, create three: `Stats_Spitter`, `Move_Spitter` (Ground),
`Perception_Spitter`.

### Enemy Stats — the fields that matter

| Field | What it does | Sensible range |
| --- | --- | --- |
| `Max Health` | Hit points | 15–60 for trash, 200+ for bosses |
| `Hit Invulnerability` | i-frames after being hit | 0.1–0.2. Set 0 to let combos chain freely |
| `Max Poise` | Poise damage absorbed before staggering | 0 = staggers on every hit; 20+ = tanky |
| `Poise Regen Delay` / `Rate` | How fast poise comes back | 1.5s / 10 per second |
| `Stagger Duration` | Length of the punish window | 0.4–0.8 |
| `Knockback Resistance` | 0 = light, 1 = immovable | 0 for bugs, 1 for crawlers |
| `Despawn Delay` | Time between dying and disappearing | Match your death animation |

Spitter: Max Health 25, Max Poise 8, Stagger Duration 0.5, Despawn Delay 0.9.

---

## Step 2 — Create the Enemy Definition

**Create ▸ Dungeon ▸ Enemies ▸ Enemy Definition**, name it `Enemy_Spitter`, and assign the
three assets from step 1 into **Stats**, **Movement** and **Perception**.

Leave **Prefab** empty for now — you'll fill it in at step 10.

> Attacks are deliberately *not* listed here. Attack components on the prefab reference
> their own definitions; listing them here too would create a second source of truth.

---

## Step 3 — Build the prefab skeleton

Create an empty GameObject and set its **Layer** to `Enemy`, then add:

- **Sprite Renderer** — your art. Set Sorting Layer to `Enemy`.
- **Rigidbody2D** — tick **Freeze Rotation Z**. Gravity is set automatically by the
  movement component, so ignore Gravity Scale here.
- **Collider2D** (Box or Capsule) — the *physical* body that collides with terrain. Not a
  trigger.

Then add these children:

```
Spitter                     layer: Enemy
├── Hurtbox                 layer: Enemy          (step 8)
├── Muzzle                  layer: Enemy          (step 9, ranged only)
└── Hitbox_Primary          layer: EnemyAttack    (step 9, melee only)
```

Author the Muzzle and Hitbox on the **right-hand side** (positive local X), as if the enemy
always faced right. Attacks mirror them to match facing automatically, so you never have to
duplicate them or add a flip script.

---

## Step 4 — Add the core components

On the root, add **`Enemy`**, then **`EnemyHealth`**, then **`EnemyStateMachine`**.

Configure `Enemy`:

| Field | Set to |
| --- | --- |
| **Definition** | `Enemy_Spitter` |
| **Team** | `Enemy` |
| **Hostile Teams** | `Player` |
| **Initialize On Awake** | ✔ |
| **Activate On Spawn** | ✔ (uncheck for ambushers woken by `EnemyActivation` or a trigger) |
| **Pooled** | ✘ (tick only if you spawn many via `EnemyFactory`) |
| **Facing Mode** | `Sprite Flip X` — but `None` for surface crawlers |
| **Sprite Renderer** | your renderer |
| **Artwork Facing** | `1` if the art faces right unflipped, else `-1` |

`EnemyHealth` needs no setup — it reads stats from the definition. Use **Stats Override**
only for a one-off variant ("elite" version of an existing enemy).

---

## Step 5 — Add movement

Add **one** of `GroundMovement`, `FlyingMovement`, `SurfaceMovement`.

Leave **Settings Override** empty (it falls back to the definition). Then set the probes —
these are prefab-specific because they depend on your collider size:

| Field | Meaning | Typical |
| --- | --- | --- |
| **Terrain Mask** | What counts as solid | **`Default` + `Terrain`** (see warning below) |
| **Ground Probe Offset** | Local point at the feet | `(0, -halfHeight)` |
| **Ground Probe Radius** | Size of the ground check | 0.1–0.15 |
| **Wall Probe Distance** | How far ahead walls are detected | slightly wider than the body |
| **Edge Probe Forward** | How far ahead the ledge check looks | same as wall probe |
| **Edge Probe Depth** | Drop depth that counts as a ledge | 0.5–0.8 |

**Check your work:** select the prefab in a scene and look at the gizmos. The ground probe
turns green when grounded, the wall probe red when blocked, the edge probe yellow at a ledge.
If they never change colour, your Terrain Mask is wrong.

> ⚠️ **Tick `Default` in every mask.** This project's level geometry is still on the
> `Default` layer, not `Terrain`. A mask set to `Terrain` alone hits nothing at all: no
> ground, no ledges, no walls — the enemy walks off every edge and never turns around.
> This applies to **Terrain Mask** here and to **Obstruction Mask** on Perception Settings.

---

## Step 6 — Add perception (skip for mindless enemies)

Add **`EnemyPerception`**. Optionally create an empty child called `Eye` at head height and
assign it to **Eye**, so sight lines start from the head rather than the feet.

Tuning lives on the Perception Settings asset:

| Field | Effect |
| --- | --- |
| **Detection Radius** | How far it can notice you |
| **Lose Radius** | Must be **larger** than detection, or aggro flickers at the boundary |
| **Field Of View Degrees** | 360 = all-round; 140–200 = has a blind spot behind it |
| **Require Line Of Sight** | Walls block sight. Costs one raycast per evaluation |
| **Obstruction Mask** | What blocks sight. **Tick `Default` as well as `Terrain`** — this project's level geometry is still on `Default`, so a Terrain-only mask means the enemy sees straight through walls |
| **Memory Duration** | How long it searches after losing you |
| **Hearing Radius** | 0 disables hearing. Non-zero reacts to `NoiseEvents` |
| **Tick Interval** | 0.1 (ten times a second) is right for almost everything |

---

## Step 7 — Add the brain

`EnemyStateMachine` is already on the root. Now add one component per behaviour. The
machine picks them up automatically — there's nothing to register.

**Priority decides who wins.** Higher pre-empts lower, if the running state allows it.

| State | Priority | Add it when |
| --- | --- | --- |
| `IdleState` | 0 | Always — it's the fallback |
| `PatrolState` | 10 | It should move around when unaware |
| `SearchState` | 25 | It should investigate where you went |
| `ChaseState` | 30 | It closes distance |
| `MaintainDistanceState` | 30 | It keeps range instead (ranged enemies) |
| `AlertState` | 55 | You want a "it noticed you" tell |
| `AttackState` | 60 | It has attacks (requires `AttackController`) |
| `DefendState` | 65 | It has a defense to raise |
| `FleeState` | 70 | It runs when hurt |
| `HurtState` | 80 | Almost always — the flinch reaction |
| `StaggerState` | 90 | Almost always — the poise-break reaction |
| `DeadState` | 1000 | Always |

Use **Priority Override** (−1 = use the default) only when one enemy needs different
ordering from the norm.

**For the Spitter:** Idle, Patrol, Alert, MaintainDistance, Attack, Search, Hurt, Stagger, Dead.

⚠️ **The most common mistake:** `ChaseState`'s **Stop Distance** must be **less than or equal
to** your attack's **Max Range**. If it stops at 3 and the attack reaches 1.5, the enemy
walks up, stops, and never attacks. Same for `MaintainDistanceState`: its band must overlap
the attack's Min/Max range.

---

## Step 8 — Add the hurtbox (or it can't be hurt)

On the `Hurtbox` child:

- **BoxCollider2D**, **Is Trigger ✔**, sized to the body.
- **`Hurtbox`** component → set **Owner Source** to the root's `EnemyHealth`.
- **Damage Multiplier** `1`. Add a *second* hurtbox child with multiplier `2` and
  **Is Weak Point ✔** for a soft spot.

Without this, player hitboxes pass straight through.

---

## Step 9 — Add attacks

Add **`AttackController`** to the root (plus `AttackState` from step 7), then one component
per attack.

### Melee

1. On `Hitbox_Primary`: **BoxCollider2D**, **Is Trigger ✔**, plus the **`Hitbox`** component.
   Set **Key** to `Primary` and **Target Teams** to `Player`. Leave the collider enabled —
   the `Hitbox` disables it on Awake and only opens it during active frames.
2. On the root, add **`MeleeAttack`** and assign its **Definition** and **Hitbox**.

### Ranged (what the Spitter uses)

1. Create a **Projectile Definition** asset and point its **Prefab** at a projectile prefab
   (copy `POC_Projectile.prefab`).
2. In the **Attack Definition**, assign that to **Projectile**.
3. On the root, add **`ProjectileAttack`**, assign **Definition** and set **Muzzle** to the
   `Muzzle` child.

### Attack Definition fields

| Field | What it controls |
| --- | --- |
| **Startup** | The player's tell. **Never set this to 0** |
| **Active** | How long the hitbox is open |
| **Recovery** | The punish window after the swing |
| **Cooldown** | Before *this* attack repeats |
| **Min / Max Range** | When it may be chosen. Min > 0 makes it refuse close targets |
| **Facing Tolerance** | 180 = can attack in any direction |
| **Selection Weight** | Relative odds when several attacks are usable |
| **Interruptible** | Unticked = committed, and therefore punishable |
| **Motion** | `Lunge`, `Leap` or `Dash` to move during the attack |
| **Follow Up** | Another Attack Definition, for combos |
| **Animation Key** | Semantic key, e.g. `attack` (see step 11) |

`AttackController`'s **Global Cooldown** paces *all* attacks — it's the enemy's overall
aggression dial.

Spitter: Startup 0.45, Active 0.08, Recovery 0.3, Cooldown 1.8, Min Range 2.5, Max Range 9,
Interruptible ✔.

---

## Step 10 — Save the prefab and close the loop

Drag the GameObject into `Assets/Enemies/Prefabs/`, then **open `Enemy_Spitter` and set its
Prefab field to the new prefab.** Spawners work from the definition, so this link is what
makes the enemy spawnable from data.

---

## Step 11 — Animation (optional)

The AI never touches Animator parameters. To hook up animation, add **`AnimatorAdapter`** to
the root and map semantic keys to your controller's parameters:

| Field | Example |
| --- | --- |
| **Locomotion Speed Parameter** | `Speed` (float) |
| **Action Bindings** | key `attack` → trigger `Attack1`; key `hurt` → trigger `Hurt`; key `death` → trigger `Death` |
| **Flag Bindings** | key `shielding` → bool `IdleBlock` |

Unmapped keys are ignored, so a half-rigged enemy still runs. Tick **Warn On Missing Keys**
while rigging to find gaps.

**Frame-accurate timing (optional):** set the Attack Definition's **Timing Source** to
`Animation Events`, add `AnimationEventRelay` to the object holding the Animator, and add
clip events calling `AE_HitboxOn`, `AE_HitboxOff`, `AE_AttackComplete`.

---

## Step 12 — Optional extras

| Want | Add |
| --- | --- |
| Touching it hurts | `ContactDamager` on the root |
| Damage reduction | `DefenseController` + `ArmorDefense` |
| Blockable from the front only | `DefenseController` + `ShieldDefense` + `DefendState` |
| Spiky shell that hurts attackers | `DefenseController` + `SpikeDefense` + `DefendState` |
| i-frame windows / phase changes | `DefenseController` + `InvulnerabilityDefense` |
| Sleeps until you approach | `EnemyActivation` |
| On-screen AI debugging | `EnemyDebugOverlay` |

`DefenseController` is required for **any** defense to take effect.

---

## Step 13 — Test it

Drag the prefab into a scene near the player and press Play. The player prefab is already
wired (`Targetable`, `Hurtbox`, `AttackHitbox`), so nothing else is needed.

Select the enemy in the Hierarchy while playing. With `EnemyDebugOverlay` you'll see its
lifecycle, current state, health, target, current attack and phase, and its ground/wall/edge
sensor readings floating above it.

---

## Archetype recipes

**Simple walker** — GroundMovement, Idle, Patrol, Hurt, Stagger, Dead, ContactDamager, Hurtbox.
No perception, no attacks.

**Melee chaser** — the above plus EnemyPerception, Alert, Chase, Search, AttackController,
AttackState, MeleeAttack, Hitbox.

**Ranged kiter** — FlyingMovement or GroundMovement, EnemyPerception, MaintainDistance,
AttackController, AttackState, ProjectileAttack, Muzzle.

**Shielded knight** — melee chaser plus DefenseController, ShieldDefense, DefendState
(Defend triggers on distance; the shield lowers automatically when the attack runs).

**Spiky roller** — walker plus DefenseController, SpikeDefense, DefendState, and a second
ContactDamager assigned to the SpikeDefense's **Shell Contact Damage**.

**Wall crawler** — SurfaceMovement, `Facing Mode: None`, Patrol with **Turn At Edges** and
**Turn At Walls** both unticked, Knockback Resistance 1.

---

## Troubleshooting

| Symptom | Likely cause |
| --- | --- |
| Doesn't move at all | No Movement Settings on the definition (check the Console — it warns), or no movement component |
| Doesn't move, and the settings asset shows **"Missing (Mono Script)"** | The ScriptableObject class shares a .cs file with another type. Unity only binds a script to the type matching the file name, so the asset loads as null. Give the class its own file named after it |
| Attacks or projectiles come out of its back | Only if you moved the Muzzle/Hitbox to negative local X — author them on the right and let the framework mirror them |
| Walks off every ledge | **Terrain Mask** wrong, so the edge probe never hits anything |
| Never notices the player | Player is missing `Targetable`; or **Hostile Teams** isn't `Player`; or no Perception Settings (it warns) |
| Notices through walls | **Require Line Of Sight** unticked, or **Obstruction Mask** doesn't include terrain |
| Aggro flickers on and off | **Lose Radius** ≤ **Detection Radius** |
| Walks up and just stands there | `ChaseState` **Stop Distance** > attack **Max Range** |
| Attacks swing but deal no damage | Hitbox **Key** doesn't match the definition's **Hitbox Key** (it warns); target has no `Hurtbox`; or **Target Teams** wrong |
| Takes no damage from the player | No `Hurtbox` child, or its **Owner Source** is empty |
| Dies in one hit / has odd health | No Enemy Stats assigned — it falls back to defaults and warns |
| Never staggers | **Max Poise** too high, or an `ArmorDefense` is suppressing it |
| Stuck in one state forever | A higher-priority state always wants control — watch the debug overlay |
| Defense does nothing | Missing `DefenseController` on the root |
| Crawler spins or faces oddly | **Facing Mode** must be `None` for `SurfaceMovement` |
| Ignores knockback | **Knockback Resistance** is 1 |

---

## Running the tests

**In the Editor:** Window ▸ General ▸ Test Runner ▸ **EditMode** ▸ Run All.

**From the terminal** (the Editor must be open):

```bash
unity command run_tests --caller plugin --skill unity-cli --mode EditMode
```

Just this framework's tests:

```bash
unity command run_tests --caller plugin --skill unity-cli --mode EditMode \
  --filter "DungeonSong.Enemies.Tests"
```

Add `--result-only` for the full per-test JSON. Tests live in `Assets/Tests/EditMode/`;
current status is **66 passing**.

Add a test by dropping a new `[Test]` method into one of those files. `EnemyTestFixture`
builds enemies in code — edit mode never calls `Awake`, which is exactly why `Enemy` exposes
explicit `Initialize()` and `Spawn()` methods.
