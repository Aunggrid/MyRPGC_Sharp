using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Engine;
using MyRPG.Gameplay.World;
using MyRPG.Gameplay.Entities;
using MyRPG.Gameplay.Systems;
using MyRPG.Gameplay.Combat;
using MyRPG.Gameplay.Character;
using MyRPG.Gameplay.Items;
using MyRPG.Gameplay.Building;
using MyRPG.Data;

namespace MyRPG
{
    // ============================================
    // GAME STATE
    // ============================================
    public enum GameState
    {
        CharacterCreation,  // NEW: Choose science path (random backstory/traits)
        AttributeSelect,    // NEW: Choose attribute on level up
        Playing,            // Normal gameplay (exploration or combat)
        MutationSelect,     // Choosing a mutation
        VitalOrganChoice,   // Choosing where to relocate vital organ damage
        GameOver,           // Player died
        Paused              // Game paused (future use)
    }
    
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Camera2D _camera;
        private WorldGrid _world;
        private ZoneManager _zoneManager;
        private PlayerEntity _player;
        
        // GAME STATE
        private GameState _gameState = GameState.CharacterCreation;
        
        // ENEMIES
        private List<EnemyEntity> _enemies = new List<EnemyEntity>();
        
        // COMBAT
        private CombatManager _combat;
        private List<string> _combatLog = new List<string>();
        private const int MAX_LOG_LINES = 5;
        
        // CHARACTER CREATION
        private CharacterBuild _pendingBuild;
        private int _selectedSciencePathIndex = 0; // 0 = Tinker, 1 = Dark
        
        // TOOLTIP SYSTEM
        private string _tooltipTitle = "";
        private string _tooltipText = "";
        private bool _showTooltip = false;
        private Vector2 _tooltipPosition;
        
        // UI THEME COLORS
        private static class UITheme
        {
            // Panel colors
            public static Color PanelBackground = new Color(20, 25, 35) * 0.95f;
            public static Color PanelBorder = new Color(80, 90, 110);
            public static Color PanelHeader = new Color(40, 50, 70);
            
            // Text colors
            public static Color TextPrimary = new Color(220, 225, 230);
            public static Color TextSecondary = new Color(150, 160, 170);
            public static Color TextHighlight = new Color(100, 180, 255);
            public static Color TextWarning = new Color(255, 200, 80);
            public static Color TextDanger = new Color(255, 100, 100);
            public static Color TextSuccess = new Color(100, 255, 150);
            
            // Selection colors
            public static Color SelectionBackground = new Color(60, 80, 120);
            public static Color SelectionBorder = new Color(100, 150, 220);
            public static Color HoverBackground = new Color(50, 60, 80);
            
            // Button colors
            public static Color ButtonNormal = new Color(50, 60, 80);
            public static Color ButtonHover = new Color(70, 85, 110);
            public static Color ButtonPressed = new Color(40, 50, 70);
            public static Color ButtonDisabled = new Color(40, 45, 55);
            
            // Category colors (for items, etc)
            public static Color CategoryWeapon = new Color(200, 80, 80);
            public static Color CategoryArmor = new Color(80, 130, 200);
            public static Color CategoryConsumable = new Color(80, 200, 100);
            public static Color CategoryMaterial = new Color(180, 150, 100);
            public static Color CategoryAmmo = new Color(220, 180, 60);
        }
        
        // ATTRIBUTE SELECTION (on level up)
        private int _selectedAttributeIndex = 0;
        
        // MUTATION SELECTION
        private List<MutationDefinition> _mutationChoices = new List<MutationDefinition>();
        private int _selectedMutationIndex = 0;
        private bool _usingFreePick = false;

        // --- ASSETS ---
        private Texture2D _pixelTexture;
        private SpriteFont _font;

        // Input States
        private MouseState _prevMouseState;
        private KeyboardState _prevKeyboardState;
        
        // Selected enemy (for targeting)
        private EnemyEntity _selectedEnemy = null;
        
        // Death animation timer
        private float _deathTimer = 0f;
        private const float DEATH_DELAY = 2f; // Seconds before game over screen
        
        // BUILD MODE
        private bool _buildMenuOpen = false;
        private int _buildCategoryIndex = 0;
        private int _buildItemIndex = 0;
        private StructureCategory[] _buildCategories = new StructureCategory[]
        {
            StructureCategory.Wall,
            StructureCategory.Door,
            StructureCategory.Floor,
            StructureCategory.Furniture,
            StructureCategory.Workstation,
            StructureCategory.Light,
            StructureCategory.Defense
        };
        
        // GROUND ITEMS (dropped loot, placed items)
        private List<WorldItem> _groundItems = new List<WorldItem>();
        private WorldItem _nearestItem = null;  // For pickup highlight
        
        // INVENTORY UI
        private bool _inventoryOpen = false;
        private int _selectedInventoryIndex = 0;
        
        // CRAFTING UI
        private bool _craftingOpen = false;
        private int _selectedRecipeIndex = 0;
        private Structure _nearestWorkstation = null;
        private StructureType? _activeWorkstationType = null;
        
        // NPCs & TRADING
        private List<NPCEntity> _npcs = new List<NPCEntity>();
        private NPCEntity _nearestNPC = null;
        private bool _tradingOpen = false;
        private NPCEntity _tradingNPC = null;
        private int _selectedTradeIndex = 0;
        private bool _tradingBuyMode = true;  // true = buying from merchant, false = selling
        
        // QUEST SYSTEM
        private bool _questLogOpen = false;
        private int _selectedQuestIndex = 0;
        private bool _questDialogueOpen = false;
        private List<QuestDefinition> _availableNPCQuests = new List<QuestDefinition>();
        private List<QuestInstance> _turnInQuests = new List<QuestInstance>();
        private int _selectedDialogueIndex = 0;
        
        // INSPECT SYSTEM (right-click to inspect)
        private bool _inspectPanelOpen = false;
        private object _inspectedObject = null;  // Can be EnemyEntity, NPCEntity, Structure, Tile, etc.
        private Vector2 _inspectPanelPosition = Vector2.Zero;
        
        // RESEARCH SYSTEM
        private bool _researchOpen = false;
        private int _selectedResearchCategory = 0;  // 0=Survival, 1=Combat, 2=Tinker/Dark
        private int _selectedResearchIndex = 0;
        private ResearchCategory[] _researchCategories;
        
        // NOTIFICATIONS
        private string _notificationText = "";
        private float _notificationTimer = 0f;
        private const float NOTIFICATION_DURATION = 3f;
        
        // BODY PANEL UI (press P)
        private bool _bodyPanelOpen = false;
        private string _selectedBodyPartId = null;
        private int _bodyPanelScroll = 0;
        
