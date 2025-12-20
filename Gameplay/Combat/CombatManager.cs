// Gameplay/Combat/CombatManager.cs
// Manages turn-based combat system

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MyRPG.Gameplay.Entities;
using MyRPG.Gameplay.World;
using MyRPG.Gameplay.Systems;
using MyRPG.Data;

namespace MyRPG.Gameplay.Combat
{
    public enum CombatPhase
    {
        Exploration,        // Real-time, no combat
        CombatStart,        // Transition into combat
        PlayerTurn,         // Player is acting
        EnemyTurn,          // Enemies are acting
        CombatEnd           // Combat resolution
    }
    
    public class CombatManager
    {
        // Combat State
        public CombatPhase Phase { get; private set; } = CombatPhase.Exploration;
        public bool InCombat => Phase != CombatPhase.Exploration;
        
        // Participants
        private PlayerEntity _player;
        private List<EnemyEntity> _allEnemies;
        private List<EnemyEntity> _combatEnemies = new List<EnemyEntity>();
        
        // Combat Zone (like BG3 but dynamic)
        public Vector2 CombatCenter { get; private set; }
        public int CombatZoneRadius { get; private set; } = 12;      // Current radius (grows)
        public int InitialZoneRadius { get; set; } = 12;             // Starting radius
        public int MaxZoneRadius { get; set; } = 25;                 // Maximum expansion
        public int ZoneEdgeThreshold { get; set; } = 3;              // Tiles from edge to trigger expansion
        public int ZoneExpansionRate { get; set; } = 2;              // Tiles to expand per trigger
        public int FleeExitRadius => CombatZoneRadius + 3;           // Always 3 beyond combat zone
        
        // Escape Mechanics (for future items/mutations)
        public bool PlayerCanEscape { get; private set; } = false;   // Set by items/abilities
        public bool PlayerIsHidden { get; private set; } = false;    // Stealth state
        public int EscapeAttempts { get; private set; } = 0;         // Track escape tries
        public int MaxEscapeAttempts { get; set; } = 3;              // Limit before zone locks
        
        // Zone expansion tracking
        private bool _zoneExpanded = false;
        private float _expansionCooldown = 0f;
        private const float EXPANSION_COOLDOWN_TIME = 2f;            // Seconds between expansions
        
        // Grid info
        private int _tileSize = 64;
        
        // Turn Order
        private List<object> _turnOrder = new List<object>(); // Mix of player and enemies
        private int _currentTurnIndex = 0;
        private object _currentActor => _turnOrder.Count > 0 ? _turnOrder[_currentTurnIndex] : null;
        
        // Player Combat - Separate AP (actions) and MP (movement)
        public int PlayerActionPoints { get; private set; } = 2;
        public int PlayerMovementPoints { get; private set; } = 4;
        public int PlayerMaxActionPoints => _player?.Stats?.ActionPoints ?? 2;
        public int PlayerMaxMovementPoints => _player?.Stats?.MovementPoints ?? 4;
        
        // Combat trigger range (tiles)
        public int CombatTriggerRange { get; set; } = 2;
        
        // Events for UI
        public event Action OnCombatStart;
        public event Action OnCombatEnd;
        public event Action<object> OnTurnStart;
        public event Action<string> OnCombatLog;
        public event Action<EnemyEntity, Vector2> OnEnemyKilled;  // Enemy killed, drop loot at position
        public event Action<int> OnZoneExpanded;                  // Zone radius expanded
        public event Action<EnemyType, Vector2> OnEnemySpawn;     // Enemy spawned (type, position)
        
        // Damage events for floating text
        public event Action<Vector2, float, bool> OnDamageDealt;  // Position, damage, isCritical
        public event Action<Vector2> OnMiss;                       // Position
        public event Action<Vector2, float> OnHeal;                // Position, amount
        public event Action<Vector2, string> OnStatusApplied;      // Position, status name
        
        // ============================================
        // INITIALIZATION
        // ============================================
        
        public CombatManager(PlayerEntity player, List<EnemyEntity> enemies, int tileSize = 64)
        {
            _player = player;
            _allEnemies = enemies;
            _tileSize = tileSize;
        }
        
        public void UpdateEnemyList(List<EnemyEntity> enemies)
        {
            _allEnemies = enemies;
        }
        
        // ============================================
        // UPDATE
        // ============================================
        
        public void Update(float deltaTime, WorldGrid grid)
        {
            // Update expansion cooldown
            if (_expansionCooldown > 0)
            {
                _expansionCooldown -= deltaTime;
            }
            
            switch (Phase)
            {
                case CombatPhase.Exploration:
                    CheckCombatTrigger(grid);
                    break;
                    
                case CombatPhase.CombatStart:
                    InitializeCombat();
                    break;
                    
                case CombatPhase.PlayerTurn:
                    // Check if player is trying to escape (near edge of zone)
                    CheckZoneExpansion();
                    // Check if new enemies should join the fight
                    CheckForReinforcements(grid);
                    // Player turn is handled by input in Game1
                    break;
                    
                case CombatPhase.EnemyTurn:
                    ProcessEnemyTurn(grid);
                    break;
                    
                case CombatPhase.CombatEnd:
                    EndCombat();
                    break;
            }
        }
        
