# Match3 — Royal Match-Style Puzzle Game

A casual match-3 puzzle game built in Unity 6 with URP 2D, inspired by *Royal Match* and *Toon Blast*. Built as a technical case study with a focus on clean architecture, design patterns, and juicy game-feel.

20 hand-designed levels • 4 cube colours • 3 obstacle types • 4 power-ups with combos • Cascade matching, gravity, animated VFX.

---

## Tech Stack

- **Engine** — Unity `6000.3.9f1` (Unity 6)
- **Render Pipeline** — Universal Render Pipeline (URP 2D)
- **Animation** — [DOTween](http://dotween.demigiant.com/) (via OpenUPM scoped registry)
- **Target** — Portrait `9:16`, iOS / Android
- **Language** — C# 9 with file-scoped namespaces

---

## Features

### Core gameplay
- Tile swap with adjacency validation, match-3/4/5, L/T-shape detection
- Cascading gravity with proper Stone-blocks-column-above rule
- Vase health (2-hit), Box (1-hit), Stone (immune to direct match, breakable by power-ups)
- Goal-driven win/lose conditions inferred from level obstacles

### Power-ups
| Match shape | Power-up |
|---|---|
| 4-in-a-row | Horizontal Rocket |
| 4-in-a-column | Vertical Rocket |
| L / T shape | TNT |
| 5-in-a-line | Light Ball |

### Combos
- **Rocket + Rocket** — clears the intersecting row and column
- **Rocket + TNT** — fires 3 horizontal and 3 vertical rocket beams from the swipe destination
- **TNT + TNT** — clears a 9×9 area
- **Light Ball + Cube** — sequenced highlight animation, then pops every cube of that colour
- **Light Ball + Rocket** — transforms most-common-colour cubes into rockets one-by-one, then fires them all simultaneously
- **Light Ball + TNT** — same flow but cubes become TNTs and all detonate together
- **Light Ball + Light Ball** — clears the entire board

### Juice / VFX
- DOTween-driven swap, fall, land squish, and invalid-swap animations
- Per-type particle bursts (one prefab per cube colour and obstacle type)
- Rocket beam projectiles using separate left/right and up/down sprite halves
- Edge-correct beams (near-edge half flies past the board so both halves finish in sync)
- TNT shockwave ring (`tnt_ring`) on every detonation
- LightBall sequenced highlight pulse (Inspector-tunable pacing)
- Goal icon swap → animated checkmark on objective completion

---

## Architecture

`GridModel`, `MatchFinder`, `SwapValidator`, `GravitySystem`, `ObstacleSystem` are pure C# — no Unity dependency, unit-testable in isolation.

| Pattern | Implementation |
|---|---|
| State Machine | `BoardController` |
| Command | `SwapCommand`, `ActivatePowerUpCommand` |
| Observer | `EventBus` → `GoalTracker`, `MoveCounter`, `LevelUIController` |
| Factory | `TileFactory`, `PowerUpFactory` |
| Strategy | `IActivationStrategy` per power-up |
| Decorator | `ObstacleDecorator` wrapping `ITile` |
| Object Pool | `TilePool`, `ParticlePool` |
| Service Locator | `ServiceLocator` (lightweight, manual registration) |
| Builder | `LevelBuilder` for level construction |

State transitions in `BoardController` always use DOTween `OnComplete` callbacks — never fixed delays. Swipe input is **queued** while the board is processing so no input is lost during cascades.

---

## Project Structure

```
Assets/
├── _Match3/
│   ├── Scripts/
│   │   ├── Core/          ServiceLocator, EventBus, GameBootstrapper, LevelSceneCoordinator
│   │   ├── Data/          LevelData, LevelLoader, SaveSystem
│   │   ├── Grid/          Cell, CellType, GridModel, GridView, TileView, LevelBuilder
│   │   ├── Input/         SwipeHandler
│   │   ├── GameLogic/     BoardController, MatchFinder, SwapValidator,
│   │   │                  GravitySystem, ObstacleSystem, GoalTracker, MoveCounter
│   │   ├── PowerUps/      Strategies, ComboResolver, PowerUpSpawner
│   │   ├── Obstacles/     CubeTile, BoxDecorator, VaseDecorator, StoneDecorator
│   │   ├── Pools/         TilePool, ParticlePool
│   │   ├── UI/            MainMenuController, LevelUIController, GoalEntryUI, TransitionManager
│   │   └── VFX/           JuiceController, RocketBeamVFX, TNTRingVFX, LightBallAnimator,
│   │                      CameraFillBackground
│   ├── Editor/            LevelDebugMenu
│   └── Prefabs/           Tiles, Obstacles, PowerUps, VFX, UI
├── Resources/
│   └── Levels/            level_01.json … level_20.json
└── Scenes/                MainScene.unity, LevelScene.unity
```

---

## How to Run

1. **Clone**
   ```bash
   git clone https://github.com/AlperenGarip/Match3.git
   cd Match3
   ```
2. **Open in Unity Hub** with Unity **`6000.3.9f1`** (or newer 6.x)
3. The first import will pull DOTween via OpenUPM (`Packages/manifest.json` is configured)
4. Open `Assets/Scenes/MainScene.unity`
5. Press **Play**

Sprites in `Assets/Sprites/` are AI-generated (Unity AI Asset Generation — Game UI Essentials 2.0, Gemini 3.1 Flash, GPT Image 1.5 Recolor) in a Candy Crush–adjacent style.

---

## Cell Code Legend

Level JSON files use these codes (parsed in `CellTypeParser.cs`):

| Code | Meaning | Code | Meaning |
|---|---|---|---|
| `r` | Red cube | `bo` | Box obstacle |
| `g` | Green cube | `s` | Stone obstacle |
| `b` | Blue cube | `v` | Vase (2-hit) |
| `y` | Yellow cube | `hro` | Horizontal Rocket |
| `rand` | Random cube | `vro` | Vertical Rocket |
| | | `tnt` | TNT |
| | | `lb` | Light Ball |

---

## Levels

20 levels of progressive difficulty, each with auto-inferred goals (clear all obstacles of each type).

| L | Grid | Moves | Theme | L | Grid | Moves | Theme |
|---|---|---|---|---|---|---|---|
| 1 | 9×10 | 20 | Boxes top rows | 11 | 9×9 | 20 | Box pyramid + TNT |
| 2 | 10×7 | 15 | Stones top rows | 12 | 8×9 | 22 | LightBall + vases |
| 3 | 9×8 | 30 | Vase edges | 13 | 9×8 | 18 | Stone corners |
| 4 | 7×8 | 17 | Stone+Box border | 14 | 7×9 | 25 | Vase checkerboard |
| 5 | 9×9 | 23 | Pre-placed power-ups | 15 | 9×10 | 22 | Stone walls |
| 6 | 8×9 | 23 | Vase rows bottom | 16 | 8×9 | 18 | Power-up showcase |
| 7 | 9×8 | 24 | Stone border | 17 | 10×9 | 20 | Box maze |
| 8 | 9×7 | 18 | Stone corners | 18 | 8×8 | 16 | Vase clusters |
| 9 | 6×9 | 20 | Vase strip | 19 | 9×10 | 20 | Stone fortress |
| 10 | 10×8 | 10 | Box-heavy blue-only | 20 | 10×10 | 24 | Final mix |

---

## Credits

- **Code & design** — Alperen Garip
- **Tweening** — [DOTween](http://dotween.demigiant.com/) by Demigiant
- **AI-generated sprites** — Unity AI Asset Generation (Game UI Essentials 2.0, Gemini 3.1 Flash, GPT Image 1.5 Recolor)

---

## License

[MIT](./LICENSE) for code and configuration. Third-party libraries (DOTween) retain their own licenses.
