# Nuclear Option — Active Decoy Countermeasure Mod

A BepInEx mod for [Nuclear Option](https://store.steampowered.com/app/2220150/Nuclear_Option/) that adds AN/ALQ-260(V)1 style expendable active decoys as a countermeasure against radar-guided missiles (ARH/SARH).

## How It Works

Active decoys are DRFM (Digital Radio Frequency Memory) expendables that replay the radar pulse with a stronger return than the launching aircraft, causing radar-guided missiles to retarget onto the decoy.

- Decoys are selected via the countermeasure cycle (same as flares/jammers)
- Each aircraft carries decoys matching its flare count
- Decoys are most effective when the aircraft is **notching** (flying perpendicular to the missile) or **turning away** with radar off
- Flying toward the missile or having radar active reduces effectiveness

## Balance

Two independent effectiveness penalties (configurable):
- **Radar active**: 0.5x effectiveness — radiating makes the decoy less convincing
- **Heading toward missile**: 0.5x effectiveness — decoy is behind you, less effective
- **Both**: 0.25x — worst case, decoy must overcome 4x threshold
- **Notching with radar off**: 1.0x — full effectiveness

## Compatibility

- **ActualStealth mod** (`com.nuclearoption.rcsmod`): Fully compatible. Decoys use the original pre-stealth RCS to generate their false return, so stealth aircraft get realistic decoy behavior.

## Configuration

Config file: `BepInEx/config/com.nuclearoption.activedecoy.cfg`

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableActiveDecoy` | `true` | Master on/off switch |
| `PenaltyMultiplier` | `0.25` | Combined effectiveness when both penalties active (0.0–1.0) |

## Installation

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx) for Nuclear Option
2. Drop `NuclearOptionActiveDecoy.dll` into `BepInEx/plugins/`
3. Launch the game

## Building from Source

Requires .NET SDK and Nuclear Option installed at the path specified in the `.csproj`.

```
dotnet build src/NuclearOptionActiveDecoy/NuclearOptionActiveDecoy.csproj
```

The build automatically copies the DLL to the game's `BepInEx/plugins/` folder.

## License

GPLv3