        /// <summary>
        /// Check if player is near edge of combat zone and expand if needed
        /// </summary>
        private void CheckZoneExpansion()
        {
            if (!InCombat) return;
            if (_expansionCooldown > 0) return;
            if (CombatZoneRadius >= MaxZoneRadius) return;
            
            // Check player's escape ability
            if (PlayerCanEscape || PlayerIsHidden)
            {
                return; // Player has escape ability, don't expand
            }
            
            // Calculate player distance from combat center
            float distanceToCenter = Vector2.Distance(_player.Position, CombatCenter);
            int tileDistanceToCenter = (int)(distanceToCenter / _tileSize);
            
            // Check if player is near the edge of the zone
            int distanceFromEdge = CombatZoneRadius - tileDistanceToCenter;
            
            if (distanceFromEdge <= ZoneEdgeThreshold)
            {
                ExpandCombatZone();
            }
        }
        
        /// <summary>
        /// Expand the combat zone and pull in new enemies
        /// </summary>
        private void ExpandCombatZone()
        {
            int oldRadius = CombatZoneRadius;
            CombatZoneRadius = Math.Min(CombatZoneRadius + ZoneExpansionRate, MaxZoneRadius);
            
            if (CombatZoneRadius > oldRadius)
            {
                _expansionCooldown = EXPANSION_COOLDOWN_TIME;
                _zoneExpanded = true;
                EscapeAttempts++;
                
                Log($"Combat zone expanded! (Radius: {CombatZoneRadius})");
                System.Diagnostics.Debug.WriteLine($">>> Combat zone expanded: {oldRadius} -> {CombatZoneRadius} <<<");
                
                // Pull in new enemies that are now inside the zone
                PullInNewEnemies();
                
                OnZoneExpanded?.Invoke(CombatZoneRadius);
                
                // Check if max escape attempts reached
                if (EscapeAttempts >= MaxEscapeAttempts && CombatZoneRadius < MaxZoneRadius)
                {
                    CombatZoneRadius = MaxZoneRadius;
                    Log("No escape! Combat zone locked at maximum.");
                }
            }
        }
        
        /// <summary>
        /// Pull in enemies that are now inside the expanded combat zone
        /// </summary>
        private void PullInNewEnemies()
        {
            foreach (var enemy in _allEnemies)
            {
                if (!enemy.IsAlive) continue;
                if (_combatEnemies.Contains(enemy)) continue;
                
                float distanceToCenter = Vector2.Distance(enemy.Position, CombatCenter);
                int tileDistance = (int)(distanceToCenter / _tileSize);
                
                if (tileDistance <= CombatZoneRadius)
                {
                    AddEnemyToCombat(enemy);
                    
                    // Add to turn order if combat already initialized
                    if (_turnOrder.Count > 0 && !_turnOrder.Contains(enemy))
                    {
                        _turnOrder.Add(enemy);
                        Log($"{enemy.Name} joined the combat!");
                    }
                }
            }
        }
        
        /// <summary>
        /// Check if any enemies not in combat should join the fight
        /// </summary>
        private void CheckForReinforcements(WorldGrid grid)
        {
            bool addedNew = false;
            
            foreach (var enemy in _allEnemies)
            {
                if (!enemy.IsAlive) continue;
                if (_combatEnemies.Contains(enemy)) continue; // Already in combat
                
                // Calculate distance to combat center (for zone check)
                float distanceToCenter = Vector2.Distance(enemy.Position, CombatCenter);
                int tileDistanceToCenter = (int)(distanceToCenter / grid.TileSize);
                
                // Calculate distance to player (for aggro check)
                float distanceToPlayer = enemy.GetDistanceToPlayer(_player.Position);
                int tileDistanceToPlayer = (int)(distanceToPlayer / grid.TileSize);
                
                // PRIORITY 1: If enemy is INSIDE the combat zone, they MUST join combat
                // This is the key fix - any enemy in the zone joins immediately
                if (tileDistanceToCenter <= CombatZoneRadius)
                {
                    AddEnemyToCombat(enemy);
                    
                    // Add to turn order (will act next round)
                    if (!_turnOrder.Contains(enemy))
                    {
                        _turnOrder.Add(enemy);
                        enemy.StartTurn(); // Give them AP
                    }
                    
                    addedNew = true;
                    Log($"{enemy.Name} entered the combat zone!");
                    System.Diagnostics.Debug.WriteLine($">>> {enemy.Name} ENTERED COMBAT ZONE and joins combat! <<<");
                    continue;
                }
                
                // PRIORITY 2: If enemy is close enough to player OR chasing, join combat
                // (This is for enemies just outside the zone who are aggressive)
                if (tileDistanceToPlayer <= CombatTriggerRange || enemy.State == EnemyState.Chasing)
                {
                    AddEnemyToCombat(enemy);
                    
                    // Add to turn order (will act next round)
                    if (!_turnOrder.Contains(enemy))
                    {
                        _turnOrder.Add(enemy);
                        enemy.StartTurn(); // Give them AP
                    }
                    
                    addedNew = true;
                    Log($"{enemy.Name} joins the fight!");
                    System.Diagnostics.Debug.WriteLine($">>> {enemy.Name} JOINS COMBAT (aggro)! <<<");
                }
            }
            
            if (addedNew)
            {
                System.Diagnostics.Debug.WriteLine($">>> Combat now has {_combatEnemies.Count} enemies <<<");
            }
        }
        
        // ============================================
        // COMBAT TRIGGER
        // ============================================
        