        // DRAG & DROP SYSTEM
        private bool _isDragging = false;
        private Item _draggedItem = null;
        private int _dragSourceIndex = -1;  // Index in inventory
        private Vector2 _dragOffset = Vector2.Zero;
        private Vector2 _dragPosition = Vector2.Zero;
        private BodyPart _hoverBodyPart = null;  // Body part mouse is over

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
        }

        protected override void Initialize()
        {
            StartNewGame();
            base.Initialize();
        }
        
        private void StartNewGame()
        {
            // Initialize Game Services (only once)
            if (!GameServices.IsInitialized)
            {
                GameServices.Initialize();
            }
            
            _camera = new Camera2D(GraphicsDevice.Viewport);
            _camera.Position = new Vector2(400, 400);

            // Initialize zone manager first
            _zoneManager = new ZoneManager(64);  // 64 = tile size
            
            // Create world grid based on current zone
            var currentZone = _zoneManager.CurrentZone;
            _world = new WorldGrid(currentZone.Width, currentZone.Height, GraphicsDevice);
            _zoneManager.GenerateZoneWorld(_world, currentZone);

            // Create player entity (but don't initialize stats yet - wait for science path choice)
            _player = new PlayerEntity();
            _player.Position = new Vector2(5 * _world.TileSize, 5 * _world.TileSize);  // Safe spawn point
            
            // Generate random character build for player to review
            _pendingBuild = GameServices.Traits.GenerateRandomBuild();
            _selectedSciencePathIndex = 0;
            
            // Spawn enemies for this zone
            _enemies = _zoneManager.GenerateZoneEnemies(currentZone, _world.TileSize);
            
            // Spawn NPCs for this zone
            SpawnNPCsForZone(currentZone);
            
            // Initialize combat manager (player will be initialized later)
            _combat = new CombatManager(_player, _enemies, _world.TileSize);
            _combat.OnCombatLog += (msg) =>
            {
                _combatLog.Add(msg);
                if (_combatLog.Count > MAX_LOG_LINES)
                    _combatLog.RemoveAt(0);
            };
            _combat.OnEnemyKilled += HandleEnemyKilled;
            _combat.OnEnemySpawn += HandleEnemySpawn;
            
            // Reset state - start at character creation!
            _gameState = GameState.CharacterCreation;
            _deathTimer = 0f;
            _combatLog.Clear();
            _selectedEnemy = null;
            _mutationChoices.Clear();
            _selectedAttributeIndex = 0;
            _groundItems.Clear();
            _inventoryOpen = false;
            _questLogOpen = false;
            _questDialogueOpen = false;
            _researchOpen = false;
            _bodyPanelOpen = false;
            _isDragging = false;
            _draggedItem = null;
            
            // Reset quest and research systems
            GameServices.Quests.Reset();
            GameServices.Research.Reset();
            
            System.Diagnostics.Debug.WriteLine(">>> NEW GAME - CHARACTER CREATION <<<");
        }
        
        /// <summary>
        /// Finalize character and start playing
        /// </summary>
        private void FinalizeCharacterCreation()
        {
            SciencePath chosenPath = _selectedSciencePathIndex == 0 ? SciencePath.Tinker : SciencePath.Dark;
            _player.Initialize(_pendingBuild, chosenPath);
            
            // Set up research categories based on chosen path
            GameServices.Research.SetPlayerPath(chosenPath);
            _researchCategories = chosenPath == SciencePath.Tinker
                ? new[] { ResearchCategory.Survival, ResearchCategory.Combat, ResearchCategory.Tinker }
                : new[] { ResearchCategory.Survival, ResearchCategory.Combat, ResearchCategory.Dark };
            
            // Subscribe to quest events
            GameServices.Quests.OnQuestStarted += (q) => ShowNotification($"Quest Started: {q.Definition.Name}");
            GameServices.Quests.OnQuestCompleted += (q) => 
            {
                if (q.Definition.AutoComplete)
                {
                    // Auto turn-in
                    GameServices.Quests.TurnInQuest(q.QuestId, (reward) =>
                    {
                        _player.Stats.Gold += reward.Gold;
                        _player.Stats.AddXP(reward.XP);
                        if (reward.MutationPoints > 0)
                            _player.Stats.AddMutationPoints(reward.MutationPoints);
                        foreach (var item in reward.Items)
                            _player.Stats.Inventory.TryAddItem(item.Key, item.Value);
                        foreach (var recipe in reward.UnlockRecipes)
                            GameServices.Crafting.UnlockRecipe(recipe);
                    });
                    ShowNotification($"Quest Complete: {q.Definition.Name}!");
                }
                else
                {
                    ShowNotification($"Quest Ready: {q.Definition.Name} - Return to turn in!");
                }
            };
            GameServices.Quests.OnObjectiveComplete += (q, obj) => ShowNotification($"Objective Complete: {obj.Description}");
            
            // Subscribe to research events
            GameServices.Research.OnResearchCompleted += (node) =>
            {
                ShowNotification($"Research Complete: {node.Name}!");
                // Unlock recipes
                foreach (var recipe in node.UnlocksRecipes)
                    GameServices.Crafting.UnlockRecipe(recipe);
            };
            
            // Auto-accept starting quest
            GameServices.Quests.AcceptQuest("main_welcome");
            
            _gameState = GameState.Playing;
            System.Diagnostics.Debug.WriteLine($">>> CHARACTER FINALIZED - Path: {chosenPath} <<<");
            System.Diagnostics.Debug.WriteLine(_player.Stats.GetStatusReport());
        }
        
        /// <summary>
        /// Load quick save and restore game state
        /// </summary>
        private void LoadQuickSave()
        {
            if (!SaveSystem.QuickSaveExists())
            {
                ShowNotification("No Save Found!");
                System.Diagnostics.Debug.WriteLine(">>> NO QUICKSAVE FOUND <<<");
                return;
            }
            
            var saveData = SaveSystem.QuickLoad();
            if (saveData == null)
            {
                ShowNotification("Load Failed!");
                System.Diagnostics.Debug.WriteLine(">>> LOAD FAILED <<<");
                return;
            }
            
            try
            {
                // Restore time
                SaveSystem.RestoreTime(GameServices.SurvivalSystem, saveData.Time);
                
                // Restore player
                SaveSystem.RestorePlayer(_player, saveData.Player, GameServices.Mutations);
                
                // Restore structures
                SaveSystem.RestoreStructures(GameServices.Building, _world, saveData.World.Structures);
                
                // Restore enemies
                _enemies = SaveSystem.RestoreEnemies(saveData.Enemies);
                _combat.UpdateEnemyList(_enemies);
                
                // Restore ground items
                _groundItems = SaveSystem.RestoreGroundItems(saveData.GroundItems);
                
                // Restore quests
                SaveSystem.RestoreQuests(saveData.Quests);
                
                // Reset UI state
                _inventoryOpen = false;
                _buildMenuOpen = false;
                _selectedEnemy = null;
                _gameState = GameState.Playing;
                
                ShowNotification("Game Loaded!");
                System.Diagnostics.Debug.WriteLine($">>> GAME LOADED! Save from: {saveData.SaveTime} <<<");
            }
            catch (Exception ex)
            {
                ShowNotification("Load Error!");
                System.Diagnostics.Debug.WriteLine($">>> LOAD ERROR: {ex.Message} <<<");
            }
        }
        
        /// <summary>
        /// Show a temporary notification on screen
        /// </summary>
        private void ShowNotification(string text)
        {
            _notificationText = text;
            _notificationTimer = NOTIFICATION_DURATION;
        }
        
        // ============================================
        // UI HELPER METHODS
        // ============================================
        
        /// <summary>
        /// Check if any menu is currently open (for blocking movement)
        /// </summary>
        private bool AnyMenuOpen => _inventoryOpen || _craftingOpen || _tradingOpen || 
                                    _questLogOpen || _researchOpen || _buildMenuOpen ||
                                    _questDialogueOpen || _bodyPanelOpen;
        
        /// <summary>
        /// Draw a themed panel with header
        /// </summary>
        private void DrawPanel(Rectangle bounds, string title = null)
        {
            // Background
            _spriteBatch.Draw(_pixelTexture, bounds, UITheme.PanelBackground);
            
            // Border
            DrawBorder(bounds, UITheme.PanelBorder, 2);
            
            // Header bar if title provided
            if (!string.IsNullOrEmpty(title))
            {
                Rectangle headerRect = new Rectangle(bounds.X, bounds.Y, bounds.Width, 30);
                _spriteBatch.Draw(_pixelTexture, headerRect, UITheme.PanelHeader);
                
                // Title text centered
                Vector2 titleSize = _font.MeasureString(title);
                Vector2 titlePos = new Vector2(bounds.X + (bounds.Width - titleSize.X) / 2, bounds.Y + 7);
                _spriteBatch.DrawString(_font, title, titlePos, UITheme.TextHighlight);
                
                // Header bottom border
                _spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y + 29, bounds.Width, 1), UITheme.PanelBorder);
            }
        }
        
        /// <summary>
        /// Draw a border around a rectangle
        /// </summary>
        private void DrawBorder(Rectangle bounds, Color color, int thickness = 1)
        {
            // Top
            _spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
            // Bottom
            _spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
            // Left
            _spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
            // Right
            _spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
        }
        
        /// <summary>
        /// Draw a button with hover state (visual only - click detection must be in Update methods)
        /// </summary>
        private void DrawButton(Rectangle bounds, string text, MouseState mState, bool enabled = true)
        {
            bool isHovered = bounds.Contains(mState.X, mState.Y);
            
            // Background
            Color bgColor = !enabled ? UITheme.ButtonDisabled :
                           isHovered ? UITheme.ButtonHover : UITheme.ButtonNormal;
            _spriteBatch.Draw(_pixelTexture, bounds, bgColor);
            
            // Border
            Color borderColor = isHovered && enabled ? UITheme.SelectionBorder : UITheme.PanelBorder;
            DrawBorder(bounds, borderColor, 1);
            
            // Text centered
            Vector2 textSize = _font.MeasureString(text);
            Vector2 textPos = new Vector2(
                bounds.X + (bounds.Width - textSize.X) / 2,
                bounds.Y + (bounds.Height - textSize.Y) / 2
            );
            Color textColor = enabled ? (isHovered ? UITheme.TextHighlight : UITheme.TextPrimary) : UITheme.TextSecondary;
            _spriteBatch.DrawString(_font, text, textPos, textColor);
        }
        
        /// <summary>
        /// Draw a selectable list item with hover support
        /// </summary>
        private bool DrawListItem(Rectangle bounds, string text, bool isSelected, MouseState mState, Color? iconColor = null)
        {
            bool isHovered = bounds.Contains(mState.X, mState.Y);
            bool isClicked = isHovered && mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released;
            
            // Background
            Color bgColor = isSelected ? UITheme.SelectionBackground :
                           isHovered ? UITheme.HoverBackground : Color.Transparent;
            _spriteBatch.Draw(_pixelTexture, bounds, bgColor);
            
            // Selection indicator
            if (isSelected)
            {
                _spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 3, bounds.Height), UITheme.SelectionBorder);
            }
            
            // Icon if provided
            int textOffset = 5;
            if (iconColor.HasValue)
            {
                Rectangle iconRect = new Rectangle(bounds.X + 5, bounds.Y + (bounds.Height - 16) / 2, 16, 16);
                _spriteBatch.Draw(_pixelTexture, iconRect, iconColor.Value);
                textOffset = 26;
            }
            
            // Text
            Color textColor = isSelected ? UITheme.TextHighlight : (isHovered ? UITheme.TextPrimary : UITheme.TextSecondary);
            _spriteBatch.DrawString(_font, text, new Vector2(bounds.X + textOffset, bounds.Y + (bounds.Height - 16) / 2), textColor);
            
            return isClicked;
        }
        
        /// <summary>
        /// Draw help text at bottom of screen
        /// </summary>
        private void DrawHelpBar(string text)
        {
            Rectangle helpRect = new Rectangle(0, 685, 1280, 35);
            _spriteBatch.Draw(_pixelTexture, helpRect, UITheme.PanelHeader * 0.8f);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 685, 1280, 1), UITheme.PanelBorder);
            
            Vector2 textSize = _font.MeasureString(text);
            Vector2 textPos = new Vector2(640 - textSize.X / 2, 693);
            _spriteBatch.DrawString(_font, text, textPos, UITheme.TextSecondary);
        }
        
        private void SpawnNPCsForZone(ZoneData zone)
        {
            _npcs.Clear();
            
            // Restore saved NPC states if available
            if (zone.SavedNPCs.Count > 0)
            {
                foreach (var saved in zone.SavedNPCs)
                {
                    NPCEntity npc;
                    switch (saved.Type)
                    {
                        case NPCType.Merchant:
                            npc = NPCEntity.CreateGeneralMerchant($"restored_{saved.Name}", saved.Position);
                            break;
                        case NPCType.Wanderer:
                            npc = NPCEntity.CreateWanderer($"restored_{saved.Name}", saved.Position);
                            break;
                        default:
                            npc = NPCEntity.CreateGeneralMerchant($"restored_{saved.Name}", saved.Position);
                            break;
                    }
                    npc.Name = saved.Name;
                    _npcs.Add(npc);
                }
                System.Diagnostics.Debug.WriteLine($">>> Restored {_npcs.Count} NPCs from saved state <<<");
                return;
            }
            
            if (!zone.HasMerchant) 
            {
                System.Diagnostics.Debug.WriteLine($">>> Zone {zone.Name} has no merchants <<<");
                return;
            }
            
            Random rand = new Random(zone.Seed + 2000);
            
            // Spawn merchants based on zone type
            switch (zone.Type)
            {
                case ZoneType.Settlement:
                    // Settlement has multiple merchants
                    _npcs.Add(NPCEntity.CreateGeneralMerchant($"trader_{zone.Id}", 
                        new Vector2(zone.Width / 2 * _world.TileSize, (zone.Height / 2 - 3) * _world.TileSize)));
                    _npcs.Add(NPCEntity.CreateWeaponsMerchant($"arms_{zone.Id}", 
                        new Vector2((zone.Width / 2 + 5) * _world.TileSize, (zone.Height / 2) * _world.TileSize)));
                    break;
                    
                case ZoneType.Ruins:
                    // Scavenger in ruins
                    _npcs.Add(NPCEntity.CreateWanderer($"scav_{zone.Id}", 
                        new Vector2(rand.Next(10, zone.Width - 10) * _world.TileSize, rand.Next(10, zone.Height - 10) * _world.TileSize)));
                    break;
                    
                default:
                    // Generic trader
                    _npcs.Add(NPCEntity.CreateGeneralMerchant($"trader_{zone.Id}", 
                        new Vector2(rand.Next(8, zone.Width - 8) * _world.TileSize, rand.Next(8, zone.Height - 8) * _world.TileSize)));
                    break;
            }
            
            System.Diagnostics.Debug.WriteLine($">>> Spawned {_npcs.Count} NPCs in {zone.Name} <<<");
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            _font = Content.Load<SpriteFont>("SystemFont");
        }

        protected override void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _totalTime += deltaTime;
            
            // Update notification timer
            if (_notificationTimer > 0)
            {
                _notificationTimer -= deltaTime;
            }
            
            KeyboardState kState = Keyboard.GetState();
            MouseState mState = Mouse.GetState();
            
            // Escape closes UI or exits game (only if no UI is open)
            if (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                // Cancel drag first
                if (_isDragging)
                {
                    _isDragging = false;
                    _draggedItem = null;
                    _dragSourceIndex = -1;
                }
                // Close UI overlays first (in order of priority)
                else if (_questDialogueOpen) { _questDialogueOpen = false; }
                else if (_questLogOpen) { _questLogOpen = false; }
                else if (_researchOpen) { _researchOpen = false; }
                else if (_bodyPanelOpen) { _bodyPanelOpen = false; }
                else if (_tradingOpen) { _tradingOpen = false; _tradingNPC = null; }
                else if (_craftingOpen) { _craftingOpen = false; }
                else if (_inventoryOpen) { _inventoryOpen = false; }
                else if (_buildMenuOpen) { _buildMenuOpen = false; }
                else if (GameServices.Building.InBuildMode) { GameServices.Building.ExitBuildMode(); }
                // Only exit game if nothing else is open and we're in Playing state
                // (removed auto-exit - player can use Alt+F4 or window close button)
            }

            // Handle different game states
            switch (_gameState)
            {
                case GameState.CharacterCreation:
                    UpdateCharacterCreation(kState, mState);
                    break;
                    
                case GameState.AttributeSelect:
                    UpdateAttributeSelect(kState, mState);
                    break;
                    
                case GameState.Playing:
                    // Check for pending vital organ damage
                    if (_player.Stats.PendingVitalOrganDamage != null)
                    {
                        _gameState = GameState.VitalOrganChoice;
                        break;
                    }
                    UpdatePlaying(deltaTime, kState, mState);
                    break;
                    
                case GameState.MutationSelect:
                    UpdateMutationSelect(kState);
                    break;
                    
                case GameState.VitalOrganChoice:
                    UpdateVitalOrganChoice(kState, mState);
                    break;
                    
                case GameState.GameOver:
                    UpdateGameOver(kState);
                    break;
            }
            
            _prevKeyboardState = kState;
            _prevMouseState = mState;
            
            base.Update(gameTime);
        }
        
        // ============================================
        // CHARACTER CREATION STATE
        // ============================================
        
        private void UpdateCharacterCreation(KeyboardState kState, MouseState mState)
        {
            // Reset tooltip
            _showTooltip = false;
            
            // Reroll character with R
            if (kState.IsKeyDown(Keys.R) && _prevKeyboardState.IsKeyUp(Keys.R))
            {
                _pendingBuild = GameServices.Traits.GenerateRandomBuild();
                System.Diagnostics.Debug.WriteLine($">>> REROLLED: {_pendingBuild} <<<");
            }
            
            // Select Science Path with arrow keys or mouse
            if ((kState.IsKeyDown(Keys.Left) || kState.IsKeyDown(Keys.A)) && 
                (_prevKeyboardState.IsKeyUp(Keys.Left) && _prevKeyboardState.IsKeyUp(Keys.A)))
            {
                _selectedSciencePathIndex = 0;
            }
            if ((kState.IsKeyDown(Keys.Right) || kState.IsKeyDown(Keys.D)) && 
                (_prevKeyboardState.IsKeyUp(Keys.Right) && _prevKeyboardState.IsKeyUp(Keys.D)))
            {
                _selectedSciencePathIndex = 1;
            }
            
            // ============================================
            // HOVER DETECTION FOR TOOLTIPS
            // ============================================
            
            // Calculate dynamic positions based on backstory description wrapping
            var backstoryDef = GameServices.Traits.GetDefinition(_pendingBuild.Backstory);
            string backstoryDesc = backstoryDef?.Description ?? "";
            var descLines = WrapText(backstoryDesc, 460f);
            int descHeight = descLines.Count * 16;
            int traitsStartY = Math.Max(155, 115 + descHeight + 10);
            
            // Backstory hover area (includes title + wrapped description)
            Rectangle backstoryBox = new Rectangle(60, 95, 480, 20 + descHeight);
            if (backstoryBox.Contains(mState.X, mState.Y))
            {
                if (backstoryDef != null)
                {
                    _showTooltip = true;
                    _tooltipTitle = backstoryDef.Name;
                    _tooltipText = backstoryDef.Description;
                    
                    // Add point cost info
                    int cost = backstoryDef.PointCost;
                    if (cost != 0)
                    {
                        string costText = cost > 0 ? $"Costs {cost} mutation point(s)" : $"Gives {-cost} mutation point(s)";
                        _tooltipText += $"\n\n{costText}";
                    }
                    
                    if (backstoryDef.StartingItems.Count > 0)
                    {
                        _tooltipText += "\n\nStarting Items: " + string.Join(", ", backstoryDef.StartingItems);
                    }
                    _tooltipPosition = new Vector2(mState.X + 15, mState.Y + 15);
                }
            }
            
            // Traits hover area (dynamic Y position based on backstory)
            int traitHeight = 18;
            for (int i = 0; i < _pendingBuild.Traits.Count; i++)
            {
                Rectangle traitBox = new Rectangle(130, traitsStartY + i * traitHeight, 400, traitHeight);
                if (traitBox.Contains(mState.X, mState.Y))
                {
                    var traitDef = GameServices.Traits.GetDefinition(_pendingBuild.Traits[i]);
                    if (traitDef != null)
                    {
                        _showTooltip = true;
                        _tooltipTitle = traitDef.Name;
                        _tooltipText = traitDef.Description;
                        
                        // Add point cost info
                        int cost = traitDef.PointCost;
                        if (cost != 0)
                        {
                            string costText = cost > 0 ? $"Costs {cost} mutation point(s)" : $"Gives {-cost} mutation point(s)";
                            _tooltipText += $"\n\n{costText}";
                        }
                        
                        // Add stat effects
                        string effects = GetTraitEffectsText(traitDef);
                        if (!string.IsNullOrEmpty(effects))
                        {
                            _tooltipText += "\n\n" + effects;
                        }
                        
                        _tooltipPosition = new Vector2(mState.X + 15, mState.Y + 15);
                    }
                }
            }
            
            // ============================================
            // ATTRIBUTE HOVER DETECTION
            // ============================================
            var attributes = (AttributeType[])Enum.GetValues(typeof(AttributeType));
            int attrX = 580;
            int attrY = 120;
            int attrWidth = 200;
            int attrHeight = 25;
            
            for (int i = 0; i < attributes.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                int x = attrX + col * attrWidth;
                int y = attrY + row * (attrHeight + 5);
                
                Rectangle attrBox = new Rectangle(x, y, attrWidth - 10, attrHeight);
                if (attrBox.Contains(mState.X, mState.Y))
                {
                    var attr = attributes[i];
                    _showTooltip = true;
                    _tooltipTitle = attr.ToString();
                    _tooltipText = GetAttributeDescription(attr);
                    _tooltipPosition = new Vector2(mState.X + 15, mState.Y + 15);
                }
            }
            
            // Science Path hover (updated positions)
            Rectangle tinkerBox = new Rectangle(340, 360, 280, 100);
            Rectangle darkBox = new Rectangle(660, 360, 280, 100);
            
            if (tinkerBox.Contains(mState.X, mState.Y))
            {
                _selectedSciencePathIndex = 0;
                _showTooltip = true;
                _tooltipTitle = "Tinker Science";
                _tooltipText = "Focus on conventional technology:\n" +
                               "- Craft implants and cybernetics\n" +
                               "- Build guns and energy weapons\n" +
                               "- Create electronics and machines\n" +
                               "- Upgrade and repair equipment";
                _tooltipPosition = new Vector2(mState.X + 15, mState.Y + 15);
                
                if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                {
                    FinalizeCharacterCreation();
                    return;
                }
            }
            if (darkBox.Contains(mState.X, mState.Y))
            {
                _selectedSciencePathIndex = 1;
                _showTooltip = true;
                _tooltipTitle = "Dark Science";
                _tooltipText = "Harness anomalous energies:\n" +
                               "- Perform rituals and transmutations\n" +
                               "- Harvest monster parts for crafting\n" +
                               "- Create eldritch weapons and armor\n" +
                               "- Manipulate void energy";
                _tooltipPosition = new Vector2(mState.X + 15, mState.Y + 15);
                
                if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                {
                    FinalizeCharacterCreation();
                    return;
                }
            }
            
            // Confirm with Enter or number keys
            if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
            {
                FinalizeCharacterCreation();
            }
            if (kState.IsKeyDown(Keys.D1) && _prevKeyboardState.IsKeyUp(Keys.D1))
            {
                _selectedSciencePathIndex = 0;
                FinalizeCharacterCreation();
            }
            if (kState.IsKeyDown(Keys.D2) && _prevKeyboardState.IsKeyUp(Keys.D2))
            {
                _selectedSciencePathIndex = 1;
                FinalizeCharacterCreation();
            }
        }
        
        /// <summary>
        /// Get a formatted string of trait stat effects
        /// </summary>
        private string GetTraitEffectsText(TraitDefinition def)
        {
            var effects = new List<string>();
            
            if (def.HealthModifier != 1.0f)
                effects.Add($"Health: {(def.HealthModifier > 1 ? "+" : "")}{(def.HealthModifier - 1) * 100:F0}%");
            if (def.SpeedModifier != 1.0f)
                effects.Add($"Speed: {(def.SpeedModifier > 1 ? "+" : "")}{(def.SpeedModifier - 1) * 100:F0}%");
            if (def.DamageModifier != 1.0f)
                effects.Add($"Damage: {(def.DamageModifier > 1 ? "+" : "")}{(def.DamageModifier - 1) * 100:F0}%");
            if (def.AccuracyModifier != 1.0f)
                effects.Add($"Accuracy: {(def.AccuracyModifier > 1 ? "+" : "")}{(def.AccuracyModifier - 1) * 100:F0}%");
            if (def.XPModifier != 1.0f)
                effects.Add($"XP Gain: {(def.XPModifier > 1 ? "+" : "")}{(def.XPModifier - 1) * 100:F0}%");
            if (def.ResearchModifier != 1.0f)
                effects.Add($"Research: {(def.ResearchModifier > 1 ? "+" : "")}{(def.ResearchModifier - 1) * 100:F0}%");
            if (def.TradeModifier != 1.0f)
                effects.Add($"Prices: {(def.TradeModifier > 1 ? "+" : "")}{(def.TradeModifier - 1) * 100:F0}%");
            if (def.HungerRateModifier != 1.0f)
                effects.Add($"Hunger Rate: {(def.HungerRateModifier > 1 ? "+" : "")}{(def.HungerRateModifier - 1) * 100:F0}%");
            if (def.HealingModifier != 1.0f)
                effects.Add($"Healing: {(def.HealingModifier > 1 ? "+" : "")}{(def.HealingModifier - 1) * 100:F0}%");
            if (def.DisguiseBonus != 0f)
                effects.Add($"Disguise: {(def.DisguiseBonus > 0 ? "+" : "")}{def.DisguiseBonus * 100:F0}%");
            if (def.IntimidationBonus != 0f)
                effects.Add($"Intimidation: {(def.IntimidationBonus > 0 ? "+" : "")}{def.IntimidationBonus * 100:F0}%");
            if (def.PersuasionBonus != 0f)
                effects.Add($"Persuasion: {(def.PersuasionBonus > 0 ? "+" : "")}{def.PersuasionBonus * 100:F0}%");
            
            // Special flags
            if (!def.CanSpeak) effects.Add("[Cannot Speak]");
            if (!def.CanDisguise) effects.Add("[Cannot Disguise]");
            if (def.IsNightPerson) effects.Add("[Night Person]");
            if (def.CanEatCorpses) effects.Add("[Can Eat Corpses]");
            if (def.IsPacifist) effects.Add("[Pacifist]");
            
            return effects.Count > 0 ? "Effects:\n" + string.Join("\n", effects) : "";
        }
        
        // ============================================
        // ATTRIBUTE SELECT STATE
        // ============================================
        
        private void UpdateAttributeSelect(KeyboardState kState, MouseState mState)
        {
            var attributes = (AttributeType[])Enum.GetValues(typeof(AttributeType));
            
            // Navigate with arrow keys
            if ((kState.IsKeyDown(Keys.Up) || kState.IsKeyDown(Keys.W)) && 
                (_prevKeyboardState.IsKeyUp(Keys.Up) && _prevKeyboardState.IsKeyUp(Keys.W)))
            {
                _selectedAttributeIndex--;
                if (_selectedAttributeIndex < 0) _selectedAttributeIndex = attributes.Length - 1;
            }
            if ((kState.IsKeyDown(Keys.Down) || kState.IsKeyDown(Keys.S)) && 
                (_prevKeyboardState.IsKeyUp(Keys.Down) && _prevKeyboardState.IsKeyUp(Keys.S)))
            {
                _selectedAttributeIndex++;
                if (_selectedAttributeIndex >= attributes.Length) _selectedAttributeIndex = 0;
            }
            
            // Mouse hover detection
            int startY = 180;
            int boxHeight = 60;
            int boxWidth = 600;
            int startX = 340;
            
            for (int i = 0; i < attributes.Length; i++)
            {
                int y = startY + i * (boxHeight + 10);
                Rectangle boxRect = new Rectangle(startX, y, boxWidth, boxHeight);
                
                if (boxRect.Contains(mState.X, mState.Y))
                {
                    _selectedAttributeIndex = i;
                    
                    if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                    {
                        ConfirmAttributeSelection();
                        return;
                    }
                }
            }
            
            // Number keys 1-6 for quick selection
            if (kState.IsKeyDown(Keys.D1) && _prevKeyboardState.IsKeyUp(Keys.D1)) { _selectedAttributeIndex = 0; ConfirmAttributeSelection(); }
            if (kState.IsKeyDown(Keys.D2) && _prevKeyboardState.IsKeyUp(Keys.D2)) { _selectedAttributeIndex = 1; ConfirmAttributeSelection(); }
            if (kState.IsKeyDown(Keys.D3) && _prevKeyboardState.IsKeyUp(Keys.D3)) { _selectedAttributeIndex = 2; ConfirmAttributeSelection(); }
            if (kState.IsKeyDown(Keys.D4) && _prevKeyboardState.IsKeyUp(Keys.D4)) { _selectedAttributeIndex = 3; ConfirmAttributeSelection(); }
            if (kState.IsKeyDown(Keys.D5) && _prevKeyboardState.IsKeyUp(Keys.D5)) { _selectedAttributeIndex = 4; ConfirmAttributeSelection(); }
            if (kState.IsKeyDown(Keys.D6) && _prevKeyboardState.IsKeyUp(Keys.D6)) { _selectedAttributeIndex = 5; ConfirmAttributeSelection(); }
            
            // Confirm with Enter
            if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
            {
                ConfirmAttributeSelection();
            }
        }
        
        private void ConfirmAttributeSelection()
        {
            var attributes = (AttributeType[])Enum.GetValues(typeof(AttributeType));
            var selectedAttr = attributes[_selectedAttributeIndex];
            
            bool success = _player.Stats.SpendAttributePoint(selectedAttr);
            
            if (success)
            {
                System.Diagnostics.Debug.WriteLine($">>> ATTRIBUTE INCREASED: {selectedAttr} -> {_player.Stats.Attributes.Get(selectedAttr)} <<<");
                
                // Now open mutation selection (we got a mutation point from spending attribute)
                OpenMutationSelect();
            }
        }
        
        // ============================================
        // PLAYING STATE
        // ============================================
        
        private void UpdatePlaying(float deltaTime, KeyboardState kState, MouseState mState)
        {
            // Check for player death
            if (!_player.IsAlive)
            {
                _deathTimer += deltaTime;
                if (_deathTimer >= DEATH_DELAY)
                {
                    _gameState = GameState.GameOver;
                    if (_combat.InCombat)
                    {
                        _combat.ForceCombatEnd();
                    }
                    System.Diagnostics.Debug.WriteLine(">>> GAME OVER <<<");
                }
                return; // Don't process other input while dying
            }
            
            // Check for PENDING ATTRIBUTE POINTS first (must spend before mutation)
            if (_player.Stats.HasPendingAttributePoints)
            {
                _gameState = GameState.AttributeSelect;
                _selectedAttributeIndex = 0;
                return;
            }
            
            // Check for mutation points - M key to open mutation screen
            if (kState.IsKeyDown(Keys.M) && _prevKeyboardState.IsKeyUp(Keys.M))
            {
                if (_player.Stats.MutationPoints > 0 || _player.Stats.FreeMutationPicks > 0)
                {
                    OpenMutationSelect();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(">>> No mutation points available! <<<");
                }
            }
            
            // I key - Toggle inventory UI
            if (kState.IsKeyDown(Keys.I) && _prevKeyboardState.IsKeyUp(Keys.I))
            {
                _inventoryOpen = !_inventoryOpen;
                _selectedInventoryIndex = 0;
                if (_inventoryOpen) _player.CurrentPath = null;  // Stop movement when opening
                System.Diagnostics.Debug.WriteLine($">>> Inventory {(_inventoryOpen ? "OPENED" : "CLOSED")} <<<");
            }
            
            // J key - Toggle quest log/journal
            if (kState.IsKeyDown(Keys.J) && _prevKeyboardState.IsKeyUp(Keys.J))
            {
                _questLogOpen = !_questLogOpen;
                _selectedQuestIndex = 0;
                if (_questLogOpen) _player.CurrentPath = null;
                System.Diagnostics.Debug.WriteLine($">>> Quest Log {(_questLogOpen ? "OPENED" : "CLOSED")} <<<");
            }
            
            // R key - Toggle research menu
            if (kState.IsKeyDown(Keys.R) && _prevKeyboardState.IsKeyUp(Keys.R))
            {
                _researchOpen = !_researchOpen;
                _selectedResearchIndex = 0;
                if (_researchOpen) _player.CurrentPath = null;
                System.Diagnostics.Debug.WriteLine($">>> Research {(_researchOpen ? "OPENED" : "CLOSED")} <<<");
            }
            
            // P key - Toggle body panel
            if (kState.IsKeyDown(Keys.P) && _prevKeyboardState.IsKeyUp(Keys.P))
            {
                _bodyPanelOpen = !_bodyPanelOpen;
                _selectedBodyPartId = null;
                _bodyPanelScroll = 0;
                if (_bodyPanelOpen) _player.CurrentPath = null;
                System.Diagnostics.Debug.WriteLine($">>> Body Panel {(_bodyPanelOpen ? "OPENED" : "CLOSED")} <<<");
            }
            
            // G key - Pick up nearby items
            if (kState.IsKeyDown(Keys.G) && _prevKeyboardState.IsKeyUp(Keys.G))
            {
                TryPickupAllNearby();
            }
            
            // Update nearest item for pickup highlight
            UpdateNearestItem();
            
            // Debug keys (always available)
            HandleDebugKeys(kState);

            // Camera controls (only when NOT in build menu or inventory)
            if (!_buildMenuOpen && !GameServices.Building.InBuildMode && !_inventoryOpen)
            {
                float camSpeed = 500f * deltaTime;
                if (kState.IsKeyDown(Keys.W)) _camera.Position.Y -= camSpeed;
                if (kState.IsKeyDown(Keys.S)) _camera.Position.Y += camSpeed;
                if (kState.IsKeyDown(Keys.A)) _camera.Position.X -= camSpeed;
                if (kState.IsKeyDown(Keys.D)) _camera.Position.X += camSpeed;
                if (kState.IsKeyDown(Keys.Q)) _camera.Zoom -= 1f * deltaTime;
                if (kState.IsKeyDown(Keys.E)) _camera.Zoom += 1f * deltaTime;
            }
            
            // Inventory UI handling
            if (_inventoryOpen)
            {
                UpdateInventoryUI(kState, mState);
                return; // Don't process other actions while inventory is open
            }
            
            // Crafting UI handling
            if (_craftingOpen)
            {
                UpdateCraftingUI(kState, mState);
                return; // Don't process other actions while crafting is open
            }
            
            // Trading UI handling
            if (_tradingOpen)
            {
                UpdateTradingUI(kState, mState);
                return; // Don't process other actions while trading is open
            }
            
            // Quest dialogue handling (NPC quests)
            if (_questDialogueOpen)
            {
                UpdateQuestDialogueUI(kState, mState);
                return;
            }
            
            // Quest log handling
            if (_questLogOpen)
            {
                UpdateQuestLogUI(kState, mState);
                return;
            }
            
            // Research UI handling
            if (_researchOpen)
            {
                UpdateResearchUI(kState, mState);
                return;
            }
            
            // Body panel UI handling
            if (_bodyPanelOpen)
            {
                UpdateBodyPanelUI(kState, mState);
                return;
            }

            // Combat or Exploration
            if (_combat.InCombat)
            {
                UpdateCombat(deltaTime, kState, mState);
            }
            else
            {
                UpdateExploration(deltaTime, kState, mState);
            }
        }
        
        private void UpdateExploration(float deltaTime, KeyboardState kState, MouseState mState)
        {
            // Update world time and temperature
            GameServices.SurvivalSystem.Update(deltaTime);
            
            // Update player survival needs
            float ambientTemp = GameServices.SurvivalSystem.AmbientTemperature;
            
            // Add warmth from nearby structures (campfires, etc.)
            Point playerTile = new Point(
                (int)(_player.Position.X / _world.TileSize),
                (int)(_player.Position.Y / _world.TileSize)
            );
            float structureWarmth = GameServices.Building.GetWarmthAt(playerTile);
            ambientTemp += structureWarmth;
            
            _player.Stats.Survival.UpdateRealTime(deltaTime, ambientTemp);
            
            // Update cooldowns (convert real-time seconds to game days)
            // Assuming 1 real minute = 1 game hour, so 24 minutes = 1 day
            float gameDays = deltaTime / (24f * 60f);  // Very slow, proper game time
            _player.Stats.TickCooldowns(gameDays);
            
            // Update research progress
            GameServices.Research.Update(deltaTime);
            
            // Apply survival health drain (starvation, dehydration, etc.)
            float healthDrain = _player.Stats.Survival.GetHealthDrain();
            if (healthDrain > 0)
            {
                _player.Stats.TakeDamage(healthDrain * deltaTime, DamageType.Physical);
            }
            
            // Update enemies (AI)
            foreach (var enemy in _enemies)
            {
                enemy.Update(deltaTime, _world, _player.Position, _enemies);
            }
            
            // Update combat manager (checks for combat trigger)
            _combat.Update(deltaTime, _world);
            
            // B key - toggle build mode/menu
            if (kState.IsKeyDown(Keys.B) && _prevKeyboardState.IsKeyUp(Keys.B))
            {
                if (GameServices.Building.InBuildMode)
                {
                    GameServices.Building.ExitBuildMode();
                    _buildMenuOpen = false;
                }
                else
                {
                    _buildMenuOpen = !_buildMenuOpen;
                    if (_buildMenuOpen) _player.CurrentPath = null; // Stop movement when opening
                }
            }
            
            // Handle build mode
            if (GameServices.Building.InBuildMode)
            {
                UpdateBuildMode(kState, mState);
                return; // Skip normal movement while in build mode
            }
            
            // Handle build menu navigation
            if (_buildMenuOpen)
            {
                UpdateBuildMenu(kState, mState);
                return; // Skip normal movement while in menu
            }
            
            // E key - interact with structure
            if (kState.IsKeyDown(Keys.E) && _prevKeyboardState.IsKeyUp(Keys.E))
            {
                TryInteractWithStructure();
            }
            
            // C key - open basic crafting (or crafting at nearby workstation)
            if (kState.IsKeyDown(Keys.C) && _prevKeyboardState.IsKeyUp(Keys.C))
            {
                // Check if near a workstation first (reuse playerTile from above)
                Structure nearbyWorkstation = null;
                foreach (var dir in new Point[] { new Point(0, 0), new Point(0, -1), new Point(1, 0), new Point(0, 1), new Point(-1, 0) })
                {
                    Point checkTile = new Point(playerTile.X + dir.X, playerTile.Y + dir.Y);
                    var structure = GameServices.Building.GetStructureAt(checkTile);
                    
                    if (structure != null && CraftingSystem.IsWorkstation(structure))
                    {
                        nearbyWorkstation = structure;
                        break;
                    }
                }
                
                OpenCrafting(nearbyWorkstation);  // null = basic crafting only
            }
            
            // Detect nearby NPCs
            _nearestNPC = null;
            float npcRange = 2.5f * _world.TileSize;
            foreach (var npc in _npcs)
            {
                float dist = Vector2.Distance(_player.Position, npc.Position);
                if (dist < npcRange)
                {
                    _nearestNPC = npc;
                    break;
                }
            }
            
            // T key - talk/trade with NPC
            if (kState.IsKeyDown(Keys.T) && _prevKeyboardState.IsKeyUp(Keys.T))
            {
                if (_nearestNPC != null)
                {
                    OpenTrading(_nearestNPC);
                }
            }
            
            // Player click-to-move (only when no menu is open)
            if (!AnyMenuOpen && mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                Vector2 worldPos = _camera.ScreenToWorld(new Vector2(mState.X, mState.Y));
                Point clickTile = new Point((int)(worldPos.X / _world.TileSize), (int)(worldPos.Y / _world.TileSize));
                
                // Close inspect panel on left click
                _inspectPanelOpen = false;
                
                // Check if clicked on enemy
                EnemyEntity clickedEnemy = GetEnemyAtTile(clickTile);
                if (clickedEnemy != null && clickedEnemy.IsAlive)
                {
                    // Provoke passive/cowardly mobs when player initiates attack
                    if (clickedEnemy.Behavior != CreatureBehavior.Aggressive && !clickedEnemy.IsProvoked)
                    {
                        clickedEnemy.Provoke();
                    }
                    _combat.StartCombat(clickedEnemy);
                }
                else
                {
                    Point startTile = new Point(
                        (int)(_player.Position.X / _world.TileSize),
                        (int)(_player.Position.Y / _world.TileSize)
                    );
                    var path = Pathfinder.FindPath(_world, startTile, clickTile);
                    if (path != null) _player.CurrentPath = path;
                }
            }
            
            // Right-click to inspect (like Rimworld/BG3) - only when no menu open
            if (!AnyMenuOpen && mState.RightButton == ButtonState.Pressed && _prevMouseState.RightButton == ButtonState.Released)
            {
                Vector2 worldPos = _camera.ScreenToWorld(new Vector2(mState.X, mState.Y));
                Point clickTile = new Point((int)(worldPos.X / _world.TileSize), (int)(worldPos.Y / _world.TileSize));
                
                // Try to find something to inspect at this tile
                object inspectTarget = GetInspectTarget(clickTile);
                if (inspectTarget != null)
                {
                    _inspectedObject = inspectTarget;
                    _inspectPanelOpen = true;
                    _inspectPanelPosition = new Vector2(mState.X + 10, mState.Y);
                    
                    // Clamp panel position to screen
                    if (_inspectPanelPosition.X > 1080) _inspectPanelPosition.X = mState.X - 210;
                    if (_inspectPanelPosition.Y > 500) _inspectPanelPosition.Y = 500;
                }
                else
                {
                    _inspectPanelOpen = false;
                }
            }
            
            // Lightning test
            if (kState.IsKeyDown(Keys.Space) && _prevKeyboardState.IsKeyUp(Keys.Space))
            {
                if (_player.HasStatus(StatusEffectType.Wet) && !_player.HasStatus(StatusEffectType.Stunned))
                {
                    _player.ApplyLightning(10f);
                }
            }
            
            _player.Update(deltaTime, _world);
            
            // Check for zone transitions (player at map edge)
            CheckZoneTransition();
        }
        
        private void CheckZoneTransition()
        {
            Point playerTile = new Point(
                (int)(_player.Position.X / _world.TileSize),
                (int)(_player.Position.Y / _world.TileSize)
            );
            
            var exit = _zoneManager.CheckForExit(playerTile, _world.Width, _world.Height);
            if (exit != null)
            {
                // Calculate entry point that preserves player's relative position
                Point adjustedEntry = CalculateEntryPoint(exit, playerTile);
                TransitionToZone(exit.TargetZoneId, adjustedEntry);
            }
        }
        
        private Point CalculateEntryPoint(ZoneExit exit, Point playerTile)
        {
            var targetZone = _zoneManager.GetZone(exit.TargetZoneId);
            if (targetZone == null) return exit.EntryPoint;
            
            int targetWidth = targetZone.Width;
            int targetHeight = targetZone.Height;
            
            // Calculate relative position (0.0 to 1.0) and apply to target zone
            switch (exit.Direction)
            {
                case ZoneExitDirection.North:
                    // Going North -> spawn at South edge, preserve X ratio
                    float xRatio = (float)playerTile.X / _world.Width;
                    int targetX = Math.Clamp((int)(xRatio * targetWidth), 1, targetWidth - 2);
                    return new Point(targetX, targetHeight - 2);
                    
                case ZoneExitDirection.South:
                    // Going South -> spawn at North edge, preserve X ratio
                    xRatio = (float)playerTile.X / _world.Width;
                    targetX = Math.Clamp((int)(xRatio * targetWidth), 1, targetWidth - 2);
                    return new Point(targetX, 1);
                    
                case ZoneExitDirection.East:
                    // Going East -> spawn at West edge, preserve Y ratio
                    float yRatio = (float)playerTile.Y / _world.Height;
                    int targetY = Math.Clamp((int)(yRatio * targetHeight), 1, targetHeight - 2);
                    return new Point(1, targetY);
                    
                case ZoneExitDirection.West:
                    // Going West -> spawn at East edge, preserve Y ratio
                    yRatio = (float)playerTile.Y / _world.Height;
                    targetY = Math.Clamp((int)(yRatio * targetHeight), 1, targetHeight - 2);
                    return new Point(targetWidth - 2, targetY);
                    
                default:
                    return exit.EntryPoint;
            }
        }
        
        private void TransitionToZone(string targetZoneId, Point entryPoint)
        {
            var targetZone = _zoneManager.GetZone(targetZoneId);
            if (targetZone == null)
            {
                System.Diagnostics.Debug.WriteLine($">>> ERROR: Zone '{targetZoneId}' not found! <<<");
                return;
            }
            
            string oldZoneName = _zoneManager.CurrentZone?.Name ?? "Unknown";
            
            // SAVE CURRENT ZONE STATE before leaving
            var currentZone = _zoneManager.CurrentZone;
            if (currentZone != null)
            {
                currentZone.SaveEnemyStates(_enemies, _world.TileSize);
                currentZone.SaveNPCStates(_npcs);
                System.Diagnostics.Debug.WriteLine($">>> Saved {_enemies.Count(e => e.IsAlive)} enemies, {_npcs.Count} NPCs <<<");
            }
            
            // Clear current zone state
            _groundItems.Clear();
            GameServices.Building.ClearAllStructures();
            
            // Switch zone
            _zoneManager.SetCurrentZone(targetZoneId);
            
            // Resize world if needed
            if (_world.Width != targetZone.Width || _world.Height != targetZone.Height)
            {
                _world = new WorldGrid(targetZone.Width, targetZone.Height, GraphicsDevice);
            }
            
            // Generate new world terrain
            _zoneManager.GenerateZoneWorld(_world, targetZone);
            
            // Move player to entry point
            _player.Position = new Vector2(entryPoint.X * _world.TileSize, entryPoint.Y * _world.TileSize);
            _player.CurrentPath = null;
            
            // Spawn/restore enemies for zone
            _enemies = _zoneManager.GenerateZoneEnemies(targetZone, _world.TileSize);
            _combat.UpdateEnemyList(_enemies);
            
            // Spawn/restore NPCs for zone
            SpawnNPCsForZone(targetZone);
            
            // Center camera on player
            _camera.Position = _player.Position;
            
            // Track quest progress - zone exploration
            GameServices.Quests.OnZoneEntered(targetZoneId);
            
            // Show transition notification
            ShowNotification($"Entered: {targetZone.Name}");
            System.Diagnostics.Debug.WriteLine($">>> ZONE TRANSITION: {oldZoneName} -> {targetZone.Name} <<<");
        }
        
        // ============================================
        // BUILD MODE
        // ============================================
        
        private void UpdateBuildMenu(KeyboardState kState, MouseState mState)
        {
            // Menu layout constants (must match DrawBuildMenuUI)
            int menuX = 200;
            int menuY = 100;
            int menuWidth = 880;
            int tabX = menuX + 10;
            int tabY = menuY + 40;
            int tabWidth = 100;
            int tabHeight = 25;
            int itemY = tabY + 40;
            int itemHeight = 60;
            int itemWidth = menuWidth - 40;
            
            var categoryItems = GameServices.Building.GetDefinitionsByCategory(_buildCategories[_buildCategoryIndex]);
            
            // ============================================
            // MOUSE SUPPORT FOR CATEGORY TABS
            // ============================================
            for (int i = 0; i < _buildCategories.Length; i++)
            {
                Rectangle tabRect = new Rectangle(tabX + i * (tabWidth + 5), tabY, tabWidth, tabHeight);
                if (tabRect.Contains(mState.X, mState.Y))
                {
                    // Hover - change category
                    if (_buildCategoryIndex != i)
                    {
                        _buildCategoryIndex = i;
                        _buildItemIndex = 0;
                        categoryItems = GameServices.Building.GetDefinitionsByCategory(_buildCategories[_buildCategoryIndex]);
                    }
                }
            }
            
            // ============================================
            // MOUSE SUPPORT FOR ITEMS
            // ============================================
            for (int i = 0; i < categoryItems.Count; i++)
            {
                int currentY = itemY + i * (itemHeight + 5);
                Rectangle itemRect = new Rectangle(menuX + 20, currentY, itemWidth, itemHeight);
                
                if (itemRect.Contains(mState.X, mState.Y))
                {
                    _buildItemIndex = i;
                    
                    // Click to select and enter build mode
                    if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                    {
                        var selected = categoryItems[i];
                        GameServices.Building.EnterBuildMode(selected.Type);
                        _buildMenuOpen = false;
                        return;
                    }
                }
            }
            
            // Navigate categories with Left/Right or A/D
            if ((kState.IsKeyDown(Keys.Left) || kState.IsKeyDown(Keys.A)) && 
                (_prevKeyboardState.IsKeyUp(Keys.Left) && _prevKeyboardState.IsKeyUp(Keys.A)))
            {
                _buildCategoryIndex--;
                if (_buildCategoryIndex < 0) _buildCategoryIndex = _buildCategories.Length - 1;
                _buildItemIndex = 0;
            }
            if ((kState.IsKeyDown(Keys.Right) || kState.IsKeyDown(Keys.D)) && 
                (_prevKeyboardState.IsKeyUp(Keys.Right) && _prevKeyboardState.IsKeyUp(Keys.D)))
            {
                _buildCategoryIndex++;
                if (_buildCategoryIndex >= _buildCategories.Length) _buildCategoryIndex = 0;
                _buildItemIndex = 0;
            }
            
            // Navigate items with Up/Down or W/S
            categoryItems = GameServices.Building.GetDefinitionsByCategory(_buildCategories[_buildCategoryIndex]);
            if ((kState.IsKeyDown(Keys.Up) || kState.IsKeyDown(Keys.W)) && 
                (_prevKeyboardState.IsKeyUp(Keys.Up) && _prevKeyboardState.IsKeyUp(Keys.W)))
            {
                _buildItemIndex--;
                if (_buildItemIndex < 0) _buildItemIndex = categoryItems.Count - 1;
            }
            if ((kState.IsKeyDown(Keys.Down) || kState.IsKeyDown(Keys.S)) && 
                (_prevKeyboardState.IsKeyUp(Keys.Down) && _prevKeyboardState.IsKeyUp(Keys.S)))
            {
                _buildItemIndex++;
                if (_buildItemIndex >= categoryItems.Count) _buildItemIndex = 0;
            }
            
            // Enter or click to select and enter build placement mode
            if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
            {
                if (categoryItems.Count > 0)
                {
                    var selected = categoryItems[_buildItemIndex];
                    GameServices.Building.EnterBuildMode(selected.Type);
                    _buildMenuOpen = false;
                }
            }
            
            // Escape or right-click to close menu
            if (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                _buildMenuOpen = false;
            }
            if (mState.RightButton == ButtonState.Pressed && _prevMouseState.RightButton == ButtonState.Released)
            {
                _buildMenuOpen = false;
            }
            
            // Quick number keys 1-9 for items in current category
            for (int i = 0; i < Math.Min(9, categoryItems.Count); i++)
            {
                Keys key = Keys.D1 + i;
                if (kState.IsKeyDown(key) && _prevKeyboardState.IsKeyUp(key))
                {
                    var selected = categoryItems[i];
                    GameServices.Building.EnterBuildMode(selected.Type);
                    _buildMenuOpen = false;
                    break;
                }
            }
        }
        
        private void UpdateBuildMode(KeyboardState kState, MouseState mState)
        {
            // Update preview position based on mouse
            Vector2 worldPos = _camera.ScreenToWorld(new Vector2(mState.X, mState.Y));
            Point previewTile = new Point((int)(worldPos.X / _world.TileSize), (int)(worldPos.Y / _world.TileSize));
            GameServices.Building.UpdatePreview(previewTile);
            
            // Left click to place
            if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                if (GameServices.Building.SelectedStructure.HasValue)
                {
                    var structType = GameServices.Building.SelectedStructure.Value;
                    if (GameServices.Building.CanPlaceAt(structType, previewTile, _world))
                    {
                        // Place the blueprint (instant for now since we don't have resource cost enforcement yet)
                        var structure = GameServices.Building.PlaceInstant(structType, previewTile, _world);
                        if (structure != null)
                        {
                            // Track quest progress - building
                            GameServices.Quests.OnStructureBuilt(structure.Definition.Name);
                            
                            System.Diagnostics.Debug.WriteLine($">>> Placed {structure.Definition.Name} at ({previewTile.X}, {previewTile.Y}) <<<");
                        }
                    }
                }
            }
            
            // Right click or Escape to exit build mode
            if (mState.RightButton == ButtonState.Pressed && _prevMouseState.RightButton == ButtonState.Released)
            {
                GameServices.Building.ExitBuildMode();
            }
            if (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                GameServices.Building.ExitBuildMode();
            }
            
            // R to rotate (for future use when structures have rotation)
            // Delete to remove structure at cursor
            if (kState.IsKeyDown(Keys.Delete) && _prevKeyboardState.IsKeyUp(Keys.Delete))
            {
                if (GameServices.Building.HasStructureAt(previewTile))
                {
                    GameServices.Building.RemoveStructure(previewTile);
                }
            }
        }
        
        private void TryInteractWithStructure()
        {
            Point playerTile = new Point(
                (int)(_player.Position.X / _world.TileSize),
                (int)(_player.Position.Y / _world.TileSize)
            );
            
            // Check adjacent tiles for interactable structures
            foreach (var dir in new Point[] { new Point(0, -1), new Point(1, 0), new Point(0, 1), new Point(-1, 0), new Point(0, 0) })
            {
                Point checkTile = new Point(playerTile.X + dir.X, playerTile.Y + dir.Y);
                var structure = GameServices.Building.GetStructureAt(checkTile);
                
                if (structure != null && structure.IsFunctional && structure.Definition.Interactable)
                {
                    // Handle different structure interactions
                    if (structure.Definition.CanBeOpened)
                    {
                        // Toggle door
                        structure.ToggleDoor();
                        return;
                    }
                    else if (structure.Type == StructureType.Bed)
                    {
                        // Rest at bed
                        _player.Stats.Survival.RestoreRest(50f);
                        ShowNotification("Rested at bed!");
                        return;
                    }
                    else if (structure.Type == StructureType.Campfire)
                    {
                        // Warm up at campfire
                        ShowNotification("Warming up...");
                        return;
                    }
                    else if (structure.Type == StructureType.StorageBox)
                    {
                        // Open storage (future: storage UI)
                        ShowNotification($"Storage: {structure.StoredItems.Count} items");
                        return;
                    }
                    else if (structure.Definition.Category == StructureCategory.Workstation)
                    {
                        // Open crafting UI with this workstation
                        OpenCrafting(structure);
                        return;
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine(">>> No interactable structure nearby <<<");
        }
        
        private void OpenCrafting(Structure workstation)
        {
            _nearestWorkstation = workstation;
            _activeWorkstationType = workstation != null ? CraftingSystem.GetWorkstationType(workstation) : null;
            _craftingOpen = true;
            _selectedRecipeIndex = 0;
            
            // Stop player movement
            _player.CurrentPath = null;
            
            string wsName = _activeWorkstationType != null ? CraftingSystem.GetWorkstationName(_activeWorkstationType) : "Basic Crafting";
            ShowNotification($"Opened {wsName}");
            System.Diagnostics.Debug.WriteLine($">>> Opened crafting: {wsName} <<<");
        }
        
        private void OpenTrading(NPCEntity npc)
        {
            _tradingNPC = npc;
            _tradingOpen = true;
            _selectedTradeIndex = 0;
            _tradingBuyMode = true;
            
            // Stop player movement
            _player.CurrentPath = null;
            
            // Track quest progress - NPC talked to
            GameServices.Quests.OnNPCTalkedTo(npc.Id);
            
            // Check for quests from this NPC
            _availableNPCQuests = GameServices.Quests.GetQuestsFromNPC(npc.Id, _player.Stats.Level);
            _turnInQuests = GameServices.Quests.GetQuestsToTurnIn(npc.Id);
            
            // If there are quests, open quest dialogue instead
            if (_availableNPCQuests.Count > 0 || _turnInQuests.Count > 0)
            {
                _questDialogueOpen = true;
                _tradingOpen = false;
                _selectedDialogueIndex = 0;
                ShowNotification($"Speaking with {npc.Name}");
            }
            else
            {
                ShowNotification($"Trading with {npc.Name}");
            }
            
            System.Diagnostics.Debug.WriteLine($">>> Interacting with: {npc.Name} ({_availableNPCQuests.Count} quests, {_turnInQuests.Count} turn-ins) <<<");
        }
        
        private void UpdateCombat(float deltaTime, KeyboardState kState, MouseState mState)
        {
            _combat.Update(deltaTime, _world);
            
            // Update player animation during combat
            _player.Update(deltaTime, _world);
            
            // Update all enemies for animations (both in and out of combat zone)
            foreach (var enemy in _enemies)
            {
                if (enemy.IsAlive)
                {
                    // Always update for animations, but non-combat enemies also do AI
                    if (enemy.IsAnimating)
                    {
                        enemy.Update(deltaTime, _world, _player.Position, _enemies);
                    }
                    else if (!enemy.InCombatZone)
                    {
                        // Non-combat enemies wander in real-time (like BG3)
                        enemy.Update(deltaTime, _world, _player.Position, _enemies);
                        
                        // Check if this enemy just wandered INTO the combat zone
                        // If so, immediately pull them into combat
                        if (_combat.IsInCombatZone(enemy.Position))
                        {
                            _combat.PullEnemyIntoCombat(enemy);
                        }
                    }
                }
            }
            
            if (_combat.IsPlayerTurn)
            {
                // End turn key
                if (kState.IsKeyDown(Keys.Space) && _prevKeyboardState.IsKeyUp(Keys.Space))
                {
                    _combat.PlayerEndTurn();
                }
                
                // Tab to cycle targets
                if (kState.IsKeyDown(Keys.Tab) && _prevKeyboardState.IsKeyUp(Keys.Tab))
                {
                    CycleTarget();
                }
                
                // E key - Attempt to escape combat
                if (kState.IsKeyDown(Keys.E) && _prevKeyboardState.IsKeyUp(Keys.E))
                {
                    if (_combat.TryEscape())
                    {
                        ShowNotification("Escaped from combat!");
                    }
                }
                
                // R key - Convert AP to MP (for extra movement)
                if (kState.IsKeyDown(Keys.R) && _prevKeyboardState.IsKeyUp(Keys.R))
                {
                    if (_combat.ConvertAPtoMP(1))
                    {
                        ShowNotification("AP → MP: +2 movement!");
                    }
                }
                
                // Attack selected target
                if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
                {
                    if (_selectedEnemy != null && _selectedEnemy.IsAlive)
                    {
                        _combat.PlayerAttack(_selectedEnemy, _world);
                        if (!_selectedEnemy.IsAlive) _selectedEnemy = null;
                    }
                }
                
                // Click handling
                if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                {
                    // Close inspect panel on left click
                    _inspectPanelOpen = false;
                    
                    Vector2 worldPos = _camera.ScreenToWorld(new Vector2(mState.X, mState.Y));
                    Point clickTile = new Point((int)(worldPos.X / _world.TileSize), (int)(worldPos.Y / _world.TileSize));
                    
                    EnemyEntity clickedEnemy = GetEnemyAtTile(clickTile);
                    if (clickedEnemy != null && clickedEnemy.IsAlive)
                    {
                        Point playerTile = new Point(
                            (int)(_player.Position.X / _world.TileSize),
                            (int)(_player.Position.Y / _world.TileSize)
                        );
                        
                        // Use Chebyshev distance for diagonal melee attacks
                        if (Pathfinder.IsAdjacent(playerTile, clickTile))
                        {
                            _combat.PlayerAttack(clickedEnemy, _world);
                            if (!clickedEnemy.IsAlive) _selectedEnemy = null;
                        }
                        else
                        {
                            _selectedEnemy = clickedEnemy;
                        }
                    }
                    else
                    {
                        Point playerTile = new Point(
                            (int)(_player.Position.X / _world.TileSize),
                            (int)(_player.Position.Y / _world.TileSize)
                        );
                        
                        // Use Chebyshev distance for diagonal movement
                        if (Pathfinder.IsAdjacent(playerTile, clickTile))
                        {
                            _combat.PlayerMove(clickTile, _world);
                        }
                    }
                }
                
                // Right-click to inspect during combat
                if (mState.RightButton == ButtonState.Pressed && _prevMouseState.RightButton == ButtonState.Released)
                {
                    Vector2 worldPos = _camera.ScreenToWorld(new Vector2(mState.X, mState.Y));
                    Point clickTile = new Point((int)(worldPos.X / _world.TileSize), (int)(worldPos.Y / _world.TileSize));
                    
                    object inspectTarget = GetInspectTarget(clickTile);
                    if (inspectTarget != null)
                    {
                        _inspectedObject = inspectTarget;
                        _inspectPanelOpen = true;
                        _inspectPanelPosition = new Vector2(mState.X + 10, mState.Y);
                        
                        // Also select enemy if it's an enemy
                        if (inspectTarget is EnemyEntity enemy && enemy.InCombatZone)
                        {
                            _selectedEnemy = enemy;
                        }
                        
                        // Clamp panel position to screen
                        if (_inspectPanelPosition.X > 1080) _inspectPanelPosition.X = mState.X - 210;
                        if (_inspectPanelPosition.Y > 500) _inspectPanelPosition.Y = 500;
                    }
                    else
                    {
                        _inspectPanelOpen = false;
                    }
                }
            }
        }
        
        // ============================================
        // MUTATION SELECT STATE
        // ============================================
        
        private void OpenMutationSelect()
        {
            // Determine if using free pick
            _usingFreePick = _player.Stats.FreeMutationPicks > 0;
            
            // Get choices
            _mutationChoices = _player.Stats.GetMutationChoices(_usingFreePick ? 100 : 3);
            _selectedMutationIndex = 0;
            
            if (_mutationChoices.Count > 0)
            {
                _gameState = GameState.MutationSelect;
                System.Diagnostics.Debug.WriteLine($">>> MUTATION SELECT OPENED ({(_usingFreePick ? "FREE PICK" : "3 Choices")}) <<<");
            }
        }
        
        private void UpdateMutationSelect(KeyboardState kState)
        {
            MouseState mState = Mouse.GetState();
            
            // Mouse click detection for mutation boxes
            // Box layout: startX=340, startY=120, boxWidth=600, boxHeight=80, gap=10
            int startX = 340;
            int startY = 120;
            int boxWidth = 600;
            int boxHeight = 80;
            int gap = 10;
            
            // Hover detection - highlight what mouse is over
            for (int i = 0; i < _mutationChoices.Count && i < 10; i++)
            {
                int boxY = startY + i * (boxHeight + gap);
                Rectangle boxRect = new Rectangle(startX, boxY, boxWidth, boxHeight);
                
                if (boxRect.Contains(mState.X, mState.Y))
                {
                    _selectedMutationIndex = i;
                    
                    // Click to select
                    if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                    {
                        ConfirmMutationSelection();
                        return;
                    }
                }
            }
            
            // Navigate with arrow keys or W/S
            if ((kState.IsKeyDown(Keys.Up) || kState.IsKeyDown(Keys.W)) && 
                (_prevKeyboardState.IsKeyUp(Keys.Up) && _prevKeyboardState.IsKeyUp(Keys.W)))
            {
                _selectedMutationIndex--;
                if (_selectedMutationIndex < 0) _selectedMutationIndex = _mutationChoices.Count - 1;
            }
            
            if ((kState.IsKeyDown(Keys.Down) || kState.IsKeyDown(Keys.S)) && 
                (_prevKeyboardState.IsKeyUp(Keys.Down) && _prevKeyboardState.IsKeyUp(Keys.S)))
            {
                _selectedMutationIndex++;
                if (_selectedMutationIndex >= _mutationChoices.Count) _selectedMutationIndex = 0;
            }
            
            // Quick select with number keys (1, 2, 3)
            if (kState.IsKeyDown(Keys.D1) && _prevKeyboardState.IsKeyUp(Keys.D1) && _mutationChoices.Count > 0)
            {
                _selectedMutationIndex = 0;
                ConfirmMutationSelection();
            }
            if (kState.IsKeyDown(Keys.D2) && _prevKeyboardState.IsKeyUp(Keys.D2) && _mutationChoices.Count > 1)
            {
                _selectedMutationIndex = 1;
                ConfirmMutationSelection();
            }
            if (kState.IsKeyDown(Keys.D3) && _prevKeyboardState.IsKeyUp(Keys.D3) && _mutationChoices.Count > 2)
            {
                _selectedMutationIndex = 2;
                ConfirmMutationSelection();
            }
            
            // Confirm with Enter
            if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
            {
                ConfirmMutationSelection();
            }
            
            // Cancel with Escape or M or right-click
            if ((kState.IsKeyDown(Keys.M) && _prevKeyboardState.IsKeyUp(Keys.M)) ||
                (kState.IsKeyDown(Keys.Back) && _prevKeyboardState.IsKeyUp(Keys.Back)) ||
                (mState.RightButton == ButtonState.Pressed && _prevMouseState.RightButton == ButtonState.Released))
            {
                _gameState = GameState.Playing;
                System.Diagnostics.Debug.WriteLine(">>> MUTATION SELECT CANCELLED <<<");
            }
        }
        
        private void ConfirmMutationSelection()
        {
            if (_selectedMutationIndex < 0 || _selectedMutationIndex >= _mutationChoices.Count)
                return;
            
            var selectedMutation = _mutationChoices[_selectedMutationIndex];
            
            bool success = _player.Stats.SpendMutationPoint(selectedMutation.Type, _usingFreePick);
            
            if (success)
            {
                System.Diagnostics.Debug.WriteLine($">>> MUTATION ACQUIRED: {selectedMutation.Name}! <<<");
                System.Diagnostics.Debug.WriteLine(_player.Stats.GetStatusReport());
            }
            
            _gameState = GameState.Playing;
            _mutationChoices.Clear();
        }
        
        // ============================================
        // VITAL ORGAN CHOICE STATE
        // ============================================
        
        private int _vitalOrganTargetIndex = 0;
        private List<BodyPart> _vitalOrganTargets = new List<BodyPart>();
        
        private void UpdateVitalOrganChoice(KeyboardState kState, MouseState mState)
        {
            // Get valid targets (non-critical parts with health > 0)
            if (_vitalOrganTargets.Count == 0)
            {
                _vitalOrganTargets = _player.Stats.Body.Parts.Values
                    .Where(p => !p.IsCriticalPart && 
                               p.CurrentHealth > 0 &&
                               p.Condition != BodyPartCondition.Missing &&
                               p.Condition != BodyPartCondition.Destroyed)
                    .OrderByDescending(p => p.CurrentHealth)  // Healthiest parts first
                    .ToList();
                _vitalOrganTargetIndex = 0;
            }
            
            if (_vitalOrganTargets.Count == 0)
            {
                // No valid targets - player dies
                _player.Stats.SkipVitalOrganRelocation();
                _gameState = GameState.GameOver;
                return;
            }
            
            // Navigate with arrow keys or W/S
            if ((kState.IsKeyDown(Keys.Up) || kState.IsKeyDown(Keys.W)) && 
                (_prevKeyboardState.IsKeyUp(Keys.Up) && _prevKeyboardState.IsKeyUp(Keys.W)))
            {
                _vitalOrganTargetIndex--;
                if (_vitalOrganTargetIndex < 0) _vitalOrganTargetIndex = _vitalOrganTargets.Count - 1;
            }
            
            if ((kState.IsKeyDown(Keys.Down) || kState.IsKeyDown(Keys.S)) && 
                (_prevKeyboardState.IsKeyUp(Keys.Down) && _prevKeyboardState.IsKeyUp(Keys.S)))
            {
                _vitalOrganTargetIndex++;
                if (_vitalOrganTargetIndex >= _vitalOrganTargets.Count) _vitalOrganTargetIndex = 0;
            }
            
            // Mouse click on body parts
            int startX = 440;
            int startY = 180;
            int boxHeight = 40;
            int boxWidth = 400;
            
            for (int i = 0; i < _vitalOrganTargets.Count && i < 10; i++)
            {
                Rectangle boxRect = new Rectangle(startX, startY + i * (boxHeight + 5), boxWidth, boxHeight);
                if (boxRect.Contains(mState.X, mState.Y))
                {
                    _vitalOrganTargetIndex = i;
                    
                    if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                    {
                        ConfirmVitalOrganRelocation();
                        return;
                    }
                }
            }
            
            // Confirm with Enter
            if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
            {
                ConfirmVitalOrganRelocation();
            }
            
            // Skip/Accept death with Escape
            if (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                _player.Stats.SkipVitalOrganRelocation();
                _vitalOrganTargets.Clear();
                _gameState = GameState.GameOver;
            }
        }
        
        private void ConfirmVitalOrganRelocation()
        {
            if (_vitalOrganTargetIndex < 0 || _vitalOrganTargetIndex >= _vitalOrganTargets.Count)
                return;
            
            var targetPart = _vitalOrganTargets[_vitalOrganTargetIndex];
            bool success = _player.Stats.RelocateVitalOrganDamage(targetPart);
            
            if (success)
            {
                var damagedPart = _player.Stats.PendingVitalOrganDamage?.HitPart;
                string sourceName = damagedPart?.Name ?? "vital organ";
                ShowNotification($"Vital organ shifted! {targetPart.Name} takes the damage!");
                System.Diagnostics.Debug.WriteLine($">>> Relocated damage from {sourceName} to {targetPart.Name}! <<<");
            }
            else
            {
                ShowNotification("Failed to relocate vital organ!");
            }
            
            _vitalOrganTargets.Clear();
            _gameState = GameState.Playing;
        }
        
        // ============================================
        // GAME OVER STATE
        // ============================================
        
        private void UpdateGameOver(KeyboardState kState)
        {
            // Press R to restart
            if (kState.IsKeyDown(Keys.R) && _prevKeyboardState.IsKeyUp(Keys.R))
            {
                StartNewGame();
            }
        }
        
        // ============================================
        // HELPERS
        // ============================================
        
        private EnemyEntity GetEnemyAtTile(Point tile)
        {
            foreach (var enemy in _enemies)
            {
                Point enemyTile = enemy.GetTilePosition(_world.TileSize);
                if (enemyTile == tile) return enemy;
            }
            return null;
        }
        
        /// <summary>
        /// Get inspectable object at a tile (enemy, NPC, structure, or tile)
        /// </summary>
        private object GetInspectTarget(Point tile)
        {
            // Priority 1: Enemy at tile
            var enemy = GetEnemyAtTile(tile);
            if (enemy != null && enemy.IsAlive) return enemy;
            
            // Priority 2: NPC at tile
            foreach (var npc in _npcs)
            {
                Point npcTile = new Point(
                    (int)(npc.Position.X / _world.TileSize),
                    (int)(npc.Position.Y / _world.TileSize)
                );
                if (npcTile == tile) return npc;
            }
            
            // Priority 3: Structure at tile
            var structure = GameServices.Building.GetStructureAt(tile);
            if (structure != null) return structure;
            
            // Priority 4: Ground item at tile
            foreach (var worldItem in _groundItems)
            {
                Point itemTile = new Point(
                    (int)(worldItem.Position.X / _world.TileSize),
                    (int)(worldItem.Position.Y / _world.TileSize)
                );
                if (itemTile == tile) return worldItem;
            }
            
            // Priority 5: The tile itself
            var tileData = _world.GetTile(tile.X, tile.Y);
            if (tileData != null) return tileData;
            
            return null;
        }
        
        private void CycleTarget()
        {
            var aliveEnemies = _enemies.FindAll(e => e.IsAlive && e.InCombatZone);
            if (aliveEnemies.Count == 0)
            {
                _selectedEnemy = null;
                return;
            }
            
            // Sort: hostile/provoked first, then passive
            aliveEnemies.Sort((a, b) =>
            {
                bool aHostile = a.Behavior == CreatureBehavior.Aggressive || a.IsProvoked;
                bool bHostile = b.Behavior == CreatureBehavior.Aggressive || b.IsProvoked;
                if (aHostile && !bHostile) return -1;
                if (!aHostile && bHostile) return 1;
                return 0;
            });
            
            // If no current selection or current selection is invalid, start at first
            if (_selectedEnemy == null || !_selectedEnemy.IsAlive || !_selectedEnemy.InCombatZone)
            {
                _selectedEnemy = aliveEnemies[0];
                return;
            }
            
            // Find current index
            int currentIndex = -1;
            for (int i = 0; i < aliveEnemies.Count; i++)
            {
                if (aliveEnemies[i] == _selectedEnemy)
                {
                    currentIndex = i;
                    break;
                }
            }
            
            // Move to next enemy
            if (currentIndex >= 0)
            {
                int nextIndex = (currentIndex + 1) % aliveEnemies.Count;
                _selectedEnemy = aliveEnemies[nextIndex];
            }
            else
            {
                // Current selection not in list, start at first
                _selectedEnemy = aliveEnemies[0];
            }
        }
        
        // ============================================
        // LOOT AND ITEM PICKUP
        // ============================================
        
        private void HandleEnemyKilled(EnemyEntity enemy, Vector2 position)
        {
            // Track quest progress
            GameServices.Quests.OnEnemyKilled(enemy.Type);
            
            // Generate loot and drop it on the ground
            var loot = enemy.GenerateLoot();
            var random = new Random();
            
            foreach (var item in loot)
            {
                // Scatter items slightly around the death position
                float offsetX = (float)(random.NextDouble() - 0.5) * 32;
                float offsetY = (float)(random.NextDouble() - 0.5) * 32;
                Vector2 dropPos = position + new Vector2(offsetX, offsetY);
                
                var worldItem = new WorldItem(item, dropPos);
                _groundItems.Add(worldItem);
                
                System.Diagnostics.Debug.WriteLine($">>> Dropped: {item.GetDisplayName()} at ({dropPos.X:F0}, {dropPos.Y:F0}) <<<");
            }
            
            if (loot.Count > 0)
            {
                _combatLog.Add($"Dropped {loot.Count} item(s)!");
            }
        }
        
        /// <summary>
        /// Handle enemy spawn from abilities (like HiveMother)
        /// </summary>
        private void HandleEnemySpawn(EnemyType type, Vector2 position)
        {
            int tileSize = _world.TileSize;
            Point spawnTile = new Point((int)(position.X / tileSize), (int)(position.Y / tileSize));
            
            // Find unoccupied spawn position
            Point finalTile = FindUnoccupiedSpawnTile(spawnTile);
            Vector2 finalPosition = new Vector2(finalTile.X * tileSize, finalTile.Y * tileSize);
            
            int index = _enemies.Count + 1;
            var newEnemy = EnemyEntity.Create(type, finalPosition, index);
            
            // Add to enemies list
            _enemies.Add(newEnemy);
            
            // If in combat, add to combat
            if (_combat.InCombat)
            {
                _combat.PullEnemyIntoCombat(newEnemy);
            }
            
            System.Diagnostics.Debug.WriteLine($">>> Spawned {type} at ({finalPosition.X:F0}, {finalPosition.Y:F0}) <<<");
        }
        
        /// <summary>
        /// Find unoccupied tile near desired position
        /// </summary>
        private Point FindUnoccupiedSpawnTile(Point desired)
        {
            int tileSize = _world.TileSize;
            Point playerTile = new Point((int)(_player.Position.X / tileSize), (int)(_player.Position.Y / tileSize));
            
            // Check if desired is free
            if (!IsTileOccupied(desired, playerTile))
            {
                return desired;
            }
            
            // Spiral search for nearby free tile
            for (int radius = 1; radius <= 5; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        Point candidate = new Point(desired.X + dx, desired.Y + dy);
                        if (!IsTileOccupied(candidate, playerTile))
                        {
                            return candidate;
                        }
                    }
                }
            }
            
            return desired; // Fallback
        }
        
        /// <summary>
        /// Check if tile is occupied by player or enemy
        /// </summary>
        private bool IsTileOccupied(Point tile, Point playerTile)
        {
            int tileSize = _world.TileSize;
            
            // Check player
            if (tile == playerTile) return true;
            
            // Check walkability
            if (tile.X < 0 || tile.X >= _world.Width || tile.Y < 0 || tile.Y >= _world.Height)
                return true;
            var t = _world.GetTile(tile.X, tile.Y);
            if (t == null || !t.IsWalkable) return true;
            
            // Check enemies
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsAlive) continue;
                Point enemyTile = new Point((int)(enemy.Position.X / tileSize), (int)(enemy.Position.Y / tileSize));
                if (enemyTile == tile) return true;
            }
            
            return false;
        }
        
        private void UpdateNearestItem()
        {
            _nearestItem = null;
            float nearestDist = float.MaxValue;
            
            foreach (var worldItem in _groundItems)
            {
                if (!worldItem.CanPickup) continue;
                
                float dist = Vector2.Distance(_player.Position, worldItem.Position);
                if (dist < worldItem.PickupRadius && dist < nearestDist)
                {
                    nearestDist = dist;
                    _nearestItem = worldItem;
                }
            }
        }
        
        private void TryPickupItem()
        {
            if (_nearestItem == null) return;
            
            // Try to add to inventory
            var itemToAdd = _nearestItem.Item;
            if (_player.Stats.Inventory.TryAddItem(itemToAdd.ItemDefId, itemToAdd.StackCount, itemToAdd.Quality))
            {
                _groundItems.Remove(_nearestItem);
                System.Diagnostics.Debug.WriteLine($">>> Picked up: {_nearestItem.Item.GetDisplayName()} <<<");
                _nearestItem = null;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(">>> Inventory full! Cannot pick up item. <<<");
            }
        }
        
        private void TryPickupAllNearby()
        {
            var itemsToRemove = new List<WorldItem>();
            
            foreach (var worldItem in _groundItems)
            {
                if (!worldItem.CanPickup) continue;
                if (!worldItem.IsInPickupRange(_player.Position)) continue;
                
                var itemToAdd = worldItem.Item;
                if (_player.Stats.Inventory.TryAddItem(itemToAdd.ItemDefId, itemToAdd.StackCount, itemToAdd.Quality))
                {
                    itemsToRemove.Add(worldItem);
                    
                    // Track quest progress
                    GameServices.Quests.OnItemCollected(worldItem.Item.ItemDefId, worldItem.Item.StackCount);
                    
                    System.Diagnostics.Debug.WriteLine($">>> Picked up: {worldItem.Item.GetDisplayName()} <<<");
                }
            }
            
            foreach (var item in itemsToRemove)
            {
                _groundItems.Remove(item);
            }
            
            if (itemsToRemove.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($">>> Picked up {itemsToRemove.Count} items total <<<");
            }
        }
        
        // ============================================
        // INVENTORY UI
        // ============================================
        
        private void UpdateInventoryUI(KeyboardState kState, MouseState mState)
        {
            var items = _player.Stats.Inventory.GetAllItems();
            int maxIndex = items.Count - 1;
            
            // Navigation with keyboard
            if (kState.IsKeyDown(Keys.W) && _prevKeyboardState.IsKeyUp(Keys.W))
            {
                _selectedInventoryIndex = Math.Max(0, _selectedInventoryIndex - 1);
            }
            if (kState.IsKeyDown(Keys.S) && _prevKeyboardState.IsKeyUp(Keys.S))
            {
                _selectedInventoryIndex = Math.Min(maxIndex, _selectedInventoryIndex + 1);
            }
            
            // P key opens body/gear panel directly
            if (kState.IsKeyDown(Keys.P) && _prevKeyboardState.IsKeyUp(Keys.P))
            {
                _inventoryOpen = false;
                _bodyPanelOpen = true;
                _bodyPanelScroll = 0;
            }
            
            // Drop item with X
            if (kState.IsKeyDown(Keys.X) && _prevKeyboardState.IsKeyUp(Keys.X))
            {
                DropSelectedItem();
            }
            
            // Mouse click handling for inventory list
            if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                // Inventory list area (must match DrawInventoryList coordinates)
                int startX = 50;
                int startY = 90;
                int itemWidth = 580;
                int itemHeight = 30;
                int maxVisible = 18;
                
                for (int i = 0; i < items.Count && i < maxVisible; i++)
                {
                    Rectangle itemRect = new Rectangle(startX, startY + i * itemHeight, itemWidth, itemHeight);
                    if (itemRect.Contains(mState.X, mState.Y))
                    {
                        if (_selectedInventoryIndex == i)
                        {
                            // Double-click effect - use consumable directly
                            if (items[i].Category == ItemCategory.Consumable)
                            {
                                UseSelectedInventoryItem();
                            }
                        }
                        else
                        {
                            _selectedInventoryIndex = i;
                        }
                        break;
                    }
                }
                
                // Gear button - opens body panel for equipping
                Rectangle gearBtn = new Rectangle(660, 520, 150, 35);
                if (gearBtn.Contains(mState.X, mState.Y))
                {
                    _inventoryOpen = false;
                    _bodyPanelOpen = true;
                    _bodyPanelScroll = 0;
                }
                
                // Drop button
                Rectangle dropBtn = new Rectangle(660, 565, 150, 35);
                if (dropBtn.Contains(mState.X, mState.Y))
                {
                    DropSelectedItem();
                }
            }
        }
        
        /// <summary>
        /// Use or equip the currently selected inventory item
        /// </summary>
        private void UseSelectedInventoryItem()
        {
            var items = _player.Stats.Inventory.GetAllItems();
            if (_selectedInventoryIndex < 0 || _selectedInventoryIndex >= items.Count) return;
            
            var item = items[_selectedInventoryIndex];
            
            if (item.Category == ItemCategory.Consumable)
            {
                // Check if it's a medical item that should target body parts
                if (item.Definition?.IsMedical == true)
                {
                    // Auto-heal most damaged/critical part
                    var targetPart = _player.Stats.Body.GetMostCriticalPart();
                    if (targetPart != null)
                    {
                        bool success = targetPart.ApplyMedicalItem(item);
                        if (success)
                        {
                            _player.Stats.Inventory.RemoveItem(item.ItemDefId, 1);
                            _player.Stats.SyncHPWithBody();
                            ShowNotification($"Applied {item.Name} to {targetPart.Name}");
                        }
                        else
                        {
                            ShowNotification($"No injuries to treat with {item.Name}");
                        }
                    }
                    else
                    {
                        ShowNotification("No injured body parts!");
                    }
                }
                else
                {
                    // Regular consumable (food/drink)
                    var effects = _player.Stats.Inventory.UseConsumable(item);
                    if (effects != null)
                    {
                        _player.Stats.Survival.RestoreHunger(effects.HungerRestore);
                        _player.Stats.Survival.RestoreThirst(effects.ThirstRestore);
                        
                        // Health restore goes to most damaged part
                        if (effects.HealthRestore > 0)
                        {
                            float hpRestored = _player.Stats.HealMostDamagedPart(effects.HealthRestore);
                            if (hpRestored > 0)
                            {
                                ShowNotification($"Used {item.Name} (+{hpRestored:F0} HP)");
                            }
                            else
                            {
                                ShowNotification($"Used {item.Name}");
                            }
                        }
                        else
                        {
                            ShowNotification($"Used {item.Name}");
                        }
                    }
                }
            }
            else if (item.Definition?.EquipSlot != EquipSlot.None)
            {
                // For weapons/armor, prompt to use Gear Window
                ShowNotification("Open GEAR WINDOW [P] to equip");
            }
        }
        
        /// <summary>
        /// Drop the currently selected inventory item
        /// </summary>
        private void DropSelectedItem()
        {
            var items = _player.Stats.Inventory.GetAllItems();
            if (_selectedInventoryIndex < 0 || _selectedInventoryIndex >= items.Count) return;
            
            var item = items[_selectedInventoryIndex];
            _player.Stats.Inventory.RemoveItem(item);
            
            // Drop on ground near player
            var worldItem = new WorldItem(item, _player.Position);
            _groundItems.Add(worldItem);
            ShowNotification($"Dropped {item.GetDisplayName()}");
            System.Diagnostics.Debug.WriteLine($">>> Dropped: {item.GetDisplayName()} <<<");
            
            // Adjust selection
            if (_selectedInventoryIndex > 0 && _selectedInventoryIndex >= items.Count - 1)
            {
                _selectedInventoryIndex--;
            }
        }
        
        private void UpdateCraftingUI(KeyboardState kState, MouseState mState)
        {
            var recipes = GameServices.Crafting.GetAvailableRecipes(_activeWorkstationType, _player.Stats);
            int maxIndex = recipes.Count - 1;
            
            // Close crafting
            if (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                _craftingOpen = false;
                _nearestWorkstation = null;
                _activeWorkstationType = null;
                return;
            }
            if (kState.IsKeyDown(Keys.C) && _prevKeyboardState.IsKeyUp(Keys.C))
            {
                _craftingOpen = false;
                _nearestWorkstation = null;
                _activeWorkstationType = null;
                return;
            }
            
            // Navigation
            if (kState.IsKeyDown(Keys.W) && _prevKeyboardState.IsKeyUp(Keys.W))
            {
                _selectedRecipeIndex = Math.Max(0, _selectedRecipeIndex - 1);
            }
            if (kState.IsKeyDown(Keys.S) && _prevKeyboardState.IsKeyUp(Keys.S))
            {
                _selectedRecipeIndex = Math.Min(maxIndex, _selectedRecipeIndex + 1);
            }
            
            // Craft selected recipe
            if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
            {
                if (_selectedRecipeIndex >= 0 && _selectedRecipeIndex < recipes.Count)
                {
                    var recipe = recipes[_selectedRecipeIndex];
                    var craftedItem = GameServices.Crafting.TryCraft(recipe, _activeWorkstationType, _player.Stats);
                    
                    if (craftedItem != null)
                    {
                        // Track quest progress - crafting
                        GameServices.Quests.OnItemCrafted(recipe.Id);
                        
                        ShowNotification($"Crafted: {craftedItem.GetDisplayName()}");
                    }
                    else
                    {
                        ShowNotification("Cannot craft - missing materials!");
                    }
                }
            }
            
            // Mouse click on recipe
            if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                // Updated coordinates to match DrawCraftingUI
                int startX = 50;
                int startY = 110;
                int itemHeight = 35;
                int itemWidth = 430;
                
                for (int i = 0; i < recipes.Count && i < 14; i++)
                {
                    Rectangle itemRect = new Rectangle(startX, startY + i * itemHeight, itemWidth, itemHeight - 2);
                    if (itemRect.Contains(mState.X, mState.Y))
                    {
                        _selectedRecipeIndex = i;
                        break;
                    }
                }
                
                // Craft button click (approximate position in recipe details panel)
                Rectangle craftBtn = new Rectangle(525, 340, 180, 35);
                if (craftBtn.Contains(mState.X, mState.Y))
                {
                    CraftSelectedRecipe();
                }
            }
        }
        
        private void UpdateTradingUI(KeyboardState kState, MouseState mState)
        {
            if (_tradingNPC == null)
            {
                _tradingOpen = false;
                return;
            }
            
            // Close trading
            if (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                _tradingOpen = false;
                _tradingNPC = null;
                return;
            }
            if (kState.IsKeyDown(Keys.T) && _prevKeyboardState.IsKeyUp(Keys.T))
            {
                _tradingOpen = false;
                _tradingNPC = null;
                return;
            }
            
            // Tab to switch buy/sell mode
            if (kState.IsKeyDown(Keys.Tab) && _prevKeyboardState.IsKeyUp(Keys.Tab))
            {
                _tradingBuyMode = !_tradingBuyMode;
                _selectedTradeIndex = 0;
            }
            
            // Get current list
            int maxIndex = 0;
            if (_tradingBuyMode)
            {
                maxIndex = _tradingNPC.Stock.Count(s => s.Quantity > 0) - 1;
            }
            else
            {
                maxIndex = _player.Stats.Inventory.GetAllItems().Count - 1;
            }
            
            // Navigation
            if (kState.IsKeyDown(Keys.W) && _prevKeyboardState.IsKeyUp(Keys.W))
            {
                _selectedTradeIndex = Math.Max(0, _selectedTradeIndex - 1);
            }
            if (kState.IsKeyDown(Keys.S) && _prevKeyboardState.IsKeyUp(Keys.S))
            {
                _selectedTradeIndex = Math.Min(maxIndex, _selectedTradeIndex + 1);
            }
            
            // Execute trade
            if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
            {
                ExecuteTrade();
            }
            
            // Mouse support
            if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                // Check tab clicks (coordinates must match DrawTradingUI)
                Rectangle buyTabRect = new Rectangle(50, 145, 150, 35);
                Rectangle sellTabRect = new Rectangle(210, 145, 150, 35);
                
                if (buyTabRect.Contains(mState.X, mState.Y))
                {
                    _tradingBuyMode = true;
                    _selectedTradeIndex = 0;
                }
                else if (sellTabRect.Contains(mState.X, mState.Y))
                {
                    _tradingBuyMode = false;
                    _selectedTradeIndex = 0;
                }
                else
                {
                    // Check item list clicks (coordinates must match DrawMerchantStock/DrawPlayerSellList)
                    int startY = 200;
                    int itemHeight = 32;
                    int itemWidth = 550;
                    int maxItems = _tradingBuyMode 
                        ? _tradingNPC.Stock.Count(s => s.Quantity > 0) 
                        : _player.Stats.Inventory.GetAllItems().Count;
                    
                    for (int i = 0; i < maxItems && i < 12; i++)
                    {
                        Rectangle itemRect = new Rectangle(50, startY + i * itemHeight, itemWidth, itemHeight - 2);
                        if (itemRect.Contains(mState.X, mState.Y))
                        {
                            if (_selectedTradeIndex == i)
                            {
                                // Double-click effect: execute trade
                                ExecuteTrade();
                            }
                            else
                            {
                                _selectedTradeIndex = i;
                            }
                            break;
                        }
                    }
                }
            }
        }
        
        private void ExecuteTrade()
        {
            if (_tradingNPC == null) return;
            
            if (_tradingBuyMode)
            {
                // Buy from merchant
                var availableStock = _tradingNPC.Stock.Where(s => s.Quantity > 0).ToList();
                if (_selectedTradeIndex >= 0 && _selectedTradeIndex < availableStock.Count)
                {
                    var stock = availableStock[_selectedTradeIndex];
                    int price = _tradingNPC.GetSellPrice(stock.ItemId);
                    int playerGold = _player.Stats.Gold;
                    
                    if (_tradingNPC.SellToPlayer(stock.ItemId, 1, _player.Stats.Inventory, ref playerGold))
                    {
                        _player.Stats.Gold = playerGold;
                        var itemDef = ItemDatabase.Get(stock.ItemId);
                        ShowNotification($"Bought {itemDef?.Name ?? stock.ItemId} for {price}g");
                    }
                    else
                    {
                        ShowNotification("Cannot buy - not enough gold or inventory full!");
                    }
                }
            }
            else
            {
                // Sell to merchant
                var items = _player.Stats.Inventory.GetAllItems();
                if (_selectedTradeIndex >= 0 && _selectedTradeIndex < items.Count)
                {
                    var item = items[_selectedTradeIndex];
                    int price = _tradingNPC.GetBuyPrice(item);
                    int playerGold = _player.Stats.Gold;
                    
                    if (_tradingNPC.BuyFromPlayer(item, 1, _player.Stats.Inventory, ref playerGold))
                    {
                        _player.Stats.Gold = playerGold;
                        ShowNotification($"Sold {item.Name} for {price}g");
                        
                        // Adjust selection if needed
                        if (_selectedTradeIndex >= _player.Stats.Inventory.GetAllItems().Count)
                        {
                            _selectedTradeIndex = Math.Max(0, _selectedTradeIndex - 1);
                        }
                    }
                    else
                    {
                        ShowNotification("Cannot sell - merchant can't afford it!");
                    }
                }
            }
        }
        
        // ============================================
        // QUEST LOG UI
        // ============================================
        
        private void UpdateQuestLogUI(KeyboardState kState, MouseState mState)
        {
            var activeQuests = GameServices.Quests.GetActiveQuests();
            int maxIndex = activeQuests.Count - 1;
            
            // Close (Escape only - J toggle is handled in UpdatePlaying)
            if (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                _questLogOpen = false;
                return;
            }
            
            // Navigation
            if (kState.IsKeyDown(Keys.W) && _prevKeyboardState.IsKeyUp(Keys.W))
            {
                _selectedQuestIndex = Math.Max(0, _selectedQuestIndex - 1);
            }
            if (kState.IsKeyDown(Keys.S) && _prevKeyboardState.IsKeyUp(Keys.S))
            {
                _selectedQuestIndex = Math.Min(maxIndex, _selectedQuestIndex + 1);
            }
            
            // Abandon quest (Delete key)
            if (kState.IsKeyDown(Keys.Delete) && _prevKeyboardState.IsKeyUp(Keys.Delete))
            {
                if (_selectedQuestIndex >= 0 && _selectedQuestIndex < activeQuests.Count)
                {
                    var quest = activeQuests[_selectedQuestIndex];
                    if (quest.Definition.Type != QuestType.Main)  // Can't abandon main quests
                    {
                        GameServices.Quests.AbandonQuest(quest.QuestId);
                        ShowNotification($"Abandoned: {quest.Definition.Name}");
                        _selectedQuestIndex = Math.Min(_selectedQuestIndex, activeQuests.Count - 2);
                    }
                    else
                    {
                        ShowNotification("Cannot abandon main quests!");
                    }
                }
            }
        }
        
        private void UpdateQuestDialogueUI(KeyboardState kState, MouseState mState)
        {
            int totalOptions = _turnInQuests.Count + _availableNPCQuests.Count + 1;  // +1 for "Trade" option
            int maxIndex = totalOptions - 1;
            
            // Close
            if (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                _questDialogueOpen = false;
                return;
            }
            
            // Navigation
            if (kState.IsKeyDown(Keys.W) && _prevKeyboardState.IsKeyUp(Keys.W))
            {
                _selectedDialogueIndex = Math.Max(0, _selectedDialogueIndex - 1);
            }
            if (kState.IsKeyDown(Keys.S) && _prevKeyboardState.IsKeyUp(Keys.S))
            {
                _selectedDialogueIndex = Math.Min(maxIndex, _selectedDialogueIndex + 1);
            }
            
            // Select option
            if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
            {
                int index = _selectedDialogueIndex;
                
                // Turn-in quests first
                if (index < _turnInQuests.Count)
                {
                    var quest = _turnInQuests[index];
                    GameServices.Quests.TurnInQuest(quest.QuestId, (reward) =>
                    {
                        // Apply rewards
                        _player.Stats.Gold += reward.Gold;
                        _player.Stats.AddXP(reward.XP);
                        if (reward.MutationPoints > 0)
                        {
                            _player.Stats.AddMutationPoints(reward.MutationPoints);
                        }
                        foreach (var item in reward.Items)
                        {
                            _player.Stats.Inventory.TryAddItem(item.Key, item.Value);
                        }
                        foreach (var recipe in reward.UnlockRecipes)
                        {
                            GameServices.Crafting.UnlockRecipe(recipe);
                        }
                    });
                    ShowNotification($"Quest complete: {quest.Definition.Name}!");
                    
                    // Refresh lists
                    _availableNPCQuests = GameServices.Quests.GetQuestsFromNPC(_tradingNPC.Id, _player.Stats.Level);
                    _turnInQuests = GameServices.Quests.GetQuestsToTurnIn(_tradingNPC.Id);
                    _selectedDialogueIndex = 0;
                }
                // Available quests next
                else if (index < _turnInQuests.Count + _availableNPCQuests.Count)
                {
                    int questIndex = index - _turnInQuests.Count;
                    var quest = _availableNPCQuests[questIndex];
                    
                    if (GameServices.Quests.AcceptQuest(quest.Id))
                    {
                        ShowNotification($"Quest accepted: {quest.Name}");
                        
                        // Refresh lists
                        _availableNPCQuests = GameServices.Quests.GetQuestsFromNPC(_tradingNPC.Id, _player.Stats.Level);
                        _selectedDialogueIndex = 0;
                    }
                }
                // "Trade" option (last)
                else
                {
                    _questDialogueOpen = false;
                    _tradingOpen = true;
                    _selectedTradeIndex = 0;
                    _tradingBuyMode = true;
                }
            }
            
            // Mouse clicks
            if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                int startY = 180;
                int itemHeight = 35;
                
                for (int i = 0; i < totalOptions; i++)
                {
                    Rectangle rect = new Rectangle(100, startY + i * itemHeight, 500, itemHeight - 2);
                    if (rect.Contains(mState.X, mState.Y))
                    {
                        if (_selectedDialogueIndex == i)
                        {
                            // Simulate Enter press
                            _selectedDialogueIndex = i;
                            // Trigger selection on next frame (avoid double handling)
                        }
                        else
                        {
                            _selectedDialogueIndex = i;
                        }
                        break;
                    }
                }
            }
        }
        
        // ============================================
        // RESEARCH UI
        // ============================================
        
        private void UpdateResearchUI(KeyboardState kState, MouseState mState)
        {
            if (_researchCategories == null || _researchCategories.Length == 0) return;
            
            var currentCategory = _researchCategories[_selectedResearchCategory];
            var nodes = GameServices.Research.GetNodesByCategory(currentCategory);
            int maxIndex = nodes.Count - 1;
            
            // Close
            if (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                _researchOpen = false;
                return;
            }
            
            // Tab to switch categories
            if (kState.IsKeyDown(Keys.Tab) && _prevKeyboardState.IsKeyUp(Keys.Tab))
            {
                _selectedResearchCategory = (_selectedResearchCategory + 1) % _researchCategories.Length;
                _selectedResearchIndex = 0;
            }
            
            // Navigation
            if (kState.IsKeyDown(Keys.W) && _prevKeyboardState.IsKeyUp(Keys.W))
            {
                _selectedResearchIndex = Math.Max(0, _selectedResearchIndex - 1);
            }
            if (kState.IsKeyDown(Keys.S) && _prevKeyboardState.IsKeyUp(Keys.S))
            {
                _selectedResearchIndex = Math.Min(maxIndex, _selectedResearchIndex + 1);
            }
            
            // Start research
            if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
            {
                if (_selectedResearchIndex >= 0 && _selectedResearchIndex < nodes.Count)
                {
                    var node = nodes[_selectedResearchIndex];
                    if (node.State == ResearchState.Available && !GameServices.Research.IsResearching)
                    {
                        // Check if player has resources
                        var inventory = _player.Stats.Inventory;
                        bool hasResources = true;
                        foreach (var cost in node.ResourceCost)
                        {
                            if (!inventory.HasItem(cost.Key, cost.Value))
                            {
                                hasResources = false;
                                break;
                            }
                        }
                        
                        if (hasResources)
                        {
                            GameServices.Research.StartResearch(node.Id, (itemId, amount) =>
                            {
                                inventory.RemoveItem(itemId, amount);
                            });
                            ShowNotification($"Started research: {node.Name}");
                        }
                        else
                        {
                            ShowNotification("Missing required resources!");
                        }
                    }
                    else if (node.State == ResearchState.InProgress)
                    {
                        ShowNotification("Research already in progress!");
                    }
                    else if (node.State == ResearchState.Locked)
                    {
                        ShowNotification("Prerequisites not met!");
                    }
                }
            }
            
            // Cancel research (X key)
            if (kState.IsKeyDown(Keys.X) && _prevKeyboardState.IsKeyUp(Keys.X))
            {
                if (GameServices.Research.IsResearching)
                {
                    GameServices.Research.CancelResearch();
                    ShowNotification("Research cancelled (resources lost)");
                }
            }
            
            // Update research progress
            GameServices.Research.UpdateAvailability(_player.Stats.Level);
        }
        
        // ============================================
        // BODY PANEL UI (with drag & drop)
        // ============================================
        
        private void UpdateBodyPanelUI(KeyboardState kState, MouseState mState)
        {
            var mousePos = new Vector2(mState.X, mState.Y);
            bool leftClick = mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released;
            bool leftHeld = mState.LeftButton == ButtonState.Pressed;
            bool leftReleased = mState.LeftButton == ButtonState.Released && _prevMouseState.LeftButton == ButtonState.Pressed;
            bool rightClick = mState.RightButton == ButtonState.Pressed && _prevMouseState.RightButton == ButtonState.Released;
            
            // ESC to close (P toggle is handled in UpdatePlaying, not here to avoid double-processing)
            if (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                // Cancel any drag in progress
                if (_isDragging)
                {
                    _isDragging = false;
                    _draggedItem = null;
                    _dragSourceIndex = -1;
                }
                _bodyPanelOpen = false;
                return;
            }
            
            // Panel dimensions
            int panelWidth = 900;
            int panelHeight = 600;
            int panelX = (1280 - panelWidth) / 2;
            int panelY = (720 - panelHeight) / 2;
            
            // Left side: Inventory (for dragging items from)
            Rectangle inventoryArea = new Rectangle(panelX + 10, panelY + 50, 280, panelHeight - 60);
            
            // Right side: Body parts display
            Rectangle bodyArea = new Rectangle(panelX + 300, panelY + 50, 590, panelHeight - 60);
            
            // Get all body parts
            var bodyParts = _player.Stats.Body.Parts.Values.ToList();
            var inventory = _player.Stats.Inventory.GetAllItems();
            
            // Reset hover
            _hoverBodyPart = null;
            
            // DRAG START - Click on inventory item
            if (leftClick && !_isDragging && inventoryArea.Contains(mousePos.ToPoint()))
            {
                int itemHeight = 28;
                int startY = inventoryArea.Y + 28;  // Must match DrawBodyPanelUI (accounts for header)
                
                for (int i = 0; i < inventory.Count; i++)
                {
                    Rectangle itemRect = new Rectangle(inventoryArea.X + 5, startY + i * itemHeight, inventoryArea.Width - 10, itemHeight - 2);
                    if (itemRect.Contains(mousePos.ToPoint()))
                    {
                        _isDragging = true;
                        _draggedItem = inventory[i];
                        _dragSourceIndex = i;
                        _dragOffset = mousePos - new Vector2(itemRect.X, itemRect.Y);
                        _dragPosition = mousePos;
                        break;
                    }
                }
            }
            
            // DRAGGING - Update position and hover target
            if (_isDragging)
            {
                _dragPosition = mousePos;
                
                // Check which body part we're hovering over (for visual feedback)
                // Sort body parts same as Draw to match positions
                var sortedParts = bodyParts.OrderBy(p => GetBodyPartSortOrder(p.Type)).ToList();
                int partHeight = 55;
                int startY = bodyArea.Y + 28 - _bodyPanelScroll;  // Must match DrawBodyPanelUI
                int col = 0;
                int row = 0;
                int colWidth = 290;
                
                _hoverBodyPart = null;  // Reset each frame
                foreach (var part in sortedParts)
                {
                    int partX = bodyArea.X + 5 + col * colWidth;
                    int partY = startY + row * partHeight;
                    Rectangle partRect = new Rectangle(partX, partY, colWidth - 10, partHeight - 5);
                    
                    if (partRect.Contains(mousePos.ToPoint()))
                    {
                        _hoverBodyPart = part;
                        break;
                    }
                    
                    row++;
                    if (row >= 10) // 10 parts per column
                    {
                        row = 0;
                        col++;
                    }
                }
            }
            
            // DRAG END - Release on body part
            if (leftReleased && _isDragging)
            {
                // Detect which body part is under the mouse at release time
                var sortedPartsForDrop = bodyParts.OrderBy(p => GetBodyPartSortOrder(p.Type)).ToList();
                int dropPartHeight = 55;
                int dropStartY = bodyArea.Y + 28 - _bodyPanelScroll;
                int dropCol = 0;
                int dropRow = 0;
                int dropColWidth = 290;
                BodyPart dropTarget = null;
                
                foreach (var part in sortedPartsForDrop)
                {
                    int partX = bodyArea.X + 5 + dropCol * dropColWidth;
                    int partY = dropStartY + dropRow * dropPartHeight;
                    Rectangle partRect = new Rectangle(partX, partY, dropColWidth - 10, dropPartHeight - 5);
                    
                    if (partRect.Contains(mousePos.ToPoint()))
                    {
                        dropTarget = part;
                        break;
                    }
                    
                    dropRow++;
                    if (dropRow >= 10)
                    {
                        dropRow = 0;
                        dropCol++;
                    }
                }
                
                if (dropTarget != null && _draggedItem != null)
                {
                    // Try to apply/equip item to body part
                    bool success = TryApplyItemToBodyPart(_draggedItem, dropTarget);
                    if (success)
                    {
                        // Show appropriate notification
                        if (_draggedItem.Definition?.IsMedical == true)
                        {
                            _player.Stats.Inventory.RemoveItem(_draggedItem.ItemDefId, 1);
                            ShowNotification($"Applied {_draggedItem.Name} to {dropTarget.Name}");
                        }
                        else if (_draggedItem.Category == ItemCategory.Weapon && dropTarget.CanEquipWeapon)
                        {
                            ShowNotification($"Equipped {_draggedItem.Name} to {dropTarget.Name}");
                        }
                        else if (_draggedItem.Category == ItemCategory.Armor && dropTarget.CanEquipArmor)
                        {
                            ShowNotification($"Equipped {_draggedItem.Name} to {dropTarget.Name}");
                        }
                        else if (_draggedItem.Category == ItemCategory.Consumable)
                        {
                            _player.Stats.Inventory.RemoveItem(_draggedItem.ItemDefId, 1);
                            ShowNotification($"Used {_draggedItem.Name} on {dropTarget.Name}");
                        }
                    }
                    else
                    {
                        ShowNotification($"Can't use {_draggedItem.Name} on {dropTarget.Name}");
                    }
                }
                
                _isDragging = false;
                _draggedItem = null;
                _dragSourceIndex = -1;
                _hoverBodyPart = null;
            }
            
            // RIGHT CLICK on body part - Show details / unequip
            if (rightClick && bodyArea.Contains(mousePos.ToPoint()))
            {
                // Sort body parts same as Draw to match positions
                var sortedParts = bodyParts.OrderBy(p => GetBodyPartSortOrder(p.Type)).ToList();
                int partHeight = 55;
                int startY = bodyArea.Y + 28 - _bodyPanelScroll;  // Must match DrawBodyPanelUI
                int col = 0;
                int row = 0;
                int colWidth = 290;
                
                foreach (var part in sortedParts)
                {
                    int partX = bodyArea.X + 5 + col * colWidth;
                    int partY = startY + row * partHeight;
                    Rectangle partRect = new Rectangle(partX, partY, colWidth - 10, partHeight - 5);
                    
                    if (partRect.Contains(mousePos.ToPoint()))
                    {
                        _selectedBodyPartId = part.Id;
                        
                        // If part has equipped item, unequip it
                        if (part.EquippedItem != null)
                        {
                            var item = part.UnequipItem();
                            _player.Stats.Inventory.TryAddItem(item.ItemDefId, item.StackCount, item.Quality);
                            ShowNotification($"Unequipped {item.Name} from {part.Name}");
                        }
                        break;
                    }
                    
                    row++;
                    if (row >= 10)
                    {
                        row = 0;
                        col++;
                    }
                }
            }
            
            // Scroll body parts list
            int scroll = mState.ScrollWheelValue - _prevMouseState.ScrollWheelValue;
            if (scroll != 0 && bodyArea.Contains(mousePos.ToPoint()))
            {
                _bodyPanelScroll -= scroll / 4;
                _bodyPanelScroll = Math.Clamp(_bodyPanelScroll, 0, Math.Max(0, bodyParts.Count * 55 - bodyArea.Height));
            }
        }
        
        private bool TryApplyItemToBodyPart(Item item, BodyPart part)
        {
            if (item == null || part == null) return false;
            
            // Medical items - heal body part
            if (item.Definition != null && item.Definition.IsMedical)
            {
                bool healed = part.ApplyMedicalItem(item);
                if (healed)
                {
                    _player.Stats.SyncHPWithBody();  // Sync overall HP
                }
                return healed;
            }
            
            // Weapons - equip to hands
            if (item.Category == ItemCategory.Weapon && part.CanEquipWeapon)
            {
                // Unequip current weapon if any
                if (part.EquippedItem != null)
                {
                    var old = part.UnequipItem();
                    _player.Stats.Inventory.TryAddItem(old.ItemDefId, old.StackCount, old.Quality);
                }
                
                // Equip new weapon
                part.EquipItem(item);
                _player.Stats.Inventory.RemoveItem(item.ItemDefId, 1);
                return true;
            }
            
            // Armor - equip to body parts
            if (item.Category == ItemCategory.Armor && part.CanEquipArmor)
            {
                // Unequip current armor if any
                if (part.EquippedItem != null)
                {
                    var old = part.UnequipItem();
                    _player.Stats.Inventory.TryAddItem(old.ItemDefId, old.StackCount, old.Quality);
                }
                
                // Equip new armor
                part.EquipItem(item);
                _player.Stats.Inventory.RemoveItem(item.ItemDefId, 1);
                return true;
            }
            
            // Consumables (food/drink) - use on injured parts to restore health
            if (item.Category == ItemCategory.Consumable && item.Definition != null)
            {
                if (item.Definition.HealthRestore > 0 && part.CurrentHealth < part.MaxHealth)
                {
                    part.Heal(item.Definition.HealthRestore);
                    _player.Stats.SyncHPWithBody();  // Sync overall HP
                    return true;
                }
            }
            
            return false;
        }
        
        private void TryUnequipSlot(EquipSlot slot)
        {
            var equipped = _player.Stats.Inventory.GetEquipped(slot);
            if (equipped != null)
            {
                _player.Stats.Inventory.UnequipSlot(slot);
                System.Diagnostics.Debug.WriteLine($">>> Unequipped: {equipped.GetDisplayName()} from {slot} <<<");
            }
        }
        
        private void HandleDebugKeys(KeyboardState kState)
        {
            // F1-F7: Only work outside combat
            if (!_combat.InCombat)
            {
                if (kState.IsKeyDown(Keys.F1) && _prevKeyboardState.IsKeyUp(Keys.F1))
                    System.Diagnostics.Debug.WriteLine("\n" + _player.Stats.GetStatusReport());
                
                if (kState.IsKeyDown(Keys.F2) && _prevKeyboardState.IsKeyUp(Keys.F2))
                    System.Diagnostics.Debug.WriteLine("\n" + _player.Stats.Body.GetStatusReport());
                
                if (kState.IsKeyDown(Keys.F3) && _prevKeyboardState.IsKeyUp(Keys.F3))
                    _player.Stats.AddXP(50);
                
                if (kState.IsKeyDown(Keys.F4) && _prevKeyboardState.IsKeyUp(Keys.F4))
                    _player.Stats.TakeDamage(20, DamageType.Physical);
                
                if (kState.IsKeyDown(Keys.F5) && _prevKeyboardState.IsKeyUp(Keys.F5))
                    _player.Stats.Heal(20);
                
                if (kState.IsKeyDown(Keys.F6) && _prevKeyboardState.IsKeyUp(Keys.F6))
                    _player.Stats.Body.TakeDamage(25, DamageType.Physical);
                
                if (kState.IsKeyDown(Keys.F7) && _prevKeyboardState.IsKeyUp(Keys.F7))
                    _player.Initialize();
            }
            
            // F8 - Toggle stealth/hidden (works IN combat only)
            if (kState.IsKeyDown(Keys.F8) && _prevKeyboardState.IsKeyUp(Keys.F8))
            {
                if (_combat.InCombat)
                {
                    var escapeStatus = _combat.GetEscapeStatus();
                    if (escapeStatus.isHidden)
                    {
                        _combat.ExitStealth();
                        ShowNotification("Stealth DISABLED (debug)");
                    }
                    else
                    {
                        _combat.EnterStealth("debug");
                        ShowNotification("Stealth ENABLED (debug)");
                    }
                }
                else
                {
                    ShowNotification("F8: Enter combat first to test stealth!");
                }
            }
            
            // F9 - Toggle escape ability (debug - for testing escape mechanics)
            if (kState.IsKeyDown(Keys.F9) && _prevKeyboardState.IsKeyUp(Keys.F9))
            {
                if (_combat.InCombat)
                {
                    var escapeStatus = _combat.GetEscapeStatus();
                    if (escapeStatus.canEscape)
                    {
                        _combat.DisableEscape();
                        ShowNotification("Escape ability DISABLED (debug)");
                    }
                    else
                    {
                        _combat.EnableEscape("debug");
                        ShowNotification("Escape ability ENABLED (debug)");
                    }
                }
                else
                {
                    ShowNotification("F9: Enter combat first to test escape!");
                }
            }
            
            // F10 - Quick Save
            if (kState.IsKeyDown(Keys.F10) && _prevKeyboardState.IsKeyUp(Keys.F10))
            {
                bool success = SaveSystem.QuickSave(
                    _player,
                    _enemies,
                    _groundItems,
                    _world,
                    GameServices.Building,
                    GameServices.SurvivalSystem
                );
                
                if (success)
                {
                    ShowNotification("Game Saved!");
                    System.Diagnostics.Debug.WriteLine(">>> GAME SAVED! <<<");
                }
                else
                {
                    ShowNotification("Save Failed!");
                }
            }
            
            // F11 - Quick Load
            if (kState.IsKeyDown(Keys.F11) && _prevKeyboardState.IsKeyUp(Keys.F11))
            {
                LoadQuickSave();
            }
            
            // F12 - Give research materials (debug)
            if (kState.IsKeyDown(Keys.F12) && _prevKeyboardState.IsKeyUp(Keys.F12))
            {
                // Give some of each research material for testing
                _player.Stats.Inventory.TryAddItem("scrap_metal", 20);
                _player.Stats.Inventory.TryAddItem("components", 10);
                _player.Stats.Inventory.TryAddItem("scrap_electronics", 10);
                _player.Stats.Inventory.TryAddItem("cloth", 15);
                _player.Stats.Inventory.TryAddItem("anomaly_shard", 10);
                _player.Stats.Inventory.TryAddItem("mutagen", 5);
                _player.Stats.Inventory.TryAddItem("bone", 15);
                _player.Stats.Inventory.TryAddItem("sinew", 10);
                _player.Stats.Inventory.TryAddItem("herbs", 10);
                ShowNotification("Research materials added!");
                System.Diagnostics.Debug.WriteLine(">>> DEBUG: Added research materials <<<");
            }
            
            // I key - Show inventory
            if (kState.IsKeyDown(Keys.I) && _prevKeyboardState.IsKeyUp(Keys.I))
            {
                System.Diagnostics.Debug.WriteLine("\n" + _player.Stats.Inventory.GetInventoryReport());
            }
            
            // H key - Show survival/hunger status
            if (kState.IsKeyDown(Keys.H) && _prevKeyboardState.IsKeyUp(Keys.H))
            {
                System.Diagnostics.Debug.WriteLine("\n" + _player.Stats.Survival.GetStatusReport());
                System.Diagnostics.Debug.WriteLine(GameServices.SurvivalSystem.GetStatusReport());
            }
            
            // T key - Fast forward time (debug)
            if (kState.IsKeyDown(Keys.T) && _prevKeyboardState.IsKeyUp(Keys.T))
            {
                // Skip 1 game hour
                for (int i = 0; i < 60; i++)
                    GameServices.SurvivalSystem.Update(1f);
                System.Diagnostics.Debug.WriteLine($">>> Time skipped! Now: {GameServices.SurvivalSystem.GetTimeString()} <<<");
            }
            
            // F10 - Add random loot to test
            if (kState.IsKeyDown(Keys.F10) && _prevKeyboardState.IsKeyUp(Keys.F10))
            {
                var randomItem = Gameplay.Items.ItemDatabase.CreateRandom();
                if (randomItem != null)
                {
                    _player.Stats.Inventory.TryAddItem(randomItem.ItemDefId, randomItem.StackCount, randomItem.Quality);
                }
            }
            
            // F11 - Equip best weapon in inventory
            if (kState.IsKeyDown(Keys.F11) && _prevKeyboardState.IsKeyUp(Keys.F11))
            {
                var weapons = _player.Stats.Inventory.GetItemsByCategory(ItemCategory.Weapon);
                if (weapons.Count > 0)
                {
                    var bestWeapon = weapons[0];
                    foreach (var w in weapons)
                    {
                        if (w.GetEffectiveDamage() > bestWeapon.GetEffectiveDamage())
                            bestWeapon = w;
                    }
                    _player.Stats.Inventory.EquipItem(bestWeapon);
                }
            }
            
            // F12 - Use food/medkit if available
            if (kState.IsKeyDown(Keys.F12) && _prevKeyboardState.IsKeyUp(Keys.F12))
            {
                // Try to use medkit first, then food
                var effects = _player.Stats.Inventory.UseConsumable("medkit");
                if (effects == null)
                    effects = _player.Stats.Inventory.UseConsumable("food_canned");
                if (effects == null)
                    effects = _player.Stats.Inventory.UseConsumable("food_jerky");
                    
                if (effects != null)
                {
                    _player.Stats.Heal(effects.HealthRestore);
                    _player.Stats.Survival.RestoreHunger(effects.HungerRestore);
                    _player.Stats.Survival.RestoreThirst(effects.ThirstRestore);
                    System.Diagnostics.Debug.WriteLine($">>> Used consumable: +{effects.HealthRestore} HP, +{effects.HungerRestore} Food, +{effects.ThirstRestore} Water <<<");
                }
            }
        }

        // ============================================
        // DRAW
        // ============================================
        
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            switch (_gameState)
            {
                case GameState.CharacterCreation:
                    DrawCharacterCreation();
                    break;
                    
                case GameState.AttributeSelect:
                    DrawPlaying(); // Draw game in background
                    DrawAttributeSelect();
                    break;
                    
                case GameState.Playing:
                    DrawPlaying();
                    break;
                    
                case GameState.MutationSelect:
                    DrawPlaying(); // Draw game in background
                    DrawMutationSelect();
                    break;
                    
                case GameState.VitalOrganChoice:
                    DrawPlaying(); // Draw game in background
                    DrawVitalOrganChoice();
                    break;
                    
                case GameState.GameOver:
                    DrawPlaying(); // Draw game in background
                    DrawGameOver();
                    break;
            }

            base.Draw(gameTime);
        }
        
        // ============================================
        // DRAW: CHARACTER CREATION
        // ============================================
        
        private void DrawCharacterCreation()
        {
            _spriteBatch.Begin();
            
            // Background
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.95f);
            
            // Main panel
            DrawPanel(new Rectangle(30, 20, 1220, 680), "CREATE YOUR MUTANT");
            
            // Random Build Info Box (left side)
            Rectangle buildPanel = new Rectangle(40, 60, 500, 230);
            _spriteBatch.Draw(_pixelTexture, buildPanel, UITheme.PanelBackground * 0.5f);
            DrawBorder(buildPanel, UITheme.PanelBorder, 1);
            
            // Backstory
            var backstoryDef = GameServices.Traits.GetDefinition(_pendingBuild.Backstory);
            _spriteBatch.DrawString(_font, "BACKSTORY:", new Vector2(60, 75), UITheme.TextHighlight);
            int backstoryCost = backstoryDef?.PointCost ?? 0;
            string backstoryCostStr = backstoryCost > 0 ? $" (-{backstoryCost})" : backstoryCost < 0 ? $" (+{-backstoryCost})" : "";
            _spriteBatch.DrawString(_font, (backstoryDef?.Name ?? _pendingBuild.Backstory.ToString()) + backstoryCostStr, new Vector2(160, 75), UITheme.TextPrimary);
            
            // Word-wrap backstory description to fit in box (max width ~460 pixels)
            string backstoryDesc = backstoryDef?.Description ?? "";
            var descLines = WrapText(backstoryDesc, 460f);
            int descY = 95;
            foreach (var line in descLines)
            {
                _spriteBatch.DrawString(_font, line, new Vector2(60, descY), UITheme.TextSecondary);
                descY += 16;
            }
            
            // Traits (adjust Y position based on description lines)
            int traitsStartY = Math.Max(145, descY + 10);
            _spriteBatch.DrawString(_font, "TRAITS:", new Vector2(60, traitsStartY), UITheme.TextHighlight);
            if (_pendingBuild.Traits.Count == 0)
            {
                _spriteBatch.DrawString(_font, "None", new Vector2(130, traitsStartY), UITheme.TextSecondary);
            }
            else
            {
                int traitY = traitsStartY;
                foreach (var trait in _pendingBuild.Traits)
                {
                    var traitDef = GameServices.Traits.GetDefinition(trait);
                    string traitText = traitDef?.Name ?? trait.ToString();
                    int cost = traitDef?.PointCost ?? 0;
                    string costStr = cost > 0 ? $" (-{cost})" : cost < 0 ? $" (+{-cost})" : "";
                    _spriteBatch.DrawString(_font, $"• {traitText}{costStr}", new Vector2(130, traitY), UITheme.TextPrimary);
                    traitY += 18;
                }
            }
            
            // Mutation Points
            _spriteBatch.DrawString(_font, $"MUTATION POINTS: {_pendingBuild.MutationPoints}", new Vector2(60, 255), UITheme.TextSuccess);
            
            // ============================================
            // STARTING ATTRIBUTES & STATS (right side)
            // ============================================
            Rectangle attrPanel = new Rectangle(560, 60, 680, 230);
            _spriteBatch.Draw(_pixelTexture, attrPanel, UITheme.PanelBackground * 0.5f);
            DrawBorder(attrPanel, UITheme.PanelBorder, 1);
            _spriteBatch.DrawString(_font, "STARTING ATTRIBUTES (Hover for info):", new Vector2(580, 75), UITheme.TextHighlight);
            
            // Get attribute values - base 10 for all
            var attributes = (AttributeType[])Enum.GetValues(typeof(AttributeType));
            int attrX = 580;
            int attrY = 100;
            int attrWidth = 200;
            int attrHeight = 25;
            
            for (int i = 0; i < attributes.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                int x = attrX + col * attrWidth;
                int y = attrY + row * (attrHeight + 5);
                
                var attr = attributes[i];
                int value = _pendingBuild.Attributes.Get(attr); // Get actual value from build
                string attrName = GetAttributeShortName(attr);
                
                // Color-code based on deviation from base (5)
                Color attrColor = UITheme.TextPrimary;
                if (value > 5) attrColor = UITheme.TextSuccess;
                else if (value < 5) attrColor = UITheme.TextDanger;
                
                // Draw attribute box
                _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, attrWidth - 10, attrHeight), UITheme.PanelBackground);
                DrawBorder(new Rectangle(x, y, attrWidth - 10, attrHeight), UITheme.PanelBorder, 1);
                _spriteBatch.DrawString(_font, $"{attrName}: {value}", new Vector2(x + 5, y + 5), attrColor);
            }
            
            // Calculate effective stats based on traits AND attributes
            var traitBonuses = GameServices.Traits.CalculateBonuses(_pendingBuild.AllTraits);
            var attrs = _pendingBuild.Attributes;
            
            // Base stats + attribute bonuses + trait modifiers
            float effectiveHP = (100f + attrs.HealthBonus) * traitBonuses.HealthModifier;
            float effectiveSpeed = 200f * (1f + attrs.SpeedBonus) * traitBonuses.SpeedModifier;
            float effectiveDamage = (10f * (1f + attrs.MeleeDamageBonus)) * traitBonuses.DamageModifier;
            float effectiveAccuracy = (0.75f + attrs.AccuracyBonus) * traitBonuses.AccuracyModifier;
            float effectiveSight = 10f + attrs.SightRangeBonus;
            
            // Show calculated stats with trait modifiers
            _spriteBatch.DrawString(_font, "Effective Stats (attributes + traits):", new Vector2(580, 165), UITheme.TextSecondary);
            
            // Color-code stats based on whether they're buffed/debuffed from base
            Color hpColor = effectiveHP > 100 ? UITheme.TextSuccess : (effectiveHP < 100 ? UITheme.TextDanger : UITheme.TextPrimary);
            Color speedColor = effectiveSpeed > 200 ? UITheme.TextSuccess : (effectiveSpeed < 200 ? UITheme.TextDanger : UITheme.TextPrimary);
            Color damageColor = effectiveDamage > 10 ? UITheme.TextSuccess : (effectiveDamage < 10 ? UITheme.TextDanger : UITheme.TextPrimary);
            Color accColor = effectiveAccuracy > 0.75f ? UITheme.TextSuccess : (effectiveAccuracy < 0.75f ? UITheme.TextDanger : UITheme.TextPrimary);
            Color sightColor = effectiveSight > 10 ? UITheme.TextSuccess : (effectiveSight < 10 ? UITheme.TextDanger : UITheme.TextPrimary);
            
            // Draw individual stats with colors
            _spriteBatch.DrawString(_font, $"HP: {effectiveHP:F0}", new Vector2(580, 185), hpColor);
            _spriteBatch.DrawString(_font, $"Speed: {effectiveSpeed:F0}", new Vector2(680, 185), speedColor);
            _spriteBatch.DrawString(_font, $"Damage: {effectiveDamage:F1}", new Vector2(800, 185), damageColor);
            _spriteBatch.DrawString(_font, $"Accuracy: {effectiveAccuracy:P0}", new Vector2(580, 205), accColor);
            _spriteBatch.DrawString(_font, $"Sight: {effectiveSight:F0} tiles", new Vector2(720, 205), sightColor);
            
            // Show special trait flags if any
            int flagY = 230;
            if (!traitBonuses.CanSpeak)
            {
                _spriteBatch.DrawString(_font, "• Cannot Speak", new Vector2(580, flagY), UITheme.TextWarning);
                flagY += 16;
            }
            if (!traitBonuses.CanDisguise)
            {
                _spriteBatch.DrawString(_font, "• Cannot Disguise", new Vector2(580, flagY), UITheme.TextWarning);
                flagY += 16;
            }
            if (traitBonuses.CanEatCorpses)
            {
                _spriteBatch.DrawString(_font, "• Can Eat Corpses", new Vector2(580, flagY), UITheme.TextDanger);
                flagY += 16;
            }
            if (traitBonuses.IsNightPerson)
            {
                _spriteBatch.DrawString(_font, "• Night Owl (+night / -day)", new Vector2(580, flagY), new Color(180, 130, 220));
            }
            
            // Reroll hint
            _spriteBatch.DrawString(_font, "[R] Reroll Character", new Vector2(60, 295), UITheme.TextSecondary);
            
            // Science Path Selection
            _spriteBatch.DrawString(_font, "CHOOSE YOUR SCIENCE PATH:", new Vector2(640 - _font.MeasureString("CHOOSE YOUR SCIENCE PATH:").X / 2, 320), UITheme.TextHighlight);
            
            var mState = Mouse.GetState();
            
            // Tinker Box
            Rectangle tinkerRect = new Rectangle(340, 355, 280, 110);
            bool tinkerHovered = tinkerRect.Contains(mState.X, mState.Y);
            Color tinkerBg = _selectedSciencePathIndex == 0 ? UITheme.SelectionBackground : 
                            (tinkerHovered ? UITheme.HoverBackground : UITheme.PanelBackground * 0.5f);
            _spriteBatch.Draw(_pixelTexture, tinkerRect, tinkerBg);
            DrawBorder(tinkerRect, _selectedSciencePathIndex == 0 ? UITheme.SelectionBorder : UITheme.PanelBorder, _selectedSciencePathIndex == 0 ? 2 : 1);
            _spriteBatch.DrawString(_font, "TINKER SCIENCE", new Vector2(400, 375), _selectedSciencePathIndex == 0 ? UITheme.TextHighlight : UITheme.TextPrimary);
            _spriteBatch.DrawString(_font, "Technology, implants,", new Vector2(360, 400), UITheme.TextSecondary);
            _spriteBatch.DrawString(_font, "guns, electronics", new Vector2(360, 420), UITheme.TextSecondary);
            
            // Dark Box
            Rectangle darkRect = new Rectangle(660, 355, 280, 110);
            bool darkHovered = darkRect.Contains(mState.X, mState.Y);
            Color darkBg = _selectedSciencePathIndex == 1 ? UITheme.SelectionBackground : 
                          (darkHovered ? UITheme.HoverBackground : UITheme.PanelBackground * 0.5f);
            _spriteBatch.Draw(_pixelTexture, darkRect, darkBg);
            DrawBorder(darkRect, _selectedSciencePathIndex == 1 ? UITheme.SelectionBorder : UITheme.PanelBorder, _selectedSciencePathIndex == 1 ? 2 : 1);
            _spriteBatch.DrawString(_font, "DARK SCIENCE", new Vector2(725, 375), _selectedSciencePathIndex == 1 ? UITheme.TextHighlight : UITheme.TextPrimary);
            _spriteBatch.DrawString(_font, "Rituals, anomalies,", new Vector2(680, 400), UITheme.TextSecondary);
            _spriteBatch.DrawString(_font, "transmutation, void", new Vector2(680, 420), UITheme.TextSecondary);
            
            // Start button
            Rectangle startBtn = new Rectangle(540, 490, 200, 40);
            DrawButton(startBtn, "START GAME [Enter]", mState);
            
            // Help bar
            DrawHelpBar("[R] Reroll  |  [Left/Right] Select Path  |  [Enter/Click] Start Game  |  Hover for tooltips");
            
            // Draw tooltip (on top of everything)
            if (_showTooltip)
            {
                DrawTooltip();
            }
            
            _spriteBatch.End();
        }
        
        private string GetAttributeShortName(AttributeType attr)
        {
            return attr switch
            {
                AttributeType.STR => "STR",
                AttributeType.AGI => "AGI",
                AttributeType.END => "END",
                AttributeType.INT => "INT",
                AttributeType.PER => "PER",
                AttributeType.WIL => "WIL",
                _ => attr.ToString()
            };
        }
        
        private Color GetAttributeColor(AttributeType attr)
        {
            return attr switch
            {
                AttributeType.STR => Color.OrangeRed,
                AttributeType.AGI => Color.LightGreen,
                AttributeType.END => Color.Yellow,
                AttributeType.INT => Color.Cyan,
                AttributeType.PER => Color.MediumPurple,
                AttributeType.WIL => Color.Pink,
                _ => Color.White
            };
        }
        
        private string GetAttributeDescription(AttributeType attr)
        {
            return attr switch
            {
                AttributeType.STR => "STRENGTH (STR)\n\n" +
                    "Physical power and raw strength.\n\n" +
                    "Effects:\n" +
                    "• Melee damage bonus (+5% per point above 10)\n" +
                    "• Carry capacity\n" +
                    "• Intimidation effectiveness\n" +
                    "• Breaking/forcing objects",
                    
                AttributeType.AGI => "AGILITY (AGI)\n\n" +
                    "Speed, reflexes, and coordination.\n\n" +
                    "Effects:\n" +
                    "• Movement speed (+2% per point above 10)\n" +
                    "• Dodge chance\n" +
                    "• Action points in combat\n" +
                    "• Stealth effectiveness",
                    
                AttributeType.END => "ENDURANCE (END)\n\n" +
                    "Toughness, stamina, and vitality.\n\n" +
                    "Effects:\n" +
                    "• Max health (+10% per point above 10)\n" +
                    "• Resistance to status effects\n" +
                    "• Stamina/fatigue rate\n" +
                    "• Survival in harsh conditions",
                    
                AttributeType.INT => "INTELLIGENCE (INT)\n\n" +
                    "Learning, reasoning, and memory.\n\n" +
                    "Effects:\n" +
                    "• Research speed (+5% per point above 10)\n" +
                    "• XP gain bonus\n" +
                    "• Crafting quality\n" +
                    "• Tech mutation effectiveness",
                    
                AttributeType.PER => "PERCEPTION (PER)\n\n" +
                    "Awareness, senses, and intuition.\n\n" +
                    "Effects:\n" +
                    "• Sight range (+0.5 tiles per point above 10)\n" +
                    "• Accuracy bonus (+2% per point above 10)\n" +
                    "• Trap/ambush detection\n" +
                    "• Loot discovery chance",
                    
                AttributeType.WIL => "WILLPOWER (WIL)\n\n" +
                    "Mental fortitude and inner strength.\n\n" +
                    "Effects:\n" +
                    "• Mental resistance\n" +
                    "• Dark Science effectiveness\n" +
                    "• Psionic mutation strength\n" +
                    "• Fear/panic resistance",
                    
                _ => "Unknown attribute."
            };
        }
        
        /// <summary>
        /// Draw a tooltip box with title and text
        /// </summary>
        private void DrawTooltip()
        {
            if (string.IsNullOrEmpty(_tooltipText)) return;
            
            // Calculate tooltip size - handle both line wrapping and existing newlines
            float maxWidth = 350f;
            
            // Split by existing newlines first, then wrap each paragraph
            var allLines = new List<string>();
            string[] paragraphs = _tooltipText.Split('\n');
            foreach (var para in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(para))
                {
                    allLines.Add(""); // Preserve empty lines
                }
                else
                {
                    allLines.AddRange(WrapText(para.Trim(), maxWidth));
                }
            }
            
            float titleHeight = _font.MeasureString(_tooltipTitle).Y;
            float textHeight = allLines.Count * _font.LineSpacing;
            float totalHeight = titleHeight + textHeight + 30; // padding
            
            // Find the widest line
            float maxLineWidth = _font.MeasureString(_tooltipTitle).X;
            foreach (var line in allLines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    float lineWidth = _font.MeasureString(line).X;
                    if (lineWidth > maxLineWidth) maxLineWidth = lineWidth;
                }
            }
            float boxWidth = Math.Min(maxLineWidth + 20, maxWidth + 20);
            
            // Adjust position to stay on screen
            Vector2 pos = _tooltipPosition;
            if (pos.X + boxWidth > 1280) pos.X = 1280 - boxWidth - 10;
            if (pos.Y + totalHeight > 720) pos.Y = 720 - totalHeight - 10;
            if (pos.X < 10) pos.X = 10;
            if (pos.Y < 10) pos.Y = 10;
            
            // Draw background
            Rectangle bgRect = new Rectangle((int)pos.X, (int)pos.Y, (int)boxWidth, (int)totalHeight);
            _spriteBatch.Draw(_pixelTexture, bgRect, Color.Black * 0.95f);
            
            // Draw border
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)pos.X, (int)pos.Y, (int)boxWidth, 2), Color.Gold);
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)pos.X, (int)pos.Y + (int)totalHeight - 2, (int)boxWidth, 2), Color.Gold);
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)pos.X, (int)pos.Y, 2, (int)totalHeight), Color.Gold);
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)pos.X + (int)boxWidth - 2, (int)pos.Y, 2, (int)totalHeight), Color.Gold);
            
            // Draw title
            _spriteBatch.DrawString(_font, _tooltipTitle, pos + new Vector2(10, 8), Color.Gold);
            
            // Draw separator line
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)pos.X + 5, (int)pos.Y + (int)titleHeight + 12, (int)boxWidth - 10, 1), Color.Gray);
            
            // Draw text lines
            float textY = pos.Y + titleHeight + 18;
            foreach (var line in allLines)
            {
                _spriteBatch.DrawString(_font, line, new Vector2(pos.X + 10, textY), Color.White);
                textY += _font.LineSpacing;
            }
        }
        
        // ============================================
        // DRAW: ATTRIBUTE SELECT
        // ============================================
        
        private void DrawAttributeSelect()
        {
            _spriteBatch.Begin();
            
            // Darken background
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.85f);
            
            // Title
            string title = "LEVEL UP! CHOOSE ATTRIBUTE";
            Vector2 titleSize = _font.MeasureString(title);
            _spriteBatch.DrawString(_font, title, new Vector2(640 - titleSize.X / 2, 50), Color.Yellow);
            
            // Current attributes display
            _spriteBatch.DrawString(_font, $"Current: {_player.Stats.Attributes.GetDisplayString()}", 
                new Vector2(640 - _font.MeasureString($"Current: {_player.Stats.Attributes.GetDisplayString()}").X / 2, 90), Color.LightGray);
            
            // Level info
            _spriteBatch.DrawString(_font, $"Level {_player.Stats.Level} | Pending Attribute Points: {_player.Stats.PendingAttributePoints}",
                new Vector2(640 - _font.MeasureString($"Level {_player.Stats.Level} | Pending Attribute Points: {_player.Stats.PendingAttributePoints}").X / 2, 120), Color.Cyan);
            
            // Draw attribute choices
            var attributes = (AttributeType[])Enum.GetValues(typeof(AttributeType));
            int startY = 180;
            int boxHeight = 60;
            int boxWidth = 600;
            int startX = 340;
            
            for (int i = 0; i < attributes.Length; i++)
            {
                var attr = attributes[i];
                int y = startY + i * (boxHeight + 10);
                int currentValue = _player.Stats.Attributes.Get(attr);
                
                // Box background
                Color boxColor = (i == _selectedAttributeIndex) ? Color.DarkGreen : Color.DarkGray * 0.7f;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(startX, y, boxWidth, boxHeight), boxColor);
                
                // Selection indicator
                if (i == _selectedAttributeIndex)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(startX - 5, y, 5, boxHeight), Color.LimeGreen);
                }
                
                // Attribute info
                string numKey = $"[{i + 1}] ";
                string attrName = Attributes.GetAttributeName(attr);
                string attrValue = $"{currentValue} -> {currentValue + 1}";
                string attrDesc = Attributes.GetAttributeDescription(attr);
                
                _spriteBatch.DrawString(_font, $"{numKey}{attrName}: {attrValue}", new Vector2(startX + 10, y + 10), Color.White);
                _spriteBatch.DrawString(_font, attrDesc, new Vector2(startX + 10, y + 35), Color.LightGray);
            }
            
            // Hint
            _spriteBatch.DrawString(_font, "After choosing, you'll select a mutation!", 
                new Vector2(640 - _font.MeasureString("After choosing, you'll select a mutation!").X / 2, 620), Color.Magenta);
            
            // Controls
            _spriteBatch.DrawString(_font, "Click or Press [Enter] to Confirm | [1-6] Quick Select", 
                new Vector2(640 - _font.MeasureString("Click or Press [Enter] to Confirm | [1-6] Quick Select").X / 2, 680), Color.Gray);
            
            _spriteBatch.End();
        }
        
        private void DrawPlaying()
        {
            // --- LAYER 1: WORLD ---
            _spriteBatch.Begin(transformMatrix: _camera.GetViewMatrix(), samplerState: SamplerState.PointClamp);

            _world.Draw(_spriteBatch);
            
            // Draw combat zone indicator (BG3 style)
            if (_combat.InCombat)
            {
                DrawCombatZone();
            }
            
            // Draw Structures
            DrawStructures();
            
            // Draw Ground Items (dropped loot)
            DrawGroundItems();

            // Draw Enemies
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsAlive) continue;
                
                int offset = (_world.TileSize - 32) / 2;
                Rectangle enemyRect = new Rectangle(
                    (int)enemy.Position.X + offset,
                    (int)enemy.Position.Y + offset,
                    32, 32
                );
                
                Color enemyColor = enemy.Type switch
                {
                    // Hostile creatures - warm/aggressive colors
                    EnemyType.Raider => Color.Red,
                    EnemyType.MutantBeast => Color.Orange,
                    EnemyType.Hunter => Color.Purple,
                    EnemyType.Abomination => Color.DarkRed,
                    
                    // Special ability enemies - use their TintColor
                    EnemyType.Spitter => enemy.TintColor,
                    EnemyType.Psionic => enemy.TintColor,
                    EnemyType.Brute => enemy.TintColor,
                    EnemyType.Stalker => enemy.TintColor,
                    EnemyType.HiveMother => enemy.TintColor,
                    EnemyType.Swarmling => enemy.TintColor,
                    
                    // Passive creatures - cool/neutral colors
                    EnemyType.Scavenger => Color.SaddleBrown,
                    EnemyType.GiantInsect => Color.Olive,
                    EnemyType.WildBoar => Color.Sienna,
                    EnemyType.MutantDeer => Color.Tan,
                    EnemyType.CaveSlug => Color.SlateGray,
                    
                    _ => Color.Red
                };
                
                // Stealthed enemies are semi-transparent (unless player has high PER)
                if (enemy.IsStealthed)
                {
                    float playerPER = _player.Stats.Attributes.PER;
                    if (playerPER >= 8)
                    {
                        // Player can see stealthed enemies but they're still faint
                        enemyColor = enemyColor * 0.5f;
                    }
                    else
                    {
                        // Player can't see - skip drawing (or very faint shimmer)
                        enemyColor = enemyColor * 0.15f;
                    }
                }
                
                // Tint provoked passive creatures
                if (enemy.IsProvoked && enemy.Behavior != CreatureBehavior.Aggressive)
                {
                    enemyColor = Color.Lerp(enemyColor, Color.Red, 0.5f);
                }
                
                // During combat: dim enemies not in combat zone
                if (_combat.InCombat && !enemy.InCombatZone)
                {
                    enemyColor = Color.Lerp(enemyColor, Color.Gray, 0.5f);
                }
                
                // Highlight selected
                if (enemy == _selectedEnemy)
                {
                    Rectangle selectRect = new Rectangle(
                        (int)enemy.Position.X + offset - 4,
                        (int)enemy.Position.Y + offset - 4,
                        40, 40
                    );
                    _spriteBatch.Draw(_pixelTexture, selectRect, Color.Yellow);
                }
                
                // Draw combat zone border for enemies in combat
                if (_combat.InCombat && enemy.InCombatZone)
                {
                    Rectangle combatBorder = new Rectangle(
                        (int)enemy.Position.X + offset - 2,
                        (int)enemy.Position.Y + offset - 2,
                        36, 36
                    );
                    _spriteBatch.Draw(_pixelTexture, combatBorder, Color.OrangeRed * 0.6f);
                }
                
                // Hit flash effect - white flash when taking damage
                if (enemy.IsFlashing)
                {
                    float flashIntensity = enemy.HitFlashTimer / 0.15f;  // 0 to 1
                    enemyColor = Color.Lerp(enemyColor, Color.White, flashIntensity * 0.8f);
                }
                
                _spriteBatch.Draw(_pixelTexture, enemyRect, enemyColor);
                
                // Enemy health bar
                float healthPercent = enemy.CurrentHealth / enemy.MaxHealth;
                Rectangle bgRect = new Rectangle((int)enemy.Position.X + offset, (int)enemy.Position.Y + offset - 8, 32, 4);
                Rectangle healthRect = new Rectangle((int)enemy.Position.X + offset, (int)enemy.Position.Y + offset - 8, (int)(32 * healthPercent), 4);
                
                _spriteBatch.Draw(_pixelTexture, bgRect, Color.DarkGray);
                
                // Health bar color - green for passive, red for hostile/provoked
                Color healthBarColor = (enemy.Behavior == CreatureBehavior.Aggressive || enemy.IsProvoked) 
                    ? Color.Red : Color.ForestGreen;
                _spriteBatch.Draw(_pixelTexture, healthRect, healthBarColor);
                
                if (_combat.InCombat)
                {
                    Vector2 namePos = new Vector2(enemy.Position.X + offset, enemy.Position.Y - 20);
                    _spriteBatch.DrawString(_font, enemy.Name, namePos + new Vector2(1, 1), Color.Black);
                    _spriteBatch.DrawString(_font, enemy.Name, namePos, Color.White);
                }
            }

            // Draw NPCs
            foreach (var npc in _npcs)
            {
                int offset = (_world.TileSize - 32) / 2;
                Rectangle npcRect = new Rectangle(
                    (int)npc.Position.X + offset,
                    (int)npc.Position.Y + offset,
                    32, 32
                );
                
                // Highlight if nearby (can interact)
                if (npc == _nearestNPC)
                {
                    Rectangle highlightRect = new Rectangle(
                        (int)npc.Position.X + offset - 4,
                        (int)npc.Position.Y + offset - 4,
                        40, 40
                    );
                    _spriteBatch.Draw(_pixelTexture, highlightRect, Color.Yellow * 0.5f);
                }
                
                // NPC body
                _spriteBatch.Draw(_pixelTexture, npcRect, npc.DisplayColor);
                
                // Name above NPC
                Vector2 nameSize = _font.MeasureString(npc.Name);
                Vector2 namePos = new Vector2(
                    npc.Position.X + offset + 16 - nameSize.X / 2,
                    npc.Position.Y - 5
                );
                _spriteBatch.DrawString(_font, npc.Name, namePos + new Vector2(1, 1), Color.Black);
                _spriteBatch.DrawString(_font, npc.Name, namePos, npc.DisplayColor);
            }

            // Draw Player
            Color playerColor = Color.Green;
            if (_player.HasStatus(StatusEffectType.Wet)) playerColor = Color.Cyan;
            if (_player.HasStatus(StatusEffectType.Stunned)) playerColor = Color.Yellow;
            if (_player.HasStatus(StatusEffectType.Burning)) playerColor = Color.OrangeRed;
            if (!_player.IsAlive) playerColor = Color.DarkRed;
            
            // Hit flash effect - white flash when taking damage
            if (_player.IsFlashing)
            {
                float flashIntensity = _player.HitFlashTimer / 0.15f;  // 0 to 1
                playerColor = Color.Lerp(playerColor, Color.White, flashIntensity * 0.8f);
            }

            int pOffset = (_world.TileSize - 32) / 2;
            Rectangle playerRect = new Rectangle((int)_player.Position.X + pOffset, (int)_player.Position.Y + pOffset, 32, 32);
            _spriteBatch.Draw(_pixelTexture, playerRect, playerColor);

            // Player status effects
            if (_player.Stats.StatusEffects.Count > 0)
            {
                var statusNames = new List<string>();
                foreach (var effect in _player.Stats.StatusEffects)
                {
                    statusNames.Add(effect.Type.ToString());
                }
                string statusText = string.Join(" + ", statusNames);
                Vector2 textSize = _font.MeasureString(statusText);
                Vector2 textPos = new Vector2(
                    _player.Position.X + pOffset - (textSize.X / 2) + 16,
                    _player.Position.Y - 20
                );
                _spriteBatch.DrawString(_font, statusText, textPos + new Vector2(2, 2), Color.Black);
                _spriteBatch.DrawString(_font, statusText, textPos, Color.White);
            }

            _spriteBatch.End();

            // --- LAYER 2: UI ---
            _spriteBatch.Begin();

            // Health Bar
            _spriteBatch.Draw(_pixelTexture, new Rectangle(10, 680, 200, 30), Color.DarkGray);
            float healthPercent2 = _player.Stats.CurrentHealth / _player.Stats.MaxHealth;
            Color healthColor = healthPercent2 > 0.5f ? Color.Green : (healthPercent2 > 0.25f ? Color.Yellow : Color.Red);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(12, 682, (int)(196 * healthPercent2), 26), healthColor);
            _spriteBatch.DrawString(_font, $"HP: {_player.Stats.CurrentHealth:F0}/{_player.Stats.MaxHealth:F0}", new Vector2(20, 685), Color.White);
            
            // Level and XP
            _spriteBatch.DrawString(_font, $"Lv.{_player.Stats.Level} | XP: {_player.Stats.CurrentXP:F0}/{_player.Stats.XPToNextLevel:F0}", new Vector2(220, 685), Color.Yellow);
            
            // Mutation Points (highlight if available!)
            int totalPoints = _player.Stats.MutationPoints + _player.Stats.FreeMutationPicks;
            if (totalPoints > 0)
            {
                string mutText = $"[M] Mutations: {_player.Stats.MutationPoints}";
                if (_player.Stats.FreeMutationPicks > 0)
                    mutText += $" (+{_player.Stats.FreeMutationPicks} FREE)";
                
                // Pulsing effect
                float pulse = (float)(Math.Sin(_totalTime * 4) * 0.3 + 0.7);
                _spriteBatch.DrawString(_font, mutText, new Vector2(450, 685), Color.Magenta * pulse);
            }

            // Combat UI
            if (_combat.InCombat)
            {
                DrawCombatUI();
            }
            else
            {
                // Build mode UI
                if (GameServices.Building.InBuildMode)
                {
                    DrawBuildModeUI();
                }
                else if (_buildMenuOpen)
                {
                    DrawBuildMenuUI();
                }
                else
                {
                    _spriteBatch.DrawString(_font, "Click: Move | Right-Click: Inspect | I: Inventory | J: Quests | R: Research | B: Build", new Vector2(10, 10), Color.Yellow);
                    _spriteBatch.DrawString(_font, "F1-F9: Debug | F10: Save | F11: Load | H: Survival | T: Time Skip", new Vector2(10, 30), Color.LightGray);
                }
            }
            
            // Backstory and Path
            string traitInfo = $"{_player.Stats.Traits[0]} | {_player.Stats.SciencePath}";
            _spriteBatch.DrawString(_font, traitInfo, new Vector2(10, 640), Color.LightBlue);
            
            // Attributes (compact display)
            string attrInfo = _player.Stats.Attributes.GetDisplayString();
            _spriteBatch.DrawString(_font, attrInfo, new Vector2(10, 660), Color.LightGreen);
            
            // --- ZONE INFO (top center) ---
            if (!_combat.InCombat && _zoneManager.CurrentZone != null)
            {
                var zone = _zoneManager.CurrentZone;
                string zoneName = $"[ {zone.Name} ]";
                Vector2 zoneNameSize = _font.MeasureString(zoneName);
                Vector2 zonePos = new Vector2(640 - zoneNameSize.X / 2, 50);
                _spriteBatch.DrawString(_font, zoneName, zonePos + new Vector2(1, 1), Color.Black);
                _spriteBatch.DrawString(_font, zoneName, zonePos, Color.White);
                
                // Show danger level
                string dangerStr = zone.DangerLevel switch
                {
                    <= 0.5f => "Safe",
                    <= 1.0f => "Normal",
                    <= 1.5f => "Dangerous",
                    <= 2.0f => "Very Dangerous",
                    _ => "Deadly"
                };
                Color dangerColor = zone.DangerLevel switch
                {
                    <= 0.5f => Color.LimeGreen,
                    <= 1.0f => Color.Yellow,
                    <= 1.5f => Color.Orange,
                    <= 2.0f => Color.OrangeRed,
                    _ => Color.Red
                };
                _spriteBatch.DrawString(_font, dangerStr, new Vector2(640 - _font.MeasureString(dangerStr).X / 2, 70), dangerColor);
            }
            
            // --- ZONE EXIT HINTS (edges of screen) ---
            if (!_combat.InCombat && _zoneManager.CurrentZone != null)
            {
                DrawZoneExitHints();
            }
            
            // --- SURVIVAL UI (top right) ---
            DrawSurvivalUI();
            
            // --- PICKUP HINT ---
            if (_nearestItem != null && !_combat.InCombat)
            {
                string pickupText = $"[G] Pick up: {_nearestItem.GetDisplayText()}";
                Vector2 hintPos = new Vector2(640 - _font.MeasureString(pickupText).X / 2, 680);
                _spriteBatch.Draw(_pixelTexture, new Rectangle((int)hintPos.X - 5, (int)hintPos.Y - 2, (int)_font.MeasureString(pickupText).X + 10, 20), Color.Black * 0.7f);
                _spriteBatch.DrawString(_font, pickupText, hintPos, Color.Yellow);
            }
            
            // --- NPC TRADE HINT ---
            if (_nearestNPC != null && !_combat.InCombat && _nearestItem == null)
            {
                string tradeText = $"[T] Talk to: {_nearestNPC.Name}";
                Vector2 hintPos = new Vector2(640 - _font.MeasureString(tradeText).X / 2, 680);
                _spriteBatch.Draw(_pixelTexture, new Rectangle((int)hintPos.X - 5, (int)hintPos.Y - 2, (int)_font.MeasureString(tradeText).X + 10, 20), Color.Black * 0.7f);
                _spriteBatch.DrawString(_font, tradeText, hintPos, Color.Cyan);
            }
            
            // --- GOLD DISPLAY ---
            if (!_combat.InCombat)
            {
                _spriteBatch.DrawString(_font, $"Gold: {_player.Stats.Gold}", new Vector2(10, 700), Color.Gold);
            }
            
            // --- GROUND ITEMS COUNT ---
            if (_groundItems.Count > 0 && !_combat.InCombat)
            {
                _spriteBatch.DrawString(_font, $"Items nearby: {_groundItems.Count}", new Vector2(1100, 680), Color.Gold);
            }
            
            // --- INSPECT PANEL (right-click menu) ---
            DrawInspectPanel();

            _spriteBatch.End();
            
            // --- INVENTORY UI (fullscreen overlay - separate batch) ---
            if (_inventoryOpen)
            {
                DrawInventoryUI();
            }
            
            // --- CRAFTING UI (fullscreen overlay - separate batch) ---
            if (_craftingOpen)
            {
                DrawCraftingUI();
            }
            
            // --- TRADING UI (fullscreen overlay - separate batch) ---
            if (_tradingOpen)
            {
                DrawTradingUI();
            }
            
            // --- QUEST DIALOGUE UI (fullscreen overlay) ---
            if (_questDialogueOpen)
            {
                DrawQuestDialogueUI();
            }
            
            // --- QUEST LOG UI (fullscreen overlay) ---
            if (_questLogOpen)
            {
                DrawQuestLogUI();
            }
            
            // --- RESEARCH UI (fullscreen overlay) ---
            if (_researchOpen)
            {
                DrawResearchUI();
            }
            
            // --- BODY PANEL UI (fullscreen overlay) ---
            if (_bodyPanelOpen)
            {
                DrawBodyPanelUI();
            }
            
            // --- NOTIFICATION (drawn last, on top of everything) ---
            if (_notificationTimer > 0 && !string.IsNullOrEmpty(_notificationText))
            {
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                
                float alpha = Math.Min(1f, _notificationTimer);  // Fade out
                Vector2 textSize = _font.MeasureString(_notificationText);
                Vector2 pos = new Vector2(640 - textSize.X / 2, 600);  // Bottom area, above hints
                
                // Background
                _spriteBatch.Draw(_pixelTexture, new Rectangle((int)pos.X - 10, (int)pos.Y - 5, (int)textSize.X + 20, (int)textSize.Y + 10), Color.Black * (alpha * 0.9f));
                
                // Border
                _spriteBatch.Draw(_pixelTexture, new Rectangle((int)pos.X - 12, (int)pos.Y - 7, (int)textSize.X + 24, 2), Color.Yellow * alpha);
                _spriteBatch.Draw(_pixelTexture, new Rectangle((int)pos.X - 12, (int)pos.Y + (int)textSize.Y + 5, (int)textSize.X + 24, 2), Color.Yellow * alpha);
                
                // Text
                _spriteBatch.DrawString(_font, _notificationText, pos, Color.Yellow * alpha);
                
                _spriteBatch.End();
            }
        }
        
        private void DrawZoneExitHints()
        {
            var zone = _zoneManager.CurrentZone;
            if (zone == null) return;
            
            // Get player tile position to determine if near edge
            Point playerTile = new Point(
                (int)(_player.Position.X / _world.TileSize),
                (int)(_player.Position.Y / _world.TileSize)
            );
            
            int edgeThreshold = 5;  // Show hint when within 5 tiles of edge
            
            // North exit
            if (zone.Exits.ContainsKey(ZoneExitDirection.North) && playerTile.Y < edgeThreshold)
            {
                var target = _zoneManager.GetZone(zone.Exits[ZoneExitDirection.North].TargetZoneId);
                if (target != null)
                {
                    string text = $"↑ {target.Name}";
                    Vector2 pos = new Vector2(640 - _font.MeasureString(text).X / 2, 95);
                    _spriteBatch.DrawString(_font, text, pos, Color.Cyan);
                }
            }
            
            // South exit
            if (zone.Exits.ContainsKey(ZoneExitDirection.South) && playerTile.Y > _world.Height - edgeThreshold)
            {
                var target = _zoneManager.GetZone(zone.Exits[ZoneExitDirection.South].TargetZoneId);
                if (target != null)
                {
                    string text = $"↓ {target.Name}";
                    Vector2 pos = new Vector2(640 - _font.MeasureString(text).X / 2, 620);
                    _spriteBatch.DrawString(_font, text, pos, Color.Cyan);
                }
            }
            
            // West exit
            if (zone.Exits.ContainsKey(ZoneExitDirection.West) && playerTile.X < edgeThreshold)
            {
                var target = _zoneManager.GetZone(zone.Exits[ZoneExitDirection.West].TargetZoneId);
                if (target != null)
                {
                    string text = $"← {target.Name}";
                    Vector2 pos = new Vector2(10, 360);
                    _spriteBatch.DrawString(_font, text, pos, Color.Cyan);
                }
            }
            
            // East exit
            if (zone.Exits.ContainsKey(ZoneExitDirection.East) && playerTile.X > _world.Width - edgeThreshold)
            {
                var target = _zoneManager.GetZone(zone.Exits[ZoneExitDirection.East].TargetZoneId);
                if (target != null)
                {
                    string text = $"{target.Name} →";
                    Vector2 pos = new Vector2(1280 - _font.MeasureString(text).X - 10, 360);
                    _spriteBatch.DrawString(_font, text, pos, Color.Cyan);
                }
            }
        }
        
        private void DrawSurvivalUI()
        {
            int rightX = 1080;
            int topY = 10;
            int barWidth = 180;
            int barHeight = 12;
            int gap = 4;
            
            // Skip if in combat (combat UI takes this space)
            if (_combat.InCombat) return;
            
            // Background panel
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rightX - 10, topY - 5, 210, 140), Color.Black * 0.7f);
            
            // Time display
            string timeStr = $"{GameServices.SurvivalSystem.GetTimeString()} - {GameServices.SurvivalSystem.CurrentTimeOfDay}";
            string dateStr = GameServices.SurvivalSystem.GetDateString();
            _spriteBatch.DrawString(_font, timeStr, new Vector2(rightX, topY), Color.White);
            _spriteBatch.DrawString(_font, dateStr, new Vector2(rightX, topY + 16), Color.LightGray);
            
            // Temperature
            float temp = GameServices.SurvivalSystem.AmbientTemperature;
            Color tempColor = _player.Stats.Survival.TempStatus switch
            {
                TemperatureStatus.Freezing => Color.Cyan,
                TemperatureStatus.Cold => Color.LightBlue,
                TemperatureStatus.Hot => Color.Orange,
                TemperatureStatus.Overheating => Color.Red,
                _ => Color.White
            };
            _spriteBatch.DrawString(_font, $"Temp: {temp:F0}°", new Vector2(rightX + 100, topY + 16), tempColor);
            
            topY += 40;
            
            // Hunger bar
            float hunger = _player.Stats.Survival.Hunger;
            Color hungerColor = hunger > 50 ? Color.SaddleBrown : (hunger > 25 ? Color.Orange : Color.Red);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rightX, topY, barWidth, barHeight), Color.DarkGray);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rightX + 1, topY + 1, (int)((barWidth - 2) * hunger / 100f), barHeight - 2), hungerColor);
            _spriteBatch.DrawString(_font, $"Food: {hunger:F0}", new Vector2(rightX + 2, topY - 1), Color.White);
            
            topY += barHeight + gap;
            
            // Thirst bar
            float thirst = _player.Stats.Survival.Thirst;
            Color thirstColor = thirst > 50 ? Color.DodgerBlue : (thirst > 25 ? Color.Orange : Color.Red);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rightX, topY, barWidth, barHeight), Color.DarkGray);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rightX + 1, topY + 1, (int)((barWidth - 2) * thirst / 100f), barHeight - 2), thirstColor);
            _spriteBatch.DrawString(_font, $"Water: {thirst:F0}", new Vector2(rightX + 2, topY - 1), Color.White);
            
            topY += barHeight + gap;
            
            // Rest bar  
            float rest = _player.Stats.Survival.Rest;
            Color restColor = rest > 50 ? Color.MediumPurple : (rest > 25 ? Color.Orange : Color.Red);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rightX, topY, barWidth, barHeight), Color.DarkGray);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rightX + 1, topY + 1, (int)((barWidth - 2) * rest / 100f), barHeight - 2), restColor);
            _spriteBatch.DrawString(_font, $"Rest: {rest:F0}", new Vector2(rightX + 2, topY - 1), Color.White);
            
            topY += barHeight + gap + 4;
            
            // Survival debuffs
            var debuffs = _player.Stats.Survival.GetActiveDebuffs();
            if (debuffs.Count > 0)
            {
                string debuffStr = string.Join(", ", debuffs);
                _spriteBatch.DrawString(_font, debuffStr, new Vector2(rightX, topY), Color.Red);
            }
        }
        
        // ============================================
        // GROUND ITEMS DRAWING
        // ============================================
        
        private void DrawGroundItems()
        {
            float bobSpeed = 2f;
            float bobAmount = 3f;
            
            foreach (var worldItem in _groundItems)
            {
                // Calculate bob offset for animation
                float bob = (float)Math.Sin(_totalTime * bobSpeed + worldItem.Position.X * 0.1f) * bobAmount;
                
                // Item size and position
                int size = 16;
                int x = (int)worldItem.Position.X + (_world.TileSize - size) / 2;
                int y = (int)worldItem.Position.Y + (_world.TileSize - size) / 2 + (int)bob;
                
                // Item color based on category
                Color itemColor = worldItem.Item.Category switch
                {
                    ItemCategory.Weapon => Color.Silver,
                    ItemCategory.Armor => Color.SteelBlue,
                    ItemCategory.Consumable => Color.LimeGreen,
                    ItemCategory.Material => Color.SandyBrown,
                    ItemCategory.Ammo => Color.Gold,
                    _ => Color.White
                };
                
                // Highlight nearest item
                if (worldItem == _nearestItem)
                {
                    // Glow effect
                    Rectangle glowRect = new Rectangle(x - 4, y - 4, size + 8, size + 8);
                    _spriteBatch.Draw(_pixelTexture, glowRect, Color.Yellow * 0.5f);
                }
                
                // Draw item
                Rectangle itemRect = new Rectangle(x, y, size, size);
                _spriteBatch.Draw(_pixelTexture, itemRect, itemColor);
                
                // Draw small border
                _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, size, 1), Color.Black * 0.5f);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, 1, size), Color.Black * 0.5f);
            }
        }
        
        // ============================================
        // INVENTORY UI DRAWING
        // ============================================
        
        private void DrawInventoryUI()
        {
            var mState = Mouse.GetState();
            
            _spriteBatch.Begin();
            
            // Dark overlay
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.9f);
            
            // Main panel
            Rectangle mainPanel = new Rectangle(30, 40, 1220, 640);
            DrawPanel(mainPanel, "INVENTORY");
            
            // Inventory list (left side - expanded)
            DrawInventoryListSimple(mState);
            
            // Item details (right side)
            DrawItemDetailsSimple(mState);
            
            // Help bar
            DrawHelpBar("[I/Esc] Close  |  [W/S/Click] Navigate  |  [P] Gear Window  |  [X] Drop");
            
            _spriteBatch.End();
        }
        
        private void DrawInventoryListSimple(MouseState mState)
        {
            int startX = 50;
            int startY = 90;
            int itemWidth = 580;
            int itemHeight = 30;
            int maxVisible = 18;
            
            var items = _player.Stats.Inventory.GetAllItems();
            
            // Panel background
            Rectangle listPanel = new Rectangle(40, 75, 600, 560);
            _spriteBatch.Draw(_pixelTexture, listPanel, UITheme.PanelBackground * 0.5f);
            DrawBorder(listPanel, UITheme.PanelBorder, 1);
            
            // Header
            _spriteBatch.DrawString(_font, $"ITEMS ({items.Count}/{_player.Stats.Inventory.MaxSlots})", new Vector2(startX, startY - 15), UITheme.TextHighlight);
            _spriteBatch.DrawString(_font, $"Weight: {_player.Stats.Inventory.CurrentWeight:F1}/{_player.Stats.Inventory.MaxWeight:F0} kg", 
                new Vector2(startX + 350, startY - 15), UITheme.TextSecondary);
            
            // Item list
            int y = startY + 5;
            for (int i = 0; i < items.Count && i < maxVisible; i++)
            {
                var item = items[i];
                bool isSelected = (i == _selectedInventoryIndex);
                Rectangle itemRect = new Rectangle(startX, y, itemWidth, itemHeight);
                bool isHovered = itemRect.Contains(mState.X, mState.Y);
                
                // Background
                Color bgColor = isSelected ? UITheme.SelectionBackground :
                               isHovered ? UITheme.HoverBackground : Color.Transparent;
                _spriteBatch.Draw(_pixelTexture, itemRect, bgColor);
                
                // Selection indicator
                if (isSelected)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(startX, y, 3, itemHeight), UITheme.SelectionBorder);
                }
                
                // Item icon (colored square based on category)
                Color iconColor = item.Category switch
                {
                    ItemCategory.Weapon => UITheme.CategoryWeapon,
                    ItemCategory.Armor => UITheme.CategoryArmor,
                    ItemCategory.Consumable => UITheme.CategoryConsumable,
                    ItemCategory.Material => UITheme.CategoryMaterial,
                    ItemCategory.Ammo => UITheme.CategoryAmmo,
                    ItemCategory.Tool => new Color(200, 150, 80),
                    _ => UITheme.TextPrimary
                };
                _spriteBatch.Draw(_pixelTexture, new Rectangle(startX + 6, y + 6, 18, 18), iconColor);
                
                // Item name with quality color
                Color nameColor = GetItemQualityColor(item.Quality);
                string displayName = item.GetDisplayName();
                if (displayName.Length > 45) displayName = displayName.Substring(0, 42) + "...";
                _spriteBatch.DrawString(_font, displayName, new Vector2(startX + 30, y + 7), nameColor);
                
                // Weight on right
                _spriteBatch.DrawString(_font, $"{item.Weight:F1}kg", new Vector2(startX + itemWidth - 55, y + 7), UITheme.TextSecondary);
                
                y += itemHeight;
            }
            
            // Empty inventory message
            if (items.Count == 0)
            {
                _spriteBatch.DrawString(_font, "Inventory is empty", new Vector2(startX + 200, startY + 150), UITheme.TextSecondary);
            }
            
            // Scroll indicator if more items
            if (items.Count > maxVisible)
            {
                _spriteBatch.DrawString(_font, $"... and {items.Count - maxVisible} more items (scroll with W/S)", 
                    new Vector2(startX + 120, y + 10), UITheme.TextSecondary);
            }
        }
        
        private void DrawItemDetailsSimple(MouseState mState)
        {
            var items = _player.Stats.Inventory.GetAllItems();
            
            // Details panel (right side)
            Rectangle detailPanel = new Rectangle(660, 75, 580, 420);
            _spriteBatch.Draw(_pixelTexture, detailPanel, UITheme.PanelBackground * 0.5f);
            DrawBorder(detailPanel, UITheme.PanelBorder, 1);
            
            _spriteBatch.DrawString(_font, "ITEM DETAILS", new Vector2(890, 80), UITheme.TextHighlight);
            
            if (_selectedInventoryIndex >= 0 && _selectedInventoryIndex < items.Count)
            {
                var item = items[_selectedInventoryIndex];
                int x = 680;
                int y = 110;
                
                // Item name
                _spriteBatch.DrawString(_font, item.GetDisplayName(), new Vector2(x, y), GetItemQualityColor(item.Quality));
                y += 28;
                
                // Category & Rarity
                _spriteBatch.DrawString(_font, $"Type: {item.Category}  |  Rarity: {item.Definition?.Rarity ?? ItemRarity.Common}", new Vector2(x, y), UITheme.TextSecondary);
                y += 22;
                
                // Value & Weight
                _spriteBatch.DrawString(_font, $"Value: {item.Value} caps  |  Weight: {item.Weight:F1} kg", new Vector2(x, y), UITheme.CategoryAmmo);
                y += 28;
                
                // Description
                if (!string.IsNullOrEmpty(item.Definition?.Description))
                {
                    // Word wrap description
                    string desc = item.Definition.Description;
                    int maxWidth = 70;
                    int lineStart = 0;
                    while (lineStart < desc.Length)
                    {
                        int lineLength = Math.Min(maxWidth, desc.Length - lineStart);
                        if (lineStart + lineLength < desc.Length)
                        {
                            int lastSpace = desc.LastIndexOf(' ', lineStart + lineLength, lineLength);
                            if (lastSpace > lineStart) lineLength = lastSpace - lineStart;
                        }
                        _spriteBatch.DrawString(_font, desc.Substring(lineStart, lineLength).Trim(), new Vector2(x, y), UITheme.TextPrimary);
                        y += 18;
                        lineStart += lineLength;
                        if (lineStart < desc.Length && desc[lineStart] == ' ') lineStart++;
                    }
                    y += 8;
                }
                
                // Stats for weapons/armor
                if (item.Category == ItemCategory.Weapon && item.Definition != null)
                {
                    _spriteBatch.DrawString(_font, $"Damage: {item.Definition.Damage}", new Vector2(x, y), UITheme.TextHighlight);
                    y += 20;
                    if (item.Definition.Range > 1)
                    {
                        _spriteBatch.DrawString(_font, $"Range: {item.Definition.Range}", new Vector2(x, y), UITheme.TextSecondary);
                        y += 20;
                    }
                }
                else if (item.Category == ItemCategory.Armor && item.Definition != null)
                {
                    _spriteBatch.DrawString(_font, $"Armor: {item.Definition.Armor}", new Vector2(x, y), UITheme.TextHighlight);
                    y += 20;
                }
                else if (item.Category == ItemCategory.Consumable && item.Definition != null)
                {
                    if (item.Definition.HealthRestore > 0)
                    {
                        _spriteBatch.DrawString(_font, $"Heals: {item.Definition.HealthRestore} HP", new Vector2(x, y), UITheme.TextSuccess);
                        y += 20;
                    }
                    if (item.Definition.HungerRestore > 0)
                    {
                        _spriteBatch.DrawString(_font, $"Hunger: +{item.Definition.HungerRestore}", new Vector2(x, y), UITheme.CategoryConsumable);
                        y += 20;
                    }
                    if (item.Definition.ThirstRestore > 0)
                    {
                        _spriteBatch.DrawString(_font, $"Thirst: +{item.Definition.ThirstRestore}", new Vector2(x, y), new Color(80, 150, 220));
                        y += 20;
                    }
                    if (item.Definition.IsMedical)
                    {
                        _spriteBatch.DrawString(_font, "[MEDICAL] Drag to body part in Gear Window", new Vector2(x, y), UITheme.TextHighlight);
                        y += 20;
                    }
                }
                
                // Usage hint
                y = 420;
                if (item.Category == ItemCategory.Weapon || item.Category == ItemCategory.Armor || item.Definition?.IsMedical == true)
                {
                    _spriteBatch.DrawString(_font, "Open GEAR WINDOW to equip/use on body parts", new Vector2(x, y), UITheme.TextSecondary);
                }
                else if (item.Category == ItemCategory.Consumable)
                {
                    _spriteBatch.DrawString(_font, "Double-click to consume", new Vector2(x, y), UITheme.TextSecondary);
                }
            }
            else
            {
                _spriteBatch.DrawString(_font, "Select an item to view details", new Vector2(820, 200), UITheme.TextSecondary);
            }
            
            // Buttons
            Rectangle gearBtn = new Rectangle(660, 520, 150, 35);
            Rectangle dropBtn = new Rectangle(660, 565, 150, 35);
            
            bool gearHover = gearBtn.Contains(mState.X, mState.Y);
            bool dropHover = dropBtn.Contains(mState.X, mState.Y);
            
            // Gear button (main action)
            _spriteBatch.Draw(_pixelTexture, gearBtn, gearHover ? UITheme.ButtonHover : UITheme.ButtonNormal);
            DrawBorder(gearBtn, UITheme.SelectionBorder, gearHover ? 2 : 1);
            _spriteBatch.DrawString(_font, "GEAR WINDOW [P]", new Vector2(gearBtn.X + 15, gearBtn.Y + 10), 
                gearHover ? UITheme.TextHighlight : UITheme.TextPrimary);
            
            // Drop button
            _spriteBatch.Draw(_pixelTexture, dropBtn, dropHover ? UITheme.ButtonHover : UITheme.ButtonNormal);
            DrawBorder(dropBtn, UITheme.PanelBorder, 1);
            _spriteBatch.DrawString(_font, "DROP [X]", new Vector2(dropBtn.X + 40, dropBtn.Y + 10), 
                dropHover ? UITheme.TextHighlight : UITheme.TextPrimary);
            
            // Body summary (bottom right)
            Rectangle summaryPanel = new Rectangle(830, 510, 400, 100);
            _spriteBatch.Draw(_pixelTexture, summaryPanel, UITheme.PanelBackground * 0.3f);
            DrawBorder(summaryPanel, UITheme.PanelBorder, 1);
            
            var body = _player.Stats.Body;
            int sy = 520;
            _spriteBatch.DrawString(_font, "BODY STATUS:", new Vector2(845, sy), UITheme.TextHighlight);
            sy += 20;
            
            int equippedWeapons = body.GetEquippedWeapons().Count;
            int totalHands = body.GetEquippableHands().Count;
            string handStatus = $"Hands: {equippedWeapons}/{totalHands} equipped";
            _spriteBatch.DrawString(_font, handStatus, new Vector2(845, sy), equippedWeapons > 0 ? UITheme.TextSuccess : UITheme.TextSecondary);
            sy += 18;
            
            string healthStatus = body.IsBleeding ? "BLEEDING" : body.HasInfection ? "INFECTED" : "Healthy";
            Color healthColor = body.IsBleeding ? UITheme.TextDanger : body.HasInfection ? UITheme.CategoryAmmo : UITheme.TextSuccess;
            _spriteBatch.DrawString(_font, $"Status: {healthStatus}", new Vector2(845, sy), healthColor);
            sy += 18;
            
            int damagedParts = body.Parts.Values.Count(p => p.CurrentHealth < p.MaxHealth);
            if (damagedParts > 0)
            {
                _spriteBatch.DrawString(_font, $"Damaged parts: {damagedParts}", new Vector2(845, sy), UITheme.CategoryAmmo);
            }
        }
        
        private void DrawEquipmentPanel(MouseState mState)
        {
            int startX = 50;
            int startY = 85;
            int slotWidth = 220;
            int slotHeight = 50;
            int gap = 4;
            
            // Panel background
            Rectangle equipPanel = new Rectangle(40, 75, 240, 500);
            _spriteBatch.Draw(_pixelTexture, equipPanel, UITheme.PanelBackground * 0.5f);
            DrawBorder(equipPanel, UITheme.PanelBorder, 1);
            
            _spriteBatch.DrawString(_font, "EQUIPPED", new Vector2(startX + 70, startY - 15), UITheme.TextHighlight);
            
            // Equipment slots to display
            var slots = new (EquipSlot slot, string label, string key)[]
            {
                (EquipSlot.MainHand, "Main Hand", "1"),
                (EquipSlot.OffHand, "Off Hand", "2"),
                (EquipSlot.Head, "Head", "3"),
                (EquipSlot.Torso, "Torso", "4"),
                (EquipSlot.Legs, "Legs", "5"),
                (EquipSlot.Feet, "Feet", "6"),
                (EquipSlot.Hands, "Hands", "7"),
                (EquipSlot.Accessory1, "Accessory 1", "8"),
            };
            
            int y = startY + 5;
            foreach (var (slot, label, key) in slots)
            {
                var equipped = _player.Stats.Inventory.GetEquipped(slot);
                Rectangle slotRect = new Rectangle(startX, y, slotWidth, slotHeight);
                bool isHovered = slotRect.Contains(mState.X, mState.Y);
                
                // Slot background
                Color bgColor = equipped != null ? 
                    (isHovered ? new Color(40, 80, 60) : new Color(30, 60, 45)) : 
                    (isHovered ? UITheme.HoverBackground : UITheme.PanelBackground * 0.3f);
                _spriteBatch.Draw(_pixelTexture, slotRect, bgColor);
                
                // Border
                DrawBorder(slotRect, isHovered ? UITheme.SelectionBorder : UITheme.PanelBorder, 1);
                
                // Key hint
                if (!string.IsNullOrEmpty(key))
                {
                    _spriteBatch.DrawString(_font, $"[{key}]", new Vector2(startX + 4, y + 3), UITheme.TextWarning * 0.7f);
                }
                
                // Slot label
                _spriteBatch.DrawString(_font, label, new Vector2(startX + 30, y + 5), UITheme.TextSecondary);
                
                // Item name
                string itemName = equipped != null ? equipped.GetDisplayName() : "(empty)";
                if (itemName.Length > 22) itemName = itemName.Substring(0, 19) + "...";
                Color nameColor = equipped != null ? GetItemQualityColor(equipped.Quality) : UITheme.TextSecondary * 0.5f;
                _spriteBatch.DrawString(_font, itemName, new Vector2(startX + 8, y + 26), nameColor);
                
                y += slotHeight + gap;
            }
            
            // Stats display at bottom
            y += 10;
            float totalArmor = _player.Stats.GetTotalArmor();
            _spriteBatch.DrawString(_font, $"Total Armor: {totalArmor:F0}", new Vector2(startX, y), UITheme.CategoryArmor);
            
            var weapon = _player.Stats.GetEquippedWeapon();
            string weaponInfo = weapon != null ? $"Weapon DMG: {weapon.GetEffectiveDamage():F0}" : "Unarmed (10 dmg)";
            _spriteBatch.DrawString(_font, weaponInfo, new Vector2(startX, y + 18), UITheme.CategoryWeapon);
        }
        
        private void DrawInventoryList(MouseState mState)
        {
            int startX = 300;
            int startY = 85;
            int itemWidth = 480;
            int itemHeight = 30;
            int maxVisible = 16;
            
            var items = _player.Stats.Inventory.GetAllItems();
            
            // Panel background
            Rectangle listPanel = new Rectangle(290, 75, 510, 500);
            _spriteBatch.Draw(_pixelTexture, listPanel, UITheme.PanelBackground * 0.5f);
            DrawBorder(listPanel, UITheme.PanelBorder, 1);
            
            // Header
            _spriteBatch.DrawString(_font, $"ITEMS ({items.Count}/{_player.Stats.Inventory.MaxSlots})", new Vector2(startX, startY - 15), UITheme.TextHighlight);
            _spriteBatch.DrawString(_font, $"Weight: {_player.Stats.Inventory.CurrentWeight:F1}/{_player.Stats.Inventory.MaxWeight:F0} kg", 
                new Vector2(startX + 250, startY - 15), UITheme.TextSecondary);
            
            // Item list
            int y = startY + 5;
            for (int i = 0; i < items.Count && i < maxVisible; i++)
            {
                var item = items[i];
                bool isSelected = (i == _selectedInventoryIndex);
                Rectangle itemRect = new Rectangle(startX, y, itemWidth, itemHeight);
                bool isHovered = itemRect.Contains(mState.X, mState.Y);
                
                // Background (hover effect only - clicks handled in Update)
                Color bgColor = isSelected ? UITheme.SelectionBackground :
                               isHovered ? UITheme.HoverBackground : Color.Transparent;
                _spriteBatch.Draw(_pixelTexture, itemRect, bgColor);
                
                // Selection indicator
                if (isSelected)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(startX, y, 3, itemHeight), UITheme.SelectionBorder);
                }
                
                // Item icon (colored square based on category)
                Color iconColor = item.Category switch
                {
                    ItemCategory.Weapon => UITheme.CategoryWeapon,
                    ItemCategory.Armor => UITheme.CategoryArmor,
                    ItemCategory.Consumable => UITheme.CategoryConsumable,
                    ItemCategory.Material => UITheme.CategoryMaterial,
                    ItemCategory.Ammo => UITheme.CategoryAmmo,
                    ItemCategory.Tool => new Color(200, 150, 80),
                    _ => UITheme.TextPrimary
                };
                _spriteBatch.Draw(_pixelTexture, new Rectangle(startX + 6, y + 6, 18, 18), iconColor);
                
                // Item name with quality color
                Color nameColor = GetItemQualityColor(item.Quality);
                string displayName = item.GetDisplayName();
                if (displayName.Length > 38) displayName = displayName.Substring(0, 35) + "...";
                _spriteBatch.DrawString(_font, displayName, new Vector2(startX + 30, y + 7), nameColor);
                
                // Weight on right
                _spriteBatch.DrawString(_font, $"{item.Weight:F1}kg", new Vector2(startX + itemWidth - 55, y + 7), UITheme.TextSecondary);
                
                y += itemHeight;
            }
            
            // Empty inventory message
            if (items.Count == 0)
            {
                _spriteBatch.DrawString(_font, "Inventory is empty", new Vector2(startX + 150, startY + 100), UITheme.TextSecondary);
            }
            
            // Scroll indicator if more items
            if (items.Count > maxVisible)
            {
                _spriteBatch.DrawString(_font, $"... and {items.Count - maxVisible} more items (scroll with W/S)", 
                    new Vector2(startX + 100, y + 10), UITheme.TextSecondary);
            }
            
            // Item details panel (right side)
            DrawItemDetails(mState, items);
        }
        
        private void DrawItemDetails(MouseState mState, List<Item> items)
        {
            Rectangle detailPanel = new Rectangle(820, 75, 420, 500);
            _spriteBatch.Draw(_pixelTexture, detailPanel, UITheme.PanelBackground * 0.5f);
            DrawBorder(detailPanel, UITheme.PanelBorder, 1);
            
            _spriteBatch.DrawString(_font, "ITEM DETAILS", new Vector2(970, 80), UITheme.TextHighlight);
            
            if (_selectedInventoryIndex >= 0 && _selectedInventoryIndex < items.Count)
            {
                var item = items[_selectedInventoryIndex];
                int x = 835;
                int y = 110;
                
                // Item name
                _spriteBatch.DrawString(_font, item.GetDisplayName(), new Vector2(x, y), GetItemQualityColor(item.Quality));
                y += 25;
                
                // Category
                _spriteBatch.DrawString(_font, $"Type: {item.Category}", new Vector2(x, y), UITheme.TextSecondary);
                y += 20;
                
                // Value
                _spriteBatch.DrawString(_font, $"Value: {item.Value} caps", new Vector2(x, y), UITheme.CategoryAmmo);
                y += 20;
                
                // Weight
                _spriteBatch.DrawString(_font, $"Weight: {item.Weight:F1} kg", new Vector2(x, y), UITheme.TextSecondary);
                y += 25;
                
                // Stats based on category
                if (item.Category == ItemCategory.Weapon)
                {
                    _spriteBatch.DrawString(_font, $"Damage: {item.GetEffectiveDamage():F0}", new Vector2(x, y), UITheme.CategoryWeapon);
                    y += 20;
                    int range = item.Definition?.Range ?? 1;
                    if (range > 1)
                    {
                        _spriteBatch.DrawString(_font, $"Range: {range}", new Vector2(x, y), UITheme.TextSecondary);
                        y += 20;
                    }
                }
                else if (item.Category == ItemCategory.Armor)
                {
                    _spriteBatch.DrawString(_font, $"Armor: {item.GetEffectiveArmor():F0}", new Vector2(x, y), UITheme.CategoryArmor);
                    y += 20;
                }
                else if (item.Category == ItemCategory.Consumable)
                {
                    float healAmount = item.Definition?.HealthRestore ?? 0;
                    if (healAmount > 0)
                        _spriteBatch.DrawString(_font, $"Heals: {healAmount} HP", new Vector2(x, y), UITheme.TextSuccess);
                    y += 20;
                }
                
                // Action buttons
                y = 480;
                Rectangle useBtn = new Rectangle(x, y, 120, 30);
                Rectangle dropBtn = new Rectangle(x + 140, y, 120, 30);
                
                string useText = item.Category == ItemCategory.Consumable ? "Use [Enter]" : "Equip [Enter]";
                DrawButton(useBtn, useText, mState);
                DrawButton(dropBtn, "Drop [X]", mState);
            }
            else
            {
                _spriteBatch.DrawString(_font, "Select an item to view details", new Vector2(860, 200), UITheme.TextSecondary);
            }
        }
        
        private void DrawEquipmentStats()
        {
            int y = 580;
            int x = 500;
            
            // Combat stats summary
            _spriteBatch.DrawString(_font, "COMBAT STATS:", new Vector2(x, y), Color.Cyan);
            _spriteBatch.DrawString(_font, $"Damage: {_player.Stats.Damage:F0}", new Vector2(x, y + 18), Color.Orange);
            _spriteBatch.DrawString(_font, $"Accuracy: {_player.Stats.Accuracy:P0}", new Vector2(x + 120, y + 18), Color.Yellow);
            _spriteBatch.DrawString(_font, $"Resistance: {_player.Stats.DamageResistance:P0}", new Vector2(x + 260, y + 18), Color.SteelBlue);
            _spriteBatch.DrawString(_font, $"Speed: {_player.Stats.Speed:F0}", new Vector2(x + 400, y + 18), Color.LimeGreen);
        }
        
        private Color GetItemQualityColor(ItemQuality quality)
        {
            return quality switch
            {
                ItemQuality.Broken => Color.DarkGray,
                ItemQuality.Poor => Color.Gray,
                ItemQuality.Normal => Color.White,
                ItemQuality.Good => Color.LimeGreen,
                ItemQuality.Excellent => Color.Cyan,
                ItemQuality.Masterwork => Color.Gold,
                _ => Color.White
            };
        }
        
        private Color GetItemCategoryColor(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Weapon => UITheme.CategoryWeapon,
                ItemCategory.Armor => UITheme.CategoryArmor,
                ItemCategory.Consumable => UITheme.CategoryConsumable,
                ItemCategory.Material => UITheme.CategoryMaterial,
                ItemCategory.Ammo => UITheme.CategoryAmmo,
                ItemCategory.Tool => new Color(150, 120, 200),
                ItemCategory.Quest => new Color(255, 215, 0),
                ItemCategory.Junk => Color.Gray,
                _ => Color.White
            };
        }
        
        // ============================================
        // CRAFTING UI DRAWING
        // ============================================
        
        private void DrawCraftingUI()
        {
            var mState = Mouse.GetState();
            
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            
            // Dark overlay
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.9f);
            
            // Main panel
            string wsName = _activeWorkstationType != null ? CraftingSystem.GetWorkstationName(_activeWorkstationType) : "Basic Crafting";
            Rectangle mainPanel = new Rectangle(30, 40, 1220, 640);
            DrawPanel(mainPanel, wsName.ToUpper());
            
            // Get available recipes
            var recipes = GameServices.Crafting.GetAvailableRecipes(_activeWorkstationType, _player.Stats);
            
            // Recipe list panel (left side)
            Rectangle listPanel = new Rectangle(40, 80, 450, 530);
            _spriteBatch.Draw(_pixelTexture, listPanel, UITheme.PanelBackground * 0.5f);
            DrawBorder(listPanel, UITheme.PanelBorder, 1);
            
            int startX = 50;
            int startY = 95;
            int itemHeight = 35;
            int itemWidth = 430;
            
            _spriteBatch.DrawString(_font, $"RECIPES ({recipes.Count})", new Vector2(startX, startY - 10), UITheme.TextHighlight);
            
            int y = startY + 15;
            for (int i = 0; i < recipes.Count && i < 14; i++)
            {
                var recipe = recipes[i];
                bool isSelected = (i == _selectedRecipeIndex);
                bool canCraft = GameServices.Crafting.HasMaterials(recipe, _player.Stats.Inventory);
                
                Rectangle itemRect = new Rectangle(startX, y, itemWidth, itemHeight - 2);
                bool isHovered = itemRect.Contains(mState.X, mState.Y);
                
                // Mouse click to select
                if (isHovered && mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                {
                    _selectedRecipeIndex = i;
                }
                
                // Background
                Color bgColor = isSelected ? UITheme.SelectionBackground :
                               isHovered ? UITheme.HoverBackground : Color.Transparent;
                _spriteBatch.Draw(_pixelTexture, itemRect, bgColor);
                
                // Selection indicator
                if (isSelected)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(startX, y, 3, itemHeight - 2), UITheme.SelectionBorder);
                }
                
                // Recipe name
                Color nameColor = canCraft ? UITheme.TextPrimary : UITheme.TextSecondary;
                _spriteBatch.DrawString(_font, recipe.Name, new Vector2(startX + 10, y + 8), nameColor);
                
                // Can craft indicator
                string craftStatus = canCraft ? "✓" : "✗";
                Color statusColor = canCraft ? UITheme.TextSuccess : UITheme.TextDanger;
                _spriteBatch.DrawString(_font, craftStatus, new Vector2(startX + itemWidth - 30, y + 8), statusColor);
                
                y += itemHeight;
            }
            
            if (recipes.Count == 0)
            {
                _spriteBatch.DrawString(_font, "No recipes available", new Vector2(startX + 120, startY + 100), UITheme.TextSecondary);
            }
            
            // Draw selected recipe details (right side)
            if (_selectedRecipeIndex >= 0 && _selectedRecipeIndex < recipes.Count)
            {
                DrawRecipeDetails(recipes[_selectedRecipeIndex], 510, 80, mState);
            }
            
            // Help bar
            DrawHelpBar("[C/Esc] Close  |  [W/S/Click] Navigate  |  [Enter/Click] Craft");
            
            _spriteBatch.End();
        }
        
        private void DrawRecipeDetails(Recipe recipe, int x, int y, MouseState mState)
        {
            // Details panel
            Rectangle detailPanel = new Rectangle(x, y, 730, 530);
            _spriteBatch.Draw(_pixelTexture, detailPanel, UITheme.PanelBackground * 0.5f);
            DrawBorder(detailPanel, UITheme.PanelBorder, 1);
            
            x += 15;
            y += 15;
            
            // Recipe name and description
            _spriteBatch.DrawString(_font, recipe.Name, new Vector2(x, y), UITheme.TextHighlight);
            _spriteBatch.DrawString(_font, recipe.Description, new Vector2(x, y + 25), UITheme.TextSecondary);
            
            // Category
            _spriteBatch.DrawString(_font, $"Category: {recipe.Category}", new Vector2(x, y + 55), UITheme.TextSecondary);
            
            // Requirements
            if (recipe.RequiredINT > 0)
            {
                Color intColor = _player.Stats.Attributes.INT >= recipe.RequiredINT ? UITheme.TextSuccess : UITheme.TextDanger;
                _spriteBatch.DrawString(_font, $"Required INT: {recipe.RequiredINT} (You: {_player.Stats.Attributes.INT})", new Vector2(x, y + 80), intColor);
            }
            
            // Ingredients
            _spriteBatch.DrawString(_font, "INGREDIENTS:", new Vector2(x, y + 115), UITheme.TextHighlight);
            int ingredientY = y + 140;
            
            foreach (var ingredient in recipe.Ingredients)
            {
                int have = _player.Stats.Inventory.GetItemCount(ingredient.Key);
                int need = ingredient.Value;
                bool hasEnough = have >= need;
                
                // Get item name
                var itemDef = ItemDatabase.Get(ingredient.Key);
                string itemName = itemDef?.Name ?? ingredient.Key;
                
                Color textColor = hasEnough ? UITheme.TextSuccess : UITheme.TextDanger;
                _spriteBatch.DrawString(_font, $"  • {itemName}: {have}/{need}", new Vector2(x, ingredientY), textColor);
                ingredientY += 22;
            }
            
            // Output
            _spriteBatch.DrawString(_font, "OUTPUT:", new Vector2(x, ingredientY + 15), UITheme.TextHighlight);
            var outputDef = ItemDatabase.Get(recipe.OutputItemId);
            string outputName = outputDef?.Name ?? recipe.OutputItemId;
            string outputText = recipe.OutputCount > 1 ? $"  {outputName} x{recipe.OutputCount}" : $"  {outputName}";
            _spriteBatch.DrawString(_font, outputText, new Vector2(x, ingredientY + 40), UITheme.TextPrimary);
            
            // Quality info
            _spriteBatch.DrawString(_font, $"  Base Quality: {recipe.BaseQuality}", new Vector2(x, ingredientY + 65), UITheme.TextSecondary);
            _spriteBatch.DrawString(_font, $"  (Higher INT = better quality chance)", new Vector2(x, ingredientY + 90), UITheme.TextSecondary * 0.7f);
            
            // Craft button
            bool canCraft = GameServices.Crafting.HasMaterials(recipe, _player.Stats.Inventory);
            Rectangle craftBtn = new Rectangle(x, ingredientY + 130, 180, 35);
            
            DrawButton(craftBtn, canCraft ? "CRAFT [Enter]" : "Need Materials", mState, canCraft);
        }
        
        private void CraftSelectedRecipe()
        {
            var recipes = GameServices.Crafting.GetAvailableRecipes(_activeWorkstationType, _player.Stats);
            if (_selectedRecipeIndex >= 0 && _selectedRecipeIndex < recipes.Count)
            {
                var recipe = recipes[_selectedRecipeIndex];
                var result = GameServices.Crafting.TryCraft(recipe, _activeWorkstationType, _player.Stats);
                if (result != null)
                {
                    ShowNotification($"Crafted {result.GetDisplayName()}!");
                }
            }
        }
        
        // ============================================
        // TRADING UI DRAWING
        // ============================================
        
        private void DrawTradingUI()
        {
            if (_tradingNPC == null) return;
            
            var mState = Mouse.GetState();
            
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            
            // Dark overlay
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.9f);
            
            // Main panel
            Rectangle mainPanel = new Rectangle(30, 40, 1220, 640);
            DrawPanel(mainPanel, $"TRADING WITH {_tradingNPC.Name.ToUpper()}");
            
            // Greeting
            _spriteBatch.DrawString(_font, $"\"{_tradingNPC.Greeting}\"", new Vector2(50, 80), UITheme.TextSecondary);
            
            // Gold display (top right)
            Rectangle goldPanel = new Rectangle(900, 75, 330, 60);
            _spriteBatch.Draw(_pixelTexture, goldPanel, UITheme.PanelBackground * 0.5f);
            DrawBorder(goldPanel, UITheme.PanelBorder, 1);
            _spriteBatch.DrawString(_font, $"Your Caps: {_player.Stats.Gold}", new Vector2(920, 85), UITheme.CategoryAmmo);
            _spriteBatch.DrawString(_font, $"Merchant: {_tradingNPC.Gold}", new Vector2(920, 108), UITheme.TextSecondary);
            
            // Tab buttons
            Rectangle buyTab = new Rectangle(50, 145, 150, 35);
            Rectangle sellTab = new Rectangle(210, 145, 150, 35);
            
            // Buy tab
            Color buyBg = _tradingBuyMode ? UITheme.SelectionBackground : UITheme.ButtonNormal;
            bool buyHovered = buyTab.Contains(mState.X, mState.Y);
            if (buyHovered && !_tradingBuyMode) buyBg = UITheme.ButtonHover;
            _spriteBatch.Draw(_pixelTexture, buyTab, buyBg);
            DrawBorder(buyTab, _tradingBuyMode ? UITheme.SelectionBorder : UITheme.PanelBorder, 1);
            _spriteBatch.DrawString(_font, "BUY", new Vector2(buyTab.X + 55, buyTab.Y + 9), 
                _tradingBuyMode ? UITheme.TextHighlight : UITheme.TextSecondary);
            
            // Sell tab
            Color sellBg = !_tradingBuyMode ? UITheme.SelectionBackground : UITheme.ButtonNormal;
            bool sellHovered = sellTab.Contains(mState.X, mState.Y);
            if (sellHovered && _tradingBuyMode) sellBg = UITheme.ButtonHover;
            _spriteBatch.Draw(_pixelTexture, sellTab, sellBg);
            DrawBorder(sellTab, !_tradingBuyMode ? UITheme.SelectionBorder : UITheme.PanelBorder, 1);
            _spriteBatch.DrawString(_font, "SELL", new Vector2(sellTab.X + 50, sellTab.Y + 9), 
                !_tradingBuyMode ? UITheme.TextHighlight : UITheme.TextSecondary);
            
            // Tab click handling
            if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                if (buyTab.Contains(mState.X, mState.Y)) _tradingBuyMode = true;
                if (sellTab.Contains(mState.X, mState.Y)) _tradingBuyMode = false;
            }
            
            int startY = 195;
            int itemHeight = 32;
            int itemWidth = 550;
            
            if (_tradingBuyMode)
            {
                DrawMerchantStock(50, startY, itemWidth, itemHeight, mState);
            }
            else
            {
                DrawPlayerSellList(50, startY, itemWidth, itemHeight, mState);
            }
            
            // Help bar
            DrawHelpBar("[T/Esc] Close  |  [Tab/Click] Buy/Sell  |  [W/S/Click] Navigate  |  [Enter/Click] Trade");
            
            _spriteBatch.End();
        }
        
        private void DrawMerchantStock(int x, int y, int width, int height, MouseState mState)
        {
            var availableStock = _tradingNPC.Stock.Where(s => s.Quantity > 0).ToList();
            
            // List panel
            Rectangle listPanel = new Rectangle(x - 5, y - 25, width + 10, 430);
            _spriteBatch.Draw(_pixelTexture, listPanel, UITheme.PanelBackground * 0.5f);
            DrawBorder(listPanel, UITheme.PanelBorder, 1);
            
            _spriteBatch.DrawString(_font, $"MERCHANT'S WARES ({availableStock.Count})", new Vector2(x, y - 20), UITheme.TextHighlight);
            
            for (int i = 0; i < availableStock.Count && i < 12; i++)
            {
                var stock = availableStock[i];
                var itemDef = ItemDatabase.Get(stock.ItemId);
                bool isSelected = (i == _selectedTradeIndex);
                int price = _tradingNPC.GetSellPrice(stock.ItemId);
                bool canAfford = _player.Stats.Gold >= price;
                
                int itemY = y + 5 + i * height;
                Rectangle itemRect = new Rectangle(x, itemY, width, height - 2);
                bool isHovered = itemRect.Contains(mState.X, mState.Y);
                
                // Mouse click to select
                if (isHovered && mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                {
                    if (_selectedTradeIndex == i)
                    {
                        // Double-click to trade
                        ExecuteTrade();
                    }
                    _selectedTradeIndex = i;
                }
                
                // Background
                Color bgColor = isSelected ? UITheme.SelectionBackground :
                               isHovered ? UITheme.HoverBackground : Color.Transparent;
                _spriteBatch.Draw(_pixelTexture, itemRect, bgColor);
                
                // Selection indicator
                if (isSelected)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(x, itemY, 3, height - 2), UITheme.SelectionBorder);
                }
                
                // Item name
                string itemName = itemDef?.Name ?? stock.ItemId;
                Color nameColor = canAfford ? UITheme.TextPrimary : UITheme.TextDanger;
                _spriteBatch.DrawString(_font, itemName, new Vector2(x + 10, itemY + 7), nameColor);
                
                // Quantity
                _spriteBatch.DrawString(_font, $"x{stock.Quantity}", new Vector2(x + 320, itemY + 7), UITheme.TextSecondary);
                
                // Price
                Color priceColor = canAfford ? UITheme.CategoryAmmo : UITheme.TextDanger;
                _spriteBatch.DrawString(_font, $"{price} caps", new Vector2(x + width - 80, itemY + 7), priceColor);
            }
            
            if (availableStock.Count == 0)
            {
                _spriteBatch.DrawString(_font, "Merchant has nothing to sell", new Vector2(x + 150, y + 80), UITheme.TextSecondary);
            }
            
            // Selected item details panel
            DrawTradeItemDetails(availableStock, 620, y - 25, mState, true);
        }
        
        private void DrawTradeItemDetails(object stockList, int x, int y, MouseState mState, bool isBuying)
        {
            // Details panel
            Rectangle detailPanel = new Rectangle(x, y, 610, 430);
            _spriteBatch.Draw(_pixelTexture, detailPanel, UITheme.PanelBackground * 0.5f);
            DrawBorder(detailPanel, UITheme.PanelBorder, 1);
            
            _spriteBatch.DrawString(_font, "ITEM DETAILS", new Vector2(x + 250, y + 5), UITheme.TextHighlight);
            
            x += 15;
            y += 35;
            
            if (isBuying)
            {
                var availableStock = stockList as List<MerchantStock>;
                if (_selectedTradeIndex >= 0 && _selectedTradeIndex < availableStock?.Count)
                {
                    var stock = availableStock[_selectedTradeIndex];
                    var itemDef = ItemDatabase.Get(stock.ItemId);
                    int price = _tradingNPC.GetSellPrice(stock.ItemId);
                    bool canAfford = _player.Stats.Gold >= price;
                    
                    _spriteBatch.DrawString(_font, itemDef?.Name ?? stock.ItemId, new Vector2(x, y), UITheme.TextHighlight);
                    y += 25;
                    _spriteBatch.DrawString(_font, itemDef?.Description ?? "No description", new Vector2(x, y), UITheme.TextSecondary);
                    y += 35;
                    _spriteBatch.DrawString(_font, $"Price: {price} caps", new Vector2(x, y), UITheme.CategoryAmmo);
                    y += 20;
                    _spriteBatch.DrawString(_font, $"Weight: {itemDef?.Weight ?? 0:F1} kg", new Vector2(x, y), UITheme.TextSecondary);
                    y += 20;
                    
                    if (itemDef != null)
                    {
                        if (itemDef.Category == ItemCategory.Weapon && itemDef.Damage > 0)
                        {
                            _spriteBatch.DrawString(_font, $"Damage: {itemDef.Damage}", new Vector2(x, y), UITheme.CategoryWeapon);
                            y += 20;
                        }
                        if (itemDef.Category == ItemCategory.Armor && itemDef.Armor > 0)
                        {
                            _spriteBatch.DrawString(_font, $"Armor: {itemDef.Armor}", new Vector2(x, y), UITheme.CategoryArmor);
                            y += 20;
                        }
                    }
                    
                    // Buy button
                    y += 20;
                    Rectangle buyBtn = new Rectangle(x, y, 150, 35);
                    DrawButton(buyBtn, canAfford ? "BUY [Enter]" : "Can't Afford", mState, canAfford);
                }
                else
                {
                    _spriteBatch.DrawString(_font, "Select an item to view details", new Vector2(x + 150, y + 100), UITheme.TextSecondary);
                }
            }
            else
            {
                var items = _player.Stats.Inventory.GetAllItems();
                if (_selectedTradeIndex >= 0 && _selectedTradeIndex < items.Count)
                {
                    var item = items[_selectedTradeIndex];
                    int price = _tradingNPC.GetBuyPrice(item);
                    bool merchantCanAfford = _tradingNPC.Gold >= price;
                    
                    _spriteBatch.DrawString(_font, item.GetDisplayName(), new Vector2(x, y), GetItemQualityColor(item.Quality));
                    y += 25;
                    _spriteBatch.DrawString(_font, item.Definition?.Description ?? "No description", new Vector2(x, y), UITheme.TextSecondary);
                    y += 35;
                    _spriteBatch.DrawString(_font, $"Sell Price: {price} caps", new Vector2(x, y), UITheme.CategoryAmmo);
                    y += 20;
                    _spriteBatch.DrawString(_font, $"Weight: {item.Weight:F1} kg", new Vector2(x, y), UITheme.TextSecondary);
                    y += 40;
                    
                    // Sell button
                    Rectangle sellBtn = new Rectangle(x, y, 150, 35);
                    string sellText = merchantCanAfford ? "SELL [Enter]" : "Merchant Broke";
                    DrawButton(sellBtn, sellText, mState, merchantCanAfford);
                }
                else
                {
                    _spriteBatch.DrawString(_font, "Select an item to view details", new Vector2(x + 150, y + 100), UITheme.TextSecondary);
                }
            }
        }
        
        private void DrawPlayerSellList(int x, int y, int width, int height, MouseState mState)
        {
            var items = _player.Stats.Inventory.GetAllItems();
            
            // List panel
            Rectangle listPanel = new Rectangle(x - 5, y - 25, width + 10, 430);
            _spriteBatch.Draw(_pixelTexture, listPanel, UITheme.PanelBackground * 0.5f);
            DrawBorder(listPanel, UITheme.PanelBorder, 1);
            
            _spriteBatch.DrawString(_font, $"YOUR ITEMS ({items.Count})", new Vector2(x, y - 20), UITheme.TextHighlight);
            
            for (int i = 0; i < items.Count && i < 12; i++)
            {
                var item = items[i];
                bool isSelected = (i == _selectedTradeIndex);
                int price = _tradingNPC.GetBuyPrice(item);
                bool merchantCanAfford = _tradingNPC.Gold >= price;
                
                int itemY = y + 5 + i * height;
                Rectangle itemRect = new Rectangle(x, itemY, width, height - 2);
                bool isHovered = itemRect.Contains(mState.X, mState.Y);
                
                // Mouse click to select
                if (isHovered && mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                {
                    if (_selectedTradeIndex == i)
                    {
                        // Double-click to trade
                        ExecuteTrade();
                    }
                    _selectedTradeIndex = i;
                }
                
                // Background
                Color bgColor = isSelected ? UITheme.SelectionBackground :
                               isHovered ? UITheme.HoverBackground : Color.Transparent;
                _spriteBatch.Draw(_pixelTexture, itemRect, bgColor);
                
                // Selection indicator
                if (isSelected)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(x, itemY, 3, height - 2), UITheme.SelectionBorder);
                }
                
                // Item name with quality color
                Color nameColor = GetItemQualityColor(item.Quality);
                string displayName = item.GetDisplayName();
                if (displayName.Length > 35) displayName = displayName.Substring(0, 32) + "...";
                _spriteBatch.DrawString(_font, displayName, new Vector2(x + 10, itemY + 7), nameColor);
                
                // Price
                Color priceColor = merchantCanAfford ? UITheme.CategoryAmmo : UITheme.TextDanger;
                _spriteBatch.DrawString(_font, $"+{price} caps", new Vector2(x + width - 90, itemY + 7), priceColor);
            }
            
            if (items.Count == 0)
            {
                _spriteBatch.DrawString(_font, "You have nothing to sell", new Vector2(x + 150, y + 80), UITheme.TextSecondary);
            }
            
            // Use the shared details panel
            DrawTradeItemDetails(items, 620, y - 25, mState, false);
        }
        
        // ============================================
        // QUEST LOG UI DRAWING
        // ============================================
        
        private void DrawQuestLogUI()
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            
            // Dark overlay
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.85f);
            
            // Title
            _spriteBatch.DrawString(_font, "=== QUEST LOG ===", new Vector2(100, 30), Color.Gold);
            _spriteBatch.DrawString(_font, "W/S: Navigate | Del: Abandon | J/Esc: Close", new Vector2(100, 55), Color.Gray);
            
            var activeQuests = GameServices.Quests.GetActiveQuests();
            
            int startY = 100;
            int itemHeight = 25;
            
            if (activeQuests.Count == 0)
            {
                _spriteBatch.DrawString(_font, "No active quests. Talk to NPCs to find quests!", new Vector2(100, startY), Color.Gray);
            }
            else
            {
                // Quest list (left side)
                for (int i = 0; i < activeQuests.Count; i++)
                {
                    var quest = activeQuests[i];
                    bool isSelected = (i == _selectedQuestIndex);
                    int itemY = startY + i * itemHeight;
                    
                    // Background
                    Color bgColor = isSelected ? Color.DarkBlue * 0.8f : Color.Transparent;
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(95, itemY - 2, 350, itemHeight - 2), bgColor);
                    
                    // Selection indicator
                    if (isSelected)
                    {
                        _spriteBatch.Draw(_pixelTexture, new Rectangle(90, itemY, 5, itemHeight - 4), Color.Yellow);
                    }
                    
                    // Status indicator
                    string statusIcon = quest.State == QuestState.Completed ? " [DONE]" : "";
                    Color statusColor = quest.State == QuestState.Completed ? Color.LimeGreen : Color.White;
                    
                    string typeIcon = quest.Definition.Type switch
                    {
                        QuestType.Main => "[M]",
                        QuestType.Side => "[S]",
                        QuestType.Bounty => "[B]",
                        _ => "[ ]"
                    };
                    
                    _spriteBatch.DrawString(_font, $"{typeIcon} {quest.Definition.Name}{statusIcon}", new Vector2(100, itemY), 
                        isSelected ? Color.Yellow : statusColor);
                }
                
                // Quest details (right side)
                if (_selectedQuestIndex >= 0 && _selectedQuestIndex < activeQuests.Count)
                {
                    DrawQuestDetails(activeQuests[_selectedQuestIndex], 500, 100);
                }
            }
            
            // Active quest count
            int mainCount = activeQuests.Count(q => q.Definition.Type == QuestType.Main);
            int sideCount = activeQuests.Count(q => q.Definition.Type != QuestType.Main);
            _spriteBatch.DrawString(_font, $"Main: {mainCount} | Side: {sideCount}", new Vector2(100, 680), Color.Gray);
            
            _spriteBatch.End();
        }
        
        private void DrawQuestDetails(QuestInstance quest, int x, int y)
        {
            // Quest name
            _spriteBatch.DrawString(_font, quest.Definition.Name, new Vector2(x, y), Color.Yellow);
            
            // Quest type
            string typeStr = quest.Definition.Type.ToString();
            if (quest.Definition.IsRepeatable) typeStr += " (Repeatable)";
            _spriteBatch.DrawString(_font, typeStr, new Vector2(x, y + 25), Color.Gray);
            
            // Description (word wrap)
            string desc = quest.Definition.Description;
            var words = desc.Split(' ');
            string line = "";
            int lineY = y + 55;
            foreach (var word in words)
            {
                if (_font.MeasureString(line + word).X > 350)
                {
                    _spriteBatch.DrawString(_font, line, new Vector2(x, lineY), Color.LightGray);
                    lineY += 20;
                    line = word + " ";
                }
                else
                {
                    line += word + " ";
                }
            }
            if (!string.IsNullOrEmpty(line))
            {
                _spriteBatch.DrawString(_font, line, new Vector2(x, lineY), Color.LightGray);
                lineY += 20;
            }
            
            // Objectives
            lineY += 15;
            _spriteBatch.DrawString(_font, "OBJECTIVES:", new Vector2(x, lineY), Color.Cyan);
            lineY += 25;
            
            foreach (var obj in quest.Objectives)
            {
                if (obj.IsHidden && !obj.IsComplete) continue;
                
                string progress = obj.GetProgressText();
                Color objColor = obj.IsComplete ? Color.LimeGreen : Color.White;
                if (obj.IsOptional) objColor = obj.IsComplete ? Color.LimeGreen : Color.Gray;
                
                string optionalStr = obj.IsOptional ? " (Optional)" : "";
                _spriteBatch.DrawString(_font, $"{progress} {obj.Description}{optionalStr}", new Vector2(x, lineY), objColor);
                lineY += 22;
            }
            
            // Rewards
            lineY += 15;
            _spriteBatch.DrawString(_font, "REWARDS:", new Vector2(x, lineY), Color.Gold);
            lineY += 25;
            
            var reward = quest.Definition.Reward;
            if (reward.Gold > 0)
            {
                _spriteBatch.DrawString(_font, $"  {reward.Gold} Gold", new Vector2(x, lineY), Color.Yellow);
                lineY += 20;
            }
            if (reward.XP > 0)
            {
                _spriteBatch.DrawString(_font, $"  {reward.XP} XP", new Vector2(x, lineY), Color.LightBlue);
                lineY += 20;
            }
            if (reward.MutationPoints > 0)
            {
                _spriteBatch.DrawString(_font, $"  {reward.MutationPoints} Mutation Points", new Vector2(x, lineY), Color.Purple);
                lineY += 20;
            }
            foreach (var item in reward.Items)
            {
                var itemDef = ItemDatabase.Get(item.Key);
                string name = itemDef?.Name ?? item.Key;
                _spriteBatch.DrawString(_font, $"  {item.Value}x {name}", new Vector2(x, lineY), Color.White);
                lineY += 20;
            }
            
            // Status
            if (quest.State == QuestState.Completed)
            {
                lineY += 20;
                _spriteBatch.DrawString(_font, ">> Return to NPC to turn in! <<", new Vector2(x, lineY), Color.LimeGreen);
            }
        }
        
        private void DrawQuestDialogueUI()
        {
            if (_tradingNPC == null) return;
            
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            
            // Dark overlay
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.85f);
            
            // NPC name and greeting
            _spriteBatch.DrawString(_font, $"=== {_tradingNPC.Name.ToUpper()} ===", new Vector2(100, 30), Color.Gold);
            _spriteBatch.DrawString(_font, _tradingNPC.Greeting, new Vector2(100, 60), Color.LightGray);
            _spriteBatch.DrawString(_font, "W/S: Navigate | Enter: Select | Esc: Close", new Vector2(100, 90), Color.Gray);
            
            int startY = 140;
            int itemHeight = 35;
            int index = 0;
            
            // Turn-in quests (highlighted)
            if (_turnInQuests.Count > 0)
            {
                _spriteBatch.DrawString(_font, "COMPLETE QUESTS:", new Vector2(100, startY), Color.LimeGreen);
                startY += 30;
                
                foreach (var quest in _turnInQuests)
                {
                    bool isSelected = (index == _selectedDialogueIndex);
                    
                    Color bgColor = isSelected ? Color.DarkGreen * 0.8f : Color.DarkGray * 0.3f;
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(95, startY, 500, itemHeight - 2), bgColor);
                    
                    if (isSelected)
                        _spriteBatch.Draw(_pixelTexture, new Rectangle(90, startY + 2, 5, itemHeight - 6), Color.Yellow);
                    
                    _spriteBatch.DrawString(_font, $"[TURN IN] {quest.Definition.Name}", 
                        new Vector2(100, startY + 8), isSelected ? Color.Yellow : Color.LimeGreen);
                    
                    startY += itemHeight;
                    index++;
                }
                startY += 10;
            }
            
            // Available quests
            if (_availableNPCQuests.Count > 0)
            {
                _spriteBatch.DrawString(_font, "AVAILABLE QUESTS:", new Vector2(100, startY), Color.Cyan);
                startY += 30;
                
                foreach (var quest in _availableNPCQuests)
                {
                    bool isSelected = (index == _selectedDialogueIndex);
                    
                    Color bgColor = isSelected ? Color.DarkBlue * 0.8f : Color.DarkGray * 0.3f;
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(95, startY, 500, itemHeight - 2), bgColor);
                    
                    if (isSelected)
                        _spriteBatch.Draw(_pixelTexture, new Rectangle(90, startY + 2, 5, itemHeight - 6), Color.Yellow);
                    
                    string typeIcon = quest.Type switch
                    {
                        QuestType.Main => "[M]",
                        QuestType.Bounty => "[B]",
                        _ => "[S]"
                    };
                    
                    _spriteBatch.DrawString(_font, $"{typeIcon} {quest.Name}", 
                        new Vector2(100, startY + 8), isSelected ? Color.Yellow : Color.White);
                    
                    startY += itemHeight;
                    index++;
                }
                startY += 10;
            }
            
            // Trade option (always available)
            startY += 10;
            bool tradeSelected = (index == _selectedDialogueIndex);
            Color tradeBg = tradeSelected ? Color.DarkOrange * 0.5f : Color.DarkGray * 0.3f;
            _spriteBatch.Draw(_pixelTexture, new Rectangle(95, startY, 500, itemHeight - 2), tradeBg);
            if (tradeSelected)
                _spriteBatch.Draw(_pixelTexture, new Rectangle(90, startY + 2, 5, itemHeight - 6), Color.Yellow);
            _spriteBatch.DrawString(_font, "[TRADE] Browse wares...", 
                new Vector2(100, startY + 8), tradeSelected ? Color.Yellow : Color.Gold);
            
            // Selected quest details (right side)
            if (_selectedDialogueIndex < _turnInQuests.Count + _availableNPCQuests.Count)
            {
                QuestDefinition selectedQuest = null;
                bool isTurnIn = false;
                
                if (_selectedDialogueIndex < _turnInQuests.Count)
                {
                    selectedQuest = _turnInQuests[_selectedDialogueIndex].Definition;
                    isTurnIn = true;
                }
                else
                {
                    int qIdx = _selectedDialogueIndex - _turnInQuests.Count;
                    if (qIdx < _availableNPCQuests.Count)
                        selectedQuest = _availableNPCQuests[qIdx];
                }
                
                if (selectedQuest != null)
                    DrawQuestPreview(selectedQuest, 650, 140, isTurnIn);
            }
            
            _spriteBatch.End();
        }
        
        private void DrawQuestPreview(QuestDefinition quest, int x, int y, bool isTurnIn)
        {
            _spriteBatch.DrawString(_font, quest.Name, new Vector2(x, y), Color.Yellow);
            _spriteBatch.DrawString(_font, quest.Type.ToString(), new Vector2(x, y + 22), Color.Gray);
            
            // Description (word wrap)
            int lineY = y + 50;
            var words = quest.Description.Split(' ');
            string line = "";
            foreach (var word in words)
            {
                if (_font.MeasureString(line + word).X > 300)
                {
                    _spriteBatch.DrawString(_font, line, new Vector2(x, lineY), Color.LightGray);
                    lineY += 18;
                    line = word + " ";
                }
                else
                    line += word + " ";
            }
            if (!string.IsNullOrEmpty(line))
            {
                _spriteBatch.DrawString(_font, line, new Vector2(x, lineY), Color.LightGray);
                lineY += 18;
            }
            
            // Objectives
            lineY += 15;
            _spriteBatch.DrawString(_font, "Objectives:", new Vector2(x, lineY), Color.Cyan);
            lineY += 22;
            foreach (var obj in quest.Objectives)
            {
                string opt = obj.IsOptional ? " (Opt)" : "";
                _spriteBatch.DrawString(_font, $"- {obj.Description}{opt}", new Vector2(x, lineY), Color.White);
                lineY += 20;
            }
            
            // Rewards
            lineY += 10;
            _spriteBatch.DrawString(_font, "Rewards:", new Vector2(x, lineY), Color.Gold);
            lineY += 22;
            _spriteBatch.DrawString(_font, quest.Reward.GetDisplayText(), new Vector2(x, lineY), Color.Yellow);
            
            // Action hint
            lineY += 40;
            if (isTurnIn)
                _spriteBatch.DrawString(_font, ">> Press ENTER to complete <<", new Vector2(x, lineY), Color.LimeGreen);
            else
                _spriteBatch.DrawString(_font, ">> Press ENTER to accept <<", new Vector2(x, lineY), Color.Cyan);
        }
        
        // ============================================
        // RESEARCH UI DRAWING
        // ============================================
        
        private void DrawResearchUI()
        {
            if (_researchCategories == null || _researchCategories.Length == 0) return;
            
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            
            // Dark overlay
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.9f);
            
            // Title
            string pathName = _player.Stats.SciencePath == SciencePath.Tinker ? "TINKER" : "DARK";
            _spriteBatch.DrawString(_font, $"=== RESEARCH ({pathName} PATH) ===", new Vector2(100, 20), Color.Gold);
            _spriteBatch.DrawString(_font, "Tab: Category | W/S: Navigate | Enter: Research | X: Cancel | R/Esc: Close", new Vector2(100, 45), Color.Gray);
            
            // Current research progress bar
            var currentResearch = GameServices.Research.GetCurrentResearch();
            if (currentResearch != null)
            {
                int barX = 650, barY = 20, barWidth = 300, barHeight = 25;
                float progress = currentResearch.ProgressPercent / 100f;
                
                _spriteBatch.Draw(_pixelTexture, new Rectangle(barX, barY, barWidth, barHeight), Color.DarkGray);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(barX + 2, barY + 2, (int)((barWidth - 4) * progress), barHeight - 4), Color.Cyan);
                _spriteBatch.DrawString(_font, $"Researching: {currentResearch.Name} ({currentResearch.ProgressPercent:F0}%)", new Vector2(barX, barY + 28), Color.Cyan);
            }
            
            // Category tabs
            int tabX = 100;
            int tabY = 75;
            for (int i = 0; i < _researchCategories.Length; i++)
            {
                var cat = _researchCategories[i];
                bool isSelected = (i == _selectedResearchCategory);
                string catName = cat.ToString().ToUpper();
                
                Color tabColor = isSelected ? Color.Yellow : Color.Gray;
                Color bgColor = isSelected ? Color.DarkBlue * 0.5f : Color.Transparent;
                
                int tabWidth = 120;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(tabX, tabY, tabWidth, 25), bgColor);
                _spriteBatch.DrawString(_font, catName, new Vector2(tabX + 10, tabY + 3), tabColor);
                
                tabX += tabWidth + 10;
            }
            
            // Research nodes list
            var currentCategory = _researchCategories[_selectedResearchCategory];
            var nodes = GameServices.Research.GetNodesByCategory(currentCategory);
            
            int startY = 115;
            int itemHeight = 28;
            int listHeight = 500;
            int visibleItems = listHeight / itemHeight;
            
            // Scrolling
            int scrollOffset = 0;
            if (_selectedResearchIndex >= visibleItems)
            {
                scrollOffset = _selectedResearchIndex - visibleItems + 1;
            }
            
            for (int i = scrollOffset; i < nodes.Count && i < scrollOffset + visibleItems; i++)
            {
                var node = nodes[i];
                bool isSelected = (i == _selectedResearchIndex);
                int itemY = startY + (i - scrollOffset) * itemHeight;
                
                // Background
                Color bgColor = isSelected ? Color.DarkBlue * 0.8f : Color.Transparent;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(95, itemY - 2, 450, itemHeight - 2), bgColor);
                
                // Selection indicator
                if (isSelected)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(90, itemY, 5, itemHeight - 4), Color.Yellow);
                }
                
                // State indicator and name
                string stateIcon = node.State switch
                {
                    ResearchState.Completed => "[✓]",
                    ResearchState.InProgress => "[...]",
                    ResearchState.Available => "[ ]",
                    _ => "[X]"
                };
                
                Color stateColor = node.State switch
                {
                    ResearchState.Completed => Color.LimeGreen,
                    ResearchState.InProgress => Color.Cyan,
                    ResearchState.Available => Color.White,
                    _ => Color.DarkGray
                };
                
                string tierStr = new string('★', node.Tier);
                _spriteBatch.DrawString(_font, $"{stateIcon} {node.Name}", new Vector2(100, itemY), isSelected ? Color.Yellow : stateColor);
                _spriteBatch.DrawString(_font, tierStr, new Vector2(400, itemY), Color.Gold * 0.7f);
            }
            
            // Research details (right side)
            if (_selectedResearchIndex >= 0 && _selectedResearchIndex < nodes.Count)
            {
                DrawResearchDetails(nodes[_selectedResearchIndex], 580, 115);
            }
            
            // Completed count
            int completedCount = GameServices.Research.GetCompletedNodes().Count;
            int totalCount = GameServices.Research.GetAllNodes().Count;
            _spriteBatch.DrawString(_font, $"Completed: {completedCount}/{totalCount}", new Vector2(100, 680), Color.Gray);
            
            _spriteBatch.End();
        }
        
        private void DrawResearchDetails(ResearchNode node, int x, int y)
        {
            // Name and tier
            _spriteBatch.DrawString(_font, node.Name, new Vector2(x, y), Color.Yellow);
            _spriteBatch.DrawString(_font, $"Tier {node.Tier} - {node.Category}", new Vector2(x, y + 22), Color.Gray);
            
            // Description (word wrap)
            int lineY = y + 50;
            var words = node.Description.Split(' ');
            string line = "";
            foreach (var word in words)
            {
                if (_font.MeasureString(line + word).X > 320)
                {
                    _spriteBatch.DrawString(_font, line, new Vector2(x, lineY), Color.LightGray);
                    lineY += 18;
                    line = word + " ";
                }
                else
                    line += word + " ";
            }
            if (!string.IsNullOrEmpty(line))
            {
                _spriteBatch.DrawString(_font, line, new Vector2(x, lineY), Color.LightGray);
                lineY += 18;
            }
            
            // Prerequisites
            if (node.Prerequisites.Count > 0)
            {
                lineY += 10;
                _spriteBatch.DrawString(_font, "Requires:", new Vector2(x, lineY), Color.Orange);
                lineY += 20;
                foreach (var prereq in node.Prerequisites)
                {
                    var prereqNode = GameServices.Research.GetNode(prereq);
                    string prereqName = prereqNode?.Name ?? prereq;
                    bool completed = prereqNode?.State == ResearchState.Completed;
                    Color prereqColor = completed ? Color.LimeGreen : Color.Red;
                    string checkMark = completed ? "✓" : "✗";
                    _spriteBatch.DrawString(_font, $"  {checkMark} {prereqName}", new Vector2(x, lineY), prereqColor);
                    lineY += 18;
                }
            }
            
            // Resource costs
            if (node.ResourceCost.Count > 0)
            {
                lineY += 10;
                _spriteBatch.DrawString(_font, "Resources:", new Vector2(x, lineY), Color.Cyan);
                lineY += 20;
                foreach (var cost in node.ResourceCost)
                {
                    var itemDef = ItemDatabase.Get(cost.Key);
                    string itemName = itemDef?.Name ?? cost.Key;
                    int playerHas = _player.Stats.Inventory.GetItemCount(cost.Key);
                    bool hasEnough = playerHas >= cost.Value;
                    Color costColor = hasEnough ? Color.LimeGreen : Color.Red;
                    _spriteBatch.DrawString(_font, $"  {itemName}: {playerHas}/{cost.Value}", new Vector2(x, lineY), costColor);
                    lineY += 18;
                }
            }
            
            // Research time
            lineY += 10;
            int minutes = node.ResearchTime / 60;
            int seconds = node.ResearchTime % 60;
            string timeStr = minutes > 0 ? $"{minutes}m {seconds}s" : $"{seconds}s";
            _spriteBatch.DrawString(_font, $"Time: {timeStr}", new Vector2(x, lineY), Color.White);
            
            // Unlocks
            lineY += 30;
            _spriteBatch.DrawString(_font, "UNLOCKS:", new Vector2(x, lineY), Color.Gold);
            lineY += 22;
            
            if (node.UnlocksRecipes.Count > 0)
            {
                _spriteBatch.DrawString(_font, $"  Recipes: {node.UnlocksRecipes.Count}", new Vector2(x, lineY), Color.White);
                lineY += 18;
            }
            if (node.UnlocksMutations.Count > 0)
            {
                _spriteBatch.DrawString(_font, $"  Mutations: {node.UnlocksMutations.Count}", new Vector2(x, lineY), Color.Purple);
                lineY += 18;
            }
            if (node.UnlocksStructures.Count > 0)
            {
                _spriteBatch.DrawString(_font, $"  Structures: {string.Join(", ", node.UnlocksStructures)}", new Vector2(x, lineY), Color.Brown);
                lineY += 18;
            }
            if (node.StatBonuses.Count > 0)
            {
                foreach (var bonus in node.StatBonuses)
                {
                    string sign = bonus.Value >= 0 ? "+" : "";
                    _spriteBatch.DrawString(_font, $"  {bonus.Key}: {sign}{bonus.Value}", new Vector2(x, lineY), Color.LimeGreen);
                    lineY += 18;
                }
            }
            if (!string.IsNullOrEmpty(node.UnlocksAbility))
            {
                _spriteBatch.DrawString(_font, $"  Ability: {node.UnlocksAbility}", new Vector2(x, lineY), Color.Magenta);
                lineY += 18;
            }
            
            // Action hint
            lineY += 20;
            if (node.State == ResearchState.Available && !GameServices.Research.IsResearching)
            {
                _spriteBatch.DrawString(_font, ">> Press ENTER to research <<", new Vector2(x, lineY), Color.Cyan);
            }
            else if (node.State == ResearchState.InProgress)
            {
                _spriteBatch.DrawString(_font, $">> Researching... {node.ProgressPercent:F0}% <<", new Vector2(x, lineY), Color.Cyan);
            }
            else if (node.State == ResearchState.Completed)
            {
                _spriteBatch.DrawString(_font, ">> COMPLETED <<", new Vector2(x, lineY), Color.LimeGreen);
            }
            else if (node.State == ResearchState.Locked)
            {
                if (node.RequiredLevel > _player.Stats.Level)
                    _spriteBatch.DrawString(_font, $">> Requires Level {node.RequiredLevel} <<", new Vector2(x, lineY), Color.Red);
                else
                    _spriteBatch.DrawString(_font, ">> Complete prerequisites first <<", new Vector2(x, lineY), Color.Red);
            }
        }
        
        // ============================================
        // BODY PANEL UI DRAWING
        // ============================================
        
        private void DrawBodyPanelUI()
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            
            var mState = Mouse.GetState();
            var mousePos = new Vector2(mState.X, mState.Y);
            
            // Panel dimensions
            int panelWidth = 900;
            int panelHeight = 600;
            int panelX = (1280 - panelWidth) / 2;
            int panelY = (720 - panelHeight) / 2;
            
            // Dim background
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.7f);
            
            // Main panel
            _spriteBatch.Draw(_pixelTexture, new Rectangle(panelX, panelY, panelWidth, panelHeight), UITheme.PanelBackground);
            DrawBorder(new Rectangle(panelX, panelY, panelWidth, panelHeight), UITheme.PanelBorder, 2);
            
            // Header
            _spriteBatch.Draw(_pixelTexture, new Rectangle(panelX, panelY, panelWidth, 40), UITheme.PanelHeader);
            _spriteBatch.DrawString(_font, "GEAR WINDOW - Drag items to equip/heal body parts", new Vector2(panelX + 15, panelY + 10), UITheme.TextPrimary);
            _spriteBatch.DrawString(_font, "[ESC] Close  [Right-Click] Unequip", new Vector2(panelX + panelWidth - 260, panelY + 10), UITheme.TextSecondary);
            
            // Left side: Inventory
            Rectangle inventoryArea = new Rectangle(panelX + 10, panelY + 50, 280, panelHeight - 60);
            _spriteBatch.Draw(_pixelTexture, inventoryArea, new Color(30, 35, 45));
            DrawBorder(inventoryArea, UITheme.PanelBorder, 1);
            
            _spriteBatch.DrawString(_font, "INVENTORY (drag to use)", new Vector2(inventoryArea.X + 5, inventoryArea.Y + 5), UITheme.TextHighlight);
            
            // Draw inventory items
            var inventory = _player.Stats.Inventory.GetAllItems();
            int itemHeight = 28;
            int startY = inventoryArea.Y + 28;
            
            for (int i = 0; i < inventory.Count && startY + itemHeight < inventoryArea.Y + inventoryArea.Height; i++)
            {
                var item = inventory[i];
                Rectangle itemRect = new Rectangle(inventoryArea.X + 5, startY, inventoryArea.Width - 10, itemHeight - 2);
                
                // Skip dragged item in list
                if (_isDragging && _dragSourceIndex == i)
                {
                    startY += itemHeight;
                    continue;
                }
                
                // Highlight if mouse over
                bool hover = itemRect.Contains(mousePos.ToPoint());
                Color bgColor = hover ? UITheme.HoverBackground : new Color(40, 45, 55);
                _spriteBatch.Draw(_pixelTexture, itemRect, bgColor);
                
                // Item category color indicator
                Color catColor = GetItemCategoryColor(item.Category);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(itemRect.X, itemRect.Y, 3, itemRect.Height), catColor);
                
                // Item name
                string itemText = item.StackCount > 1 ? $"{item.Name} x{item.StackCount}" : item.Name;
                _spriteBatch.DrawString(_font, itemText, new Vector2(itemRect.X + 8, itemRect.Y + 4), UITheme.TextPrimary);
                
                // Show if medical or weapon (usable on body parts)
                if (item.Definition?.IsMedical == true)
                {
                    _spriteBatch.DrawString(_font, "[MED]", new Vector2(itemRect.Right - 45, itemRect.Y + 4), UITheme.TextSuccess);
                }
                else if (item.Category == ItemCategory.Weapon)
                {
                    _spriteBatch.DrawString(_font, "[WPN]", new Vector2(itemRect.Right - 45, itemRect.Y + 4), UITheme.CategoryWeapon);
                }
                
                startY += itemHeight;
            }
            
            // Right side: Body parts
            Rectangle bodyArea = new Rectangle(panelX + 300, panelY + 50, 590, panelHeight - 60);
            _spriteBatch.Draw(_pixelTexture, bodyArea, new Color(30, 35, 45));
            DrawBorder(bodyArea, UITheme.PanelBorder, 1);
            
            _spriteBatch.DrawString(_font, "BODY PARTS", new Vector2(bodyArea.X + 5, bodyArea.Y + 5), UITheme.TextHighlight);
            
            // Summary stats
            var body = _player.Stats.Body;
            string summaryText = $"Hands: {body.GetEquippableHands().Count}  |  Bleeding: {(body.IsBleeding ? "YES" : "No")}  |  Infected: {(body.HasInfection ? "YES" : "No")}";
            _spriteBatch.DrawString(_font, summaryText, new Vector2(bodyArea.X + 150, bodyArea.Y + 5), 
                body.IsBleeding || body.HasInfection ? UITheme.TextDanger : UITheme.TextSecondary);
            
            // Draw body parts in two columns
            var bodyParts = body.Parts.Values.OrderBy(p => GetBodyPartSortOrder(p.Type)).ToList();
            int partHeight = 55;
            int partStartY = bodyArea.Y + 28 - _bodyPanelScroll;
            int col = 0;
            int row = 0;
            int colWidth = 290;
            
            foreach (var part in bodyParts)
            {
                int partX = bodyArea.X + 5 + col * colWidth;
                int partY = partStartY + row * partHeight;
                
                // Skip if outside visible area
                if (partY + partHeight < bodyArea.Y + 25 || partY > bodyArea.Y + bodyArea.Height)
                {
                    row++;
                    if (row >= 10) { row = 0; col++; }
                    continue;
                }
                
                Rectangle partRect = new Rectangle(partX, partY, colWidth - 10, partHeight - 5);
                
                // Background - highlight if hovering with dragged item
                bool isHoverTarget = _isDragging && _hoverBodyPart == part;
                bool isSelected = _selectedBodyPartId == part.Id;
                Color partBg = isHoverTarget ? new Color(60, 80, 60) : 
                               isSelected ? UITheme.SelectionBackground : new Color(40, 45, 55);
                _spriteBatch.Draw(_pixelTexture, partRect, partBg);
                
                // Left color bar based on condition
                Color conditionColor = GetConditionColor(part.Condition);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(partX, partY, 4, partHeight - 5), conditionColor);
                
                // Part name + mutation tag
                string partName = part.Name;
                if (part.IsMutationPart) partName += " [M]";
                _spriteBatch.DrawString(_font, partName, new Vector2(partX + 8, partY + 2), UITheme.TextPrimary);
                
                // Health bar
                int barX = partX + 8;
                int barY = partY + 18;
                int barWidth = 100;
                int barHeight = 8;
                float healthPercent = part.CurrentHealth / part.MaxHealth;
                
                _spriteBatch.Draw(_pixelTexture, new Rectangle(barX, barY, barWidth, barHeight), new Color(40, 40, 40));
                _spriteBatch.Draw(_pixelTexture, new Rectangle(barX, barY, (int)(barWidth * healthPercent), barHeight), conditionColor);
                _spriteBatch.DrawString(_font, $"{part.CurrentHealth:F0}/{part.MaxHealth:F0}", 
                    new Vector2(barX + barWidth + 5, barY - 2), UITheme.TextSecondary);
                
                // Efficiency
                _spriteBatch.DrawString(_font, $"Eff: {part.Efficiency:P0}", 
                    new Vector2(barX + barWidth + 60, barY - 2), 
                    part.Efficiency < 0.5f ? UITheme.TextDanger : UITheme.TextSecondary);
                
                // Status line (injuries, ailments, equipped item)
                int statusY = partY + 32;
                string statusText = "";
                
                // Injuries
                if (part.Injuries.Any())
                {
                    statusText += $"Injuries: {part.Injuries.Count}  ";
                }
                
                // Ailments
                if (part.IsBleeding) statusText += "[BLEEDING] ";
                if (part.IsInfected) statusText += "[INFECTED] ";
                if (part.HasFracture) statusText += "[FRACTURE] ";
                
                // Equipped item
                if (part.EquippedItem != null)
                {
                    statusText += $"[{part.EquippedItem.Name}]";
                }
                else if (part.CanEquipWeapon)
                {
                    statusText += "(empty hand)";
                }
                
                Color statusColor = (part.IsBleeding || part.IsInfected) ? UITheme.TextDanger : UITheme.TextSecondary;
                if (part.EquippedItem != null) statusColor = UITheme.TextSuccess;
                
                _spriteBatch.DrawString(_font, statusText, new Vector2(partX + 8, statusY), statusColor);
                
                // Valid drop target indicator
                if (isHoverTarget && _draggedItem != null)
                {
                    bool canDrop = CanDropItemOnPart(_draggedItem, part);
                    Color dropColor = canDrop ? Color.LimeGreen : Color.Red;
                    DrawBorder(partRect, dropColor, 2);
                }
                
                row++;
                if (row >= 10) { row = 0; col++; }
            }
            
            // Draw dragged item following cursor
            if (_isDragging && _draggedItem != null)
            {
                Rectangle dragRect = new Rectangle((int)_dragPosition.X - 50, (int)_dragPosition.Y - 10, 120, 24);
                _spriteBatch.Draw(_pixelTexture, dragRect, new Color(60, 70, 90) * 0.9f);
                DrawBorder(dragRect, UITheme.SelectionBorder, 1);
                _spriteBatch.DrawString(_font, _draggedItem.Name, new Vector2(dragRect.X + 5, dragRect.Y + 4), Color.White);
            }
            
            // Instructions at bottom
            string instructions = "WEAPONS → Hands  |  ARMOR → Body parts  |  MEDICAL → Injured parts  |  Right-click to unequip";
            Vector2 instrSize = _font.MeasureString(instructions);
            _spriteBatch.DrawString(_font, instructions, 
                new Vector2(panelX + (panelWidth - instrSize.X) / 2, panelY + panelHeight - 25), UITheme.TextSecondary);
            
            _spriteBatch.End();
        }
        
        private bool CanDropItemOnPart(Item item, BodyPart part)
        {
            if (item == null || part == null) return false;
            
            // Medical items can go on any damaged/injured part
            if (item.Definition?.IsMedical == true)
            {
                return part.CurrentHealth < part.MaxHealth || part.IsBleeding || part.IsInfected || part.HasFracture;
            }
            
            // Weapons can go on hands
            if (item.Category == ItemCategory.Weapon && part.CanEquipWeapon)
            {
                return true;
            }
            
            // Armor can go on appropriate body parts
            if (item.Category == ItemCategory.Armor && part.CanEquipArmor)
            {
                return true;
            }
            
            // Consumables with health restore on damaged parts
            if (item.Category == ItemCategory.Consumable && item.Definition?.HealthRestore > 0)
            {
                return part.CurrentHealth < part.MaxHealth;
            }
            
            return false;
        }
        
        private Color GetConditionColor(BodyPartCondition condition)
        {
            return condition switch
            {
                BodyPartCondition.Healthy => new Color(80, 200, 80),
                BodyPartCondition.Scratched => new Color(150, 200, 80),
                BodyPartCondition.Bruised => new Color(200, 200, 80),
                BodyPartCondition.Cut => new Color(200, 150, 80),
                BodyPartCondition.Injured => new Color(200, 100, 80),
                BodyPartCondition.SeverelyInjured => new Color(200, 60, 60),
                BodyPartCondition.Broken => new Color(150, 40, 40),
                BodyPartCondition.Destroyed => new Color(80, 40, 40),
                BodyPartCondition.Missing => new Color(60, 60, 60),
                _ => Color.Gray
            };
        }
        
        private int GetBodyPartSortOrder(BodyPartType type)
        {
            // Sort order: Head parts, Torso parts, Arms/Hands, Legs/Feet, Mutation parts
            return type switch
            {
                BodyPartType.Head => 0,
                BodyPartType.Brain => 1,
                BodyPartType.LeftEye => 2,
                BodyPartType.RightEye => 3,
                BodyPartType.Nose => 4,
                BodyPartType.Jaw => 5,
                BodyPartType.Torso => 10,
                BodyPartType.Heart => 11,
                BodyPartType.LeftLung => 12,
                BodyPartType.RightLung => 13,
                BodyPartType.Stomach => 14,
                BodyPartType.Liver => 15,
                BodyPartType.LeftArm => 20,
                BodyPartType.LeftHand => 21,
                BodyPartType.RightArm => 22,
                BodyPartType.RightHand => 23,
                BodyPartType.MutantArm => 24,
                BodyPartType.MutantHand => 25,
                BodyPartType.LeftLeg => 30,
                BodyPartType.LeftFoot => 31,
                BodyPartType.RightLeg => 32,
                BodyPartType.RightFoot => 33,
                BodyPartType.MutantLeg => 34,
                BodyPartType.Tail => 40,
                BodyPartType.Wings => 41,
                BodyPartType.Tentacle => 42,
                _ => 50
            };
        }
        
        // ============================================
        // COMBAT ZONE DRAWING
        // ============================================
        
        private void DrawCombatZone()
        {
            // Draw a subtle circle/area indicating the combat zone
            int centerX = (int)_combat.CombatCenter.X;
            int centerY = (int)_combat.CombatCenter.Y;
            int radiusPixels = _combat.CombatZoneRadius * _world.TileSize;
            int fleeRadiusPixels = _combat.FleeExitRadius * _world.TileSize;
            int maxRadiusPixels = _combat.MaxZoneRadius * _world.TileSize;
            
            // Get zone info
            var zoneInfo = _combat.GetZoneInfo();
            
            // Draw max zone boundary (very subtle, shows how much it can expand)
            if (zoneInfo.current < zoneInfo.max)
            {
                DrawCircleOutline(centerX, centerY, maxRadiusPixels, Color.DarkRed * 0.15f, 1);
            }
            
            // Draw flee boundary (outer, subtle)
            DrawCircleOutline(centerX, centerY, fleeRadiusPixels, Color.Gray * 0.3f, 2);
            
            // Draw combat zone boundary - color changes based on expansion state
            Color zoneColor = zoneInfo.expanded ? Color.Red * 0.6f : Color.OrangeRed * 0.5f;
            int zoneThickness = zoneInfo.expanded ? 4 : 3;
            DrawCircleOutline(centerX, centerY, radiusPixels, zoneColor, zoneThickness);
            
            // Draw combat center marker
            Rectangle centerMarker = new Rectangle(
                centerX - 8,
                centerY - 8,
                16, 16
            );
            _spriteBatch.Draw(_pixelTexture, centerMarker, Color.OrangeRed * 0.4f);
            
            // Draw "danger zone" warning near edge
            float playerDist = Vector2.Distance(_player.Position, _combat.CombatCenter);
            int playerTileDist = (int)(playerDist / _world.TileSize);
            int distFromEdge = _combat.CombatZoneRadius - playerTileDist;
            
            if (distFromEdge <= _combat.ZoneEdgeThreshold && distFromEdge >= 0)
            {
                // Player is near edge - draw warning indicator
                DrawCircleOutline(centerX, centerY, radiusPixels - (_combat.ZoneEdgeThreshold * _world.TileSize), 
                    Color.Yellow * 0.4f, 2);
            }
        }
        
        /// <summary>
        /// Draw a circle outline using line segments
        /// </summary>
        private void DrawCircleOutline(int centerX, int centerY, int radius, Color color, int thickness)
        {
            int segments = 32;
            float angleStep = MathHelper.TwoPi / segments;
            
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;
                
                int x1 = centerX + (int)(Math.Cos(angle1) * radius);
                int y1 = centerY + (int)(Math.Sin(angle1) * radius);
                int x2 = centerX + (int)(Math.Cos(angle2) * radius);
                int y2 = centerY + (int)(Math.Sin(angle2) * radius);
                
                DrawLine(x1, y1, x2, y2, color, thickness);
            }
        }
        
        /// <summary>
        /// Draw a line between two points
        /// </summary>
        private void DrawLine(int x1, int y1, int x2, int y2, Color color, int thickness)
        {
            float distance = Vector2.Distance(new Vector2(x1, y1), new Vector2(x2, y2));
            float angle = (float)Math.Atan2(y2 - y1, x2 - x1);
            
            Rectangle rect = new Rectangle(x1, y1, (int)distance, thickness);
            _spriteBatch.Draw(_pixelTexture, rect, null, color, angle, Vector2.Zero, SpriteEffects.None, 0);
        }
        
        // ============================================
        // BUILDING DRAWING
        // ============================================
        
        private void DrawStructures()
        {
            var structures = GameServices.Building.GetAllStructures();
            var drawnPositions = new HashSet<Point>(); // Avoid drawing same structure twice
            
            foreach (var structure in structures)
            {
                // Skip if already drawn (multi-tile structures)
                if (drawnPositions.Contains(structure.Position)) continue;
                drawnPositions.Add(structure.Position);
                
                int x = structure.Position.X * _world.TileSize;
                int y = structure.Position.Y * _world.TileSize;
                int w = structure.Definition.Width * _world.TileSize;
                int h = structure.Definition.Height * _world.TileSize;
                
                // Get display color based on state
                Color color = structure.GetDisplayColor();
                
                // Door special rendering (different when open)
                if (structure.Definition.CanBeOpened && structure.IsOpen)
                {
                    // Draw open door as thin line
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, 8, _world.TileSize), color);
                }
                else
                {
                    // Draw structure
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(x + 2, y + 2, w - 4, h - 4), color);
                    
                    // Draw border
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, w, 2), Color.Black * 0.5f);
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, 2, h), Color.Black * 0.5f);
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(x + w - 2, y, 2, h), Color.Black * 0.5f);
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + h - 2, w, 2), Color.Black * 0.5f);
                }
                
                // Draw health bar for damaged structures
                if (structure.State == StructureState.Damaged || structure.HealthPercent < 0.9f)
                {
                    float hp = structure.HealthPercent;
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y - 6, w, 4), Color.DarkGray);
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y - 6, (int)(w * hp), 4), hp > 0.5f ? Color.Green : Color.Red);
                }
                
                // Draw construction progress for incomplete structures
                if (structure.State == StructureState.UnderConstruction)
                {
                    string progress = $"{structure.BuildProgress * 100:F0}%";
                    _spriteBatch.DrawString(_font, progress, new Vector2(x + 4, y + 4), Color.White);
                }
                
                // Draw special indicators
                if (structure.Definition.LightRadius > 0 && structure.IsFunctional)
                {
                    // Draw light glow effect
                    int glowSize = (int)(structure.Definition.LightRadius * _world.TileSize);
                    _spriteBatch.Draw(_pixelTexture, 
                        new Rectangle(x + w/2 - glowSize/2, y + h/2 - glowSize/2, glowSize, glowSize), 
                        Color.Yellow * 0.1f);
                }
            }
            
            // Draw build mode preview
            if (GameServices.Building.InBuildMode && GameServices.Building.PreviewPosition.HasValue)
            {
                var preview = GameServices.Building.PreviewPosition.Value;
                var structType = GameServices.Building.SelectedStructure.Value;
                var def = GameServices.Building.GetDefinition(structType);
                
                int px = preview.X * _world.TileSize;
                int py = preview.Y * _world.TileSize;
                int pw = def.Width * _world.TileSize;
                int ph = def.Height * _world.TileSize;
                
                Color previewColor = GameServices.Building.GetPlacementColor(structType, preview, _world);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(px, py, pw, ph), previewColor);
                
                // Draw outline
                Color outlineColor = GameServices.Building.CanPlaceAt(structType, preview, _world) ? Color.Green : Color.Red;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(px, py, pw, 2), outlineColor);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(px, py, 2, ph), outlineColor);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(px + pw - 2, py, 2, ph), outlineColor);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(px, py + ph - 2, pw, 2), outlineColor);
            }
        }
        
        private void DrawBuildModeUI()
        {
            var structType = GameServices.Building.SelectedStructure;
            if (!structType.HasValue) return;
            
            var def = GameServices.Building.GetDefinition(structType.Value);
            
            // Build mode panel
            _spriteBatch.Draw(_pixelTexture, new Rectangle(10, 10, 300, 80), Color.Black * 0.8f);
            
            _spriteBatch.DrawString(_font, "BUILD MODE", new Vector2(15, 15), Color.Yellow);
            _spriteBatch.DrawString(_font, $"Placing: {def.Name}", new Vector2(15, 35), Color.White);
            
            // Show cost
            string costStr = "Cost: " + string.Join(", ", def.BuildCost.Select(kv => $"{kv.Key}x{kv.Value}"));
            _spriteBatch.DrawString(_font, costStr, new Vector2(15, 50), Color.LightGray);
            
            _spriteBatch.DrawString(_font, "Click: Place | Right-click/Esc: Cancel | Del: Remove", new Vector2(15, 65), Color.Gray);
        }
        
        private void DrawBuildMenuUI()
        {
            int menuX = 200;
            int menuY = 100;
            int menuWidth = 880;
            int menuHeight = 500;
            
            // Background
            _spriteBatch.Draw(_pixelTexture, new Rectangle(menuX, menuY, menuWidth, menuHeight), Color.Black * 0.9f);
            
            // Title
            _spriteBatch.DrawString(_font, "BUILD MENU - Press B to close", new Vector2(menuX + 10, menuY + 10), Color.Yellow);
            
            // Category tabs
            int tabX = menuX + 10;
            int tabY = menuY + 40;
            int tabWidth = 100;
            
            for (int i = 0; i < _buildCategories.Length; i++)
            {
                Color tabColor = (i == _buildCategoryIndex) ? Color.DarkGreen : Color.DarkGray;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(tabX + i * (tabWidth + 5), tabY, tabWidth, 25), tabColor);
                _spriteBatch.DrawString(_font, _buildCategories[i].ToString(), new Vector2(tabX + i * (tabWidth + 5) + 5, tabY + 5), Color.White);
            }
            
            // Items in selected category
            var categoryItems = GameServices.Building.GetDefinitionsByCategory(_buildCategories[_buildCategoryIndex]);
            
            int itemY = tabY + 40;
            int itemHeight = 60;
            int itemWidth = menuWidth - 40;
            
            for (int i = 0; i < categoryItems.Count; i++)
            {
                var item = categoryItems[i];
                int currentY = itemY + i * (itemHeight + 5);
                
                // Skip if off screen
                if (currentY > menuY + menuHeight - 30) break;
                
                // Background
                Color bgColor = (i == _buildItemIndex) ? Color.DarkGreen : Color.DarkGray * 0.5f;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(menuX + 20, currentY, itemWidth, itemHeight), bgColor);
                
                // Number key hint
                if (i < 9)
                {
                    _spriteBatch.DrawString(_font, $"[{i + 1}]", new Vector2(menuX + 25, currentY + 5), Color.Yellow);
                }
                
                // Item info
                _spriteBatch.DrawString(_font, item.Name, new Vector2(menuX + 60, currentY + 5), Color.White);
                _spriteBatch.DrawString(_font, item.Description, new Vector2(menuX + 60, currentY + 22), Color.LightGray);
                
                // Cost
                string costStr = string.Join(", ", item.BuildCost.Select(kv => $"{kv.Key}x{kv.Value}"));
                _spriteBatch.DrawString(_font, $"Cost: {costStr}", new Vector2(menuX + 60, currentY + 40), Color.Cyan);
                
                // Stats
                string statsStr = "";
                if (item.MaxHealth > 0) statsStr += $"HP:{item.MaxHealth} ";
                if (item.CoverValue > 0) statsStr += $"Cover:{item.CoverValue:P0} ";
                if (item.LightRadius > 0) statsStr += $"Light:{item.LightRadius} ";
                if (item.WarmthAmount > 0) statsStr += $"Warmth:+{item.WarmthAmount} ";
                if (item.RestQuality > 0) statsStr += $"Rest:{item.RestQuality:P0} ";
                if (item.StorageSlots > 0) statsStr += $"Storage:{item.StorageSlots} ";
                
                if (!string.IsNullOrEmpty(statsStr))
                {
                    _spriteBatch.DrawString(_font, statsStr, new Vector2(menuX + 400, currentY + 22), Color.LightGreen);
                }
            }
            
            // Instructions
            _spriteBatch.DrawString(_font, "Click or A/D: Category | Click or W/S: Select | Enter/1-9: Place | Esc/Right-click: Close", 
                new Vector2(menuX + 10, menuY + menuHeight - 25), Color.Gray);
        }
        
        private double _totalTime = 0; // For animations
        
        // ============================================
        // INSPECT PANEL (Right-click to inspect)
        // ============================================
        
        private void DrawInspectPanel()
        {
            if (!_inspectPanelOpen || _inspectedObject == null) return;
            
            int panelWidth = 200;
            int panelHeight = 180;
            int x = (int)_inspectPanelPosition.X;
            int y = (int)_inspectPanelPosition.Y;
            
            // Background
            _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, panelWidth, panelHeight), Color.Black * 0.9f);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, panelWidth, 2), Color.White * 0.5f);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, 2, panelHeight), Color.White * 0.5f);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(x + panelWidth - 2, y, 2, panelHeight), Color.White * 0.5f);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + panelHeight - 2, panelWidth, 2), Color.White * 0.5f);
            
            int lineY = y + 5;
            int lineHeight = 16;
            
            // Draw based on object type
            if (_inspectedObject is EnemyEntity enemy)
            {
                DrawInspectEnemy(enemy, x, ref lineY, lineHeight, panelWidth);
            }
            else if (_inspectedObject is NPCEntity npc)
            {
                DrawInspectNPC(npc, x, ref lineY, lineHeight);
            }
            else if (_inspectedObject is Structure structure)
            {
                DrawInspectStructure(structure, x, ref lineY, lineHeight);
            }
            else if (_inspectedObject is WorldItem worldItem)
            {
                DrawInspectWorldItem(worldItem, x, ref lineY, lineHeight);
            }
            else if (_inspectedObject is Tile tile)
            {
                DrawInspectTile(tile, x, ref lineY, lineHeight);
            }
            
            // Close hint
            _spriteBatch.DrawString(_font, "[Left-click to close]", new Vector2(x + 5, y + panelHeight - 18), Color.Gray);
        }
        
        private void DrawInspectEnemy(EnemyEntity enemy, int x, ref int lineY, int lineHeight, int panelWidth)
        {
            // Header
            Color headerColor = (enemy.Behavior == CreatureBehavior.Aggressive || enemy.IsProvoked) ? Color.Red : Color.ForestGreen;
            _spriteBatch.DrawString(_font, $">> {enemy.Name} <<", new Vector2(x + 5, lineY), headerColor);
            lineY += lineHeight + 2;
            
            // Behavior
            string behaviorText = enemy.Behavior switch
            {
                CreatureBehavior.Aggressive => "Hostile",
                CreatureBehavior.Passive => enemy.IsProvoked ? "Provoked!" : "Passive",
                CreatureBehavior.Cowardly => enemy.IsProvoked ? "Fleeing!" : "Timid",
                CreatureBehavior.Territorial => "Territorial",
                _ => "Unknown"
            };
            _spriteBatch.DrawString(_font, $"Behavior: {behaviorText}", new Vector2(x + 5, lineY), Color.LightGray);
            lineY += lineHeight;
            
            // Health bar
            float healthPct = enemy.CurrentHealth / enemy.MaxHealth;
            _spriteBatch.Draw(_pixelTexture, new Rectangle(x + 5, lineY, 190, 12), Color.DarkGray);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(x + 5, lineY, (int)(190 * healthPct), 12), 
                healthPct > 0.5f ? Color.Green : healthPct > 0.25f ? Color.Yellow : Color.Red);
            _spriteBatch.DrawString(_font, $"{enemy.CurrentHealth:F0}/{enemy.MaxHealth:F0}", new Vector2(x + 70, lineY - 1), Color.White);
            lineY += lineHeight + 2;
            
            // Stats
            _spriteBatch.DrawString(_font, $"Damage: {enemy.Damage}", new Vector2(x + 5, lineY), Color.White);
            lineY += lineHeight;
            _spriteBatch.DrawString(_font, $"Accuracy: {enemy.Accuracy:P0}", new Vector2(x + 5, lineY), Color.White);
            lineY += lineHeight;
            _spriteBatch.DrawString(_font, $"Speed: {enemy.Speed}", new Vector2(x + 5, lineY), Color.White);
            lineY += lineHeight;
            
            // Combat status
            if (enemy.InCombatZone)
            {
                _spriteBatch.DrawString(_font, "[In Combat]", new Vector2(x + 5, lineY), Color.OrangeRed);
                lineY += lineHeight;
            }
            else
            {
                string stateText = enemy.State.ToString();
                _spriteBatch.DrawString(_font, $"State: {stateText}", new Vector2(x + 5, lineY), Color.Gray);
                lineY += lineHeight;
            }
            
            // Special ability info
            if (enemy.PrimaryAbility != EnemyAbility.None)
            {
                string abilityName = enemy.PrimaryAbility switch
                {
                    EnemyAbility.AcidSpit => "Acid Spit (ranged, burns)",
                    EnemyAbility.PsionicBlast => "Psionic Blast (stuns)",
                    EnemyAbility.Knockback => "Heavy Swing (knockback)",
                    EnemyAbility.Ambush => "Ambush (2x stealth dmg)",
                    EnemyAbility.SpawnSwarmling => "Spawn Swarmling",
                    EnemyAbility.Explode => "Explodes on death!",
                    EnemyAbility.Regenerate => "Regenerates HP",
                    _ => enemy.PrimaryAbility.ToString()
                };
                _spriteBatch.DrawString(_font, $"Ability: {abilityName}", new Vector2(x + 5, lineY), Color.Magenta);
                lineY += lineHeight;
            }
            
            // Stealth indicator
            if (enemy.IsStealthed)
            {
                _spriteBatch.DrawString(_font, "[STEALTHED]", new Vector2(x + 5, lineY), new Color(80, 80, 100));
            }
        }
        
        private void DrawInspectNPC(NPCEntity npc, int x, ref int lineY, int lineHeight)
        {
            // Header
            _spriteBatch.DrawString(_font, $">> {npc.Name} <<", new Vector2(x + 5, lineY), Color.Cyan);
            lineY += lineHeight + 2;
            
            // Type
            _spriteBatch.DrawString(_font, $"Type: {npc.Type}", new Vector2(x + 5, lineY), Color.LightGray);
            lineY += lineHeight;
            
            // Can trade
            if (npc.Type == NPCType.Merchant || npc.Type == NPCType.Wanderer)
            {
                _spriteBatch.DrawString(_font, "[Can Trade - Press T]", new Vector2(x + 5, lineY), Color.Yellow);
                lineY += lineHeight;
            }
            
            // Quest giver hint
            if (npc.Type == NPCType.QuestGiver)
            {
                _spriteBatch.DrawString(_font, "[May Have Quests]", new Vector2(x + 5, lineY), Color.Gold);
            }
        }
        
        private void DrawInspectStructure(Structure structure, int x, ref int lineY, int lineHeight)
        {
            // Header
            _spriteBatch.DrawString(_font, $">> {structure.Definition.Name} <<", new Vector2(x + 5, lineY), Color.SandyBrown);
            lineY += lineHeight + 2;
            
            // State
            string stateText = structure.State switch
            {
                StructureState.Blueprint => "Blueprint",
                StructureState.UnderConstruction => "Under Construction",
                StructureState.Complete => "Complete",
                StructureState.Damaged => "Damaged",
                _ => "Unknown"
            };
            _spriteBatch.DrawString(_font, $"State: {stateText}", new Vector2(x + 5, lineY), Color.LightGray);
            lineY += lineHeight;
            
            // Health
            if (structure.State == StructureState.Complete || structure.State == StructureState.Damaged)
            {
                float healthPct = structure.HealthPercent;
                _spriteBatch.DrawString(_font, $"Health: {healthPct:P0}", new Vector2(x + 5, lineY), 
                    healthPct > 0.5f ? Color.Green : healthPct > 0.25f ? Color.Yellow : Color.Red);
                lineY += lineHeight;
            }
            
            // Functional info based on type
            if (structure.Definition.CanBeOpened)
            {
                _spriteBatch.DrawString(_font, structure.IsOpen ? "[Open]" : "[Closed]", new Vector2(x + 5, lineY), Color.White);
                lineY += lineHeight;
            }
            
            // Show structure type benefits
            string benefitText = structure.Type switch
            {
                StructureType.Bed => "[Provides Rest]",
                StructureType.Campfire => "[Provides Warmth]",
                StructureType.StorageBox => "[Storage]",
                StructureType.CraftingBench => "[Crafting]",
                StructureType.ResearchTable => "[Research]",
                StructureType.CookingStation => "[Cooking]",
                StructureType.Torch => "[Light Source]",
                StructureType.Barricade => "[Defense]",
                _ => null
            };
            
            if (benefitText != null)
            {
                _spriteBatch.DrawString(_font, benefitText, new Vector2(x + 5, lineY), Color.LightBlue);
            }
        }
        
        private void DrawInspectWorldItem(WorldItem worldItem, int x, ref int lineY, int lineHeight)
        {
            // Header
            _spriteBatch.DrawString(_font, $">> {worldItem.Item.GetDisplayName()} <<", new Vector2(x + 5, lineY), Color.Yellow);
            lineY += lineHeight + 2;
            
            // Category
            _spriteBatch.DrawString(_font, $"Type: {worldItem.Item.Definition.Category}", new Vector2(x + 5, lineY), Color.LightGray);
            lineY += lineHeight;
            
            // Quantity
            if (worldItem.Item.StackCount > 1)
            {
                _spriteBatch.DrawString(_font, $"Quantity: {worldItem.Item.StackCount}", new Vector2(x + 5, lineY), Color.White);
                lineY += lineHeight;
            }
            
            // Value
            _spriteBatch.DrawString(_font, $"Value: {worldItem.Item.Value * worldItem.Item.StackCount} caps", new Vector2(x + 5, lineY), Color.Gold);
            lineY += lineHeight;
            
            // Pickup hint
            _spriteBatch.DrawString(_font, "[Press G to pickup]", new Vector2(x + 5, lineY), Color.Cyan);
        }
        
        private void DrawInspectTile(Tile tile, int x, ref int lineY, int lineHeight)
        {
            // Header
            _spriteBatch.DrawString(_font, $">> {tile.Type} <<", new Vector2(x + 5, lineY), Color.White);
            lineY += lineHeight + 2;
            
            // Walkable
            string walkText = tile.IsWalkable ? "Walkable" : "Blocked";
            Color walkColor = tile.IsWalkable ? Color.Green : Color.Red;
            _spriteBatch.DrawString(_font, walkText, new Vector2(x + 5, lineY), walkColor);
            lineY += lineHeight;
            
            // Movement cost
            _spriteBatch.DrawString(_font, $"Move Cost: {tile.MovementCost:F1}", new Vector2(x + 5, lineY), Color.LightGray);
        }
        
        private void DrawCombatUI()
        {
            Color combatColor = _combat.IsPlayerTurn ? Color.LimeGreen : Color.OrangeRed;
            string turnText = _combat.IsPlayerTurn ? "YOUR TURN" : "ENEMY TURN";
            
            _spriteBatch.Draw(_pixelTexture, new Rectangle(10, 10, 280, 50), Color.Black * 0.7f);
            _spriteBatch.DrawString(_font, $"COMBAT - {turnText}", new Vector2(15, 15), combatColor);
            
            if (_combat.IsPlayerTurn)
            {
                // Show AP, MP, and reserved AP
                string reservedStr = _player.Stats.ReservedAP > 0 ? $" (Reserved: {_player.Stats.ReservedAP})" : "";
                _spriteBatch.DrawString(_font, 
                    $"AP: {_combat.PlayerActionPoints}/{_combat.PlayerMaxActionPoints}  |  MP: {_combat.PlayerMovementPoints}/{_combat.PlayerMaxMovementPoints}{reservedStr}", 
                    new Vector2(15, 35), Color.Cyan);
                _spriteBatch.DrawString(_font, "Space: End | Tab: Target | R: AP→MP | E: Escape", new Vector2(10, 55), Color.Yellow);
            }
            
            // Combat zone info (top right corner)
            var zoneInfo = _combat.GetZoneInfo();
            var escapeInfo = _combat.GetEscapeStatus();
            
            _spriteBatch.Draw(_pixelTexture, new Rectangle(1080, 110, 190, 70), Color.Black * 0.7f);
            
            // Zone radius
            string zoneText = zoneInfo.expanded 
                ? $"Zone: {zoneInfo.current}/{zoneInfo.max} (EXPANDED!)" 
                : $"Zone: {zoneInfo.current}/{zoneInfo.max}";
            Color zoneTextColor = zoneInfo.expanded ? Color.OrangeRed : Color.White;
            _spriteBatch.DrawString(_font, zoneText, new Vector2(1085, 115), zoneTextColor);
            
            // Escape attempts
            Color attemptColor = escapeInfo.attempts >= escapeInfo.maxAttempts ? Color.Red : Color.Yellow;
            _spriteBatch.DrawString(_font, $"Escape Tries: {escapeInfo.attempts}/{escapeInfo.maxAttempts}", new Vector2(1085, 135), attemptColor);
            
            // Escape/stealth status
            if (escapeInfo.isHidden)
            {
                _spriteBatch.DrawString(_font, "[HIDDEN - Can Escape!]", new Vector2(1085, 155), Color.LimeGreen);
            }
            else if (escapeInfo.canEscape)
            {
                _spriteBatch.DrawString(_font, "[Can Escape at Edge]", new Vector2(1085, 155), Color.Cyan);
            }
            else
            {
                _spriteBatch.DrawString(_font, "[No Escape]", new Vector2(1085, 155), Color.Gray);
            }
            
            // Selected enemy info
            if (_selectedEnemy != null && _selectedEnemy.IsAlive)
            {
                _spriteBatch.Draw(_pixelTexture, new Rectangle(1080, 10, 190, 95), Color.Black * 0.7f);
                _spriteBatch.DrawString(_font, $"Target: {_selectedEnemy.Name}", new Vector2(1085, 15), Color.Yellow);
                _spriteBatch.DrawString(_font, $"HP: {_selectedEnemy.CurrentHealth:F0}/{_selectedEnemy.MaxHealth:F0}", new Vector2(1085, 35), Color.White);
                _spriteBatch.DrawString(_font, $"DMG: {_selectedEnemy.Damage} | ACC: {_selectedEnemy.Accuracy:P0}", new Vector2(1085, 55), Color.LightGray);
                
                // Show behavior
                string behaviorText = _selectedEnemy.Behavior switch
                {
                    CreatureBehavior.Aggressive => "Hostile",
                    CreatureBehavior.Passive => _selectedEnemy.IsProvoked ? "Provoked!" : "Passive",
                    CreatureBehavior.Cowardly => _selectedEnemy.IsProvoked ? "Fleeing!" : "Passive",
                    CreatureBehavior.Territorial => "Territorial",
                    _ => ""
                };
                Color behaviorColor = (_selectedEnemy.Behavior == CreatureBehavior.Aggressive || _selectedEnemy.IsProvoked) 
                    ? Color.Red : Color.ForestGreen;
                _spriteBatch.DrawString(_font, behaviorText, new Vector2(1085, 75), behaviorColor);
            }
            
            // Combat log
            _spriteBatch.Draw(_pixelTexture, new Rectangle(10, 560, 400, 90), Color.Black * 0.7f);
            _spriteBatch.DrawString(_font, "Combat Log:", new Vector2(15, 565), Color.Gray);
            for (int i = 0; i < _combatLog.Count; i++)
            {
                _spriteBatch.DrawString(_font, _combatLog[i], new Vector2(15, 580 + i * 14), Color.White);
            }
        }
        
        private void DrawMutationSelect()
        {
            _spriteBatch.Begin();
            
            // Darken background
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.8f);
            
            // Title
            string title = _usingFreePick ? "FREE MUTATION PICK!" : "CHOOSE YOUR MUTATION";
            Vector2 titleSize = _font.MeasureString(title);
            _spriteBatch.DrawString(_font, title, new Vector2(640 - titleSize.X / 2, 50), Color.Magenta);
            
            // Points remaining
            string pointsText = $"Points: {_player.Stats.MutationPoints} | Free Picks: {_player.Stats.FreeMutationPicks}";
            _spriteBatch.DrawString(_font, pointsText, new Vector2(640 - _font.MeasureString(pointsText).X / 2, 80), Color.Yellow);
            
            // Draw mutation choices
            int startY = 120;
            int boxHeight = 80;
            int boxWidth = 600;
            int startX = 340;
            
            for (int i = 0; i < _mutationChoices.Count && i < 10; i++) // Max 10 shown
            {
                var mutation = _mutationChoices[i];
                int y = startY + i * (boxHeight + 10);
                
                // Box background
                Color boxColor = (i == _selectedMutationIndex) ? Color.DarkMagenta : Color.DarkGray * 0.8f;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(startX, y, boxWidth, boxHeight), boxColor);
                
                // Selection indicator
                if (i == _selectedMutationIndex)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(startX - 5, y, 5, boxHeight), Color.Magenta);
                }
                
                // Mutation info
                string numKey = (i < 3) ? $"[{i + 1}] " : "";
                string mutName = $"{numKey}{mutation.Name}";
                if (mutation.MaxLevel > 1)
                {
                    int currentLevel = _player.Stats.GetMutationLevel(mutation.Type);
                    mutName += $" (Lv.{currentLevel} -> {currentLevel + 1}/{mutation.MaxLevel})";
                }
                
                _spriteBatch.DrawString(_font, mutName, new Vector2(startX + 10, y + 10), Color.White);
                _spriteBatch.DrawString(_font, mutation.Description, new Vector2(startX + 10, y + 30), Color.LightGray);
                
                // Category and rarity
                string categoryText = $"[{mutation.Category}]";
                _spriteBatch.DrawString(_font, categoryText, new Vector2(startX + 10, y + 55), Color.Cyan);
            }
            
            // Controls
            _spriteBatch.DrawString(_font, "Click: Select | Right-Click: Cancel | W/S: Navigate | 1-3: Quick Select", 
                new Vector2(280, 680), Color.Gray);
            
            _spriteBatch.End();
        }
        
        private void DrawVitalOrganChoice()
        {
            _spriteBatch.Begin();
            
            // Dark overlay with red pulse
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.9f);
            float pulse = (float)Math.Sin(_totalTime * 5) * 0.1f + 0.2f;
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.DarkRed * pulse);
            
            // Main panel
            Rectangle panel = new Rectangle(240, 80, 800, 560);
            _spriteBatch.Draw(_pixelTexture, panel, UITheme.PanelBackground);
            DrawBorder(panel, Color.Red, 3);
            
            // Title
            string title = "CRITICAL HIT - VITAL ORGAN DAMAGED!";
            Vector2 titleSize = _font.MeasureString(title);
            _spriteBatch.DrawString(_font, title, new Vector2(640 - titleSize.X / 2, 95), Color.Red);
            
            // Mutation name
            _spriteBatch.DrawString(_font, "MOVEABLE VITAL ORGAN ACTIVATED", new Vector2(440, 125), Color.Yellow);
            
            // Explanation
            var damageResult = _player.Stats.PendingVitalOrganDamage;
            string hitPart = damageResult?.HitPart?.Name ?? "vital organ";
            _spriteBatch.DrawString(_font, $"Your {hitPart} has been critically damaged!", new Vector2(260, 160), Color.White);
            _spriteBatch.DrawString(_font, "Select a body part to redirect the damage:", new Vector2(260, 180), Color.LightGray);
            
            // Body part selection
            int startX = 440;
            int startY = 220;
            int boxHeight = 40;
            int boxWidth = 400;
            
            for (int i = 0; i < _vitalOrganTargets.Count && i < 10; i++)
            {
                var part = _vitalOrganTargets[i];
                bool isSelected = (i == _vitalOrganTargetIndex);
                
                Rectangle boxRect = new Rectangle(startX, startY + i * (boxHeight + 5), boxWidth, boxHeight);
                
                // Background
                Color bgColor = isSelected ? new Color(100, 50, 50) : UITheme.PanelBackground * 0.7f;
                _spriteBatch.Draw(_pixelTexture, boxRect, bgColor);
                DrawBorder(boxRect, isSelected ? Color.Red : UITheme.PanelBorder, isSelected ? 2 : 1);
                
                // Part info
                float healthPercent = part.CurrentHealth / part.MaxHealth;
                Color healthColor = healthPercent > 0.6f ? Color.Green : healthPercent > 0.3f ? Color.Yellow : Color.Red;
                
                _spriteBatch.DrawString(_font, part.Name, new Vector2(boxRect.X + 10, boxRect.Y + 5), Color.White);
                _spriteBatch.DrawString(_font, $"HP: {part.CurrentHealth:F0}/{part.MaxHealth:F0}", 
                    new Vector2(boxRect.X + 180, boxRect.Y + 5), healthColor);
                
                // Damage that will be taken (1.5x penalty)
                float damageToTake = (part.MaxHealth * 0.25f) * 1.5f;
                _spriteBatch.DrawString(_font, $"Will take: {damageToTake:F0} damage", 
                    new Vector2(boxRect.X + 10, boxRect.Y + 22), Color.Orange);
                
                // Selection indicator
                if (isSelected)
                {
                    _spriteBatch.DrawString(_font, ">>>", new Vector2(boxRect.X - 30, boxRect.Y + 10), Color.Yellow);
                }
            }
            
            // Cooldown info
            int level = _player.Stats.GetMutationLevel(MutationType.MoveableVitalOrgan);
            int cooldownDays = 4 - level;
            _spriteBatch.DrawString(_font, $"Cooldown after use: {cooldownDays} days", 
                new Vector2(260, 580), Color.Gray);
            
            // Instructions
            _spriteBatch.DrawString(_font, "[W/S] Navigate  |  [Enter] Confirm  |  [Esc] Accept Death", 
                new Vector2(320, 610), Color.Gray);
            
            _spriteBatch.End();
        }
        
        private void DrawGameOver()
        {
            _spriteBatch.Begin();
            
            // Darken and red tint
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.85f);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.DarkRed * 0.3f);
            
            // Game Over text
            string gameOverText = "YOU DIED";
            Vector2 goSize = _font.MeasureString(gameOverText);
            _spriteBatch.DrawString(_font, gameOverText, new Vector2(640 - goSize.X / 2 + 3, 250 + 3), Color.Black);
            _spriteBatch.DrawString(_font, gameOverText, new Vector2(640 - goSize.X / 2, 250), Color.Red);
            
            // Stats summary
            string statsText = $"Level {_player.Stats.Level} | Enemies Killed: {CountDeadEnemies()}";
            Vector2 statsSize = _font.MeasureString(statsText);
            _spriteBatch.DrawString(_font, statsText, new Vector2(640 - statsSize.X / 2, 320), Color.White);
            
            // Traits summary
            string traitsText = $"Build: {_player.Stats.Traits[0]}";
            if (_player.Stats.Mutations.Count > 0)
            {
                traitsText += $" | Mutations: {_player.Stats.Mutations.Count}";
            }
            Vector2 traitsSize = _font.MeasureString(traitsText);
            _spriteBatch.DrawString(_font, traitsText, new Vector2(640 - traitsSize.X / 2, 350), Color.LightGray);
            
            // Restart prompt
            string restartText = "Press R to Restart";
            Vector2 restartSize = _font.MeasureString(restartText);
            _spriteBatch.DrawString(_font, restartText, new Vector2(640 - restartSize.X / 2, 450), Color.Yellow);
            
            _spriteBatch.End();
        }
        
        private int CountDeadEnemies()
        {
            int count = 0;
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsAlive) count++;
            }
            return count;
        }
        
        /// <summary>
        /// Wrap text to fit within a maximum width, returns list of lines
        /// </summary>
        private List<string> WrapText(string text, float maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            
            string[] words = text.Split(' ');
            string currentLine = "";
            
            foreach (string word in words)
            {
                string testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
                Vector2 size = _font.MeasureString(testLine);
                
                if (size.X > maxWidth && currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
            
            if (currentLine.Length > 0)
            {
                lines.Add(currentLine);
            }
            
            return lines;
        }
        
        /// <summary>
        /// Truncate text with ellipsis if it exceeds max width
        /// </summary>
        private string TruncateText(string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            if (_font.MeasureString(text).X <= maxWidth)
                return text;
            
            string ellipsis = "...";
            float ellipsisWidth = _font.MeasureString(ellipsis).X;
            
            for (int i = text.Length - 1; i > 0; i--)
            {
                string truncated = text.Substring(0, i);
                if (_font.MeasureString(truncated).X + ellipsisWidth <= maxWidth)
                {
                    return truncated + ellipsis;
                }
            }
            
            return ellipsis;
        }
        
        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            GameServices.Shutdown();
            base.OnExiting(sender, args);
        }
    }
}
