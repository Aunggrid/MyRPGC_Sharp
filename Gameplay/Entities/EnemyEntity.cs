// Gameplay/Entities/EnemyEntity.cs
// Basic enemy entity with simple AI

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using MyRPG.Gameplay.World;
using MyRPG.Gameplay.Systems;
using MyRPG.Gameplay.Items;
using MyRPG.Data;

namespace MyRPG.Gameplay.Entities
{
    public enum EnemyState
    {
        Idle,           // Standing still
        Patrolling,     // Moving between patrol points
        Chasing,        // Moving toward player
        Attacking,      // In combat, taking turn
        Stunned,        // Can't act
        Dead            // No longer active
    }
    
    public enum EnemyType
    {
        Raider,         // Basic melee human enemy
        MutantBeast,    // Fast, aggressive animal
        Hunter,         // Ranged human enemy
        Abomination     // Tough mutant enemy
    }
    
    public class EnemyEntity
    {
        // Identity
        public string Id { get; private set; }
        public string Name { get; set; }
        public EnemyType Type { get; private set; }
        
        // Position and Movement
        public Vector2 Position;
        public List<Point> CurrentPath = new List<Point>();
        public Point? PatrolTarget = null;
        
        // State
        public EnemyState State { get; set; } = EnemyState.Idle;
        public bool IsAlive => CurrentHealth > 0 && State != EnemyState.Dead;
        
        // Stats
        public float MaxHealth { get; set; } = 50f;
        public float CurrentHealth { get; set; } = 50f;
        public float Speed { get; set; } = 150f;
        public float Damage { get; set; } = 8f;
        public float Accuracy { get; set; } = 0.65f;
        public int SightRange { get; set; } = 8;        // Tiles
        public int AttackRange { get; set; } = 1;       // Tiles (1 = melee)
        
        // Combat
        public int MaxActionPoints { get; set; } = 3;
        public int CurrentActionPoints { get; set; } = 3;
        public int Initiative { get; set; } = 0;
        
        // Status Effects
        public List<StatusEffect> StatusEffects { get; set; } = new List<StatusEffect>();
        
        // AI
        private Random _random = new Random();
        private float _idleTimer = 0f;
        private float _idleDuration = 2f;
        
        // ============================================
        // CONSTRUCTOR & FACTORY
        // ============================================
        
        public EnemyEntity(string id, EnemyType type)
        {
            Id = id;
            Type = type;
            ApplyTypeStats(type);
        }
        
        private void ApplyTypeStats(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Raider:
                    Name = "Raider";
                    MaxHealth = 50f;
                    Speed = 150f;
                    Damage = 8f;
                    Accuracy = 0.65f;
                    SightRange = 8;
                    AttackRange = 1;
                    break;
                    
                case EnemyType.MutantBeast:
                    Name = "Mutant Beast";
                    MaxHealth = 35f;
                    Speed = 220f;
                    Damage = 12f;
                    Accuracy = 0.7f;
                    SightRange = 10;
                    AttackRange = 1;
                    break;
                    
                case EnemyType.Hunter:
                    Name = "Hunter";
                    MaxHealth = 40f;
                    Speed = 130f;
                    Damage = 15f;
                    Accuracy = 0.75f;
                    SightRange = 12;
                    AttackRange = 6;  // Ranged!
                    break;
                    
                case EnemyType.Abomination:
                    Name = "Abomination";
                    MaxHealth = 100f;
                    Speed = 100f;
                    Damage = 20f;
                    Accuracy = 0.6f;
                    SightRange = 6;
                    AttackRange = 1;
                    break;
            }
            
            CurrentHealth = MaxHealth;
        }
        
        /// <summary>
        /// Factory method to create enemy at position
        /// </summary>
        public static EnemyEntity Create(EnemyType type, Vector2 position, int index)
        {
            var enemy = new EnemyEntity($"{type}_{index}", type);
            enemy.Position = position;
            return enemy;
        }
        
        // ============================================
        // UPDATE (Real-time exploration mode)
        // ============================================
        