        private void CheckCombatTrigger(WorldGrid grid)
        {
            foreach (var enemy in _allEnemies)
            {
                if (!enemy.IsAlive) continue;
                
                // Only aggressive or provoked enemies trigger combat
                if (enemy.Behavior != CreatureBehavior.Aggressive && !enemy.IsProvoked)
                    continue;
                
                // Check if enemy is chasing (detected player)
                if (enemy.State == EnemyState.Chasing)
                {
                    float distance = enemy.GetDistanceToPlayer(_player.Position);
                    int tileDistance = (int)(distance / grid.TileSize);
                    
                    // If close enough, trigger combat
                    if (tileDistance <= CombatTriggerRange)
                    {
                        StartCombat(enemy);
                        return;
                    }
                }
            }
        }
        
        /// <summary>
        /// Force start combat (e.g., player attacks first)
        /// </summary>
        public void StartCombat(EnemyEntity triggeringEnemy = null)
        {
            if (InCombat) return;
            
            Phase = CombatPhase.CombatStart;
            
            // Reset combat zone to initial state
            CombatZoneRadius = InitialZoneRadius;
            EscapeAttempts = 0;
            PlayerCanEscape = false;
            PlayerIsHidden = false;
            _zoneExpanded = false;
            _expansionCooldown = 0f;
            
            // IMPORTANT: Clear player's exploration path so they don't continue moving after combat
            if (_player.CurrentPath != null)
            {
                _player.CurrentPath.Clear();
            }
            _player.CurrentPath = null;
            
            // SNAP PLAYER TO NEAREST GRID TILE
            _player.Position = SnapToGrid(_player.Position);
            System.Diagnostics.Debug.WriteLine($">>> Player snapped to grid: ({_player.Position.X}, {_player.Position.Y}) <<<");
            
            // Set combat center (midpoint between player and triggering enemy, or player position)
            if (triggeringEnemy != null)
            {
                CombatCenter = ((_player.Position + triggeringEnemy.Position) / 2f);
            }
            else
            {
                CombatCenter = _player.Position;
            }
            System.Diagnostics.Debug.WriteLine($">>> Combat Zone Center: ({CombatCenter.X}, {CombatCenter.Y}), Radius: {CombatZoneRadius} tiles <<<");
            
            // Gather ALL enemies within combat zone (like BG3)
            // Hostile enemies will fight, passive/cowardly will flee or wander
            _combatEnemies.Clear();
            bool hasHostile = false;
            
            foreach (var enemy in _allEnemies)
            {
                if (!enemy.IsAlive) continue;
                
                float distanceToCenter = Vector2.Distance(enemy.Position, CombatCenter);
                int tileDistance = (int)(distanceToCenter / _tileSize);
                
                // Include ALL enemies within combat zone radius, or the triggering enemy
                if (tileDistance <= CombatZoneRadius || enemy == triggeringEnemy)
                {
                    AddEnemyToCombat(enemy);
                    
                    // Track if we have any hostile enemies
                    if (enemy.Behavior == CreatureBehavior.Aggressive || enemy.IsProvoked)
                    {
                        hasHostile = true;
                    }
                }
            }
            
            // If no hostile enemies, don't start combat (only passive mobs in zone)
            if (!hasHostile)
            {
                // Clear combat state for passive mobs
                foreach (var enemy in _combatEnemies)
                {
                    enemy.InCombatZone = false;
                    enemy.State = EnemyState.Idle;
                }
                _combatEnemies.Clear();
                Phase = CombatPhase.Exploration;
                System.Diagnostics.Debug.WriteLine(">>> No hostile enemies nearby, combat cancelled <<<");
                return;
            }
            
            int hostileCount = _combatEnemies.Count(e => e.Behavior == CreatureBehavior.Aggressive || e.IsProvoked);
            int passiveCount = _combatEnemies.Count - hostileCount;
            Log($"Combat started! {hostileCount} hostile, {passiveCount} passive creatures in zone.");
            System.Diagnostics.Debug.WriteLine($">>> COMBAT STARTED: {hostileCount} hostile, {passiveCount} passive <<<");
            
            OnCombatStart?.Invoke();
        }
        
        /// <summary>
        /// Add an enemy to combat (snap to grid, set state)
        /// </summary>
        private void AddEnemyToCombat(EnemyEntity enemy)
        {
            if (_combatEnemies.Contains(enemy)) return;
            
            _combatEnemies.Add(enemy);
            enemy.State = EnemyState.Chasing;
            enemy.InCombatZone = true;
            
            if (enemy.CurrentPath != null)
            {
                enemy.CurrentPath.Clear();
            }
            
            // SNAP ENEMY TO NEAREST GRID TILE
            enemy.Position = SnapToGrid(enemy.Position);
            System.Diagnostics.Debug.WriteLine($">>> {enemy.Name} joined combat <<<");
        }
        
        /// <summary>
        /// Remove an enemy from combat (fled or exited zone)
        /// </summary>
        public void RemoveEnemyFromCombat(EnemyEntity enemy)
        {
            if (!_combatEnemies.Contains(enemy)) return;
            
            _combatEnemies.Remove(enemy);
            _turnOrder.Remove(enemy);
            enemy.InCombatZone = false;
            enemy.State = EnemyState.Idle;
            enemy.CurrentActionPoints = enemy.MaxActionPoints;
            
            // Adjust turn index if needed
            if (_currentTurnIndex >= _turnOrder.Count && _turnOrder.Count > 0)
            {
                _currentTurnIndex = 0;
            }
            
            Log($"{enemy.Name} fled from combat!");
            System.Diagnostics.Debug.WriteLine($">>> {enemy.Name} exited combat zone <<<");
        }
        
