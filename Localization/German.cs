using System.Collections.Generic;
using DooDesch.Localization;

namespace MarcosBodyshop.Localization
{
    /// <summary>
    /// German translation table. Keys are the English source strings as they appear in code and in the page
    /// (gettext style, see DooDesch.Localization.L10n) - changing an English literal there requires updating its key
    /// here, or that line silently falls back to English.
    /// </summary>
    internal static class German
    {
        internal static void Register()
        {
            L10n.Register("de", new Dictionary<string, string>
            {
                // Categories
                ["Paint"] = "Lack",
                ["Second Paint"] = "Zweitlack",
                ["Lights & Looks"] = "Licht & Optik",
                ["Horn"] = "Hupe",
                ["Engine"] = "Motor",
                ["Brakes"] = "Bremsen",
                ["Transmission"] = "Getriebe",
                ["Suspension"] = "Fahrwerk",
                ["Tyres"] = "Reifen",
                ["Steering"] = "Lenkung",
                ["Weight"] = "Gewicht",

                // Category notes and shared entries
                ["Stock"] = "Serie",
                ["Custom"] = "Individuell",
                ["None"] = "Ohne",
                ["{0} fitted"] = "{0} eingebaut",
                ["Back to stock. Free, no refund."] = "Zurück zur Serie. Gratis, keine Erstattung.",

                // Paint
                ["Factory"] = "Werksfarben",
                ["Metallic"] = "Metallic",
                ["Matte"] = "Matt",
                ["Chrome"] = "Chrom",
                ["Factory paint."] = "Werkslack.",
                ["Factory Paint"] = "Werkslack",
                ["Back to the factory colour. Free, no refund."] = "Zurück zur Werksfarbe. Gratis, keine Erstattung.",
                ["Metallic paint."] = "Metalliclack.",
                ["Matte paint."] = "Mattlack.",
                ["Chrome paint."] = "Chromlack.",

                // Colours (paint, lights, rims)
                ["Black"] = "Schwarz",
                ["Graphite"] = "Graphit",
                ["Silver"] = "Silber",
                ["White"] = "Weiß",
                ["Yellow"] = "Gelb",
                ["Red"] = "Rot",
                ["Wine"] = "Weinrot",
                ["Purple"] = "Lila",
                ["Navy"] = "Marineblau",
                ["Blue"] = "Blau",
                ["Sky Blue"] = "Himmelblau",
                ["Ice Blue"] = "Eisblau",
                ["Teal"] = "Petrol",
                ["Lime"] = "Limette",
                ["Forest"] = "Waldgrün",
                ["Green"] = "Grün",
                ["Gold Chrome"] = "Goldchrom",
                ["Rose Chrome"] = "Roséchrom",
                ["Black Chrome"] = "Schwarzchrom",

                // Lights & Looks
                ["Xenon Headlights"] = "Xenon-Scheinwerfer",
                ["Coloured headlight glow."] = "Farbiges Scheinwerferlicht.",
                ["Underglow"] = "Unterboden-Neon",
                ["Neon under the body."] = "Neon unter der Karosserie.",
                ["Underglow Sides"] = "Unterboden-Neon: Seiten",
                ["All Four Sides"] = "Rundum",
                ["Sides Only"] = "Nur die Seiten",
                ["Front and Back"] = "Vorne und hinten",
                ["Neon all around the body."] = "Neon rund um die Karosserie.",
                ["Neon along both sides."] = "Neon an beiden Seiten.",
                ["Neon under the front and the back."] = "Neon unter Front und Heck.",
                ["Fit underglow first."] = "Bau zuerst Unterboden-Neon ein.",
                ["Window Tint"] = "Scheibentönung",
                ["Light Smoke"] = "Leicht getönt",
                ["Dark Smoke"] = "Dunkel getönt",
                ["Limo"] = "Limousine",
                ["Darker glass."] = "Dunkleres Glas.",
                ["Rim Colour"] = "Felgenfarbe",
                ["Painted rims."] = "Lackierte Felgen.",
                ["Tyre Smoke"] = "Reifenqualm",
                ["Coloured drift smoke."] = "Farbiger Driftqualm.",

                // Horns
                ["Two-Tone Horn"] = "Zweiklanghupe",
                ["Air Horn"] = "Drucklufthupe",
                ["Short Beep"] = "Kurzer Piep",
                ["Euro Fanfare"] = "Europa-Fanfare",
                ["Clown Horn"] = "Clownshupe",
                ["Rubber Duck"] = "Quietscheente",
                ["Freight Train"] = "Güterzug",
                ["Bicycle Horn"] = "Fahrradhupe",
                ["Hold G to honk."] = "Halte G zum Hupen.",

                // Performance
                ["Engine upgrade."] = "Motortuning.",
                ["Brake upgrade."] = "Bessere Bremsen.",
                ["Transmission upgrade."] = "Besseres Getriebe.",
                ["Suspension upgrade."] = "Besseres Fahrwerk.",
                ["Turbo upgrade."] = "Turbotuning.",
                ["Tyre upgrade."] = "Bessere Reifen.",
                ["Steering upgrade."] = "Bessere Lenkung.",
                ["Less weight, quicker off the line."] = "Weniger Gewicht, schneller vom Fleck.",
                ["Quick Steering Rack"] = "Direkte Lenkung",
                ["Race Steering Rack"] = "Rennlenkung",
                ["Lightweight Panels"] = "Leichtbau-Verkleidung",
                ["Carbon Body"] = "Carbon-Karosserie",
                ["Nitrous Kit"] = "Lachgas-Kit",
                ["Hold {0} for nitro. The tank refills by itself."] = "Halte {0} für Nitro. Der Tank füllt sich von selbst wieder auf.",
                ["Left Ctrl"] = "Strg links",
                ["Right Ctrl"] = "Strg rechts",
                ["Left Shift"] = "Umschalt links",
                ["Left Alt"] = "Alt links",

                // Stat bars
                ["Speed"] = "Tempo",
                ["Acceleration"] = "Beschleunigung",
                ["Braking"] = "Bremsen",
                ["Handling"] = "Handling",

                // Marco
                ["Pimp my ride"] = "Motz meine Karre auf",
                ["Thanks buddy"] = "Danke, Kumpel",
                ["Someone is in the vehicle"] = "Jemand sitzt im Fahrzeug",
                ["Vehicle must be parked inside the shop"] = "Das Fahrzeug muss in der Werkstatt stehen",
                ["There is no garage '{0}'."] = "Es gibt keine Werkstatt '{0}'.",

                // Opening and buying
                ["The menu is already open."] = "Das Menü ist schon offen.",
                ["The vehicle is not ready yet."] = "Das Fahrzeug ist noch nicht bereit.",
                ["The player is not ready yet."] = "Der Spieler ist noch nicht bereit.",
                ["Sideload 1.35.0 or newer is missing."] = "Sideload 1.35.0 oder neuer fehlt.",
                ["The menu could not open. Details are in MelonLoader/Latest.log."] = "Das Menü konnte nicht öffnen. Details stehen in MelonLoader/Latest.log.",
                ["The menu could not load. Details are in MelonLoader/Latest.log."] = "Das Menü konnte nicht laden. Details stehen in MelonLoader/Latest.log.",
                ["The vehicle moved."] = "Das Fahrzeug wurde bewegt.",
                ["One moment."] = "Einen Moment.",
                ["Not available."] = "Nicht verfügbar.",
                ["Already installed."] = "Schon eingebaut.",
                ["Not enough money."] = "Nicht genug Geld.",
                ["The garage refused."] = "Die Werkstatt hat abgelehnt.",
                ["{0} fitted."] = "{0} eingebaut.",
                ["{0} removed."] = "{0} ausgebaut.",
                ["{0} installed."] = "{0} eingebaut.",

                // Refusals the host sends in English; the buyer translates them on receipt (Net.Sync.Heard).
                ["There is no garage here."] = "Hier gibt es keine Werkstatt.",
                ["{0} is using the garage."] = "{0} benutzt gerade die Werkstatt.",
                ["Another player"] = "Ein anderer Spieler",
                ["The vehicle is gone."] = "Das Fahrzeug ist weg.",
                ["The garage is not yours right now."] = "Die Werkstatt gehört gerade nicht dir.",
                ["This category is not installed on the host."] = "Diese Kategorie ist beim Host nicht installiert.",
                ["This category does not fit the vehicle."] = "Diese Kategorie passt nicht zum Fahrzeug.",
                ["This part is not available on the host."] = "Dieses Teil gibt es beim Host nicht.",
                ["The price changed to {0}."] = "Der Preis hat sich auf {0} geändert.",
                ["This part cannot be fitted."] = "Dieses Teil kann nicht eingebaut werden.",
                ["The garage could not do it."] = "Die Werkstatt hat es nicht geschafft.",
                ["The garage could not fit it."] = "Die Werkstatt konnte es nicht einbauen.",
                ["The paint was refused."] = "Der Lack wurde abgelehnt.",
                ["The part does not fit."] = "Das Teil passt nicht.",

                // The page (sent to it with the root model, Shop.Model.PageText)
                ["Browse"] = "Blättern",
                ["Buy"] = "Kaufen",
                ["Back"] = "Zurück",
                ["Nothing here"] = "Hier ist nichts",
                ["more above"] = "weiter oben",
                ["more below"] = "weiter unten",
                ["Fitted"] = "Eingebaut",
                ["Removed"] = "Ausgebaut",
                ["Free"] = "Gratis",
                ["Installed"] = "Eingebaut",
                ["Unlocked. Removing and refitting is free."] = "Freigeschaltet. Aus- und Wiedereinbau sind gratis.",
                ["Pay once to unlock. Removing and refitting is free after that."] =
                    "Einmal zahlen zum Freischalten. Danach sind Aus- und Wiedereinbau gratis.",
                ["The garage sent bad data."] = "Die Werkstatt hat fehlerhafte Daten geschickt.",
            });
        }
    }
}
