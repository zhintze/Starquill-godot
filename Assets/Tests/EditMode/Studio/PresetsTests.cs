using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Starquill.Core;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class PresetsTests
    {
        private static StudioData D => StudioTestRepo.Data;

        private static StudioCharacter Full(int seed, string species = "human")
        {
            var s = new RandomSettings();
            s.Head = s.Torso = s.Arms = s.Legs = s.Feet = s.MainHand = s.OffHand = s.MiscEach = 1f;
            return new StudioRoller(D).RollCharacter(new[] { species }, s, new System.Random(seed));
        }

        private static CharacterPreset ThroughJson(CharacterPreset p) =>
            JsonUtility.FromJson<CharacterPreset>(JsonUtility.ToJson(p, true));

        [Test]
        public void Character_RoundTrip_KeepsEveryChoice()
        {
            var c = Full(1);
            c.HideFacialHair = true;
            c.Locked = true;
            c.LockedSlots.Add(EquipmentSlot.Feet);
            var warnings = new List<string>();

            var back = Presets.ToCharacter(ThroughJson(Presets.FromCharacter(c)), D, "t", warnings);

            CollectionAssert.IsEmpty(warnings);
            Assert.AreEqual(c.SpeciesName, back.SpeciesName);
            Assert.AreEqual(Presets.Hex(c.Body.SkinColor), Presets.Hex(back.Body.SkinColor));
            Assert.AreEqual(Presets.Hex(c.Body.HairColor), Presets.Hex(back.Body.HairColor));
            Assert.AreEqual(c.Body.ChosenHairGroup, back.Body.ChosenHairGroup);
            CollectionAssert.AreEquivalent(c.Body.ModularImageNums, back.Body.ModularImageNums);
            Assert.IsTrue(back.HideFacialHair);
            Assert.IsTrue(back.Locked);
            CollectionAssert.AreEquivalent(c.LockedSlots, back.LockedSlots);
            CollectionAssert.AreEquivalent(c.Gear.Keys, back.Gear.Keys);
            foreach (var slot in c.Gear.Keys)
            {
                var a = c.Gear[slot];
                var b = back.Gear[slot];
                Assert.AreEqual(a.ItemType, b.ItemType, slot.ToString());
                Assert.AreEqual(a.ItemNum, b.ItemNum, slot.ToString());
                CollectionAssert.AreEqual(a.LayerVariants ?? new int[0], b.LayerVariants ?? new int[0], slot.ToString());
                Assert.AreEqual(Presets.Hex(a.BaseColor), Presets.Hex(b.BaseColor), slot.ToString());
                CollectionAssert.AreEquivalent(a.VarianceColors.Keys, b.VarianceColors.Keys, slot.ToString());
            }
        }

        [Test]
        public void Json_IsReadable()
        {
            string json = JsonUtility.ToJson(Presets.FromCharacter(Full(2)), true);
            StringAssert.Contains("\"species\": \"human\"", json);
            StringAssert.Contains("\n", json);
            StringAssert.Contains("#", json);
        }

        [Test]
        public void MissingVariant_EmptySlotAndAWarning()
        {
            var p = Presets.FromCharacter(Full(3));
            var head = p.gear.First(g => g.slot == "Head");
            head.type = "hd01";
            head.number = 9999;
            head.parts = new int[0];
            var warnings = new List<string>();

            var c = Presets.ToCharacter(p, D, "old", warnings);

            Assert.IsFalse(c.Gear.ContainsKey(EquipmentSlot.Head));
            Assert.AreEqual(1, warnings.Count, string.Join("\n", warnings));
            StringAssert.Contains("hd01-9999", warnings[0]);
            StringAssert.Contains("Preset old", warnings[0]);
        }

        [Test]
        public void UnknownSpecies_SkipsTheCharacter()
        {
            var p = Presets.FromCharacter(Full(4));
            p.species = "dragon";
            var warnings = new List<string>();

            Assert.IsNull(Presets.ToCharacter(p, D, "x", warnings));
            StringAssert.Contains("dragon", warnings.Single());
        }

        [Test]
        public void MissingPartVariant_TakesTheFirstDrawing()
        {
            var p = Presets.FromCharacter(Full(5));
            p.parts.First(x => x.group == "f01").variant = "9999";
            var warnings = new List<string>();

            var c = Presets.ToCharacter(p, D, "x", warnings);

            Assert.AreEqual(D.Registry.ModularPartVariants["f01"][0].ToString("D4"), c.Body.ModularImageNums["f01"]);
            StringAssert.Contains("f01-9999", warnings.Single());
        }

        [Test]
        public void BadColor_BecomesWhiteWithAWarning()
        {
            var p = Presets.FromCharacter(Full(6));
            p.skinColor = "pink-ish";
            var warnings = new List<string>();

            var c = Presets.ToCharacter(p, D, "x", warnings);

            Assert.AreEqual(Color.white, c.Body.SkinColor);
            StringAssert.Contains("pink-ish", warnings.Single());
        }

        [Test]
        public void Cast_RoundTrip_CharactersForcedItemsAndSpecies()
        {
            var chars = new[] { Full(7), Full(8, "dwarf"), Full(9, "naga") };
            var forced = new[] { new ForcedItem("hd01", ItemSearch.NumbersFor(D, "hd01")[0]) };

            var p = JsonUtility.FromJson<CastPreset>(JsonUtility.ToJson(Presets.FromCast(chars, forced, new[] { "ogre" }), true));

            Assert.AreEqual(3, p.characters.Count);
            Assert.AreEqual("dwarf", p.characters[1].species);
            Assert.AreEqual(forced[0], Presets.ParseToken(p.forcedItems.Single()));
            CollectionAssert.AreEqual(new[] { "ogre" }, p.excludedSpecies);
        }

        [Test]
        public void ParseToken()
        {
            Assert.AreEqual(new ForcedItem("w01", 3), Presets.ParseToken("w01-0003"));
            Assert.IsNull(Presets.ParseToken("nonsense"));
            Assert.IsNull(Presets.ParseToken(""));
        }

        [Test]
        public void RandomSettings_RoundTrip()
        {
            var s = new RandomSettings { Head = 0.25f, MiscEach = 0.9f, FacialDetail = 0f };
            s.TypeWeights["hd01"] = 0f;
            s.TypeWeights["w04"] = 2.5f;

            var p = JsonUtility.FromJson<RandomPreset>(JsonUtility.ToJson(Presets.FromRandom(s), true));
            var back = new RandomSettings();
            back.TypeWeights["mc01"] = 0.1f; // replaced, not merged
            Presets.ToRandom(p, back);

            Assert.AreEqual(0.25f, back.Head);
            Assert.AreEqual(0.9f, back.MiscEach);
            Assert.AreEqual(0f, back.FacialDetail);
            Assert.AreEqual(0f, back.WeightOf("hd01"));
            Assert.AreEqual(2.5f, back.WeightOf("w04"));
            Assert.AreEqual(1f, back.WeightOf("mc01"));
        }
    }
}
