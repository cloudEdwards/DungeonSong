# Creating Attacks, Spells, Tools and Items

Recipes for adding player content. Almost none of it requires C#.

> **Rule of thumb:** if you are about to edit `PlayerController.cs`, `PlayerActor.cs` or
> `PlayerCombat.cs` to add content, stop — there is probably an asset that does it. Those
> files change when the *framework* gains a capability, not when the *game* gains content.

**Menu paths** are all under `Create ▸ Dungeon ▸ Player ▸ …` in the Project window.

---

## 1. A new attack

**Example: an upward holy thrust.**

1. **Create ▸ Dungeon ▸ Player ▸ Attack Definition**, name it `Attack_HolyThrust`.
2. Fill it in:

| Field | Meaning |
| --- | --- |
| **Direction** | Which input produces it: `Forward`, `Up`, `Down`, `DiagonalUpForward`, `DiagonalDownForward`, `Wall` |
| **Allowed Context** | `Grounded`, `Airborne`, `OnWall`, or a combination. An air-only pogo is `Airborne` alone |
| **Priority** | Higher wins when two attacks match the same direction and context |
| **Startup / Active / Recovery** | The tell, the damaging frames, the punish window. **Never set Startup to 0** |
| **Cooldown** | Per-attack, on top of the combat component's global cooldown |
| **Payload** | Damage, poise damage, damage type, knockback, hit-stop |
| **Hitbox Key** | Which hitbox to open — must match a key in the player's `HitboxDirectory` |
| **Animation Key** | Semantic key, resolved by `PlayerAnimatorAdapter` |
| **Cost** | Optional resource cost. Leave the resource empty for a free swing |

3. Add it to the **Attacks** list on the player's `PlayerCombat` component.
4. Done. If you reused an existing hitbox key and animation key, there is nothing else to do.

### Adding a new hitbox
Only needed for a direction the rig does not cover yet:

1. Add a child object to the player with a trigger `BoxCollider2D` and a `Hitbox` component.
2. Set the `Hitbox`'s **Key** (e.g. `DiagonalUp`) and **Target Teams** to `Enemy`.
3. Add a row to the player's `HitboxDirectory`: key, the hitbox, and **Mirror With Facing**
   (on for sideways boxes, off for symmetrical up/down ones).

Author hitboxes on the **right-hand side** (positive local X). The directory mirrors them.

### Combos
Set **Follow Up** to the next attack and **Combo Window** to how long the player has to
press again. The follow-up needs **Combo Index** ≥ 1 so it can only be reached through the
chain, never selected cold. The stock 3-hit chain is `Attack_Forward1 → 2 → 3`.

### Pogo / recoil
Set **Recoil On Hit** (upward velocity on connect) and leave **Recoil Requires Airborne**
ticked. That is the whole pogo — see `Attack_AirDown`.

---

## 2. A new spell

Spells are abilities. There is no spell system.

**Example: Eldritch Spear — a piercing projectile.**

1. **Create ▸ Dungeon ▸ Combat ▸ Projectile Definition**, `Projectile_EldritchSpear`.
   Set motion, speed, damage, **Pierce Count: 3**, and point **Prefab** at a projectile
   prefab (copy `Assets/Enemies/Prefabs/POC_Projectile.prefab`).
2. **Create ▸ Dungeon ▸ Player ▸ Ability Definition**, `Ability_EldritchSpear`:

| Field | Meaning |
| --- | --- |
| **Id** | Stable string for saves and loadouts. Do not change once players have saves |
| **Category** | `Spell`, `Tool`, `Healing`… for UI and progression only |
| **Tags** | Free-form: `warlock`, `eldritch`, `ranged` |
| **Cost** | Resource + amount. Leave empty for a cantrip. A spell slot is `{Resource_WarlockSlots, 1}` or `{Resource_PaladinSlots, 1}` |
| **Spend Cost On Resolve** | Charge the cost when the ability takes effect, so an interrupted cast is free |
| **Cooldown** | Seconds |
| **Targeting** | `Self`, `Direction`, `NearestEnemy`, `PointInFront` |
| **Cast Time / Recovery** | Wind-up and commitment |
| **Lock Movement** | Root the player while casting |
| **Suspend In Air** | Cast in the air, hang in place (gravity off) until the ability finishes |
| **Effects** | Applied on resolve. Leave empty when a projectile carries the damage |
| **Requirements** | Conditions that must hold (see §5) |
| **Animation Key** | Semantic key |

