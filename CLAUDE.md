# DungeonSong

2D pixel-art dungeon crawler. Unity **6000.4.4f1** (Unity 6.0), **URP 2D** (17.4.0).
Player art/animation comes from the *Hero Knight - Pixel Art* asset pack. The game is a
Hollow Knight / Silksong-style action platformer with D&D-flavoured casting: a
paladin/warlock knight with spell slots, cantrips, and short and long rests.

Deeper docs live beside the code — read them before changing those systems:
`Assets/Scripts/Player/README.md` (player framework, spells, rests, saves),
`Assets/Scripts/Player/CREATING_ABILITIES.md` (recipes), and the READMEs under
`Assets/Scripts/Enemies/`.

## Coding rules

1. **Every bug fixed after a manual check gets an automated regression test, in the same
   change.** If you verified something by playing or by probing the Editor, turn that check
   into a test before calling the fix done. See *Testing* below.
2. **Run the test suites before and after a change.** A change is not done while either
   suite is red. Fix tests your change broke; don't delete or loosen them to get green.
3. **Data before code.** Spells, attacks, resources and effects are ScriptableObject assets.
   A new spell is usually a new `AbilityDefinition` plus an existing executor; write a new
   `AbilityBehaviour`/`GameplayEffect` only for genuinely new mechanics. Tuning numbers
   (damage, heal %, slot counts, cooldowns) live on assets or serialized fields, never as
   literals in logic.
4. **Drive the Editor, don't hand-edit YAML.** Scene, prefab and asset wiring goes through the
   Editor (see *Driving the Editor*). Never hand-edit `.unity` / `.prefab` / `.asset` files
   while an Editor is reachable.
5. **Don't edit third-party assets.** `Assets/Hero Knight - Pixel Art/` (including its Animator
   Controller) stays untouched; work around it in code, as `PlayerHealth` does for the Death
   state, which has no exit transition.
6. **Respect assembly boundaries.** `DungeonSong.Combat` ← `Player` ← `World` ← `UI`. The
   legacy scripts in `Assets/Scripts/*.cs` (`PlayerController`, `PlayerHealth`,
   `CameraController`, `SceneTransitionManager`) compile into `Assembly-CSharp`, which the
   asmdef assemblies cannot reference: reach them through interfaces (`IPlayerMotionContext`,
   `IHealth`, `IDamageable`) or `SendMessage`, never by adding a reference.
7. **Enums instead of magic strings** for scenes, tags and gates (see *Conventions*).
8. **Match the surrounding code.** XML `<summary>` docs on public types and members that say
   *why*, not just what; Allman braces; `[SerializeField, Tooltip(...)]` on inspector fields;
   no LINQ or per-frame allocation in runtime paths; no per-frame `GetComponent`.
