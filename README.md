# BONK BRAWL

A party platform fighter for the browser, made in Unity 6 WebGL and hosted on Vercel. Up to 4 brawlers fight on the same stage, weapons fall from the sky, and the online mode uses **rollback netcode** so fights feel local even when the connection isn't.

## Why this design (2026 trends)
- **Platform fighters are still growing, and they win on accessibility.** Brawlhalla is still pulling [10k+ concurrent Steam players](https://steamplayercount.com/app/291550) in 2026, and Rivals of Aether II's big 2026 update was the ["Fun For All" update](https://steamcommunity.com/games/2217000/announcements/detail/490469454386301033). It added casual stages and **items** to reach a wider audience. BONK BRAWL is built casual-first: simple inputs, items on by default, 3 stocks.
- **Party chaos and "friendslop"** (Party Animals, Gang Beasts, Stick Fight) are hugely shareable. That's why there's a big, silly hit feel (screen shake, hitstop, tumbling launches, "BONK!" pop-ups) and couch 2-player mode.
- **Rollback netcode is now expected** for fighters (see [GGPO-style browser rollback](https://github.com/genxium/DelayNoMore)). A WebSocket relay is the [2026 default for browser multiplayer](https://app.cinevva.com/guides/multiplayer-browser-game), so there is no WebRTC NAT pain.
- **Instant browser play is the growth channel.** There's no install, it runs on phones with touch controls, there are invite links, and a share card.

## How to play
- Hits raise **damage %**, and the higher it is the further a fighter flies. Knock rivals past the edge of the screen to take a **stock**. Last one standing wins.
- **Attack + direction** gives a different move neutral, sideways, up and down, both on the ground and in the air.
- **Heavy** attacks can be held to charge. **Up + Heavy** is your recovery, and **Down + Heavy** in the air is a meteor stomp.
- You get **three jumps** (one ground jump and two in the air). **Dodge** makes you invincible for a moment and works both on the ground and in the air.
- **Weapons fall from the sky.** Press **Throw** to pick one up, or to throw the one you're holding:
  - Sword
  - Spear
  - Frying pan (it goes *BONK*)
  - Blaster (Light fires shots, Heavy fires a charged blast)
  - Grenade

| | Keys (P1) | Keys (P2, local) | Gamepad | Touch |
|---|---|---|---|---|
| Move | WASD / arrows | arrows | stick / d-pad | left-side joystick |
| Jump | Space | Right Shift | A | JUMP |
| Attack | J / Z | . | X | ATTACK |
| Heavy | K / X | / | B | HEAVY |
| Dodge | L / Shift / C | , | bumpers / triggers | DODGE |
| Throw | H / E / V | ; | Y | THROW |

**Roster.** There are 10 crossover fighters from the whole collection. They come in Light, Medium and Heavy weight classes, with different weight, speed, gravity and jump.

**Stages.** There are 4, one from each game:
- Neon Rooftop (City Rush)
- Moonlit Crypt (Grave Shift)
- Moon Base (Space Diner). It has low gravity.
- The Pass (Order Up!)

## Tech
- `Sim.cs` is the **deterministic simulation**. It uses integers only (millimetres, 60 Hz frames, lookup-table trig and a seeded xorshift RNG), so every browser computes bit-identical fights. The CPU AI lives inside the sim, so bots stay in sync online too.
- `Rollback.cs` is a **GGPO-style session**:
  - Local input is delayed by 2 frames.
  - Remote inputs are predicted.
  - States are saved every frame for a 64-frame ring.
  - When a late input differs from the prediction, it rewinds and resimulates.
  - It also handles time-sync throttling, stall protection, and periodic **state-hash desync checks**.
  - If a player leaves, a CPU takes over their fighter on every screen at the same frame.
- `View.cs` / `StageArt.cs` / `UI.cs` / `Sfx.cs` handle rendering, particles, camera, HUD and synthesized audio. They only read the sim, so rollbacks simply re-render.
- **Server** (`orbyt/server/brawl.js`, on the shared Railway service): rooms (quick match / private code / invite link `?brawl=CODE`, host picks stage and CPUs), a relay for input and hash packets tagged with the sender's slot, rematch votes, and an online-wins leaderboard. A win only counts when every player in the match reports the same winner.

**Verification.** Two browsers played through a relay with about 110 ms of lag plus jitter. Over 6,600+ frames the state hashes were identical at every checkpoint, with 0 desyncs and 0 stalls, and there were hundreds of rollbacks of up to 9 frames each.

## Build and deploy
```bash
"C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -nographics -projectPath unity -executeMethod BonkBuild.WebGL -quit -logFile build.log
npx vercel --prod
```
Dev URL flags (they need `dev=1`):
- `autodrive=1`: a random player drives you, for online soak tests.
- `stage=N`: pick the stage.
- `api=http://localhost:8787`: use a local relay.
- `fresh=1`: reset your save.

The public `bot=1` flag is an all-CPU attract mode.

Assets: Kenney Mini Characters, Graveyard Kit, Mini Dungeon, Blaster Kit, Food Kit, City Kit, Space Kit and Furniture Kit (all CC0).