        /// <summary>
        /// Pull an enemy into combat immediately (called when enemy enters combat zone)
        /// </summary>
        public void PullEnemyIntoCombat(EnemyEntity enemy)
        {
            if (!InCombat) return;
            if (_combatEnemies.Contains(enemy)) return;
            
            AddEnemyToCombat(enemy);
            
            // Add to turn order (will act when their turn comes)
            if (!_turnOrder.Contains(enemy))
            {
                _turnOrder.Add(enemy);
                enemy.StartTurn(); // Give them AP
            }
            
            Log($"{enemy.Name} entered the combat zone!");
            System.Diagnostics.Debug.WriteLine($">>> {enemy.Name} PULLED INTO COMBAT (entered zone) <<<");
        }
        
        /// <summary>
        /// Check if a position is within the combat zone
        /// </summary>
        public bool IsInCombatZone(Vector2 position)
        {
            float distance = Vector2.Distance(position, CombatCenter);
            int tileDistance = (int)(distance / _tileSize);
            return tileDistance <= CombatZoneRadius;
        }
        
        /// <summary>
        /// Check if an enemy has fled far enough to exit combat
        /// </summary>
        public bool HasFledCombat(EnemyEntity enemy)
        {
            float distance = Vector2.Distance(enemy.Position, CombatCenter);
            int tileDistance = (int)(distance / _tileSize);
            return tileDistance > FleeExitRadius;
        }
        
        /// <summary>
        /// Get enemies that are NOT in combat (for real-time updates)
        /// </summary>
        public List<EnemyEntity> GetNonCombatEnemies()
        {
            return _allEnemies.Where(e => e.IsAlive && !_combatEnemies.Contains(e)).ToList();
        }
        
        // ============================================
        // COMBAT INITIALIZATION
        // ============================================
        
        private void InitializeCombat()
        {
            // Roll initiative for all participants
            var random = new Random();
            
            // Player initiative (based on speed)
            int playerInit = (int)(_player.Stats.Speed / 10) + random.Next(1, 21);
            
            // Enemy initiatives
            foreach (var enemy in _combatEnemies)
            {
                enemy.Initiative = (int)(enemy.Speed / 10) + random.Next(1, 21);
            }
            
            // Build turn order (highest initiative first)
            _turnOrder.Clear();
            _turnOrder.Add(_player);
            _turnOrder.AddRange(_combatEnemies.Cast<object>());
            
            // Sort by initiative (we need to store player initiative somewhere)
            // For simplicity, player goes first if tied
            _turnOrder = _turnOrder.OrderByDescending(actor =>
            {
                if (actor is PlayerEntity) return playerInit;
                if (actor is EnemyEntity e) return e.Initiative;
                return 0;
            }).ToList();
            
            // Log turn order
            System.Diagnostics.Debug.WriteLine(">>> TURN ORDER <<<");
            for (int i = 0; i < _turnOrder.Count; i++)
            {
                string name = _turnOrder[i] is PlayerEntity ? "Player" : ((EnemyEntity)_turnOrder[i]).Name;
                int init = _turnOrder[i] is PlayerEntity ? playerInit : ((EnemyEntity)_turnOrder[i]).Initiative;
                System.Diagnostics.Debug.WriteLine($"  {i + 1}. {name} (Init: {init})");
            }
            
            // Start first turn
            _currentTurnIndex = 0;
            StartCurrentTurn();
        }
        
        // ============================================
        // TURN MANAGEMENT
        // ============================================
        
        private void StartCurrentTurn()
        {
            // Remove dead actors
            CleanupDeadActors();
            
            // Check combat end conditions
            if (CheckCombatEnd()) return;
            
            // Wrap around if needed
            if (_currentTurnIndex >= _turnOrder.Count)
            {
                _currentTurnIndex = 0;
            }
            
            var actor = _currentActor;
            
            if (actor is PlayerEntity)
            {
                Phase = CombatPhase.PlayerTurn;
                
                // Base AP + Reserved AP from previous turn(s)
                int reservedAP = _player.Stats.ReservedAP;
                PlayerActionPoints = PlayerMaxActionPoints + reservedAP;
                PlayerMovementPoints = PlayerMaxMovementPoints;
                
                // Clear reserved AP since we've added it
                _player.Stats.ReservedAP = 0;
                
                // Check if player is stunned
                if (_player.HasStatus(StatusEffectType.Stunned))
                {
                    PlayerActionPoints = 0;
                    PlayerMovementPoints = 0;
                    Log("Player is STUNNED! Turn skipped.");
                    System.Diagnostics.Debug.WriteLine(">>> Player is STUNNED! Skipping turn. <<<");
                    EndCurrentTurn();
                    return;
                }
                
                string reservedStr = reservedAP > 0 ? $" (+{reservedAP} reserved)" : "";
                Log($"Your turn! {PlayerActionPoints} AP{reservedStr}, {PlayerMovementPoints} MP available.");
                System.Diagnostics.Debug.WriteLine($">>> PLAYER TURN - {PlayerActionPoints} AP{reservedStr}, {PlayerMovementPoints} MP <<<");
            }
            else if (actor is EnemyEntity enemy)
            {
                Phase = CombatPhase.EnemyTurn;
                enemy.StartTurn();
                System.Diagnostics.Debug.WriteLine($">>> {enemy.Name}'s TURN <<<");
            }
            
            OnTurnStart?.Invoke(actor);
        }
        