9. **Static state must be resettable.** Anything static that outlives a scene
   (`CheckpointService`, `ResourcePool`'s session values) needs a reset for new games and
   tests, and is cleared on `SubsystemRegistration` so it works with domain reload disabled.
10. **Never touch the player's real data from tools or tests.** Tests swap `GameSave.Service`
    for an in-memory service. If a manual test must change the save file or EditorPrefs (such
    as *Load Save On Play*), back it up first, restore it after, and check the restore worked.

## Testing

`com.unity.test-framework` 1.6. Two suites:

| Suite | Path | Use for |
| --- | --- | --- |
| EditMode | `Assets/Tests/EditMode` (`DungeonSong.Enemies.Tests`) | Logic: resource pools, abilities, combat, enemies, asset values |
| PlayMode | `Assets/Tests/PlayMode` (`DungeonSong.PlayMode.Tests`) | Real scenes: death/respawn, checkpoints, save resume, animator states |

```bash
unity command run_tests --mode EditMode --caller plugin --skill <skill>
unity command run_tests --mode PlayMode --caller plugin --skill <skill>   # async: poll test_status
```

- Prefer EditMode: build a player from `PlayerActor` + modules + fakes (`FakeMotionContext`,
  `FakeHealth`, `ProbeAbility`), call `PlayerActor.Initialize()`, and drive modules with
  `Tick(dt)`. `Awake`/`OnEnable`/`Destroy(obj, t)`/coroutines don't run in EditMode, so anything
  relying on those (e.g. `ResourceOnHit`, `HitboxBurstAbility`) is tested in PlayMode.
- Call `ResourcePool.ResetSession()` and `CheckpointService.Clear()` in setup and teardown.
- PlayMode tests load `Scene1`/`Scene2` (both in Build Settings), and must leave
  `PlayerDataDto` health full: it is an asset, so the value persists in the Editor.
- Shipped tuning that the user has asked for (e.g. short rest heals 30%, key layout
  Q/E/R/C/Tab) is pinned by `SpellbookDataTests`. Update the test when the design changes.

## Driving the Editor

The `com.unity.pipeline` package is installed, so when the Editor is open prefer driving it
over hand-editing `.unity` / `.prefab` YAML:

```bash
unity status                                     # look for state "ready"
unity command --caller plugin --skill <skill>    # list / run Editor commands
unity command get_scene_hierarchy --caller plugin --skill <skill>
unity command run_script --file X.cs --entry Class.Method --caller plugin --skill <skill>
```

If `unity` isn't on `PATH` (a shell started before the CLI was installed), call
`~/.unity/bin/unity` directly.

- `eval` does not accept `using` directives; use `run_script` with a static class for anything
  longer than a line.
- In Play mode the Editor stops advancing frames while unfocused. Set
  `Application.runInBackground = true` (runtime only) when driving Play mode from the CLI.
- `capture_game_view --save_path` writes under `Assets/` even for a `Temp/` path. Delete what
  it creates.

## Conventions

**Enums instead of magic strings.** `Assets/Scripts/Enums/` holds `SceneNameEnum`,
`TagEnum`, `GateIdEnum`, `GateSideEnum`. Scene loads and tag checks go through
`.ToString()` on the enum (`collision.CompareTag(TagEnum.Player.ToString())`,
`SceneManager.LoadSceneAsync(roomData.roomB.ToString())`). Enum member names must therefore
match the real scene and tag names exactly — renaming a scene means renaming the enum member.
When adding a scene, tag or gate, add the enum member first.

**ScriptableObject DTOs for shared state.** Types named `*Dto` under
`Assets/Scripts/{PlayerData,RoomData}/`, each with `[CreateAssetMenu(menuName="Dungeon/...")]`.

- `PlayerDataDto` — health/heal stats. This is *how health survives a scene change*:
  `PlayerHealth` reads and writes `playerData.Health` directly. `OnEnable` resets
  `Health = StartingHealth`. Edits persist into the asset in-Editor during play. A player
  that *arrives* in a scene at 0 health is mid-respawn and is set back to full.
- `RoomDataDto` — one asset per gate *pair*: `roomA`/`gateIdA` and `roomB`/`gateIdB`.
  A `RoomGateScript` knows only its own `GateSideEnum`; it resolves destination room and
  gate by picking the opposite side. One asset therefore wires both directions of a door.

**Stable ids.** `ResourceDefinition.Id`, `AbilityDefinition.Id` and `Campfire` **Rest Point
Id** are written into saves and session state. They must be unique and must not change once
players have saves.

**Field style.** `[SerializeField]` on inspector-facing fields, `protected` where a subclass
may need it. Hero-Knight-derived code keeps that pack's `m_` prefix (`m_body2d`,
`m_animator`); newer scripts don't.

**Input.** Still the legacy `Input.*` API. Abilities, attacks and interact go through
`LegacyInputSource` (`IPlayerInputSource`); `PlayerController` still reads movement, jump,
roll and block directly. Input System 1.19 and `InputSystem_Actions` are installed but unused;
`activeInputHandler: 2` (Both) keeps the legacy calls working. Don't half-migrate: a move to
the Input System is a new `IPlayerInputSource` plus converting `PlayerController` together.

