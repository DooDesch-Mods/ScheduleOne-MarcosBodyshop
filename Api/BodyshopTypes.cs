// Copyright (c) 2026 DooDesch. DooDesch API License (see LICENSE.md): you may copy this file unchanged.
using System;
using System.Collections.Generic;
using Chassis.Api;
using Il2CppScheduleOne.Vehicles;
using UnityEngine;

namespace MarcosBodyshop.Api
{
    /// <summary>How a <see cref="BodyshopEntry"/> charges.</summary>
    public enum PriceRule
    {
        /// <summary>Every purchase costs the price. Back to stock is an entry with price 0.</summary>
        EveryTime,
        /// <summary>The first purchase unlocks the entry for this vehicle; after that it is free. Needs <see cref="BodyshopEntry.Unlocked"/>.</summary>
        UnlockOnce,
    }

    /// <summary>A camera position in world space: where the camera is, what it looks at, and its field of view.</summary>
    public struct CameraShot
    {
        public Vector3 Eye;
        public Vector3 Target;
        /// <summary>Field of view in degrees; 0 keeps the garage's.</summary>
        public float Fov;

        public CameraShot(Vector3 eye, Vector3 target, float fov = 0f) { Eye = eye; Target = target; Fov = fov; }

        /// <summary>A shot given in the vehicle's own space (x right, y up, z forward), turned into world space.</summary>
        public static CameraShot Around(LandVehicle vehicle, Vector3 localEye, Vector3 localTarget, float fov = 0f)
        {
            var t = vehicle.transform;
            return new CameraShot(t.TransformPoint(localEye), t.TransformPoint(localTarget), fov);
        }
    }

    /// <summary>
    /// A garage: a place in the world with one or more bays a vehicle parks in. The menu opens for the vehicle in a
    /// bay, through <see cref="BodyshopApi.Open"/> (from a dialogue choice, a trigger or a console command of yours).
    /// </summary>
    public sealed class BodyshopGarage
    {
        /// <summary>Unique id, lower case with a mod prefix, e.g. "mymod_dock". Marco's is "marco".</summary>
        public string Id;
        /// <summary>Name in the menu's banner line.</summary>
        public string Name;
        public readonly List<BodyshopBay> Bays = new List<BodyshopBay>();
    }

    /// <summary>One parking spot of a garage.</summary>
    public sealed class BodyshopBay
    {
        /// <summary>Unique within the garage.</summary>
        public string Id;

        /// <summary>
        /// The vehicle standing in the bay now, or null. Leave it null to use the box <see cref="Center"/>,
        /// <see cref="Size"/>, <see cref="Yaw"/>: the nearest vehicle whose position lies inside.
        /// </summary>
        public Func<LandVehicle> FindVehicle;

        /// <summary>Centre of the bay's box in world space.</summary>
        public Vector3 Center;
        /// <summary>Size of the bay's box, before <see cref="Yaw"/> turns it.</summary>
        public Vector3 Size = new Vector3(4f, 3f, 7f);
        /// <summary>Turn of the bay's box around the up axis, in degrees.</summary>
        public float Yaw;

        /// <summary>Which vehicles this bay serves; null for all.</summary>
        public Func<LandVehicle, bool> Accepts;

        /// <summary>Where the vehicle is put while the menu is open (the game's AlignTo, rear to kerb); null leaves it where it stands.</summary>
        public Func<LandVehicle, Transform> ParkAt;

        /// <summary>The menu's camera when no category or entry asks for its own.</summary>
        public Func<LandVehicle, CameraShot> Camera;

        /// <summary>Paints the outline of the box on the ground while the world is loaded.</summary>
        public bool Marked;
    }

    /// <summary>
    /// A category of the menu (Paint, Engine, Interior). Its entries are asked for each time the menu shows it, so a
    /// list can depend on the vehicle and on what is installed.
    /// </summary>
    public sealed class BodyshopCategory
    {
        /// <summary>Unique id, lower case with a mod prefix for categories of your own. Built in: paint, looks, engine, brakes, transmission, suspension, turbo, tyres.</summary>
        public string Id;
        public string Label;
        /// <summary>A built-in icon: paint, looks, engine, brakes, gears, susp, turbo, tyres, interior, workshop, wrench.</summary>
        public string Icon = "wrench";
        /// <summary>Position in the menu; built-in categories use 100 to 800 in steps of 100.</summary>
        public int Order = 1000;
        /// <summary>Only in this garage; null for every garage.</summary>
        public string Garage;

        /// <summary>Whether the category shows for this vehicle; null for always.</summary>
        public Func<LandVehicle, bool> AppliesTo;
        /// <summary>A reason the category is locked for this vehicle (shown with a lock), or null.</summary>
        public Func<LandVehicle, string> Locked;
        /// <summary>Short state on the right of the row, e.g. "Stage 2"; null for the installed entry's label.</summary>
        public Func<LandVehicle, string> Note;
        /// <summary>The camera while the category is open; null keeps the bay's.</summary>
        public Func<LandVehicle, CameraShot?> Camera;
        /// <summary>Called on the player's machine when the category opens and closes, e.g. to hide a roof.</summary>
        public Action<LandVehicle> Entered, Left;

        /// <summary>The entries for this vehicle. Called on the buyer's machine for the menu and on the host for a purchase.</summary>
        public Func<LandVehicle, IEnumerable<BodyshopEntry>> Entries;
    }

    /// <summary>One thing to buy in a category.</summary>
    public sealed class BodyshopEntry
    {
        /// <summary>Unique within its category, the same on every machine.</summary>
        public string Id;
        public string Label;
        /// <summary>One line under the list, e.g. what the part does.</summary>
        public string Text;
        /// <summary>
        /// Entries with the same group sit in a sub-list of the category, e.g. "Xenon Headlights" inside
        /// "Lights &amp; Looks". Null puts the entry straight into the category.
        /// </summary>
        public string Group;
        public int Price;
        public PriceRule Rule = PriceRule.EveryTime;
        /// <summary>A colour square in the row, "#RRGGBB"; null for none.</summary>
        public string Swatch;
        /// <summary>The driving factors of the part, for the stat bars; null shows no bars.</summary>
        public TuningStats Stats;
        /// <summary>
        /// A part that is fitted or removed (Fitted/Removed in the row) instead of one choice of many. Buying a
        /// toggle entry flips it; with <see cref="PriceRule.UnlockOnce"/> only the first time costs.
        /// </summary>
        public bool Toggle;

        /// <summary>Whether the vehicle has it now (a tick in the row, or Fitted). Null for never.</summary>
        public Func<LandVehicle, bool> Installed;
        /// <summary>For <see cref="PriceRule.UnlockOnce"/>: whether it is already paid for on this vehicle.</summary>
        public Func<LandVehicle, bool> Unlocked;
        /// <summary>A reason it cannot be bought right now (shown instead of buying), or null.</summary>
        public Func<LandVehicle, string> Blocked;

        /// <summary>Shows the entry on the vehicle on the player's machine while it is picked. Must not change saved state.</summary>
        public Action<LandVehicle> Preview;
        /// <summary>Takes the preview back when the pick moves on or the menu closes.</summary>
        public Action<LandVehicle> EndPreview;

        /// <summary>
        /// Runs on the host after the buyer paid: make the change for real and sync it. Return null on success, or a
        /// reason; the buyer then gets the money back.
        /// </summary>
        public Func<LandVehicle, string> Apply;

        /// <summary>The camera while the entry is picked; null keeps the category's.</summary>
        public Func<LandVehicle, CameraShot?> Camera;
    }
}