        public void Update(float deltaTime, WorldGrid grid, Vector2 playerPosition)
        {
            if (!IsAlive) return;
            
            // Update status effects
            GameServices.StatusEffects.UpdateEffectsRealTime(StatusEffects, deltaTime);
            
            // Check if stunned
            if (GameServices.StatusEffects.HasEffect(StatusEffects, StatusEffectType.Stunned))
            {
                State = EnemyState.Stunned;
                return;
            }
            
            // Calculate distance to player
            float distanceToPlayer = Vector2.Distance(Position, playerPosition);
            int tileDistance = (int)(distanceToPlayer / grid.TileSize);
            
            // State machine
            switch (State)
            {
                case EnemyState.Idle:
                    UpdateIdle(deltaTime, grid, playerPosition, tileDistance);
                    break;
                    
                case EnemyState.Patrolling:
                    UpdatePatrolling(deltaTime, grid, playerPosition, tileDistance);
                    break;
                    
                case EnemyState.Chasing:
                    UpdateChasing(deltaTime, grid, playerPosition, tileDistance);
                    break;
                    
                case EnemyState.Stunned:
                    // Wait for stun to wear off
                    if (!GameServices.StatusEffects.HasEffect(StatusEffects, StatusEffectType.Stunned))
                    {
                        State = EnemyState.Idle;
                    }
                    break;
            }
        }
        
        private void UpdateIdle(float deltaTime, WorldGrid grid, Vector2 playerPosition, int tileDistance)
        {
            // Check if player in sight range
            if (tileDistance <= SightRange)
            {
                State = EnemyState.Chasing;
                System.Diagnostics.Debug.WriteLine($">>> {Name} spotted player! <<<");
                return;
            }
            
            // Idle timer - occasionally start patrolling
            _idleTimer += deltaTime;
            if (_idleTimer >= _idleDuration)
            {
                _idleTimer = 0f;
                _idleDuration = 1f + (float)_random.NextDouble() * 3f; // 1-4 seconds
                
                // 30% chance to start patrolling
                if (_random.NextDouble() < 0.3f)
                {
                    StartPatrol(grid);
                }
            }
        }
        
        private void UpdatePatrolling(float deltaTime, WorldGrid grid, Vector2 playerPosition, int tileDistance)
        {
            // Check if player in sight range
            if (tileDistance <= SightRange)
            {
                State = EnemyState.Chasing;
                CurrentPath.Clear();
                System.Diagnostics.Debug.WriteLine($">>> {Name} spotted player while patrolling! <<<");
                return;
            }
            
            // Move along patrol path
            if (CurrentPath.Count > 0)
            {
                MoveAlongPath(deltaTime, grid);
            }
            else
            {
                State = EnemyState.Idle;
            }
        }
        
        private void UpdateChasing(float deltaTime, WorldGrid grid, Vector2 playerPosition, int tileDistance)
        {
            // If player escaped sight range * 1.5, give up
            if (tileDistance > SightRange * 1.5f)
            {
                State = EnemyState.Idle;
                CurrentPath.Clear();
                System.Diagnostics.Debug.WriteLine($">>> {Name} lost sight of player. <<<");
                return;
            }
            
            // If in attack range, stop (combat will handle attacks)
            if (tileDistance <= AttackRange)
            {
                CurrentPath.Clear();
                return;
            }
            
            // Update path to player periodically
            if (CurrentPath.Count == 0)
            {
                Point enemyTile = new Point((int)(Position.X / grid.TileSize), (int)(Position.Y / grid.TileSize));
                Point playerTile = new Point((int)(playerPosition.X / grid.TileSize), (int)(playerPosition.Y / grid.TileSize));
                
                var path = Pathfinder.FindPath(grid, enemyTile, playerTile);
                if (path != null)
                {
                    CurrentPath = path;
                }
            }
            
            // Move along path
            if (CurrentPath.Count > 0)
            {
                MoveAlongPath(deltaTime, grid);
            }
        }
        
        private void StartPatrol(WorldGrid grid)
        {
            Point currentTile = new Point((int)(Position.X / grid.TileSize), (int)(Position.Y / grid.TileSize));
            
            // Pick a random nearby tile
            int offsetX = _random.Next(-5, 6);
            int offsetY = _random.Next(-5, 6);
            Point targetTile = new Point(
                Math.Clamp(currentTile.X + offsetX, 1, grid.Width - 2),
                Math.Clamp(currentTile.Y + offsetY, 1, grid.Height - 2)
            );
            
            // Check if walkable
            if (grid.Tiles[targetTile.X, targetTile.Y].IsWalkable)
            {
                var path = Pathfinder.FindPath(grid, currentTile, targetTile);
                if (path != null && path.Count > 0)
                {
                    CurrentPath = path;
                    PatrolTarget = targetTile;
                    State = EnemyState.Patrolling;
                }
            }
        }
        
