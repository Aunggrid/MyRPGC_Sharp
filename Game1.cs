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
        GameOver,           // Player died
        Paused              // Game paused (future use)
    }
    
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Camera2D _camera;
        private WorldGrid _world;
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

            _world = new WorldGrid(50, 50, GraphicsDevice);

            // Create player entity (but don't initialize stats yet - wait for science path choice)
            _player = new PlayerEntity();
            _player.Position = new Vector2(2 * _world.TileSize, 2 * _world.TileSize);
            
            // Generate random character build for player to review
            _pendingBuild = GameServices.Traits.GenerateRandomBuild();
            _selectedSciencePathIndex = 0;
            
            // Spawn enemies
            SpawnEnemies();
            
            // Initialize combat manager (player will be initialized later)
            _combat = new CombatManager(_player, _enemies, _world.TileSize);
            _combat.OnCombatLog += (msg) =>
            {
                _combatLog.Add(msg);
                if (_combatLog.Count > MAX_LOG_LINES)
                    _combatLog.RemoveAt(0);
            };
            _combat.OnEnemyKilled += HandleEnemyKilled;
            
            // Reset state - start at character creation!
            _gameState = GameState.CharacterCreation;
            _deathTimer = 0f;
            _combatLog.Clear();
            _selectedEnemy = null;
            _mutationChoices.Clear();
            _selectedAttributeIndex = 0;
            _groundItems.Clear();
            _inventoryOpen = false;
            
            System.Diagnostics.Debug.WriteLine(">>> NEW GAME - CHARACTER CREATION <<<");
        }
        
        /// <summary>
        /// Finalize character and start playing
        /// </summary>
        private void FinalizeCharacterCreation()
        {
            SciencePath chosenPath = _selectedSciencePathIndex == 0 ? SciencePath.Tinker : SciencePath.Dark;
            _player.Initialize(_pendingBuild, chosenPath);
            
            _gameState = GameState.Playing;
            System.Diagnostics.Debug.WriteLine($">>> CHARACTER FINALIZED - Path: {chosenPath} <<<");
            System.Diagnostics.Debug.WriteLine(_player.Stats.GetStatusReport());
        }
        
        private void SpawnEnemies()
        {
            _enemies.Clear();
            
            // Spawn raiders
            _enemies.Add(EnemyEntity.Create(EnemyType.Raider, new Vector2(20 * _world.TileSize, 5 * _world.TileSize), 1));
            _enemies.Add(EnemyEntity.Create(EnemyType.Raider, new Vector2(22 * _world.TileSize, 7 * _world.TileSize), 2));
            
            // Spawn a mutant beast
            _enemies.Add(EnemyEntity.Create(EnemyType.MutantBeast, new Vector2(8 * _world.TileSize, 15 * _world.TileSize), 3));
            
            // Spawn an abomination
            _enemies.Add(EnemyEntity.Create(EnemyType.Abomination, new Vector2(30 * _world.TileSize, 30 * _world.TileSize), 4));
            
            System.Diagnostics.Debug.WriteLine($">>> Spawned {_enemies.Count} enemies <<<");
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
            KeyboardState kState = Keyboard.GetState();
            MouseState mState = Mouse.GetState();
            
            if (kState.IsKeyDown(Keys.Escape)) Exit();

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
                    UpdatePlaying(deltaTime, kState, mState);
                    break;
                    
                case GameState.MutationSelect:
                    UpdateMutationSelect(kState);
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
                System.Diagnostics.Debug.WriteLine($">>> Inventory {(_inventoryOpen ? "OPENED" : "CLOSED")} <<<");
            }
            
            // G key - Pick up nearby items
            if (kState.IsKeyDown(Keys.G) && _prevKeyboardState.IsKeyUp(Keys.G))
            {
                TryPickupAllNearby();
            }
            
            // Update nearest item for pickup highlight
            UpdateNearestItem();
            
            // Debug keys (only in exploration)
            if (!_combat.InCombat)
            {
                HandleDebugKeys(kState);
            }

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
            
            // Apply survival health drain (starvation, dehydration, etc.)
            float healthDrain = _player.Stats.Survival.GetHealthDrain();
            if (healthDrain > 0)
            {
                _player.Stats.TakeDamage(healthDrain * deltaTime, DamageType.Physical);
            }
            
            // Update enemies (AI)
            foreach (var enemy in _enemies)
            {
                enemy.Update(deltaTime, _world, _player.Position);
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
            
            // Player click-to-move
            if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                Vector2 worldPos = _camera.ScreenToWorld(new Vector2(mState.X, mState.Y));
                Point clickTile = new Point((int)(worldPos.X / _world.TileSize), (int)(worldPos.Y / _world.TileSize));
                
                // Check if clicked on enemy
                EnemyEntity clickedEnemy = GetEnemyAtTile(clickTile);
                if (clickedEnemy != null && clickedEnemy.IsAlive)
                {
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
            
            // Lightning test
            if (kState.IsKeyDown(Keys.Space) && _prevKeyboardState.IsKeyUp(Keys.Space))
            {
                if (_player.HasStatus(StatusEffectType.Wet) && !_player.HasStatus(StatusEffectType.Stunned))
                {
                    _player.ApplyLightning(10f);
                }
            }
            
            _player.Update(deltaTime, _world);
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
            foreach (var dir in new Point[] { new Point(0, -1), new Point(1, 0), new Point(0, 1), new Point(-1, 0) })
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
                        System.Diagnostics.Debug.WriteLine(">>> Resting at bed... Rest restored! <<<");
                        return;
                    }
                    else if (structure.Type == StructureType.Campfire)
                    {
                        // Warm up at campfire
                        System.Diagnostics.Debug.WriteLine(">>> Warming up at campfire... <<<");
                        return;
                    }
                    else if (structure.Type == StructureType.StorageBox)
                    {
                        // Open storage (future: storage UI)
                        System.Diagnostics.Debug.WriteLine($">>> Storage has {structure.StoredItems.Count} items <<<");
                        return;
                    }
                    else if (structure.Type == StructureType.CraftingBench)
                    {
                        // Open crafting (future: crafting UI)
                        System.Diagnostics.Debug.WriteLine(">>> Crafting bench - crafting not yet implemented <<<");
                        return;
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine(">>> No interactable structure nearby <<<");
        }
        
        private void UpdateCombat(float deltaTime, KeyboardState kState, MouseState mState)
        {
            _combat.Update(deltaTime, _world);
            
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
        
        private void CycleTarget()
        {
            var aliveEnemies = _enemies.FindAll(e => e.IsAlive);
            if (aliveEnemies.Count == 0)
            {
                _selectedEnemy = null;
                return;
            }
            
            if (_selectedEnemy == null)
            {
                _selectedEnemy = aliveEnemies[0];
            }
            else
            {
                int index = aliveEnemies.IndexOf(_selectedEnemy);
                index = (index + 1) % aliveEnemies.Count;
                _selectedEnemy = aliveEnemies[index];
            }
        }
        
        // ============================================
        // LOOT AND ITEM PICKUP
        // ============================================
        
        private void HandleEnemyKilled(EnemyEntity enemy, Vector2 position)
        {
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
            if (_player.Stats.Inventory.TryAddItem(_nearestItem.Item))
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
                
                if (_player.Stats.Inventory.TryAddItem(worldItem.Item))
                {
                    itemsToRemove.Add(worldItem);
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
            
            // Navigation
            if (kState.IsKeyDown(Keys.W) && _prevKeyboardState.IsKeyUp(Keys.W))
            {
                _selectedInventoryIndex = Math.Max(0, _selectedInventoryIndex - 1);
            }
            if (kState.IsKeyDown(Keys.S) && _prevKeyboardState.IsKeyUp(Keys.S))
            {
                _selectedInventoryIndex = Math.Min(maxIndex, _selectedInventoryIndex + 1);
            }
            
            // Equip/Use selected item
            if (kState.IsKeyDown(Keys.Enter) && _prevKeyboardState.IsKeyUp(Keys.Enter))
            {
                if (_selectedInventoryIndex >= 0 && _selectedInventoryIndex < items.Count)
                {
                    var item = items[_selectedInventoryIndex];
                    
                    if (item.Category == ItemCategory.Consumable)
                    {
                        // Use consumable
                        var effects = _player.Stats.Inventory.UseConsumable(item);
                        if (effects != null)
                        {
                            _player.Stats.Survival.RestoreHunger(effects.HungerRestore);
                            _player.Stats.Survival.RestoreThirst(effects.ThirstRestore);
                            _player.Stats.Heal(effects.HealthRestore);
                            System.Diagnostics.Debug.WriteLine($">>> Used: {item.Name} <<<");
                        }
                    }
                    else if (item.Definition?.EquipSlot != EquipSlot.None)
                    {
                        // Equip item
                        _player.Stats.Inventory.EquipItem(item);
                    }
                }
            }
            
            // Drop item
            if (kState.IsKeyDown(Keys.X) && _prevKeyboardState.IsKeyUp(Keys.X))
            {
                if (_selectedInventoryIndex >= 0 && _selectedInventoryIndex < items.Count)
                {
                    var item = items[_selectedInventoryIndex];
                    _player.Stats.Inventory.RemoveItem(item);
                    
                    // Drop on ground near player
                    var worldItem = new WorldItem(item, _player.Position);
                    _groundItems.Add(worldItem);
                    System.Diagnostics.Debug.WriteLine($">>> Dropped: {item.GetDisplayName()} <<<");
                    
                    // Adjust selection
                    if (_selectedInventoryIndex > 0 && _selectedInventoryIndex >= items.Count - 1)
                    {
                        _selectedInventoryIndex--;
                    }
                }
            }
            
            // Unequip slot with number keys
            if (kState.IsKeyDown(Keys.D1) && _prevKeyboardState.IsKeyUp(Keys.D1))
                TryUnequipSlot(EquipSlot.MainHand);
            if (kState.IsKeyDown(Keys.D2) && _prevKeyboardState.IsKeyUp(Keys.D2))
                TryUnequipSlot(EquipSlot.OffHand);
            if (kState.IsKeyDown(Keys.D3) && _prevKeyboardState.IsKeyUp(Keys.D3))
                TryUnequipSlot(EquipSlot.Head);
            if (kState.IsKeyDown(Keys.D4) && _prevKeyboardState.IsKeyUp(Keys.D4))
                TryUnequipSlot(EquipSlot.Torso);
            if (kState.IsKeyDown(Keys.D5) && _prevKeyboardState.IsKeyUp(Keys.D5))
                TryUnequipSlot(EquipSlot.Legs);
            if (kState.IsKeyDown(Keys.D6) && _prevKeyboardState.IsKeyUp(Keys.D6))
                TryUnequipSlot(EquipSlot.Feet);
            
            // Mouse click handling
            // (Equipment slots on left, inventory on right)
            if (mState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                // Check inventory list clicks (right side)
                int invStartX = 500;
                int invStartY = 120;
                int itemHeight = 28;
                
                for (int i = 0; i < items.Count && i < 15; i++)
                {
                    Rectangle itemRect = new Rectangle(invStartX, invStartY + i * itemHeight, 400, itemHeight);
                    if (itemRect.Contains(mState.X, mState.Y))
                    {
                        _selectedInventoryIndex = i;
                        break;
                    }
                }
            }
            
            // Double-click to equip/use
            // (simplified: just use Enter key for now)
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
            
            if (kState.IsKeyDown(Keys.F8) && _prevKeyboardState.IsKeyUp(Keys.F8))
            {
                var choices = _player.Stats.GetMutationChoices(3);
                System.Diagnostics.Debug.WriteLine("\n>>> MUTATION CHOICES <<<");
                for (int i = 0; i < choices.Count; i++)
                {
                    var m = choices[i];
                    System.Diagnostics.Debug.WriteLine($"  [{i + 1}] {m.Name} (Max Lv.{m.MaxLevel}) - {m.Description}");
                }
            }
            
            if (kState.IsKeyDown(Keys.F9) && _prevKeyboardState.IsKeyUp(Keys.F9))
            {
                SpawnEnemies();
                _combat.UpdateEnemyList(_enemies);
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
                    _player.Stats.Inventory.TryAddItem(randomItem);
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
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black);
            
            // Title
            string title = "CREATE YOUR MUTANT";
            Vector2 titleSize = _font.MeasureString(title);
            _spriteBatch.DrawString(_font, title, new Vector2(640 - titleSize.X / 2, 30), Color.Magenta);
            
            // Random Build Info Box (left side)
            _spriteBatch.Draw(_pixelTexture, new Rectangle(40, 80, 500, 200), Color.DarkGray * 0.5f);
            
            // Backstory
            var backstoryDef = GameServices.Traits.GetDefinition(_pendingBuild.Backstory);
            _spriteBatch.DrawString(_font, "BACKSTORY:", new Vector2(60, 95), Color.Yellow);
            int backstoryCost = backstoryDef?.PointCost ?? 0;
            string backstoryCostStr = backstoryCost > 0 ? $" (-{backstoryCost})" : backstoryCost < 0 ? $" (+{-backstoryCost})" : "";
            _spriteBatch.DrawString(_font, (backstoryDef?.Name ?? _pendingBuild.Backstory.ToString()) + backstoryCostStr, new Vector2(160, 95), Color.White);
            
            // Word-wrap backstory description to fit in box (max width ~460 pixels)
            string backstoryDesc = backstoryDef?.Description ?? "";
            var descLines = WrapText(backstoryDesc, 460f);
            int descY = 115;
            foreach (var line in descLines)
            {
                _spriteBatch.DrawString(_font, line, new Vector2(60, descY), Color.LightGray);
                descY += 16;
            }
            
            // Traits (adjust Y position based on description lines)
            int traitsStartY = Math.Max(155, descY + 10);
            _spriteBatch.DrawString(_font, "TRAITS:", new Vector2(60, traitsStartY), Color.Yellow);
            if (_pendingBuild.Traits.Count == 0)
            {
                _spriteBatch.DrawString(_font, "None", new Vector2(130, traitsStartY), Color.Gray);
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
                    _spriteBatch.DrawString(_font, $"• {traitText}{costStr}", new Vector2(130, traitY), Color.White);
                    traitY += 18;
                }
            }
            
            // Mutation Points
            _spriteBatch.DrawString(_font, $"MUTATION POINTS: {_pendingBuild.MutationPoints}", new Vector2(60, 250), Color.Cyan);
            
            // ============================================
            // STARTING ATTRIBUTES & STATS (right side)
            // ============================================
            _spriteBatch.Draw(_pixelTexture, new Rectangle(560, 80, 680, 200), Color.DarkGray * 0.5f);
            _spriteBatch.DrawString(_font, "STARTING ATTRIBUTES (Hover for info):", new Vector2(580, 95), Color.Yellow);
            
            // Get attribute values - base 10 for all
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
                
                var attr = attributes[i];
                int value = _pendingBuild.Attributes.Get(attr); // Get actual value from build
                string attrName = GetAttributeShortName(attr);
                
                // Color-code based on deviation from base (5)
                Color attrColor = GetAttributeColor(attr);
                if (value > 5) attrColor = Color.LightGreen;
                else if (value < 5) attrColor = Color.Salmon;
                
                // Draw attribute box
                _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, attrWidth - 10, attrHeight), Color.DarkSlateGray);
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
            _spriteBatch.DrawString(_font, "Effective Stats (attributes + traits):", new Vector2(580, 185), Color.Gray);
            
            // Color-code stats based on whether they're buffed/debuffed from base
            Color hpColor = effectiveHP > 100 ? Color.LightGreen : (effectiveHP < 100 ? Color.Salmon : Color.White);
            Color speedColor = effectiveSpeed > 200 ? Color.LightGreen : (effectiveSpeed < 200 ? Color.Salmon : Color.White);
            Color damageColor = effectiveDamage > 10 ? Color.LightGreen : (effectiveDamage < 10 ? Color.Salmon : Color.White);
            Color accColor = effectiveAccuracy > 0.75f ? Color.LightGreen : (effectiveAccuracy < 0.75f ? Color.Salmon : Color.White);
            Color sightColor = effectiveSight > 10 ? Color.LightGreen : (effectiveSight < 10 ? Color.Salmon : Color.White);
            
            // Draw individual stats with colors
            _spriteBatch.DrawString(_font, $"HP: {effectiveHP:F0}", new Vector2(580, 205), hpColor);
            _spriteBatch.DrawString(_font, $"Speed: {effectiveSpeed:F0}", new Vector2(680, 205), speedColor);
            _spriteBatch.DrawString(_font, $"Damage: {effectiveDamage:F1}", new Vector2(800, 205), damageColor);
            _spriteBatch.DrawString(_font, $"Accuracy: {effectiveAccuracy:P0}", new Vector2(580, 225), accColor);
            _spriteBatch.DrawString(_font, $"Sight: {effectiveSight:F0} tiles", new Vector2(720, 225), sightColor);
            
            // Show special trait flags if any
            int flagY = 245;
            if (!traitBonuses.CanSpeak)
            {
                _spriteBatch.DrawString(_font, "• Cannot Speak", new Vector2(580, flagY), Color.Orange);
                flagY += 16;
            }
            if (!traitBonuses.CanDisguise)
            {
                _spriteBatch.DrawString(_font, "• Cannot Disguise", new Vector2(580, flagY), Color.Orange);
                flagY += 16;
            }
            if (traitBonuses.CanEatCorpses)
            {
                _spriteBatch.DrawString(_font, "• Can Eat Corpses", new Vector2(580, flagY), Color.DarkRed);
                flagY += 16;
            }
            if (traitBonuses.IsNightPerson)
            {
                _spriteBatch.DrawString(_font, "• Night Owl (+night / -day)", new Vector2(580, flagY), Color.MediumPurple);
            }
            
            // Reroll hint
            _spriteBatch.DrawString(_font, "[R] Reroll Character", new Vector2(60, 300), Color.Gray);
            
            // Science Path Selection
            _spriteBatch.DrawString(_font, "CHOOSE YOUR SCIENCE PATH:", new Vector2(640 - _font.MeasureString("CHOOSE YOUR SCIENCE PATH:").X / 2, 330), Color.Yellow);
            
            // Tinker Box
            Color tinkerColor = _selectedSciencePathIndex == 0 ? Color.DarkCyan : Color.DarkGray * 0.5f;
            _spriteBatch.Draw(_pixelTexture, new Rectangle(340, 360, 280, 100), tinkerColor);
            if (_selectedSciencePathIndex == 0)
            {
                _spriteBatch.Draw(_pixelTexture, new Rectangle(335, 360, 5, 100), Color.Cyan);
            }
            _spriteBatch.DrawString(_font, "[1] TINKER SCIENCE", new Vector2(360, 380), Color.White);
            _spriteBatch.DrawString(_font, "Technology, implants,", new Vector2(360, 405), Color.LightGray);
            _spriteBatch.DrawString(_font, "guns, electronics", new Vector2(360, 425), Color.LightGray);
            
            // Dark Box
            Color darkColor = _selectedSciencePathIndex == 1 ? Color.DarkMagenta : Color.DarkGray * 0.5f;
            _spriteBatch.Draw(_pixelTexture, new Rectangle(660, 360, 280, 100), darkColor);
            if (_selectedSciencePathIndex == 1)
            {
                _spriteBatch.Draw(_pixelTexture, new Rectangle(655, 360, 5, 100), Color.Magenta);
            }
            _spriteBatch.DrawString(_font, "[2] DARK SCIENCE", new Vector2(680, 380), Color.White);
            _spriteBatch.DrawString(_font, "Rituals, anomalies,", new Vector2(680, 405), Color.LightGray);
            _spriteBatch.DrawString(_font, "transmutation, void", new Vector2(680, 425), Color.LightGray);
            
            // Controls
            _spriteBatch.DrawString(_font, "Click or Press [Enter] to Start | [R] to Reroll", 
                new Vector2(640 - _font.MeasureString("Click or Press [Enter] to Start | [R] to Reroll").X / 2, 490), Color.Gray);
            
            _spriteBatch.DrawString(_font, "Hover over backstory/traits/attributes for details", 
                new Vector2(640 - _font.MeasureString("Hover over backstory/traits/attributes for details").X / 2, 520), Color.DarkGray);
            
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
                    EnemyType.Raider => Color.Red,
                    EnemyType.MutantBeast => Color.Orange,
                    EnemyType.Hunter => Color.Purple,
                    EnemyType.Abomination => Color.DarkRed,
                    _ => Color.Red
                };
                
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
                
                _spriteBatch.Draw(_pixelTexture, enemyRect, enemyColor);
                
                // Enemy health bar
                float healthPercent = enemy.CurrentHealth / enemy.MaxHealth;
                Rectangle bgRect = new Rectangle((int)enemy.Position.X + offset, (int)enemy.Position.Y + offset - 8, 32, 4);
                Rectangle healthRect = new Rectangle((int)enemy.Position.X + offset, (int)enemy.Position.Y + offset - 8, (int)(32 * healthPercent), 4);
                
                _spriteBatch.Draw(_pixelTexture, bgRect, Color.DarkGray);
                _spriteBatch.Draw(_pixelTexture, healthRect, Color.Red);
                
                if (_combat.InCombat)
                {
                    Vector2 namePos = new Vector2(enemy.Position.X + offset, enemy.Position.Y - 20);
                    _spriteBatch.DrawString(_font, enemy.Name, namePos + new Vector2(1, 1), Color.Black);
                    _spriteBatch.DrawString(_font, enemy.Name, namePos, Color.White);
                }
            }

            // Draw Player
            Color playerColor = Color.Green;
            if (_player.HasStatus(StatusEffectType.Wet)) playerColor = Color.Cyan;
            if (_player.HasStatus(StatusEffectType.Stunned)) playerColor = Color.Yellow;
            if (_player.HasStatus(StatusEffectType.Burning)) playerColor = Color.OrangeRed;
            if (!_player.IsAlive) playerColor = Color.DarkRed;

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
                    _spriteBatch.DrawString(_font, "Click: Move/Attack | I: Inventory | G: Pickup | M: Mutations | B: Build", new Vector2(10, 10), Color.Yellow);
                    _spriteBatch.DrawString(_font, "F1-F9: Debug | H: Survival | T: Time Skip | WASD: Camera", new Vector2(10, 30), Color.LightGray);
                }
            }
            
            // Backstory and Path
            string traitInfo = $"{_player.Stats.Traits[0]} | {_player.Stats.SciencePath}";
            _spriteBatch.DrawString(_font, traitInfo, new Vector2(10, 640), Color.LightBlue);
            
            // Attributes (compact display)
            string attrInfo = _player.Stats.Attributes.GetDisplayString();
            _spriteBatch.DrawString(_font, attrInfo, new Vector2(10, 660), Color.LightGreen);
            
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
            
            // --- GROUND ITEMS COUNT ---
            if (_groundItems.Count > 0 && !_combat.InCombat)
            {
                _spriteBatch.DrawString(_font, $"Items nearby: {_groundItems.Count}", new Vector2(1100, 680), Color.Gold);
            }

            _spriteBatch.End();
            
            // --- INVENTORY UI (fullscreen overlay - separate batch) ---
            if (_inventoryOpen)
            {
                DrawInventoryUI();
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
            _spriteBatch.Begin();
            
            // Dark overlay
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 720), Color.Black * 0.85f);
            
            // Title
            string title = "INVENTORY";
            Vector2 titleSize = _font.MeasureString(title);
            _spriteBatch.DrawString(_font, title, new Vector2(640 - titleSize.X / 2, 20), Color.White);
            
            // Equipment panel (left side)
            DrawEquipmentPanel();
            
            // Inventory list (right side)
            DrawInventoryList();
            
            // Stats summary (bottom)
            DrawEquipmentStats();
            
            // Controls help
            _spriteBatch.DrawString(_font, "[I] Close | [W/S] Navigate | [Enter] Equip/Use | [X] Drop | [1-6] Unequip Slot", 
                new Vector2(200, 680), Color.Gray);
            
            _spriteBatch.End();
        }
        
        private void DrawEquipmentPanel()
        {
            int startX = 50;
            int startY = 70;
            int slotWidth = 180;
            int slotHeight = 40;
            int gap = 5;
            
            _spriteBatch.DrawString(_font, "EQUIPPED", new Vector2(startX, startY - 20), Color.Cyan);
            
            // Equipment slots to display
            var slots = new (EquipSlot slot, string label, string key)[]
            {
                (EquipSlot.MainHand, "Main Hand", "1"),
                (EquipSlot.OffHand, "Off Hand", "2"),
                (EquipSlot.Head, "Head", "3"),
                (EquipSlot.Torso, "Torso", "4"),
                (EquipSlot.Legs, "Legs", "5"),
                (EquipSlot.Feet, "Feet", "6"),
                (EquipSlot.Hands, "Hands", ""),
                (EquipSlot.Accessory1, "Accessory 1", ""),
                (EquipSlot.Accessory2, "Accessory 2", ""),
            };
            
            int y = startY;
            foreach (var (slot, label, key) in slots)
            {
                var equipped = _player.Stats.Inventory.GetEquipped(slot);
                
                // Slot background
                Color bgColor = equipped != null ? Color.DarkGreen * 0.8f : Color.DarkGray * 0.6f;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(startX, y, slotWidth, slotHeight), bgColor);
                
                // Border
                _spriteBatch.Draw(_pixelTexture, new Rectangle(startX, y, slotWidth, 1), Color.Gray);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(startX, y + slotHeight - 1, slotWidth, 1), Color.Gray);
                
                // Key hint
                if (!string.IsNullOrEmpty(key))
                {
                    _spriteBatch.DrawString(_font, $"[{key}]", new Vector2(startX + 2, y + 2), Color.Yellow * 0.7f);
                }
                
                // Slot label
                _spriteBatch.DrawString(_font, label + ":", new Vector2(startX + 30, y + 5), Color.LightGray);
                
                // Item name
                string itemName = equipped != null ? equipped.GetDisplayName() : "(empty)";
                Color nameColor = equipped != null ? GetItemQualityColor(equipped.Quality) : Color.Gray;
                _spriteBatch.DrawString(_font, itemName, new Vector2(startX + 5, y + 22), nameColor);
                
                y += slotHeight + gap;
            }
            
            // Total armor display
            float totalArmor = _player.Stats.GetTotalArmor();
            _spriteBatch.DrawString(_font, $"Total Armor: {totalArmor:F0}", new Vector2(startX, y + 10), Color.SteelBlue);
            
            // Weapon damage display
            var weapon = _player.Stats.GetEquippedWeapon();
            string weaponInfo = weapon != null ? $"Weapon Damage: {weapon.GetEffectiveDamage():F0}" : "Unarmed (10 damage)";
            _spriteBatch.DrawString(_font, weaponInfo, new Vector2(startX, y + 28), Color.Orange);
        }
        
        private void DrawInventoryList()
        {
            int startX = 500;
            int startY = 70;
            int itemWidth = 400;
            int itemHeight = 28;
            int maxVisible = 18;
            
            var items = _player.Stats.Inventory.GetAllItems();
            
            // Header
            _spriteBatch.DrawString(_font, $"ITEMS ({items.Count}/{_player.Stats.Inventory.MaxSlots})", new Vector2(startX, startY - 20), Color.Cyan);
            _spriteBatch.DrawString(_font, $"Weight: {_player.Stats.Inventory.CurrentWeight:F1}/{_player.Stats.Inventory.MaxWeight:F0} kg", 
                new Vector2(startX + 200, startY - 20), Color.LightGray);
            
            // Item list
            int y = startY;
            for (int i = 0; i < items.Count && i < maxVisible; i++)
            {
                var item = items[i];
                bool isSelected = (i == _selectedInventoryIndex);
                
                // Background
                Color bgColor = isSelected ? Color.DarkBlue * 0.8f : Color.DarkGray * 0.4f;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(startX, y, itemWidth, itemHeight), bgColor);
                
                // Selection indicator
                if (isSelected)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle(startX - 5, y, 5, itemHeight), Color.Yellow);
                }
                
                // Item icon (colored square based on category)
                Color iconColor = item.Category switch
                {
                    ItemCategory.Weapon => Color.Silver,
                    ItemCategory.Armor => Color.SteelBlue,
                    ItemCategory.Consumable => Color.LimeGreen,
                    ItemCategory.Material => Color.SandyBrown,
                    ItemCategory.Ammo => Color.Gold,
                    ItemCategory.Tool => Color.Orange,
                    _ => Color.White
                };
                _spriteBatch.Draw(_pixelTexture, new Rectangle(startX + 4, y + 4, 20, 20), iconColor);
                
                // Item name with quality color
                Color nameColor = GetItemQualityColor(item.Quality);
                string displayName = item.GetDisplayName();
                if (displayName.Length > 35) displayName = displayName.Substring(0, 32) + "...";
                _spriteBatch.DrawString(_font, displayName, new Vector2(startX + 28, y + 6), nameColor);
                
                // Weight on right
                _spriteBatch.DrawString(_font, $"{item.Weight:F1}kg", new Vector2(startX + itemWidth - 50, y + 6), Color.Gray);
                
                y += itemHeight;
            }
            
            // Empty inventory message
            if (items.Count == 0)
            {
                _spriteBatch.DrawString(_font, "Inventory is empty", new Vector2(startX + 100, startY + 50), Color.Gray);
            }
            
            // Scroll indicator if more items
            if (items.Count > maxVisible)
            {
                _spriteBatch.DrawString(_font, $"... and {items.Count - maxVisible} more items", 
                    new Vector2(startX, y + 10), Color.Gray);
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
        
        private void DrawCombatUI()
        {
            Color combatColor = _combat.IsPlayerTurn ? Color.LimeGreen : Color.OrangeRed;
            string turnText = _combat.IsPlayerTurn ? "YOUR TURN" : "ENEMY TURN";
            
            _spriteBatch.Draw(_pixelTexture, new Rectangle(10, 10, 200, 30), Color.Black * 0.7f);
            _spriteBatch.DrawString(_font, $"COMBAT - {turnText}", new Vector2(15, 15), combatColor);
            
            if (_combat.IsPlayerTurn)
            {
                _spriteBatch.DrawString(_font, $"AP: {_combat.PlayerActionPoints}/{_combat.PlayerMaxActionPoints}", new Vector2(15, 35), Color.Cyan);
                _spriteBatch.DrawString(_font, "Click: Move(1)/Attack(2) | Space: End Turn | Tab: Target", new Vector2(10, 55), Color.Yellow);
            }
            
            // Selected enemy info
            if (_selectedEnemy != null && _selectedEnemy.IsAlive)
            {
                _spriteBatch.Draw(_pixelTexture, new Rectangle(1080, 10, 190, 80), Color.Black * 0.7f);
                _spriteBatch.DrawString(_font, $"Target: {_selectedEnemy.Name}", new Vector2(1085, 15), Color.Yellow);
                _spriteBatch.DrawString(_font, $"HP: {_selectedEnemy.CurrentHealth:F0}/{_selectedEnemy.MaxHealth:F0}", new Vector2(1085, 35), Color.White);
                _spriteBatch.DrawString(_font, $"DMG: {_selectedEnemy.Damage} | ACC: {_selectedEnemy.Accuracy:P0}", new Vector2(1085, 55), Color.LightGray);
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
