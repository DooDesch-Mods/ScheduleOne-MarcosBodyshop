# Marco's Bodyshop - A Tuning Garage For Schedule I

> 🛟 **Need help or found a bug?** Get support at [support.doodesch.de/marcosbodyshop](https://support.doodesch.de/marcosbodyshop).

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![Game](https://img.shields.io/badge/game-Schedule%20I-purple)
![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.3+-green)
![Status](https://img.shields.io/badge/status-beta-yellow)
[![Polyfill](https://polyfill.doomods.com/badge/marcosbodyshop.svg)](https://polyfill.doomods.com/mod/marcosbodyshop)

![Marco's Bodyshop](https://raw.githubusercontent.com/DooDesch-Mods/ScheduleOne-MarcosBodyshop/main/docs/img/header.jpg)

> Marco does more than paint now.

## What it does

Park any car in Marco's shop at the gas station and pick **Modify my vehicle** in his dialogue. A menu opens
over the car, the camera moves to the part you look at, and the car shows each choice before you pay.

- **Paint** - the game's colours, plus metallic, matte and chrome.
- **Second Paint** - the trim in its own colour, on vehicles whose model has one (the RV of RV Repair Van).
- **Lights & Looks** - xenon headlights, underglow, window tint, rim colour and tyre smoke, each in colours.
- **Engine, Brakes, Transmission, Suspension** - three or four stages each, with bars for speed,
  acceleration, braking and handling.
- **Turbo** and **Drift Tyres**.

Stock is always free. A part you replace is not refunded.

A vehicle too big for the hall parks on the lot in front of it.

Browse with W/S or the arrow keys, buy with Space or Enter, or use the mouse. Escape goes back.

## Settings

Every price is in `UserData/MelonPreferences.cfg` under `[MarcosBodyshop]`, one `Price_<id>` line per item.

## Multiplayer

Everyone needs the mod. One player at a time uses Marco's garage. The buyer pays, and the host fits the part
on the car, so every player sees it.

## For mod authors

`MarcosBodyshop.Api.BodyshopApi` adds categories and entries to the menu, gates the built-in ones per vehicle,
and registers garages and bays of your own. RV Repair Van uses it for the RV's interior and workshop.

```csharp
BodyshopApi.RegisterCategory(new BodyshopCategory
{
    Id = "mymod.horns", Label = "Horns", Icon = "wrench", Order = 900,
    Entries = v => MyHorns.All.Select(h => new BodyshopEntry
    {
        Id = h.Id, Label = h.Name, Price = 200,
        Installed = car => MyHorns.Fitted(car) == h.Id,
        Apply = car => MyHorns.Fit(car, h.Id),   // runs on the host; null = done, text = refused
    }),
});
```

## Uninstalling

The tuning is saved by Chassis and stays on the cars without this mod.

## Screenshots

| | |
|---|---|
| ![Paint](https://raw.githubusercontent.com/DooDesch-Mods/ScheduleOne-MarcosBodyshop/main/docs/img/paint.jpg) | ![Lights & Looks](https://raw.githubusercontent.com/DooDesch-Mods/ScheduleOne-MarcosBodyshop/main/docs/img/looks.jpg) |
| ![Engine stages with stat bars](https://raw.githubusercontent.com/DooDesch-Mods/ScheduleOne-MarcosBodyshop/main/docs/img/engine.jpg) | ![The garage menu](https://raw.githubusercontent.com/DooDesch-Mods/ScheduleOne-MarcosBodyshop/main/docs/img/menu.jpg) |
| ![The RV on Marco's lot](https://raw.githubusercontent.com/DooDesch-Mods/ScheduleOne-MarcosBodyshop/main/docs/img/rv.jpg) | ![The RV's interior parts](https://raw.githubusercontent.com/DooDesch-Mods/ScheduleOne-MarcosBodyshop/main/docs/img/interior.jpg) |

## Requirements

- [MelonLoader](https://melonwiki.xyz/) 0.7.3 or newer
- [S1API](https://thunderstore.io/c/schedule-i/p/ifBars/S1API_Forked/) 3.2.1 or newer
- [Chassis](https://thunderstore.io/c/schedule-i/p/DooDesch/Chassis/) 1.7.0 or newer
- [Sideload](https://thunderstore.io/c/schedule-i/p/DooDesch/Sideload/) 1.35.0 or newer

## Credits

Built by DooDesch on [S1API](https://github.com/ifBars/S1API) by ifBars.
Menu icons from [Lucide](https://lucide.dev), licensed [ISC](https://lucide.dev/license).
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) has every part with its license text.

## License

DooDesch, see [LICENSE.md](LICENSE.md). Forks, changes and pull requests are welcome. Publishing or reuploading
the mod or a changed copy needs written permission.
