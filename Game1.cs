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
        private WorldEventSystem _worldEvents;
        private PlayerEntity _player;
        private Random _random = new Random();

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
        private EnemyEntity _hoverEnemy = null;  // Enemy under mouse cursor for hover tooltip

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
        private int _craftingCategoryIndex = 0;  // Current category tab
        private int _craftingScrollOffset = 0;   // For scrolling recipe list
        private int _craftQuantity = 1;          // Batch crafting amount
        private float _craftingFeedbackTimer = 0f;  // Success/fail animation
        private string _craftingFeedbackText = "";
        private bool _craftingFeedbackSuccess = false;

        // Category filter for crafting
        private static readonly RecipeCategory[] CraftingCategories = new RecipeCategory[]
        {
            RecipeCategory.Basic,
            RecipeCategory.Weapons,
            RecipeCategory.Armor,
            RecipeCategory.Consumables,
            RecipeCategory.Materials,
            RecipeCategory.Tools,
            RecipeCategory.Gadgets,
            RecipeCategory.Anomalies
        };

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
        private string _selectedResearchNodeId = null;  // For visual tree selection
        private Dictionary<string, Vector2> _researchNodePositions = new Dictionary<string, Vector2>();
        private float _researchTreeScrollX = 0f;  // Horizontal scroll offset
        private float _maxResearchScrollX = 0f;   // Maximum scroll value
        private const int RESEARCH_NODE_WIDTH = 155;
        private const int RESEARCH_NODE_HEIGHT = 58;
        private const int RESEARCH_NODE_SPACING_X = 175;
        private const int RESEARCH_NODE_SPACING_Y = 75;
        private const int RESEARCH_TREE_LEFT = 20;
        private const int RESEARCH_TREE_RIGHT = 850;
        private const int RESEARCH_TREE_TOP = 105;
        private const int RESEARCH_TREE_BOTTOM = 605;
        private ResearchCategory[] _researchCategories;

        // NOTIFICATIONS
        private string _notificationText = "";
        private float _notificationTimer = 0f;
        private const float NOTIFICATION_DURATION = 3f;

        // FLOATING TEXT SYSTEM (damage numbers, heal numbers, etc.)
        private List<FloatingText> _floatingTexts = new List<FloatingText>();

        // HIT FLASH SYSTEM
        private Dictionary<object, float> _hitFlashTimers = new Dictionary<object, float>();
        private const float HIT_FLASH_DURATION = 0.15f;

        // DEBUG CONSOLE (press ` or F12)
        private bool _consoleOpen = false;
        private string _consoleInput = "";
        private List<string> _consoleHistory = new List<string>();
        private List<string> _consoleOutput = new List<string>();
        private int _consoleHistoryIndex = -1;
        private const int MAX_CONSOLE_LINES = 15;
        private KeyboardState _prevConsoleKeyState;

        // BODY PANEL UI (press P)
        private bool _bodyPanelOpen = false;

        // Pause menu
        private bool _pauseMenuOpen = false;
        private int _pauseMenuMode = 0;  // 0 = main, 1 = save slots, 2 = load slots
        private string _selectedBodyPartId = null;
        private int _bodyPanelScroll = 0;

        // GRIP SELECTION DIALOG (for versatile weapons)
        private bool _gripDialogOpen = false;
        private Item _gripDialogItem = null;
        private BodyPart _gripDialogTargetPart = null;
        private int _gripDialogSelection = 0;  // 0 = one-hand, 1 = two-hand

        // DRAG & DROP SYSTEM
        private bool _isDragging = false;
        private Item _draggedItem = null;
        private int _dragSourceIndex = -1;  // Index in inventory
        private Vector2 _dragOffset = Vector2.Zero;
        private Vector2 _dragPosition = Vector2.Zero;
        private BodyPart _hoverBodyPart = null;  // Body part mouse is over

        // WORLD MAP UI (press N)
        private bool _worldMapOpen = false;
        private string _hoveredZoneId = null;

        // WORLD EVENT NOTIFICATIONS
        private List<EventNotification> _eventNotifications = new List<EventNotification>();
        private const float EVENT_NOTIFICATION_DURATION = 5f;

        private struct EventNotification
        {
            public string Text;
            public float TimeRemaining;
            public Color Color;
        }

        // MINIMAP (always visible, toggle size with -)
        private bool _minimapExpanded = false;  // false = small corner, true = larger overlay
        private bool _showKeybinds = false;     // Toggle keybind hints with ? key
        private const int MINIMAP_SIZE_SMALL = 150;
        private const int MINIMAP_SIZE_LARGE = 300;

        // Zoom threshold for showing entity names (hide when zoomed out for cleaner look)
        private const float MIN_ZOOM_FOR_NAMES = 0.7f;

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

            // Initialize Fog of War for this zone
            GameServices.FogOfWar.Initialize(_world);

            // Create player entity (but don't initialize stats yet - wait for science path choice)
            _player = new PlayerEntity();
            _player = new PlayerEntity();
            _player.Position = new Vector2(5 * _world.TileSize, 5 * _world.TileSize);  // Safe spawn point

            // Generate random character build for player to review
            _pendingBuild = GameServices.Traits.GenerateRandomBuild();
            _selectedSciencePathIndex = 0;

            // Spawn enemies for this zone (pass world for walkability check)
            _enemies = _zoneManager.GenerateZoneEnemies(currentZone, _world.TileSize, _world);

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

            // Subscribe to damage events for floating text
            _combat.OnDamageDealt += (pos, damage, isCrit) => SpawnDamageNumber(pos, damage, isCrit, false);
            _combat.OnMiss += (pos) => SpawnMissText(pos);
            _combat.OnHeal += (pos, amount) => SpawnHealNumber(pos, amount);
            _combat.OnStatusApplied += (pos, status) => SpawnStatusText(pos, status);

            // Subscribe to enemy events (static events)
            EnemyEntity.OnDamageDealtToPlayer += (pos, damage, isCrit) => SpawnDamageNumber(pos, damage, isCrit, true);
            EnemyEntity.OnMissPlayer += (pos) => SpawnMissText(pos);
            // Note: OnEnemyTakeDamage removed - player attacks already handled by _combat.OnDamageDealt
            // OnEnemyTakeDamage could be used for poison/fire/trap damage later

            // Initialize World Event System
            _worldEvents = new WorldEventSystem();
            _worldEvents.OnEventStarted += HandleWorldEventStarted;
            _worldEvents.OnEventEnded += HandleWorldEventEnded;
            _worldEvents.OnEventNotification += (text) => AddEventNotification(text, GetEventNotificationColor(text));

            // Subscribe to Faction System messages
            GameServices.Factions.OnFactionMessage += (text) => AddEventNotification(text, GetFactionNotificationColor(text));
            GameServices.Factions.OnReputationChanged += HandleReputationChanged;

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

            RestoreGameFromSave(saveData);
        }

        private void LoadFromSlot(int slot)
        {
            if (!SaveSystem.SlotExists(slot))
            {
                ShowNotification("Slot is empty!");
                return;
            }

            var saveData = SaveSystem.LoadFromSlot(slot);
            if (saveData == null)
            {
                ShowNotification("Load Failed!");
                return;
            }

            RestoreGameFromSave(saveData);
            ShowNotification($"Loaded Slot {slot + 1}!");
        }

        private void RestoreGameFromSave(GameSaveData saveData)
        {
            try
            {
                // Restore time
                SaveSystem.RestoreTime(GameServices.SurvivalSystem, saveData.Time);

                // Restore player position first (before zone handling)
                SaveSystem.RestorePlayer(_player, saveData.Player, GameServices.Mutations);

                // Handle zone - transition to saved zone if different
                string savedZoneId = saveData.World?.CurrentZoneId ?? "rusthollow";
                var currentZone = _zoneManager.CurrentZone;

                if (currentZone == null || currentZone.Id != savedZoneId)
                {
                    // Need to transition to the saved zone
                    var targetZone = _zoneManager.GetZone(savedZoneId);
                    if (targetZone != null)
                    {
                        _zoneManager.SetCurrentZone(savedZoneId);
                        
                        // Resize world if needed
                        if (_world.Width != targetZone.Width || _world.Height != targetZone.Height)
                        {
                            _world = new WorldGrid(targetZone.Width, targetZone.Height, GraphicsDevice);
                        }
                        
                        _zoneManager.GenerateZoneWorld(_world, targetZone);
                        System.Diagnostics.Debug.WriteLine($">>> Restored zone: {savedZoneId} <<<");
                    }
                    else
                    {
                        // Fallback to rusthollow if zone not found
                        var fallbackZone = _zoneManager.GetZone("rusthollow");
                        if (fallbackZone != null)
                        {
                            _zoneManager.SetCurrentZone("rusthollow");
                            _zoneManager.GenerateZoneWorld(_world, fallbackZone);
                        }
                    }
                }

                // Initialize Fog of War for this zone BEFORE restoring exploration
                GameServices.FogOfWar.Initialize(_world);

                // Restore structures
                SaveSystem.RestoreStructures(GameServices.Building, _world, saveData.World?.Structures);

                // Restore enemies
                _enemies = SaveSystem.RestoreEnemies(saveData.Enemies);
                _combat.UpdateEnemyList(_enemies);

                // Restore ground items
                _groundItems = SaveSystem.RestoreGroundItems(saveData.GroundItems);

                // Restore quests
                SaveSystem.RestoreQuests(saveData.Quests);

                // NEW: Restore faction reputations
                if (saveData.Factions != null)
                {
                    SaveSystem.RestoreFactions(saveData.Factions);
                }

                // NEW: Restore research progress
                if (saveData.Research != null)
                {
                    SaveSystem.RestoreResearch(saveData.Research, _player.Stats.SciencePath);
                }

                // NEW: Restore fog of war exploration
                if (saveData.FogOfWar != null)
                {
                    SaveSystem.RestoreFogOfWar(saveData.FogOfWar, savedZoneId, _world.Width, _world.Height);
                }

                // NEW: Restore NPCs
                if (saveData.NPCs != null && saveData.NPCs.Count > 0)
                {
                    _npcs = SaveSystem.RestoreNPCs(saveData.NPCs, savedZoneId);
                }
                else
                {
                    // Spawn default NPCs for zone if none saved
                    SpawnNPCsForZone(_zoneManager.CurrentZone);
                }

                // Reset camera to player position
                _camera.Position = _player.Position;

                // Reset UI state
                _inventoryOpen = false;
                _buildMenuOpen = false;
                _pauseMenuOpen = false;
                _pauseMenuMode = 0;
                _selectedEnemy = null;
                _gameState = GameState.Playing;

                // Exit combat if in combat
                if (_combat.InCombat)
                {
                    _combat.EndCombat();
                }

                ShowNotification("Game Loaded!");
                System.Diagnostics.Debug.WriteLine($">>> GAME LOADED! Save from: {saveData.SaveTime}, Zone: {savedZoneId} <<<");
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
        /// Note: _inspectPanelOpen is NOT included - it's a tooltip, not a full menu
        /// </summary>
        private bool AnyMenuOpen => _inventoryOpen || _craftingOpen || _tradingOpen ||
                                    _questLogOpen || _researchOpen || _buildMenuOpen ||
                                    _questDialogueOpen || _bodyPanelOpen || _pauseMenuOpen ||
                                    _worldMapOpen;

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

                // Title text centered (use integer position)
                Vector2 titleSize = _font.MeasureString(title);
                int titleX = bounds.X + (int)((bounds.Width - titleSize.X) / 2);
                _spriteBatch.DrawString(_font, title, new Vector2(titleX, bounds.Y + 7), UITheme.TextHighlight);

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

            // Text centered (use integer positions to avoid sub-pixel artifacts)
            Vector2 textSize = _font.MeasureString(text);
            int textX = bounds.X + (int)((bounds.Width - textSize.X) / 2);
            int textY = bounds.Y + (int)((bounds.Height - textSize.Y) / 2);
            Color textColor = enabled ? (isHovered ? UITheme.TextHighlight : UITheme.TextPrimary) : UITheme.TextSecondary;
            _spriteBatch.DrawString(_font, text, new Vector2(textX, textY), textColor);
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

            // Text (use integer position)
            Color textColor = isSelected ? UITheme.TextHighlight : (isHovered ? UITheme.TextPrimary : UITheme.TextSecondary);
            int textY = bounds.Y + (bounds.Height - 16) / 2;
            _spriteBatch.DrawString(_font, text, new Vector2(bounds.X + textOffset, textY), textColor);

            return isClicked;
        }

        /// <summary>
        /// Draw help text at bottom of screen (for menu overlays)
        /// </summary>
        private void DrawHelpBar(string text)
        {
            Rectangle helpRect = new Rectangle(0, 695, 1280, 25);
            _spriteBatch.Draw(_pixelTexture, helpRect, new Color(20, 25, 35) * 0.95f);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 695, 1280, 1), new Color(60, 70, 90));

            Vector2 textSize = _font.MeasureString(text);
            int textX = (int)(640 - textSize.X / 2);
            _spriteBatch.DrawString(_font, text, new Vector2(textX, 700), UITheme.TextSecondary);
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
                    // Settlement has multiple merchants - use fixed positions but validate walkability
                    var traderPos = FindWalkableNPCSpawn(zone.Width / 2, zone.Height / 2 - 3, rand);
                    _npcs.Add(NPCEntity.CreateGeneralMerchant($"trader_{zone.Id}", traderPos));
                    
                    var armsPos = FindWalkableNPCSpawn(zone.Width / 2 + 5, zone.Height / 2, rand);
                    _npcs.Add(NPCEntity.CreateWeaponsMerchant($"arms_{zone.Id}", armsPos));
                    break;

                case ZoneType.Ruins:
                    // Scavenger in ruins - random but walkable
                    var scavPos = FindWalkableNPCSpawnRandom(10, zone.Width - 10, 10, zone.Height - 10, rand);
                    _npcs.Add(NPCEntity.CreateWanderer($"scav_{zone.Id}", scavPos));
                    break;

                default:
                    // Generic trader - random but walkable
                    var genericPos = FindWalkableNPCSpawnRandom(8, zone.Width - 8, 8, zone.Height - 8, rand);
                    _npcs.Add(NPCEntity.CreateGeneralMerchant($"trader_{zone.Id}", genericPos));
                    break;
            }

            System.Diagnostics.Debug.WriteLine($">>> Spawned {_npcs.Count} NPCs in {zone.Name} <<<");
        }

        /// <summary>
        /// Find a walkable spawn position near a target tile, searching outward if needed
        /// </summary>
        private Vector2 FindWalkableNPCSpawn(int targetTileX, int targetTileY, Random rand)
        {
            // Try the target position first
            if (IsTileWalkable(targetTileX, targetTileY))
            {
                return new Vector2(targetTileX * _world.TileSize, targetTileY * _world.TileSize);
            }

            // Spiral outward to find walkable tile
            for (int radius = 1; radius < 15; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int x = targetTileX + dx;
                        int y = targetTileY + dy;
                        if (x >= 2 && x < _world.Width - 2 && y >= 2 && y < _world.Height - 2)
                        {
                            if (IsTileWalkable(x, y))
                            {
                                return new Vector2(x * _world.TileSize, y * _world.TileSize);
                            }
                        }
                    }
                }
            }

            // Last resort - return center of map
            return new Vector2(_world.Width / 2 * _world.TileSize, _world.Height / 2 * _world.TileSize);
        }

        /// <summary>
        /// Find a random walkable spawn position within given bounds
        /// </summary>
        private Vector2 FindWalkableNPCSpawnRandom(int minX, int maxX, int minY, int maxY, Random rand)
        {
            // Try random positions with walkability check
            for (int attempt = 0; attempt < 100; attempt++)
            {
                int x = rand.Next(minX, maxX);
                int y = rand.Next(minY, maxY);
                if (IsTileWalkable(x, y))
                {
                    return new Vector2(x * _world.TileSize, y * _world.TileSize);
                }
            }

            // Fallback - spiral search from center of bounds
            int cx = (minX + maxX) / 2;
            int cy = (minY + maxY) / 2;
            return FindWalkableNPCSpawn(cx, cy, rand);
        }

        /// <summary>
        /// Check if a tile is walkable (not a wall, water, etc.)
        /// </summary>
        private bool IsTileWalkable(int tileX, int tileY)
        {
            if (tileX < 0 || tileX >= _world.Width || tileY < 0 || tileY >= _world.Height)
                return false;
            
            var tile = _world.GetTile(tileX, tileY);
            return tile.IsWalkable;
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

            // Escape closes UI or opens pause menu
            if (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                // Close console first
                if (_consoleOpen)
                {
                    _consoleOpen = false;
                    _consoleInput = "";
                }
                // Cancel drag first
                else if (_isDragging)
                {
                    _isDragging = false;
                    _draggedItem = null;
                    _dragSourceIndex = -1;
                }
                // Close pause menu if open (or go back to main menu)
                else if (_pauseMenuOpen)
                {
                    if (_pauseMenuMode != 0)
                    {
                        _pauseMenuMode = 0;  // Go back to main menu
                    }
                    else
                    {
                        _pauseMenuOpen = false;
                    }
                }
                // Close UI overlays first (in order of priority)
                else if (_questDialogueOpen) { _questDialogueOpen = false; }
                else if (_questLogOpen) { _questLogOpen = false; }
                else if (_researchOpen) { _researchOpen = false; }
                else if (_bodyPanelOpen) { _bodyPanelOpen = false; }
                else if (_tradingOpen) { _tradingOpen = false; _tradingNPC = null; }
                else if (_craftingOpen) { _craftingOpen = false; }
                else if (_worldMapOpen) { _worldMapOpen = false; }
                else if (_inventoryOpen) { _inventoryOpen = false; }
                else if (_buildMenuOpen) { _buildMenuOpen = false; }
                else if (GameServices.Building.InBuildMode) { GameServices.Building.ExitBuildMode(); }
                // Open pause menu if nothing else is open
                else
                {
                    _pauseMenuOpen = true;
                    _player.CurrentPath = null;  // Stop movement
                }
            }

            // Toggle debug console with ` (tilde) or F12
            if ((kState.IsKeyDown(Keys.OemTilde) && _prevKeyboardState.IsKeyUp(Keys.OemTilde)) ||
                (kState.IsKeyDown(Keys.F12) && _prevKeyboardState.IsKeyUp(Keys.F12)))
            {
                _consoleOpen = !_consoleOpen;
                _consoleInput = "";
                _consoleHistoryIndex = -1;
            }

            // If console is open, handle console input and skip game input
            if (_consoleOpen)
            {
                UpdateConsole(kState);
                _prevKeyboardState = kState;
                _prevMouseState = mState;
                _prevConsoleKeyState = kState;
                base.Update(gameTime);
                return;
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

            // Update visual effects
            UpdateFloatingTexts(deltaTime);
            UpdateHitFlashes(deltaTime);

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

            // SAFETY: Clear player path when any menu is open to prevent continued movement
            if (AnyMenuOpen && _player.CurrentPath != null && _player.CurrentPath.Count > 0)
            {
                _player.CurrentPath = null;
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

            // N key - Toggle world map
            if (kState.IsKeyDown(Keys.N) && _prevKeyboardState.IsKeyUp(Keys.N))
            {
                _worldMapOpen = !_worldMapOpen;
                _hoveredZoneId = null;
                if (_worldMapOpen)
                {
                    _player.CurrentPath = null;
                    System.Diagnostics.Debug.WriteLine($">>> World Map OPENED <<<");
                    // Return to prevent UpdateWorldMapUI from closing it in the same frame
                    _prevKeyboardState = kState;
                    _prevMouseState = mState;
                    return;
                }
                System.Diagnostics.Debug.WriteLine($">>> World Map CLOSED <<<");
            }

            // R key - Toggle research menu
            if (kState.IsKeyDown(Keys.R) && _prevKeyboardState.IsKeyUp(Keys.R))
            {
                _researchOpen = !_researchOpen;
                _selectedResearchIndex = 0;
                _selectedResearchNodeId = null;  // Clear node selection
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

            // Minus key - Toggle minimap size
            if (kState.IsKeyDown(Keys.OemMinus) && _prevKeyboardState.IsKeyUp(Keys.OemMinus))
            {
                _minimapExpanded = !_minimapExpanded;
                System.Diagnostics.Debug.WriteLine($">>> Minimap {(_minimapExpanded ? "EXPANDED" : "SMALL")} <<<");
            }

            // Question mark / Slash key - Toggle keybind hints
            if (kState.IsKeyDown(Keys.OemQuestion) && _prevKeyboardState.IsKeyUp(Keys.OemQuestion))
            {
                _showKeybinds = !_showKeybinds;
            }

            // Update nearest item for pickup highlight
            UpdateNearestItem();

            // Debug keys (always available)
            HandleDebugKeys(kState);

            // Camera controls (only when NO menu is open)
            if (!AnyMenuOpen && !GameServices.Building.InBuildMode)
            {
                float camSpeed = 500f * deltaTime;
                if (kState.IsKeyDown(Keys.W)) _camera.Position.Y -= camSpeed;
                if (kState.IsKeyDown(Keys.S)) _camera.Position.Y += camSpeed;
                if (kState.IsKeyDown(Keys.A)) _camera.Position.X -= camSpeed;
                if (kState.IsKeyDown(Keys.D)) _camera.Position.X += camSpeed;
                if (kState.IsKeyDown(Keys.Q)) _camera.Zoom -= 1f * deltaTime;
                if (kState.IsKeyDown(Keys.E)) _camera.Zoom += 1f * deltaTime;
            }

            // Pause menu UI handling (takes priority over everything)
            if (_pauseMenuOpen)
            {
                UpdatePauseMenuUI(mState);
                _prevMouseState = mState;  // Must update mouse state!
                return; // Don't process other actions while pause menu is open
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

            // World Map UI handling
            if (_worldMapOpen)
            {
                UpdateWorldMapUI(kState, mState);
                return; // Don't process other actions while world map is open
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

            // Update World Events System
            float gameTimeHours = GameServices.SurvivalSystem.GameDay * 24f + GameServices.SurvivalSystem.GameHour;
            _worldEvents.Update(gameTimeHours, _zoneManager.CurrentZone);
            UpdateEventNotifications(deltaTime);

            // Update Faction System
            GameServices.Factions.Update(GameServices.SurvivalSystem.GameDay);

            // Update Fog of War visibility
            float sightRange = _player.Stats?.SightRange ?? 9f;
            GameServices.FogOfWar.Update(_player.Position, sightRange, _world.TileSize);

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

            // Close inspect panel with left-click or Escape (handle BEFORE AnyMenuOpen check)
            if (_inspectPanelOpen)
            {
                if ((mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released) ||
                    (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape)))
                {
                    _inspectPanelOpen = false;
                    _inspectedObject = null;
                    return; // Don't process other clicks this frame
                }
            }

            // Player click-to-move (only when no menu is open)
            if (!AnyMenuOpen && mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                Vector2 worldPos = _camera.ScreenToWorld(new Vector2(mState.X, mState.Y));
                Point clickTile = new Point((int)(worldPos.X / _world.TileSize), (int)(worldPos.Y / _world.TileSize));

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

            // Initialize Fog of War for new zone
            // TODO: In future, save/restore exploration data per zone
            GameServices.FogOfWar.Initialize(_world);

            // Move player to entry point
            _player.Position = new Vector2(entryPoint.X * _world.TileSize, entryPoint.Y * _world.TileSize);
            _player.CurrentPath = null;

            // Spawn/restore enemies for zone (pass world for walkability check)
            _enemies = _zoneManager.GenerateZoneEnemies(targetZone, _world.TileSize, _world);
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
            _activeWorkstationType = workstation?.Type;
            _craftingOpen = true;
            _selectedRecipeIndex = 0;
            _craftingCategoryIndex = 0;
            _craftingScrollOffset = 0;
            _craftQuantity = 1;
            _craftingFeedbackTimer = 0f;

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
                        ShowNotification("AP → MP: +1 movement!");
                    }
                }

                // F key - Melee attack (bash/pistol whip with ranged weapon)
                if (kState.IsKeyDown(Keys.F) && _prevKeyboardState.IsKeyUp(Keys.F))
                {
                    if (_selectedEnemy != null && _selectedEnemy.IsAlive)
                    {
                        Point playerTile = new Point(
                            (int)(_player.Position.X / _world.TileSize),
                            (int)(_player.Position.Y / _world.TileSize)
                        );
                        Point enemyTile = _selectedEnemy.GetTilePosition(_world.TileSize);
                        int distance = Pathfinder.GetDistance(playerTile, enemyTile);

                        if (distance <= 1)
                        {
                            _combat.PlayerMeleeAttack(_selectedEnemy, _world);
                            if (!_selectedEnemy.IsAlive) _selectedEnemy = null;
                        }
                        else
                        {
                            ShowNotification("Too far for melee attack!");
                        }
                    }
                    else
                    {
                        ShowNotification("No target selected! (Tab to cycle targets)");
                    }
                }

                // Attack selected target
                if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
                {
                    if (_selectedEnemy != null && _selectedEnemy.IsAlive)
                    {
                        // NEW: Check line of sight for ranged attacks
                        int attackRange = _player.Stats.GetAttackRange();
                        if (attackRange > 1)
                        {
                            Point playerTile = new Point(
                                (int)(_player.Position.X / _world.TileSize),
                                (int)(_player.Position.Y / _world.TileSize)
                            );
                            Point enemyTile = _selectedEnemy.GetTilePosition(_world.TileSize);
                            
                            if (!_world.HasLineOfSight(playerTile, enemyTile))
                            {
                                ShowNotification("Cannot hit target - no line of sight!");
                                return;
                            }
                        }
                        
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

                        // Check if enemy is within attack range (melee OR ranged)
                        int attackRange = _player.Stats.GetAttackRange();
                        int distance = Pathfinder.GetDistance(playerTile, clickTile);

                        if (distance <= attackRange)
                        {
                            // NEW: Check line of sight for ranged attacks
                            if (attackRange > 1 && !_world.HasLineOfSight(playerTile, clickTile))
                            {
                                ShowNotification("Cannot hit target - no line of sight!");
                                _selectedEnemy = clickedEnemy;
                            }
                            else
                            {
                                _combat.PlayerAttack(clickedEnemy, _world);
                                if (!clickedEnemy.IsAlive) _selectedEnemy = null;
                            }
                        }
                        else
                        {
                            _selectedEnemy = clickedEnemy;
                            ShowNotification($"Target too far! (Range: {attackRange}, Distance: {distance})");
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

                // Track mouse hover for tooltip (BG3 style)
                {
                    Vector2 worldPos = _camera.ScreenToWorld(new Vector2(mState.X, mState.Y));
                    Point hoverTile = new Point((int)(worldPos.X / _world.TileSize), (int)(worldPos.Y / _world.TileSize));
                    _hoverEnemy = GetEnemyAtTile(hoverTile);
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
            // FOG OF WAR: Check if tile is visible for entities (enemies, NPCs, items)
            bool canSeeEntities = GameServices.FogOfWar.IsVisible(tile);
            bool canSeeTerrain = GameServices.FogOfWar.IsExplored(tile);
            
            // Priority 1: Enemy at tile (only if visible)
            if (canSeeEntities)
            {
                var enemy = GetEnemyAtTile(tile);
                if (enemy != null && enemy.IsAlive) return enemy;
            }

            // Priority 2: NPC at tile (only if visible)
            if (canSeeEntities)
            {
                foreach (var npc in _npcs)
                {
                    Point npcTile = new Point(
                        (int)(npc.Position.X / _world.TileSize),
                        (int)(npc.Position.Y / _world.TileSize)
                    );
                    if (npcTile == tile) return npc;
                }
            }

            // Priority 3: Structure at tile (visible if terrain explored)
            if (canSeeTerrain)
            {
                var structure = GameServices.Building.GetStructureAt(tile);
                if (structure != null) return structure;
            }

            // Priority 4: Ground item at tile (only if visible)
            if (canSeeEntities)
            {
                foreach (var worldItem in _groundItems)
                {
                    Point itemTile = new Point(
                        (int)(worldItem.Position.X / _world.TileSize),
                        (int)(worldItem.Position.Y / _world.TileSize)
                    );
                    if (itemTile == tile) return worldItem;
                }
            }

            // Priority 5: The tile itself (visible if explored)
            if (canSeeTerrain)
            {
                var tileData = _world.GetTile(tile.X, tile.Y);
                if (tileData != null) return tileData;
            }

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

            // Track world event progress
            _worldEvents.OnEnemyKilledInEvent(enemy.Id);

            // Track faction reputation
            GameServices.Factions.OnEnemyKilled(enemy.Type, enemy.IsProvoked);

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
        // PAUSE MENU UI UPDATE
        // ============================================

        private void UpdatePauseMenuUI(MouseState mState)
        {
            var mousePos = new Point(mState.X, mState.Y);

            // Check for click release
            bool wasPressed = _prevMouseState.LeftButton == ButtonState.Pressed;
            bool isReleased = mState.LeftButton == ButtonState.Released;
            bool clicked = wasPressed && isReleased;

            // Menu panel dimensions
            int menuWidth = 350;
            int menuHeight = 400;
            int menuX = (1280 - menuWidth) / 2;
            int menuY = (720 - menuHeight) / 2;

            // Button dimensions
            int buttonWidth = 250;
            int buttonHeight = 40;
            int buttonX = menuX + (menuWidth - buttonWidth) / 2;
            int buttonSpacing = 55;
            int startY = menuY + 70;

            if (_pauseMenuMode == 0)
            {
                // Main menu
                if (!clicked) return;

                Rectangle resumeBtn = new Rectangle(buttonX, startY, buttonWidth, buttonHeight);
                if (resumeBtn.Contains(mousePos))
                {
                    _pauseMenuOpen = false;
                    _pauseMenuMode = 0;
                    return;
                }

                Rectangle saveBtn = new Rectangle(buttonX, startY + buttonSpacing, buttonWidth, buttonHeight);
                if (saveBtn.Contains(mousePos))
                {
                    _pauseMenuMode = 1;  // Switch to save slot selection
                    return;
                }

                Rectangle loadBtn = new Rectangle(buttonX, startY + buttonSpacing * 2, buttonWidth, buttonHeight);
                if (loadBtn.Contains(mousePos))
                {
                    _pauseMenuMode = 2;  // Switch to load slot selection
                    return;
                }

                Rectangle settingsBtn = new Rectangle(buttonX, startY + buttonSpacing * 3, buttonWidth, buttonHeight);
                if (settingsBtn.Contains(mousePos))
                {
                    ShowNotification("Settings coming soon!");
                    return;
                }

                Rectangle quitBtn = new Rectangle(buttonX, startY + buttonSpacing * 4, buttonWidth, buttonHeight);
                if (quitBtn.Contains(mousePos))
                {
                    Exit();
                    return;
                }
            }
            else if (_pauseMenuMode == 1)
            {
                // Save slot selection
                if (!clicked) return;

                // Back button
                Rectangle backBtn = new Rectangle(buttonX, startY + buttonSpacing * 4, buttonWidth, buttonHeight);
                if (backBtn.Contains(mousePos))
                {
                    _pauseMenuMode = 0;
                    return;
                }

                // Slot buttons
                for (int i = 0; i < SaveSystem.MAX_SLOTS; i++)
                {
                    Rectangle slotBtn = new Rectangle(buttonX, startY + buttonSpacing * i, buttonWidth, buttonHeight);
                    if (slotBtn.Contains(mousePos))
                    {
                        bool success = SaveSystem.SaveToSlot(
                            i,
                            _player,
                            _enemies,
                            _groundItems,
                            _world,
                            GameServices.Building,
                            GameServices.SurvivalSystem,
                            _npcs,  // NEW: Save NPCs
                            _zoneManager.CurrentZone?.Id ?? "rusthollow"
                        );
                        ShowNotification(success ? $"Saved to Slot {i + 1}!" : "Save Failed!");
                        _pauseMenuMode = 0;
                        return;
                    }
                }
            }
            else if (_pauseMenuMode == 2)
            {
                // Load slot selection
                if (!clicked) return;

                // Back button
                Rectangle backBtn = new Rectangle(buttonX, startY + buttonSpacing * 4, buttonWidth, buttonHeight);
                if (backBtn.Contains(mousePos))
                {
                    _pauseMenuMode = 0;
                    return;
                }

                // Slot buttons
                for (int i = 0; i < SaveSystem.MAX_SLOTS; i++)
                {
                    Rectangle slotBtn = new Rectangle(buttonX, startY + buttonSpacing * i, buttonWidth, buttonHeight);
                    if (slotBtn.Contains(mousePos))
                    {
                        if (SaveSystem.SlotExists(i))
                        {
                            LoadFromSlot(i);
                            _pauseMenuOpen = false;
                            _pauseMenuMode = 0;
                        }
                        else
                        {
                            ShowNotification("Slot is empty!");
                        }
                        return;
                    }
                }
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
                // Check if it's a medical item
                if (item.Definition?.IsMedical == true)
                {
                    bool usedItem = false;
                    string message = "";

                    // Percentage-based healing (heals overall HP, distributes to all body parts)
                    if (item.Definition.HealPercent > 0)
                    {
                        float oldHP = _player.Stats.CurrentHealth;
                        _player.Stats.HealByPercent(item.Definition.HealPercent);
                        float actualHealed = _player.Stats.CurrentHealth - oldHP;
                        if (actualHealed > 0)
                        {
                            usedItem = true;
                            message = $"+{actualHealed:F0} HP";
                            SpawnHealNumber(_player.Position + new Vector2(16, 0), actualHealed);
                        }
                    }

                    // Handle bleeding/infection/fracture on most critical part
                    var targetPart = _player.Stats.Body.GetMostCriticalPart();
                    if (targetPart != null)
                    {
                        if (item.Definition.CanHealBleeding && targetPart.IsBleeding)
                        {
                            foreach (var injury in targetPart.Injuries)
                            {
                                injury.BleedRate = 0;
                            }
                            usedItem = true;
                            message += (message.Length > 0 ? ", " : "") + "stopped bleeding";
                        }

                        if (item.Definition.CanHealInfection && targetPart.IsInfected)
                        {
                            targetPart.RemoveAilment(BodyPartAilment.Infected);
                            usedItem = true;
                            message += (message.Length > 0 ? ", " : "") + "cured infection";
                        }

                        if (item.Definition.CanHealFracture && targetPart.HasFracture)
                        {
                            var fracture = targetPart.Injuries.FirstOrDefault(i => i.Type == InjuryType.Fracture);
                            if (fracture != null)
                            {
                                fracture.HealProgress += 0.5f;
                            }
                            usedItem = true;
                            message += (message.Length > 0 ? ", " : "") + "treated fracture";
                        }
                    }

                    if (usedItem)
                    {
                        _player.Stats.Inventory.RemoveItem(item.ItemDefId, 1);
                        _player.Stats.SyncHPWithBody();
                        ShowNotification($"Used {item.Name}: {message}");
                    }
                    else
                    {
                        ShowNotification($"No injuries to treat with {item.Name}");
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

                        // Health restore using HealByPercent if HealPercent set, otherwise flat
                        if (item.Definition?.HealPercent > 0)
                        {
                            float oldHP = _player.Stats.CurrentHealth;
                            _player.Stats.HealByPercent(item.Definition.HealPercent);
                            float actualHealed = _player.Stats.CurrentHealth - oldHP;
                            if (actualHealed > 0)
                            {
                                SpawnHealNumber(_player.Position + new Vector2(16, 0), actualHealed);
                                ShowNotification($"Used {item.Name} (+{actualHealed:F0} HP)");
                            }
                            else
                            {
                                ShowNotification($"Used {item.Name}");
                            }
                        }
                        else if (effects.HealthRestore > 0)
                        {
                            _player.Stats.Heal(effects.HealthRestore);
                            ShowNotification($"Used {item.Name} (+{effects.HealthRestore:F0} HP)");
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
            // Update feedback timer
            if (_craftingFeedbackTimer > 0)
            {
                _craftingFeedbackTimer -= 0.016f; // Approximate delta time
            }

            // Get filtered recipes based on category
            var allRecipes = GameServices.Crafting.GetAvailableRecipes(_activeWorkstationType, _player.Stats);
            List<RecipeDefinition> recipes;
            if (_craftingCategoryIndex == 0)
            {
                recipes = allRecipes;
            }
            else
            {
                var selectedCategory = CraftingCategories[_craftingCategoryIndex - 1];
                recipes = allRecipes.Where(r => r.Category == selectedCategory).ToList();
            }

            int maxIndex = recipes.Count - 1;
            int maxVisible = 13;

            // Close crafting
            if ((kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape)) ||
                (kState.IsKeyDown(Keys.C) && _prevKeyboardState.IsKeyUp(Keys.C)))
            {
                _craftingOpen = false;
                _nearestWorkstation = null;
                _activeWorkstationType = null;
                _craftingCategoryIndex = 0;
                _craftingScrollOffset = 0;
                _craftQuantity = 1;
                return;
            }

            // Category switching with A/D
            int totalCategories = 1 + CraftingCategories.Length; // "All" + categories
            if (kState.IsKeyDown(Keys.A) && _prevKeyboardState.IsKeyUp(Keys.A))
            {
                _craftingCategoryIndex = (_craftingCategoryIndex - 1 + totalCategories) % totalCategories;
                _selectedRecipeIndex = 0;
                _craftingScrollOffset = 0;
                _craftQuantity = 1;
            }
            if (kState.IsKeyDown(Keys.D) && _prevKeyboardState.IsKeyUp(Keys.D))
            {
                _craftingCategoryIndex = (_craftingCategoryIndex + 1) % totalCategories;
                _selectedRecipeIndex = 0;
                _craftingScrollOffset = 0;
                _craftQuantity = 1;
            }

            // Recipe navigation with W/S
            if (kState.IsKeyDown(Keys.W) && _prevKeyboardState.IsKeyUp(Keys.W))
            {
                if (_selectedRecipeIndex > 0)
                {
                    _selectedRecipeIndex--;
                    // Scroll up if needed
                    if (_selectedRecipeIndex < _craftingScrollOffset)
                    {
                        _craftingScrollOffset = _selectedRecipeIndex;
                    }
                }
            }
            if (kState.IsKeyDown(Keys.S) && _prevKeyboardState.IsKeyUp(Keys.S))
            {
                if (_selectedRecipeIndex < maxIndex)
                {
                    _selectedRecipeIndex++;
                    // Scroll down if needed
                    if (_selectedRecipeIndex >= _craftingScrollOffset + maxVisible)
                    {
                        _craftingScrollOffset = _selectedRecipeIndex - maxVisible + 1;
                    }
                }
            }

            // Mouse scroll for recipe list
            int scrollDelta = mState.ScrollWheelValue - _prevMouseState.ScrollWheelValue;
            if (scrollDelta != 0 && mState.X < 470) // Only if mouse is over recipe list
            {
                int scrollAmount = scrollDelta > 0 ? -1 : 1;
                int maxScroll = Math.Max(0, recipes.Count - maxVisible);
                _craftingScrollOffset = Math.Clamp(_craftingScrollOffset + scrollAmount, 0, maxScroll);
            }

            // Get max craftable for current recipe
            int maxCraftable = 0;
            if (_selectedRecipeIndex >= 0 && _selectedRecipeIndex < recipes.Count)
            {
                maxCraftable = GetMaxCraftableAmount(recipes[_selectedRecipeIndex]);
            }

            // Clamp quantity to what's actually craftable
            if (maxCraftable == 0)
            {
                _craftQuantity = 1; // Reset to 1 when can't craft
            }
            else
            {
                _craftQuantity = Math.Clamp(_craftQuantity, 1, Math.Min(99, maxCraftable));
            }

            // Quantity adjustment with Q/E (Shift for x5)
            bool shiftHeld = kState.IsKeyDown(Keys.LeftShift) || kState.IsKeyDown(Keys.RightShift);
            int quantityStep = shiftHeld ? 5 : 1;

            if (kState.IsKeyDown(Keys.Q) && _prevKeyboardState.IsKeyUp(Keys.Q))
            {
                _craftQuantity = Math.Max(1, _craftQuantity - quantityStep);
            }
            if (kState.IsKeyDown(Keys.E) && _prevKeyboardState.IsKeyUp(Keys.E))
            {
                if (maxCraftable > 0)
                {
                    _craftQuantity = Math.Min(Math.Min(99, maxCraftable), _craftQuantity + quantityStep);
                }
            }

            // Craft with Enter
            if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
            {
                CraftSelectedRecipe();
            }

            // Handle craft button click (position based on new UI layout)
            if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                // Craft button area (approximate)
                Rectangle craftBtn = new Rectangle(895, 505, 200, 40);
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
        // RESEARCH UI - Input Handling
        // ============================================

        private void UpdateResearchUI(KeyboardState kState, MouseState mState)
        {
            if (_researchCategories == null || _researchCategories.Length == 0) return;

            var currentCategory = _researchCategories[_selectedResearchCategory];
            var nodes = GameServices.Research.GetNodesByCategory(currentCategory);
            bool leftClick = mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released;

            // Close with Escape only (R toggle is handled in UpdatePlaying)
            if (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                _researchOpen = false;
                _selectedResearchNodeId = null;
                _researchTreeScrollX = 0f;
                return;
            }

            // Tab to switch categories
            if (kState.IsKeyDown(Keys.Tab) && _prevKeyboardState.IsKeyUp(Keys.Tab))
            {
                _selectedResearchCategory = (_selectedResearchCategory + 1) % _researchCategories.Length;
                _selectedResearchNodeId = null;  // Clear selection when switching categories
                _researchTreeScrollX = 0f;       // Reset scroll when switching categories
            }

            // Mouse wheel for horizontal scrolling (when mouse is in tree area)
            int scrollDelta = mState.ScrollWheelValue - _prevMouseState.ScrollWheelValue;
            if (scrollDelta != 0 && mState.X >= RESEARCH_TREE_LEFT && mState.X <= RESEARCH_TREE_RIGHT &&
                mState.Y >= RESEARCH_TREE_TOP && mState.Y <= RESEARCH_TREE_BOTTOM)
            {
                float scrollSpeed = 40f;
                _researchTreeScrollX -= scrollDelta / 120f * scrollSpeed;
                _researchTreeScrollX = MathHelper.Clamp(_researchTreeScrollX, 0, _maxResearchScrollX);
            }

            // A/D keys for horizontal scroll as alternative
            if (kState.IsKeyDown(Keys.A))
            {
                _researchTreeScrollX = MathHelper.Clamp(_researchTreeScrollX - 5f, 0, _maxResearchScrollX);
            }
            if (kState.IsKeyDown(Keys.D))
            {
                _researchTreeScrollX = MathHelper.Clamp(_researchTreeScrollX + 5f, 0, _maxResearchScrollX);
            }

            // Mouse click on nodes (in tree area)
            if (leftClick)
            {
                // Check if clicking on category tabs first
                int tabX = 20;
                int tabY = 65;
                for (int i = 0; i < _researchCategories.Length; i++)
                {
                    Rectangle tabRect = new Rectangle(tabX, tabY, 130, 28);
                    if (tabRect.Contains(mState.X, mState.Y))
                    {
                        _selectedResearchCategory = i;
                        _selectedResearchNodeId = null;
                        _researchTreeScrollX = 0f;  // Reset scroll
                        break;
                    }
                    tabX += 135;
                }

                // Check if clicking on a research node (apply scroll offset to hit test)
                foreach (var node in nodes)
                {
                    if (_researchNodePositions.ContainsKey(node.Id))
                    {
                        Vector2 pos = _researchNodePositions[node.Id];
                        // Apply scroll offset for hit testing
                        int drawX = (int)(pos.X - _researchTreeScrollX);
                        Rectangle nodeRect = new Rectangle(drawX, (int)pos.Y, RESEARCH_NODE_WIDTH, RESEARCH_NODE_HEIGHT);

                        // Only clickable if visible in tree area
                        if (nodeRect.Contains(mState.X, mState.Y) &&
                            drawX + RESEARCH_NODE_WIDTH > RESEARCH_TREE_LEFT &&
                            drawX < RESEARCH_TREE_RIGHT)
                        {
                            _selectedResearchNodeId = node.Id;
                            break;
                        }
                    }
                }
            }

            // Double-click or Enter to start research
            bool enterPressed = kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter);

            if (enterPressed && !string.IsNullOrEmpty(_selectedResearchNodeId))
            {
                var node = GameServices.Research.GetNode(_selectedResearchNodeId);
                if (node != null)
                {
                    TryStartResearch(node);
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

            // Arrow keys for node navigation (alternative to mouse)
            if (kState.IsKeyDown(Keys.Left) && _prevKeyboardState.IsKeyUp(Keys.Left))
            {
                NavigateResearchTree(-1, 0, nodes);
            }
            if (kState.IsKeyDown(Keys.Right) && _prevKeyboardState.IsKeyUp(Keys.Right))
            {
                NavigateResearchTree(1, 0, nodes);
            }
            if (kState.IsKeyDown(Keys.Up) && _prevKeyboardState.IsKeyUp(Keys.Up))
            {
                NavigateResearchTree(0, -1, nodes);
            }
            if (kState.IsKeyDown(Keys.Down) && _prevKeyboardState.IsKeyUp(Keys.Down))
            {
                NavigateResearchTree(0, 1, nodes);
            }

            // Update research availability
            GameServices.Research.UpdateAvailability(_player.Stats.Level);
        }

        /// <summary>
        /// Try to start research on a node, checking requirements
        /// </summary>
        private void TryStartResearch(ResearchNode node)
        {
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
                // Give specific feedback on why it's locked
                if (node.RequiredLevel > _player.Stats.Level)
                {
                    ShowNotification($"Requires Level {node.RequiredLevel}!");
                }
                else
                {
                    ShowNotification("Complete prerequisites first!");
                }
            }
            else if (node.State == ResearchState.Completed)
            {
                ShowNotification("Already researched!");
            }
        }

        /// <summary>
        /// Navigate the research tree using arrow keys
        /// </summary>
        private void NavigateResearchTree(int dx, int dy, List<ResearchNode> nodes)
        {
            if (nodes.Count == 0) return;

            // If nothing selected, select first available
            if (string.IsNullOrEmpty(_selectedResearchNodeId))
            {
                _selectedResearchNodeId = nodes[0].Id;
                return;
            }

            // Find current position
            if (!_researchNodePositions.ContainsKey(_selectedResearchNodeId)) return;
            Vector2 currentPos = _researchNodePositions[_selectedResearchNodeId];

            // Find nearest node in the requested direction
            string bestNodeId = null;
            float bestDistance = float.MaxValue;

            foreach (var node in nodes)
            {
                if (node.Id == _selectedResearchNodeId) continue;
                if (!_researchNodePositions.ContainsKey(node.Id)) continue;

                Vector2 nodePos = _researchNodePositions[node.Id];
                float diffX = nodePos.X - currentPos.X;
                float diffY = nodePos.Y - currentPos.Y;

                // Check if node is in the requested direction
                bool validDirection = false;
                if (dx > 0 && diffX > 20) validDirection = true;      // Right
                else if (dx < 0 && diffX < -20) validDirection = true; // Left
                else if (dy > 0 && diffY > 20) validDirection = true;  // Down
                else if (dy < 0 && diffY < -20) validDirection = true; // Up

                if (validDirection)
                {
                    float distance = Vector2.Distance(currentPos, nodePos);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestNodeId = node.Id;
                    }
                }
            }

            if (bestNodeId != null)
            {
                _selectedResearchNodeId = bestNodeId;

                // Auto-scroll to keep selected node visible
                if (_researchNodePositions.ContainsKey(bestNodeId))
                {
                    Vector2 newPos = _researchNodePositions[bestNodeId];
                    float drawX = newPos.X - _researchTreeScrollX;

                    // Scroll left if node is too far left
                    if (drawX < RESEARCH_TREE_LEFT + 30)
                    {
                        _researchTreeScrollX = Math.Max(0, newPos.X - RESEARCH_TREE_LEFT - 50);
                    }
                    // Scroll right if node is too far right
                    else if (drawX + RESEARCH_NODE_WIDTH > RESEARCH_TREE_RIGHT - 30)
                    {
                        _researchTreeScrollX = Math.Min(_maxResearchScrollX,
                            newPos.X + RESEARCH_NODE_WIDTH - RESEARCH_TREE_RIGHT + 50);
                    }
                }
            }
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

            // GRIP SELECTION DIALOG - intercepts all input when open
            if (_gripDialogOpen)
            {
                UpdateGripSelectionDialog(kState, mState, leftClick);
                return;  // Don't process body panel while dialog is open
            }

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
                            Item item;
                            // Handle two-handed weapons
                            if (part.IsHoldingTwoHandedWeapon)
                            {
                                item = _player.Stats.Body.UnequipWeaponFromHands(part.EquippedItem);
                            }
                            else
                            {
                                item = part.UnequipItem();
                            }

                            if (item != null)
                            {
                                _player.Stats.Inventory.TryAddItem(item.ItemDefId, item.StackCount, item.Quality);
                                ShowNotification($"Unequipped {item.Name} from {part.Name}");
                            }
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

        /// <summary>
        /// Handle World Map UI input
        /// </summary>
        private void UpdateWorldMapUI(KeyboardState kState, MouseState mState)
        {
            // N or Escape to close
            if ((kState.IsKeyDown(Keys.N) && _prevKeyboardState.IsKeyUp(Keys.N)) ||
                (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape)))
            {
                _worldMapOpen = false;
                return;
            }

            // Mouse hover detection for zones is handled in DrawWorldMap
            // Left click could be used for fast travel in the future
        }

        private bool TryApplyItemToBodyPart(Item item, BodyPart part)
        {
            if (item == null || part == null) return false;

            // Medical items
            if (item.Definition != null && item.Definition.IsMedical)
            {
                bool usedItem = false;
                float totalHpHealed = 0f;

                // Percentage-based healing (heals overall HP, distributes to all body parts)
                if (item.Definition.HealPercent > 0)
                {
                    float oldHP = _player.Stats.CurrentHealth;
                    _player.Stats.HealByPercent(item.Definition.HealPercent);
                    totalHpHealed = _player.Stats.CurrentHealth - oldHP;
                    if (totalHpHealed > 0)
                    {
                        usedItem = true;
                    }
                }

                // Treat specific conditions on the targeted part
                if (item.Definition.CanHealBleeding && part.IsBleeding)
                {
                    foreach (var injury in part.Injuries)
                    {
                        injury.BleedRate = 0;
                    }
                    usedItem = true;
                }

                if (item.Definition.CanHealInfection && part.IsInfected)
                {
                    part.RemoveAilment(BodyPartAilment.Infected);
                    usedItem = true;
                }

                if (item.Definition.CanHealFracture && part.HasFracture)
                {
                    var fracture = part.Injuries.FirstOrDefault(i => i.Type == InjuryType.Fracture);
                    if (fracture != null)
                    {
                        fracture.HealProgress += 0.5f;
                    }
                    usedItem = true;
                }

                if (usedItem)
                {
                    _player.Stats.SyncHPWithBody();

                    // Spawn floating heal number if HP was restored
                    if (totalHpHealed > 0)
                    {
                        SpawnHealNumber(_player.Position + new Vector2(16, 0), totalHpHealed);
                        ShowNotification($"Used {item.Name} (+{totalHpHealed:F0} HP)");
                    }
                }

                return usedItem;
            }

            // Weapons - equip to hands
            if (item.Category == ItemCategory.Weapon && part.CanEquipWeapon)
            {
                var def = item.Definition;
                if (def == null) return false;

                int handsNeeded = def.HandsRequired;
                bool isVersatile = def.CanUseOneHand && def.CanUseTwoHand && handsNeeded == 1;

                // Check if this is a versatile weapon - show grip selection dialog
                if (isVersatile && def.TwoHandDamageBonus > 0)
                {
                    _gripDialogOpen = true;
                    _gripDialogItem = item;
                    _gripDialogTargetPart = part;
                    _gripDialogSelection = 0;
                    return false;  // Don't equip yet, wait for dialog
                }

                // Check if we have enough hands for two-handed
                if (handsNeeded >= 2 || (def.HandsRequired >= 2 && !def.CanUseOneHand))
                {
                    // Two-handed weapon - use Body's method
                    if (!_player.Stats.Body.CanEquipWeapon(item))
                    {
                        ShowNotification("Need 2 free hands for this weapon!");
                        return false;
                    }

                    // Equip via Body (handles pairing)
                    if (_player.Stats.Body.EquipWeaponToHand(item))
                    {
                        _player.Stats.Inventory.RemoveItem(item.ItemDefId, 1);
                        return true;
                    }
                    return false;
                }
                else
                {
                    // One-handed weapon - equip directly to part
                    // Unequip current weapon if any
                    if (part.EquippedItem != null)
                    {
                        // If it's a 2H weapon, use Body's unequip
                        if (part.IsHoldingTwoHandedWeapon)
                        {
                            var old = _player.Stats.Body.UnequipWeaponFromHands(part.EquippedItem);
                            if (old != null)
                            {
                                _player.Stats.Inventory.TryAddItem(old.ItemDefId, old.StackCount, old.Quality);
                            }
                        }
                        else
                        {
                            var old = part.UnequipItem();
                            _player.Stats.Inventory.TryAddItem(old.ItemDefId, old.StackCount, old.Quality);
                        }
                    }

                    // Equip new weapon
                    part.EquipItem(item);
                    _player.Stats.Inventory.RemoveItem(item.ItemDefId, 1);
                    return true;
                }
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

            // Consumables (food/drink) - use percentage healing if available
            if (item.Category == ItemCategory.Consumable && item.Definition != null)
            {
                if (item.Definition.HealPercent > 0)
                {
                    float oldHP = _player.Stats.CurrentHealth;
                    _player.Stats.HealByPercent(item.Definition.HealPercent);
                    _player.Stats.SyncHPWithBody();
                    float hpHealed = _player.Stats.CurrentHealth - oldHP;

                    // Spawn floating heal number
                    if (hpHealed > 0)
                    {
                        SpawnHealNumber(_player.Position + new Vector2(16, 0), hpHealed);
                        ShowNotification($"Used {item.Name} (+{hpHealed:F0} HP)");
                    }
                    return true;
                }
                else if (item.Definition.HealthRestore > 0 && part.CurrentHealth < part.MaxHealth)
                {
                    float oldHP = _player.Stats.CurrentHealth;
                    part.Heal(item.Definition.HealthRestore);
                    _player.Stats.SyncHPWithBody();
                    float hpHealed = _player.Stats.CurrentHealth - oldHP;

                    if (hpHealed > 0)
                    {
                        SpawnHealNumber(_player.Position + new Vector2(16, 0), hpHealed);
                    }
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

                // F6 alone = damage test (but NOT when Ctrl is held)
                if (!kState.IsKeyDown(Keys.LeftControl) && kState.IsKeyDown(Keys.F6) && _prevKeyboardState.IsKeyUp(Keys.F6))
                    _player.Stats.Body.TakeDamage(25, DamageType.Physical);

                if (kState.IsKeyDown(Keys.F7) && _prevKeyboardState.IsKeyUp(Keys.F7))
                    _player.Initialize();

                // Ctrl+F6 - Give crafting materials for testing
                if (kState.IsKeyDown(Keys.LeftControl) && kState.IsKeyDown(Keys.F6) && _prevKeyboardState.IsKeyUp(Keys.F6))
                {
                    // Add basic crafting materials
                    _player.Stats.Inventory.TryAddItem("wood", 20);
                    _player.Stats.Inventory.TryAddItem("cloth", 15);
                    _player.Stats.Inventory.TryAddItem("scrap_metal", 15);
                    _player.Stats.Inventory.TryAddItem("leather", 10);
                    _player.Stats.Inventory.TryAddItem("bone", 10);
                    _player.Stats.Inventory.TryAddItem("stone", 10);
                    _player.Stats.Inventory.TryAddItem("metal", 10);
                    _player.Stats.Inventory.TryAddItem("sinew", 8);
                    _player.Stats.Inventory.TryAddItem("salt", 5);
                    _player.Stats.Inventory.TryAddItem("herbs", 8);
                    _player.Stats.Inventory.TryAddItem("mutant_meat", 10);
                    _player.Stats.Inventory.TryAddItem("scrap_electronics", 5);
                    _player.Stats.Inventory.TryAddItem("components", 5);
                    _player.Stats.Inventory.TryAddItem("energy_cell", 3);
                    _player.Stats.Inventory.TryAddItem("anomaly_shard", 5);
                    _player.Stats.Inventory.TryAddItem("essence", 5);
                    _player.Stats.Inventory.TryAddItem("junk_bottle", 5);
                    ShowNotification("DEBUG: Added crafting materials!");
                    System.Diagnostics.Debug.WriteLine(">>> DEBUG: Added crafting materials <<<");
                }

                // Ctrl+F7 - Trigger random world event for testing
                if (kState.IsKeyDown(Keys.LeftControl) && kState.IsKeyDown(Keys.F7) && _prevKeyboardState.IsKeyUp(Keys.F7))
                {
                    float gameTimeHours = GameServices.SurvivalSystem.GameDay * 24f + GameServices.SurvivalSystem.GameHour;

                    // Pick a random event type
                    var eventTypes = new[] {
                        WorldEventType.RaiderAttack,
                        WorldEventType.TraderCaravan,
                        WorldEventType.CacheDiscovered,
                        WorldEventType.BeastMigration,
                        WorldEventType.VoidStorm
                    };
                    var randomEvent = eventTypes[_random.Next(eventTypes.Length)];

                    _worldEvents.TriggerEvent(randomEvent, gameTimeHours, _zoneManager.CurrentZone);
                    ShowNotification($"DEBUG: Triggered {randomEvent}!");
                }
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
                    GameServices.SurvivalSystem,
                    _npcs,  // NEW: Save NPCs
                    _zoneManager.CurrentZone?.Id ?? "rusthollow"
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

            // Draw debug console on top of everything
            if (_consoleOpen)
            {
                DrawConsole();
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
            string currentStr = $"Current: {_player.Stats.Attributes.GetDisplayString()}";
            _spriteBatch.DrawString(_font, currentStr,
                new Vector2((int)(640 - _font.MeasureString(currentStr).X / 2), 90), Color.LightGray);

            // Level info
            string levelStr = $"Level {_player.Stats.Level} | Pending Attribute Points: {_player.Stats.PendingAttributePoints}";
            _spriteBatch.DrawString(_font, levelStr,
                new Vector2((int)(640 - _font.MeasureString(levelStr).X / 2), 120), Color.Cyan);

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
            string hintStr = "After choosing, you'll select a mutation!";
            _spriteBatch.DrawString(_font, hintStr,
                new Vector2((int)(640 - _font.MeasureString(hintStr).X / 2), 620), Color.Magenta);

            // Controls
            string ctrlStr = "Click or Press [Enter] to Confirm | [1-6] Quick Select";
            _spriteBatch.DrawString(_font, ctrlStr,
                new Vector2((int)(640 - _font.MeasureString(ctrlStr).X / 2), 680), Color.Gray);

            _spriteBatch.End();
        }

        private void DrawPlaying()
        {
            // --- LAYER 1: WORLD ---
            _spriteBatch.Begin(transformMatrix: _camera.GetViewMatrix(), samplerState: SamplerState.PointClamp);

            _world.Draw(_spriteBatch);

            // Draw Fog of War overlay
            DrawFogOfWar();

            // Draw combat zone indicator (BG3 style)
            if (_combat.InCombat)
            {
                DrawCombatZone();
            }

            // Draw Structures
            DrawStructures();

            // Draw Ground Items (dropped loot)
            DrawGroundItems();

            // Draw Enemies (only if visible)
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsAlive) continue;

                // FOG OF WAR: Only draw enemies the player can see
                if (!GameServices.FogOfWar.CanSeeEntity(enemy.Position, _world.TileSize))
                    continue;

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

                // Names are drawn in screen space (UI layer) to avoid zoom blur
            }

            // Draw NPCs (only if visible)
            foreach (var npc in _npcs)
            {
                // FOG OF WAR: Only draw NPCs the player can see
                if (!GameServices.FogOfWar.CanSeeEntity(npc.Position, _world.TileSize))
                    continue;

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

                // Names are drawn in screen space (UI layer) to avoid zoom blur
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

            // --- LAYER 1.5: FLOATING TEXTS (in world space) ---
            _spriteBatch.Begin(transformMatrix: _camera.GetViewMatrix(), samplerState: SamplerState.PointClamp);
            DrawFloatingTexts();
            _spriteBatch.End();

            // --- LAYER 2: UI ---
            _spriteBatch.Begin();

            // --- ENTITY NAMES (screen space, only when zoomed in enough) ---
            if (_camera.Zoom >= MIN_ZOOM_FOR_NAMES)
            {
                DrawEntityNamesScreenSpace();
            }

            // ========================================================
            // BOTTOM HUD BAR - Clean, organized layout
            // ========================================================

            // Dark background bar
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 672, 1280, 48), new Color(15, 18, 25) * 0.95f);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 672, 1280, 1), new Color(60, 70, 90));  // Top border

            // === LEFT SECTION (X=10-320): Player Status ===

            // HP Bar (compact)
            _spriteBatch.Draw(_pixelTexture, new Rectangle(10, 678, 160, 22), new Color(40, 40, 40));
            float healthPercent2 = _player.Stats.CurrentHealth / _player.Stats.MaxHealth;
            Color healthColor = healthPercent2 > 0.5f ? Color.Green : (healthPercent2 > 0.25f ? Color.Yellow : Color.Red);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(11, 679, (int)(158 * healthPercent2), 20), healthColor);
            _spriteBatch.DrawString(_font, $"HP: {_player.Stats.CurrentHealth:F0}/{_player.Stats.MaxHealth:F0}", new Vector2(16, 680), Color.White);

            // Character info (Trait | Path)
            string traitInfo = $"{_player.Stats.Traits[0]} | {_player.Stats.SciencePath}";
            _spriteBatch.DrawString(_font, traitInfo, new Vector2(10, 702), new Color(140, 180, 220));

            // Attributes (compact, right of HP bar)
            string attrInfo = _player.Stats.Attributes.GetDisplayString();
            _spriteBatch.DrawString(_font, attrInfo, new Vector2(180, 680), new Color(150, 200, 150));

            // === CENTER SECTION (X=450-830): Level & Mutations ===

            // Level and XP (centered)
            string lvlText = $"Lv.{_player.Stats.Level}  |  XP: {_player.Stats.CurrentXP:F0}/{_player.Stats.XPToNextLevel:F0}";
            Vector2 lvlSize = _font.MeasureString(lvlText);
            int lvlX = (int)(640 - lvlSize.X / 2);
            _spriteBatch.DrawString(_font, lvlText, new Vector2(lvlX, 678), Color.Yellow);

            // Mutation Points (if available, pulsing)
            int totalPoints = _player.Stats.MutationPoints + _player.Stats.FreeMutationPicks;
            if (totalPoints > 0)
            {
                string mutText = $"[M] Mutations: {_player.Stats.MutationPoints}";
                if (_player.Stats.FreeMutationPicks > 0)
                    mutText += $" (+{_player.Stats.FreeMutationPicks} FREE)";

                Vector2 mutSize = _font.MeasureString(mutText);
                int mutX = (int)(640 - mutSize.X / 2);
                float pulse = (float)(Math.Sin(_totalTime * 4) * 0.3 + 0.7);
                _spriteBatch.DrawString(_font, mutText, new Vector2(mutX, 696), Color.Magenta * pulse);
            }

            // === RIGHT SECTION (X=950-1270): Gold & Help ===

            // Gold
            string goldText = $"Gold: {_player.Stats.Gold}";
            Vector2 goldSize = _font.MeasureString(goldText);
            _spriteBatch.DrawString(_font, goldText, new Vector2(1270 - goldSize.X - 80, 678), Color.Gold);

            // Help toggle indicator
            string helpIndicator = _showKeybinds ? "[?] Hide" : "[?] Help";
            Vector2 helpSize = _font.MeasureString(helpIndicator);
            Color helpColor = _showKeybinds ? Color.Yellow : new Color(120, 120, 120);
            _spriteBatch.DrawString(_font, helpIndicator, new Vector2(1270 - helpSize.X - 5, 700), helpColor);

            // === KEYBIND HINTS (above HUD bar, only when toggled on) ===
            if (_showKeybinds && !_combat.InCombat)
            {
                string helpLine1 = "Move: Click | Inspect: Right-Click | I: Inventory | J: Quests | R: Research | B: Build | N: Map";
                string helpLine2 = "F10: Save | F11: Load | H: Survival | T: Time | M: Mutations | -: Minimap | G: Pickup";
                Vector2 size1 = _font.MeasureString(helpLine1);
                Vector2 size2 = _font.MeasureString(helpLine2);
                int helpX1 = (int)(640 - size1.X / 2);
                int helpX2 = (int)(640 - size2.X / 2);

                // Background
                _spriteBatch.Draw(_pixelTexture, new Rectangle(helpX1 - 8, 628, (int)size1.X + 16, 18), Color.Black * 0.8f);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(helpX2 - 8, 648, (int)size2.X + 16, 18), Color.Black * 0.8f);

                _spriteBatch.DrawString(_font, helpLine1, new Vector2(helpX1, 630), Color.White * 0.9f);
                _spriteBatch.DrawString(_font, helpLine2, new Vector2(helpX2, 650), Color.LightGray * 0.9f);
            }

            // Combat UI
            if (_combat.InCombat)
            {
                DrawCombatUI();
                DrawEnemyHoverTooltip();  // BG3 style hit chance tooltip
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
            }

            // --- ZONE INFO (top center) ---
            if (!_combat.InCombat && _zoneManager.CurrentZone != null)
            {
                var zone = _zoneManager.CurrentZone;
                string zoneName = $"[ {zone.Name} ]";
                Vector2 zoneNameSize = _font.MeasureString(zoneName);
                int zoneX = (int)(640 - zoneNameSize.X / 2);
                _spriteBatch.DrawString(_font, zoneName, new Vector2(zoneX + 1, 36), Color.Black);
                _spriteBatch.DrawString(_font, zoneName, new Vector2(zoneX, 35), Color.White);

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
                int dangerX = (int)(640 - _font.MeasureString(dangerStr).X / 2);
                _spriteBatch.DrawString(_font, dangerStr, new Vector2(dangerX, 53), dangerColor);

                // Show available exits as compass
                string exitInfo = "Exits: ";
                List<string> exits = new List<string>();
                if (zone.Exits.ContainsKey(ZoneExitDirection.North)) exits.Add("N");
                if (zone.Exits.ContainsKey(ZoneExitDirection.South)) exits.Add("S");
                if (zone.Exits.ContainsKey(ZoneExitDirection.East)) exits.Add("E");
                if (zone.Exits.ContainsKey(ZoneExitDirection.West)) exits.Add("W");
                exitInfo += string.Join(" ", exits);

                // Show FREE ZONE badge if applicable
                if (zone.IsFreeZone)
                {
                    exitInfo = "[FREE ZONE] " + exitInfo;
                }

                Vector2 exitSize = _font.MeasureString(exitInfo);
                Color exitColor = zone.IsFreeZone ? new Color(80, 220, 120) : new Color(150, 150, 180);
                int exitX = (int)(640 - exitSize.X / 2);
                _spriteBatch.DrawString(_font, exitInfo, new Vector2(exitX, 71), exitColor);
            }

            // --- ZONE EXIT HINTS (edges of screen) ---
            if (!_combat.InCombat && _zoneManager.CurrentZone != null)
            {
                DrawZoneExitHints();
            }

            // --- SURVIVAL UI (top right) ---
            DrawSurvivalUI();

            // --- WORLD EVENT NOTIFICATIONS (center top) ---
            DrawEventNotifications();

            // --- ACTIVE EVENT INDICATOR (bottom left) ---
            DrawActiveEventIndicator();

            // --- MINIMAP (top left corner) ---
            if (!_worldMapOpen)  // Don't show minimap when world map is open
            {
                DrawMinimap();
            }

            // --- PICKUP HINT (above HUD bar) ---
            if (_nearestItem != null && !_combat.InCombat)
            {
                string pickupText = $"[G] Pick up: {_nearestItem.GetDisplayText()}";
                Vector2 pickupSize = _font.MeasureString(pickupText);
                int hintX = (int)(640 - pickupSize.X / 2);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(hintX - 5, 648, (int)pickupSize.X + 10, 18), Color.Black * 0.8f);
                _spriteBatch.DrawString(_font, pickupText, new Vector2(hintX, 650), Color.Yellow);
            }

            // --- NPC TRADE HINT (above HUD bar) ---
            if (_nearestNPC != null && !_combat.InCombat && _nearestItem == null)
            {
                string tradeText = $"[T] Talk to: {_nearestNPC.Name}";
                Vector2 tradeSize = _font.MeasureString(tradeText);
                int tradeX = (int)(640 - tradeSize.X / 2);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(tradeX - 5, 648, (int)tradeSize.X + 10, 18), Color.Black * 0.8f);
                _spriteBatch.DrawString(_font, tradeText, new Vector2(tradeX, 650), Color.Cyan);
            }

            // --- GROUND ITEMS COUNT (in HUD bar, right section) ---
            if (_groundItems.Count > 0 && !_combat.InCombat)
            {
                string itemsText = $"Items: {_groundItems.Count}";
                Vector2 itemsSize = _font.MeasureString(itemsText);
                _spriteBatch.DrawString(_font, itemsText, new Vector2(1270 - itemsSize.X - 80, 696), new Color(200, 180, 100));
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

            // --- WORLD MAP UI (fullscreen overlay) ---
            if (_worldMapOpen)
            {
                DrawWorldMap();
            }

            // --- BODY PANEL UI (fullscreen overlay) ---
            if (_bodyPanelOpen)
            {
                DrawBodyPanelUI();

                // Grip selection dialog (drawn on top of body panel)
                if (_gripDialogOpen)
                {
                    DrawGripSelectionDialog();
                }
            }

            // --- PAUSE MENU UI (fullscreen overlay - drawn on top) ---
            if (_pauseMenuOpen)
            {
                DrawPauseMenuUI();
            }

            // --- NOTIFICATION (drawn last, on top of everything) ---
            if (_notificationTimer > 0 && !string.IsNullOrEmpty(_notificationText))
            {
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                float alpha = Math.Min(1f, _notificationTimer);  // Fade out
                Vector2 textSize = _font.MeasureString(_notificationText);
                int posX = (int)(640 - textSize.X / 2);
                int posY = 600;  // Bottom area, above hints

                // Background
                _spriteBatch.Draw(_pixelTexture, new Rectangle(posX - 10, posY - 5, (int)textSize.X + 20, (int)textSize.Y + 10), Color.Black * (alpha * 0.9f));

                // Border
                _spriteBatch.Draw(_pixelTexture, new Rectangle(posX - 12, posY - 7, (int)textSize.X + 24, 2), Color.Yellow * alpha);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(posX - 12, posY + (int)textSize.Y + 5, (int)textSize.X + 24, 2), Color.Yellow * alpha);

                // Text (integer position)
                _spriteBatch.DrawString(_font, _notificationText, new Vector2(posX, posY), Color.Yellow * alpha);

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

        /// <summary>
        /// Draw minimap in top-left corner
        /// </summary>
        private void DrawMinimap()
        {
            // Minimap configuration
            int mapSize = _minimapExpanded ? MINIMAP_SIZE_LARGE : MINIMAP_SIZE_SMALL;
            int mapX = 10;
            int mapY = 10;
            int padding = 2;

            // Calculate scale (world tiles to minimap pixels)
            float scaleX = (float)(mapSize - padding * 2) / _world.Width;
            float scaleY = (float)(mapSize - padding * 2) / _world.Height;

            // Background
            Rectangle mapBounds = new Rectangle(mapX, mapY, mapSize, mapSize);
            _spriteBatch.Draw(_pixelTexture, mapBounds, new Color(10, 15, 20) * 0.9f);
            DrawBorder(mapBounds, new Color(60, 70, 90), 2);

            // Draw terrain (simplified)
            int blockSize = Math.Max(1, (int)Math.Ceiling(scaleX));
            for (int y = 0; y < _world.Height; y += Math.Max(1, _world.Height / 50))
            {
                for (int x = 0; x < _world.Width; x += Math.Max(1, _world.Width / 50))
                {
                    var tile = _world.GetTile(x, y);
                    Color tileColor = GetMinimapTileColor(tile.Type);

                    int px = mapX + padding + (int)(x * scaleX);
                    int py = mapY + padding + (int)(y * scaleY);

                    _spriteBatch.Draw(_pixelTexture, new Rectangle(px, py, blockSize, blockSize), tileColor);
                }
            }

            // Draw structures
            foreach (var structure in GameServices.Building.GetAllStructures())
            {
                int px = mapX + padding + (int)(structure.Position.X * scaleX);
                int py = mapY + padding + (int)(structure.Position.Y * scaleY);

                Color structColor = structure.IsComplete ? new Color(139, 90, 43) : new Color(100, 100, 80);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(px, py, Math.Max(2, blockSize), Math.Max(2, blockSize)), structColor);
            }

            // Draw ground items (yellow dots)
            foreach (var item in _groundItems)
            {
                int px = mapX + padding + (int)((item.Position.X / _world.TileSize) * scaleX);
                int py = mapY + padding + (int)((item.Position.Y / _world.TileSize) * scaleY);

                _spriteBatch.Draw(_pixelTexture, new Rectangle(px - 1, py - 1, 3, 3), Color.Yellow);
            }

            // Draw NPCs (green dots)
            foreach (var npc in _npcs)
            {
                int px = mapX + padding + (int)((npc.Position.X / _world.TileSize) * scaleX);
                int py = mapY + padding + (int)((npc.Position.Y / _world.TileSize) * scaleY);

                Color npcColor = (npc.Type == NPCType.Merchant || npc.Type == NPCType.Wanderer) ? Color.LimeGreen : Color.Cyan;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(px - 2, py - 2, 5, 5), npcColor);
            }

            // Draw enemies (red dots, orange if in combat)
            foreach (var enemy in _enemies.Where(e => e.IsAlive))
            {
                int px = mapX + padding + (int)((enemy.Position.X / _world.TileSize) * scaleX);
                int py = mapY + padding + (int)((enemy.Position.Y / _world.TileSize) * scaleY);

                Color enemyColor = enemy.IsProvoked ? Color.OrangeRed : Color.Red;
                int dotSize = _combat.InCombat ? 5 : 4;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(px - dotSize / 2, py - dotSize / 2, dotSize, dotSize), enemyColor);
            }

            // Draw zone exits (arrows at edges)
            var zone = _zoneManager.CurrentZone;
            if (zone != null)
            {
                Color exitColor = new Color(100, 200, 255);
                int arrowSize = 6;

                if (zone.Exits.ContainsKey(ZoneExitDirection.North))
                {
                    // Draw arrow at top
                    int cx = mapX + mapSize / 2;
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(cx - arrowSize / 2, mapY + 3, arrowSize, 3), exitColor);
                }
                if (zone.Exits.ContainsKey(ZoneExitDirection.South))
                {
                    int cx = mapX + mapSize / 2;
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(cx - arrowSize / 2, mapY + mapSize - 6, arrowSize, 3), exitColor);
                }
                if (zone.Exits.ContainsKey(ZoneExitDirection.West))
                {
                    int cy = mapY + mapSize / 2;
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(mapX + 3, cy - arrowSize / 2, 3, arrowSize), exitColor);
                }
                if (zone.Exits.ContainsKey(ZoneExitDirection.East))
                {
                    int cy = mapY + mapSize / 2;
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(mapX + mapSize - 6, cy - arrowSize / 2, 3, arrowSize), exitColor);
                }
            }

            // Draw player (white dot with pulse effect)
            Point playerTile = new Point(
                (int)(_player.Position.X / _world.TileSize),
                (int)(_player.Position.Y / _world.TileSize)
            );
            int playerPx = mapX + padding + (int)(playerTile.X * scaleX);
            int playerPy = mapY + padding + (int)(playerTile.Y * scaleY);

            // Pulsing effect
            float pulse = (float)Math.Sin(DateTime.Now.Millisecond / 200.0) * 0.3f + 0.7f;
            Color playerColor = Color.White * pulse;

            // Draw player marker (larger, with border)
            _spriteBatch.Draw(_pixelTexture, new Rectangle(playerPx - 4, playerPy - 4, 9, 9), Color.Black);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(playerPx - 3, playerPy - 3, 7, 7), playerColor);

            // Draw active event markers (flashing)
            if (_worldEvents.HasActiveEvent)
            {
                float flash = (float)Math.Sin(DateTime.Now.Millisecond / 150.0) * 0.5f + 0.5f;
                Color eventColor = new Color(255, 100, 100) * flash;

                // Draw warning icon at top of minimap
                string warning = "!";
                Vector2 warnPos = new Vector2(mapX + mapSize - 15, mapY + 5);
                _spriteBatch.DrawString(_font, warning, warnPos, eventColor);
            }

            // Zone name label
            if (zone != null)
            {
                string zoneName = zone.Name.Length > 18 ? zone.Name.Substring(0, 16) + ".." : zone.Name;
                Vector2 namePos = new Vector2(mapX + 5, mapY + mapSize - 18);
                _spriteBatch.DrawString(_font, zoneName, namePos + Vector2.One, Color.Black * 0.8f);
                _spriteBatch.DrawString(_font, zoneName, namePos, Color.White * 0.9f);
            }
        }

        /// <summary>
        /// Get color for tile on minimap
        /// </summary>
        private Color GetMinimapTileColor(TileType type)
        {
            return type switch
            {
                TileType.Grass => new Color(40, 60, 30),
                TileType.Dirt => new Color(60, 50, 35),
                TileType.Sand => new Color(80, 75, 50),
                TileType.Stone => new Color(70, 70, 75),
                TileType.StoneWall => new Color(50, 50, 55),
                TileType.Water => new Color(30, 50, 80),
                TileType.DeepWater => new Color(20, 35, 70),
                _ => new Color(45, 45, 45)
            };
        }

        /// <summary>
        /// Draw entity names in screen space (not affected by camera zoom)
        /// This prevents blurry text when zoomed out
        /// </summary>
        private void DrawEntityNamesScreenSpace()
        {
            int tileOffset = (_world.TileSize - 32) / 2;

            // Draw enemy names (only in combat)
            if (_combat.InCombat)
            {
                foreach (var enemy in _enemies)
                {
                    if (!enemy.IsAlive) continue;

                    // Convert world position to screen position
                    Vector2 worldPos = new Vector2(enemy.Position.X + tileOffset + 16, enemy.Position.Y - 10);
                    Vector2 screenPos = _camera.WorldToScreen(worldPos);

                    // Skip if off-screen
                    if (screenPos.X < -100 || screenPos.X > 1380 || screenPos.Y < -50 || screenPos.Y > 770)
                        continue;

                    // Center the name
                    Vector2 nameSize = _font.MeasureString(enemy.Name);
                    int textX = (int)(screenPos.X - nameSize.X / 2);
                    int textY = (int)screenPos.Y;

                    // Draw with shadow for readability
                    _spriteBatch.DrawString(_font, enemy.Name, new Vector2(textX + 1, textY + 1), Color.Black * 0.7f);
                    _spriteBatch.DrawString(_font, enemy.Name, new Vector2(textX, textY), Color.White);
                }
            }

            // Draw NPC names (always visible when zoomed in)
            foreach (var npc in _npcs)
            {
                // Convert world position to screen position
                Vector2 worldPos = new Vector2(npc.Position.X + tileOffset + 16, npc.Position.Y - 5);
                Vector2 screenPos = _camera.WorldToScreen(worldPos);

                // Skip if off-screen
                if (screenPos.X < -100 || screenPos.X > 1380 || screenPos.Y < -50 || screenPos.Y > 770)
                    continue;

                // Center the name
                Vector2 nameSize = _font.MeasureString(npc.Name);
                int textX = (int)(screenPos.X - nameSize.X / 2);
                int textY = (int)screenPos.Y;

                // Draw with shadow for readability
                _spriteBatch.DrawString(_font, npc.Name, new Vector2(textX + 1, textY + 1), Color.Black * 0.7f);
                _spriteBatch.DrawString(_font, npc.Name, new Vector2(textX, textY), npc.DisplayColor);
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
                // FOG OF WAR: Only draw items the player can see
                if (!GameServices.FogOfWar.CanSeeEntity(worldItem.Position, _world.TileSize))
                    continue;

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
        // FOG OF WAR DRAWING
        // ============================================

        /// <summary>
        /// Draw fog of war overlay on tiles
        /// </summary>
        private void DrawFogOfWar()
        {
            if (!GameServices.FogOfWar.IsEnabled) return;
            if (GameServices.FogOfWar.DebugRevealAll) return;

            // Get visible area (camera bounds + buffer)
            var viewMatrix = _camera.GetViewMatrix();
            Matrix inverseView = Matrix.Invert(viewMatrix);
            
            // Calculate visible tile range
            Vector2 topLeft = Vector2.Transform(Vector2.Zero, inverseView);
            Vector2 bottomRight = Vector2.Transform(new Vector2(1280, 720), inverseView);
            
            int startX = Math.Max(0, (int)(topLeft.X / _world.TileSize) - 1);
            int startY = Math.Max(0, (int)(topLeft.Y / _world.TileSize) - 1);
            int endX = Math.Min(_world.Width, (int)(bottomRight.X / _world.TileSize) + 2);
            int endY = Math.Min(_world.Height, (int)(bottomRight.Y / _world.TileSize) + 2);

            // Draw fog overlay for each tile
            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    var visibility = GameServices.FogOfWar.GetVisibility(x, y);
                    
                    if (visibility == TileVisibility.Visible)
                        continue;  // No fog on visible tiles
                    
                    Rectangle tileRect = new Rectangle(
                        x * _world.TileSize,
                        y * _world.TileSize,
                        _world.TileSize,
                        _world.TileSize
                    );

                    if (visibility == TileVisibility.Unexplored)
                    {
                        // Completely black - never seen
                        _spriteBatch.Draw(_pixelTexture, tileRect, Color.Black);
                    }
                    else if (visibility == TileVisibility.Explored)
                    {
                        // Semi-transparent fog - can see terrain but not units
                        _spriteBatch.Draw(_pixelTexture, tileRect, new Color(0, 0, 0, 160));
                    }
                }
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
                _spriteBatch.DrawString(_font, $"Value: {item.Value} gold  |  Weight: {item.Weight:F1} kg", new Vector2(x, y), UITheme.CategoryAmmo);
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

                // Stats for weapons/armor (with quality modifiers)
                if (item.Category == ItemCategory.Weapon && item.Definition != null)
                {
                    float effectiveDmg = item.GetEffectiveDamage();
                    float baseDmg = item.Definition.Damage;

                    // Show quality effect on damage
                    if (item.Quality != ItemQuality.Normal && item.Quality != ItemQuality.Broken)
                    {
                        float modifier = item.GetQualityMultiplier();
                        _spriteBatch.DrawString(_font, $"Damage: {effectiveDmg:F1}", new Vector2(x, y), UITheme.TextHighlight);
                        _spriteBatch.DrawString(_font, $" ({baseDmg:F0} x{modifier:F2})", new Vector2(x + 120, y), GetItemQualityColor(item.Quality));
                    }
                    else
                    {
                        _spriteBatch.DrawString(_font, $"Damage: {effectiveDmg:F1}", new Vector2(x, y), UITheme.TextHighlight);
                    }
                    y += 22;

                    if (item.Definition.Range > 1)
                    {
                        _spriteBatch.DrawString(_font, $"Range: {item.Definition.Range} tiles", new Vector2(x, y), UITheme.TextSecondary);
                        y += 22;
                    }

                    if (item.Definition.Accuracy != 0)
                    {
                        float effAcc = item.GetEffectiveAccuracy();
                        string accStr = effAcc >= 0 ? $"+{effAcc:F0}%" : $"{effAcc:F0}%";
                        _spriteBatch.DrawString(_font, $"Accuracy: {accStr}", new Vector2(x, y), effAcc >= 0 ? UITheme.TextSuccess : UITheme.TextDanger);
                        y += 22;
                    }

                    // Weapon properties
                    if (item.Definition.WeaponLength != WeaponLength.None)
                    {
                        _spriteBatch.DrawString(_font, $"Length: {item.Definition.WeaponLength}", new Vector2(x, y), UITheme.TextSecondary);
                        y += 22;
                    }

                    // Grip info
                    if (item.Definition.HandsRequired >= 2)
                    {
                        _spriteBatch.DrawString(_font, "Two-Handed", new Vector2(x, y), UITheme.TextWarning);
                        if (item.Definition.CanUseOneHand)
                            _spriteBatch.DrawString(_font, " (can one-hand with penalty)", new Vector2(x + 100, y), UITheme.TextSecondary);
                        y += 22;
                    }
                    else if (item.Definition.CanUseTwoHand && item.Definition.TwoHandDamageBonus > 0)
                    {
                        _spriteBatch.DrawString(_font, $"Versatile (+{item.Definition.TwoHandDamageBonus * 100:F0}% two-handed)", new Vector2(x, y), UITheme.TextSuccess);
                        y += 22;
                    }

                    // Dual wield penalty
                    if (item.Definition.DualWieldPenalty > 0)
                    {
                        _spriteBatch.DrawString(_font, $"Dual-Wield Penalty: -{item.Definition.DualWieldPenalty * 100:F0}%", new Vector2(x, y), UITheme.TextDanger);
                        y += 22;
                    }
                }
                else if (item.Category == ItemCategory.Armor && item.Definition != null)
                {
                    float effectiveArmor = item.GetEffectiveArmor();
                    float baseArmor = item.Definition.Armor;

                    // Show quality effect on armor
                    if (item.Quality != ItemQuality.Normal && item.Quality != ItemQuality.Broken)
                    {
                        float modifier = item.GetQualityMultiplier();
                        _spriteBatch.DrawString(_font, $"Armor: {effectiveArmor:F1}", new Vector2(x, y), UITheme.TextHighlight);
                        _spriteBatch.DrawString(_font, $" ({baseArmor:F0} x{modifier:F2})", new Vector2(x + 100, y), GetItemQualityColor(item.Quality));
                    }
                    else
                    {
                        _spriteBatch.DrawString(_font, $"Armor: {effectiveArmor:F1}", new Vector2(x, y), UITheme.TextHighlight);
                    }
                    y += 22;

                    // Show combat bonuses
                    if (item.Definition.ActionPointBonus != 0)
                    {
                        string apStr = item.Definition.ActionPointBonus >= 0 ? $"+{item.Definition.ActionPointBonus}" : $"{item.Definition.ActionPointBonus}";
                        _spriteBatch.DrawString(_font, $"Action Points: {apStr}", new Vector2(x, y), UITheme.TextSuccess);
                        y += 22;
                    }
                    if (item.Definition.MovementPointBonus != 0)
                    {
                        string mpStr = item.Definition.MovementPointBonus >= 0 ? $"+{item.Definition.MovementPointBonus}" : $"{item.Definition.MovementPointBonus}";
                        _spriteBatch.DrawString(_font, $"Movement Points: {mpStr}", new Vector2(x, y), UITheme.TextSuccess);
                        y += 22;
                    }
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
                _spriteBatch.DrawString(_font, $"Value: {item.Value} gold", new Vector2(x, y), UITheme.CategoryAmmo);
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

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Dark overlay
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.92f);

            // Main panel
            string wsName = _activeWorkstationType != null ? CraftingSystem.GetWorkstationName(_activeWorkstationType) : "Basic Crafting";
            Rectangle mainPanel = new Rectangle(30, 30, 1220, 660);
            DrawPanel(mainPanel, $"⚒ {wsName.ToUpper()}");

            // Get all available recipes
            var allRecipes = GameServices.Crafting.GetAvailableRecipes(_activeWorkstationType, _player.Stats);

            // Filter by selected category (index 0 = "All")
            List<RecipeDefinition> filteredRecipes;
            if (_craftingCategoryIndex == 0)
            {
                filteredRecipes = allRecipes;
            }
            else
            {
                var selectedCategory = CraftingCategories[_craftingCategoryIndex - 1];
                filteredRecipes = allRecipes.Where(r => r.Category == selectedCategory).ToList();
            }

            // Draw category tabs
            DrawCraftingCategoryTabs(mState, allRecipes);

            // Recipe list panel (left side)
            Rectangle listPanel = new Rectangle(40, 115, 420, 500);
            _spriteBatch.Draw(_pixelTexture, listPanel, UITheme.PanelBackground * 0.6f);
            DrawBorder(listPanel, UITheme.PanelBorder, 1);

            // Recipe list header
            int craftableCount = filteredRecipes.Count(r => GameServices.Crafting.HasMaterials(r, _player.Stats.Inventory));
            _spriteBatch.DrawString(_font, $"RECIPES", new Vector2(50, 120), UITheme.TextHighlight);
            _spriteBatch.DrawString(_font, $"{craftableCount}/{filteredRecipes.Count} craftable",
                new Vector2(50 + 320, 120), craftableCount > 0 ? UITheme.TextSuccess : UITheme.TextSecondary);

            // Draw recipe list with scrolling
            DrawRecipeList(filteredRecipes, mState);

            // Draw selected recipe details (right side)
            if (_selectedRecipeIndex >= 0 && _selectedRecipeIndex < filteredRecipes.Count)
            {
                DrawRecipeDetailsEnhanced(filteredRecipes[_selectedRecipeIndex], 475, 115, mState);
            }
            else if (filteredRecipes.Count == 0)
            {
                // No recipes message
                Rectangle emptyPanel = new Rectangle(475, 115, 765, 500);
                _spriteBatch.Draw(_pixelTexture, emptyPanel, UITheme.PanelBackground * 0.6f);
                DrawBorder(emptyPanel, UITheme.PanelBorder, 1);
                _spriteBatch.DrawString(_font, "No recipes in this category", new Vector2(720, 350), UITheme.TextSecondary);
            }

            // Crafting feedback animation
            if (_craftingFeedbackTimer > 0)
            {
                float alpha = Math.Min(1f, _craftingFeedbackTimer * 2);
                Color feedbackColor = _craftingFeedbackSuccess ? UITheme.TextSuccess : UITheme.TextDanger;
                Vector2 feedbackPos = new Vector2(640, 620);
                Vector2 textSize = _font.MeasureString(_craftingFeedbackText);
                _spriteBatch.DrawString(_font, _craftingFeedbackText, feedbackPos - textSize / 2, feedbackColor * alpha);
            }

            // Help bar with context-sensitive hints
            string helpText = "[C/Esc] Close  |  [W/S] Navigate  |  [A/D] Categories  |  [Q/E] Quantity  |  [Enter] Craft";
            DrawHelpBar(helpText);

            _spriteBatch.End();
        }

        private void DrawCraftingCategoryTabs(MouseState mState, List<RecipeDefinition> allRecipes)
        {
            int tabX = 45;
            int tabY = 75;
            int tabWidth = 90;
            int tabHeight = 28;
            int tabSpacing = 5;

            // "All" tab first
            string[] tabNames = new string[] { "All", "Basic", "Weapons", "Armor", "Consume", "Material", "Tools", "Gadgets", "Anomaly" };
            Color[] tabColors = new Color[]
            {
                UITheme.TextPrimary,
                UITheme.TextSecondary,
                UITheme.CategoryWeapon,
                UITheme.CategoryArmor,
                UITheme.CategoryConsumable,
                UITheme.CategoryMaterial,
                new Color(150, 120, 200),
                new Color(100, 200, 255),
                new Color(180, 100, 220)
            };

            for (int i = 0; i < tabNames.Length; i++)
            {
                Rectangle tabRect = new Rectangle(tabX + i * (tabWidth + tabSpacing), tabY, tabWidth, tabHeight);
                bool isSelected = (i == _craftingCategoryIndex);
                bool isHovered = tabRect.Contains(mState.X, mState.Y);

                // Count recipes in this category
                int count;
                if (i == 0)
                    count = allRecipes.Count;
                else
                    count = allRecipes.Count(r => r.Category == CraftingCategories[i - 1]);

                // Skip empty categories (except All)
                if (i > 0 && count == 0) continue;

                // Tab background
                Color bgColor = isSelected ? UITheme.SelectionBackground :
                               (isHovered ? UITheme.HoverBackground : UITheme.ButtonNormal);
                _spriteBatch.Draw(_pixelTexture, tabRect, bgColor);

                // Tab border
                Color borderColor = isSelected ? tabColors[i] : UITheme.PanelBorder;
                DrawBorder(tabRect, borderColor, isSelected ? 2 : 1);

                // Selection indicator bar at bottom
                if (isSelected)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(tabRect.X, tabRect.Bottom - 3, tabRect.Width, 3), tabColors[i]);
                }

                // Tab text
                string tabText = $"{tabNames[i]}";
                Color textColor = isSelected ? tabColors[i] : (isHovered ? UITheme.TextPrimary : UITheme.TextSecondary);
                Vector2 textSize = _font.MeasureString(tabText);
                Vector2 textPos = new Vector2(tabRect.X + (tabRect.Width - textSize.X) / 2, tabRect.Y + 6);
                _spriteBatch.DrawString(_font, tabText, textPos, textColor);

                // Mouse click to select tab
                if (isHovered && mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                {
                    _craftingCategoryIndex = i;
                    _selectedRecipeIndex = 0;
                    _craftingScrollOffset = 0;
                }
            }
        }

        private void DrawRecipeList(List<RecipeDefinition> recipes, MouseState mState)
        {
            int startX = 50;
            int startY = 145;
            int itemHeight = 36;
            int itemWidth = 400;
            int maxVisible = 13;

            // Clamp scroll offset
            int maxScroll = Math.Max(0, recipes.Count - maxVisible);
            _craftingScrollOffset = Math.Clamp(_craftingScrollOffset, 0, maxScroll);

            // Clamp selected index
            if (recipes.Count > 0)
            {
                _selectedRecipeIndex = Math.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
            }

            int y = startY;
            for (int i = _craftingScrollOffset; i < recipes.Count && i < _craftingScrollOffset + maxVisible; i++)
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

                // Double-click to craft
                // (Handled in UpdateCraftingInput)

                // Background with gradient effect
                Color bgColor = isSelected ? UITheme.SelectionBackground :
                               (isHovered ? UITheme.HoverBackground : Color.Transparent);
                _spriteBatch.Draw(_pixelTexture, itemRect, bgColor);

                // Selection indicator
                if (isSelected)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(startX, y, 4, itemHeight - 2), UITheme.SelectionBorder);
                }

                // Category color indicator (small bar on left)
                Color catColor = GetRecipeCategoryColor(recipe.Category);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(startX + 6, y + 4, 4, itemHeight - 10), catColor);

                // Recipe name
                Color nameColor = canCraft ? UITheme.TextPrimary : UITheme.TextSecondary * 0.7f;
                _spriteBatch.DrawString(_font, recipe.Name, new Vector2(startX + 16, y + 9), nameColor);

                // Output amount if > 1
                if (recipe.OutputAmount > 1)
                {
                    _spriteBatch.DrawString(_font, $"x{recipe.OutputAmount}", new Vector2(startX + itemWidth - 80, y + 9), UITheme.TextSecondary);
                }

                // Can craft indicator with better visual
                if (canCraft)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(startX + itemWidth - 30, y + 8, 20, 20), UITheme.TextSuccess * 0.3f);
                    _spriteBatch.DrawString(_font, "+", new Vector2(startX + itemWidth - 26, y + 9), UITheme.TextSuccess);
                }
                else
                {
                    _spriteBatch.DrawString(_font, "x", new Vector2(startX + itemWidth - 26, y + 9), UITheme.TextDanger * 0.6f);
                }

                y += itemHeight;
            }

            // Scroll indicators
            if (_craftingScrollOffset > 0)
            {
                _spriteBatch.DrawString(_font, "▲ More above", new Vector2(startX + 140, startY - 18), UITheme.TextSecondary * 0.7f);
            }
            if (_craftingScrollOffset < maxScroll)
            {
                _spriteBatch.DrawString(_font, "▼ More below", new Vector2(startX + 140, startY + maxVisible * itemHeight - 5), UITheme.TextSecondary * 0.7f);
            }

            // Empty state
            if (recipes.Count == 0)
            {
                _spriteBatch.DrawString(_font, "No recipes available", new Vector2(startX + 100, startY + 150), UITheme.TextSecondary);
                _spriteBatch.DrawString(_font, "Try a different workstation", new Vector2(startX + 80, startY + 180), UITheme.TextSecondary * 0.7f);
            }
        }

        private void DrawRecipeDetailsEnhanced(RecipeDefinition recipe, int x, int y, MouseState mState)
        {
            // Details panel
            Rectangle detailPanel = new Rectangle(x, y, 765, 500);
            _spriteBatch.Draw(_pixelTexture, detailPanel, UITheme.PanelBackground * 0.6f);
            DrawBorder(detailPanel, UITheme.PanelBorder, 1);

            int contentX = x + 20;
            int contentY = y + 15;

            // Recipe name with category color accent
            Color catColor = GetRecipeCategoryColor(recipe.Category);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(contentX, contentY, 4, 24), catColor);
            _spriteBatch.DrawString(_font, recipe.Name, new Vector2(contentX + 12, contentY), UITheme.TextHighlight);

            // Category badge
            string catText = recipe.Category.ToString();
            Vector2 catTextSize = _font.MeasureString(catText);
            Rectangle catBadge = new Rectangle(contentX + 12 + (int)_font.MeasureString(recipe.Name).X + 15, contentY, (int)catTextSize.X + 16, 22);
            _spriteBatch.Draw(_pixelTexture, catBadge, catColor * 0.3f);
            DrawBorder(catBadge, catColor * 0.6f, 1);
            _spriteBatch.DrawString(_font, catText, new Vector2(catBadge.X + 8, catBadge.Y + 3), catColor);

            // Description
            _spriteBatch.DrawString(_font, recipe.Description, new Vector2(contentX, contentY + 30), UITheme.TextSecondary);

            // Divider line
            _spriteBatch.Draw(_pixelTexture, new Rectangle(contentX, contentY + 55, 725, 1), UITheme.PanelBorder * 0.5f);

            // Requirements section
            int reqY = contentY + 70;
            bool hasRequirements = recipe.RequiredINT > 0 || recipe.RequiredLevel > 1 || recipe.RequiredScience.HasValue;

            if (hasRequirements)
            {
                _spriteBatch.DrawString(_font, "REQUIREMENTS", new Vector2(contentX, reqY), UITheme.TextHighlight * 0.9f);
                reqY += 25;

                if (recipe.RequiredLevel > 1)
                {
                    bool metLevel = _player.Stats.Level >= recipe.RequiredLevel;
                    Color lvlColor = metLevel ? UITheme.TextSuccess : UITheme.TextDanger;
                    _spriteBatch.DrawString(_font, $"  Level {recipe.RequiredLevel}+", new Vector2(contentX, reqY), lvlColor);
                    _spriteBatch.DrawString(_font, metLevel ? "+" : "x", new Vector2(contentX + 100, reqY), lvlColor);
                    reqY += 22;
                }

                if (recipe.RequiredINT > 0)
                {
                    bool metInt = _player.Stats.Attributes.INT >= recipe.RequiredINT;
                    Color intColor = metInt ? UITheme.TextSuccess : UITheme.TextDanger;
                    _spriteBatch.DrawString(_font, $"  INT {recipe.RequiredINT}+", new Vector2(contentX, reqY), intColor);
                    _spriteBatch.DrawString(_font, $"(You: {_player.Stats.Attributes.INT})", new Vector2(contentX + 100, reqY), UITheme.TextSecondary);
                    reqY += 22;
                }

                if (recipe.RequiredScience.HasValue)
                {
                    bool metScience = _player.Stats.SciencePath == recipe.RequiredScience.Value;
                    Color sciColor = metScience ? UITheme.TextSuccess : UITheme.TextDanger;
                    string sciName = recipe.RequiredScience.Value == SciencePath.Tinker ? "Tinker Science" : "Dark Science";
                    _spriteBatch.DrawString(_font, $"  {sciName}", new Vector2(contentX, reqY), sciColor);
                    _spriteBatch.DrawString(_font, metScience ? "+" : "x", new Vector2(contentX + 150, reqY), sciColor);
                    reqY += 22;
                }

                reqY += 10;
            }

            // Ingredients section with progress bars
            _spriteBatch.DrawString(_font, "INGREDIENTS", new Vector2(contentX, reqY), UITheme.TextHighlight * 0.9f);
            reqY += 28;

            foreach (var ingredient in recipe.Ingredients)
            {
                int have = _player.Stats.Inventory.GetItemCount(ingredient.ItemId);
                int need = ingredient.Amount * _craftQuantity;
                bool hasEnough = have >= need;

                var itemDef = ItemDatabase.Get(ingredient.ItemId);
                string itemName = itemDef?.Name ?? ingredient.ItemId;

                // Item name
                Color textColor = hasEnough ? UITheme.TextPrimary : UITheme.TextDanger;
                _spriteBatch.DrawString(_font, $"  {itemName}", new Vector2(contentX, reqY), textColor);

                // Progress bar background
                int barX = contentX + 200;
                int barWidth = 150;
                int barHeight = 14;
                Rectangle barBg = new Rectangle(barX, reqY + 3, barWidth, barHeight);
                _spriteBatch.Draw(_pixelTexture, barBg, UITheme.ButtonDisabled);

                // Progress bar fill
                float progress = Math.Min(1f, (float)have / need);
                int fillWidth = (int)(barWidth * progress);
                Color barColor = hasEnough ? UITheme.TextSuccess : (progress > 0.5f ? UITheme.TextWarning : UITheme.TextDanger);
                if (fillWidth > 0)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(barX, reqY + 3, fillWidth, barHeight), barColor * 0.7f);
                }
                DrawBorder(barBg, UITheme.PanelBorder, 1);

                // Count text
                string countText = $"{have}/{need}";
                Color countColor = hasEnough ? UITheme.TextSuccess : UITheme.TextDanger;
                _spriteBatch.DrawString(_font, countText, new Vector2(barX + barWidth + 10, reqY), countColor);

                reqY += 26;
            }

            // Divider
            reqY += 10;
            _spriteBatch.Draw(_pixelTexture, new Rectangle(contentX, reqY, 725, 1), UITheme.PanelBorder * 0.5f);
            reqY += 15;

            // Output section
            _spriteBatch.DrawString(_font, "OUTPUT", new Vector2(contentX, reqY), UITheme.TextHighlight * 0.9f);
            reqY += 28;

            var outputDef = ItemDatabase.Get(recipe.OutputItemId);
            string outputName = outputDef?.Name ?? recipe.OutputItemId;
            int totalOutput = recipe.OutputAmount * _craftQuantity;

            // Output item with icon
            Color outputCatColor = outputDef != null ? GetItemCategoryColor(outputDef.Category) : UITheme.TextPrimary;
            _spriteBatch.Draw(_pixelTexture, new Rectangle(contentX + 8, reqY + 2, 16, 16), outputCatColor);
            _spriteBatch.DrawString(_font, $"  {outputName}", new Vector2(contentX + 28, reqY), UITheme.TextPrimary);
            if (totalOutput > 1)
            {
                _spriteBatch.DrawString(_font, $"x{totalOutput}", new Vector2(contentX + 250, reqY), UITheme.TextHighlight);
            }
            reqY += 28;

            // Quality info
            if (recipe.AffectedByQuality)
            {
                _spriteBatch.DrawString(_font, "  Quality varies by INT:", new Vector2(contentX, reqY), UITheme.TextSecondary);
                reqY += 22;

                // Quality chance preview based on player INT
                int playerInt = _player.Stats.Attributes.INT;
                DrawQualityChancePreview(contentX + 20, reqY, playerInt);
                reqY += 30;
            }

            // Quantity selector
            reqY = y + detailPanel.Height - 110;
            _spriteBatch.DrawString(_font, "QUANTITY:", new Vector2(contentX, reqY), UITheme.TextSecondary);

            // Minus button
            Rectangle minusBtn = new Rectangle(contentX + 100, reqY - 3, 30, 26);
            bool canDecrease = _craftQuantity > 1;
            DrawButton(minusBtn, "-", mState, canDecrease);
            if (canDecrease && minusBtn.Contains(mState.X, mState.Y) &&
                mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                _craftQuantity--;
            }

            // Quantity display
            _spriteBatch.DrawString(_font, _craftQuantity.ToString(), new Vector2(contentX + 145, reqY), UITheme.TextHighlight);

            // Plus button
            Rectangle plusBtn = new Rectangle(contentX + 170, reqY - 3, 30, 26);
            int maxCraftable = GetMaxCraftableAmount(recipe);
            bool canIncrease = _craftQuantity < maxCraftable && _craftQuantity < 99;
            DrawButton(plusBtn, "+", mState, canIncrease);
            if (canIncrease && plusBtn.Contains(mState.X, mState.Y) &&
                mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                _craftQuantity++;
            }

            // Max button
            Rectangle maxBtn = new Rectangle(contentX + 210, reqY - 3, 50, 26);
            DrawButton(maxBtn, "Max", mState, maxCraftable > 0);
            if (maxCraftable > 0 && maxBtn.Contains(mState.X, mState.Y) &&
                mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                _craftQuantity = Math.Min(maxCraftable, 99);
            }

            // Craft button
            bool canCraft = GameServices.Crafting.HasMaterials(recipe, _player.Stats.Inventory) && maxCraftable >= _craftQuantity;
            Rectangle craftBtn = new Rectangle(contentX + 400, reqY - 8, 200, 40);

            // Enhanced craft button
            Color craftBtnColor = canCraft ? new Color(60, 120, 80) : UITheme.ButtonDisabled;
            Color craftBtnHover = canCraft ? new Color(80, 150, 100) : UITheme.ButtonDisabled;
            bool craftHovered = craftBtn.Contains(mState.X, mState.Y);
            _spriteBatch.Draw(_pixelTexture, craftBtn, craftHovered && canCraft ? craftBtnHover : craftBtnColor);
            DrawBorder(craftBtn, canCraft ? UITheme.TextSuccess : UITheme.PanelBorder, canCraft ? 2 : 1);

            string craftText = canCraft ? $"CRAFT ({_craftQuantity})" : "Need Materials";
            Vector2 craftTextSize = _font.MeasureString(craftText);
            Vector2 craftTextPos = new Vector2(craftBtn.X + (craftBtn.Width - craftTextSize.X) / 2, craftBtn.Y + 12);
            _spriteBatch.DrawString(_font, craftText, craftTextPos, canCraft ? Color.White : UITheme.TextSecondary);

            // Keyboard hint
            if (canCraft)
            {
                _spriteBatch.DrawString(_font, "[Enter]", new Vector2(craftBtn.X + craftBtn.Width + 10, craftBtn.Y + 12), UITheme.TextSecondary * 0.7f);
            }
        }

        private void DrawQualityChancePreview(int x, int y, int playerInt)
        {
            // Show quality distribution based on INT
            string[] qualities = { "Poor", "Normal", "Good", "Excellent", "Master" };
            Color[] colors = { Color.Gray, Color.White, Color.LimeGreen, Color.Cyan, Color.Gold };

            // Simplified quality chances (approximation)
            int intBonus = playerInt - 5;
            int[] baseChances = { 20, 35, 25, 15, 5 };

            int totalWidth = 300;
            int barHeight = 16;
            int currentX = x;

            for (int i = 0; i < 5; i++)
            {
                int chance = Math.Max(5, Math.Min(50, baseChances[i] + (i > 1 ? intBonus * 3 : -intBonus * 2)));
                if (i == 0 && intBonus > 3) chance = Math.Max(5, chance - intBonus * 4);

                int segmentWidth = totalWidth * chance / 100;
                if (segmentWidth > 5)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(currentX, y, segmentWidth - 1, barHeight), colors[i] * 0.6f);
                    currentX += segmentWidth;
                }
            }

            DrawBorder(new Rectangle(x, y, totalWidth, barHeight), UITheme.PanelBorder, 1);
        }

        private int GetMaxCraftableAmount(RecipeDefinition recipe)
        {
            if (recipe.Ingredients.Count == 0) return 99;

            int maxAmount = 99;
            foreach (var ingredient in recipe.Ingredients)
            {
                int have = _player.Stats.Inventory.GetItemCount(ingredient.ItemId);
                int canMake = have / ingredient.Amount;
                maxAmount = Math.Min(maxAmount, canMake);
            }
            return maxAmount;
        }

        private Color GetRecipeCategoryColor(RecipeCategory category)
        {
            return category switch
            {
                RecipeCategory.Basic => UITheme.TextSecondary,
                RecipeCategory.Weapons => UITheme.CategoryWeapon,
                RecipeCategory.Armor => UITheme.CategoryArmor,
                RecipeCategory.Consumables => UITheme.CategoryConsumable,
                RecipeCategory.Materials => UITheme.CategoryMaterial,
                RecipeCategory.Tools => new Color(150, 120, 200),
                RecipeCategory.Gadgets => new Color(100, 200, 255),
                RecipeCategory.Anomalies => new Color(180, 100, 220),
                RecipeCategory.Structures => new Color(139, 119, 101),
                _ => UITheme.TextPrimary
            };
        }

        private void CraftSelectedRecipe()
        {
            var allRecipes = GameServices.Crafting.GetAvailableRecipes(_activeWorkstationType, _player.Stats);

            // Filter by category
            List<RecipeDefinition> recipes;
            if (_craftingCategoryIndex == 0)
            {
                recipes = allRecipes;
            }
            else
            {
                var selectedCategory = CraftingCategories[_craftingCategoryIndex - 1];
                recipes = allRecipes.Where(r => r.Category == selectedCategory).ToList();
            }

            if (_selectedRecipeIndex >= 0 && _selectedRecipeIndex < recipes.Count)
            {
                var recipe = recipes[_selectedRecipeIndex];

                // Craft multiple if quantity > 1
                int successCount = 0;
                string lastItemName = "";
                ItemQuality bestQuality = ItemQuality.Poor;

                for (int i = 0; i < _craftQuantity; i++)
                {
                    if (!GameServices.Crafting.HasMaterials(recipe, _player.Stats.Inventory))
                        break;

                    var result = GameServices.Crafting.TryCraft(recipe, _activeWorkstationType, _player.Stats);
                    if (result != null && result.Success)
                    {
                        successCount++;
                        lastItemName = result.CraftedItem.GetDisplayName();
                        if (result.Quality > bestQuality) bestQuality = result.Quality;
                    }
                }

                if (successCount > 0)
                {
                    // Success feedback
                    _craftingFeedbackSuccess = true;
                    if (successCount == 1)
                    {
                        _craftingFeedbackText = $"+ Crafted: {lastItemName}";
                    }
                    else
                    {
                        _craftingFeedbackText = $"+ Crafted {successCount}x {recipe.Name}";
                    }
                    _craftingFeedbackTimer = 2f;

                    // Also show notification
                    ShowNotification(_craftingFeedbackText);

                    // Track quest progress
                    GameServices.Quests.OnItemCrafted(recipe.Id);
                }
                else
                {
                    // Failure feedback
                    _craftingFeedbackSuccess = false;
                    _craftingFeedbackText = "x Missing materials!";
                    _craftingFeedbackTimer = 2f;
                }

                // Reset quantity after crafting
                _craftQuantity = 1;
            }
        }

        // ============================================
        // TRADING UI DRAWING
        // ============================================

        private void DrawTradingUI()
        {
            if (_tradingNPC == null) return;

            var mState = Mouse.GetState();

            // Use default sampler (LinearClamp) for cleaner font rendering
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

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
                _spriteBatch.DrawString(_font, $"{price} gold", new Vector2(x + width - 80, itemY + 7), priceColor);
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
                    _spriteBatch.DrawString(_font, $"Price: {price} gold", new Vector2(x, y), UITheme.CategoryAmmo);
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
                    _spriteBatch.DrawString(_font, $"Sell Price: {price} gold", new Vector2(x, y), UITheme.CategoryAmmo);
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
                _spriteBatch.DrawString(_font, $"+{price} gold", new Vector2(x + width - 90, itemY + 7), priceColor);
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
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

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
                    string statusIcon = quest.State == MyRPG.Gameplay.Systems.QuestState.Completed ? " [DONE]" : "";
                    Color statusColor = quest.State == MyRPG.Gameplay.Systems.QuestState.Completed ? Color.LimeGreen : Color.White;

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
            if (quest.State == MyRPG.Gameplay.Systems.QuestState.Completed)
            {
                lineY += 20;
                _spriteBatch.DrawString(_font, ">> Return to NPC to turn in! <<", new Vector2(x, lineY), Color.LimeGreen);
            }
        }

        private void DrawQuestDialogueUI()
        {
            if (_tradingNPC == null) return;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

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
        // RESEARCH UI DRAWING - Visual Tech Tree
        // ============================================

        private void DrawResearchUI()
        {
            if (_researchCategories == null || _researchCategories.Length == 0) return;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Dark overlay
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.92f);

            // Title bar
            string pathName = _player.Stats.SciencePath == SciencePath.Tinker ? "TINKER" : "DARK";
            Color pathColor = _player.Stats.SciencePath == SciencePath.Tinker ? new Color(100, 180, 255) : new Color(180, 100, 255);

            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 60), new Color(25, 30, 40));
            _spriteBatch.DrawString(_font, $"TECH TREE - {pathName} SCIENCE PATH", new Vector2(20, 8), pathColor);
            _spriteBatch.DrawString(_font, "Click/Arrows: Select | Scroll/A,D: Pan | Enter: Research | X: Cancel | Tab: Category | Esc: Close", new Vector2(20, 30), Color.Gray);

            // Current research progress bar (top right)
            var currentResearch = GameServices.Research.GetCurrentResearch();
            if (currentResearch != null)
            {
                int barX = 850, barY = 8, barWidth = 400, barHeight = 20;
                float progress = currentResearch.ProgressPercent / 100f;

                _spriteBatch.Draw(_pixelTexture, new Rectangle(barX, barY, barWidth, barHeight), new Color(40, 40, 50));
                _spriteBatch.Draw(_pixelTexture, new Rectangle(barX + 2, barY + 2, (int)((barWidth - 4) * progress), barHeight - 4), Color.Cyan);

                // Animated glow effect
                float pulse = (float)(Math.Sin(_totalTime * 3) * 0.3 + 0.7);
                _spriteBatch.DrawString(_font, $"Researching: {currentResearch.Name} ({currentResearch.ProgressPercent:F0}%)",
                    new Vector2(barX, barY + 22), Color.Cyan * pulse);
            }

            // Category tabs
            int tabX = 20;
            int tabY = 65;
            for (int i = 0; i < _researchCategories.Length; i++)
            {
                var cat = _researchCategories[i];
                bool isSelected = (i == _selectedResearchCategory);
                string catName = cat.ToString().ToUpper();

                Color tabBgColor = isSelected ? new Color(50, 60, 80) : new Color(30, 35, 45);
                Color tabTextColor = isSelected ? Color.Yellow : Color.Gray;
                Color tabBorderColor = isSelected ? pathColor : new Color(60, 70, 90);

                int tabWidth = 130;
                Rectangle tabRect = new Rectangle(tabX, tabY, tabWidth, 28);
                _spriteBatch.Draw(_pixelTexture, tabRect, tabBgColor);
                DrawBorder(tabRect, tabBorderColor, isSelected ? 2 : 1);

                Vector2 catSize = _font.MeasureString(catName);
                int catTextX = tabX + (tabWidth - (int)catSize.X) / 2;
                _spriteBatch.DrawString(_font, catName, new Vector2(catTextX, tabY + 6), tabTextColor);

                tabX += tabWidth + 5;
            }

            // Calculate tree positions for current category
            var currentCategory = _researchCategories[_selectedResearchCategory];
            CalculateResearchTreePositions(currentCategory);

            // Draw tree area background
            Rectangle treeArea = new Rectangle(RESEARCH_TREE_LEFT, RESEARCH_TREE_TOP,
                                               RESEARCH_TREE_RIGHT - RESEARCH_TREE_LEFT,
                                               RESEARCH_TREE_BOTTOM - RESEARCH_TREE_TOP);
            _spriteBatch.Draw(_pixelTexture, treeArea, new Color(18, 22, 32));
            DrawBorder(treeArea, new Color(50, 60, 80), 1);

            // Draw connection lines first (behind nodes)
            DrawResearchTreeConnections(currentCategory);

            // Draw research nodes
            DrawResearchTreeNodes(currentCategory);

            // Scroll indicators (arrows on left/right edges if scrollable)
            if (_maxResearchScrollX > 0)
            {
                // Left scroll indicator
                if (_researchTreeScrollX > 5)
                {
                    int arrowX = RESEARCH_TREE_LEFT + 5;
                    int arrowY = (RESEARCH_TREE_TOP + RESEARCH_TREE_BOTTOM) / 2;
                    Color arrowColor = new Color(150, 150, 180) * 0.8f;

                    // Draw left arrow
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(arrowX, arrowY - 15, 3, 30), arrowColor);
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(arrowX - 6, arrowY - 6, 8, 3), arrowColor);
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(arrowX - 6, arrowY + 3, 8, 3), arrowColor);

                    _spriteBatch.DrawString(_font, "<", new Vector2(arrowX - 3, arrowY - 8), arrowColor);
                }

                // Right scroll indicator
                if (_researchTreeScrollX < _maxResearchScrollX - 5)
                {
                    int arrowX = RESEARCH_TREE_RIGHT - 15;
                    int arrowY = (RESEARCH_TREE_TOP + RESEARCH_TREE_BOTTOM) / 2;
                    Color arrowColor = new Color(150, 150, 180) * 0.8f;

                    _spriteBatch.DrawString(_font, ">", new Vector2(arrowX, arrowY - 8), arrowColor);
                }

                // Scroll bar at bottom of tree area
                int scrollBarWidth = RESEARCH_TREE_RIGHT - RESEARCH_TREE_LEFT - 40;
                int scrollBarX = RESEARCH_TREE_LEFT + 20;
                int scrollBarY = RESEARCH_TREE_BOTTOM - 12;

                // Background
                _spriteBatch.Draw(_pixelTexture, new Rectangle(scrollBarX, scrollBarY, scrollBarWidth, 6), new Color(30, 35, 45));

                // Thumb
                float scrollPercent = _researchTreeScrollX / _maxResearchScrollX;
                int thumbWidth = Math.Max(30, scrollBarWidth / 4);
                int thumbX = scrollBarX + (int)((scrollBarWidth - thumbWidth) * scrollPercent);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(thumbX, scrollBarY, thumbWidth, 6), new Color(80, 90, 110));
            }

            // Details panel (right side)
            Rectangle detailsPanel = new Rectangle(875, RESEARCH_TREE_TOP, 390, RESEARCH_TREE_BOTTOM - RESEARCH_TREE_TOP);
            _spriteBatch.Draw(_pixelTexture, detailsPanel, new Color(25, 30, 40));
            DrawBorder(detailsPanel, new Color(60, 70, 90), 2);

            // Get selected node
            ResearchNode selectedNode = null;
            if (!string.IsNullOrEmpty(_selectedResearchNodeId))
            {
                selectedNode = GameServices.Research.GetNode(_selectedResearchNodeId);
            }

            if (selectedNode != null)
            {
                DrawResearchDetailsPanel(selectedNode, 885, 110);
            }
            else
            {
                _spriteBatch.DrawString(_font, "Click a node to view details", new Vector2(900, 300), Color.Gray);
            }

            // Footer stats
            int completedCount = GameServices.Research.GetCompletedNodes().Count;
            int totalCount = GameServices.Research.GetAllNodes().Count;
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 620, 1280, 100), new Color(20, 25, 35));
            _spriteBatch.DrawString(_font, $"Research Progress: {completedCount}/{totalCount} completed", new Vector2(20, 625), Color.Gray);

            // Legend
            int legendX = 300;
            _spriteBatch.DrawString(_font, "Legend:", new Vector2(legendX, 625), Color.White);

            _spriteBatch.Draw(_pixelTexture, new Rectangle(legendX + 60, 627, 12, 12), Color.LimeGreen);
            _spriteBatch.DrawString(_font, "Done", new Vector2(legendX + 78, 625), Color.LimeGreen);

            _spriteBatch.Draw(_pixelTexture, new Rectangle(legendX + 130, 627, 12, 12), Color.Cyan);
            _spriteBatch.DrawString(_font, "In Progress", new Vector2(legendX + 148, 625), Color.Cyan);

            _spriteBatch.Draw(_pixelTexture, new Rectangle(legendX + 240, 627, 12, 12), Color.White);
            _spriteBatch.DrawString(_font, "Available", new Vector2(legendX + 258, 625), Color.White);

            _spriteBatch.Draw(_pixelTexture, new Rectangle(legendX + 340, 627, 12, 12), Color.DarkGray);
            _spriteBatch.DrawString(_font, "Locked", new Vector2(legendX + 358, 625), Color.DarkGray);

            _spriteBatch.End();
        }

        private void CalculateResearchTreePositions(ResearchCategory category)
        {
            _researchNodePositions.Clear();
            var nodes = GameServices.Research.GetNodesByCategory(category);
            if (nodes.Count == 0)
            {
                _maxResearchScrollX = 0;
                return;
            }

            // Group nodes by tier
            var tiers = new Dictionary<int, List<ResearchNode>>();
            foreach (var node in nodes)
            {
                if (!tiers.ContainsKey(node.Tier))
                    tiers[node.Tier] = new List<ResearchNode>();
                tiers[node.Tier].Add(node);
            }

            // Calculate positions - each tier is a column
            int startX = RESEARCH_TREE_LEFT + 15;
            int startY = RESEARCH_TREE_TOP + 30;
            int tierSpacing = RESEARCH_NODE_SPACING_X + 15;
            int maxX = startX;
            int treeViewHeight = RESEARCH_TREE_BOTTOM - RESEARCH_TREE_TOP - 60;

            foreach (var tier in tiers.OrderBy(t => t.Key))
            {
                int tierX = startX + (tier.Key - 1) * tierSpacing;
                int nodeCount = tier.Value.Count;
                int nodeSpacingY = Math.Min(RESEARCH_NODE_SPACING_Y, (treeViewHeight - nodeCount * RESEARCH_NODE_HEIGHT) / Math.Max(1, nodeCount - 1) + RESEARCH_NODE_HEIGHT);
                int totalHeight = nodeCount * RESEARCH_NODE_HEIGHT + (nodeCount - 1) * (nodeSpacingY - RESEARCH_NODE_HEIGHT);
                int tierStartY = startY + (treeViewHeight - totalHeight) / 2;  // Center vertically

                for (int i = 0; i < tier.Value.Count; i++)
                {
                    var node = tier.Value[i];
                    int nodeY = tierStartY + i * nodeSpacingY;
                    _researchNodePositions[node.Id] = new Vector2(tierX, nodeY);
                }

                maxX = Math.Max(maxX, tierX + RESEARCH_NODE_WIDTH);
            }

            // Calculate max scroll (how far right the tree extends beyond visible area)
            int visibleWidth = RESEARCH_TREE_RIGHT - RESEARCH_TREE_LEFT - 30;
            _maxResearchScrollX = Math.Max(0, maxX - RESEARCH_TREE_LEFT - visibleWidth + 20);
        }

        private void DrawResearchTreeConnections(ResearchCategory category)
        {
            var nodes = GameServices.Research.GetNodesByCategory(category);

            foreach (var node in nodes)
            {
                if (!_researchNodePositions.ContainsKey(node.Id)) continue;
                Vector2 toPos = _researchNodePositions[node.Id];

                // Apply scroll offset
                toPos.X -= _researchTreeScrollX;

                // Draw lines from prerequisites to this node
                foreach (var prereqId in node.Prerequisites)
                {
                    if (_researchNodePositions.ContainsKey(prereqId))
                    {
                        Vector2 fromPos = _researchNodePositions[prereqId];
                        fromPos.X -= _researchTreeScrollX;  // Apply scroll offset

                        // Skip if both ends are outside visible area
                        if ((fromPos.X + RESEARCH_NODE_WIDTH < RESEARCH_TREE_LEFT && toPos.X + RESEARCH_NODE_WIDTH < RESEARCH_TREE_LEFT) ||
                            (fromPos.X > RESEARCH_TREE_RIGHT && toPos.X > RESEARCH_TREE_RIGHT))
                            continue;

                        // Determine line color based on prerequisite completion
                        var prereqNode = GameServices.Research.GetNode(prereqId);
                        Color lineColor = prereqNode?.State == ResearchState.Completed
                            ? new Color(80, 140, 80)
                            : new Color(60, 65, 80);

                        // Draw L-shaped connector
                        Vector2 fromCenter = new Vector2(fromPos.X + RESEARCH_NODE_WIDTH, fromPos.Y + RESEARCH_NODE_HEIGHT / 2);
                        Vector2 toCenter = new Vector2(toPos.X, toPos.Y + RESEARCH_NODE_HEIGHT / 2);

                        // Horizontal then vertical
                        float midX = (fromCenter.X + toCenter.X) / 2;
                        DrawResearchLine(fromCenter, new Vector2(midX, fromCenter.Y), lineColor);
                        DrawResearchLine(new Vector2(midX, fromCenter.Y), new Vector2(midX, toCenter.Y), lineColor);
                        DrawResearchLine(new Vector2(midX, toCenter.Y), toCenter, lineColor);
                    }
                }
            }
        }

        private void DrawResearchLine(Vector2 from, Vector2 to, Color color)
        {
            Vector2 diff = to - from;
            float length = diff.Length();
            if (length < 1) return;

            int steps = Math.Max(1, (int)(length / 2));
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 pos = Vector2.Lerp(from, to, t);
                _spriteBatch.Draw(_pixelTexture, new Rectangle((int)pos.X - 1, (int)pos.Y - 1, 3, 3), color);
            }
        }

        private void DrawResearchTreeNodes(ResearchCategory category)
        {
            var nodes = GameServices.Research.GetNodesByCategory(category);
            var mState = Mouse.GetState();

            foreach (var node in nodes)
            {
                if (!_researchNodePositions.ContainsKey(node.Id)) continue;
                Vector2 basePos = _researchNodePositions[node.Id];

                // Apply scroll offset
                float drawX = basePos.X - _researchTreeScrollX;
                float drawY = basePos.Y;

                // Skip if completely outside visible tree area
                if (drawX + RESEARCH_NODE_WIDTH < RESEARCH_TREE_LEFT || drawX > RESEARCH_TREE_RIGHT)
                    continue;

                Rectangle nodeRect = new Rectangle((int)drawX, (int)drawY, RESEARCH_NODE_WIDTH, RESEARCH_NODE_HEIGHT);
                bool isHovered = nodeRect.Contains(mState.X, mState.Y) &&
                                 mState.X >= RESEARCH_TREE_LEFT && mState.X <= RESEARCH_TREE_RIGHT;
                bool isSelected = node.Id == _selectedResearchNodeId;

                // Determine colors based on state
                Color bgColor, borderColor, textColor;
                switch (node.State)
                {
                    case ResearchState.Completed:
                        bgColor = new Color(30, 60, 30);
                        borderColor = Color.LimeGreen;
                        textColor = Color.LimeGreen;
                        break;
                    case ResearchState.InProgress:
                        float pulse = (float)(Math.Sin(_totalTime * 4) * 0.3 + 0.7);
                        bgColor = new Color(30, 50, 60);
                        borderColor = Color.Cyan * pulse;
                        textColor = Color.Cyan;
                        break;
                    case ResearchState.Available:
                        bgColor = new Color(40, 48, 58);
                        borderColor = Color.White;
                        textColor = Color.White;
                        break;
                    default:  // Locked
                        bgColor = new Color(28, 32, 40);
                        borderColor = new Color(70, 75, 90);
                        textColor = new Color(100, 100, 110);
                        break;
                }

                // Highlight if hovered or selected
                if (isSelected)
                {
                    bgColor = new Color(55, 60, 80);
                    borderColor = Color.Yellow;
                }
                else if (isHovered)
                {
                    bgColor = new Color(48, 55, 70);
                    borderColor = new Color(borderColor.R + 40, borderColor.G + 40, borderColor.B + 40);
                }

                // Draw node background with slight gradient effect (darker at top)
                _spriteBatch.Draw(_pixelTexture, nodeRect, bgColor);
                _spriteBatch.Draw(_pixelTexture, new Rectangle((int)drawX, (int)drawY, RESEARCH_NODE_WIDTH, 3), bgColor * 0.7f);

                // Draw border (thicker if selected)
                int borderThickness = isSelected ? 3 : (isHovered ? 2 : 1);
                DrawBorder(nodeRect, borderColor, borderThickness);

                // Draw tier indicator - top left (gold squares as tier stars)
                int starSize = 6;
                int starSpacing = 8;
                for (int i = 0; i < Math.Min(node.Tier, 5); i++)
                {
                    int starX = (int)drawX + 4 + (i * starSpacing);
                    int starY = (int)drawY + 4;
                    // Draw a small diamond/star shape
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(starX + 1, starY, starSize - 2, starSize), Color.Gold);
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(starX, starY + 1, starSize, starSize - 2), Color.Gold);
                }

                // Draw node name - allow two lines if needed
                string displayName = node.Name;
                Vector2 nameSize = _font.MeasureString(displayName);
                int maxNameWidth = RESEARCH_NODE_WIDTH - 12;

                if (nameSize.X <= maxNameWidth)
                {
                    // Single line, centered vertically
                    _spriteBatch.DrawString(_font, displayName, new Vector2(drawX + 6, drawY + 20), textColor);
                }
                else
                {
                    // Try to split into two lines
                    string[] words = displayName.Split(' ');
                    string line1 = "";
                    string line2 = "";

                    foreach (var word in words)
                    {
                        string testLine = string.IsNullOrEmpty(line1) ? word : line1 + " " + word;
                        if (_font.MeasureString(testLine).X <= maxNameWidth)
                        {
                            line1 = testLine;
                        }
                        else
                        {
                            line2 += (string.IsNullOrEmpty(line2) ? "" : " ") + word;
                        }
                    }

                    // Truncate line2 if still too long
                    if (_font.MeasureString(line2).X > maxNameWidth)
                    {
                        while (_font.MeasureString(line2 + "..").X > maxNameWidth && line2.Length > 2)
                            line2 = line2.Substring(0, line2.Length - 1);
                        line2 += "..";
                    }

                    _spriteBatch.DrawString(_font, line1, new Vector2(drawX + 6, drawY + 16), textColor);
                    if (!string.IsNullOrEmpty(line2))
                        _spriteBatch.DrawString(_font, line2, new Vector2(drawX + 6, drawY + 32), textColor * 0.9f);
                }

                // Progress bar for in-progress research
                if (node.State == ResearchState.InProgress)
                {
                    int barY = (int)drawY + RESEARCH_NODE_HEIGHT - 10;
                    int barWidth = RESEARCH_NODE_WIDTH - 12;
                    float progress = node.ProgressPercent / 100f;

                    _spriteBatch.Draw(_pixelTexture, new Rectangle((int)drawX + 6, barY, barWidth, 6), new Color(30, 30, 35));
                    _spriteBatch.Draw(_pixelTexture, new Rectangle((int)drawX + 6, barY, (int)(barWidth * progress), 6), Color.Cyan);

                    // Show percentage
                    string pctStr = $"{node.ProgressPercent:F0}%";
                    Vector2 pctSize = _font.MeasureString(pctStr);
                    _spriteBatch.DrawString(_font, pctStr, new Vector2(drawX + RESEARCH_NODE_WIDTH - pctSize.X - 6, drawY + 3), Color.Cyan * 0.8f);
                }

                // Status icon - top right
                if (node.State == ResearchState.Completed)
                {
                    _spriteBatch.DrawString(_font, "+", new Vector2(drawX + RESEARCH_NODE_WIDTH - 16, drawY + 3), Color.LimeGreen);
                }
                else if (node.State == ResearchState.Locked)
                {
                    _spriteBatch.DrawString(_font, "x", new Vector2(drawX + RESEARCH_NODE_WIDTH - 14, drawY + 3), new Color(80, 80, 90));
                }
            }
        }

        private void DrawResearchDetailsPanel(ResearchNode node, int x, int y)
        {
            // Name and tier with background
            _spriteBatch.Draw(_pixelTexture, new Rectangle(x - 5, y - 5, 380, 40), new Color(35, 40, 50));
            _spriteBatch.DrawString(_font, node.Name, new Vector2(x, y), Color.Yellow);
            _spriteBatch.DrawString(_font, $"Tier {node.Tier} - {node.Category}", new Vector2(x, y + 18), Color.Gray);

            // Description (word wrap) - wider for better readability
            int lineY = y + 50;
            var words = node.Description.Split(' ');
            string line = "";
            int maxWidth = 365;
            foreach (var word in words)
            {
                if (_font.MeasureString(line + word).X > maxWidth)
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
                    string checkMark = completed ? "+" : "x";
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
        // PAUSE MENU UI DRAWING
        // ============================================

        private void DrawPauseMenuUI()
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            var mState = Mouse.GetState();
            var mousePos = new Point(mState.X, mState.Y);

            // Darken background
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.7f);

            // Menu panel
            int menuWidth = 350;
            int menuHeight = 400;
            int menuX = (1280 - menuWidth) / 2;
            int menuY = (720 - menuHeight) / 2;

            // Panel background
            _spriteBatch.Draw(_pixelTexture, new Rectangle(menuX, menuY, menuWidth, menuHeight), new Color(30, 30, 40));

            // Panel border
            _spriteBatch.Draw(_pixelTexture, new Rectangle(menuX, menuY, menuWidth, 3), Color.Gold);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(menuX, menuY + menuHeight - 3, menuWidth, 3), Color.Gold);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(menuX, menuY, 3, menuHeight), Color.Gold);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(menuX + menuWidth - 3, menuY, 3, menuHeight), Color.Gold);

            // Button dimensions
            int buttonWidth = 250;
            int buttonHeight = 40;
            int buttonX = menuX + (menuWidth - buttonWidth) / 2;
            int buttonSpacing = 55;
            int startY = menuY + 70;

            if (_pauseMenuMode == 0)
            {
                // Main menu
                string title = "PAUSED";
                Vector2 titleSize = _font.MeasureString(title);
                _spriteBatch.DrawString(_font, title, new Vector2(menuX + (menuWidth - titleSize.X) / 2, menuY + 20), Color.Gold);

                DrawPauseButton(new Rectangle(buttonX, startY, buttonWidth, buttonHeight), "RESUME", mousePos);
                DrawPauseButton(new Rectangle(buttonX, startY + buttonSpacing, buttonWidth, buttonHeight), "SAVE GAME", mousePos);
                DrawPauseButton(new Rectangle(buttonX, startY + buttonSpacing * 2, buttonWidth, buttonHeight), "LOAD GAME", mousePos);
                DrawPauseButton(new Rectangle(buttonX, startY + buttonSpacing * 3, buttonWidth, buttonHeight), "SETTINGS", mousePos);
                DrawPauseButton(new Rectangle(buttonX, startY + buttonSpacing * 4, buttonWidth, buttonHeight), "QUIT GAME", mousePos);

                _spriteBatch.DrawString(_font, "Press ESC to resume", new Vector2(menuX + 85, menuY + menuHeight - 35), Color.Gray);
            }
            else if (_pauseMenuMode == 1)
            {
                // Save slot selection
                string title = "SAVE GAME";
                Vector2 titleSize = _font.MeasureString(title);
                _spriteBatch.DrawString(_font, title, new Vector2(menuX + (menuWidth - titleSize.X) / 2, menuY + 20), Color.Gold);

                var slots = SaveSystem.GetAllSlotInfo();
                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    string slotText = $"Slot {i + 1}: {slot.GetDisplayText()}";
                    DrawPauseButton(new Rectangle(buttonX, startY + buttonSpacing * i, buttonWidth, buttonHeight), slotText, mousePos);
                }

                DrawPauseButton(new Rectangle(buttonX, startY + buttonSpacing * 4, buttonWidth, buttonHeight), "< BACK", mousePos);

                _spriteBatch.DrawString(_font, "Select a slot to save", new Vector2(menuX + 95, menuY + menuHeight - 35), Color.Gray);
            }
            else if (_pauseMenuMode == 2)
            {
                // Load slot selection
                string title = "LOAD GAME";
                Vector2 titleSize = _font.MeasureString(title);
                _spriteBatch.DrawString(_font, title, new Vector2(menuX + (menuWidth - titleSize.X) / 2, menuY + 20), Color.Gold);

                var slots = SaveSystem.GetAllSlotInfo();
                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    string slotText = $"Slot {i + 1}: {slot.GetDisplayText()}";
                    Color slotColor = slot.Exists && !slot.IsCorrupted ? Color.White : Color.Gray;
                    DrawPauseButtonWithColor(new Rectangle(buttonX, startY + buttonSpacing * i, buttonWidth, buttonHeight), slotText, mousePos, slot.Exists && !slot.IsCorrupted);
                }

                DrawPauseButton(new Rectangle(buttonX, startY + buttonSpacing * 4, buttonWidth, buttonHeight), "< BACK", mousePos);

                _spriteBatch.DrawString(_font, "Select a slot to load", new Vector2(menuX + 95, menuY + menuHeight - 35), Color.Gray);
            }

            _spriteBatch.End();
        }

        private void DrawPauseButton(Rectangle rect, string text, Point mousePos)
        {
            DrawPauseButtonWithColor(rect, text, mousePos, true);
        }

        private void DrawPauseButtonWithColor(Rectangle rect, string text, Point mousePos, bool enabled)
        {
            bool isHovered = rect.Contains(mousePos) && enabled;

            // Button background
            Color bgColor = !enabled ? new Color(30, 30, 35) :
                           isHovered ? new Color(60, 60, 80) : new Color(40, 40, 55);
            _spriteBatch.Draw(_pixelTexture, rect, bgColor);

            // Button border
            Color borderColor = !enabled ? Color.DimGray :
                               isHovered ? Color.Gold : Color.DarkGoldenrod;
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, 2), borderColor);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), borderColor);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, 2, rect.Height), borderColor);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), borderColor);

            // Button text (use integer positions)
            Vector2 textSize = _font.MeasureString(text);
            int textX = rect.X + (int)((rect.Width - textSize.X) / 2);
            int textY = rect.Y + (int)((rect.Height - textSize.Y) / 2);
            Color textColor = !enabled ? Color.DimGray :
                             isHovered ? Color.White : Color.LightGray;
            _spriteBatch.DrawString(_font, text, new Vector2(textX, textY), textColor);
        }

        // ============================================
        // BODY PANEL UI DRAWING
        // ============================================

        private void DrawBodyPanelUI()
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

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

                // Check if this is a valid drop target when dragging
                bool isValidDropTarget = _isDragging && _draggedItem != null && CanDropItemOnPart(_draggedItem, part);

                // Background - highlight if hovering with dragged item or valid target
                bool isHoverTarget = _isDragging && _hoverBodyPart == part;
                bool isSelected = _selectedBodyPartId == part.Id;
                Color partBg;
                if (isHoverTarget && isValidDropTarget)
                {
                    partBg = new Color(40, 100, 40);  // Bright green when hovering valid target
                }
                else if (isHoverTarget && !isValidDropTarget && _draggedItem != null)
                {
                    partBg = new Color(100, 40, 40);  // Red when hovering invalid target
                }
                else if (isValidDropTarget)
                {
                    partBg = new Color(50, 70, 50);   // Subtle green for valid targets
                }
                else if (isSelected)
                {
                    partBg = UITheme.SelectionBackground;
                }
                else
                {
                    partBg = new Color(40, 45, 55);   // Normal background
                }
                _spriteBatch.Draw(_pixelTexture, partRect, partBg);

                // Draw border for valid drop targets (always visible when dragging)
                if (_isDragging && _draggedItem != null)
                {
                    if (isValidDropTarget)
                    {
                        Color borderColor = isHoverTarget ? Color.LimeGreen : new Color(80, 180, 80);
                        int borderWidth = isHoverTarget ? 2 : 1;
                        DrawBorder(partRect, borderColor, borderWidth);
                    }
                    else if (isHoverTarget)
                    {
                        DrawBorder(partRect, Color.Red, 2);
                    }
                }

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
                    string twoH = part.IsHoldingTwoHandedWeapon ? " (2H)" : "";
                    statusText += $"[{part.EquippedItem.Name}{twoH}]";
                }
                else if (part.CanEquipWeapon)
                {
                    statusText += "(empty hand)";
                }

                Color statusColor = (part.IsBleeding || part.IsInfected) ? UITheme.TextDanger : UITheme.TextSecondary;
                if (part.EquippedItem != null) statusColor = UITheme.TextSuccess;

                _spriteBatch.DrawString(_font, statusText, new Vector2(partX + 8, statusY), statusColor);

                row++;
                if (row >= 10) { row = 0; col++; }
            }

            // Draw dragged item following cursor with type indicator
            if (_isDragging && _draggedItem != null)
            {
                // Determine item type color
                Color typeColor = _draggedItem.Category switch
                {
                    ItemCategory.Weapon => Color.Orange,
                    ItemCategory.Armor => Color.Cyan,
                    ItemCategory.Consumable => Color.LimeGreen,
                    _ => _draggedItem.Definition?.IsMedical == true ? Color.Red : Color.White
                };

                string typeLabel = _draggedItem.Category switch
                {
                    ItemCategory.Weapon => _draggedItem.Definition?.IsTwoHanded == true ? "[2H WEAPON]" : "[WEAPON]",
                    ItemCategory.Armor => "[ARMOR]",
                    ItemCategory.Consumable => _draggedItem.Definition?.IsMedical == true ? "[MEDICAL]" : "[CONSUMABLE]",
                    _ => ""
                };

                Rectangle dragRect = new Rectangle((int)_dragPosition.X - 60, (int)_dragPosition.Y - 10, 140, 38);
                _spriteBatch.Draw(_pixelTexture, dragRect, new Color(40, 45, 55) * 0.95f);
                DrawBorder(dragRect, typeColor, 2);
                _spriteBatch.DrawString(_font, _draggedItem.Name, new Vector2(dragRect.X + 5, dragRect.Y + 4), Color.White);
                _spriteBatch.DrawString(_font, typeLabel, new Vector2(dragRect.X + 5, dragRect.Y + 20), typeColor);
            }

            // Instructions at bottom - context-sensitive
            string instructions;
            if (_isDragging && _draggedItem != null)
            {
                instructions = _draggedItem.Category switch
                {
                    ItemCategory.Weapon => "Drop on HANDS (highlighted green)",
                    ItemCategory.Armor => "Drop on matching BODY PART (highlighted green)",
                    _ => _draggedItem.Definition?.IsMedical == true
                        ? "Drop on INJURED/BLEEDING parts (highlighted green)"
                        : "Drop on DAMAGED parts to heal"
                };
            }
            else
            {
                instructions = "Drag items to body parts  |  WEAPONS → Hands  |  ARMOR → Body  |  MEDICAL → Injuries  |  Right-click to unequip";
            }
            Vector2 instrSize = _font.MeasureString(instructions);
            _spriteBatch.DrawString(_font, instructions,
                new Vector2(panelX + (panelWidth - instrSize.X) / 2, panelY + panelHeight - 25),
                _isDragging ? Color.Yellow : UITheme.TextSecondary);

            _spriteBatch.End();
        }

        // ============================================
        // GRIP SELECTION DIALOG
        // ============================================

        private void DrawGripSelectionDialog()
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            var mState = Mouse.GetState();

            // Dim everything else more
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.5f);

            // Dialog box
            int dialogWidth = 400;
            int dialogHeight = 280;
            int dialogX = (1280 - dialogWidth) / 2;
            int dialogY = (720 - dialogHeight) / 2;

            Rectangle dialogRect = new Rectangle(dialogX, dialogY, dialogWidth, dialogHeight);
            _spriteBatch.Draw(_pixelTexture, dialogRect, UITheme.PanelBackground);
            DrawBorder(dialogRect, UITheme.SelectionBorder, 3);

            // Header
            _spriteBatch.Draw(_pixelTexture, new Rectangle(dialogX, dialogY, dialogWidth, 35), UITheme.PanelHeader);
            _spriteBatch.DrawString(_font, "CHOOSE GRIP STYLE", new Vector2(dialogX + 100, dialogY + 8), UITheme.TextHighlight);

            // Item name
            string itemName = _gripDialogItem?.GetDisplayName() ?? "Weapon";
            Vector2 itemNameSize = _font.MeasureString(itemName);
            _spriteBatch.DrawString(_font, itemName, new Vector2(dialogX + (dialogWidth - itemNameSize.X) / 2, dialogY + 45), GetItemQualityColor(_gripDialogItem?.Quality ?? ItemQuality.Normal));

            // Get weapon stats
            var def = _gripDialogItem?.Definition;
            float baseDamage = _gripDialogItem?.GetEffectiveDamage() ?? 0;
            float twoHandBonus = def?.TwoHandDamageBonus ?? 0.25f;
            float strBonus = _player.Stats.Attributes.STR * 0.02f;
            float twoHandDamage = baseDamage * (1f + twoHandBonus + strBonus);

            int optionY = dialogY + 75;
            int optionHeight = 70;

            // Option 1: One-Handed
            Rectangle opt1Rect = new Rectangle(dialogX + 20, optionY, dialogWidth - 40, optionHeight);
            bool opt1Hover = opt1Rect.Contains(mState.X, mState.Y);
            bool opt1Selected = _gripDialogSelection == 0;

            Color opt1Bg = opt1Selected ? UITheme.SelectionBackground : (opt1Hover ? UITheme.HoverBackground : UITheme.ButtonNormal);
            _spriteBatch.Draw(_pixelTexture, opt1Rect, opt1Bg);
            DrawBorder(opt1Rect, opt1Selected ? UITheme.SelectionBorder : UITheme.PanelBorder, opt1Selected ? 2 : 1);

            _spriteBatch.DrawString(_font, "ONE-HANDED", new Vector2(opt1Rect.X + 15, opt1Rect.Y + 8), UITheme.TextHighlight);
            _spriteBatch.DrawString(_font, $"Damage: {baseDamage:F1}", new Vector2(opt1Rect.X + 15, opt1Rect.Y + 28), UITheme.TextPrimary);
            _spriteBatch.DrawString(_font, "Can equip shield or second weapon", new Vector2(opt1Rect.X + 15, opt1Rect.Y + 48), UITheme.TextSecondary);

            // Option 2: Two-Handed
            optionY += optionHeight + 10;
            Rectangle opt2Rect = new Rectangle(dialogX + 20, optionY, dialogWidth - 40, optionHeight);
            bool opt2Hover = opt2Rect.Contains(mState.X, mState.Y);
            bool opt2Selected = _gripDialogSelection == 1;

            Color opt2Bg = opt2Selected ? UITheme.SelectionBackground : (opt2Hover ? UITheme.HoverBackground : UITheme.ButtonNormal);
            _spriteBatch.Draw(_pixelTexture, opt2Rect, opt2Bg);
            DrawBorder(opt2Rect, opt2Selected ? UITheme.SelectionBorder : UITheme.PanelBorder, opt2Selected ? 2 : 1);

            _spriteBatch.DrawString(_font, "TWO-HANDED", new Vector2(opt2Rect.X + 15, opt2Rect.Y + 8), UITheme.TextHighlight);
            _spriteBatch.DrawString(_font, $"Damage: {twoHandDamage:F1}", new Vector2(opt2Rect.X + 15, opt2Rect.Y + 28), UITheme.TextSuccess);
            string bonusText = $"+{(twoHandBonus + strBonus) * 100:F0}% from two-handing (+STR)";
            _spriteBatch.DrawString(_font, bonusText, new Vector2(opt2Rect.X + 130, opt2Rect.Y + 28), UITheme.TextSuccess);
            _spriteBatch.DrawString(_font, "Uses both hands, more powerful swings", new Vector2(opt2Rect.X + 15, opt2Rect.Y + 48), UITheme.TextSecondary);

            // Buttons
            int buttonY = dialogY + dialogHeight - 45;
            Rectangle confirmBtn = new Rectangle(dialogX + 50, buttonY, 120, 32);
            Rectangle cancelBtn = new Rectangle(dialogX + dialogWidth - 170, buttonY, 120, 32);

            DrawButton(confirmBtn, "EQUIP", mState, true);
            DrawButton(cancelBtn, "Cancel", mState, true);

            // Keyboard hints
            _spriteBatch.DrawString(_font, "[W/S] Select  [Enter] Confirm  [Esc] Cancel",
                new Vector2(dialogX + 55, buttonY + 38), UITheme.TextSecondary * 0.7f);

            _spriteBatch.End();
        }

        private void UpdateGripSelectionDialog(KeyboardState kState, MouseState mState, bool leftClick)
        {
            // Close on ESC
            if (kState.IsKeyDown(Keys.Escape) && _prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                _gripDialogOpen = false;
                _gripDialogItem = null;
                _gripDialogTargetPart = null;
                return;
            }

            // Navigate options
            if (kState.IsKeyDown(Keys.W) && _prevKeyboardState.IsKeyUp(Keys.W))
            {
                _gripDialogSelection = 0;
            }
            if (kState.IsKeyDown(Keys.S) && _prevKeyboardState.IsKeyUp(Keys.S))
            {
                _gripDialogSelection = 1;
            }

            // Dialog dimensions (must match Draw)
            int dialogWidth = 400;
            int dialogHeight = 280;
            int dialogX = (1280 - dialogWidth) / 2;
            int dialogY = (720 - dialogHeight) / 2;
            int optionY = dialogY + 75;
            int optionHeight = 70;

            // Click on options
            Rectangle opt1Rect = new Rectangle(dialogX + 20, optionY, dialogWidth - 40, optionHeight);
            Rectangle opt2Rect = new Rectangle(dialogX + 20, optionY + optionHeight + 10, dialogWidth - 40, optionHeight);

            if (leftClick)
            {
                if (opt1Rect.Contains(mState.X, mState.Y))
                {
                    _gripDialogSelection = 0;
                }
                else if (opt2Rect.Contains(mState.X, mState.Y))
                {
                    _gripDialogSelection = 1;
                }

                // Confirm button
                int buttonY = dialogY + dialogHeight - 45;
                Rectangle confirmBtn = new Rectangle(dialogX + 50, buttonY, 120, 32);
                Rectangle cancelBtn = new Rectangle(dialogX + dialogWidth - 170, buttonY, 120, 32);

                if (confirmBtn.Contains(mState.X, mState.Y))
                {
                    ConfirmGripSelection();
                }
                else if (cancelBtn.Contains(mState.X, mState.Y))
                {
                    _gripDialogOpen = false;
                    _gripDialogItem = null;
                    _gripDialogTargetPart = null;
                }
            }

            // Confirm with Enter
            if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
            {
                ConfirmGripSelection();
            }
        }

        private void ConfirmGripSelection()
        {
            if (_gripDialogItem == null) return;

            bool twoHanded = _gripDialogSelection == 1;

            if (twoHanded)
            {
                // Two-handed: use Body's equip method
                if (!_player.Stats.Body.CanEquipWeapon(_gripDialogItem))
                {
                    ShowNotification("Need 2 free hands for two-handed grip!");
                    return;
                }

                if (_player.Stats.Body.EquipWeaponToHand(_gripDialogItem, forceTwoHand: true))
                {
                    _player.Stats.Inventory.RemoveItem(_gripDialogItem.ItemDefId, 1);
                    _player.Stats.Inventory.SetGripMode(EquipSlot.TwoHand, GripMode.TwoHand);
                    ShowNotification($"Equipped {_gripDialogItem.Name} (Two-Handed)");
                }
            }
            else
            {
                // One-handed: equip to the target part
                if (_gripDialogTargetPart != null)
                {
                    // Unequip current if any
                    if (_gripDialogTargetPart.EquippedItem != null)
                    {
                        if (_gripDialogTargetPart.IsHoldingTwoHandedWeapon)
                        {
                            var old = _player.Stats.Body.UnequipWeaponFromHands(_gripDialogTargetPart.EquippedItem);
                            if (old != null) _player.Stats.Inventory.TryAddItem(old.ItemDefId, old.StackCount, old.Quality);
                        }
                        else
                        {
                            var old = _gripDialogTargetPart.UnequipItem();
                            if (old != null) _player.Stats.Inventory.TryAddItem(old.ItemDefId, old.StackCount, old.Quality);
                        }
                    }

                    _gripDialogTargetPart.EquipItem(_gripDialogItem);
                    _player.Stats.Inventory.RemoveItem(_gripDialogItem.ItemDefId, 1);
                    ShowNotification($"Equipped {_gripDialogItem.Name} (One-Handed) to {_gripDialogTargetPart.Name}");
                }
            }

            // Close dialog
            _gripDialogOpen = false;
            _gripDialogItem = null;
            _gripDialogTargetPart = null;
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
                // Two-handed weapons need 2 free hands
                int handsNeeded = item.Definition?.HandsRequired ?? 1;
                if (handsNeeded >= 2)
                {
                    return _player.Stats.Body.CanEquipWeapon(item);
                }
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
                        new Rectangle(x + w / 2 - glowSize / 2, y + h / 2 - glowSize / 2, glowSize, glowSize),
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
            _spriteBatch.DrawString(_font, "[Click/Esc to close]", new Vector2(x + 5, y + panelHeight - 18), Color.Gray);
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
            _spriteBatch.DrawString(_font, $"Value: {worldItem.Item.Value * worldItem.Item.StackCount} gold", new Vector2(x + 5, lineY), Color.Gold);
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

            // Position combat info below minimap
            int minimapBottom = (_minimapExpanded ? MINIMAP_SIZE_LARGE : MINIMAP_SIZE_SMALL) + 15;
            int combatInfoY = minimapBottom + 5;

            // Calculate panel height based on content (add space for EP if player has it)
            bool hasEP = _player.Stats.EsperPoints > 0;
            int panelHeight = _combat.IsPlayerTurn ? (hasEP ? 110 : 90) : 30;

            // Combat info panel background
            _spriteBatch.Draw(_pixelTexture, new Rectangle(10, combatInfoY, 230, panelHeight), Color.Black * 0.85f);
            DrawBorder(new Rectangle(10, combatInfoY, 230, panelHeight), combatColor * 0.6f, 2);

            // Turn indicator
            _spriteBatch.DrawString(_font, $"COMBAT - {turnText}", new Vector2(15, combatInfoY + 5), combatColor);

            if (_combat.IsPlayerTurn)
            {
                // AP display with visual bar
                int apY = combatInfoY + 28;
                string apText = $"AP: {_combat.PlayerActionPoints}/{_combat.PlayerMaxActionPoints}";
                _spriteBatch.DrawString(_font, apText, new Vector2(15, apY), Color.Cyan);

                // AP bar
                int barX = 90;
                int barWidth = 130;
                float apPercent = (float)_combat.PlayerActionPoints / _combat.PlayerMaxActionPoints;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(barX, apY + 2, barWidth, 12), new Color(30, 30, 40));
                _spriteBatch.Draw(_pixelTexture, new Rectangle(barX, apY + 2, (int)(barWidth * apPercent), 12), Color.Cyan);
                DrawBorder(new Rectangle(barX, apY + 2, barWidth, 12), Color.Cyan * 0.5f, 1);

                // MP display with visual bar
                int mpY = combatInfoY + 48;
                string mpText = $"MP: {_combat.PlayerMovementPoints}/{_combat.PlayerMaxMovementPoints}";
                _spriteBatch.DrawString(_font, mpText, new Vector2(15, mpY), Color.LimeGreen);

                // MP bar
                float mpPercent = (float)_combat.PlayerMovementPoints / Math.Max(1, _combat.PlayerMaxMovementPoints);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(barX, mpY + 2, barWidth, 12), new Color(30, 30, 40));
                _spriteBatch.Draw(_pixelTexture, new Rectangle(barX, mpY + 2, (int)(barWidth * mpPercent), 12), Color.LimeGreen);
                DrawBorder(new Rectangle(barX, mpY + 2, barWidth, 12), Color.LimeGreen * 0.5f, 1);

                // EP display (if player has Esper Points)
                if (_player.Stats.EsperPoints > 0)
                {
                    int epY = combatInfoY + 68;
                    string epText = $"EP: {_player.Stats.CurrentEsperPoints}/{_player.Stats.EsperPoints}";
                    _spriteBatch.DrawString(_font, epText, new Vector2(15, epY), Color.Magenta);

                    // EP bar
                    float epPercent = (float)_player.Stats.CurrentEsperPoints / _player.Stats.EsperPoints;
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(barX, epY + 2, barWidth, 12), new Color(30, 30, 40));
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(barX, epY + 2, (int)(barWidth * epPercent), 12), Color.Magenta);
                    DrawBorder(new Rectangle(barX, epY + 2, barWidth, 12), Color.Magenta * 0.5f, 1);
                }

                // Combat help text at bottom center
                string combatHelp = "Space: End Turn | Tab: Target | F: Melee | E: Escape";
                Vector2 helpSize = _font.MeasureString(combatHelp);
                int helpX = (int)(640 - helpSize.X / 2);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(helpX - 8, 648, (int)helpSize.X + 16, 18), Color.Black * 0.85f);
                _spriteBatch.DrawString(_font, combatHelp, new Vector2(helpX, 650), Color.Yellow);
            }

            // Combat zone info (top right corner)
            var zoneInfo = _combat.GetZoneInfo();
            var escapeInfo = _combat.GetEscapeStatus();

            _spriteBatch.Draw(_pixelTexture, new Rectangle(1080, 130, 190, 70), Color.Black * 0.7f);

            // Zone radius
            string zoneText = zoneInfo.expanded
                ? $"Zone: {zoneInfo.current}/{zoneInfo.max} (EXPANDED!)"
                : $"Zone: {zoneInfo.current}/{zoneInfo.max}";
            Color zoneTextColor = zoneInfo.expanded ? Color.OrangeRed : Color.White;
            _spriteBatch.DrawString(_font, zoneText, new Vector2(1085, 135), zoneTextColor);

            // Escape attempts
            Color attemptColor = escapeInfo.attempts >= escapeInfo.maxAttempts ? Color.Red : Color.Yellow;
            _spriteBatch.DrawString(_font, $"Escape Tries: {escapeInfo.attempts}/{escapeInfo.maxAttempts}", new Vector2(1085, 155), attemptColor);

            // Escape/stealth status
            if (escapeInfo.isHidden)
            {
                _spriteBatch.DrawString(_font, "[HIDDEN - Can Escape!]", new Vector2(1085, 175), Color.LimeGreen);
            }
            else if (escapeInfo.canEscape)
            {
                _spriteBatch.DrawString(_font, "[Can Escape at Edge]", new Vector2(1085, 175), Color.Cyan);
            }
            else
            {
                _spriteBatch.DrawString(_font, "[No Escape]", new Vector2(1085, 175), Color.Gray);
            }

            // Selected enemy info with HIT CHANCE (BG3 style)
            if (_selectedEnemy != null && _selectedEnemy.IsAlive)
            {
                // Calculate distance for hit chance
                Point playerTile = new Point(
                    (int)(_player.Position.X / _world.TileSize),
                    (int)(_player.Position.Y / _world.TileSize)
                );
                Point enemyTile = _selectedEnemy.GetTilePosition(_world.TileSize);
                int distance = Pathfinder.GetDistance(playerTile, enemyTile);
                int attackRange = _player.Stats.GetAttackRange();

                // Check line of sight for ranged attacks
                bool hasLOS = attackRange <= 1 || _world.HasLineOfSight(playerTile, enemyTile);
                float cover = attackRange > 1 ? _world.GetCoverValue(playerTile, enemyTile) : 0f;

                // Get hit chances (apply cover penalty)
                float rangedHitChance = _player.Stats.GetHitChance(distance);
                if (cover > 0 && hasLOS)
                {
                    rangedHitChance *= (1f - cover * 0.5f);  // Up to 50% penalty at full cover
                }
                float meleeHitChance = _player.Stats.GetMeleeAccuracy();
                float meleeDamage = _player.Stats.GetMeleeDamageWithRangedWeapon();

                // Determine panel height based on info to display
                bool showCover = hasLOS && cover > 0 && distance <= attackRange;
                int targetPanelHeight = showCover ? 130 : 115;

                // Larger info panel
                _spriteBatch.Draw(_pixelTexture, new Rectangle(1080, 10, 190, targetPanelHeight), Color.Black * 0.7f);
                _spriteBatch.DrawString(_font, $"Target: {_selectedEnemy.Name}", new Vector2(1085, 15), Color.Yellow);
                _spriteBatch.DrawString(_font, $"HP: {_selectedEnemy.CurrentHealth:F0}/{_selectedEnemy.MaxHealth:F0}", new Vector2(1085, 32), Color.White);
                _spriteBatch.DrawString(_font, $"Distance: {distance} tiles", new Vector2(1085, 49), Color.LightGray);

                // Hit chance display (BG3 style)
                bool inRange = distance <= attackRange;
                bool canMelee = distance <= 1;

                // Ranged attack hit chance (with LOS check)
                Color rangedColor = GetHitChanceColor(rangedHitChance);
                string rangedText;
                if (!hasLOS)
                {
                    rangedText = "Attack: NO LINE OF SIGHT";
                    rangedColor = Color.Red;
                }
                else if (!inRange)
                {
                    rangedText = $"Attack: OUT OF RANGE ({attackRange})";
                    rangedColor = Color.Gray;
                }
                else
                {
                    rangedText = $"Attack: {rangedHitChance:P0} ({_player.Stats.Damage:F0} dmg)";
                }
                _spriteBatch.DrawString(_font, rangedText, new Vector2(1085, 66), rangedColor);

                // Show cover if applicable
                int nextY = 83;
                if (showCover)
                {
                    string coverText = cover >= 0.75f ? "FULL COVER" : cover >= 0.4f ? "HALF COVER" : "LIGHT COVER";
                    _spriteBatch.DrawString(_font, $"({coverText}: -{cover * 50:F0}%)", new Vector2(1085, 80), Color.Orange);
                    nextY = 97;
                }

                // Melee attack option (if adjacent)
                if (canMelee)
                {
                    Color meleeColor = GetHitChanceColor(meleeHitChance);
                    _spriteBatch.DrawString(_font, $"Melee[F]: {meleeHitChance:P0} ({meleeDamage:F0} dmg)", new Vector2(1085, nextY), meleeColor);
                }
                else
                {
                    _spriteBatch.DrawString(_font, "Melee[F]: Too far", new Vector2(1085, nextY), Color.Gray);
                }

                // Show behavior
                int behaviorY = nextY + 17;
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
                _spriteBatch.DrawString(_font, behaviorText, new Vector2(1085, behaviorY), behaviorColor);
            }

            // Combat log (above HUD bar)
            _spriteBatch.Draw(_pixelTexture, new Rectangle(10, 565, 380, 100), Color.Black * 0.75f);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(10, 565, 380, 1), new Color(60, 70, 90));  // Top border
            _spriteBatch.DrawString(_font, "Combat Log:", new Vector2(15, 568), Color.Gray);
            for (int i = 0; i < _combatLog.Count && i < 6; i++)  // Limit to 6 lines
            {
                _spriteBatch.DrawString(_font, _combatLog[i], new Vector2(15, 583 + i * 14), Color.White);
            }
        }

        private Color GetHitChanceColor(float hitChance)
        {
            if (hitChance >= 0.8f) return Color.LimeGreen;      // 80%+ = Green
            if (hitChance >= 0.6f) return Color.Yellow;         // 60-79% = Yellow
            if (hitChance >= 0.4f) return Color.Orange;         // 40-59% = Orange
            return Color.Red;                                    // <40% = Red
        }

        /// <summary>
        /// Draw hit chance tooltip when hovering over enemy (BG3 style)
        /// </summary>
        private void DrawEnemyHoverTooltip()
        {
            if (_hoverEnemy == null || !_hoverEnemy.IsAlive) return;
            if (!_combat.IsPlayerTurn) return;

            // Calculate hit chance
            Point playerTile = new Point(
                (int)(_player.Position.X / _world.TileSize),
                (int)(_player.Position.Y / _world.TileSize)
            );
            Point enemyTile = _hoverEnemy.GetTilePosition(_world.TileSize);
            int distance = Pathfinder.GetDistance(playerTile, enemyTile);

            float hitChance = _player.Stats.GetHitChance(distance);
            int attackRange = _player.Stats.GetAttackRange();
            bool inRange = distance <= attackRange;

            // Get mouse position for tooltip
            MouseState mState = Mouse.GetState();
            int tooltipX = mState.X + 15;
            int tooltipY = mState.Y - 40;

            // Clamp to screen
            if (tooltipX > 1150) tooltipX = mState.X - 130;
            if (tooltipY < 5) tooltipY = 5;

            // Draw tooltip background
            int tooltipWidth = 120;
            int tooltipHeight = inRange ? 55 : 35;
            _spriteBatch.Draw(_pixelTexture, new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight), Color.Black * 0.85f);
            DrawBorder(new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight), Color.White * 0.5f, 1);

            // Enemy name
            _spriteBatch.DrawString(_font, _hoverEnemy.Name, new Vector2(tooltipX + 5, tooltipY + 3), Color.Yellow);

            if (inRange)
            {
                // Hit chance with color
                Color hitColor = GetHitChanceColor(hitChance);
                string hitText = $"Hit: {hitChance:P0}";
                _spriteBatch.DrawString(_font, hitText, new Vector2(tooltipX + 5, tooltipY + 20), hitColor);

                // Damage
                _spriteBatch.DrawString(_font, $"Dmg: {_player.Stats.Damage:F0}", new Vector2(tooltipX + 5, tooltipY + 37), Color.White);
            }
            else
            {
                _spriteBatch.DrawString(_font, $"Out of range ({distance}/{attackRange})", new Vector2(tooltipX + 5, tooltipY + 20), Color.Red);
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

        // ============================================
        // FLOATING TEXT SYSTEM
        // ============================================

        private void SpawnFloatingText(Vector2 worldPos, string text, Color color, bool isCritical = false)
        {
            _floatingTexts.Add(new FloatingText
            {
                Position = worldPos,
                Text = text,
                Color = color,
                Timer = 0f,
                Duration = isCritical ? 1.5f : 1.0f,
                VelocityY = isCritical ? -80f : -60f,
                Scale = isCritical ? 1.3f : 1.0f,
                IsCritical = isCritical
            });
        }

        private void SpawnDamageNumber(Vector2 worldPos, float damage, bool isCritical = false, bool isPlayer = false)
        {
            Color color = isPlayer ? Color.Red : Color.Yellow;
            if (isCritical) color = Color.OrangeRed;

            string text = $"-{damage:F0}";
            if (isCritical) text = $"CRIT! {text}";

            // Add slight random offset so numbers don't stack
            var offset = new Vector2(
                (float)(_random.NextDouble() * 20 - 10),
                (float)(_random.NextDouble() * 10 - 5)
            );

            SpawnFloatingText(worldPos + offset, text, color, isCritical);
        }

        private void SpawnHealNumber(Vector2 worldPos, float heal)
        {
            string text = $"+{heal:F0}";
            SpawnFloatingText(worldPos, text, Color.LimeGreen, false);
        }

        private void SpawnMissText(Vector2 worldPos)
        {
            SpawnFloatingText(worldPos, "MISS", Color.Gray, false);
        }

        private void SpawnBlockText(Vector2 worldPos, float blocked)
        {
            SpawnFloatingText(worldPos, $"BLOCKED {blocked:F0}", Color.CornflowerBlue, false);
        }

        private void SpawnStatusText(Vector2 worldPos, string status)
        {
            SpawnFloatingText(worldPos, status, Color.Orchid, false);
        }

        /// <summary>
        /// Handle faction reputation changes - spawn floating text
        /// </summary>
        private void HandleReputationChanged(FactionType faction, int amount)
        {
            if (amount == 0) return;
            
            var def = GameServices.Factions.GetDefinition(faction);
            if (def == null) return;
            
            // Spawn floating rep text above player
            string text = amount > 0 ? $"+{amount} {def.Name}" : $"{amount} {def.Name}";
            Color color = amount > 0 ? new Color(100, 200, 255) : new Color(255, 150, 100);
            
            // Position slightly above player, with random offset to stack nicely
            Vector2 pos = _player.Position + new Vector2(
                (float)(_random.NextDouble() * 30 - 15),
                -20 - (float)(_random.NextDouble() * 10)
            );
            
            SpawnFloatingText(pos, text, color, Math.Abs(amount) >= 5);
        }

        private void UpdateFloatingTexts(float deltaTime)
        {
            for (int i = _floatingTexts.Count - 1; i >= 0; i--)
            {
                var ft = _floatingTexts[i];
                ft.Timer += deltaTime;
                ft.Position.Y += ft.VelocityY * deltaTime;
                ft.VelocityY *= 0.95f;  // Slow down
                _floatingTexts[i] = ft;

                if (ft.Timer >= ft.Duration)
                {
                    _floatingTexts.RemoveAt(i);
                }
            }
        }

        private void DrawFloatingTexts()
        {
            foreach (var ft in _floatingTexts)
            {
                // Since we're drawing with camera transform, use world position directly
                Vector2 drawPos = ft.Position;

                // Fade out in last 30% of duration
                float alpha = 1f;
                float fadeStart = ft.Duration * 0.7f;
                if (ft.Timer > fadeStart)
                {
                    alpha = 1f - ((ft.Timer - fadeStart) / (ft.Duration - fadeStart));
                }

                Color color = ft.Color * alpha;

                // Scale effect for criticals
                float scale = ft.Scale;
                if (ft.IsCritical && ft.Timer < 0.2f)
                {
                    scale = ft.Scale * (1f + (0.2f - ft.Timer) * 2f);  // Pop effect
                }

                // Draw shadow
                _spriteBatch.DrawString(_font, ft.Text, drawPos + new Vector2(1, 1), Color.Black * alpha * 0.5f,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                // Draw text
                _spriteBatch.DrawString(_font, ft.Text, drawPos, color,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        // ============================================
        // HIT FLASH SYSTEM
        // ============================================

        private void TriggerHitFlash(object entity)
        {
            _hitFlashTimers[entity] = HIT_FLASH_DURATION;
        }

        private void UpdateHitFlashes(float deltaTime)
        {
            var keysToRemove = new List<object>();
            var keys = _hitFlashTimers.Keys.ToList();

            foreach (var key in keys)
            {
                _hitFlashTimers[key] -= deltaTime;
                if (_hitFlashTimers[key] <= 0)
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _hitFlashTimers.Remove(key);
            }
        }

        private bool IsFlashing(object entity)
        {
            return _hitFlashTimers.ContainsKey(entity) && _hitFlashTimers[entity] > 0;
        }

        private Color GetFlashColor(object entity, Color baseColor)
        {
            if (IsFlashing(entity))
            {
                // Flash white then back to normal
                float t = _hitFlashTimers[entity] / HIT_FLASH_DURATION;
                return Color.Lerp(baseColor, Color.White, t * 0.8f);
            }
            return baseColor;
        }

        // ============================================
        // DEBUG CONSOLE
        // ============================================

        private void UpdateConsole(KeyboardState kState)
        {
            // Handle text input
            Keys[] pressedKeys = kState.GetPressedKeys();

            foreach (Keys key in pressedKeys)
            {
                // Skip if key was already pressed
                if (_prevConsoleKeyState.IsKeyDown(key)) continue;

                // Enter - execute command
                if (key == Keys.Enter && _consoleInput.Length > 0)
                {
                    ExecuteCommand(_consoleInput);
                    _consoleHistory.Add(_consoleInput);
                    _consoleInput = "";
                    _consoleHistoryIndex = -1;
                    continue;
                }

                // Backspace - delete character
                if (key == Keys.Back && _consoleInput.Length > 0)
                {
                    _consoleInput = _consoleInput.Substring(0, _consoleInput.Length - 1);
                    continue;
                }

                // Up arrow - previous command
                if (key == Keys.Up && _consoleHistory.Count > 0)
                {
                    if (_consoleHistoryIndex < _consoleHistory.Count - 1)
                    {
                        _consoleHistoryIndex++;
                        _consoleInput = _consoleHistory[_consoleHistory.Count - 1 - _consoleHistoryIndex];
                    }
                    continue;
                }

                // Down arrow - next command
                if (key == Keys.Down)
                {
                    if (_consoleHistoryIndex > 0)
                    {
                        _consoleHistoryIndex--;
                        _consoleInput = _consoleHistory[_consoleHistory.Count - 1 - _consoleHistoryIndex];
                    }
                    else if (_consoleHistoryIndex == 0)
                    {
                        _consoleHistoryIndex = -1;
                        _consoleInput = "";
                    }
                    continue;
                }

                // Convert key to character
                char? c = KeyToChar(key, kState.IsKeyDown(Keys.LeftShift) || kState.IsKeyDown(Keys.RightShift));
                if (c.HasValue && _consoleInput.Length < 50)
                {
                    _consoleInput += c.Value;
                }
            }
        }

        private char? KeyToChar(Keys key, bool shift)
        {
            // Letters
            if (key >= Keys.A && key <= Keys.Z)
            {
                char c = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpper(c) : c;
            }

            // Numbers
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return (char)('0' + (key - Keys.D0));
            }
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)('0' + (key - Keys.NumPad0));
            }

            // Special characters
            return key switch
            {
                Keys.Space => ' ',
                Keys.OemPeriod => '.',
                Keys.OemComma => ',',
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPlus => shift ? '+' : '=',
                _ => null
            };
        }

        private void DrawConsole()
        {
            _spriteBatch.Begin();

            // Console background
            int consoleHeight = 300;
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, consoleHeight), Color.Black * 0.9f);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, consoleHeight - 2, 1280, 2), Color.Cyan);

            // Title
            _spriteBatch.DrawString(_font, "DEBUG CONSOLE (F12 or ` to close)", new Vector2(10, 5), Color.Cyan);

            // Output lines
            int lineY = 25;
            int startIndex = Math.Max(0, _consoleOutput.Count - MAX_CONSOLE_LINES);
            for (int i = startIndex; i < _consoleOutput.Count; i++)
            {
                Color lineColor = _consoleOutput[i].StartsWith(">") ? Color.Yellow :
                                  _consoleOutput[i].StartsWith("Error") ? Color.Red : Color.LightGray;
                _spriteBatch.DrawString(_font, _consoleOutput[i], new Vector2(10, lineY), lineColor);
                lineY += 16;
            }

            // Input line
            _spriteBatch.Draw(_pixelTexture, new Rectangle(5, consoleHeight - 25, 1270, 20), Color.DarkGray * 0.5f);
            string inputLine = "> " + _consoleInput + ((int)(_totalTime * 2) % 2 == 0 ? "_" : "");
            _spriteBatch.DrawString(_font, inputLine, new Vector2(10, consoleHeight - 23), Color.White);

            // Help hint
            _spriteBatch.DrawString(_font, "Type 'help' for commands", new Vector2(900, consoleHeight - 23), Color.Gray);

            _spriteBatch.End();
        }

        private void ConsoleLog(string message)
        {
            _consoleOutput.Add(message);
            if (_consoleOutput.Count > 100)
            {
                _consoleOutput.RemoveAt(0);
            }
        }

        private void ExecuteCommand(string input)
        {
            ConsoleLog("> " + input);

            string[] parts = input.ToLower().Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            string command = parts[0];
            string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

            switch (command)
            {
                case "help":
                    ConsoleLog("=== COMMANDS ===");
                    ConsoleLog("give <item_id> [count] - Give item");
                    ConsoleLog("  Bow: give bow_simple + give arrow_basic 20");
                    ConsoleLog("  Gun: give pistol_9mm + give ammo_9mm 20");
                    ConsoleLog("items - List all item IDs");
                    ConsoleLog("weapons - List weapon IDs");
                    ConsoleLog("stats - Show player stats & equipped weapons");
                    ConsoleLog("heal - Full heal");
                    ConsoleLog("healtest [%] - Test % healing (default 15)");
                    ConsoleLog("damage <amount> - Take damage");
                    ConsoleLog("gold <amount> - Add gold");
                    ConsoleLog("xp <amount> - Add XP");
                    ConsoleLog("level - Level up instantly");
                    ConsoleLog("mutation <type> - Add mutation");
                    ConsoleLog("spawn <enemy_type> - Spawn enemy");
                    ConsoleLog("kill - Kill all enemies");
                    ConsoleLog("tp <x> <y> - Teleport to tile");
                    ConsoleLog("factions - Show faction standings");
                    ConsoleLog("rep <faction> <amount> - Modify reputation");
                    ConsoleLog("clear - Clear console");
                    break;

                case "give":
                    CommandGive(args);
                    break;

                case "items":
                    CommandListItems(null);
                    break;

                case "weapons":
                    CommandListItems("weapon");
                    break;

                case "armor":
                    CommandListItems("armor");
                    break;

                case "consumables":
                case "food":
                    CommandListItems("consumable");
                    break;

                case "heal":
                    _player.Stats.CurrentHealth = _player.Stats.MaxHealth;
                    foreach (var part in _player.Stats.Body.Parts.Values)
                    {
                        part.CurrentHealth = part.MaxHealth;
                        part.Injuries.Clear();
                        part.Ailments.Clear();
                    }
                    _player.Stats.SyncHPWithBody();
                    ConsoleLog("Fully healed!");
                    break;

                case "healtest":
                    // Test percentage healing (like bandage)
                    float hpBefore = _player.Stats.CurrentHealth;
                    float percentToHeal = args.Length > 0 && float.TryParse(args[0], out float p) ? p : 15f;
                    float actualHealed = _player.Stats.HealByPercent(percentToHeal);
                    float hpAfter = _player.Stats.CurrentHealth;
                    ConsoleLog($"HealByPercent({percentToHeal}%):");
                    ConsoleLog($"  HP: {hpBefore:F1} -> {hpAfter:F1} (+{hpAfter - hpBefore:F1})");
                    ConsoleLog($"  Expected: +{_player.Stats.MaxHealth * percentToHeal / 100f:F1}");
                    break;

                case "damage":
                    if (args.Length > 0 && float.TryParse(args[0], out float dmg))
                    {
                        _player.Stats.TakeDamage(dmg, DamageType.Physical);
                        ConsoleLog($"Took {dmg} damage. HP: {_player.Stats.CurrentHealth:F0}/{_player.Stats.MaxHealth:F0}");
                    }
                    else
                    {
                        ConsoleLog("Usage: damage <amount>");
                    }
                    break;

                case "gold":
                    if (args.Length > 0 && int.TryParse(args[0], out int gold))
                    {
                        _player.Stats.Gold += gold;
                        ConsoleLog($"Added {gold} gold. Total: {_player.Stats.Gold}");
                    }
                    else
                    {
                        ConsoleLog("Usage: gold <amount>");
                    }
                    break;

                case "xp":
                    if (args.Length > 0 && float.TryParse(args[0], out float xp))
                    {
                        _player.Stats.AddXP(xp);
                        ConsoleLog($"Added {xp} XP. Level: {_player.Stats.Level}");
                    }
                    else
                    {
                        ConsoleLog("Usage: xp <amount>");
                    }
                    break;

                case "level":
                    _player.Stats.AddXP(_player.Stats.XPToNextLevel - _player.Stats.CurrentXP + 1);
                    ConsoleLog($"Leveled up! Now level {_player.Stats.Level}");
                    break;

                case "spawn":
                    CommandSpawn(args);
                    break;

                case "kill":
                    int killed = 0;
                    foreach (var enemy in _enemies)
                    {
                        if (enemy.IsAlive)
                        {
                            enemy.TakeDamage(9999, DamageType.Physical);
                            killed++;
                        }
                    }
                    ConsoleLog($"Killed {killed} enemies.");
                    break;

                case "tp":
                case "teleport":
                    if (args.Length >= 2 && int.TryParse(args[0], out int tx) && int.TryParse(args[1], out int ty))
                    {
                        _player.Position = new Vector2(tx * _world.TileSize, ty * _world.TileSize);
                        ConsoleLog($"Teleported to ({tx}, {ty})");
                    }
                    else
                    {
                        ConsoleLog("Usage: tp <x> <y>");
                    }
                    break;

                case "clear":
                    _consoleOutput.Clear();
                    break;

                case "mutations":
                    ConsoleLog("=== MUTATIONS ===");
                    foreach (MutationType mt in Enum.GetValues(typeof(MutationType)))
                    {
                        ConsoleLog($"  {mt}");
                    }
                    break;

                case "mutation":
                    if (args.Length > 0)
                    {
                        if (Enum.TryParse<MutationType>(args[0], true, out var mutType))
                        {
                            var result = GameServices.Mutations.ApplyMutation(_player.Stats.Mutations, _player.Stats.Body, mutType);
                            if (result != null)
                            {
                                var def = GameServices.Mutations.GetDefinition(result.Type);
                                ConsoleLog($"Added mutation: {def?.Name ?? result.Type.ToString()}");
                            }
                            else
                            {
                                ConsoleLog("Failed to add mutation (already maxed?)");
                            }
                        }
                        else
                        {
                            ConsoleLog($"Unknown mutation: {args[0]}. Type 'mutations' for list.");
                        }
                    }
                    else
                    {
                        ConsoleLog("Usage: mutation <type>");
                    }
                    break;

                case "enemies":
                    ConsoleLog("=== ENEMY TYPES ===");
                    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType)))
                    {
                        ConsoleLog($"  {et}");
                    }
                    break;

                case "pos":
                case "position":
                    Point playerTile = new Point((int)(_player.Position.X / _world.TileSize), (int)(_player.Position.Y / _world.TileSize));
                    ConsoleLog($"Position: ({playerTile.X}, {playerTile.Y})");
                    break;

                case "stats":
                    ConsoleLog($"HP: {_player.Stats.CurrentHealth:F0}/{_player.Stats.MaxHealth:F0}");
                    ConsoleLog($"DMG: {_player.Stats.Damage:F0} | ACC: {_player.Stats.Accuracy:P0}");
                    ConsoleLog($"Range: {_player.Stats.GetAttackRange()} tiles");
                    ConsoleLog($"AP: {_player.Stats.ActionPoints} | MP: {_player.Stats.MovementPoints}");

                    // Show equipped weapons
                    var weapons = _player.Stats.Body.GetEquippedWeapons();
                    if (weapons.Count > 0)
                    {
                        ConsoleLog("Weapons:");
                        foreach (var w in weapons)
                        {
                            string twoH = w.Definition?.IsTwoHanded == true ? " (2H)" : "";
                            string ammo = w.Definition?.RequiresAmmo != null ? $" [Ammo: {w.Definition.RequiresAmmo}]" : "";
                            ConsoleLog($"  {w.Name}{twoH} - Range:{w.Definition?.Range ?? 1}{ammo}");
                        }
                    }
                    else
                    {
                        ConsoleLog("Weapons: Unarmed");
                    }

                    // Show ammo
                    var ammoItems = _player.Stats.Inventory.GetAllItems()
                        .Where(i => i.Category == ItemCategory.Ammo)
                        .ToList();
                    if (ammoItems.Count > 0)
                    {
                        ConsoleLog("Ammo: " + string.Join(", ", ammoItems.Select(a => $"{a.Name}x{a.StackCount}")));
                    }
                    break;

                case "factions":
                    ConsoleLog(GameServices.Factions.GetFactionSummary());
                    break;

                case "rep":
                    // rep <faction> <amount> - modify reputation
                    if (args.Length >= 2 && Enum.TryParse<FactionType>(args[0], true, out var factionType) && int.TryParse(args[1], out int repAmount))
                    {
                        GameServices.Factions.ModifyReputation(factionType, repAmount, "debug");
                        ConsoleLog($"Modified {factionType} reputation by {repAmount}");
                    }
                    else
                    {
                        ConsoleLog("Usage: rep <faction> <amount>");
                        ConsoleLog("Factions: TheChanged, GeneElders, VoidCult, UnitedSanctum, IronSyndicate, VerdantOrder, Traders");
                    }
                    break;

                case "fog":
                    // fog <on/off/reveal/reset> - control fog of war
                    if (args.Length >= 1)
                    {
                        string fogCmd = args[0].ToLower();
                        if (fogCmd == "off" || fogCmd == "disable")
                        {
                            GameServices.FogOfWar.IsEnabled = false;
                            ConsoleLog("Fog of War DISABLED");
                        }
                        else if (fogCmd == "on" || fogCmd == "enable")
                        {
                            GameServices.FogOfWar.IsEnabled = true;
                            ConsoleLog("Fog of War ENABLED");
                        }
                        else if (fogCmd == "reveal" || fogCmd == "revealall")
                        {
                            GameServices.FogOfWar.RevealAll();
                            ConsoleLog("Revealed entire map!");
                        }
                        else if (fogCmd == "reset")
                        {
                            GameServices.FogOfWar.ResetExploration();
                            ConsoleLog("Reset all exploration");
                        }
                        else if (fogCmd == "debug")
                        {
                            GameServices.FogOfWar.DebugRevealAll = !GameServices.FogOfWar.DebugRevealAll;
                            ConsoleLog($"Debug reveal: {GameServices.FogOfWar.DebugRevealAll}");
                        }
                        else
                        {
                            ConsoleLog("Usage: fog <on/off/reveal/reset/debug>");
                        }
                    }
                    else
                    {
                        float explored = GameServices.FogOfWar.GetExplorationPercentage();
                        ConsoleLog($"Fog of War: {(GameServices.FogOfWar.IsEnabled ? "ON" : "OFF")}");
                        ConsoleLog($"Explored: {explored:F1}%");
                    }
                    break;

                default:
                    ConsoleLog($"Unknown command: {command}. Type 'help' for list.");
                    break;
            }
        }

        private void CommandGive(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleLog("Usage: give <item_id> [count]");
                ConsoleLog("Type 'items' to see available item IDs");
                return;
            }

            string itemId = args[0];
            int count = args.Length > 1 && int.TryParse(args[1], out int c) ? c : 1;

            var def = ItemDatabase.Get(itemId);
            if (def == null)
            {
                // Try partial match
                var allItems = ItemDatabase.GetAll();
                var matches = allItems.Where(i => i.Id.Contains(itemId) || i.Name.ToLower().Contains(itemId)).ToList();

                if (matches.Count == 1)
                {
                    def = matches[0];
                    itemId = def.Id;
                }
                else if (matches.Count > 1)
                {
                    ConsoleLog($"Multiple matches for '{itemId}':");
                    foreach (var m in matches.Take(10))
                    {
                        ConsoleLog($"  {m.Id} - {m.Name}");
                    }
                    return;
                }
                else
                {
                    ConsoleLog($"Unknown item: {itemId}");
                    return;
                }
            }

            bool success = _player.Stats.Inventory.TryAddItem(itemId, count);
            if (success)
            {
                ConsoleLog($"Gave {count}x {def.Name}");
            }
            else
            {
                ConsoleLog("Inventory full!");
            }
        }

        private void CommandListItems(string category)
        {
            var items = ItemDatabase.GetAll();

            if (category != null)
            {
                items = category.ToLower() switch
                {
                    "weapon" => items.Where(i => i.Category == ItemCategory.Weapon).ToList(),
                    "armor" => items.Where(i => i.Category == ItemCategory.Armor).ToList(),
                    "consumable" => items.Where(i => i.Category == ItemCategory.Consumable).ToList(),
                    _ => items
                };
            }

            ConsoleLog($"=== ITEMS ({items.Count}) ===");
            foreach (var item in items.Take(30))
            {
                string rangeInfo = item.Category == ItemCategory.Weapon ? $" [Range:{item.Range}]" : "";
                ConsoleLog($"  {item.Id} - {item.Name}{rangeInfo}");
            }

            if (items.Count > 30)
            {
                ConsoleLog($"  ... and {items.Count - 30} more");
            }
        }

        private void CommandSpawn(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleLog("Usage: spawn <enemy_type>");
                ConsoleLog("Type 'enemies' for list");
                return;
            }

            if (Enum.TryParse<EnemyType>(args[0], true, out var enemyType))
            {
                Point playerTile = new Point((int)(_player.Position.X / _world.TileSize), (int)(_player.Position.Y / _world.TileSize));

                // Spawn near player - pick desired location
                Point desiredTile = new Point(
                    playerTile.X + _random.Next(-3, 4),
                    playerTile.Y + _random.Next(-3, 4)
                );

                // Use FindUnoccupiedSpawnTile to prevent enemies stacking
                Point finalTile = FindUnoccupiedSpawnTile(desiredTile);
                Vector2 finalPosition = new Vector2(finalTile.X * _world.TileSize, finalTile.Y * _world.TileSize);

                var enemy = EnemyEntity.Create(enemyType, finalPosition, _enemies.Count);
                _enemies.Add(enemy);

                ConsoleLog($"Spawned {enemy.Name} at ({finalTile.X}, {finalTile.Y})");
            }
            else
            {
                ConsoleLog($"Unknown enemy type: {args[0]}");
            }
        }

        // ============================================
        // WORLD MAP UI
        // ============================================

        private void DrawWorldMap()
        {
            var mState = Mouse.GetState();

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Dark overlay
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.92f);

            // Title
            string title = "WORLD MAP";
            Vector2 titleSize = _font.MeasureString(title);
            _spriteBatch.DrawString(_font, title, new Vector2(640 - titleSize.X / 2, 20), UITheme.TextHighlight);

            // Current zone info
            var currentZone = _zoneManager.CurrentZone;
            if (currentZone != null)
            {
                string currentText = $"Current Location: {currentZone.Name}";
                _spriteBatch.DrawString(_font, currentText, new Vector2(640 - _font.MeasureString(currentText).X / 2, 50), Color.Yellow);
            }

            // Get all zones
            var allZones = _zoneManager.GetAllZones();

            // Calculate zone positions on map
            var zonePositions = CalculateZoneMapPositions(allZones);

            // Draw connections first (behind nodes)
            foreach (var zone in allZones)
            {
                if (!zonePositions.ContainsKey(zone.Id)) continue;
                Vector2 fromPos = zonePositions[zone.Id];

                foreach (var exit in zone.Exits.Values)
                {
                    if (zonePositions.ContainsKey(exit.TargetZoneId))
                    {
                        Vector2 toPos = zonePositions[exit.TargetZoneId];
                        DrawMapConnection(fromPos, toPos, zone.HasBeenVisited);
                    }
                }
            }

            // Draw zone nodes
            _hoveredZoneId = null;
            foreach (var zone in allZones)
            {
                if (!zonePositions.ContainsKey(zone.Id)) continue;
                Vector2 pos = zonePositions[zone.Id];

                bool isHovered = DrawZoneNode(zone, pos, mState);
                if (isHovered)
                {
                    _hoveredZoneId = zone.Id;
                }
            }

            // Draw tooltip for hovered zone
            if (_hoveredZoneId != null)
            {
                var hoveredZone = _zoneManager.GetZone(_hoveredZoneId);
                if (hoveredZone != null)
                {
                    DrawZoneTooltip(hoveredZone, new Vector2(mState.X + 15, mState.Y + 15));
                }
            }

            // Legend
            DrawMapLegend();

            // Faction Standings Panel (right side)
            DrawFactionStandingsPanel();

            // Help text
            DrawHelpBar("[N/Esc] Close  |  Hover zones for details");

            _spriteBatch.End();
        }

        private Dictionary<string, Vector2> CalculateZoneMapPositions(List<ZoneData> allZones)
        {
            var positions = new Dictionary<string, Vector2>();

            // Map display area
            int centerX = 640;
            int centerY = 360;
            int gx = 90;  // Grid spacing X
            int gy = 60;  // Grid spacing Y

            // ==========================================
            // GRID-BASED LAYOUT - Matches actual zone connections
            // All connections are strictly N/S/E/W (no diagonals)
            // ==========================================

            // --- MAIN COLUMN (center): Rusthollow's vertical path ---
            positions["rusthollow"] = new Vector2(centerX, centerY + gy * 2);             // PLAYER START ★
            positions["outer_ruins_north"] = new Vector2(centerX, centerY + gy);          // N of rusthollow
            positions["inner_ruins_south"] = new Vector2(centerX, centerY);               // N of outer_ruins_north
            positions["the_spire"] = new Vector2(centerX, centerY - gy);                  // N of inner_ruins_south
            positions["deep_zone_south"] = new Vector2(centerX, centerY - gy * 2);        // N of the_spire
            positions["the_wound"] = new Vector2(centerX, centerY - gy * 3);              // N of deep_zone_south
            positions["the_epicenter"] = new Vector2(centerX, centerY - gy * 4);          // FINAL AREA

            // --- SOUTH COLUMN: Scavenger Plains path ---
            positions["scavenger_plains"] = new Vector2(centerX, centerY + gy * 3);       // S of rusthollow
            positions["free_zone_1"] = new Vector2(centerX, centerY + gy * 4);            // S of scavenger_plains
            positions["free_zone_4"] = new Vector2(centerX - gx, centerY + gy * 3);       // W of scavenger_plains

            // --- WEST COLUMN: Twisted Woods path ---
            positions["twisted_woods"] = new Vector2(centerX - gx, centerY + gy * 2);     // W of rusthollow
            positions["free_zone_2"] = new Vector2(centerX - gx * 2, centerY + gy * 2);   // W of twisted_woods
            positions["dark_forest"] = new Vector2(centerX - gx, centerY + gy);           // N of twisted_woods
            positions["free_zone_5"] = new Vector2(centerX - gx * 2, centerY + gy);       // W of dark_forest (cave)
            positions["void_temple"] = new Vector2(centerX - gx, centerY);                // N of dark_forest
            positions["free_zone_7"] = new Vector2(centerX - gx * 2, centerY);            // W of void_temple
            positions["deep_zone_west"] = new Vector2(centerX - gx, centerY - gy * 2);    // N of void_temple (same row as deep_zone_south)

            // --- EAST COLUMN: Outer Ruins East path ---
            positions["outer_ruins_east"] = new Vector2(centerX + gx, centerY + gy * 2);  // E of rusthollow
            positions["free_zone_8"] = new Vector2(centerX + gx, centerY + gy * 3);       // S of outer_ruins_east
            positions["syndicate_post"] = new Vector2(centerX + gx, centerY + gy);        // N of outer_ruins_east, E of outer_ruins_north
            positions["free_zone_6"] = new Vector2(centerX + gx * 2, centerY + gy);       // E of syndicate_post
            positions["inner_ruins_east"] = new Vector2(centerX + gx, centerY);           // N of syndicate_post, E of inner_ruins_south
            positions["vault_omega"] = new Vector2(centerX + gx, centerY - gy);           // N of inner_ruins_east
            positions["free_zone_9"] = new Vector2(centerX + gx * 2, centerY - gy);       // E of vault_omega
            positions["deep_zone_east"] = new Vector2(centerX + gx, centerY - gy * 2);    // N of vault_omega

            // --- FAR EAST COLUMN: Forward Base path ---
            positions["forward_base_purity"] = new Vector2(centerX + gx * 2, centerY + gy * 2);  // E of outer_ruins_east
            positions["free_zone_3"] = new Vector2(centerX + gx * 3, centerY + gy * 2);   // E of forward_base_purity
            positions["the_nursery"] = new Vector2(centerX + gx * 2, centerY);            // N of forward_base, E of inner_ruins_east

            // --- SPECIAL: The Rift (near The Wound) ---
            positions["the_rift"] = new Vector2(centerX + gx * 0.5f, centerY - gy * 3.5f);

            // --- FREE ZONE 10 (W of The Spire) ---
            positions["free_zone_10"] = new Vector2(centerX - gx * 0.5f, centerY - gy);   // W of the_spire, between columns

            // For any zones not manually positioned, place them dynamically
            int dynamicX = 100;
            int dynamicY = centerY + gy * 4;
            foreach (var zone in allZones)
            {
                if (!positions.ContainsKey(zone.Id))
                {
                    positions[zone.Id] = new Vector2(dynamicX, dynamicY);
                    dynamicY -= 60;
                    if (dynamicY < centerY - gy * 3)
                    {
                        dynamicY = centerY + gy * 2;
                        dynamicX += 80;
                    }
                }
            }

            return positions;
        }

        private void DrawMapConnection(Vector2 from, Vector2 to, bool isVisited)
        {
            Color lineColor = isVisited ? new Color(80, 120, 80) : new Color(60, 60, 80);
            int thickness = 2;

            // Check if connection is roughly orthogonal (within tolerance)
            float dx = Math.Abs(to.X - from.X);
            float dy = Math.Abs(to.Y - from.Y);
            float tolerance = 20f;  // Allow small misalignment

            if (dx < tolerance || dy < tolerance)
            {
                // Nearly orthogonal - draw straight line
                DrawLineSegment(from, to, lineColor, thickness);
            }
            else
            {
                // Diagonal - draw L-shaped path (horizontal then vertical)
                Vector2 corner = new Vector2(to.X, from.Y);
                DrawLineSegment(from, corner, lineColor, thickness);
                DrawLineSegment(corner, to, lineColor, thickness);
            }
        }

        private void DrawLineSegment(Vector2 from, Vector2 to, Color color, int thickness)
        {
            Vector2 diff = to - from;
            float length = diff.Length();
            if (length < 1) return;

            int steps = Math.Max(1, (int)(length / 3));

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 pos = Vector2.Lerp(from, to, t);
                _spriteBatch.Draw(_pixelTexture, new Rectangle((int)pos.X - thickness / 2, (int)pos.Y - thickness / 2, thickness, thickness), color);
            }
        }

        private bool DrawZoneNode(ZoneData zone, Vector2 position, MouseState mState)
        {
            bool isCurrent = zone.Id == _zoneManager.CurrentZoneId;
            bool isVisited = zone.HasBeenVisited;
            bool isFreeZone = zone.IsFreeZone;

            // Free zones are slightly smaller
            int nodeSize = isCurrent ? 50 : (isFreeZone ? 32 : 40);
            Rectangle nodeRect = new Rectangle(
                (int)position.X - nodeSize / 2,
                (int)position.Y - nodeSize / 2,
                nodeSize, nodeSize
            );

            bool isHovered = nodeRect.Contains(mState.X, mState.Y);

            // Background color based on zone type and status
            Color bgColor;
            if (isFreeZone)
            {
                // Free zones use their biome color but slightly muted
                bgColor = GetZoneTypeColor(zone.BiomeType);
                bgColor = Color.Lerp(bgColor, new Color(60, 80, 60), 0.3f);  // Greenish tint for "safe"
            }
            else
            {
                bgColor = GetZoneTypeColor(zone.Type);
            }

            if (!isVisited)
            {
                bgColor = new Color(50, 50, 60);  // Undiscovered = gray
            }
            if (isHovered)
            {
                bgColor = Color.Lerp(bgColor, Color.White, 0.3f);
            }

            // Draw node background
            _spriteBatch.Draw(_pixelTexture, nodeRect, bgColor);

            // Border - Free zones have dashed/different style (cyan border)
            Color borderColor;
            if (isCurrent)
            {
                borderColor = Color.Yellow;
            }
            else if (isFreeZone && isVisited)
            {
                borderColor = new Color(80, 200, 120);  // Green - buildable
            }
            else if (isVisited)
            {
                borderColor = UITheme.PanelBorder;
            }
            else
            {
                borderColor = Color.DarkGray;
            }

            DrawBorder(nodeRect, borderColor, isCurrent ? 3 : (isFreeZone ? 2 : 2));

            // Zone type icon/letter
            string icon;
            if (isFreeZone)
            {
                icon = "+";  // Plus sign for free/buildable zones
            }
            else
            {
                icon = GetZoneTypeIcon(zone.Type);
            }

            Color iconColor = isVisited ? Color.White : Color.Gray;
            if (isFreeZone && isVisited)
            {
                iconColor = new Color(150, 255, 150);  // Bright green for free zones
            }

            Vector2 iconSize = _font.MeasureString(icon);
            _spriteBatch.DrawString(_font, icon,
                new Vector2(position.X - iconSize.X / 2, position.Y - iconSize.Y / 2),
                iconColor);

            // Zone name below (only if visited or current)
            if (isVisited || isCurrent)
            {
                string name = zone.Name.Length > 12 ? zone.Name.Substring(0, 10) + ".." : zone.Name;
                Vector2 nameSize = _font.MeasureString(name);
                Color nameColor = isCurrent ? Color.Yellow : (isFreeZone ? new Color(120, 200, 120) : UITheme.TextSecondary);
                _spriteBatch.DrawString(_font, name,
                    new Vector2(position.X - nameSize.X / 2, position.Y + nodeSize / 2 + 3),
                    nameColor);
            }
            else
            {
                // Show "???" for unvisited
                string unknown = "???";
                Vector2 unknownSize = _font.MeasureString(unknown);
                _spriteBatch.DrawString(_font, unknown,
                    new Vector2(position.X - unknownSize.X / 2, position.Y + nodeSize / 2 + 3),
                    Color.DarkGray);
            }

            // Danger indicator (small colored dot) - not for free zones (show resource icon instead)
            if (isVisited)
            {
                if (isFreeZone && zone.AllowBaseBuilding)
                {
                    // Show hammer icon for buildable zones
                    Color buildColor = new Color(80, 200, 120);
                    Rectangle buildDot = new Rectangle(nodeRect.Right - 10, nodeRect.Top + 2, 8, 8);
                    _spriteBatch.Draw(_pixelTexture, buildDot, buildColor);
                }
                else
                {
                    Color dangerColor = GetDangerColor(zone.DangerLevel);
                    Rectangle dangerDot = new Rectangle(nodeRect.Right - 10, nodeRect.Top + 2, 8, 8);
                    _spriteBatch.Draw(_pixelTexture, dangerDot, dangerColor);
                }
            }

            return isHovered;
        }

        private void DrawZoneTooltip(ZoneData zone, Vector2 position)
        {
            // Build tooltip content
            List<string> lines = new List<string>();
            lines.Add(zone.Name);

            // Show FREE ZONE badge
            if (zone.IsFreeZone)
            {
                lines.Add("[FREE ZONE]");
            }

            lines.Add($"Type: {(zone.IsFreeZone ? zone.BiomeType : zone.Type)}");

            // Show controlling faction (not for free zones)
            if (!zone.IsFreeZone)
            {
                string factionName = zone.ControllingFaction switch
                {
                    FactionType.TheChanged => "The Changed",
                    FactionType.GeneElders => "Gene-Elders",
                    FactionType.VoidCult => "Void Cult",
                    FactionType.UnitedSanctum => "United Sanctum",
                    FactionType.IronSyndicate => "Iron Syndicate",
                    FactionType.VerdantOrder => "Verdant Order",
                    FactionType.VoidSpawn => "Void Spawn",
                    FactionType.Wildlife => "Wildlife",
                    _ => "Unclaimed"
                };
                lines.Add($"Control: {factionName}");
            }

            lines.Add($"Danger: {GetDangerText(zone.DangerLevel)}");

            // Free zone specific info
            if (zone.IsFreeZone)
            {
                if (zone.AllowBaseBuilding)
                    lines.Add("+ Base Building Allowed");
                if (zone.ResourceMultiplier > 1.0f)
                    lines.Add($"+ Resources: +{(zone.ResourceMultiplier - 1) * 100:F0}%");
            }

            if (zone.HasMerchant)
                lines.Add("+ Has Merchant");
            if (zone.LootMultiplier > 1.0f)
                lines.Add($"+ Loot Bonus: +{(zone.LootMultiplier - 1) * 100:F0}%");

            // Lore description (wrap if too long)
            if (!string.IsNullOrEmpty(zone.Description) && zone.HasBeenVisited)
            {
                lines.Add("");
                // Word wrap description
                string desc = zone.Description;
                int maxLineWidth = 35;
                while (desc.Length > 0)
                {
                    if (desc.Length <= maxLineWidth)
                    {
                        lines.Add(desc);
                        break;
                    }
                    int breakPoint = desc.LastIndexOf(' ', Math.Min(maxLineWidth, desc.Length - 1));
                    if (breakPoint <= 0) breakPoint = maxLineWidth;
                    lines.Add(desc.Substring(0, breakPoint));
                    desc = desc.Substring(breakPoint).TrimStart();
                }
            }

            lines.Add("");
            lines.Add(zone.HasBeenVisited ? "Status: Explored" : "Status: Unexplored");

            // Calculate size
            int padding = 10;
            int lineHeight = 18;
            int maxWidth = 0;
            foreach (var line in lines)
            {
                int width = (int)_font.MeasureString(line).X;
                if (width > maxWidth) maxWidth = width;
            }

            int tooltipWidth = maxWidth + padding * 2;
            int tooltipHeight = lines.Count * lineHeight + padding * 2;

            // Clamp to screen
            if (position.X + tooltipWidth > 1270)
                position.X = 1270 - tooltipWidth;
            if (position.Y + tooltipHeight > 680)
                position.Y = 680 - tooltipHeight;

            // Background
            Rectangle bgRect = new Rectangle((int)position.X, (int)position.Y, tooltipWidth, tooltipHeight);
            _spriteBatch.Draw(_pixelTexture, bgRect, UITheme.PanelBackground);
            DrawBorder(bgRect, zone.IsFreeZone ? new Color(80, 200, 120) : UITheme.PanelBorder, 1);

            // Draw lines
            int y = (int)position.Y + padding;
            for (int i = 0; i < lines.Count; i++)
            {
                Color textColor = UITheme.TextSecondary;

                if (i == 0)
                    textColor = UITheme.TextHighlight;  // Zone name
                else if (lines[i] == "[FREE ZONE]")
                    textColor = new Color(80, 220, 120);  // Bright green
                else if (lines[i].StartsWith("+"))
                    textColor = UITheme.TextSuccess;
                else if (lines[i].Contains("Danger:"))
                    textColor = GetDangerColor(zone.DangerLevel);
                else if (lines[i].Contains("Control:"))
                    textColor = GetFactionColor(zone.ControllingFaction);

                _spriteBatch.DrawString(_font, lines[i], new Vector2(position.X + padding, y), textColor);
                y += lineHeight;
            }
        }

        private Color GetFactionColor(FactionType faction)
        {
            return faction switch
            {
                FactionType.TheChanged => new Color(180, 140, 100),      // Brown - your people
                FactionType.GeneElders => new Color(200, 180, 100),      // Gold - respected
                FactionType.VoidCult => new Color(140, 80, 180),         // Purple - void
                FactionType.UnitedSanctum => new Color(100, 150, 200),   // Blue - military
                FactionType.IronSyndicate => new Color(200, 180, 80),    // Yellow - trade
                FactionType.VerdantOrder => new Color(80, 180, 100),     // Green - bio
                FactionType.VoidSpawn => new Color(180, 60, 180),        // Magenta - hostile
                FactionType.Wildlife => new Color(140, 160, 120),        // Muted green
                _ => UITheme.TextSecondary
            };
        }

        private void DrawMapLegend()
        {
            int legendX = 30;
            int legendY = 100;
            int lineHeight = 20;

            // === ZONE TYPES ===
            _spriteBatch.DrawString(_font, "ZONE TYPES", new Vector2(legendX, legendY), UITheme.TextHighlight);
            legendY += lineHeight + 2;

            DrawLegendItem(legendX, legendY, GetZoneTypeColor(ZoneType.Settlement), "Settlement");
            legendY += lineHeight;
            DrawLegendItem(legendX, legendY, GetZoneTypeColor(ZoneType.OuterRuins), "Outer Ruins");
            legendY += lineHeight;
            DrawLegendItem(legendX, legendY, GetZoneTypeColor(ZoneType.InnerRuins), "Inner Ruins");
            legendY += lineHeight;
            DrawLegendItem(legendX, legendY, GetZoneTypeColor(ZoneType.DeepZone), "Deep Zone");
            legendY += lineHeight;
            DrawLegendItem(legendX, legendY, GetZoneTypeColor(ZoneType.VoidRift), "Void Rift");
            legendY += lineHeight + 6;

            // === FREE ZONES ===
            _spriteBatch.DrawString(_font, "FREE ZONES", new Vector2(legendX, legendY), new Color(80, 220, 120));
            legendY += lineHeight + 2;

            DrawLegendItem(legendX, legendY, new Color(80, 200, 120), "+ Base Building");
            legendY += lineHeight;
            DrawLegendItem(legendX, legendY, new Color(100, 180, 100), "+ Resource Farming");
            legendY += lineHeight + 6;

            // === DANGER LEVELS ===
            _spriteBatch.DrawString(_font, "DANGER", new Vector2(legendX, legendY), UITheme.TextHighlight);
            legendY += lineHeight + 2;
            DrawLegendItem(legendX, legendY, GetDangerColor(0.5f), "Safe");
            legendY += lineHeight;
            DrawLegendItem(legendX, legendY, GetDangerColor(1.5f), "Low");
            legendY += lineHeight;
            DrawLegendItem(legendX, legendY, GetDangerColor(2.5f), "Dangerous");
            legendY += lineHeight;
            DrawLegendItem(legendX, legendY, GetDangerColor(4.0f), "DEADLY");
        }

        private void DrawFactionStandingsPanel()
        {
            int panelX = 1080;
            int panelY = 100;
            int panelWidth = 180;
            int lineHeight = 18;

            // Panel background
            _spriteBatch.Draw(_pixelTexture, new Rectangle(panelX - 10, panelY - 10, panelWidth + 20, 320), Color.Black * 0.7f);

            // Title
            _spriteBatch.DrawString(_font, "FACTION STANDINGS", new Vector2(panelX, panelY), UITheme.TextHighlight);
            panelY += lineHeight + 8;

            // Get all standings
            var standings = GameServices.Factions.GetAllStandingsForUI();

            foreach (var (name, rep, standing, colorHex) in standings)
            {
                // Standing color
                Color standingColor = standing switch
                {
                    FactionStanding.Revered => new Color(255, 215, 0),   // Gold
                    FactionStanding.Allied => new Color(100, 255, 100),  // Green
                    FactionStanding.Friendly => new Color(150, 255, 150), // Light green
                    FactionStanding.Neutral => Color.White,
                    FactionStanding.Unfriendly => new Color(255, 200, 100), // Orange
                    FactionStanding.Hostile => new Color(255, 100, 100),  // Red
                    FactionStanding.Hated => new Color(200, 50, 50),      // Dark red
                    _ => Color.Gray
                };

                // Faction name (shortened if needed)
                string displayName = name.Length > 14 ? name.Substring(0, 12) + ".." : name;
                _spriteBatch.DrawString(_font, displayName, new Vector2(panelX, panelY), standingColor);

                // Rep bar background
                int barX = panelX + 110;
                int barY = panelY + 2;
                int barWidth = 60;
                int barHeight = 12;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(barX, barY, barWidth, barHeight), Color.DarkGray);

                // Rep bar fill (centered at 0, -100 to +100)
                int centerX = barX + barWidth / 2;
                if (rep >= 0)
                {
                    int fillWidth = (int)(rep / 100f * (barWidth / 2));
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(centerX, barY + 1, fillWidth, barHeight - 2), standingColor);
                }
                else
                {
                    int fillWidth = (int)(Math.Abs(rep) / 100f * (barWidth / 2));
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(centerX - fillWidth, barY + 1, fillWidth, barHeight - 2), standingColor);
                }

                // Center line
                _spriteBatch.Draw(_pixelTexture, new Rectangle(centerX, barY, 1, barHeight), Color.White * 0.5f);

                // Rep number
                string repText = rep >= 0 ? $"+{rep}" : $"{rep}";
                Vector2 repSize = _font.MeasureString(repText);
                // Draw rep number small, to the right of bar
                
                panelY += lineHeight + 2;
            }

            // Standing legend at bottom
            panelY += 10;
            _spriteBatch.DrawString(_font, "-------------", new Vector2(panelX, panelY), Color.Gray * 0.5f);
            panelY += lineHeight;
            
            int legendStartY = panelY;
            DrawStandingLegendItem(panelX, panelY, new Color(255, 215, 0), "Revered"); panelY += 14;
            DrawStandingLegendItem(panelX, panelY, new Color(100, 255, 100), "Allied"); panelY += 14;
            DrawStandingLegendItem(panelX, panelY, Color.White, "Neutral"); panelY += 14;
            DrawStandingLegendItem(panelX, panelY, new Color(255, 100, 100), "Hostile"); panelY += 14;
        }

        private void DrawStandingLegendItem(int x, int y, Color color, string text)
        {
            _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + 2, 8, 8), color);
            _spriteBatch.DrawString(_font, text, new Vector2(x + 12, y - 1), Color.Gray);
        }

        private void DrawLegendItem(int x, int y, Color color, string text)
        {
            _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + 3, 14, 14), color);
            _spriteBatch.DrawString(_font, text, new Vector2(x + 20, y), UITheme.TextSecondary);
        }

        private Color GetZoneTypeColor(ZoneType type)
        {
            return type switch
            {
                // Settlements
                ZoneType.Settlement => new Color(80, 180, 80),         // Green - safe
                ZoneType.TradingPost => new Color(200, 180, 80),       // Yellow - trade
                ZoneType.GeneElderHold => new Color(180, 140, 60),     // Bronze - mutant leaders

                // Ruins of Aethelgard
                ZoneType.OuterRuins => new Color(140, 120, 100),       // Light brown
                ZoneType.InnerRuins => new Color(120, 100, 80),        // Brown
                ZoneType.Ruins => new Color(120, 100, 80),

                // Deep corruption
                ZoneType.DeepZone => new Color(100, 80, 120),          // Purple-gray
                ZoneType.Epicenter => new Color(140, 60, 140),         // Purple - void
                ZoneType.VoidRift => new Color(160, 80, 180),          // Bright purple

                // Wilderness
                ZoneType.Wasteland => new Color(160, 140, 80),         // Tan
                ZoneType.Forest => new Color(60, 140, 60),             // Green
                ZoneType.DarkForest => new Color(40, 80, 60),          // Dark green
                ZoneType.Cave => new Color(80, 80, 100),               // Gray

                // Faction outposts
                ZoneType.SanctumOutpost => new Color(100, 140, 180),   // Blue - military
                ZoneType.VerdantLab => new Color(80, 160, 100),        // Green - bio
                ZoneType.TradeOutpost => new Color(180, 160, 80),      // Yellow

                // Special
                ZoneType.Laboratory => new Color(100, 120, 160),       // Steel blue
                ZoneType.AncientVault => new Color(160, 140, 80),      // Gold - relics
                ZoneType.RadiationZone => new Color(100, 180, 80),     // Toxic green
                ZoneType.Spawn => new Color(100, 140, 180),            // Light blue

                _ => new Color(100, 100, 120)
            };
        }

        private string GetZoneTypeIcon(ZoneType type)
        {
            return type switch
            {
                // Settlements
                ZoneType.Settlement => "H",         // Home
                ZoneType.TradingPost => "$",        // Trade
                ZoneType.GeneElderHold => "E",      // Elder

                // Ruins
                ZoneType.OuterRuins => "o",         // outer
                ZoneType.InnerRuins => "i",         // inner
                ZoneType.Ruins => "R",

                // Deep Zone
                ZoneType.DeepZone => "D",           // Deep
                ZoneType.Epicenter => "X",          // Ground zero
                ZoneType.VoidRift => "V",           // Void

                // Wilderness
                ZoneType.Wasteland => "~",
                ZoneType.Forest => "F",
                ZoneType.DarkForest => "F",
                ZoneType.Cave => "C",

                // Faction outposts
                ZoneType.SanctumOutpost => "S",     // Sanctum
                ZoneType.VerdantLab => "B",         // Bio
                ZoneType.TradeOutpost => "$",

                // Special
                ZoneType.Laboratory => "L",
                ZoneType.AncientVault => "A",       // Ancient
                ZoneType.RadiationZone => "!",
                ZoneType.Spawn => "*",

                _ => "?"
            };
        }

        private Color GetDangerColor(float dangerLevel)
        {
            if (dangerLevel <= 0.8f) return new Color(60, 180, 60);       // Bright green - Safe
            if (dangerLevel <= 1.5f) return new Color(80, 180, 80);       // Green - Low
            if (dangerLevel <= 2.0f) return new Color(180, 180, 80);      // Yellow - Moderate
            if (dangerLevel <= 2.8f) return new Color(200, 140, 60);      // Orange - Dangerous
            if (dangerLevel <= 3.5f) return new Color(200, 80, 60);       // Red - Very Dangerous
            return new Color(180, 40, 180);                                // Purple - DEADLY (Epicenter)
        }

        private string GetDangerText(float dangerLevel)
        {
            if (dangerLevel <= 0.8f) return "Safe";
            if (dangerLevel <= 1.5f) return "Low";
            if (dangerLevel <= 2.0f) return "Moderate";
            if (dangerLevel <= 2.8f) return "Dangerous";
            if (dangerLevel <= 3.5f) return "Very Dangerous";
            return "DEADLY";
        }

        // ============================================
        // WORLD EVENT HANDLERS
        // ============================================

        private void HandleWorldEventStarted(ActiveWorldEvent evt)
        {
            System.Diagnostics.Debug.WriteLine($">>> EVENT STARTED: {evt.Definition.Name} <<<");

            // Spawn enemies for this event
            if (evt.Definition.EnemySpawnCount > 0)
            {
                var enemiesToSpawn = _worldEvents.GetEnemiesToSpawn(evt);
                List<string> spawnedIds = new List<string>();

                foreach (var enemyType in enemiesToSpawn)
                {
                    // Find spawn position (edge of screen, away from player)
                    // FIX: Use FindUnoccupiedSpawnTile to prevent enemy stacking
                    Point baseSpawnTile = FindEventSpawnTile();
                    Point finalTile = FindUnoccupiedSpawnTile(baseSpawnTile);
                    Vector2 spawnPos = new Vector2(finalTile.X * _world.TileSize, finalTile.Y * _world.TileSize);

                    var enemy = EnemyEntity.Create(enemyType, spawnPos, _enemies.Count + 1);
                    enemy.IsProvoked = true;  // Event enemies start aggressive
                    _enemies.Add(enemy);
                    spawnedIds.Add(enemy.Id);

                    System.Diagnostics.Debug.WriteLine($">>> Spawned event enemy: {enemy.Name} at tile ({finalTile.X}, {finalTile.Y}) <<<");
                }

                _worldEvents.RegisterSpawnedEnemies(evt, spawnedIds);
                _combat.UpdateEnemyList(_enemies);
            }

            // Spawn items for this event
            if (evt.Definition.ItemSpawnCount > 0)
            {
                var itemsToSpawn = _worldEvents.GetItemsToSpawn(evt);

                foreach (var itemId in itemsToSpawn)
                {
                    // Find spawn position
                    Point spawnTile = FindRandomGroundTile();
                    Vector2 spawnPos = new Vector2(spawnTile.X * _world.TileSize + 32, spawnTile.Y * _world.TileSize + 32);

                    var itemDef = ItemDatabase.Get(itemId);
                    if (itemDef != null)
                    {
                        var worldItem = new WorldItem(new Item(itemId), spawnPos);
                        _groundItems.Add(worldItem);
                        evt.SpawnedItemPositions.Add(spawnTile);

                        System.Diagnostics.Debug.WriteLine($">>> Spawned event item: {itemDef.Name} at {spawnTile} <<<");
                    }
                }
            }

            // Spawn trader for this event
            if (evt.Definition.SpawnsTrader)
            {
                SpawnEventTrader(evt);
            }

            // Spawn friendly reinforcements for this event
            if (evt.Definition.SpawnsFriendlyNPC)
            {
                SpawnFriendlyReinforcements(evt);
            }
        }

        private void HandleWorldEventEnded(ActiveWorldEvent evt)
        {
            System.Diagnostics.Debug.WriteLine($">>> EVENT ENDED: {evt.Definition.Name} <<<");

            // Clean up trader NPCs from this event
            foreach (var npcId in evt.SpawnedNPCIds)
            {
                var npc = _npcs.Find(n => n.Id == npcId);
                if (npc != null)
                {
                    _npcs.Remove(npc);
                    System.Diagnostics.Debug.WriteLine($">>> Removed event NPC: {npc.Name} <<<");
                }
            }

            // Show completion message
            if (evt.EnemiesKilled > 0)
            {
                AddEventNotification($"Event Complete: {evt.EnemiesKilled} enemies defeated!", Color.LimeGreen);
            }
        }

        private Point FindEventSpawnTile()
        {
            // Spawn at edge of map, away from player
            Point playerTile = new Point(
                (int)(_player.Position.X / _world.TileSize),
                (int)(_player.Position.Y / _world.TileSize)
            );

            // Pick a random edge
            int edge = _random.Next(4);
            int x, y;

            switch (edge)
            {
                case 0: // North
                    x = _random.Next(5, _world.Width - 5);
                    y = 2;
                    break;
                case 1: // South
                    x = _random.Next(5, _world.Width - 5);
                    y = _world.Height - 3;
                    break;
                case 2: // East
                    x = _world.Width - 3;
                    y = _random.Next(5, _world.Height - 5);
                    break;
                default: // West
                    x = 2;
                    y = _random.Next(5, _world.Height - 5);
                    break;
            }

            return new Point(x, y);
        }

        private Vector2 FindEventSpawnPosition()
        {
            // Legacy method - converts tile to world position
            Point tile = FindEventSpawnTile();
            return new Vector2(tile.X * _world.TileSize, tile.Y * _world.TileSize);
        }

        private Point FindRandomGroundTile()
        {
            // Find a walkable tile near the player
            Point playerTile = new Point(
                (int)(_player.Position.X / _world.TileSize),
                (int)(_player.Position.Y / _world.TileSize)
            );

            for (int attempts = 0; attempts < 50; attempts++)
            {
                int x = playerTile.X + _random.Next(-10, 11);
                int y = playerTile.Y + _random.Next(-10, 11);

                if (x >= 0 && x < _world.Width && y >= 0 && y < _world.Height)
                {
                    if (_world.Tiles[x, y].IsWalkable)
                    {
                        return new Point(x, y);
                    }
                }
            }

            return playerTile;  // Fallback
        }

        private void SpawnEventTrader(ActiveWorldEvent evt)
        {
            // Find spawn position
            Vector2 spawnPos = FindEventSpawnPosition();

            // Create trader NPC
            string traderId = $"event_trader_{evt.Type}_{DateTime.Now.Ticks}";
            NPCEntity trader;

            if (evt.Type == WorldEventType.WanderingMutant)
            {
                trader = NPCEntity.CreateWanderer(traderId, spawnPos);
                trader.Name = "Wandering Mutant";
            }
            else if (evt.Type == WorldEventType.FriendlyScavengers)
            {
                trader = NPCEntity.CreateWanderer(traderId, spawnPos);
                trader.Name = "Friendly Scavenger";
            }
            else if (evt.Type == WorldEventType.TraderDiscount)
            {
                trader = NPCEntity.CreateGeneralMerchant(traderId, spawnPos);
                trader.Name = "Guild Discount Trader";
                // Could add special discount flag here in future
            }
            else
            {
                trader = NPCEntity.CreateGeneralMerchant(traderId, spawnPos);
                trader.Name = "Caravan Trader";
            }

            _npcs.Add(trader);
            evt.SpawnedNPCIds.Add(traderId);

            System.Diagnostics.Debug.WriteLine($">>> Spawned event trader: {trader.Name} <<<");
        }

        /// <summary>
        /// Spawn friendly reinforcements from allied factions
        /// </summary>
        private void SpawnFriendlyReinforcements(ActiveWorldEvent evt)
        {
            // Friendly reinforcements leave supplies as a gift
            // (Full ally combat system would require significant work)
            
            int supplyCount = 3 + _random.Next(3);  // 3-5 items
            string[] friendlySupplies = { "food_jerky", "water_clean", "bandage", "medkit", "stimpak", "ammo_9mm" };
            
            for (int i = 0; i < supplyCount; i++)
            {
                Point spawnTile = FindRandomGroundTile();
                Vector2 spawnPos = new Vector2(spawnTile.X * _world.TileSize + 32, spawnTile.Y * _world.TileSize + 32);
                
                string itemId = friendlySupplies[_random.Next(friendlySupplies.Length)];
                var itemDef = ItemDatabase.Get(itemId);
                if (itemDef != null)
                {
                    var worldItem = new WorldItem(new Item(itemId), spawnPos);
                    _groundItems.Add(worldItem);
                }
            }
            
            // Also give a small rep boost for being helped
            if (evt.Definition.LinkedFaction.HasValue)
            {
                GameServices.Factions.ModifyReputation(evt.Definition.LinkedFaction.Value, 2, "sent reinforcements");
            }
            
            AddEventNotification("Friendly supplies have been left nearby!", Color.LimeGreen);
            System.Diagnostics.Debug.WriteLine($">>> Spawned {supplyCount} friendly supply items <<<");
        }

        // ============================================
        // EVENT NOTIFICATIONS
        // ============================================

        private void AddEventNotification(string text, Color color)
        {
            _eventNotifications.Add(new EventNotification
            {
                Text = text,
                TimeRemaining = EVENT_NOTIFICATION_DURATION,
                Color = color
            });

            // Play a sound or flash (could add later)
            System.Diagnostics.Debug.WriteLine($">>> EVENT NOTIFICATION: {text} <<<");
        }

        private Color GetEventNotificationColor(string text)
        {
            // Determine color based on notification content
            if (text.Contains("⚔️") || text.Contains("ATTACK") || text.Contains("PURGE") || text.Contains("SLAVER"))
                return Color.Red;
            if (text.Contains("🟣") || text.Contains("VOID"))
                return new Color(180, 80, 220);  // Purple
            if (text.Contains("💰") || text.Contains("📦") || text.Contains("💎") || text.Contains("+"))
                return Color.LimeGreen;
            if (text.Contains("⚡") || text.Contains("💥"))
                return Color.Yellow;
            if (text.Contains("👤") || text.Contains("👥"))
                return Color.Cyan;

            return Color.White;
        }

        private Color GetFactionNotificationColor(string text)
        {
            // Faction-specific colors
            if (text.Contains("🤝") || text.Contains("Allied"))
                return Color.LimeGreen;
            if (text.Contains("⚔️") || text.Contains("Hostile"))
                return Color.Red;
            if (text.Contains("+"))
                return Color.Cyan;
            if (text.Contains("-"))
                return Color.Orange;
            return Color.Yellow;
        }

        private void UpdateEventNotifications(float deltaTime)
        {
            for (int i = _eventNotifications.Count - 1; i >= 0; i--)
            {
                var notif = _eventNotifications[i];
                notif.TimeRemaining -= deltaTime;
                _eventNotifications[i] = notif;

                if (notif.TimeRemaining <= 0)
                {
                    _eventNotifications.RemoveAt(i);
                }
            }
        }

        private void DrawEventNotifications()
        {
            if (_eventNotifications.Count == 0) return;

            // Draw at very top of screen (above zone info which is at Y=50)
            int startY = 5;
            int notifHeight = 26;

            for (int i = 0; i < _eventNotifications.Count; i++)
            {
                var notif = _eventNotifications[i];
                int y = startY + i * notifHeight;

                // Fade out effect
                float alpha = Math.Min(1f, notif.TimeRemaining / 1.5f);
                Color textColor = notif.Color * alpha;
                Color bgColor = new Color(20, 20, 30) * (alpha * 0.9f);

                // Background
                Vector2 textSize = _font.MeasureString(notif.Text);
                int width = (int)textSize.X + 20;
                Rectangle bgRect = new Rectangle(640 - width / 2, y, width, 22);
                _spriteBatch.Draw(_pixelTexture, bgRect, bgColor);

                // Border
                DrawBorder(bgRect, notif.Color * (alpha * 0.6f), 1);

                // Text
                Vector2 textPos = new Vector2(640 - textSize.X / 2, y + 3);
                _spriteBatch.DrawString(_font, notif.Text, textPos, textColor);
            }
        }

        private void DrawActiveEventIndicator()
        {
            if (!_worldEvents.HasActiveEvent) return;

            float gameTimeHours = GameServices.SurvivalSystem.GameDay * 24f + GameServices.SurvivalSystem.GameHour;
            var eventInfo = _worldEvents.GetActiveEventInfo(gameTimeHours);

            // Position at right side of screen, below survival UI (which ends around Y=150)
            int startY = _combat.InCombat ? 10 : 160;  // Higher when in combat to avoid overlap
            int rightX = 1270;  // Right-aligned
            int y = startY;

            foreach (var (name, remainingHours, category, enemiesLeft) in eventInfo)
            {
                // Determine color based on category
                Color eventColor = category switch
                {
                    EventCategory.Positive => Color.LimeGreen,
                    EventCategory.Warning => Color.Yellow,
                    EventCategory.Hostile => Color.Red,
                    EventCategory.Void => new Color(180, 80, 220),
                    _ => Color.White
                };

                // Build text
                string eventText;
                if (enemiesLeft > 0)
                {
                    eventText = $"! {name}: {enemiesLeft} left";
                }
                else if (remainingHours < 999)
                {
                    int hours = (int)remainingHours;
                    int mins = (int)((remainingHours - hours) * 60);
                    eventText = $"! {name}: {hours}h {mins}m";
                }
                else
                {
                    eventText = $"! {name}";
                }

                Vector2 textSize = _font.MeasureString(eventText);
                int textX = rightX - (int)textSize.X - 10;

                // Background (right-aligned)
                Rectangle bgRect = new Rectangle(textX - 5, y - 2, (int)textSize.X + 10, 20);
                _spriteBatch.Draw(_pixelTexture, bgRect, new Color(20, 20, 30) * 0.85f);
                DrawBorder(bgRect, eventColor * 0.6f, 1);

                // Text
                _spriteBatch.DrawString(_font, eventText, new Vector2(textX, y), eventColor);

                y += 24;  // Move down for next event
            }
        }
    }

    // ============================================
    // FLOATING TEXT DATA STRUCTURE
    // ============================================

    public struct FloatingText
    {
        public Vector2 Position;
        public string Text;
        public Color Color;
        public float Timer;
        public float Duration;
        public float VelocityY;
        public float Scale;
        public bool IsCritical;
    }
}