        private void MoveAlongPath(float deltaTime, WorldGrid grid)
        {
            if (CurrentPath.Count == 0) return;
            
            Point nextTile = CurrentPath[0];
            Vector2 targetPos = new Vector2(nextTile.X * grid.TileSize, nextTile.Y * grid.TileSize);
            
            Vector2 direction = targetPos - Position;
            if (direction != Vector2.Zero) direction.Normalize();
            
            // Apply speed modifiers from status effects
            float currentSpeed = Speed * GameServices.StatusEffects.GetSpeedModifier(StatusEffects);
            
            Position += direction * currentSpeed * deltaTime;
            
            if (Vector2.Distance(Position, targetPos) < 4f)
            {
                Position = targetPos;
                CurrentPath.RemoveAt(0);
            }
        }
        
        // ============================================
        // COMBAT ACTIONS
        // ============================================
        
        /// <summary>
        /// Reset AP at start of turn
        /// </summary>
        public void StartTurn()
        {
            CurrentActionPoints = MaxActionPoints;
            
            // Check stunned
            if (GameServices.StatusEffects.HasEffect(StatusEffects, StatusEffectType.Stunned))
            {
                CurrentActionPoints = 0;
                System.Diagnostics.Debug.WriteLine($">>> {Name} is STUNNED! Skipping turn. <<<");
            }
        }
        
        /// <summary>
        /// AI decides what to do with its turn
        /// Returns true when turn is complete
        /// </summary>
        public bool TakeTurn(WorldGrid grid, PlayerEntity player, List<EnemyEntity> allEnemies = null)
        {
            if (CurrentActionPoints <= 0) return true;
            if (!IsAlive) return true;
            
            Point enemyTile = new Point((int)(Position.X / grid.TileSize), (int)(Position.Y / grid.TileSize));
            Point playerTile = new Point((int)(player.Position.X / grid.TileSize), (int)(player.Position.Y / grid.TileSize));
            
            // Use Chebyshev distance (allows diagonal)
            int distance = Pathfinder.GetDistance(enemyTile, playerTile);
            
            // If in attack range, attack
            if (distance <= AttackRange && CurrentActionPoints >= 2)
            {
                AttackPlayer(player);
                CurrentActionPoints -= 2;
                return CurrentActionPoints <= 0;
            }
            
            // Otherwise, move toward player
            if (CurrentActionPoints >= 1)
            {
                MoveTowardPlayer(grid, playerTile, enemyTile, allEnemies);
                CurrentActionPoints -= 1;
                return CurrentActionPoints <= 0;
            }
            
            return true;
        }
        
        private void MoveTowardPlayer(WorldGrid grid, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies)
        {
            // Find path if needed
            if (CurrentPath.Count == 0)
            {
                var path = Pathfinder.FindPath(grid, enemyTile, playerTile);
                if (path != null && path.Count > 0)
                {
                    // IMPORTANT: Remove the last tile (player's position) - we want to stop ADJACENT
                    path.RemoveAt(path.Count - 1);
                    CurrentPath = path;
                }
            }
            
            // Move one tile (but never onto player's tile or another enemy's tile!)
            if (CurrentPath.Count > 0)
            {
                Point nextTile = CurrentPath[0];
                
                // Check if tile is blocked by player
                if (nextTile == playerTile)
                {
                    CurrentPath.Clear();
                    return;
                }
                
                // Check if tile is blocked by another enemy
                if (allEnemies != null && IsTileOccupiedByEnemy(nextTile, grid.TileSize, allEnemies))
                {
                    // Try to find alternate path or skip movement
                    CurrentPath.Clear();
                    System.Diagnostics.Debug.WriteLine($">>> {Name} blocked by another enemy! <<<");
                    return;
                }
                
                // Move to tile
                Position = new Vector2(nextTile.X * grid.TileSize, nextTile.Y * grid.TileSize);
                CurrentPath.RemoveAt(0);
                System.Diagnostics.Debug.WriteLine($">>> {Name} moves to ({nextTile.X}, {nextTile.Y}) <<<");
            }
        }
        
        /// <summary>
        /// Check if a tile is occupied by another enemy
        /// </summary>
        private bool IsTileOccupiedByEnemy(Point tile, int tileSize, List<EnemyEntity> allEnemies)
        {
            foreach (var other in allEnemies)
            {
                if (other == this) continue; // Skip self
                if (!other.IsAlive) continue; // Skip dead enemies
                
                Point otherTile = new Point(
                    (int)(other.Position.X / tileSize),
                    (int)(other.Position.Y / tileSize)
                );
                
                if (otherTile == tile)
                {
                    return true; // Tile is occupied
                }
            }
            return false;
        }
        
