// GameServices.cs
// Static service locator for all game systems.
// Initialize() must be called once at game startup.

using MyRPG.Gameplay.Character;
using MyRPG.Gameplay.Systems;
using MyRPG.Gameplay.Building;

namespace MyRPG
{
    /// <summary>
    /// Central access point for all game systems.
    /// Initialize() must be called once at game startup.
    /// </summary>
    public static class GameServices
    {
        // ============================================
        // SYSTEMS
        // ============================================

        public static MutationSystem Mutations { get; private set; }
        public static TraitSystem Traits { get; private set; }
        public static StatusEffectSystem StatusEffects { get; private set; }
        public static BuildingSystem Building { get; private set; }
        public static SurvivalSystem SurvivalSystem { get; private set; }
        public static QuestSystem Quests { get; private set; }
        public static ResearchSystem Research { get; private set; }
        public static CraftingSystem Crafting { get; private set; }
        public static FactionSystem Factions { get; private set; }
        public static FogOfWarSystem FogOfWar { get; private set; }  // NEW: Fog of War visibility system

        public static bool IsInitialized { get; private set; }

        // ============================================
        // INITIALIZATION
        // ============================================

        public static void Initialize()
        {
            if (IsInitialized) return;

            // Initialize all systems
            Mutations = new MutationSystem();
            Traits = new TraitSystem();
            StatusEffects = new StatusEffectSystem();
            Building = new BuildingSystem();
            SurvivalSystem = new SurvivalSystem();
            Quests = new QuestSystem();
            Research = new ResearchSystem();
            Crafting = new CraftingSystem();
            Factions = new FactionSystem();
            FogOfWar = new FogOfWarSystem();  // NEW

            IsInitialized = true;

            System.Diagnostics.Debug.WriteLine(">>> GameServices Initialized <<<");
        }

        // ============================================
        // SHUTDOWN
        // ============================================

        public static void Shutdown()
        {
            // Clean up any resources if needed
            Mutations = null;
            Traits = null;
            StatusEffects = null;
            Building = null;
            SurvivalSystem = null;
            Quests = null;
            Research = null;
            Crafting = null;
            Factions = null;
            FogOfWar = null;  // NEW

            IsInitialized = false;

            System.Diagnostics.Debug.WriteLine(">>> GameServices Shutdown <<<");
        }

        // ============================================
        // RESET (for new game)
        // ============================================

        public static void Reset()
        {
            if (!IsInitialized) return;

            // Reset systems that need it
            Quests?.Reset();
            Research?.Reset();
            Crafting?.Reset();
            // Factions reset happens via LoadReputationSnapshot with default values

            System.Diagnostics.Debug.WriteLine(">>> GameServices Reset <<<");
        }
    }
}
