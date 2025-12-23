// Gameplay/Systems/CraftingSystem.cs
// Crafting system with recipes, workstations, and quality mechanics

using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Data;
using MyRPG.Gameplay.Character;
using MyRPG.Gameplay.Items;

namespace MyRPG.Gameplay.Systems
{
    // ============================================
    // RECIPE INGREDIENT
    // ============================================
    
    public class RecipeIngredient
    {
        public string ItemId { get; set; }
        public int Amount { get; set; }
        
        public RecipeIngredient(string itemId, int amount = 1)
        {
            ItemId = itemId;
            Amount = amount;
        }
    }
    
    // ============================================
    // RECIPE DEFINITION
    // ============================================
    
    public class RecipeDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public RecipeCategory Category { get; set; }
        
        // Requirements
        public List<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
        public WorkstationType RequiredWorkstation { get; set; } = WorkstationType.None;
        public int RequiredLevel { get; set; } = 1;
        public int RequiredINT { get; set; } = 0;
        public SciencePath? RequiredScience { get; set; } = null;
        
        // Output
        public string OutputItemId { get; set; }
        public int OutputAmount { get; set; } = 1;
        
        // Crafting details
        public float CraftTime { get; set; } = 1f;  // In seconds
        public bool CanBatchCraft { get; set; } = true;
        public bool AffectedByQuality { get; set; } = true;
        