        private void AttackPlayer(PlayerEntity player)
        {
            // Roll to hit
            float roll = (float)_random.NextDouble();
            
            if (roll <= Accuracy)
            {
                // Hit!
                player.Stats.TakeDamage(Damage, DamageType.Physical);
                System.Diagnostics.Debug.WriteLine($">>> {Name} hits player for {Damage} damage! <<<");
            }
            else
            {
                // Miss!
                System.Diagnostics.Debug.WriteLine($">>> {Name} misses! <<<");
            }
        }
        
        // ============================================
        // DAMAGE & DEATH
        // ============================================
        
        public void TakeDamage(float amount, DamageType type = DamageType.Physical)
        {
            CurrentHealth -= amount;
            System.Diagnostics.Debug.WriteLine($">>> {Name} takes {amount} damage! HP: {CurrentHealth}/{MaxHealth} <<<");
            
            if (CurrentHealth <= 0)
            {
                Die();
            }
        }
        
        private void Die()
        {
            CurrentHealth = 0;
            State = EnemyState.Dead;
            CurrentPath.Clear();
            System.Diagnostics.Debug.WriteLine($">>> {Name} has been defeated! <<<");
        }
        
        // ============================================
        // HELPERS
        // ============================================
        
        public Point GetTilePosition(int tileSize)
        {
            return new Point((int)(Position.X / tileSize), (int)(Position.Y / tileSize));
        }
        
        public float GetDistanceToPlayer(Vector2 playerPosition)
        {
            return Vector2.Distance(Position, playerPosition);
        }
        
        public bool IsInAttackRange(Vector2 playerPosition, int tileSize)
        {
            int distance = (int)(Vector2.Distance(Position, playerPosition) / tileSize);
            return distance <= AttackRange;
        }
        
        /// <summary>
        /// Generate loot drops when enemy is killed
        /// </summary>
        public List<Item> GenerateLoot()
        {
            var loot = new List<Item>();
            
            switch (Type)
            {
                case EnemyType.Raider:
                    // Raiders drop weapons, ammo, and junk
                    if (_random.NextDouble() < 0.3) // 30% chance for weapon
                    {
                        string[] weapons = { "knife_rusty", "pipe_club", "machete" };
                        loot.Add(new Item(weapons[_random.Next(weapons.Length)]));
                    }
                    if (_random.NextDouble() < 0.5) // 50% chance for some scrap
                    {
                        loot.Add(new Item("scrap_metal", ItemQuality.Normal, _random.Next(1, 5)));
                    }
                    if (_random.NextDouble() < 0.3)
                    {
                        loot.Add(new Item("food_jerky", ItemQuality.Normal, _random.Next(1, 3)));
                    }
                    break;
                    
                case EnemyType.MutantBeast:
                    // Beasts drop meat and leather
                    loot.Add(new Item("mutant_meat", ItemQuality.Normal, _random.Next(1, 4)));
                    if (_random.NextDouble() < 0.4)
                    {
                        loot.Add(new Item("leather", ItemQuality.Normal, _random.Next(1, 3)));
                    }
                    break;
                    
                case EnemyType.Hunter:
                    // Hunters drop better gear and ammo
                    if (_random.NextDouble() < 0.4)
                    {
                        string[] weapons = { "knife_combat", "pistol_9mm", "bow_simple" };
                        loot.Add(new Item(weapons[_random.Next(weapons.Length)]));
                    }
                    if (_random.NextDouble() < 0.6)
                    {
                        string[] ammo = { "ammo_9mm", "arrow_basic" };
                        loot.Add(new Item(ammo[_random.Next(ammo.Length)], ItemQuality.Normal, _random.Next(5, 15)));
                    }
                    if (_random.NextDouble() < 0.3)
                    {
                        loot.Add(new Item("medkit"));
                    }
                    break;
                    
                case EnemyType.Abomination:
                    // Abominations drop rare stuff
                    loot.Add(new Item("mutant_meat", ItemQuality.Normal, _random.Next(3, 8)));
                    if (_random.NextDouble() < 0.3)
                    {
                        loot.Add(new Item("void_essence", ItemQuality.Normal, _random.Next(1, 3)));
                    }
                    if (_random.NextDouble() < 0.2)
                    {
                        loot.Add(new Item("stimpack"));
                    }
                    break;
            }
            
            // Everyone has a chance for bonus random loot
            if (_random.NextDouble() < 0.1) // 10% chance
            {
                var bonusItem = ItemDatabase.CreateRandom(ItemRarity.Uncommon, _random);
                if (bonusItem != null) loot.Add(bonusItem);
            }
            
            return loot;
        }
    }
}