3. On the player, add a **`ProjectileAbility`** component. Set its **Definition** and
   **Projectile**, and point **Muzzle** at the `Muzzle` child.
4. Add the component to a slot on **`AbilityLoadout`**.
5. Bind a key: add an `AbilityN` action with its binding to the `Player` map in
   `Assets/InputSystem_Actions.inputactions`, and add its name to **Ability Action Names** on
   the knight's `InputSystemSource` at the same index as the loadout slot.

The HUD picks it up automatically — icon, key label, cooldown sweep and greying out all come
from the definition.

### A spell with no projectile
Use **`EffectAbility`** instead and put the consequences in **Effects**. That is all Cure
Wounds is: `EffectAbility` + `HealEffect` + `Self` targeting.

### An area burst in front of the player
Use **`HitboxBurstAbility`** and point **Burst Prefab** at a prefab with a `Hitbox` and a
trigger collider, authored facing right. That is Burning Hands:
`Assets/Prefabs/BurningHandsCone.prefab`. Damage goes through the normal hitbox path.

### A buff on the player's sword
Implement `IAttackModifier` and register with `PlayerCombat.AddAttackModifier`; it can add
damage and damage types to each swing before the hitbox opens. `SmiteAbility` is the example.
Override `ActiveStacks` to show a counter in the ability slot.

### A spell that needs genuinely new mechanics
Only then write code: subclass `AbilityBehaviour` and implement `OnResolve`. Cost, cooldown,
charges, cast timing, movement lock, animation and requirements are all handled for you.

```csharp
public class TeleportAbility : AbilityBehaviour
{
    protected override void OnResolve(in TargetInfo target)
    {
        Owner.Motion.SetVelocity(Vector2.zero);
        Owner.transform.position = target.Position;
    }
}
```

---

## 3. A new tool

A tool **is** an ability with `Category: Tool` and usually limited charges.

1. Create the `AbilityDefinition` as above; set **Category** to `Tool` and **Max Charges**
   to the number of uses between rests.
2. Pick an executor: `ProjectileAbility` for a thrown tool, `EffectAbility` for a buff or a
   trap, or a new subclass for something exotic.
3. Charges refill automatically at campfires — `Campfire` calls
   `AbilityLoadout.RestoreAllCharges()`.

The HUD shows the remaining count in the slot corner without any extra work.

---

## 4. A new effect

Effects are the reusable consequences abilities are assembled from.

Stock effects: `DamageEffect`, `HealEffect`
(**Create ▸ Dungeon ▸ Player ▸ Effects ▸ …**).

Write a new one only for a genuinely new *kind* of consequence:

```csharp
[CreateAssetMenu(menuName = "Dungeon/Player/Effects/Knockback")]
public class KnockbackEffect : GameplayEffect
{
    public float Force = 10f;

    public override void Apply(in EffectContext context)
    {
        if (context.Target == null) { return; }

        var receiver = context.Target.GetComponentInParent<IKnockbackReceiver>();
        receiver?.ApplyKnockback(context.Direction, Force, 0.2f);
    }
}
```

Drop it into any ability's **Effects** list. One effect, every ability.

---

## 5. A new requirement (gating)

Requirements answer "can this be used right now?" — the campfire-only heal is one.

```csharp
[CreateAssetMenu(menuName = "Dungeon/Player/Requirements/Grounded")]
public class RequireGrounded : AbilityRequirement
{
    public override bool IsSatisfied(PlayerActor player) => player.Motion?.IsGrounded ?? false;
}
```

Create the asset, set its **Unmet Message**, and add it to an ability's **Requirements**.
`AreRequirementsMet(out string reason)` gives UI the message; the ability bar greys the slot
out automatically.

Existing: `Require_NearCampfire` (`RequireNearRestPoint`) — currently unused since Cure Wounds went to Paladin slots.

---

## 6. A new resource

