# Changelog

All notable changes to Marco's Bodyshop are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/), and this
project adheres to Semantic Versioning.

## [1.0.1] - 2026-10-27

### Added
- German menu text. The menu follows the game language.

### Fixed
- The RV stands in front of the hall in full view of the camera while the menu is open.
- The cash label and the "more below" row are no longer cut off or unstyled.

## [1.0.0] - 2026-10-27

### Added
- Marco's "I'd like to repaint my vehicle" is now "Pimp my ride": two paints, lights, underglow, horns and
  performance parts in one garage menu. The lot in front of the hall takes the RV.
- Nitro: hold Left Ctrl for a boost with a nitrous rush and blue flames at the exhaust. `NitroKey`, `NitroBoost`,
  `NitroSeconds` and `NitroRechargeSeconds` set it.
- Six horns (clown, rubber duck, freight train, fanfare, La Cucaracha, bicycle), steering and weight stages, and
  underglow all around, on the sides only or front and back.
- Every pick shows on the car before you pay, and the camera fades out walls and people in the way. Enter, Space or D
  buys, A or Esc goes back, W/S or the arrow keys browse.
- Co-op: one player uses the menu at a time, the buyer pays, everyone sees the result.
- A radio on Marco's desk plays music while the menu is open, at your music volume. Picking another horn cuts the
  one still sounding.

### For mod authors
- `BodyshopApi` registers garages, bays, categories, entries, prices, unlock gates and camera shots.
- Cars with `ChassisApi.SetPaintTargets` get only custom paint; the stock colours would paint the hidden stock body.
