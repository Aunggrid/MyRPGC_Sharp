// GameServices.cs
// Central access point for all game systems
// This allows any class to access shared systems without passing references everywhere

using MyRPG.Gameplay.Character;
using MyRPG.Gameplay.Systems;
using MyRPG.Gameplay.Items;
using MyRPG.Gameplay.Building;

namespace MyRPG
{
    public static class GameServices
    {
        // Core Systems
        public static MutationSystem Mutations { get; private set; }
        public static TraitSystem Traits { get; private set; }
        public static StatusEffectSystem StatusEffects { get; private set; }
        public static SurvivalSystem SurvivalSystem { get; private set; }
        public static BuildingSystem Building { get; private set; }
        public static CraftingSystem Crafting { get; private set; }
        public static QuestSystem Quests { get; private set; }
        public static ResearchSystem Research { get; private set; }
        
        // Is the service initialized?
        public static bool IsInitialized { get; private set; } = false;
        
        /// <summary>
        /// Initialize all game services. Call once at game startup.
        /// </summary>
        public static void Initialize()
        {
            if (IsInitialized) return;
            
            Mutations = new MutationSystem();
            Traits = new TraitSystem();
            StatusEffects = new StatusEffectSystem();
            SurvivalSystem = new SurvivalSystem();
            Building = new BuildingSystem();
            Crafting = new CraftingSystem();
            Quests = new QuestSystem();
            Research = new ResearchSystem();
            
            // Initialize item database
            ItemDatabase.Initialize();
            
            IsInitialized = true;
            
            System.Diagnostics.Debug.WriteLine(">>> GAME SERVICES INITIALIZED <<<");
        }
        
        /// <summary>
        /// Shutdown and cleanup services.
        /// </summary>
        public static void Shutdown()
        {
            Mutations = null;
            Traits = null;
            StatusEffects = null;
            SurvivalSystem = null;
            Building = null;
            Crafting = null;
            Quests = null;
            Research = null;
            IsInitialized = false;
            
            System.Diagnostics.Debug.WriteLine(">>> GAME SERVICES SHUTDOWN <<<");
        }
    }
}
