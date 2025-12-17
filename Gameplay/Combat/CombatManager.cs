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
        
        // Grid info
        private int _tileSize = 64;
        
        // Turn Order
        private List<object> _turnOrder = new List<object>(); // Mix of player and enemies
        private int _currentTurnIndex = 0;
        private object _currentActor => _turnOrder.Count > 0 ? _turnOrder[_currentTurnIndex] : null;
        
        // Player Combat
        public int PlayerActionPoints { get; private set; } = 3;
        public int PlayerMaxActionPoints => _player?.Stats?.ActionPoints ?? 3;
        
        // Combat trigger range (tiles)
        public int CombatTriggerRange { get; set; } = 2;
        
        // Events for UI
        public event Action OnCombatStart;
        public event Action OnCombatEnd;
        public event Action<object> OnTurnStart;
        public event Action<string> OnCombatLog;
        public event Action<EnemyEntity, Vector2> OnEnemyKilled;  // Enemy killed, drop loot at position
        
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
            switch (Phase)
            {
                case CombatPhase.Exploration:
                    CheckCombatTrigger(grid);
                    break;
                    
                case CombatPhase.CombatStart:
                    InitializeCombat();
                    break;
                    
                case CombatPhase.PlayerTurn:
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
        /// Check if any enemies not in combat should join the fight
        /// </summary>
        private void CheckForReinforcements(WorldGrid grid)
        {
            bool addedNew = false;
            
            foreach (var enemy in _allEnemies)
            {
                if (!enemy.IsAlive) continue;
                if (_combatEnemies.Contains(enemy)) continue; // Already in combat
                
                float distance = enemy.GetDistanceToPlayer(_player.Position);
                int tileDistance = (int)(distance / grid.TileSize);
                
                // If enemy is close enough OR has spotted the player, join combat
                if (tileDistance <= CombatTriggerRange || enemy.State == EnemyState.Chasing)
                {
                    // Add to combat
                    _combatEnemies.Add(enemy);
                    enemy.State = EnemyState.Chasing;
                    
                    // Add to turn order (will act next round)
                    if (!_turnOrder.Contains(enemy))
                    {
                        _turnOrder.Add(enemy);
                        enemy.StartTurn(); // Give them AP
                    }
                    
                    addedNew = true;
                    Log($"{enemy.Name} joins the fight!");
                    System.Diagnostics.Debug.WriteLine($">>> {enemy.Name} JOINS COMBAT! <<<");
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
            
            // IMPORTANT: Clear player's exploration path so they don't continue moving after combat
            _player.CurrentPath.Clear();
            
            // SNAP PLAYER TO NEAREST GRID TILE
            _player.Position = SnapToGrid(_player.Position);
            System.Diagnostics.Debug.WriteLine($">>> Player snapped to grid: ({_player.Position.X}, {_player.Position.Y}) <<<");
            
            // Gather all nearby enemies into combat
            _combatEnemies.Clear();
            
            foreach (var enemy in _allEnemies)
            {
                if (!enemy.IsAlive) continue;
                
                float distance = enemy.GetDistanceToPlayer(_player.Position);
                int tileDistance = (int)(distance / _tileSize);
                
                // Include enemies within extended range
                if (tileDistance <= 10 || enemy == triggeringEnemy)
                {
                    _combatEnemies.Add(enemy);
                    enemy.State = EnemyState.Chasing; // All combat enemies are now hostile
                    enemy.CurrentPath.Clear(); // Clear their path too
                    
                    // SNAP ENEMY TO NEAREST GRID TILE
                    enemy.Position = SnapToGrid(enemy.Position);
                }
            }
            
            Log($"Combat started! {_combatEnemies.Count} enemies engaged.");
            System.Diagnostics.Debug.WriteLine($">>> COMBAT STARTED with {_combatEnemies.Count} enemies! <<<");
            
            OnCombatStart?.Invoke();
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
                PlayerActionPoints = PlayerMaxActionPoints;
                
                // Check if player is stunned
                if (_player.HasStatus(StatusEffectType.Stunned))
                {
                    PlayerActionPoints = 0;
                    Log("Player is STUNNED! Turn skipped.");
                    System.Diagnostics.Debug.WriteLine(">>> Player is STUNNED! Skipping turn. <<<");
                    EndCurrentTurn();
                    return;
                }
                
                Log("Your turn! " + PlayerActionPoints + " AP available.");
                System.Diagnostics.Debug.WriteLine($">>> PLAYER TURN - {PlayerActionPoints} AP <<<");
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
                // Pass all enemies so this enemy can avoid occupied tiles
                bool turnDone = enemy.TakeTurn(grid, _player, _allEnemies);
                
                if (turnDone)
                {
                    EndCurrentTurn();
                }
            }
        }
        
        public void EndCurrentTurn()
        {
            _currentTurnIndex++;
            
            // Wrap around
            if (_currentTurnIndex >= _turnOrder.Count)
            {
                _currentTurnIndex = 0;
                System.Diagnostics.Debug.WriteLine(">>> NEW ROUND <<<");
            }
            
            StartCurrentTurn();
        }
        
        private void CleanupDeadActors()
        {
            _turnOrder.RemoveAll(actor =>
            {
                if (actor is EnemyEntity enemy && !enemy.IsAlive)
                {
                    _combatEnemies.Remove(enemy);
                    return true;
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
            
            // All enemies dead
            if (_combatEnemies.All(e => !e.IsAlive))
            {
                Phase = CombatPhase.CombatEnd;
                Log("Victory! All enemies defeated.");
                return true;
            }
            
            return false;
        }
        
        // ============================================
        // PLAYER ACTIONS
        // ============================================
        
        /// <summary>
        /// Player moves one tile (costs 1 AP) - supports diagonal movement
        /// </summary>
        public bool PlayerMove(Point targetTile, WorldGrid grid)
        {
            if (Phase != CombatPhase.PlayerTurn) return false;
            if (PlayerActionPoints < 1) return false;
            
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
            
            // Move
            _player.Position = new Vector2(targetTile.X * grid.TileSize, targetTile.Y * grid.TileSize);
            PlayerActionPoints--;
            
            string dirStr = GetDirectionName(dx, dy);
            Log($"Moved {dirStr}. {PlayerActionPoints} AP left.");
            System.Diagnostics.Debug.WriteLine($">>> Player moves {dirStr} to ({targetTile.X}, {targetTile.Y}). {PlayerActionPoints} AP left. <<<");
            
            if (PlayerActionPoints <= 0) EndCurrentTurn();
            
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
            if (PlayerActionPoints < 2) return false;
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
            
            // Roll to hit
            var random = new Random();
            float roll = (float)random.NextDouble();
            
            PlayerActionPoints -= 2;
            
            // Consume ammo if needed
            _player.Stats.ConsumeAmmoForAttack();
            
            if (roll <= _player.Stats.Accuracy)
            {
                // Hit!
                float damage = _player.Stats.Damage;
                target.TakeDamage(damage, DamageType.Physical);
                Log($"Hit {target.Name} for {damage:F0} damage!");
                System.Diagnostics.Debug.WriteLine($">>> Player hits {target.Name} for {damage:F0} damage! <<<");
                
                // Grant XP and trigger loot if killed
                if (!target.IsAlive)
                {
                    float xp = target.MaxHealth; // XP = enemy max health
                    _player.Stats.AddXP(xp);
                    
                    // Fire event for loot drop (handled by Game1)
                    OnEnemyKilled?.Invoke(target, target.Position);
                    
                    Log($"{target.Name} defeated! +{xp} XP");
                }
            }
            else
            {
                Log($"Missed {target.Name}!");
                System.Diagnostics.Debug.WriteLine($">>> Player misses {target.Name}! <<<");
            }
            
            if (PlayerActionPoints <= 0) EndCurrentTurn();
            
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
        
        private void Log(string message)
        {
            OnCombatLog?.Invoke(message);
        }
    }
}