1. **Create ▸ Dungeon ▸ Player ▸ Resource Definition** — max, starting amount, regen rate
   and delay, which rests refill it (**Refilled By**), and whether death empties it.
   The **Id** keys the session and save values; do not change it once players have saves.
2. Add it to the **Resources** list on the player's `ResourcePool`.
3. Reference it from any ability's **Cost**.
4. For a HUD bar: duplicate the `LoyaltyBar` object under `HudCanvas` and point its
   `ResourceView` at the new definition. For whole units such as spell slots, duplicate
   `WarlockSlots` instead — **Show As Pips** renders them as ◆◇.
5. To fill it from combat, add a `ResourceOnHit` to the player, as Loyalty does.

Runtime values live on `ResourcePool`, never in the asset — the asset is shared configuration.

---

## 7. A new rest point

`Campfire` is one implementation of `IRestPoint`. For a shrine or a bed:

1. Add a trigger `Collider2D` and a `Campfire` component (or write a new `IRestPoint` +
   `IInteractable`), and give it a **unique Rest Point Id** — it goes in the save.
2. Set the **Interaction Prompt** verb; the HUD renders "Press {key} to {verb}".
3. Choose what resting restores: health, resources (everything whose **Refilled By**
   includes `Long`), ability charges, whether it saves.

To react to resting from elsewhere, listen rather than editing the campfire:

```csharp
RestEvents.PlayerRested += (player, restPoint) => RespawnMyEnemies();
```

---

## 8. Items (not built yet)

There is no inventory. The hooks that exist:

- `AbilityDefinition.MaxCharges` — consumable-style limited uses
- `AbilityCategory.Tool` — classification
- `AbilityLoadout.Equip(slot, ability)` — runtime equipping, ready for pickups

An item system should grant or unlock abilities through `Equip`, not introduce a parallel
"item effect" pipeline — items should produce abilities, and abilities already produce effects.

---

## 9. A new HUD element

1. Add a child object under `HudCanvas`.
2. Add a component deriving from `HudView`.
3. Override `OnBind` to subscribe, `OnUnbind` to unsubscribe, `Tick` for continuous values.

`PlayerHud` finds and binds every `HudView` beneath it automatically. The HUD only ever
reads — never call gameplay from a view.

---

## 10. Hit feedback for a new enemy

1. Set the enemy's sprite material to `Assets/Materials/SpriteFlash.mat`.
2. Add a `HitFeedback` component; point **Health Source** at its `EnemyHealth`.
3. Set **Flash Colour/Duration** and **Particle Colour** (green for ooze; red for flesh).
4. Optionally point **Hit Particles** at a different burst prefab.

It works on the player too — `HitFeedback` listens to `IHealth`, which both sides implement.

---

## Troubleshooting

| Symptom | Likely cause |
| --- | --- |
| Attack does nothing | No attack matches that direction + context. Check **Allowed Context**, and that it is in `PlayerCombat`'s list |
| Attack swings but deals no damage | **Hitbox Key** does not match a key in `HitboxDirectory` (logs a warning), or the hitbox's **Target Teams** is wrong |
| Attack hits behind the player | Hitbox authored at negative local X, or **Mirror With Facing** unticked on a sideways box |
| Combo never chains | **Follow Up** unset, **Combo Window** 0, or the follow-up's **Combo Index** is 0 |
| Ability does nothing on keypress | Not in an `AbilityLoadout` slot, or the key index does not match the slot index |
| Ability greyed out | Cooldown, unaffordable cost, no charges, or an unmet requirement — `AreRequirementsMet` says which |
| Spell costs nothing | **Cost** has no resource assigned — that is how cantrips are made |
| Slots refill when changing rooms | A new resource is missing from `ResourcePool`, or two resources share an **Id** |
| Prompt shows the wrong key | Labels come from the bindings in `InputSystem_Actions` (Keyboard&Mouse group); check the binding there |
| Enemy does not flash | Sprite material is not `SpriteFlash.mat`, or `HitFeedback` has no health source |
| Player is stuck unable to move | A movement lock leaked. Locks are reference-counted and released on cancel/death; `PlayerActor.ClearMovementLocks()` is the escape hatch |
