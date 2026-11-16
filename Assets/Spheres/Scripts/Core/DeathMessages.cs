using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Funny kill-feed lines. Templates use {k} for the killer and {v} for the victim.
    /// Mods can add lines to any category, or make new categories for their own
    /// <see cref="DamageInfo.Cause"/> values: <c>DeathMessages.Add("laser", "{k} gave {v} a tan")</c>.
    /// A cause category with a "_k" suffix is used when someone else gets the credit.
    /// </summary>
    public static class DeathMessages
    {
        static readonly Dictionary<string, List<string>> pools = new Dictionary<string, List<string>>();
        static readonly Dictionary<string, int> last = new Dictionary<string, int>();

        static DeathMessages()
        {
            AddAll("bullet",
                "{k} popped {v}",
                "{k} deflated {v}",
                "{k} turned {v} into a donut",
                "{k} rounded {v} down to zero",
                "{v} got rolled by {k}",
                "{v} was perforated by {k}",
                "{k} gave {v} a brand new air hole",
                "{k} made {v} noticeably less spherical",
                "{v} caught {k}'s bullet. With their face.",
                "{k} reduced {v} to a hemisphere",
                "{v} has been unrounded by {k}",
                "{k} sent {v} back to the drawing board",
                "{v} rolled straight into {k}'s line of fire",
                "{k} let the air out of {v}",
                "{k} dribbled {v} right off the court",
                "{v} forgot the block button exists. {k} did not.",
                "{k} put {v} in the lost and found",
                "{k} gave {v} a participation trophy");
            AddAll("explosion",
                "{k} sent {v} into orbit",
                "{k} turned {v} into confetti",
                "{k} gave {v} a very warm hug",
                "{v} was scattered across the arena by {k}",
                "{k} launched {v} to the moon",
                "{v} found out what {k}'s explosives taste like",
                "{k} redecorated the arena with {v}");
            AddAll("self",
                "{v} played themselves",
                "{v} blew up their own spot",
                "{v} forgot which side of the explosion was the safe one",
                "{v} achieved self-destruction",
                "{v} was their own worst enemy",
                "{v} tried rocket jumping. It did not go well.");
            AddAll("fall",
                "{v} forgot spheres can't fly",
                "{v} tested gravity. Gravity won.",
                "{v} landed. Badly.",
                "{v} hit the ground like a bowling ball",
                "{v} forgot to block the floor",
                "{v} bounced once. Not twice.",
                "{v} took the express route down");
            AddAll("fall_k",
                "{k} taught {v} about gravity",
                "{k} knocked {v} off the map",
                "{v} took the stairs the fast way, thanks to {k}",
                "{k} helped {v} find the floor");
            AddAll("border",
                "{v} hugged the forcefield",
                "{v} tried to leave early",
                "{v} found the edge of the world",
                "{v} touched the fence. It was electric.",
                "{v} mistook the border for an exit");
            AddAll("border_k",
                "{k} pinned {v} to the forcefield",
                "{k} showed {v} the exit",
                "{k} bounced {v} off the edge of reality");
            AddAll("lava",
                "{v} took a lava bath",
                "{v} thought the floor was lava. It was.",
                "{v} melted into a puddle",
                "{v} is now fondue",
                "{v} went for a dip in the hot tub",
                "{v} got a little too toasty");
            AddAll("lava_k",
                "{k} served {v} on a lava platter",
                "{k} dunked {v} in the lava",
                "{k} turned {v} into a lava lamp");
            AddAll("trap",
                "{v} lost a fight with the scenery",
                "{v} got caught in the machinery",
                "{v} ignored the safety signs");
            AddAll("trap_k",
                "{k} fed {v} to the machinery",
                "{k} shoved {v} into a trap",
                "{k} introduced {v} to the level design");
            AddAll("spikes",
                "{v} got the point",
                "{v} became a pincushion",
                "{v} sat on something sharp",
                "{v} found the spikes the pointy way",
                "{v} discovered why the floor was flashing red");
            AddAll("spikes_k",
                "{k} made a pincushion out of {v}",
                "{k} helped {v} get the point");
            AddAll("pendulum",
                "{v} timed the pendulum wrong",
                "{v} was tick-tocked out of the round",
                "{v} learned how pendulums work",
                "{v} got swung at, and hit",
                "{v} stood under the wrecking ball");
            AddAll("pendulum_k",
                "{k} pushed {v} into the swing",
                "{k} timed the pendulum perfectly for {v}");
            AddAll("saw",
                "{v} got sliced like a melon",
                "{v} is now two hemispheres",
                "{v} was halved by the saw blade",
                "{v} tried to high-five the saw");
            AddAll("saw_k",
                "{k} cut {v} in half",
                "{k} fed {v} to the saw blade");
            AddAll("piston",
                "{v} got pancaked",
                "{v} was flattened into a frisbee",
                "{v} is now a coaster",
                "{v} found out what the piston does");
            AddAll("piston_k",
                "{k} pancaked {v}",
                "{k} pushed {v} into the press");
            AddAll("spinner",
                "{v} got swatted by the spinner",
                "{v} was hit for a home run",
                "{v} lost an argument with a windmill");
            AddAll("spinner_k",
                "{k} batted {v} into the spinner",
                "{k} let the spinner take care of {v}");
            AddAll("barrel",
                "{v} stood next to the wrong barrel",
                "{v} learned barrels are explosive",
                "{v} hugged a barrel at the worst moment");
            AddAll("barrel_k",
                "{k} introduced {v} to an explosive barrel",
                "{k} uncorked a barrel on {v}",
                "{k} barrel-rolled {v}");
            AddAll("barrel_self",
                "{v} shot the barrel. The barrel shot back.",
                "{v} really should have read the label on that barrel",
                "{v} found out the red barrels are red for a reason");
            AddAll("blast_block",
                "{v} stood on a blast block. Bold.",
                "{v} found the one block you shouldn't shoot");
            AddAll("blast_block_k",
                "{k} detonated the floor under {v}",
                "{k} turned {v}'s cover into a bomb");
            AddAll("blast_block_self",
                "{v} shot the floor they were standing on",
                "{v} detonated their own cover");
            AddAll("friendly",
                "{k} betrayed {v}",
                "{k} mistook {v} for an enemy",
                "{k} committed a friendly foul against {v}",
                "{k} and {v} need to have a talk");
            AddAll("generic",
                "{v} spontaneously unrounded",
                "{v} ceased to be round",
                "{v} popped for no reason at all");
            AddAll("generic_k",
                "{k} eliminated {v}",
                "{k} outplayed {v}",
                "{k} rolled right over {v}");
        }

        static void AddAll(string category, params string[] lines)
        {
            foreach (var l in lines) Add(category, l);
        }

        /// <summary>Add a line to a category (creates the category if needed).</summary>
        public static void Add(string category, string template)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(template)) return;
            if (!pools.TryGetValue(category, out var list)) pools[category] = list = new List<string>();
            if (!list.Contains(template)) list.Add(template);
        }

        public static bool HasCategory(string category) => category != null && pools.TryGetValue(category, out var l) && l.Count > 0;

        /// <summary>The category used for a death (exposed for mods and tests).</summary>
        public static string Category(Player killer, Player victim, DamageInfo info)
        {
            bool self = info != null && info.Source == victim;
            bool byOther = !self && killer != null && killer != victim;
            string cause = info?.Cause;
            if (byOther && killer.Team == victim.Team && info != null && info.Source == killer) return "friendly";
            if (!string.IsNullOrEmpty(cause))
            {
                if (self && HasCategory(cause + "_self")) return cause + "_self";
                if (byOther && HasCategory(cause + "_k")) return cause + "_k";
                if (!byOther && HasCategory(cause)) return cause;
                if (HasCategory(cause)) return cause;
            }
            var type = info != null ? info.Type : DamageType.Generic;
            switch (type)
            {
                case DamageType.Bullet: return byOther ? "bullet" : self ? "self" : "generic";
                case DamageType.Explosion: return byOther ? "explosion" : self ? "self" : "generic";
                case DamageType.Fall: return byOther ? "fall_k" : "fall";
                case DamageType.Border: return byOther ? "border_k" : "border";
                case DamageType.Hazard: return byOther ? "lava_k" : "lava";
                case DamageType.Trap: return byOther ? "trap_k" : "trap";
                default: return byOther ? "generic_k" : "generic";
            }
        }

        /// <summary>Build a kill-feed line with colored names.</summary>
        public static string Format(Player killer, Player victim, DamageInfo info)
        {
            string category = Category(killer, victim, info);
            if (!pools.TryGetValue(category, out var list) || list.Count == 0) list = pools["generic"];
            // Avoid repeating the previous line of a category.
            int idx = Random.Range(0, list.Count);
            if (list.Count > 1 && last.TryGetValue(category, out int prev) && prev == idx) idx = (idx + 1 + Random.Range(0, list.Count - 1)) % list.Count;
            last[category] = idx;
            if (killer == victim) killer = null;
            string k = killer != null ? Colored(killer.DisplayName, killer.Color) : "Someone";
            string v = Colored(victim.DisplayName, victim.Color);
            return list[idx].Replace("{k}", k).Replace("{v}", v);
        }

        static string Colored(string name, Color c) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + name + "</color>";
    }
}
