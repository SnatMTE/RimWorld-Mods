# RimWorld Mods

Small, self-contained mods for RimWorld. Most of them smooth over the rough edges of the Biotech mechanoid systems; the rest patch interactions between other people's mods.

Every folder here follows the same rule: change the smallest part of the vanilla pipeline that needs to change, leave everything else alone, and do not hard-depend on another mod unless the mod is explicitly a patch for it.

## Mods

| Mod | Supported | Requires | What it does |
| --- | --- | --- | --- |
| [Advanced Mech Upgrades](AdvancedMechUpgrades) | 1.7 | Biotech, Harmony | Rebuilds a colony mech into its advanced variant in place for half the gestating cost, or lets the mech upgrade itself over time. |
| [Archotech: Unbreakable Implants](ArchotechNoDestroy) | 1.6, 1.7 | Harmony | Archotech implants break and self-repair instead of being destroyed. |
| [Mech Carrier Fix](MechCarrierFix) | 1.6, 1.7 | Harmony | Restores vanilla hauling so resources reach a mech carrier instead of the mech's own inventory. |
| [Mech Work Types](MechWorkTypes) | 1.6, 1.7 | Biotech | Moves mech repair, mech birthing and subcore encoding out of Smithing into work types of their own. |
| [Mechs Never Die](MechsNeverDie) | 1.6 | Biotech, Harmony | Non-hostile mechanoids are downed instead of killed, so the vanilla repair systems can bring them back. |
| [Quarry Drone Patch](QuarryDronePatch) | 1.6, 1.7 | Quarry [Adopted], Vanilla Quests Expanded - Drone Factory | Lets the Drone Factory miner drone work at Quarry plots. |
| [Yayo Auto-Reload (Standalone)](YayoAutoReload) | 1.6, 1.7 | Harmony, Yayo's Combat 3 (Continued), Yayo's Combat 3 - Addon | Replaces the broken auto-reload regression fix and restores the Addon 1.6.10 reload trigger. |

## What each mod changes

### Advanced Mech Upgrades

Once the research for an advanced mech variant is finished, the regular mech gains an upgrade gizmo. Upgrading consumes half the resources needed to gestate the advanced mech, taken from anywhere on the mech's map, and rebuilds the mech in place, fully repaired. The new mech keeps the old one's name, charge level, drafted state and mechanitor control group, and no gestator is required.

A second gizmo lets the mech upgrade itself: it spends no resources, but shuts itself down for half the time a gestator would need and cannot be used again until the rebuild finishes.

Works with any mod that follows the vanilla naming pattern of `Mech_AdvancedX` for `Mech_X`, such as Advanced Mechanoids by Sobb.

### Archotech: Unbreakable Implants

Archotech implants can never be destroyed. Damage that would destroy one instead breaks it: the body part drops to 0% condition and stops providing its bonus, then archotech self-repair brings it back to 100% over roughly one in-game day. The part carrying the implant can no longer be blown off; damage is capped rather than fatal.

### Mech Carrier Fix

Some mods (Rules of Engagement's `CompArsenal`, for example) add an `IThingHolder` comp to mechs that reports its own container first, hijacking the vanilla destination resolution for anything hauled into the mech. Steel delivered to a war queen then lands in her inventory instead of her carrier, so she can never restock war urchins.

This mod restores vanilla behaviour: when a pawn has a mech carrier, hauling targets the carrier. It does not modify any other mod.

### Mech Work Types

Moves the mech jobs normally performed under Smithing into dedicated work types:

- **Mech repair**: repair damaged mechanoids (mechanitors only)
- **Mech birthing**: gestate new mechanoids and resurrect dead ones at mech gestators and large mech gestators (mechanitors only)
- **Subcores**: encode subcores at the subcore encoder (mechanitors only)

When added to an existing save, the new work types start disabled; enable them in the Work tab for your mechanitor.

### Mechs Never Die

Prevents non-hostile mechanoids from dying. Lethal damage, or a direct kill effect, downs the mech instead so the vanilla Biotech repair systems can restore its injuries and missing parts. Hostile mechs still die normally, and so do mechs on a non-rechargeable power cell such as war urchins once that cell runs dry.

RimWorld kills pawns through several paths, so the patch covers `Pawn_HealthTracker.ShouldBeDead`, `Pawn.Kill` and `HealthUtility.DamageUntilDead`. If `MakeDowned` cannot be found by reflection, the mod logs an error and lets mechs die as normal rather than patching something that cannot work.

### Quarry Drone Patch

Allows the Miner Drone from Vanilla Quests Expanded - Drone Factory to work at quarries from Quarry [Adopted]. Works only on newly built drones. Published on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3797567322).

### Yayo Auto-Reload (Standalone)

Standalone auto-reload fix for Yayo's Combat 3, replacing the broken "Auto Reload Regression Fix" mod (disable that one if both are enabled, since it fails to apply its patches).

It restores the Addon 1.6.10 auto-reload trigger (reload right after the last round is fired), adds a periodic empty-weapon reload retry for undrafted pawns including mechs, and routes mech refills through a virtual reload that spawns no physical ammo. It does not modify any other mod. Load after Harmony, Yayo's Combat 3 (Continued) and the Addon.

## How they are built

- Most mods are C# with Harmony patches that hook the smallest possible method. Mech Work Types and Quarry Drone Patch are XML only.
- Each mod has its own `About/About.xml` with `packageId`, `supportedVersions`, `modDependencies` and `loadAfter` entries, so RimWorld and mod managers can order them correctly.
- Player-facing text lives in `Languages/English/Keyed` so it can be translated.
- The C# projects are SDK-style and target `net472`, `net48` or `net10.0` depending on the mod, matching what each one needs from the game assembly.

## Installing

Subscribe on the Steam Workshop, or copy the mod folder into `RimWorld/Mods` and enable it in the mod list. Load after Harmony and after the mods listed in the table above.

## Mod folder layout

Every top-level folder is a complete mod:

```text
ModName/
  About/About.xml      metadata, dependencies, load order
  Assemblies/          compiled assembly, where the mod has code
  Defs/                XML defs
  Patches/             XML patches
  Languages/           translations
  Source/              C# project
```

## Building

Open the `.csproj` under `Source/` and build it. Each project copies its output into the mod's `Assemblies/` folder after a successful build.

Game and Harmony paths come from MSBuild properties (`RimWorldDir` or `GameDir`, and `HarmonyDir`) that default to my own install, so override them for your machine, for example:

```text
dotnet build -p:RimWorldDir="C:\Program Files (x86)\Steam\steamapps\common\RimWorld"
```