**Keys.** Q Eldritch Blast · E Divine Smite · R Burning Hands · C Cure Wounds · Tab Short
Rest · F interact / long rest at a campfire. Bindings live on `LegacyInputSource` on the
`KnightAxios` prefab; the HUD reads them.

## Architecture

**Player** — `PlayerActor` is the hub; capabilities are `PlayerModule`s ticked in order
(`PlayerCombat`, `AbilityLoadout`, `ResourcePool`, `PlayerInteractor`, …). The legacy
`PlayerController` (movement) and `PlayerHealth` (health, death animation) sit beside it
behind `IPlayerMotionContext` / `IHealth`. Ground and wall detection use child
`Sensor_HeroKnight` objects found by name — renaming `GroundSensor`, `WallSensor_R1/R2`,
`WallSensor_L1/L2` breaks the controller.

**Casting and resting** — spell slots and Loyalty are `ResourceDefinition`s in a
`ResourcePool`; a spell costing `{Resource_WarlockSlots, 1}` is all a "slot spell" is, and a
cantrip has no cost. `RefilledBy` decides which rest refills a pool. Short rest = an
`EffectAbility` costing a full Loyalty bar; long rest = `Campfire.Rest`. Loyalty fills from
landed hits via `CombatEvents.DamageDealt` (raised in `Hurtbox.Receive`). Pool values persist
across scenes for the session, keyed by id.

**Checkpoints and saves** — `Campfire.Rest` sets the checkpoint (`CheckpointService`) and
writes `SaveData` through `GameSave.Service`. `CheckpointTravel` puts the player at a
checkpoint, loading its scene first if needed; it serves both `SaveLoader` (resume on Play,
only when the starting scene has a player; Editor toggle **Dungeon ▸ Save ▸ Load Save On
Play**) and `PlayerRespawner` (death in another scene).

**Scene transitions** — `SceneTransitionManager` is a `DontDestroyOnLoad` singleton spawned by
`GameManagerBootstrap` in each scene's `Awake` if missing, so any scene opens directly. A
gate pushes the destination `GateIdEnum` and facing into the manager, which fades, loads, and
on `sceneLoaded` moves the player to the matching gate's `gateSpawn`. A missing or mismatched
gate id silently leaves the player at the scene's authored position.

**Enemies** — the modular framework in `Assets/Scripts/Enemies/` (brain states, attacks,
defenses, movement). The older `Enemy/EnemyScript` + `OozlingEnemy` still exist.

## Layout

```
Assets/Scripts/            legacy scripts (Assembly-CSharp) + Enums/ PlayerData/ RoomData/
  Combat/                  DungeonSong.Combat — hitboxes, damage, projectiles, CombatEvents
  Player/                  DungeonSong.Player — actor, modules, abilities, resources, effects
  World/                   DungeonSong.World — campfires, checkpoints, saves; Editor/ menus
  UI/                      DungeonSong.UI — HUD views
  Enemies/                 DungeonSong.Enemies
Assets/PlayerData/         ability, resource and effect assets
Assets/Prefabs/            KnightAxios, HudCanvas, CampFire, EldritchBlastProjectile,
                           BurningHandsCone, RoomGate, SceneTransitionManager, …
Assets/Scenes/             Scene1, Scene2 (in Build Settings), SampleScene
Assets/Tests/              EditMode/ PlayMode/
Assets/Hero Knight - Pixel Art/   third-party art pack — do not edit
```

## Notes

- Commit messages in this repo use a leading `=` (`=add campfire`).
- `Debug.Log` calls are left in gameplay paths (scene fade, heal, save load); they're
  intentional for now — don't strip them as cleanup unless asked.
