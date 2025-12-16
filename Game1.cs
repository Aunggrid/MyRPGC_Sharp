using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using MyRPG.Engine;
using MyRPG.Gameplay.World;
using MyRPG.Gameplay.Entities;
using MyRPG.Gameplay.Systems;
using MyRPG.Gameplay.Combat;
using MyRPG.Gameplay.Character;
using MyRPG.Data;

namespace MyRPG
{
    // ============================================
    // GAME STATE
    // ============================================
    public enum GameState
    {
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
        private GameState _gameState = GameState.Playing;
        
        // ENEMIES
        private List<EnemyEntity> _enemies = new List<EnemyEntity>();
        
        // COMBAT
        private CombatManager _combat;
        private List<string> _combatLog = new List<string>();
        private const int MAX_LOG_LINES = 5;
        
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

            // Initialize player
            _player = new PlayerEntity();
            _player.Position = new Vector2(2 * _world.TileSize, 2 * _world.TileSize);
            _player.Initialize();
            
            // Spawn enemies
            SpawnEnemies();
            
            // Initialize combat manager
            _combat = new CombatManager(_player, _enemies);
            _combat.OnCombatLog += (msg) =>
            {
                _combatLog.Add(msg);
                if (_combatLog.Count > MAX_LOG_LINES)
                    _combatLog.RemoveAt(0);
            };
            
            // Reset state
            _gameState = GameState.Playing;
            _deathTimer = 0f;
            _combatLog.Clear();
            _selectedEnemy = null;
            _mutationChoices.Clear();
            
            System.Diagnostics.Debug.WriteLine(">>> NEW GAME STARTED <<<");
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
            
            // Debug keys (only in exploration)
            if (!_combat.InCombat)
            {
                HandleDebugKeys(kState);
            }

            // Camera controls
            float camSpeed = 500f * deltaTime;
            if (kState.IsKeyDown(Keys.W)) _camera.Position.Y -= camSpeed;
            if (kState.IsKeyDown(Keys.S)) _camera.Position.Y += camSpeed;
            if (kState.IsKeyDown(Keys.A)) _camera.Position.X -= camSpeed;
            if (kState.IsKeyDown(Keys.D)) _camera.Position.X += camSpeed;
            if (kState.IsKeyDown(Keys.Q)) _camera.Zoom -= 1f * deltaTime;
            if (kState.IsKeyDown(Keys.E)) _camera.Zoom += 1f * deltaTime;

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
            // Update enemies (AI)
            foreach (var enemy in _enemies)
            {
                enemy.Update(deltaTime, _world, _player.Position);
            }
            
            // Update combat manager (checks for combat trigger)
            _combat.Update(deltaTime, _world);
            
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
                        int dist = Math.Abs(clickTile.X - playerTile.X) + Math.Abs(clickTile.Y - playerTile.Y);
                        
                        if (dist <= 1)
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
                        int dist = Math.Abs(clickTile.X - playerTile.X) + Math.Abs(clickTile.Y - playerTile.Y);
                        
                        if (dist == 1)
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
        }

        // ============================================
        // DRAW
        // ============================================
        
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            switch (_gameState)
            {
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
        
        private void DrawPlaying()
        {
            // --- LAYER 1: WORLD ---
            _spriteBatch.Begin(transformMatrix: _camera.GetViewMatrix(), samplerState: SamplerState.PointClamp);

            _world.Draw(_spriteBatch);

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
                _spriteBatch.DrawString(_font, "Click: Move/Attack | M: Mutations | WASD: Camera", new Vector2(10, 10), Color.Yellow);
                _spriteBatch.DrawString(_font, "F1-F9: Debug", new Vector2(10, 30), Color.LightGray);
            }
            
            // Backstory
            string traitInfo = $"{_player.Stats.Traits[0]} | {_player.Stats.SciencePath}";
            _spriteBatch.DrawString(_font, traitInfo, new Vector2(10, 660), Color.LightBlue);

            _spriteBatch.End();
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
        
        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            GameServices.Shutdown();
            base.OnExiting(sender, args);
        }
    }
}
