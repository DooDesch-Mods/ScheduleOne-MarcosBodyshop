// Copyright (c) 2026 DooDesch. DooDesch API License (see LICENSE.md): you may copy this file unchanged.
using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Shop;

namespace MarcosBodyshop.Api
{
    /// <summary>
    /// Marco's Bodyshop for other mods: garages, categories and entries of your own, and rules for the built-in ones.
    /// Register the same things on every machine (in OnInitializeMelon or when the world loads); a purchase is
    /// found again on the host by garage, category and entry id.
    ///
    /// The flow: the player opens the menu for the vehicle in a bay. Picking an entry previews it on the player's
    /// machine. Buying takes the price from the buyer's bank balance, the host runs <see cref="BodyshopEntry.Apply"/> and
    /// syncs it, and the buyer gets the money back when the host refuses. Leaving the menu ends every preview. One
    /// player at a time uses a garage.
    /// </summary>
    public static class BodyshopApi
    {
        /// <summary>Marco's garage at the gas station: the hall and the yard bay in front of it.</summary>
        public const string MarcoGarage = "marco";

        /// <summary>Raised on the buyer's machine after the host applied a purchase: vehicle, category id, entry id.</summary>
        public static event Action<LandVehicle, string, string> Purchased;

        /// <summary>Raised on the player's machine when the menu opens (true) and closes (false).</summary>
        public static event Action<LandVehicle, bool> MenuChanged;

        /// <summary>Adds or replaces a garage.</summary>
        public static void RegisterGarage(BodyshopGarage garage) => Registry.AddGarage(garage);

        /// <summary>The garage with this id, or null.</summary>
        public static BodyshopGarage Garage(string id) => Registry.Garage(id);

        /// <summary>Adds a bay to a garage, e.g. a big bay for a vehicle that does not fit the hall.</summary>
        public static bool AddBay(string garageId, BodyshopBay bay) => Registry.AddBay(garageId, bay);

        /// <summary>Adds or replaces a category.</summary>
        public static void RegisterCategory(BodyshopCategory category) => Registry.AddCategory(category);

        /// <summary>Removes a category, built-in ones included.</summary>
        public static bool RemoveCategory(string id) => Registry.RemoveCategory(id);

        /// <summary>The category with this id, or null.</summary>
        public static BodyshopCategory Category(string id) => Registry.Category(id);

        /// <summary>
        /// A rule for a category of any mod, built-in ones included. It returns null to leave the category as it
        /// is, "" to hide it for this vehicle, or a reason to show it locked. Rules run in the order they were added.
        /// </summary>
        public static void Gate(string categoryId, Func<LandVehicle, string> rule) => Registry.AddGate(categoryId, rule);

        /// <summary>The vehicle in a bay of the garage that the menu would open for, or null with the reason.</summary>
        public static LandVehicle VehicleIn(string garageId, out string reason) => Registry.VehicleIn(garageId, out reason, out _);

        /// <summary>
        /// Opens the menu for the vehicle in a bay of this garage, for the local player. Returns false with the
        /// reason when no vehicle is there, someone sits in it, or another player uses the garage.
        /// </summary>
        public static bool Open(string garageId, out string reason) => Session.Request(garageId, out reason);

        /// <summary>Whether the local player has the menu open.</summary>
        public static bool IsOpen => Session.IsOpen;

        /// <summary>Closes the menu of the local player.</summary>
        public static void Close() => Session.Close("closed by a mod");

        internal static void RaisePurchased(LandVehicle v, string category, string entry)
        {
            if (Purchased == null) return;
            try { Purchased(v, category, entry); }
            catch (Exception e) { Core.Log.Error("[Api] a Purchased handler failed: " + e); }
        }

        internal static void RaiseMenu(LandVehicle v, bool open)
        {
            if (MenuChanged == null) return;
            try { MenuChanged(v, open); }
            catch (Exception e) { Core.Log.Error("[Api] a MenuChanged handler failed: " + e); }
        }
    }
}
