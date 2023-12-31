using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Oblivion;
using Noggog;
using System.Linq;
using System.Runtime;
using System.Text.RegularExpressions;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins;
using Newtonsoft.Json;

namespace UnleveledOblivion
{
    public class Program
    {
        private static Lazy<Settings> _settings = null!;
        public static Settings Settings => _settings.Value;
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<IOblivionMod, IOblivionModGetter>(RunPatch)
                .SetAutogeneratedSettings("Settings", "settings.json", out _settings)
                .SetTypicalOpen(GameRelease.Oblivion, "YourPatcher.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<IOblivionMod, IOblivionModGetter> state)
        {
            UpdateGameSettings(state);           
            var creatures = CreaturePatcher.UpdateCreatures(state);
            CreatureLeveledListPatcher.UpdateCreatureLeveledLists(state, creatures);
            NPCPatcher.UpdateNPCs(state);
        }

        public static void UpdateGameSettings(IPatcherState<IOblivionMod, IOblivionModGetter> state)
        {
            foreach (var gsGettor in state.LoadOrder.PriorityOrder.GameSetting().WinningOverrides().ToArray())
            {
                var setting = gsGettor.DeepCopy();
                if (setting.EditorID == "iLevCreaLevelDifferenceMax")
                {
                    setting = new GameSettingInt(setting.FormKey)
                    {
                        EditorID = "iLevCreaLevelDifferenceMax",
                        Data = 999
                    };
                    state.PatchMod.GameSettings.Set(setting);
                }
            }
        }
    }
}