        // Unlocking
        public bool StartsUnlocked { get; set; } = true;
    }
    
    // ============================================
    // CRAFTING RESULT
    // ============================================
    
    public class CraftingResult
    {
        public bool Success { get; set; }
        public Item CraftedItem { get; set; }
        public ItemQuality Quality { get; set; }
        public string FailReason { get; set; }
        
        public static CraftingResult Fail(string reason) => new CraftingResult 
        { 
            Success = false, 
            FailReason = reason 
        };
        
        public static CraftingResult Succeed(Item item, ItemQuality quality) => new CraftingResult
        {
            Success = true,
            CraftedItem = item,
            Quality = quality
        };
    }
    
    // ============================================
    // CRAFTING SYSTEM
    // ============================================
    
    public class CraftingSystem
    {
        private Dictionary<string, RecipeDefinition> _recipes = new Dictionary<string, RecipeDefinition>();
        private HashSet<string> _unlockedRecipes = new HashSet<string>();
        private Random _random = new Random();
        
        // Events
        public event Action<RecipeDefinition, CraftingResult> OnItemCrafted;
        public event Action<string> OnRecipeUnlocked;
        
        public CraftingSystem()
        {
            InitializeRecipes();
        }
        
        // ============================================
        // INITIALIZATION
        // ============================================
        
        private void InitializeRecipes()
        {
            // ========== BASIC (No Workstation) ==========
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_torch",
                Name = "Torch",
                Description = "A simple light source",
                Category = RecipeCategory.Basic,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("wood", 2),
                    new RecipeIngredient("cloth", 1)
                },
                OutputItemId = "torch",
                OutputAmount = 1,
                StartsUnlocked = true,
                AffectedByQuality = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_bandage",
                Name = "Bandage",
                Description = "Basic wound dressing",
                Category = RecipeCategory.Consumables,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("cloth", 2)
                },
                OutputItemId = "bandage",
                OutputAmount = 2,
                StartsUnlocked = true,
                AffectedByQuality = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_shiv",
                Name = "Shiv",
                Description = "Crude but deadly",
                Category = RecipeCategory.Weapons,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("scrap_metal", 2),
                    new RecipeIngredient("cloth", 1)
                },
                OutputItemId = "knife_rusty",
                OutputAmount = 1,
                StartsUnlocked = true
            });
            
            // ========== CRAFTING BENCH ==========
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_pipe_club",
                Name = "Pipe Club",
                Description = "A crude but effective weapon",
                Category = RecipeCategory.Weapons,
                RequiredWorkstation = WorkstationType.CraftingBench,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("scrap_metal", 3),
                    new RecipeIngredient("cloth", 1)
                },
                OutputItemId = "pipe_club",
                OutputAmount = 1,
                StartsUnlocked = true
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_leather_armor",
                Name = "Leather Armor",
                Description = "Light protective gear",
                Category = RecipeCategory.Armor,
                RequiredWorkstation = WorkstationType.CraftingBench,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("leather", 5),
                    new RecipeIngredient("cloth", 2)
                },
                OutputItemId = "armor_leather",
                OutputAmount = 1,
                StartsUnlocked = true
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_combat_knife",
                Name = "Combat Knife",
                Description = "A well-made blade",
                Category = RecipeCategory.Weapons,
                RequiredWorkstation = WorkstationType.CraftingBench,
                RequiredINT = 5,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("scrap_metal", 4),
                    new RecipeIngredient("leather", 2),
                    new RecipeIngredient("bone", 1)
                },
                OutputItemId = "knife_combat",
                OutputAmount = 1,
                StartsUnlocked = true
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_makeshift_spear",
                Name = "Makeshift Spear",
                Description = "Good range, decent damage",
                Category = RecipeCategory.Weapons,
                RequiredWorkstation = WorkstationType.CraftingBench,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("wood", 3),
                    new RecipeIngredient("scrap_metal", 2),
                    new RecipeIngredient("sinew", 1)
                },
                OutputItemId = "spear_makeshift",
                OutputAmount = 1,
                StartsUnlocked = true
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_simple_bow",
                Name = "Simple Bow",
                Description = "Ranged weapon for hunting",
                Category = RecipeCategory.Weapons,
                RequiredWorkstation = WorkstationType.CraftingBench,
                RequiredINT = 5,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("wood", 4),
                    new RecipeIngredient("sinew", 3)
                },
                OutputItemId = "bow_simple",
                OutputAmount = 1,
                StartsUnlocked = true
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_arrows",
                Name = "Arrows",
                Description = "Basic ammunition",
                Category = RecipeCategory.Weapons,
                RequiredWorkstation = WorkstationType.CraftingBench,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("wood", 2),
                    new RecipeIngredient("stone", 1)
                },
                OutputItemId = "arrow_basic",
                OutputAmount = 10,
                StartsUnlocked = true,
                AffectedByQuality = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_machete",
                Name = "Machete",
                Description = "Heavy chopping blade",
                Category = RecipeCategory.Weapons,
                RequiredWorkstation = WorkstationType.CraftingBench,
                RequiredINT = 6,
                RequiredLevel = 3,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("metal", 4),
                    new RecipeIngredient("leather", 2)
                },
                OutputItemId = "machete",
                OutputAmount = 1,
                StartsUnlocked = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_raider_armor",
                Name = "Raider Armor",
                Description = "Scavenged protective gear",
                Category = RecipeCategory.Armor,
                RequiredWorkstation = WorkstationType.CraftingBench,
                RequiredLevel = 4,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("metal", 5),
                    new RecipeIngredient("leather", 4),
                    new RecipeIngredient("cloth", 2)
                },
                OutputItemId = "armor_raider",
                OutputAmount = 1,
                StartsUnlocked = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_hardhat",
                Name = "Hardhat",
                Description = "Basic head protection",
                Category = RecipeCategory.Armor,
                RequiredWorkstation = WorkstationType.CraftingBench,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("scrap_metal", 3),
                    new RecipeIngredient("cloth", 1)
                },
                OutputItemId = "helmet_hardhat",
                OutputAmount = 1,
                StartsUnlocked = true
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_tactical_gloves",
                Name = "Tactical Gloves",
                Description = "Improved grip and protection",
                Category = RecipeCategory.Armor,
                RequiredWorkstation = WorkstationType.CraftingBench,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("leather", 3),
                    new RecipeIngredient("cloth", 2)
                },
                OutputItemId = "gloves_tactical",
                OutputAmount = 1,
                StartsUnlocked = true
            });
            
            // ========== COOKING STATION ==========
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_cooked_steak",
                Name = "Cooked Steak",
                Description = "Nutritious and safe to eat",
                Category = RecipeCategory.Consumables,
                RequiredWorkstation = WorkstationType.CookingStation,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("mutant_meat", 1)
                },
                OutputItemId = "food_steak",
                OutputAmount = 1,
                StartsUnlocked = true,
                AffectedByQuality = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_stew",
                Name = "Hearty Stew",
                Description = "Restores health and hunger",
                Category = RecipeCategory.Consumables,
                RequiredWorkstation = WorkstationType.CookingStation,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("mutant_meat", 2),
                    new RecipeIngredient("herbs", 1),
                    new RecipeIngredient("salt", 1)
                },
                OutputItemId = "food_stew",
                OutputAmount = 2,
                StartsUnlocked = true,
                AffectedByQuality = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_medkit",
                Name = "Medical Kit",
                Description = "Professional healing supplies",
                Category = RecipeCategory.Consumables,
                RequiredWorkstation = WorkstationType.CookingStation,
                RequiredINT = 6,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("bandage", 3),
                    new RecipeIngredient("herbs", 2),
                    new RecipeIngredient("cloth", 2)
                },
                OutputItemId = "medkit",
                OutputAmount = 1,
                StartsUnlocked = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_stimpack",
                Name = "Stimpack",
                Description = "Emergency healing injection",
                Category = RecipeCategory.Consumables,
                RequiredWorkstation = WorkstationType.CookingStation,
                RequiredINT = 7,
                RequiredLevel = 5,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("herbs", 3),
                    new RecipeIngredient("essence", 1),
                    new RecipeIngredient("junk_bottle", 1)
                },
                OutputItemId = "stimpack",
                OutputAmount = 1,
                StartsUnlocked = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_antidote",
                Name = "Antidote",
                Description = "Cures poison",
                Category = RecipeCategory.Consumables,
                RequiredWorkstation = WorkstationType.CookingStation,
                RequiredINT = 6,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("herbs", 2),
                    new RecipeIngredient("mutagen", 1),
                    new RecipeIngredient("junk_bottle", 1)
                },
                OutputItemId = "antidote",
                OutputAmount = 1,
                StartsUnlocked = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_rad_away",
                Name = "Rad-Away",
                Description = "Removes radiation",
                Category = RecipeCategory.Consumables,
                RequiredWorkstation = WorkstationType.CookingStation,
                RequiredINT = 7,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("herbs", 2),
                    new RecipeIngredient("mutagen", 2),
                    new RecipeIngredient("junk_bottle", 1)
                },
                OutputItemId = "rad_away",
                OutputAmount = 1,
                StartsUnlocked = false
            });
            
            // ========== TINKER BENCH (Tinker Science) ==========
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_fire_axe",
                Name = "Fire Axe",
                Description = "Heavy weapon with fire damage",
                Category = RecipeCategory.Gadgets,
                RequiredWorkstation = WorkstationType.TinkerBench,
                RequiredScience = SciencePath.Tinker,
                RequiredINT = 8,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("metal", 5),
                    new RecipeIngredient("scrap_electronics", 2),
                    new RecipeIngredient("energy_cell", 1)
                },
                OutputItemId = "axe_fire",
                OutputAmount = 1,
                StartsUnlocked = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_psi_amplifier",
                Name = "Psi Amplifier",
                Description = "Enhances mental powers",
                Category = RecipeCategory.Gadgets,
                RequiredWorkstation = WorkstationType.TinkerBench,
                RequiredScience = SciencePath.Tinker,
                RequiredINT = 10,
                RequiredLevel = 6,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("scrap_electronics", 4),
                    new RecipeIngredient("essence", 2),
                    new RecipeIngredient("components", 3)
                },
                OutputItemId = "psi_amplifier",
                OutputAmount = 1,
                StartsUnlocked = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_exosuit",
                Name = "Exosuit",
                Description = "Powered armor suit",
                Category = RecipeCategory.Gadgets,
                RequiredWorkstation = WorkstationType.TinkerBench,
                RequiredScience = SciencePath.Tinker,
                RequiredINT = 12,
                RequiredLevel = 10,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("metal", 10),
                    new RecipeIngredient("scrap_electronics", 6),
                    new RecipeIngredient("components", 5),
                    new RecipeIngredient("energy_cell", 3)
                },
                OutputItemId = "armor_exosuit",
                OutputAmount = 1,
                StartsUnlocked = false
            });
            
            // ========== RITUAL CIRCLE (Dark Science) ==========
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_void_essence",
                Name = "Void Essence",
                Description = "Concentrated anomalous energy",
                Category = RecipeCategory.Anomalies,
                RequiredWorkstation = WorkstationType.RitualCircle,
                RequiredScience = SciencePath.Dark,
                RequiredINT = 8,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("anomaly_shard", 3),
                    new RecipeIngredient("essence", 1)
                },
                OutputItemId = "void_essence",
                OutputAmount = 1,
                StartsUnlocked = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_mutagen",
                Name = "Mutagen Serum",
                Description = "Induces controlled mutations",
                Category = RecipeCategory.Anomalies,
                RequiredWorkstation = WorkstationType.RitualCircle,
                RequiredScience = SciencePath.Dark,
                RequiredINT = 10,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("void_essence", 2),
                    new RecipeIngredient("herbs", 3),
                    new RecipeIngredient("brain_tissue", 1)
                },
                OutputItemId = "mutagen",
                OutputAmount = 2,
                StartsUnlocked = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_psi_crown",
                Name = "Psi Crown",
                Description = "Dark mental amplifier",
                Category = RecipeCategory.Anomalies,
                RequiredWorkstation = WorkstationType.RitualCircle,
                RequiredScience = SciencePath.Dark,
                RequiredINT = 12,
                RequiredLevel = 8,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("void_essence", 3),
                    new RecipeIngredient("anomaly_shard", 5),
                    new RecipeIngredient("brain_tissue", 2)
                },
                OutputItemId = "psi_crown",
                OutputAmount = 1,
                StartsUnlocked = false
            });
            
            // ========== MATERIALS ==========
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_leather_from_hide",
                Name = "Leather",
                Description = "Process raw materials",
                Category = RecipeCategory.Materials,
                RequiredWorkstation = WorkstationType.CraftingBench,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("bone", 2),
                    new RecipeIngredient("salt", 1)
                },
                OutputItemId = "leather",
                OutputAmount = 2,
                StartsUnlocked = true,
                AffectedByQuality = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_metal_from_scrap",
                Name = "Refined Metal",
                Description = "Process scrap into usable metal",
                Category = RecipeCategory.Materials,
                RequiredWorkstation = WorkstationType.CraftingBench,
                RequiredINT = 5,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("scrap_metal", 3)
                },
                OutputItemId = "metal",
                OutputAmount = 1,
                StartsUnlocked = true,
                AffectedByQuality = false
            });
            
            AddRecipe(new RecipeDefinition
            {
                Id = "recipe_components",
                Name = "Components",
                Description = "Salvage electronics for parts",
                Category = RecipeCategory.Materials,
                RequiredWorkstation = WorkstationType.CraftingBench,
                RequiredINT = 6,
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient("scrap_electronics", 2)
                },
                OutputItemId = "components",
                OutputAmount = 1,
                StartsUnlocked = true,
                AffectedByQuality = false
            });
            
            // Unlock starting recipes
            foreach (var recipe in _recipes.Values.Where(r => r.StartsUnlocked))
            {
                _unlockedRecipes.Add(recipe.Id);
            }
        }
        
        private void AddRecipe(RecipeDefinition recipe)
        {
            _recipes[recipe.Id] = recipe;
        }
        
        // ============================================
        // PUBLIC METHODS
        // ============================================
        
        public RecipeDefinition GetRecipe(string recipeId)
        {
            return _recipes.TryGetValue(recipeId, out var recipe) ? recipe : null;
        }
        
        public List<RecipeDefinition> GetAllRecipes()
        {
            return _recipes.Values.ToList();
        }
        
        public List<RecipeDefinition> GetAvailableRecipes(StructureType? workstationType, CharacterStats stats)
        {
            var wsType = StructureToWorkstation(workstationType);
            
            return _recipes.Values
                .Where(r => IsRecipeAvailable(r, wsType, stats))
                .OrderBy(r => r.Category)
                .ThenBy(r => r.RequiredLevel)
                .ToList();
        }
        
        public List<RecipeDefinition> GetRecipesByCategory(RecipeCategory category)
        {
            return _recipes.Values.Where(r => r.Category == category).ToList();
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
            }
        }
        
        public bool IsRecipeAvailable(RecipeDefinition recipe, WorkstationType currentWorkstation, CharacterStats stats)
        {
            // Must be unlocked
            if (!_unlockedRecipes.Contains(recipe.Id))
                return false;
            
            // Check workstation
            if (recipe.RequiredWorkstation != WorkstationType.None && 
                recipe.RequiredWorkstation != currentWorkstation)
                return false;
            
            // Check level
            if (stats.Level < recipe.RequiredLevel)
                return false;
            
            // Check INT
            if (stats.Attributes.INT < recipe.RequiredINT)
                return false;
            
            // Check science path
            if (recipe.RequiredScience.HasValue && stats.SciencePath != recipe.RequiredScience.Value)
                return false;
            
            return true;
        }
        
        public bool CanCraft(RecipeDefinition recipe, StructureType? workstationType, CharacterStats stats)
        {
            var wsType = StructureToWorkstation(workstationType);
            
            if (!IsRecipeAvailable(recipe, wsType, stats))
                return false;
            
            // Check ingredients
            foreach (var ingredient in recipe.Ingredients)
            {
                int playerHas = stats.Inventory.GetItemCount(ingredient.ItemId);
                if (playerHas < ingredient.Amount)
                    return false;
            }
            
            return true;
        }
        
        public bool HasMaterials(RecipeDefinition recipe, Inventory inventory)
        {
            if (recipe == null || inventory == null)
                return false;
                
            foreach (var ingredient in recipe.Ingredients)
            {
                int playerHas = inventory.GetItemCount(ingredient.ItemId);
                if (playerHas < ingredient.Amount)
                    return false;
            }
            
            return true;
        }
        
        public CraftingResult TryCraft(RecipeDefinition recipe, StructureType? workstationType, CharacterStats stats)
        {
            // Check if can craft
            if (!CanCraft(recipe, workstationType, stats))
            {
                // Find specific reason
                foreach (var ingredient in recipe.Ingredients)
                {
                    int playerHas = stats.Inventory.GetItemCount(ingredient.ItemId);
                    if (playerHas < ingredient.Amount)
                    {
                        var itemDef = ItemDatabase.Get(ingredient.ItemId);
                        string itemName = itemDef?.Name ?? ingredient.ItemId;
                        return CraftingResult.Fail($"Need {ingredient.Amount} {itemName} (have {playerHas})");
                    }
                }
                return CraftingResult.Fail("Cannot craft this recipe");
            }
            
            // Consume ingredients
            foreach (var ingredient in recipe.Ingredients)
            {
                stats.Inventory.RemoveItem(ingredient.ItemId, ingredient.Amount);
            }
            
            // Determine quality
            ItemQuality quality = ItemQuality.Normal;
            if (recipe.AffectedByQuality)
            {
                quality = RollQuality(stats.Attributes.INT);
            }
            
            // Create item(s)
            var outputDef = ItemDatabase.Get(recipe.OutputItemId);
            if (outputDef == null)
            {
                return CraftingResult.Fail($"Unknown item: {recipe.OutputItemId}");
            }
            
            var craftedItem = new Item(recipe.OutputItemId, quality, recipe.OutputAmount);
            
            // Add to inventory
            stats.Inventory.TryAddItem(craftedItem);
            
            var result = CraftingResult.Succeed(craftedItem, quality);
            OnItemCrafted?.Invoke(recipe, result);
            
            return result;
        }
        
        // ============================================
        // QUALITY SYSTEM
        // ============================================
        
        private ItemQuality RollQuality(int intelligence)
        {
            // Base chances modified by INT
            // INT 5 = baseline, each point above/below shifts chances
            int intBonus = intelligence - 5;
            
            int roll = _random.Next(100);
            
            // Adjust roll by INT (higher INT = better quality)
            roll += intBonus * 5;
            
            if (roll >= 95)
                return ItemQuality.Masterwork;
            else if (roll >= 80)
                return ItemQuality.Excellent;
            else if (roll >= 55)
                return ItemQuality.Good;
            else if (roll >= 20)
                return ItemQuality.Normal;
            else
                return ItemQuality.Poor;
        }
        
        public static float GetQualityMultiplier(ItemQuality quality)
        {
            return quality switch
            {
                ItemQuality.Poor => 0.8f,
                ItemQuality.Normal => 1.0f,
                ItemQuality.Good => 1.15f,
                ItemQuality.Excellent => 1.3f,
                ItemQuality.Masterwork => 1.5f,
                _ => 1.0f
            };
        }
        
        public static string GetQualityName(ItemQuality quality)
        {
            return quality switch
            {
                ItemQuality.Poor => "Poor",
                ItemQuality.Normal => "Normal",
                ItemQuality.Good => "Good",
                ItemQuality.Excellent => "Excellent",
                ItemQuality.Masterwork => "Masterwork",
                _ => "Unknown"
            };
        }
        
        public static Microsoft.Xna.Framework.Color GetQualityColor(ItemQuality quality)
        {
            return quality switch
            {
                ItemQuality.Poor => Microsoft.Xna.Framework.Color.Gray,
                ItemQuality.Normal => Microsoft.Xna.Framework.Color.White,
                ItemQuality.Good => Microsoft.Xna.Framework.Color.Green,
                ItemQuality.Excellent => Microsoft.Xna.Framework.Color.Blue,
                ItemQuality.Masterwork => Microsoft.Xna.Framework.Color.Gold,
                _ => Microsoft.Xna.Framework.Color.White
            };
        }
        
        // ============================================
        // WORKSTATION HELPERS
        // ============================================
        
        public static WorkstationType StructureToWorkstation(StructureType? structureType)
        {
            if (!structureType.HasValue)
                return WorkstationType.None;
            
            return structureType.Value switch
            {
                StructureType.CraftingBench => WorkstationType.CraftingBench,
                StructureType.CookingStation => WorkstationType.CookingStation,
                // Add more as structures are added
                _ => WorkstationType.None
            };
        }
        
        public static WorkstationType GetWorkstationType(Building.Structure structure)
        {
            if (structure == null)
                return WorkstationType.None;
            
            return StructureToWorkstation(structure.Type);
        }
        
        public static bool IsWorkstation(Building.Structure structure)
        {
            if (structure == null)
                return false;
            
            return structure.Type switch
            {
                StructureType.CraftingBench => true,
                StructureType.CookingStation => true,
                StructureType.ResearchTable => true,
                _ => false
            };
        }
        
        public static string GetWorkstationName(StructureType? structureType)
        {
            if (!structureType.HasValue)
                return "Basic Crafting";
            
            return structureType.Value switch
            {
                StructureType.CraftingBench => "Crafting Bench",
                StructureType.CookingStation => "Cooking Station",
                // Add more as structures are added
                _ => "Basic Crafting"
            };
        }
        
        public static string GetWorkstationName(WorkstationType workstation)
        {
            return workstation switch
            {
                WorkstationType.None => "Basic Crafting",
                WorkstationType.CraftingBench => "Crafting Bench",
                WorkstationType.Forge => "Forge",
                WorkstationType.CookingStation => "Cooking Station",
                WorkstationType.AlchemyTable => "Alchemy Table",
                WorkstationType.TinkerBench => "Tinker Bench",
                WorkstationType.RitualCircle => "Ritual Circle",
                _ => "Unknown"
            };
        }
        
        // ============================================
        // INGREDIENT HELPERS
        // ============================================
        
        public string GetMissingIngredients(RecipeDefinition recipe, CharacterStats stats)
        {
            var missing = new List<string>();
            
            foreach (var ingredient in recipe.Ingredients)
            {
                int playerHas = stats.Inventory.GetItemCount(ingredient.ItemId);
                if (playerHas < ingredient.Amount)
                {
                    var itemDef = ItemDatabase.Get(ingredient.ItemId);
                    string itemName = itemDef?.Name ?? ingredient.ItemId;
                    missing.Add($"{itemName}: {playerHas}/{ingredient.Amount}");
                }
            }
            
            return missing.Count > 0 ? string.Join(", ", missing) : null;
        }
        
        public List<(string ItemName, int Have, int Need)> GetIngredientStatus(RecipeDefinition recipe, CharacterStats stats)
        {
            var result = new List<(string, int, int)>();
            
            foreach (var ingredient in recipe.Ingredients)
            {
                int playerHas = stats.Inventory.GetItemCount(ingredient.ItemId);
                var itemDef = ItemDatabase.Get(ingredient.ItemId);
                string itemName = itemDef?.Name ?? ingredient.ItemId;
                result.Add((itemName, playerHas, ingredient.Amount));
            }
            
            return result;
        }
        
        // ============================================
        // RESET
        // ============================================
        
        public void Reset()
        {
            _unlockedRecipes.Clear();
            foreach (var recipe in _recipes.Values.Where(r => r.StartsUnlocked))
            {
                _unlockedRecipes.Add(recipe.Id);
            }
        }
    }
}