        private void ProcessEnemyTurn(WorldGrid grid)
        {
            if (_currentActor is EnemyEntity enemy)
            {
                // Check if this enemy has fled far enough to exit combat
                if (HasFledCombat(enemy))
                {
                    RemoveEnemyFromCombat(enemy);
                    
                    // Check if combat should end after this removal
                    if (CheckCombatEnd()) return;
                    
                    // Skip to next turn if this enemy was removed
                    if (_turnOrder.Count > 0)
                    {
                        // Adjust index since we removed an element
                        if (_currentTurnIndex >= _turnOrder.Count)
                        {
                            _currentTurnIndex = 0;
                        }
                        StartCurrentTurn();
                    }
                    return;
                }
                
                // Skip enemies that are no longer in combat zone (shouldn't happen, but safety check)
                if (!enemy.InCombatZone)
                {
                    System.Diagnostics.Debug.WriteLine($">>> WARNING: {enemy.Name} not in combat zone but still in turn order, skipping <<<");
                    EndCurrentTurn();
                    return;
                }
                
                // Pass all enemies and combat center so enemy can behave appropriately
                bool turnDone = enemy.TakeTurn(grid, _player, _allEnemies, CombatCenter);
                
                // Check if enemy used an ability and handle the result
                if (enemy.LastAbilityResult != null && enemy.LastAbilityResult.Success)
                {
                    HandleAbilityResult(enemy.LastAbilityResult, grid);
                }
                
                // After turn, check again if they fled out of combat
                if (HasFledCombat(enemy))
                {
                    RemoveEnemyFromCombat(enemy);
                    
                    // Check if combat should end after this removal
                    if (CheckCombatEnd()) return;
                    
                    // IMPORTANT: Always advance to next turn after enemy is removed,
                    // regardless of whether their turn was "done"
                    if (_turnOrder.Count > 0)
                    {
                        if (_currentTurnIndex >= _turnOrder.Count)
                        {
                            _currentTurnIndex = 0;
                        }
                        StartCurrentTurn();
                    }
                    return;
                }
                
                if (turnDone)
                {
                    EndCurrentTurn();
                }
            }
            else
            {
                // Safety: if current actor is not an enemy during enemy turn, skip
                System.Diagnostics.Debug.WriteLine(">>> WARNING: Non-enemy actor during enemy turn, skipping <<<");
                EndCurrentTurn();
            }
        }
        
        public void EndCurrentTurn()
        {
            // Save unused AP to reserved pool (player only)
            if (_currentActor is PlayerEntity && PlayerActionPoints > 0)
            {
                _player.Stats.ReserveAP(PlayerActionPoints);
                if (_player.Stats.ReservedAP > 0)
                {
                    Log($"Reserved {Math.Min(PlayerActionPoints, _player.Stats.MaxReservedAP)} AP for next turn. (Total: {_player.Stats.ReservedAP})");
                }
            }
            
            _currentTurnIndex++;
            
            // Wrap around
            if (_currentTurnIndex >= _turnOrder.Count)
            {
                _currentTurnIndex = 0;
                System.Diagnostics.Debug.WriteLine(">>> NEW ROUND <<<");
            }
            
            StartCurrentTurn();
        }
        
        /// <summary>
        /// Convert AP to MP (1 AP = 1 MP)
        /// </summary>
        public bool ConvertAPtoMP(int apToConvert = 1)
        {
            if (PlayerActionPoints < apToConvert)
            {
                Log("Not enough AP to convert!");
                return false;
            }
            
            PlayerActionPoints -= apToConvert;
            int mpGained = apToConvert;  // 1 AP = 1 MP
            PlayerMovementPoints += mpGained;
            
            Log($"Converted {apToConvert} AP to {mpGained} MP. ({PlayerActionPoints} AP, {PlayerMovementPoints} MP remaining)");
            System.Diagnostics.Debug.WriteLine($">>> Converted {apToConvert} AP to {mpGained} MP <<<");
            
            return true;
        }
        
        private void CleanupDeadActors()
        {
            // Remove dead actors AND actors that have left the combat zone
            _turnOrder.RemoveAll(actor =>
            {
                if (actor is EnemyEntity enemy)
                {
                    if (!enemy.IsAlive)
                    {
                        _combatEnemies.Remove(enemy);
                        return true;
                    }
                    // Also remove enemies that are no longer in the combat zone
                    if (!enemy.InCombatZone)
                    {
                        _combatEnemies.Remove(enemy);
                        return true;
                    }
                }
                return false;
            });
            
            // Adjust index if needed
            if (_currentTurnIndex >= _turnOrder.Count)
            {
                _currentTurnIndex = 0;
            }
        }
        
        private bool CheckCombatEnd()
        {
            // Player died
            if (!_player.IsAlive)
            {
                Phase = CombatPhase.CombatEnd;
                Log("You have been defeated...");
                return true;
            }
            
            // Check if any hostile or provoked enemies remain IN THE COMBAT ZONE
            bool hostileRemaining = _combatEnemies.Any(e => 
                e.IsAlive && e.InCombatZone && (e.Behavior == CreatureBehavior.Aggressive || e.IsProvoked));
            
            if (!hostileRemaining)
            {
                Phase = CombatPhase.CombatEnd;
                Log("Victory! All hostile enemies defeated or fled.");
                return true;
            }
            
            return false;
        }
        
        // ============================================
        // PLAYER ACTIONS
        // ============================================
        
