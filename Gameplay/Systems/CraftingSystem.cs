// Gameplay/Systems/CraftingSystem.cs
// Crafting system with recipes and workstation requirements

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MyRPG.Data;
using MyRPG.Gameplay.Building;
using MyRPG.Gameplay.Character;
using MyRPG.Gameplay.Items;

namespace MyRPG.Gameplay.Systems
{
    // ============================================
    // RECIPE DEFINITION
    // ============================================
    
    public class Recipe
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        
        // Requirements
        public Dictionary<string, int> Ingredients { get; set; } = new Dictionary<string, int>();
        public StructureType? RequiredWorkstation { get; set; } = null;  // null = can craft anywhere
        public int RequiredLevel { get; set; } = 1;
        public int RequiredINT { get; set; } = 0;  // Minimum INT to learn/craft
        
        // Output
        public string OutputItemId { get; set; }
        public int OutputCount { get; set; } = 1;
        public ItemQuality BaseQuality { get; set; } = ItemQuality.Normal;
        
        // Crafting
        public float CraftTime { get; set; } = 1f;  // Seconds (not used yet, instant for now)
        public RecipeCategory Category { get; set; } = RecipeCategory.Basic;
        
        // Is this recipe unlocked by default?
        public bool UnlockedByDefault { get; set; } = true;
    }
    
    public enum RecipeCategory
    {
        Basic,      // No workstation needed
        Weapons,    // CraftingBench
        Armor,      // CraftingBench
        Tools,      // CraftingBench
        Cooking,    // CookingStation
        Medical,    // CraftingBench or ResearchTable
        Advanced    // ResearchTable
    }
    
    // ============================================
    // CRAFTING SYSTEM
    // ============================================
    
    public class CraftingSystem
    {
        private Dictionary<string, Recipe> _recipes = new Dictionary<string, Recipe>();
        private HashSet<string> _unlockedRecipes = new HashSet<string>();
        
        // Currently open workstation (if any)
        public Structure ActiveWorkstation { get; set; } = null;
        
        // Events
        public event Action<Recipe, Item> OnItemCrafted;
        public event Action<string> OnRecipeUnlocked;
        
        public CraftingSystem()
        {
            InitializeRecipes();
            
            // Unlock default recipes
            foreach (var recipe in _recipes.Values)
            {
                if (recipe.UnlockedByDefault)
                {
                    _unlockedRecipes.Add(recipe.Id);
                }
            }
        }
        
        // ============================================
        // RECIPE INITIALIZATION
        // ============================================
        
        private void InitializeRecipes()
        {
            // ==================
            // BASIC (No workstation)
            // ==================
            
            AddRecipe(new Recipe
            {
                Id = "torch",
                Name = "Torch",
                Description = "A simple light source",
                Category = RecipeCategory.Basic,
                Ingredients = new Dictionary<string, int> { ["wood"] = 2, ["cloth"] = 1 },
                OutputItemId = "torch",
                RequiredWorkstation = null
            });
            
            AddRecipe(new Recipe
            {
                Id = "bandage_craft",
                Name = "Cloth Bandage",
                Description = "Basic wound dressing",
                Category = RecipeCategory.Basic,
                Ingredients = new Dictionary<string, int> { ["cloth"] = 3 },
                OutputItemId = "bandage",
                RequiredWorkstation = null
            });
            
            AddRecipe(new Recipe
            {
                Id = "spear_makeshift_craft",
                Name = "Makeshift Spear",
                Description = "A sharpened stick. Better than nothing.",
                Category = RecipeCategory.Basic,
                Ingredients = new Dictionary<string, int> { ["wood"] = 3 },
                OutputItemId = "spear_makeshift",
                RequiredWorkstation = null
            });
            
            AddRecipe(new Recipe
            {
                Id = "arrow_basic_craft",
                Name = "Basic Arrows (x5)",
                Description = "Simple wooden arrows",
                Category = RecipeCategory.Basic,
                Ingredients = new Dictionary<string, int> { ["wood"] = 2, ["stone"] = 1 },
                OutputItemId = "arrow_basic",
                OutputCount = 5,
                RequiredWorkstation = null
            });
            
            // ==================
            // WEAPONS (CraftingBench)
            // ==================
            
            AddRecipe(new Recipe
            {
                Id = "pipe_club_craft",
                Name = "Pipe Club",
                Description = "Heavy metal pipe. Smashes good.",
                Category = RecipeCategory.Weapons,
                Ingredients = new Dictionary<string, int> { ["scrap_metal"] = 3, ["cloth"] = 1 },
                OutputItemId = "pipe_club",
                RequiredWorkstation = StructureType.CraftingBench
            });
            
            AddRecipe(new Recipe
            {
                Id = "knife_combat_craft",
                Name = "Combat Knife",
                Description = "A proper fighting blade",
                Category = RecipeCategory.Weapons,
                Ingredients = new Dictionary<string, int> { ["metal"] = 2, ["leather"] = 1 },
                OutputItemId = "knife_combat",
                RequiredWorkstation = StructureType.CraftingBench,
                RequiredINT = 3
            });
            
            AddRecipe(new Recipe
            {
                Id = "machete_craft",
                Name = "Machete",
                Description = "Long blade for slashing",
                Category = RecipeCategory.Weapons,
                Ingredients = new Dictionary<string, int> { ["metal"] = 4, ["leather"] = 2, ["wood"] = 1 },
                OutputItemId = "machete",
                RequiredWorkstation = StructureType.CraftingBench,
                RequiredINT = 4
            });
            
            AddRecipe(new Recipe
            {
                Id = "axe_fire_craft",
                Name = "Fire Axe",
                Description = "Heavy axe, good for chopping",
                Category = RecipeCategory.Weapons,
                Ingredients = new Dictionary<string, int> { ["metal"] = 5, ["wood"] = 3 },
                OutputItemId = "axe_fire",
                RequiredWorkstation = StructureType.CraftingBench,
                RequiredINT = 5
            });
            
            AddRecipe(new Recipe
            {
                Id = "bow_simple_craft",
                Name = "Simple Bow",
                Description = "Wooden bow for ranged attacks",
                Category = RecipeCategory.Weapons,
                Ingredients = new Dictionary<string, int> { ["wood"] = 4, ["cloth"] = 2 },
                OutputItemId = "bow_simple",
                RequiredWorkstation = StructureType.CraftingBench,
                RequiredINT = 3
            });
            
            // ==================
            // ARMOR (CraftingBench)
            // ==================
            
            AddRecipe(new Recipe
            {
                Id = "armor_leather_craft",
                Name = "Leather Armor",
                Description = "Basic protection from leather",
                Category = RecipeCategory.Armor,
                Ingredients = new Dictionary<string, int> { ["leather"] = 5, ["cloth"] = 2 },
                OutputItemId = "armor_leather",
                RequiredWorkstation = StructureType.CraftingBench
            });
            
            AddRecipe(new Recipe
            {
                Id = "armor_raider_craft",
                Name = "Raider Armor",
                Description = "Reinforced armor with metal plates",
                Category = RecipeCategory.Armor,
                Ingredients = new Dictionary<string, int> { ["leather"] = 3, ["scrap_metal"] = 5, ["cloth"] = 2 },
                OutputItemId = "armor_raider",
                RequiredWorkstation = StructureType.CraftingBench,
                RequiredINT = 4
            });
            
            AddRecipe(new Recipe
            {
                Id = "helmet_hardhat_craft",
                Name = "Hardhat Helmet",
                Description = "Protects your head... somewhat",
                Category = RecipeCategory.Armor,
                Ingredients = new Dictionary<string, int> { ["scrap_metal"] = 3, ["cloth"] = 1 },
                OutputItemId = "helmet_hardhat",
                RequiredWorkstation = StructureType.CraftingBench
            });
            
            // ==================
            // COOKING (CookingStation)
            // ==================
            
            AddRecipe(new Recipe
            {
                Id = "food_steak_craft",
                Name = "Cooked Steak",
                Description = "Grilled mutant meat. Tastes... interesting.",
                Category = RecipeCategory.Cooking,
                Ingredients = new Dictionary<string, int> { ["mutant_meat"] = 2 },
                OutputItemId = "food_steak",
                RequiredWorkstation = StructureType.CookingStation
            });
            
            AddRecipe(new Recipe
            {
                Id = "water_purified_craft",
                Name = "Purified Water",
                Description = "Boiled and filtered water",
                Category = RecipeCategory.Cooking,
                Ingredients = new Dictionary<string, int> { ["water_dirty"] = 2 },
                OutputItemId = "water_purified",
                RequiredWorkstation = StructureType.CookingStation
            });
            
            AddRecipe(new Recipe
            {
                Id = "stew_craft",
                Name = "Hearty Stew",
                Description = "Nutritious meat and vegetable stew",
                Category = RecipeCategory.Cooking,
                Ingredients = new Dictionary<string, int> { ["mutant_meat"] = 1, ["food_mutfruit"] = 1, ["water_purified"] = 1 },
                OutputItemId = "food_stew",
                RequiredWorkstation = StructureType.CookingStation,
                RequiredINT = 3
            });
            
            // ==================
            // MEDICAL (CraftingBench)
            // ==================
            
            AddRecipe(new Recipe
            {
                Id = "medkit_craft",
                Name = "Medical Kit",
                Description = "Proper first aid supplies",
                Category = RecipeCategory.Medical,
                Ingredients = new Dictionary<string, int> { ["bandage"] = 2, ["cloth"] = 2, ["components"] = 1 },
                OutputItemId = "medkit",
                RequiredWorkstation = StructureType.CraftingBench,
                RequiredINT = 4
            });
            
            AddRecipe(new Recipe
            {
                Id = "antidote_craft",
                Name = "Antidote",
                Description = "Cures poison effects",
                Category = RecipeCategory.Medical,
                Ingredients = new Dictionary<string, int> { ["food_mutfruit"] = 2, ["water_purified"] = 1, ["components"] = 1 },
                OutputItemId = "antidote",
                RequiredWorkstation = StructureType.CraftingBench,
                RequiredINT = 5
            });
            
            // ==================
            // ADVANCED (ResearchTable)
            // ==================
            
            AddRecipe(new Recipe
            {
                Id = "stimpack_craft",
                Name = "Stimpack",
                Description = "Advanced healing injection",
                Category = RecipeCategory.Advanced,
                Ingredients = new Dictionary<string, int> { ["medkit"] = 1, ["components"] = 2, ["scrap_electronics"] = 1 },
                OutputItemId = "stimpack",
                RequiredWorkstation = StructureType.ResearchTable,
                RequiredINT = 6,
                UnlockedByDefault = false
            });
            
            AddRecipe(new Recipe
            {
                Id = "ammo_9mm_craft",
                Name = "9mm Ammo (x10)",
                Description = "Handcrafted pistol ammunition",
                Category = RecipeCategory.Advanced,
                Ingredients = new Dictionary<string, int> { ["scrap_metal"] = 2, ["components"] = 1 },
                OutputItemId = "ammo_9mm",
                OutputCount = 10,
                RequiredWorkstation = StructureType.ResearchTable,
                RequiredINT = 5,
                UnlockedByDefault = false
            });
            
            AddRecipe(new Recipe
            {
                Id = "ammo_shells_craft",
                Name = "Shotgun Shells (x6)",
                Description = "Handcrafted shotgun shells",
                Category = RecipeCategory.Advanced,
                Ingredients = new Dictionary<string, int> { ["scrap_metal"] = 3, ["components"] = 1 },
                OutputItemId = "ammo_shells",
                OutputCount = 6,
                RequiredWorkstation = StructureType.ResearchTable,
                RequiredINT = 5,
                UnlockedByDefault = false
            });
            
            System.Diagnostics.Debug.WriteLine($">>> CRAFTING: Initialized {_recipes.Count} recipes <<<");
        }
        
        private void AddRecipe(Recipe recipe)
        {
            _recipes[recipe.Id] = recipe;
        }
        
        // ============================================
        // RECIPE ACCESS
        // ============================================
        
        public Recipe GetRecipe(string id)
        {
            return _recipes.GetValueOrDefault(id);
        }
        
        public List<Recipe> GetAllRecipes()
        {
            return _recipes.Values.ToList();
        }
        
        public List<Recipe> GetUnlockedRecipes()
        {
            return _recipes.Values.Where(r => _unlockedRecipes.Contains(r.Id)).ToList();
        }
        
        public List<Recipe> GetAvailableRecipes(StructureType? workstation, CharacterStats stats)
        {
            return GetUnlockedRecipes()
                .Where(r => CanAccessRecipe(r, workstation, stats))
                .ToList();
        }
        
        public List<Recipe> GetRecipesByCategory(RecipeCategory category)
        {
            return GetUnlockedRecipes().Where(r => r.Category == category).ToList();
        }
        
        public bool IsRecipeUnlocked(string recipeId)
        {
            return _unlockedRecipes.Contains(recipeId);
        }
        
        public void UnlockRecipe(string recipeId)
        {
            if (_recipes.ContainsKey(recipeId) && !_unlockedRecipes.Contains(recipeId))
            {
                _unlockedRecipes.Add(recipeId);
                OnRecipeUnlocked?.Invoke(recipeId);
                System.Diagnostics.Debug.WriteLine($">>> CRAFTING: Unlocked recipe '{recipeId}' <<<");
            }
        }
        
        // ============================================
        // CRAFTING CHECKS
        // ============================================
        
        /// <summary>
        /// Check if player can access a recipe (has workstation, meets requirements)
        /// </summary>
        public bool CanAccessRecipe(Recipe recipe, StructureType? availableWorkstation, CharacterStats stats)
        {
            // Check workstation requirement
            if (recipe.RequiredWorkstation != null)
            {
                if (availableWorkstation != recipe.RequiredWorkstation)
                    return false;
            }
            
            // Check INT requirement
            if (stats.Attributes.INT < recipe.RequiredINT)
                return false;
            
            // Check level requirement
            if (stats.Level < recipe.RequiredLevel)
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Check if player has materials for a recipe
        /// </summary>
        public bool HasMaterials(Recipe recipe, Inventory inventory)
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                if (!inventory.HasItem(ingredient.Key, ingredient.Value))
                    return false;
            }
            return true;
        }
        
        /// <summary>
        /// Check if player can craft a recipe (access + materials)
        /// </summary>
        public bool CanCraft(Recipe recipe, StructureType? workstation, CharacterStats stats)
        {
            return CanAccessRecipe(recipe, workstation, stats) && HasMaterials(recipe, stats.Inventory);
        }
        
        /// <summary>
        /// Get missing materials for a recipe
        /// </summary>
        public Dictionary<string, int> GetMissingMaterials(Recipe recipe, Inventory inventory)
        {
            var missing = new Dictionary<string, int>();
            
            foreach (var ingredient in recipe.Ingredients)
            {
                int have = inventory.GetItemCount(ingredient.Key);
                int need = ingredient.Value;
                
                if (have < need)
                {
                    missing[ingredient.Key] = need - have;
                }
            }
            
            return missing;
        }
        
        // ============================================
        // CRAFTING
        // ============================================
        
        /// <summary>
        /// Attempt to craft a recipe
        /// </summary>
        public Item TryCraft(Recipe recipe, StructureType? workstation, CharacterStats stats)
        {
            if (!CanCraft(recipe, workstation, stats))
            {
                System.Diagnostics.Debug.WriteLine($">>> CRAFTING: Cannot craft {recipe.Name} <<<");
                return null;
            }
            
            // Consume materials
            foreach (var ingredient in recipe.Ingredients)
            {
                stats.Inventory.RemoveItem(ingredient.Key, ingredient.Value);
            }
            
            // Calculate output quality based on INT
            ItemQuality quality = CalculateOutputQuality(recipe.BaseQuality, stats.Attributes.INT);
            
            // Create output item
            var outputItem = new Item(recipe.OutputItemId, quality, recipe.OutputCount);
            
            // Add to inventory
            if (stats.Inventory.TryAddItem(outputItem))
            {
                OnItemCrafted?.Invoke(recipe, outputItem);
                System.Diagnostics.Debug.WriteLine($">>> CRAFTED: {outputItem.GetDisplayName()} ({quality}) <<<");
                return outputItem;
            }
            else
            {
                // Inventory full - drop on ground? For now just fail
                System.Diagnostics.Debug.WriteLine($">>> CRAFTING: Inventory full, cannot add {recipe.Name} <<<");
                return null;
            }
        }
        
        /// <summary>
        /// Calculate output quality based on INT stat
        /// </summary>
        private ItemQuality CalculateOutputQuality(ItemQuality baseQuality, int intelligence)
        {
            // INT bonus: chance to upgrade quality
            // INT 5 = base quality
            // INT 7+ = chance for +1 quality
            // INT 10+ = chance for +2 quality
            
            Random rand = new Random();
            int qualityBonus = 0;
            
            if (intelligence >= 10 && rand.NextDouble() < 0.3)
                qualityBonus = 2;
            else if (intelligence >= 7 && rand.NextDouble() < 0.4)
                qualityBonus = 1;
            else if (intelligence >= 5 && rand.NextDouble() < 0.2)
                qualityBonus = 1;
            else if (intelligence < 4 && rand.NextDouble() < 0.3)
                qualityBonus = -1;  // Low INT can make worse quality
            
            int newQuality = (int)baseQuality + qualityBonus;
            newQuality = Math.Clamp(newQuality, (int)ItemQuality.Broken, (int)ItemQuality.Masterwork);
            
            return (ItemQuality)newQuality;
        }
        
        // ============================================
        // WORKSTATION HELPERS
        // ============================================
        
        /// <summary>
        /// Get the workstation type from a structure
        /// </summary>
        public static StructureType? GetWorkstationType(Structure structure)
        {
            if (structure == null) return null;
            if (structure.State != StructureState.Complete) return null;
            
            // Only workstation structures count
            if (structure.Definition.Category != StructureCategory.Workstation)
                return null;
            
            return structure.Type;
        }
        
        /// <summary>
        /// Check if a structure is a usable workstation
        /// </summary>
        public static bool IsWorkstation(Structure structure)
        {
            return GetWorkstationType(structure) != null;
        }
        
        /// <summary>
        /// Get display name for a workstation type
        /// </summary>
        public static string GetWorkstationName(StructureType? type)
        {
            return type switch
            {
                StructureType.CraftingBench => "Crafting Bench",
                StructureType.CookingStation => "Cooking Station",
                StructureType.ResearchTable => "Research Table",
                _ => "None"
            };
        }
    }
}
