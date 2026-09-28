# DungeonSong

2D pixel-art dungeon crawler. Unity **6000.4.4f1** (Unity 6.0), **URP 2D** (17.4.0).
Player art/animation comes from the *Hero Knight - Pixel Art* asset pack.

## Driving the Editor

The `com.unity.pipeline` package is installed, so when the Editor is open prefer driving it
over hand-editing `.unity` / `.prefab` YAML:

```bash
unity status                                     # look for state "ready"
unity command --caller plugin --skill <skill>    # list / run Editor commands
unity command get_scene_hierarchy --caller plugin --skill <skill>
```

If `unity` isn't on `PATH` (a shell started before the CLI was installed), call
`~/.unity/bin/unity` directly.

Scene and prefab wiring (component references, spawn transforms, `RoomDataDto` assignments)
lives in YAML that is painful to edit by hand — go through the Editor for it.

## Conventions

**Enums instead of magic strings.** `Assets/Scripts/Enums/` holds `SceneNameEnum`,
`TagEnum`, `GateIdEnum`, `GateSideEnum`. Scene loads and tag checks go through
`.ToString()` on the enum (`collision.CompareTag(TagEnum.Player.ToString())`,
`SceneManager.LoadSceneAsync(roomData.roomB.ToString())`). Enum member names must therefore
match the real scene and tag names exactly — renaming a scene means renaming the enum member.
When adding a scene, tag or gate, add the enum member first.

**ScriptableObject DTOs for shared state.** Types named `*Dto` under
`Assets/Scripts/{PlayerData,RoomData}/`, each with `[CreateAssetMenu(menuName="Dungeon/...")]`.
Instances live beside their script (`PlayerDataDto.asset`, `Room_1_2_DataDto.asset`).

- `PlayerDataDto` — health/heal stats. This is *how health survives a scene change*:
  `PlayerHealth` reads and writes `playerData.Health` directly rather than keeping its own
  field, so the value persists across loads. `OnEnable` resets `Health = StartingHealth`.
  Note this means edits persist into the asset in-Editor during play.
- `RoomDataDto` — one asset per gate *pair*: `roomA`/`gateIdA` and `roomB`/`gateIdB`.
  A `RoomGateScript` knows only its own `GateSideEnum`; it resolves destination room and
  gate by picking the opposite side. One asset therefore wires both directions of a door.

**Field style.** `[SerializeField]` on inspector-facing fields, `protected` where a subclass
may need it (`EnemyScript`, `RoomGateScript`, `SceneTransitionManager`). Hero-Knight-derived
code keeps that pack's `m_` prefix (`m_body2d`, `m_animator`); newer scripts don't.

**Input.** `PlayerController` uses the legacy `Input.*` API (`GetAxis("Horizontal")`,
`GetKeyDown("space")`). Input System 1.19 and an `InputSystem_Actions` asset are present but
unused by gameplay; `activeInputHandler: 2` (Both) is what keeps the legacy calls working.
Don't half-migrate one script — either stay on legacy or convert the whole controller.

## Architecture

**Scene transitions** — `SceneTransitionManager` is a `DontDestroyOnLoad` singleton
(`Instance`), spawned by `GameManagerBootstrap` in each scene's `Awake` only if one isn't
already alive, so any scene can be opened directly from the Editor. Flow:

1. Player trigger-enters a `RoomGateScript`.
2. The gate pushes destination `GateIdEnum` + player facing direction into the manager,
   then calls `LoadNextScene(name)`.
3. The manager fades a `CanvasGroup` out, loads async, fades in.
4. On `sceneLoaded`, if `nextGateId != None` it finds the matching gate in the new scene,
   moves the player to that gate's `gateSpawn`, restores facing direction, snaps the camera
   look-ahead, and clears `nextGateId`.

Because arrival depends on a gate with the matching `GateIdEnum` existing in the target
scene, a missing or mismatched gate id silently leaves the player at the scene's authored
position.

**Enemies** — `EnemyScript` is the base: constant horizontal velocity, contact damage on
`OnCollisionStay2D` against the player tag, with `GetContactDamage()` `virtual` for
subclasses to override (see `Enemy/OozlingEnemy.cs`).

**Player** — `PlayerController` (movement, wall-slide/wall-jump, roll, attack combo) and
`PlayerHealth` (damage with i-frames, heal, hold-to-heal coroutine at campfires, death) are
separate components on the same object. Ground and wall detection use child `Sensor_HeroKnight`
objects looked up by name in `Start` via `transform.Find` — renaming `GroundSensor`,
`WallSensor_R1/R2`, `WallSensor_L1/L2` in a prefab breaks the controller at runtime.

## Layout

```
Assets/Scripts/            gameplay code (flat, plus Enums/ Enemy/ PlayerData/ RoomData/)
Assets/Prefabs/            KnightAxios, OozlingEnemy, RoomGate, CampFire, HudCanvas,
                           SceneTransitionManager, BlockFlash, SlideDust
Assets/Scenes/             SampleScene, Scene1, Scene2
Assets/Hero Knight - Pixel Art/   third-party art pack — avoid editing
```

No `.asmdef` files and no tests, though `com.unity.test-framework` is installed.

## Notes

- Commit messages in this repo use a leading `=` (`=add campfire`).
- `Debug.Log` calls are left in gameplay paths (scene fade, heal); they're intentional for
  now — don't strip them as cleanup unless asked.
