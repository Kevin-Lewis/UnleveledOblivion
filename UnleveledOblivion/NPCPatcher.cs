using Mutagen.Bethesda;
using Mutagen.Bethesda.Oblivion;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Synthesis;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnleveledOblivion
{
    public static class NPCPatcher
    {
        public static void UpdateNPCs(IPatcherState<IOblivionMod, IOblivionModGetter> state)
        {
            // Read the creature file and create a dictionary
            if (state?.ExtraSettingsDataPath is null) { return; }
            var path = Path.Combine(state.ExtraSettingsDataPath, "NPCs.json");
            string npcLevelsJson = File.ReadAllText(path);
            var npcLevelsFromFile = JsonConvert.DeserializeObject<List<NPCLevel>>(npcLevelsJson)
                        .Where(npcLevel => npcLevel != null && !string.IsNullOrWhiteSpace(npcLevel?.EditorID))
                        .ToDictionary(npcLevel => npcLevel.EditorID, npc => npc.Level);


            foreach (var npcGetter in state.LoadOrder.PriorityOrder.Npc().WinningOverrides())
            {
                // Add it to the patch
                Npc npc = state.PatchMod.Npcs.GetOrAddAsOverride(npcGetter) ?? throw new Exception("Could not add npc as override.");
                if (npc.EditorID == "Player") { continue; }
                // Adjust
                if (npc?.Configuration?.Flags is not null)
                {
                    if (npc.Configuration.Flags.HasFlag(Npc.NpcFlag.PCLevelOffset))
                    {
                        npc.Configuration.Flags -= Npc.NpcFlag.PCLevelOffset;
                        CalculateNPCLevel(npc, state.LinkCache, isStatic: false, npcLevelsFromFile);
                    }
                    else
                    {
                        CalculateNPCLevel(npc, state.LinkCache, isStatic: true, npcLevelsFromFile);
                    }
                    ﻿if (npc.AIData == null || (npc.BuySellServices == 0 && npc.MaximumTrainingLevel == 0))
                    {
                        npc.Configuration.Flags |= Npc.NpcFlag.AutoCalcStats;
                    }
                }
            }
        }

        public static void CalculateNPCLevel(Npc npc, ILinkCache linkCache, bool isStatic, Dictionary<string, short> npcLevelsFromFile)
        {
            if (npc.Configuration is null || npc.EditorID is null || npc?.Name is null) { return; }

            // If the creature is in the file, use the level from the file
            if (npcLevelsFromFile.TryGetValue(npc.EditorID, out short levelFromFile))
            {
                npc.Configuration.LevelOffset = levelFromFile;
                return;
            }
            else if (npc.Name.ToLower().Contains("dremora"))
            {
                var name = npc.Name.ToLower();
                if (name.Contains("churl"))
                {
                    npc.Configuration.LevelOffset = 7;
                }
                else if (name.Contains("caitiff"))
                {
                    npc.Configuration.LevelOffset = 9;
                }
                else if (name.Contains("kynval"))
                {
                    npc.Configuration.LevelOffset = 11;
                }
                else if (name.Contains("kynreeve"))
                {
                    npc.Configuration.LevelOffset = 13;
                }
                else if (name.Contains("kynmarcher"))
                {
                    npc.Configuration.LevelOffset = 15;
                }
                else if (name.Contains("markynaz"))
                {
                    npc.Configuration.LevelOffset = 19;
                }
                else if (name.Contains("valkynaz"))
                {
                    npc.Configuration.LevelOffset = 22;
                }
                else
                {
                    npc.Configuration.LevelOffset = 12;
                }
                return;
            }

            short startingLevel = npc.Configuration.LevelOffset;
            if (npc.Name.ToLower().Contains("vampire"))
            {
                npc.Configuration.LevelOffset = Program.Settings.NPCSettings.VampireBaseLevel;
            }           
            else
            {
                npc.Configuration.LevelOffset = Program.Settings.NPCSettings.BaseLevel;
            }
            if (npc.Configuration.Flags.HasFlag(Npc.NpcFlag.Female))
            {
                npc.Configuration.LevelOffset += Program.Settings.NPCSettings.GenderOffset;
            }
            if (!isStatic)
            {
                _ = startingLevel switch
                {
                    _ when startingLevel < 0 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier1Offset,
                    _ when startingLevel <= 0 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier2Offset,
                    _ when startingLevel <= 1 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier3Offset,
                    _ when startingLevel <= 2 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier4Offset,
                    _ when startingLevel <= 3 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier5Offset,
                    _ when startingLevel <= 4 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier6Offset,
                    _ when startingLevel <= 5 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier7Offset,
                    _ when startingLevel <= 6 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier8Offset,
                    _ when startingLevel <= 50 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier9Offset,
                    _ => npc.Configuration.LevelOffset += 0
                };
            }
            else
            {
                _ = startingLevel switch
                {
                    _ when startingLevel <= 2 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier1Offset,
                    _ when startingLevel <= 5 => npc.Configuration.LevelOffset += 0,
                    _ when startingLevel <= 10 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier2Offset,
                    _ when startingLevel <= 17 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier3Offset,
                    _ when startingLevel <= 20 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier4Offset,
                    _ when startingLevel <= 50 => npc.Configuration.LevelOffset += Program.Settings.NPCSettings.Tier9Offset,
                    _ => npc.Configuration.LevelOffset += 0
                };
            }

            AdjustNPCLevelByClass(npc, linkCache);
            AdjustNPCLevelByFaction(npc, linkCache);
            AdjustNPCLevelByRace(npc, linkCache);
            npc.Configuration.LevelOffset = Math.Max((short)1, Math.Min((short)50, npc.Configuration.LevelOffset));
        }

        public static void AdjustNPCLevelByClass(Npc npc, ILinkCache linkCache)
        {
            IClassGetter? npcClass = npc.Class.TryResolve(linkCache);
            if (npcClass is null || npcClass?.EditorID is null || npc?.Configuration?.LevelOffset is null) { return; }
            if (npcClass.EditorID.ToLower().Contains("guard") || npcClass.EditorID.ToLower().Contains("soldier"))
            {
                npc.Configuration.LevelOffset = Program.Settings.NPCSettings.ClassSettings.GuardLevel;
            }
            if (npcClass.EditorID == "Herald" || npcClass.EditorID == "Herder"
                || npcClass.EditorID == "Commoner" || npcClass.EditorID == "Farmer"
                || npcClass.EditorID == "Bard" || npcClass.EditorID == "Merchant")
            {
                npc.Configuration.LevelOffset += Program.Settings.NPCSettings.ClassSettings.CommonerOffset;
            }
            if (npcClass.EditorID == "Warrior" || npcClass.EditorID.ToLower().Contains("knight")
                || npcClass.EditorID == "Crusader" || npcClass.EditorID == "Assassin"
                || npcClass.EditorID == "DBEnforcer" || npcClass.EditorID == "FGChampion"
                || npcClass.EditorID == "Blademaster" || npcClass.EditorID == "Pirate"
                || npcClass.EditorID == "Barbarian" || npcClass.EditorID == "Battlemage"
                || npcClass.EditorID == "Warlock" || npcClass.EditorID == "Witch"
                || npcClass.EditorID == "Sharpshooter" || npcClass.EditorID == "Hunter"
                || npcClass.EditorID == "Necromancer" || npcClass.EditorID == "Conjurer"
                || npcClass.EditorID == "Mage"
                || npcClass.EditorID.ToLower().Contains("mythic dawn")
                || npcClass.EditorID == "Sorcerer" || npcClass.EditorID == "Spellsword")
            {
                npc.Configuration.LevelOffset = Math.Max(Program.Settings.NPCSettings.ClassSettings.WarriorMin, npc.Configuration.LevelOffset);
            }
            if (npcClass.EditorID == "Noble")
            {
                npc.Configuration.LevelOffset += Program.Settings.NPCSettings.ClassSettings.NobleOffset;
            }
            if (npcClass.EditorID.ToLower().Contains("bandit") || npcClass.EditorID.ToLower().Contains("marauder") || npcClass.EditorID.ToLower().Contains("zealot"))
            {
                npc.Configuration.LevelOffset = Math.Max((short)Program.Settings.NPCSettings.ClassSettings.BanditMin, Math.Min((short)Program.Settings.NPCSettings.ClassSettings.BanditMax, npc.Configuration.LevelOffset));
            }
        }

        public static void AdjustNPCLevelByRace(Npc npc, ILinkCache linkCache)
        {
            IRaceGetter? npcRace = npc.Race.TryResolve(linkCache);
            if (npcRace is null || npcRace?.EditorID is null || npc?.Configuration?.LevelOffset is null) { return; }
            switch (npcRace.EditorID)
            {
                case "Imperial":
                    npc.Configuration.LevelOffset += Program.Settings.NPCSettings.RaceSettings.ImperialLevel;
                    break;
                case "Redguard":
                    npc.Configuration.LevelOffset += Program.Settings.NPCSettings.RaceSettings.RedguardLevel;
                    break;
                case "DarkSeducer":
                    npc.Configuration.LevelOffset += Program.Settings.NPCSettings.RaceSettings.DarkSeducerLevel;
                    break;
                case "GoldenSaint":
                    npc.Configuration.LevelOffset += Program.Settings.NPCSettings.RaceSettings.GoldenSaintLevel;
                    break;
                case "Orc":
                    npc.Configuration.LevelOffset += Program.Settings.NPCSettings.RaceSettings.OrcLevel;
                    break;
                case "DarkElf":
                    npc.Configuration.LevelOffset += Program.Settings.NPCSettings.RaceSettings.DarkElfLevel;
                    break;
                case "Khajiit":
                    npc.Configuration.LevelOffset += Program.Settings.NPCSettings.RaceSettings.KhajiitLevel;
                    break;
                case "WoodElf":
                    npc.Configuration.LevelOffset += Program.Settings.NPCSettings.RaceSettings.WoodElfLevel;
                    break;
                case "Breton":
                    npc.Configuration.LevelOffset += Program.Settings.NPCSettings.RaceSettings.BretonLevel;
                    break;
                case "Nord":
                    npc.Configuration.LevelOffset += Program.Settings.NPCSettings.RaceSettings.NordLevel;
                    break;
                case "Argonian":
                    npc.Configuration.LevelOffset += Program.Settings.NPCSettings.RaceSettings.ArgonianLevel;
                    break;
                case "Dremora":
                    npc.Configuration.LevelOffset += Program.Settings.NPCSettings.RaceSettings.DremoraLevel;
                    break;
            }
        }

        public static void AdjustNPCLevelByFaction(Npc npc, ILinkCache linkCache)
        {
            foreach (var item in npc.Factions)
            {
                var faction = item.Faction.TryResolve(linkCache);
                if (faction is null || faction?.EditorID is null || npc?.Configuration?.LevelOffset is null) { return; }
                if (faction.EditorID == "SEOrderFaction")
                {
                    npc.Configuration.LevelOffset = Math.Max(npc.Configuration.LevelOffset, Program.Settings.NPCSettings.FactionSettings.ForcesOrderMin);
                }
                if (faction.EditorID == "MythicDawn")
                {
                    npc.Configuration.LevelOffset = Math.Max(npc.Configuration.LevelOffset, Program.Settings.NPCSettings.FactionSettings.MythicDawnMin);
                }
                if (faction.EditorID == "FightersGuild")
                {
                    npc.Configuration.LevelOffset = Math.Max(npc.Configuration.LevelOffset, Program.Settings.NPCSettings.FactionSettings.FightersGuildMin);
                }
                if (faction.EditorID == "MagesGuild")
                {
                    npc.Configuration.LevelOffset = Math.Max(npc.Configuration.LevelOffset, Program.Settings.NPCSettings.FactionSettings.MagesGuildMin);
                }
                if (faction.EditorID == "DarkBrotherhood")
                {
                    npc.Configuration.LevelOffset = Math.Max(npc.Configuration.LevelOffset, Program.Settings.NPCSettings.FactionSettings.DarkBrotherhoodMin);
                }
                if (faction.EditorID == "ThievesGuild")
                {
                    npc.Configuration.LevelOffset = Math.Max(npc.Configuration.LevelOffset, Program.Settings.NPCSettings.FactionSettings.DarkBrotherhoodMin);
                }
                if (faction.EditorID == "VampireFaction")
                {
                    npc.Configuration.LevelOffset = Math.Max(npc.Configuration.LevelOffset, Program.Settings.NPCSettings.FactionSettings.VampireMin);
                }
                if (faction.EditorID == "NDKnightsoftheNine" || faction.EditorID == "NDOriginalKnightsFaction")
                {
                    npc.Configuration.LevelOffset = Math.Max(npc.Configuration.LevelOffset, Program.Settings.NPCSettings.FactionSettings.KnightsNineMin);
                }
            }
        }

        public static void OutputNPCStats(IPatcherState<IOblivionMod, IOblivionModGetter> state, string extension = "")
        {
            var npcs = new List<string>();
            foreach (var npcGetter in state.LoadOrder.PriorityOrder.Npc().WinningOverrides())
            {
                var npc = npcGetter.DeepCopy();
                npcs.Add($"{npc.Name} - {npc.EditorID} - Level:{npc.Configuration.LevelOffset}");
            }

            string downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            // Write lists to files in the Downloads directory
            File.WriteAllLines(Path.Combine(downloadsFolder, $"npcs{extension}.txt"), npcs);
        }
    }
}