        /// <summary>
        /// Player moves one tile (costs 1 MP, or auto-converts AP if out of MP)
        /// </summary>
        public bool PlayerMove(Point targetTile, WorldGrid grid)
        {
            if (Phase != CombatPhase.PlayerTurn) return false;
            
            // Auto-convert AP to MP if out of MP but have AP
            if (PlayerMovementPoints < 1)
            {
                if (PlayerActionPoints >= 1)
                {
                    // Auto-convert 1 AP → 1 MP
                    PlayerActionPoints--;
                    PlayerMovementPoints += 1;
                    Log($"Auto-converted 1 AP to 1 MP for movement.");
                    System.Diagnostics.Debug.WriteLine($">>> Auto-converted 1 AP to 1 MP <<<");
                }
                else
                {
                    Log("No movement points or AP left!");
                    return false;
                }
            }
            
            Point playerTile = new Point(
                (int)(_player.Position.X / grid.TileSize),
                (int)(_player.Position.Y / grid.TileSize)
            );
            
            // Check if adjacent (including diagonals - Chebyshev distance)
            if (!Pathfinder.IsAdjacent(playerTile, targetTile)) return false;
            
            // Check if walkable
            if (!grid.Tiles[targetTile.X, targetTile.Y].IsWalkable) return false;
            
            // For diagonal movement, check corner cutting
            int dx = targetTile.X - playerTile.X;
            int dy = targetTile.Y - playerTile.Y;
            if (dx != 0 && dy != 0)
            {
                // Diagonal - check both adjacent cardinal tiles
                Point cardinalX = new Point(playerTile.X + dx, playerTile.Y);
                Point cardinalY = new Point(playerTile.X, playerTile.Y + dy);
                
                bool xBlocked = !grid.Tiles[cardinalX.X, cardinalX.Y].IsWalkable;
                bool yBlocked = !grid.Tiles[cardinalY.X, cardinalY.Y].IsWalkable;
                
                if (xBlocked && yBlocked)
                {
                    Log("Can't squeeze through that corner!");
                    return false;
                }
            }
            
            // Move (costs MP)
            _player.Position = new Vector2(targetTile.X * grid.TileSize, targetTile.Y * grid.TileSize);
            PlayerMovementPoints--;
            
            string dirStr = GetDirectionName(dx, dy);
            Log($"Moved {dirStr}. {PlayerMovementPoints} MP, {PlayerActionPoints} AP left.");
            System.Diagnostics.Debug.WriteLine($">>> Player moves {dirStr}. {PlayerMovementPoints} MP, {PlayerActionPoints} AP left. <<<");
            
            // Turn ends when both AP and MP are depleted
            if (PlayerActionPoints <= 0 && PlayerMovementPoints <= 0) EndCurrentTurn();
            
            return true;
        }
        
        private string GetDirectionName(int dx, int dy)
        {
            if (dx == 0 && dy < 0) return "North";
            if (dx > 0 && dy < 0) return "NE";
            if (dx > 0 && dy == 0) return "East";
            if (dx > 0 && dy > 0) return "SE";
            if (dx == 0 && dy > 0) return "South";
            if (dx < 0 && dy > 0) return "SW";
            if (dx < 0 && dy == 0) return "West";
            if (dx < 0 && dy < 0) return "NW";
            return "";
        }
        
        /// <summary>
        /// Player attacks an enemy (costs 2 AP)
        /// </summary>
        public bool PlayerAttack(EnemyEntity target, WorldGrid grid)
        {
            if (Phase != CombatPhase.PlayerTurn) return false;
            if (PlayerActionPoints < 1) return false;  // Attacks cost 1 AP now
            if (!target.IsAlive) return false;
            
            // Check range (based on equipped weapon) - uses Chebyshev distance for diagonal
            int attackRange = _player.Stats.GetAttackRange();
            Point playerTile = new Point(
                (int)(_player.Position.X / grid.TileSize),
                (int)(_player.Position.Y / grid.TileSize)
            );
            Point enemyTile = target.GetTilePosition(grid.TileSize);
            int dist = Pathfinder.GetDistance(playerTile, enemyTile); // Chebyshev distance
            
            if (dist > attackRange)
            {
                Log($"Target is too far! (Range: {attackRange})");
                return false;
            }
            
            // Check if can attack (has ammo etc)
            if (!_player.Stats.CanAttack())
            {
                Log("Cannot attack! Check weapon/ammo.");
                return false;
            }
            
            // Start attack animation (lunge toward target)
            _player.StartAttackAnimation(target.Position, grid.TileSize);
            
            // Calculate hit chance based on distance (ranged weapons have penalties at close range)
            float hitChance = _player.Stats.GetHitChance(dist);
            
            // Roll to hit
            var random = new Random();
            float roll = (float)random.NextDouble();
            
            PlayerActionPoints--;  // 1 AP per attack
            
            // Consume ammo if needed
            _player.Stats.ConsumeAmmoForAttack();
            
            if (roll <= hitChance)
            {
                // Hit!
                float damage = _player.Stats.Damage;
                target.TakeDamage(damage, DamageType.Physical);
                Log($"Hit {target.Name} for {damage:F0} damage! ({hitChance:P0} chance)");
                System.Diagnostics.Debug.WriteLine($">>> Player hits {target.Name} for {damage:F0} damage! <<<");
                
                // Fire damage event for floating text
                OnDamageDealt?.Invoke(target.Position + new Vector2(16, 0), damage, false);
                
                // Grant XP and trigger loot if killed
                if (!target.IsAlive)
                {
                    float xp = target.MaxHealth; // XP = enemy max health
                    _player.Stats.AddXP(xp);
                    
                    // Check for explosion on death
                    HandleEnemyDeath(target);
                    
                    // Fire event for loot drop (handled by Game1)
                    OnEnemyKilled?.Invoke(target, target.Position);
                    
                    Log($"{target.Name} defeated! +{xp} XP");
                }
            }
            else
            {
                Log($"Missed {target.Name}! ({hitChance:P0} chance)");
                System.Diagnostics.Debug.WriteLine($">>> Player misses {target.Name}! <<<");
                
                // Fire miss event for floating text
                OnMiss?.Invoke(target.Position + new Vector2(16, 0));
            }
            
            // Turn ends only when both AP and MP are depleted
            if (PlayerActionPoints <= 0 && PlayerMovementPoints <= 0) EndCurrentTurn();
            
            return true;
        }
        
