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
        
        // Behavior
        public CreatureBehavior Behavior { get; set; } = CreatureBehavior.Aggressive;
        public bool IsProvoked { get; set; } = false;  // For passive creatures
        public float ProvokedTimer { get; set; } = 0f; // How long they stay angry
        public bool InCombatZone { get; set; } = false; // Currently in turn-based combat
        
        // Attack Animation
        public bool IsAnimating { get; private set; } = false;
        public Vector2 AnimationStartPos { get; private set; }
        public Vector2 AnimationTargetPos { get; private set; }
        private float _animationTimer = 0f;
        private float _animationDuration = 0.12f;  // Slightly faster for enemies
        private bool _animationReturning = false;
        
        // Hit Flash (visual feedback when taking damage)
        public float HitFlashTimer { get; private set; } = 0f;
        public bool IsFlashing => HitFlashTimer > 0f;
        
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
                    Behavior = CreatureBehavior.Aggressive;
                    break;
                    
                // === PASSIVE CREATURES ===
                
                case EnemyType.Scavenger:
                    Name = "Scavenger";
                    MaxHealth = 15f;
                    Speed = 200f;
                    Damage = 3f;
                    Accuracy = 0.5f;
                    SightRange = 6;
                    AttackRange = 1;
                    Behavior = CreatureBehavior.Cowardly;
                    break;
                    
                case EnemyType.GiantInsect:
                    Name = "Giant Insect";
                    MaxHealth = 25f;
                    Speed = 120f;
                    Damage = 6f;
                    Accuracy = 0.6f;
                    SightRange = 4;
                    AttackRange = 1;
                    Behavior = CreatureBehavior.Passive;
                    break;
                    
                case EnemyType.WildBoar:
                    Name = "Wild Boar";
                    MaxHealth = 45f;
                    Speed = 180f;
                    Damage = 10f;
                    Accuracy = 0.7f;
                    SightRange = 5;
                    AttackRange = 1;
                    Behavior = CreatureBehavior.Passive;
                    break;
                    
                case EnemyType.MutantDeer:
                    Name = "Mutant Deer";
                    MaxHealth = 30f;
                    Speed = 250f;  // Very fast runner
                    Damage = 5f;
                    Accuracy = 0.5f;
                    SightRange = 10;
                    AttackRange = 1;
                    Behavior = CreatureBehavior.Cowardly;
                    break;
                    
                case EnemyType.CaveSlug:
                    Name = "Cave Slug";
                    MaxHealth = 40f;
                    Speed = 60f;   // Very slow
                    Damage = 8f;
                    Accuracy = 0.8f;
                    SightRange = 3;
                    AttackRange = 1;
                    Behavior = CreatureBehavior.Passive;
                    break;
            }
            
            // Set behavior for hostile types
            if (type == EnemyType.Raider || type == EnemyType.MutantBeast || 
                type == EnemyType.Hunter || type == EnemyType.Abomination)
            {
                Behavior = CreatureBehavior.Aggressive;
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
            
            // Update hit flash timer
            if (HitFlashTimer > 0f)
            {
                HitFlashTimer -= deltaTime;
            }
            
            // Update attack animation (always runs, even during combat)
            if (IsAnimating)
            {
                UpdateAnimation(deltaTime);
                return; // Don't move while animating
            }
            
            // Update status effects
            GameServices.StatusEffects.UpdateEffectsRealTime(StatusEffects, deltaTime);
            
            // Update provoked timer
            if (IsProvoked)
            {
                ProvokedTimer -= deltaTime;
                if (ProvokedTimer <= 0)
                {
                    IsProvoked = false;
                    State = EnemyState.Idle;
                    System.Diagnostics.Debug.WriteLine($">>> {Name} calmed down <<<");
                }
            }
            
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
                    
                case EnemyState.Fleeing:
                    UpdateFleeing(deltaTime, grid, playerPosition, tileDistance);
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
            // Check if player in sight range - behavior determines response
            if (tileDistance <= SightRange)
            {
                if (Behavior == CreatureBehavior.Aggressive)
                {
                    State = EnemyState.Chasing;
                    System.Diagnostics.Debug.WriteLine($">>> {Name} spotted player! <<<");
                    return;
                }
                else if (Behavior == CreatureBehavior.Territorial && tileDistance <= 3)
                {
                    // Only attack if player gets very close
                    State = EnemyState.Chasing;
                    System.Diagnostics.Debug.WriteLine($">>> {Name} is defending territory! <<<");
                    return;
                }
                else if (IsProvoked)
                {
                    // Provoked creatures attack or flee based on behavior
                    if (Behavior == CreatureBehavior.Cowardly)
                    {
                        State = EnemyState.Fleeing;
                    }
                    else
                    {
                        State = EnemyState.Chasing;
                    }
                    return;
                }
                // Passive/Cowardly creatures ignore player unless provoked
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
            // Check if player in sight range - behavior determines response
            if (tileDistance <= SightRange)
            {
                if (Behavior == CreatureBehavior.Aggressive)
                {
                    State = EnemyState.Chasing;
                    CurrentPath.Clear();
                    System.Diagnostics.Debug.WriteLine($">>> {Name} spotted player while patrolling! <<<");
                    return;
                }
                else if (IsProvoked)
                {
                    CurrentPath.Clear();
                    State = Behavior == CreatureBehavior.Cowardly ? EnemyState.Fleeing : EnemyState.Chasing;
                    return;
                }
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
        
        private void UpdateFleeing(float deltaTime, WorldGrid grid, Vector2 playerPosition, int tileDistance)
        {
            // If far enough away, calm down
            if (tileDistance > SightRange * 2)
            {
                State = EnemyState.Idle;
                CurrentPath.Clear();
                IsProvoked = false;
                System.Diagnostics.Debug.WriteLine($">>> {Name} escaped and calmed down <<<");
                return;
            }
            
            // Move away from player
            if (CurrentPath.Count == 0)
            {
                Point enemyTile = new Point((int)(Position.X / grid.TileSize), (int)(Position.Y / grid.TileSize));
                Point playerTile = new Point((int)(playerPosition.X / grid.TileSize), (int)(playerPosition.Y / grid.TileSize));
                
                // Calculate direction away from player
                int dx = enemyTile.X - playerTile.X;
                int dy = enemyTile.Y - playerTile.Y;
                
                // Normalize and scale to flee distance
                float length = (float)Math.Sqrt(dx * dx + dy * dy);
                if (length > 0)
                {
                    dx = (int)(dx / length * 8);  // Flee 8 tiles away
                    dy = (int)(dy / length * 8);
                }
                else
                {
                    // Random direction if on same tile
                    dx = _random.Next(-8, 9);
                    dy = _random.Next(-8, 9);
                }
                
                Point fleeTile = new Point(
                    Math.Clamp(enemyTile.X + dx, 1, grid.Width - 2),
                    Math.Clamp(enemyTile.Y + dy, 1, grid.Height - 2)
                );
                
                var path = Pathfinder.FindPath(grid, enemyTile, fleeTile);
                if (path != null && path.Count > 0)
                {
                    CurrentPath = path;
                }
            }
            
            // Move along flee path (faster when fleeing)
            if (CurrentPath.Count > 0)
            {
                MoveAlongPath(deltaTime * 1.3f, grid);  // 30% faster when fleeing
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
        public bool TakeTurn(WorldGrid grid, PlayerEntity player, List<EnemyEntity> allEnemies = null, Vector2? combatCenter = null)
        {
            if (CurrentActionPoints <= 0) return true;
            if (!IsAlive) return true;
            
            Point enemyTile = new Point((int)(Position.X / grid.TileSize), (int)(Position.Y / grid.TileSize));
            Point playerTile = new Point((int)(player.Position.X / grid.TileSize), (int)(player.Position.Y / grid.TileSize));
            
            // Use Chebyshev distance (allows diagonal)
            int distanceToPlayer = Pathfinder.GetDistance(enemyTile, playerTile);
            
            // Calculate distance to combat center
            int distanceToCombatCenter = 0;
            Point combatCenterTile = playerTile; // Default to player if no center
            if (combatCenter.HasValue)
            {
                combatCenterTile = new Point(
                    (int)(combatCenter.Value.X / grid.TileSize),
                    (int)(combatCenter.Value.Y / grid.TileSize)
                );
                distanceToCombatCenter = Pathfinder.GetDistance(enemyTile, combatCenterTile);
            }
            
            // === PASSIVE BEHAVIOR (not provoked) ===
            // These creatures got caught in combat zone but aren't hostile
            if ((Behavior == CreatureBehavior.Passive || Behavior == CreatureBehavior.Cowardly) && !IsProvoked)
            {
                return TakePassiveTurn(grid, combatCenterTile, enemyTile, distanceToCombatCenter, allEnemies);
            }
            
            // === COWARDLY BEHAVIOR (provoked) ===
            // Provoked cowardly creatures try to flee
            if (Behavior == CreatureBehavior.Cowardly && IsProvoked)
            {
                if (CurrentActionPoints >= 1)
                {
                    FleeFromPoint(grid, playerTile, enemyTile, allEnemies);
                    CurrentActionPoints -= 1;
                }
                return CurrentActionPoints <= 0;
            }
            
            // === AGGRESSIVE/TERRITORIAL BEHAVIOR ===
            // Normal combat - attack if in range
            if (distanceToPlayer <= AttackRange && CurrentActionPoints >= 2)
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
        
        /// <summary>
        /// Passive creature turn - flee from combat center if close, wander if far
        /// </summary>
        private bool TakePassiveTurn(WorldGrid grid, Point combatCenterTile, Point enemyTile, int distanceToCombatCenter, List<EnemyEntity> allEnemies)
        {
            // If close to combat (within 8 tiles), flee away from combat center
            if (distanceToCombatCenter <= 8)
            {
                if (CurrentActionPoints >= 1)
                {
                    FleeFromPoint(grid, combatCenterTile, enemyTile, allEnemies);
                    CurrentActionPoints -= 1;
                    System.Diagnostics.Debug.WriteLine($">>> {Name} fleeing from combat zone (dist: {distanceToCombatCenter}) <<<");
                }
                return CurrentActionPoints <= 0;
            }
            
            // If far enough from combat, wander randomly
            if (CurrentActionPoints >= 1)
            {
                WanderRandomly(grid, enemyTile, allEnemies);
                CurrentActionPoints -= 1;
                System.Diagnostics.Debug.WriteLine($">>> {Name} wandering at edge of combat zone <<<");
            }
            
            return CurrentActionPoints <= 0;
        }
        
        /// <summary>
        /// Wander to a random adjacent tile
        /// </summary>
        private void WanderRandomly(WorldGrid grid, Point currentTile, List<EnemyEntity> allEnemies)
        {
            // Try random adjacent tiles
            int[] offsets = { -1, 0, 1 };
            List<Point> validTiles = new List<Point>();
            
            foreach (int ox in offsets)
            {
                foreach (int oy in offsets)
                {
                    if (ox == 0 && oy == 0) continue;
                    
                    Point tryTile = new Point(currentTile.X + ox, currentTile.Y + oy);
                    var tile = grid.GetTile(tryTile.X, tryTile.Y);
                    
                    if (tile != null && tile.IsWalkable)
                    {
                        if (allEnemies == null || !IsTileOccupiedByEnemy(tryTile, grid.TileSize, allEnemies))
                        {
                            validTiles.Add(tryTile);
                        }
                    }
                }
            }
            
            // Pick a random valid tile
            if (validTiles.Count > 0)
            {
                Point wanderTile = validTiles[_random.Next(validTiles.Count)];
                Position = new Vector2(wanderTile.X * grid.TileSize, wanderTile.Y * grid.TileSize);
            }
        }
        
        /// <summary>
        /// Flee from a specific point (combat center or player)
        /// </summary>
        private void FleeFromPoint(WorldGrid grid, Point fleeFromTile, Point enemyTile, List<EnemyEntity> allEnemies)
        {
            // Calculate direction away from the flee point
            int dx = enemyTile.X - fleeFromTile.X;
            int dy = enemyTile.Y - fleeFromTile.Y;
            
            // Normalize to single tile movement
            if (dx != 0) dx = dx > 0 ? 1 : -1;
            if (dy != 0) dy = dy > 0 ? 1 : -1;
            
            // If on same tile, pick random direction
            if (dx == 0 && dy == 0)
            {
                dx = _random.Next(-1, 2);
                dy = _random.Next(-1, 2);
                if (dx == 0 && dy == 0) dx = 1;
            }
            
            Point fleeTarget = new Point(
                Math.Clamp(enemyTile.X + dx, 1, grid.Width - 2),
                Math.Clamp(enemyTile.Y + dy, 1, grid.Height - 2)
            );
            
            // Check if flee target is walkable and not occupied
            var fleeTileTerrain = grid.GetTile(fleeTarget.X, fleeTarget.Y);
            if (fleeTileTerrain != null && fleeTileTerrain.IsWalkable)
            {
                if (allEnemies == null || !IsTileOccupiedByEnemy(fleeTarget, grid.TileSize, allEnemies))
                {
                    Position = new Vector2(fleeTarget.X * grid.TileSize, fleeTarget.Y * grid.TileSize);
                    System.Diagnostics.Debug.WriteLine($">>> {Name} flees to ({fleeTarget.X}, {fleeTarget.Y}) <<<");
                    return;
                }
            }
            
            // If direct flee fails, try adjacent tiles
            int[] offsets = { -1, 0, 1 };
            foreach (int ox in offsets)
            {
                foreach (int oy in offsets)
                {
                    if (ox == 0 && oy == 0) continue;
                    Point tryTile = new Point(enemyTile.X + ox, enemyTile.Y + oy);
                    
                    // Prefer tiles away from flee point
                    int newDist = Pathfinder.GetDistance(tryTile, fleeFromTile);
                    int oldDist = Pathfinder.GetDistance(enemyTile, fleeFromTile);
                    
                    var tile = grid.GetTile(tryTile.X, tryTile.Y);
                    if (newDist > oldDist && tile != null && tile.IsWalkable)
                    {
                        if (allEnemies == null || !IsTileOccupiedByEnemy(tryTile, grid.TileSize, allEnemies))
                        {
                            Position = new Vector2(tryTile.X * grid.TileSize, tryTile.Y * grid.TileSize);
                            System.Diagnostics.Debug.WriteLine($">>> {Name} flees to ({tryTile.X}, {tryTile.Y}) <<<");
                            return;
                        }
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($">>> {Name} can't flee - cornered! <<<");
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
            // Start attack animation
            StartAttackAnimation(player.Position, 64);  // Assume 64 tile size
            
            // Roll to hit
            float roll = (float)_random.NextDouble();
            
            if (roll <= Accuracy)
            {
                // Hit!
                player.Stats.TakeDamage(Damage, DamageType.Physical);
                player.TriggerHitFlash();  // Visual feedback
                System.Diagnostics.Debug.WriteLine($">>> {Name} hits player for {Damage} damage! <<<");
            }
            else
            {
                // Miss!
                System.Diagnostics.Debug.WriteLine($">>> {Name} misses! <<<");
            }
        }
        
        /// <summary>
        /// Start attack lunge animation toward target
        /// </summary>
        public void StartAttackAnimation(Vector2 targetPosition, int tileSize)
        {
            if (IsAnimating) return;
            
            AnimationStartPos = Position;
            
            // Calculate lunge position (move 60% toward target)
            Vector2 direction = targetPosition - Position;
            float lungeDistance = tileSize * 0.6f;
            if (direction.Length() > lungeDistance)
            {
                direction.Normalize();
                direction *= lungeDistance;
            }
            AnimationTargetPos = Position + direction;
            
            _animationTimer = 0f;
            _animationReturning = false;
            IsAnimating = true;
        }
        
        /// <summary>
        /// Update attack animation
        /// </summary>
        private void UpdateAnimation(float deltaTime)
        {
            _animationTimer += deltaTime;
            float progress = _animationTimer / _animationDuration;
            
            if (!_animationReturning)
            {
                // Lunge toward target
                if (progress >= 1f)
                {
                    Position = AnimationTargetPos;
                    _animationTimer = 0f;
                    _animationReturning = true;
                }
                else
                {
                    // Ease out - fast start, slow end
                    float easedProgress = 1f - (1f - progress) * (1f - progress);
                    Position = Vector2.Lerp(AnimationStartPos, AnimationTargetPos, easedProgress);
                }
            }
            else
            {
                // Return to original position
                if (progress >= 1f)
                {
                    Position = AnimationStartPos;
                    IsAnimating = false;
                    _animationReturning = false;
                }
                else
                {
                    // Ease in - slow start, fast end
                    float easedProgress = progress * progress;
                    Position = Vector2.Lerp(AnimationTargetPos, AnimationStartPos, easedProgress);
                }
            }
        }
        
        // ============================================
        // DAMAGE & DEATH
        // ============================================
        
        public void TakeDamage(float amount, DamageType type = DamageType.Physical)
        {
            CurrentHealth -= amount;
            HitFlashTimer = 0.15f;  // Trigger hit flash
            System.Diagnostics.Debug.WriteLine($">>> {Name} takes {amount} damage! HP: {CurrentHealth}/{MaxHealth} <<<");
            
            // Provoke passive/cowardly creatures when attacked
            if (Behavior == CreatureBehavior.Passive || Behavior == CreatureBehavior.Cowardly)
            {
                Provoke();
            }
            
            if (CurrentHealth <= 0)
            {
                Die();
            }
        }
        
        /// <summary>
        /// Provoke a passive creature - makes it fight or flee
        /// </summary>
        public void Provoke()
        {
            if (!IsProvoked)
            {
                IsProvoked = true;
                ProvokedTimer = 30f;  // Stay provoked for 30 seconds
                
                if (Behavior == CreatureBehavior.Cowardly)
                {
                    State = EnemyState.Fleeing;
                    System.Diagnostics.Debug.WriteLine($">>> {Name} is fleeing! <<<");
                }
                else
                {
                    State = EnemyState.Chasing;
                    System.Diagnostics.Debug.WriteLine($">>> {Name} is provoked and attacking! <<<");
                }
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
                    // Raiders drop weapons, ammo, scrap, and tinker materials
                    if (_random.NextDouble() < 0.3) // 30% chance for weapon
                    {
                        string[] weapons = { "knife_rusty", "pipe_club", "machete" };
                        loot.Add(new Item(weapons[_random.Next(weapons.Length)]));
                    }
                    if (_random.NextDouble() < 0.5) // 50% chance for some scrap
                    {
                        loot.Add(new Item("scrap_metal", ItemQuality.Normal, _random.Next(1, 5)));
                    }
                    if (_random.NextDouble() < 0.25) // Components for tinker science
                    {
                        loot.Add(new Item("components", ItemQuality.Normal, _random.Next(1, 3)));
                    }
                    if (_random.NextDouble() < 0.3)
                    {
                        loot.Add(new Item("food_jerky", ItemQuality.Normal, _random.Next(1, 3)));
                    }
                    break;
                    
                case EnemyType.MutantBeast:
                    // Beasts drop meat, leather, and dark science materials
                    loot.Add(new Item("mutant_meat", ItemQuality.Normal, _random.Next(1, 4)));
                    if (_random.NextDouble() < 0.4)
                    {
                        loot.Add(new Item("leather", ItemQuality.Normal, _random.Next(1, 3)));
                    }
                    if (_random.NextDouble() < 0.3)  // Bone for dark science
                    {
                        loot.Add(new Item("bone", ItemQuality.Normal, _random.Next(1, 4)));
                    }
                    if (_random.NextDouble() < 0.2)  // Sinew for dark science
                    {
                        loot.Add(new Item("sinew", ItemQuality.Normal, _random.Next(1, 2)));
                    }
                    break;
                    
                case EnemyType.Hunter:
                    // Hunters drop better gear, ammo, and tinker materials
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
                    if (_random.NextDouble() < 0.35)  // Scrap electronics for tinker
                    {
                        loot.Add(new Item("scrap_electronics", ItemQuality.Normal, _random.Next(1, 3)));
                    }
                    if (_random.NextDouble() < 0.2)  // Energy cell (rare)
                    {
                        loot.Add(new Item("energy_cell", ItemQuality.Normal, 1));
                    }
                    if (_random.NextDouble() < 0.3)
                    {
                        loot.Add(new Item("medkit"));
                    }
                    break;
                    
                case EnemyType.Abomination:
                    // Abominations drop rare dark science materials
                    loot.Add(new Item("mutant_meat", ItemQuality.Normal, _random.Next(3, 8)));
                    if (_random.NextDouble() < 0.4)  // Anomaly shard (rare)
                    {
                        loot.Add(new Item("anomaly_shard", ItemQuality.Normal, _random.Next(1, 3)));
                    }
                    if (_random.NextDouble() < 0.25)  // Mutagen (rare)
                    {
                        loot.Add(new Item("mutagen", ItemQuality.Normal, 1));
                    }
                    if (_random.NextDouble() < 0.15)  // Brain tissue (very rare)
                    {
                        loot.Add(new Item("brain_tissue", ItemQuality.Normal, 1));
                    }
                    if (_random.NextDouble() < 0.2)
                    {
                        loot.Add(new Item("stimpack"));
                    }
                    break;
                    
                // === PASSIVE CREATURES ===
                
                case EnemyType.Scavenger:
                    // Small creature - minimal loot
                    if (_random.NextDouble() < 0.6)
                    {
                        loot.Add(new Item("bone", ItemQuality.Normal, _random.Next(1, 2)));
                    }
                    if (_random.NextDouble() < 0.3)
                    {
                        loot.Add(new Item("mutant_meat", ItemQuality.Normal, 1));
                    }
                    break;
                    
                case EnemyType.GiantInsect:
                    // Drops chitin (new material) and sometimes anomaly shards
                    loot.Add(new Item("bone", ItemQuality.Normal, _random.Next(1, 3)));  // Exoskeleton as bone
                    if (_random.NextDouble() < 0.4)
                    {
                        loot.Add(new Item("sinew", ItemQuality.Normal, _random.Next(1, 2)));
                    }
                    if (_random.NextDouble() < 0.15)  // Rare anomaly shard
                    {
                        loot.Add(new Item("anomaly_shard", ItemQuality.Normal, 1));
                    }
                    break;
                    
                case EnemyType.WildBoar:
                    // Good meat source, some leather
                    loot.Add(new Item("mutant_meat", ItemQuality.Normal, _random.Next(3, 6)));
                    if (_random.NextDouble() < 0.5)
                    {
                        loot.Add(new Item("leather", ItemQuality.Normal, _random.Next(2, 4)));
                    }
                    if (_random.NextDouble() < 0.3)
                    {
                        loot.Add(new Item("bone", ItemQuality.Normal, _random.Next(1, 3)));
                    }
                    break;
                    
                case EnemyType.MutantDeer:
                    // Fast runner - best leather source
                    loot.Add(new Item("leather", ItemQuality.Normal, _random.Next(3, 6)));
                    loot.Add(new Item("mutant_meat", ItemQuality.Normal, _random.Next(2, 4)));
                    if (_random.NextDouble() < 0.4)
                    {
                        loot.Add(new Item("sinew", ItemQuality.Normal, _random.Next(2, 4)));
                    }
                    if (_random.NextDouble() < 0.2)
                    {
                        loot.Add(new Item("bone", ItemQuality.Normal, _random.Next(2, 4)));
                    }
                    break;
                    
                case EnemyType.CaveSlug:
                    // Drops slime (herbs substitute) and sometimes rare materials
                    loot.Add(new Item("herbs", ItemQuality.Normal, _random.Next(2, 5)));  // Slime as herbs
                    if (_random.NextDouble() < 0.3)
                    {
                        loot.Add(new Item("anomaly_shard", ItemQuality.Normal, _random.Next(1, 2)));
                    }
                    if (_random.NextDouble() < 0.15)
                    {
                        loot.Add(new Item("mutagen", ItemQuality.Normal, 1));
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