        /// <summary>
        /// Player performs melee attack (bash/pistol whip) even with ranged weapon equipped
        /// Higher accuracy but lower damage than ranged
        /// </summary>
        public bool PlayerMeleeAttack(EnemyEntity target, WorldGrid grid)
        {
            if (Phase != CombatPhase.PlayerTurn) return false;
            if (PlayerActionPoints < 1) return false;
            if (!target.IsAlive) return false;
            
            // Must be adjacent for melee
            Point playerTile = new Point(
                (int)(_player.Position.X / grid.TileSize),
                (int)(_player.Position.Y / grid.TileSize)
            );
            Point enemyTile = target.GetTilePosition(grid.TileSize);
            int dist = Pathfinder.GetDistance(playerTile, enemyTile);
            
            if (dist > 1)
            {
                Log("Too far for melee attack!");
                return false;
            }
            
            // Start attack animation
            _player.StartAttackAnimation(target.Position, grid.TileSize);
            
            // Melee accuracy is generally higher
            float hitChance = _player.Stats.GetMeleeAccuracy();
            
            var random = new Random();
            float roll = (float)random.NextDouble();
            
            PlayerActionPoints--;
            
            // No ammo consumed for melee
            
            if (roll <= hitChance)
            {
                // Hit with melee damage
                float damage = _player.Stats.GetMeleeDamageWithRangedWeapon();
                target.TakeDamage(damage, DamageType.Physical);
                Log($"Melee hit {target.Name} for {damage:F0} damage! ({hitChance:P0} chance)");
                System.Diagnostics.Debug.WriteLine($">>> Player melee hits {target.Name} for {damage:F0} damage! <<<");
                
                OnDamageDealt?.Invoke(target.Position + new Vector2(16, 0), damage, false);
                
                if (!target.IsAlive)
                {
                    float xp = target.MaxHealth;
                    _player.Stats.AddXP(xp);
                    HandleEnemyDeath(target);
                    OnEnemyKilled?.Invoke(target, target.Position);
                    Log($"{target.Name} defeated! +{xp} XP");
                }
            }
            else
            {
                Log($"Melee missed {target.Name}! ({hitChance:P0} chance)");
                OnMiss?.Invoke(target.Position + new Vector2(16, 0));
            }
            
            if (PlayerActionPoints <= 0 && PlayerMovementPoints <= 0) EndCurrentTurn();
            
            return true;
        }
        
        /// <summary>
        /// Player ends turn early
        /// </summary>
        public void PlayerEndTurn()
        {
            if (Phase != CombatPhase.PlayerTurn) return;
            
            Log("Turn ended.");
            System.Diagnostics.Debug.WriteLine(">>> Player ends turn early. <<<");
            EndCurrentTurn();
        }
        
        /// <summary>
        /// Player waits (regain 1 AP next turn? For now just ends turn)
        /// </summary>
        public void PlayerWait()
        {
            PlayerEndTurn();
        }
        
        // ============================================
        // COMBAT END
        // ============================================
        
        private void EndCombat()
        {
            System.Diagnostics.Debug.WriteLine(">>> COMBAT ENDED <<<");
            
            // Clear InCombatZone flag for all enemies that were in combat
            foreach (var enemy in _combatEnemies)
            {
                enemy.InCombatZone = false;
                enemy.CurrentActionPoints = enemy.MaxActionPoints; // Reset AP
            }
            
            _combatEnemies.Clear();
            _turnOrder.Clear();
            _currentTurnIndex = 0;
            
            Phase = CombatPhase.Exploration;
            
            OnCombatEnd?.Invoke();
        }
        
        /// <summary>
        /// Force end combat (e.g., player fled)
        /// </summary>
        public void ForceCombatEnd()
        {
            Log("Combat ended.");
            Phase = CombatPhase.CombatEnd;
            EndCombat();
        }
        
        // ============================================
        // ESCAPE MECHANICS (for future items/mutations)
        // ============================================
        
        /// <summary>
        /// Enable escape ability (from items, mutations, abilities)
        /// </summary>
        public void EnableEscape(string source = "ability")
        {
            PlayerCanEscape = true;
            Log($"Escape enabled! ({source})");
            System.Diagnostics.Debug.WriteLine($">>> Player can escape combat ({source}) <<<");
        }
        
        /// <summary>
        /// Disable escape ability
        /// </summary>
        public void DisableEscape()
        {
            PlayerCanEscape = false;
        }
        
        /// <summary>
        /// Enter stealth/hidden state - prevents zone expansion
        /// </summary>
        public void EnterStealth(string source = "ability")
        {
            PlayerIsHidden = true;
            Log($"Player is hidden! ({source})");
            System.Diagnostics.Debug.WriteLine($">>> Player entered stealth ({source}) <<<");
        }
        
        /// <summary>
        /// Exit stealth/hidden state
        /// </summary>
        public void ExitStealth()
        {
            if (PlayerIsHidden)
            {
                PlayerIsHidden = false;
                Log("Player is no longer hidden.");
            }
        }
        
        /// <summary>
        /// Attempt to escape combat (requires PlayerCanEscape or PlayerIsHidden)
        /// </summary>
        /// <returns>True if escape successful</returns>
        public bool TryEscape()
        {
            if (!InCombat) return true;
            
            if (PlayerIsHidden)
            {
                Log("Escaped while hidden!");
                ForceCombatEnd();
                return true;
            }
            
            if (PlayerCanEscape)
            {
                // Check if player is near edge of zone
                float distanceToCenter = Vector2.Distance(_player.Position, CombatCenter);
                int tileDistanceToCenter = (int)(distanceToCenter / _tileSize);
                int distanceFromEdge = CombatZoneRadius - tileDistanceToCenter;
                
                if (distanceFromEdge <= ZoneEdgeThreshold)
                {
                    Log("Successfully escaped combat!");
                    ForceCombatEnd();
                    return true;
                }
                else
                {
                    Log("Move to the edge of combat zone to escape!");
                    return false;
                }
            }
            
            Log("Cannot escape - no escape ability active!");
            return false;
        }
        
        /// <summary>
        /// Get current escape status for UI
        /// </summary>
        public (bool canEscape, bool isHidden, int attempts, int maxAttempts) GetEscapeStatus()
        {
            return (PlayerCanEscape, PlayerIsHidden, EscapeAttempts, MaxEscapeAttempts);
        }
        
        /// <summary>
        /// Get zone expansion info for UI
        /// </summary>
        public (int current, int max, int initial, bool expanded) GetZoneInfo()
        {
            return (CombatZoneRadius, MaxZoneRadius, InitialZoneRadius, _zoneExpanded);
        }
        
        // ============================================
        // HELPERS
        // ============================================
        
        public EnemyEntity GetNearestEnemy(WorldGrid grid)
        {
            EnemyEntity nearest = null;
            float nearestDist = float.MaxValue;
            
            foreach (var enemy in _combatEnemies)
            {
                if (!enemy.IsAlive) continue;
                
                float dist = enemy.GetDistanceToPlayer(_player.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = enemy;
                }
            }
            
            return nearest;
        }
        
        public List<EnemyEntity> GetEnemiesInRange(int range, WorldGrid grid)
        {
            return _combatEnemies.Where(e => 
                e.IsAlive && 
                e.GetDistanceToPlayer(_player.Position) / grid.TileSize <= range
            ).ToList();
        }
        
        public bool IsPlayerTurn => Phase == CombatPhase.PlayerTurn;
        public bool IsEnemyTurn => Phase == CombatPhase.EnemyTurn;
        
        /// <summary>
        /// Snap a position to the nearest grid tile
        /// </summary>
        private Vector2 SnapToGrid(Vector2 position)
        {
            int tileX = (int)Math.Round(position.X / _tileSize);
            int tileY = (int)Math.Round(position.Y / _tileSize);
            return new Vector2(tileX * _tileSize, tileY * _tileSize);
        }
        
        /// <summary>
        /// Handle the result of an enemy using a special ability
        /// </summary>
        private void HandleAbilityResult(AbilityResult result, WorldGrid grid)
        {
            if (result == null) return;
            
            // Log the ability message
            if (!string.IsNullOrEmpty(result.Message))
            {
                Log(result.Message);
            }
            
            // Handle knockback
            if (result.KnockbackDistance > 0 && result.KnockbackDirection != Vector2.Zero)
            {
                Vector2 newPos = _player.Position + (result.KnockbackDirection * result.KnockbackDistance);
                
                // Check if new position is valid
                Point newTile = new Point((int)(newPos.X / _tileSize), (int)(newPos.Y / _tileSize));
                if (Pathfinder.IsWalkable(grid, newTile))
                {
                    _player.Position = SnapToGrid(newPos);
                    Log($"You're knocked back!");
                    _player.TriggerHitFlash();
                }
            }
            
            // Handle spawning
            if (result.Ability == EnemyAbility.SpawnSwarmling && result.SpawnPosition != Vector2.Zero)
            {
                OnEnemySpawn?.Invoke(result.SpawnType, result.SpawnPosition);
                Log(result.Message);
            }
            
            // Trigger hit flash on player if damage was dealt
            if (result.Damage > 0)
            {
                _player.TriggerHitFlash();
            }
        }
        
        /// <summary>
        /// Handle enemy death, including explosion damage
        /// </summary>
        public float HandleEnemyDeath(EnemyEntity enemy)
        {
            float explosionDamage = enemy.OnDeath();
            
            if (explosionDamage > 0)
            {
                // Check if player is nearby (within 2 tiles)
                float distance = Vector2.Distance(enemy.Position, _player.Position) / _tileSize;
                if (distance <= 2f)
                {
                    _player.Stats.TakeDamage(explosionDamage, DamageType.Fire);
                    _player.TriggerHitFlash();
                    Log($"{enemy.Name} explodes for {explosionDamage:F0} damage!");
                }
                else
                {
                    Log($"{enemy.Name} explodes!");
                }
            }
            
            return explosionDamage;
        }
        
        private void Log(string message)
        {
            OnCombatLog?.Invoke(message);
        }
    }
}
