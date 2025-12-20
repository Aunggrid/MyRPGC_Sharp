// Gameplay/Entities/EnemyEntity.cs
// Basic enemy entity with simple AI

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using MyRPG.Gameplay.World;
using MyRPG.Gameplay.Systems;
using MyRPG.Gameplay.Items;
using MyRPG.Data;

namespace MyRPG.Gameplay.Entities
{
    /// <summary>
    /// Result of using a special ability
    /// </summary>
    public class AbilityResult
    {
        public EnemyAbility Ability { get; set; }
        public EnemyEntity User { get; set; }
        public bool Success { get; set; } = false;
        public float Damage { get; set; } = 0f;
        public string Message { get; set; } = "";
        
        // For knockback
        public Vector2 KnockbackDirection { get; set; } = Vector2.Zero;
        public float KnockbackDistance { get; set; } = 0f;
        
        // For spawning
        public Vector2 SpawnPosition { get; set; } = Vector2.Zero;
        public EnemyType SpawnType { get; set; } = EnemyType.Swarmling;
    }
    
    public class EnemyEntity
    {
        // Static events for floating text (subscribed by Game1)
        public static event Action<Vector2, float, bool> OnDamageDealtToPlayer;  // Position, damage, isCritical
        public static event Action<Vector2> OnMissPlayer;                          // Position
        public static event Action<Vector2, float> OnEnemyTakeDamage;             // Position, damage
        
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
        
        // Combat - Separate AP (Actions) and MP (Movement) like BG3
        public int MaxActionPoints { get; set; } = 2;      // Actions: attack, use ability
        public int CurrentActionPoints { get; set; } = 2;
        public int MaxMovementPoints { get; set; } = 3;    // Movement tiles per turn
        public int CurrentMovementPoints { get; set; } = 3;
        public int Initiative { get; set; } = 0;
        
        // AI Personality (Rimworld-style)
        public AIPersonality Personality { get; set; } = AIPersonality.Balanced;
        public float AggressionLevel { get; set; } = 0.5f;  // 0-1: cautious to aggressive
        public float RetreatThreshold { get; set; } = 0.15f; // HP% to consider retreating
        
        // Tactical Awareness
        public bool PrefersFlanking { get; set; } = true;
        public bool SeeksCover { get; set; } = false;
        public bool FocusesWeakTargets { get; set; } = true;
        
        // Special Abilities
        public EnemyAbility PrimaryAbility { get; set; } = EnemyAbility.None;
        public EnemyAbility SecondaryAbility { get; set; } = EnemyAbility.None;
        public float AbilityCooldown { get; set; } = 0f;  // Turns until ability ready
        public int AbilityCooldownMax { get; set; } = 3;  // Turns between uses
        public float AbilityChance { get; set; } = 0.5f;  // Chance to use ability vs normal attack
        
        // Stealth System (for Stalker)
        public bool IsStealthed { get; set; } = false;
        public bool WasStealthed { get; private set; } = false; // For ambush damage bonus
        
        // Spawn System (for HiveMother)
        public int SpawnCount { get; set; } = 0;
        public int MaxSpawns { get; set; } = 3;
        
        // Visual indicator for special enemies
        public Color TintColor { get; set; } = Color.White;
        
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
                    // Combat stats
                    MaxActionPoints = 2;
                    MaxMovementPoints = 4;
                    Personality = AIPersonality.Balanced;
                    RetreatThreshold = 0.15f;  // Only retreats at 15% HP
                    PrefersFlanking = true;
                    break;
                    
                case EnemyType.MutantBeast:
                    Name = "Mutant Beast";
                    MaxHealth = 35f;
                    Speed = 220f;
                    Damage = 12f;
                    Accuracy = 0.7f;
                    SightRange = 10;
                    AttackRange = 1;
                    // Fast and aggressive
                    MaxActionPoints = 2;
                    MaxMovementPoints = 6;  // Very mobile
                    Personality = AIPersonality.Aggressive;
                    RetreatThreshold = 0.0f;  // Never retreats
                    PrefersFlanking = false;
                    AggressionLevel = 0.9f;
                    break;
                    
                case EnemyType.Hunter:
                    Name = "Hunter";
                    MaxHealth = 40f;
                    Speed = 130f;
                    Damage = 15f;
                    Accuracy = 0.75f;
                    SightRange = 12;
                    AttackRange = 6;  // Ranged!
                    // Tactical ranged
                    MaxActionPoints = 2;
                    MaxMovementPoints = 3;
                    Personality = AIPersonality.Tactical;
                    RetreatThreshold = 0.25f;  // Retreats early
                    SeeksCover = true;
                    AggressionLevel = 0.4f;
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
                    // Slow but powerful tank
                    MaxActionPoints = 2;
                    MaxMovementPoints = 2;
                    Personality = AIPersonality.Berserk;
                    RetreatThreshold = 0.0f;  // Never retreats
                    AggressionLevel = 1.0f;
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
                    MaxActionPoints = 1;
                    MaxMovementPoints = 5;
                    Personality = AIPersonality.Cowardly;
                    RetreatThreshold = 0.8f;  // Flees at 80% HP
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
                    MaxActionPoints = 2;
                    MaxMovementPoints = 3;
                    Personality = AIPersonality.Cautious;
                    RetreatThreshold = 0.3f;
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
                    MaxActionPoints = 2;
                    MaxMovementPoints = 4;
                    Personality = AIPersonality.Aggressive;  // Charges when provoked
                    RetreatThreshold = 0.0f;
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
                    MaxActionPoints = 1;
                    MaxMovementPoints = 7;  // Very fast
                    Personality = AIPersonality.Cowardly;
                    RetreatThreshold = 0.9f;
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
                    MaxActionPoints = 2;
                    MaxMovementPoints = 1;  // Very slow
                    Personality = AIPersonality.Cautious;
                    RetreatThreshold = 0.1f;
                    break;
                    
                // === SPECIAL ABILITY ENEMIES ===
                
                case EnemyType.Spitter:
                    Name = "Acid Spitter";
                    MaxHealth = 30f;
                    Speed = 120f;
                    Damage = 8f;
                    Accuracy = 0.7f;
                    SightRange = 8;
                    AttackRange = 5;  // Ranged!
                    Behavior = CreatureBehavior.Aggressive;
                    PrimaryAbility = EnemyAbility.AcidSpit;
                    AbilityChance = 0.6f;
                    AbilityCooldownMax = 2;
                    TintColor = new Color(150, 255, 100);  // Green tint
                    MaxActionPoints = 2;
                    MaxMovementPoints = 3;
                    Personality = AIPersonality.Tactical;
                    SeeksCover = true;
                    RetreatThreshold = 0.2f;
                    break;
                    
                case EnemyType.Psionic:
                    Name = "Psionic Mutant";
                    MaxHealth = 45f;
                    Speed = 100f;
                    Damage = 6f;
                    Accuracy = 0.85f;
                    SightRange = 10;
                    AttackRange = 4;  // Medium range mental attack
                    Behavior = CreatureBehavior.Aggressive;
                    PrimaryAbility = EnemyAbility.PsionicBlast;
                    AbilityChance = 0.7f;
                    AbilityCooldownMax = 3;
                    TintColor = new Color(200, 100, 255);  // Purple tint
                    MaxActionPoints = 2;
                    MaxMovementPoints = 2;
                    Personality = AIPersonality.Tactical;
                    FocusesWeakTargets = true;
                    RetreatThreshold = 0.3f;
                    break;
                    
                case EnemyType.Brute:
                    Name = "Mutant Brute";
                    MaxHealth = 150f;
                    Speed = 80f;  // Slow but tanky
                    Damage = 25f;
                    Accuracy = 0.55f;
                    SightRange = 6;
                    AttackRange = 1;
                    Behavior = CreatureBehavior.Aggressive;
                    PrimaryAbility = EnemyAbility.Knockback;
                    AbilityChance = 0.4f;
                    TintColor = new Color(180, 120, 80);  // Brown tint
                    MaxActionPoints = 2;
                    MaxMovementPoints = 2;  // Slow but hits hard
                    Personality = AIPersonality.Berserk;
                    RetreatThreshold = 0.0f;  // Never retreats
                    AggressionLevel = 1.0f;
                    break;
                    
                case EnemyType.Stalker:
                    Name = "Shadow Stalker";
                    MaxHealth = 35f;
                    Speed = 200f;
                    Damage = 18f;  // High damage
                    Accuracy = 0.8f;
                    SightRange = 12;
                    AttackRange = 1;
                    Behavior = CreatureBehavior.Aggressive;
                    PrimaryAbility = EnemyAbility.Ambush;
                    IsStealthed = true;  // Starts stealthed
                    TintColor = new Color(80, 80, 100);  // Dark tint
                    MaxActionPoints = 2;
                    MaxMovementPoints = 5;  // Fast and sneaky
                    Personality = AIPersonality.Tactical;
                    PrefersFlanking = true;
                    RetreatThreshold = 0.25f;  // Retreats to re-stealth
                    AggressionLevel = 0.7f;
                    break;
                    
                case EnemyType.HiveMother:
                    Name = "Hive Mother";
                    MaxHealth = 80f;
                    Speed = 70f;
                    Damage = 10f;
                    Accuracy = 0.6f;
                    SightRange = 8;
                    AttackRange = 1;
                    Behavior = CreatureBehavior.Aggressive;
                    PrimaryAbility = EnemyAbility.SpawnSwarmling;
                    SecondaryAbility = EnemyAbility.Regenerate;
                    AbilityChance = 0.5f;
                    AbilityCooldownMax = 2;
                    MaxSpawns = 4;
                    TintColor = new Color(255, 200, 150);  // Pale orange
                    MaxActionPoints = 2;
                    MaxMovementPoints = 2;  // Slow, relies on spawns
                    Personality = AIPersonality.Cautious;
                    RetreatThreshold = 0.15f;
                    break;
                    
                case EnemyType.Swarmling:
                    Name = "Swarmling";
                    MaxHealth = 12f;
                    Speed = 250f;  // Fast
                    Damage = 4f;
                    Accuracy = 0.6f;
                    SightRange = 6;
                    AttackRange = 1;
                    Behavior = CreatureBehavior.Aggressive;
                    PrimaryAbility = EnemyAbility.Explode;  // Explodes on death
                    TintColor = new Color(200, 180, 100);  // Yellow-brown
                    MaxActionPoints = 3;  // Extra actions - can attack twice
                    MaxMovementPoints = 6;  // Very mobile
                    Personality = AIPersonality.Berserk;
                    RetreatThreshold = 0.0f;  // Never retreats, explodes on death
                    AggressionLevel = 1.0f;
                    break;
            }
            
            // Set behavior for hostile types
            if (type == EnemyType.Raider || type == EnemyType.MutantBeast || 
                type == EnemyType.Hunter || type == EnemyType.Abomination ||
                type == EnemyType.Spitter || type == EnemyType.Psionic ||
                type == EnemyType.Brute || type == EnemyType.Stalker ||
                type == EnemyType.HiveMother || type == EnemyType.Swarmling)
            {
                Behavior = CreatureBehavior.Aggressive;
            }
            
            // Initialize current values
            CurrentHealth = MaxHealth;
            CurrentActionPoints = MaxActionPoints;
            CurrentMovementPoints = MaxMovementPoints;
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
        
        public void Update(float deltaTime, WorldGrid grid, Vector2 playerPosition, List<EnemyEntity> allEnemies = null)
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
                    UpdateIdle(deltaTime, grid, playerPosition, tileDistance, allEnemies);
                    break;
                    
                case EnemyState.Patrolling:
                    UpdatePatrolling(deltaTime, grid, playerPosition, tileDistance, allEnemies);
                    break;
                    
                case EnemyState.Chasing:
                    UpdateChasing(deltaTime, grid, playerPosition, tileDistance, allEnemies);
                    break;
                    
                case EnemyState.Fleeing:
                    UpdateFleeing(deltaTime, grid, playerPosition, tileDistance, allEnemies);
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
        
        private void UpdateIdle(float deltaTime, WorldGrid grid, Vector2 playerPosition, int tileDistance, List<EnemyEntity> allEnemies)
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
        
        private void UpdatePatrolling(float deltaTime, WorldGrid grid, Vector2 playerPosition, int tileDistance, List<EnemyEntity> allEnemies)
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
                MoveAlongPath(deltaTime, grid, allEnemies);
            }
            else
            {
                State = EnemyState.Idle;
            }
        }
        
        private void UpdateChasing(float deltaTime, WorldGrid grid, Vector2 playerPosition, int tileDistance, List<EnemyEntity> allEnemies)
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
                MoveAlongPath(deltaTime, grid, allEnemies);
            }
        }
        
        private void UpdateFleeing(float deltaTime, WorldGrid grid, Vector2 playerPosition, int tileDistance, List<EnemyEntity> allEnemies)
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
                MoveAlongPath(deltaTime * 1.3f, grid, allEnemies);  // 30% faster when fleeing
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
        
        private void MoveAlongPath(float deltaTime, WorldGrid grid, List<EnemyEntity> allEnemies)
        {
            if (CurrentPath.Count == 0) return;
            
            Point nextTile = CurrentPath[0];
            
            // Check if next tile is occupied by another enemy
            if (allEnemies != null && IsTileOccupiedByEnemy(nextTile, grid.TileSize, allEnemies))
            {
                // Try to find alternate path around the blocking enemy
                Point currentTile = new Point((int)(Position.X / grid.TileSize), (int)(Position.Y / grid.TileSize));
                
                // Look for alternate adjacent tile
                Point? alternateTile = FindAlternateMoveTile(grid, currentTile, nextTile, allEnemies);
                if (alternateTile.HasValue)
                {
                    nextTile = alternateTile.Value;
                    // Replace the blocked path segment
                    CurrentPath[0] = nextTile;
                }
                else
                {
                    // Blocked, wait
                    CurrentPath.Clear();
                    return;
                }
            }
            
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
        
        /// <summary>
        /// Find an alternate tile to move to when path is blocked
        /// </summary>
        private Point? FindAlternateMoveTile(WorldGrid grid, Point current, Point blocked, List<EnemyEntity> allEnemies)
        {
            // Get direction we're trying to go
            int dx = Math.Sign(blocked.X - current.X);
            int dy = Math.Sign(blocked.Y - current.Y);
            
            // Try perpendicular directions first
            Point[] alternatives;
            if (dx != 0 && dy != 0)
            {
                // Diagonal - try both straight directions
                alternatives = new Point[]
                {
                    new Point(current.X + dx, current.Y),
                    new Point(current.X, current.Y + dy)
                };
            }
            else if (dx != 0)
            {
                // Horizontal - try vertical detours
                alternatives = new Point[]
                {
                    new Point(current.X + dx, current.Y + 1),
                    new Point(current.X + dx, current.Y - 1),
                    new Point(current.X, current.Y + 1),
                    new Point(current.X, current.Y - 1)
                };
            }
            else
            {
                // Vertical - try horizontal detours
                alternatives = new Point[]
                {
                    new Point(current.X + 1, current.Y + dy),
                    new Point(current.X - 1, current.Y + dy),
                    new Point(current.X + 1, current.Y),
                    new Point(current.X - 1, current.Y)
                };
            }
            
            // Shuffle to add variety
            for (int i = alternatives.Length - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (alternatives[i], alternatives[j]) = (alternatives[j], alternatives[i]);
            }
            
            foreach (var alt in alternatives)
            {
                if (IsValidAndWalkable(grid, alt) && !IsTileOccupiedByEnemy(alt, grid.TileSize, allEnemies))
                {
                    return alt;
                }
            }
            
            return null;
        }
        
        // ============================================
        // COMBAT ACTIONS
        // ============================================
        
        /// <summary>
        /// Reset AP and MP at start of turn
        /// </summary>
        public void StartTurn()
        {
            CurrentActionPoints = MaxActionPoints;
            CurrentMovementPoints = MaxMovementPoints;
            
            // Check stunned
            if (GameServices.StatusEffects.HasEffect(StatusEffects, StatusEffectType.Stunned))
            {
                CurrentActionPoints = 0;
                CurrentMovementPoints = 0;
                System.Diagnostics.Debug.WriteLine($">>> {Name} is STUNNED! Skipping turn. <<<");
            }
        }
        
        /// <summary>
        /// AI decides what to do with its turn (BG3/Rimworld-style tactical AI)
        /// Returns true when turn is complete
        /// </summary>
        public bool TakeTurn(WorldGrid grid, PlayerEntity player, List<EnemyEntity> allEnemies = null, Vector2? combatCenter = null)
        {
            if (CurrentActionPoints <= 0 && CurrentMovementPoints <= 0) return true;
            if (!IsAlive) return true;
            
            // Tick cooldowns at start of turn
            TickCooldown();
            
            // Apply regeneration if we have it
            ApplyRegeneration();
            
            Point enemyTile = new Point((int)(Position.X / grid.TileSize), (int)(Position.Y / grid.TileSize));
            Point playerTile = new Point((int)(player.Position.X / grid.TileSize), (int)(player.Position.Y / grid.TileSize));
            
            // Use Chebyshev distance (allows diagonal)
            int distanceToPlayer = Pathfinder.GetDistance(enemyTile, playerTile);
            
            // Calculate distance to combat center
            int distanceToCombatCenter = 0;
            Point combatCenterTile = playerTile;
            if (combatCenter.HasValue)
            {
                combatCenterTile = new Point(
                    (int)(combatCenter.Value.X / grid.TileSize),
                    (int)(combatCenter.Value.Y / grid.TileSize)
                );
                distanceToCombatCenter = Pathfinder.GetDistance(enemyTile, combatCenterTile);
            }
            
            // === PASSIVE BEHAVIOR (not provoked) ===
            if ((Behavior == CreatureBehavior.Passive || Behavior == CreatureBehavior.Cowardly) && !IsProvoked)
            {
                return TakePassiveTurn(grid, combatCenterTile, enemyTile, distanceToCombatCenter, allEnemies);
            }
            
            // === COWARDLY BEHAVIOR (provoked) ===
            if (Behavior == CreatureBehavior.Cowardly && IsProvoked)
            {
                return TakeCowardlyTurn(grid, playerTile, enemyTile, allEnemies);
            }
            
            // === TACTICAL AI FOR AGGRESSIVE ENEMIES ===
            return TakeTacticalTurn(grid, player, playerTile, enemyTile, allEnemies, distanceToPlayer);
        }
        
        /// <summary>
        /// Smart tactical turn for aggressive enemies (BG3/Rimworld-style)
        /// Uses separate AP for actions and MP for movement
        /// </summary>
        private bool TakeTacticalTurn(WorldGrid grid, PlayerEntity player, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies, int distanceToPlayer)
        {
            float healthPercent = CurrentHealth / MaxHealth;
            
            // =============================================
            // PHASE 1: PERSONALITY-BASED RETREAT CHECK
            // Only some personalities retreat, with varying thresholds
            // =============================================
            if (ShouldConsiderRetreat(healthPercent))
            {
                if (TryRetreat(grid, playerTile, enemyTile, allEnemies))
                {
                    return true;
                }
            }
            
            // =============================================
            // PHASE 2: SPECIAL ABILITIES / TACTICAL ACTIONS
            // Check for special behaviors based on enemy type
            // =============================================
            
            // Stalker: Re-stealth when far and low on health
            if (Type == EnemyType.Stalker && !IsStealthed && distanceToPlayer > 5)
            {
                if (healthPercent < RetreatThreshold || _random.NextDouble() < 0.2f)
                {
                    EnterStealth();
                    // Use all MP to get away and set up ambush
                    while (CurrentMovementPoints > 0)
                    {
                        var retreatPos = FindRetreatPosition(grid, playerTile, enemyTile, allEnemies);
                        if (retreatPos.HasValue)
                        {
                            Position = new Vector2(retreatPos.Value.X * grid.TileSize, retreatPos.Value.Y * grid.TileSize);
                            enemyTile = retreatPos.Value;
                            CurrentMovementPoints--;
                        }
                        else break;
                    }
                    return true;
                }
            }
            
            // HiveMother: Prioritize spawning when hurt or low on spawns
            if (Type == EnemyType.HiveMother && SpawnCount < MaxSpawns && CurrentActionPoints >= 1)
            {
                bool shouldSpawn = healthPercent < 0.5f || SpawnCount == 0 || ShouldUseAbility();
                if (shouldSpawn)
                {
                    LastAbilityResult = UseAbility(player, grid.TileSize);
                    CurrentActionPoints--;
                    // Don't return - can still move/attack
                }
            }
            
            // =============================================
            // PHASE 3: MOVEMENT (Use MP)
            // Different strategies based on personality and range
            // =============================================
            
            if (CurrentMovementPoints > 0)
            {
                ExecuteMovementPhase(grid, player, playerTile, enemyTile, allEnemies, distanceToPlayer);
                // Update position after movement
                enemyTile = new Point((int)(Position.X / grid.TileSize), (int)(Position.Y / grid.TileSize));
                distanceToPlayer = Pathfinder.GetDistance(enemyTile, playerTile);
            }
            
            // =============================================
            // PHASE 4: ACTIONS (Use AP)
            // Attack, use abilities, etc.
            // =============================================
            
            if (CurrentActionPoints > 0)
            {
                ExecuteActionPhase(grid, player, playerTile, enemyTile, allEnemies, distanceToPlayer);
            }
            
            return true;  // Turn complete
        }
        
        /// <summary>
        /// Check if this enemy should consider retreating based on personality
        /// </summary>
        private bool ShouldConsiderRetreat(float healthPercent)
        {
            // Berserk enemies NEVER retreat
            if (Personality == AIPersonality.Berserk) return false;
            
            // Check if below retreat threshold
            if (healthPercent > RetreatThreshold) return false;
            
            // Personality affects retreat chance
            float retreatChance = Personality switch
            {
                AIPersonality.Cowardly => 0.8f,    // 80% chance
                AIPersonality.Cautious => 0.5f,    // 50% chance
                AIPersonality.Tactical => 0.3f,    // 30% chance - only if tactically smart
                AIPersonality.Balanced => 0.15f,   // 15% chance
                AIPersonality.Aggressive => 0.05f, // 5% chance - rarely retreats
                _ => 0.1f
            };
            
            return _random.NextDouble() < retreatChance;
        }
        
        /// <summary>
        /// Attempt to retreat from combat
        /// </summary>
        private bool TryRetreat(WorldGrid grid, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies)
        {
            // Use all movement points to flee
            int movesMade = 0;
            while (CurrentMovementPoints > 0)
            {
                var retreatPos = FindRetreatPosition(grid, playerTile, enemyTile, allEnemies);
                if (retreatPos.HasValue)
                {
                    Position = new Vector2(retreatPos.Value.X * grid.TileSize, retreatPos.Value.Y * grid.TileSize);
                    enemyTile = retreatPos.Value;
                    CurrentMovementPoints--;
                    movesMade++;
                }
                else break;
            }
            
            System.Diagnostics.Debug.WriteLine($">>> {Name} is retreating! (HP: {CurrentHealth:F0}/{MaxHealth:F0}) <<<");
            return movesMade > 0;
        }
        
        /// <summary>
        /// Execute movement phase based on personality and situation
        /// </summary>
        private void ExecuteMovementPhase(WorldGrid grid, PlayerEntity player, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies, int distanceToPlayer)
        {
            // Ranged enemies: Maintain optimal distance
            if (AttackRange > 1)
            {
                ExecuteRangedMovement(grid, playerTile, enemyTile, allEnemies, distanceToPlayer);
                return;
            }
            
            // Melee enemies: Close in, potentially flank
            ExecuteMeleeMovement(grid, player, playerTile, enemyTile, allEnemies, distanceToPlayer);
        }
        
        /// <summary>
        /// Ranged enemy movement - maintain distance, seek cover
        /// </summary>
        private void ExecuteRangedMovement(WorldGrid grid, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies, int distanceToPlayer)
        {
            int optimalRange = Math.Max(AttackRange - 1, 3);
            
            // Too close? Back up
            if (distanceToPlayer < optimalRange)
            {
                while (CurrentMovementPoints > 0 && distanceToPlayer < optimalRange)
                {
                    var retreatPos = FindRetreatPosition(grid, playerTile, enemyTile, allEnemies);
                    if (retreatPos.HasValue)
                    {
                        Position = new Vector2(retreatPos.Value.X * grid.TileSize, retreatPos.Value.Y * grid.TileSize);
                        enemyTile = retreatPos.Value;
                        distanceToPlayer = Pathfinder.GetDistance(enemyTile, playerTile);
                        CurrentMovementPoints--;
                    }
                    else break;
                }
            }
            // Too far? Move closer but not too close
            else if (distanceToPlayer > AttackRange)
            {
                while (CurrentMovementPoints > 0 && distanceToPlayer > AttackRange)
                {
                    MoveTowardPlayer(grid, playerTile, enemyTile, allEnemies);
                    enemyTile = new Point((int)(Position.X / grid.TileSize), (int)(Position.Y / grid.TileSize));
                    distanceToPlayer = Pathfinder.GetDistance(enemyTile, playerTile);
                    CurrentMovementPoints--;
                }
            }
            // In range - maybe strafe for tactical advantage
            else if (Personality == AIPersonality.Tactical && _random.NextDouble() < 0.4f)
            {
                var strafePos = FindStrafePosition(grid, playerTile, enemyTile, allEnemies);
                if (strafePos.HasValue && CurrentMovementPoints > 0)
                {
                    Position = new Vector2(strafePos.Value.X * grid.TileSize, strafePos.Value.Y * grid.TileSize);
                    CurrentMovementPoints--;
                }
            }
        }
        
        /// <summary>
        /// Melee enemy movement - close in, flank if tactical
        /// </summary>
        private void ExecuteMeleeMovement(WorldGrid grid, PlayerEntity player, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies, int distanceToPlayer)
        {
            // Already adjacent? Don't need to move (unless flanking)
            if (distanceToPlayer <= AttackRange)
            {
                // Tactical enemies might reposition for flanking
                if (PrefersFlanking && Personality == AIPersonality.Tactical && CurrentMovementPoints > 0)
                {
                    var flankPos = FindFlankingPosition(grid, playerTile, enemyTile, allEnemies);
                    if (flankPos.HasValue && flankPos.Value != enemyTile)
                    {
                        Position = new Vector2(flankPos.Value.X * grid.TileSize, flankPos.Value.Y * grid.TileSize);
                        CurrentMovementPoints--;
                    }
                }
                return;
            }
            
            // Move toward player, using all MP if needed
            while (CurrentMovementPoints > 0 && distanceToPlayer > AttackRange)
            {
                Point oldTile = enemyTile;
                MoveTowardPlayer(grid, playerTile, enemyTile, allEnemies);
                
                enemyTile = new Point((int)(Position.X / grid.TileSize), (int)(Position.Y / grid.TileSize));
                distanceToPlayer = Pathfinder.GetDistance(enemyTile, playerTile);
                
                // Stuck? Stop trying
                if (enemyTile == oldTile) break;
                
                CurrentMovementPoints--;
            }
        }
        
        /// <summary>
        /// Execute action phase - attacks, abilities
        /// </summary>
        private void ExecuteActionPhase(WorldGrid grid, PlayerEntity player, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies, int distanceToPlayer)
        {
            // Use all available action points
            while (CurrentActionPoints > 0)
            {
                // Not in range? Can't attack
                if (distanceToPlayer > AttackRange)
                {
                    break;
                }
                
                // Decide: ability or normal attack?
                if (ShouldUseAbility() && PrimaryAbility != EnemyAbility.SpawnSwarmling)
                {
                    LastAbilityResult = UseAbility(player, grid.TileSize);
                    CurrentActionPoints--;
                }
                else
                {
                    AttackPlayer(player);
                    CurrentActionPoints--;
                }
            }
        }
        
        /// <summary>
        /// Find a flanking position (opposite side from other enemies)
        /// </summary>
        private Point? FindFlankingPosition(WorldGrid grid, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies)
        {
            // Get tiles adjacent to player
            var adjacentTiles = new List<Point>
            {
                new Point(playerTile.X + 1, playerTile.Y),
                new Point(playerTile.X - 1, playerTile.Y),
                new Point(playerTile.X, playerTile.Y + 1),
                new Point(playerTile.X, playerTile.Y - 1),
                new Point(playerTile.X + 1, playerTile.Y + 1),
                new Point(playerTile.X - 1, playerTile.Y - 1),
                new Point(playerTile.X + 1, playerTile.Y - 1),
                new Point(playerTile.X - 1, playerTile.Y + 1)
            };
            
            Point? bestTile = null;
            int lowestAdjacentEnemies = int.MaxValue;
            
            foreach (var tile in adjacentTiles)
            {
                if (!IsValidAndWalkable(grid, tile)) continue;
                if (IsTileOccupied(tile, grid.TileSize, allEnemies, playerTile)) continue;
                
                // Count adjacent enemies at this position
                int adjacentCount = CountAdjacentEnemies(tile, grid.TileSize, allEnemies);
                
                // Prefer tiles with fewer adjacent enemies (true flanking)
                if (adjacentCount < lowestAdjacentEnemies)
                {
                    lowestAdjacentEnemies = adjacentCount;
                    bestTile = tile;
                }
            }
            
            return bestTile;
        }
        
        /// <summary>
        /// Cowardly creature turn - flee!
        /// </summary>
        private bool TakeCowardlyTurn(WorldGrid grid, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies)
        {
            // Use all movement points to flee
            while (CurrentMovementPoints >= 1)
            {
                var fleePos = FindRetreatPosition(grid, playerTile, enemyTile, allEnemies);
                if (fleePos.HasValue)
                {
                    Position = new Vector2(fleePos.Value.X * grid.TileSize, fleePos.Value.Y * grid.TileSize);
                    enemyTile = fleePos.Value;
                    CurrentMovementPoints--;
                }
                else
                {
                    break;  // Can't flee, cornered
                }
            }
            return true;
        }
        
        // Store last ability result for external systems to read
        public AbilityResult LastAbilityResult { get; private set; } = null;
        
        /// <summary>
        /// Passive creature turn - flee from combat center if close, wander if far
        /// </summary>
        private bool TakePassiveTurn(WorldGrid grid, Point combatCenterTile, Point enemyTile, int distanceToCombatCenter, List<EnemyEntity> allEnemies)
        {
            // If close to combat (within 8 tiles), flee away from combat center
            if (distanceToCombatCenter <= 8)
            {
                while (CurrentMovementPoints >= 1)
                {
                    FleeFromPoint(grid, combatCenterTile, enemyTile, allEnemies);
                    CurrentMovementPoints--;
                    enemyTile = new Point((int)(Position.X / 64), (int)(Position.Y / 64));
                }
                System.Diagnostics.Debug.WriteLine($">>> {Name} fleeing from combat zone <<<");
                return true;
            }
            
            // If far enough from combat, wander randomly
            if (CurrentMovementPoints >= 1)
            {
                WanderRandomly(grid, enemyTile, allEnemies);
                CurrentMovementPoints--;
                System.Diagnostics.Debug.WriteLine($">>> {Name} wandering at edge of combat zone <<<");
            }
            
            return true;
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
            // === SMART AI: Choose optimal destination ===
            Point? destination = null;
            
            // Ranged enemies: maintain distance
            if (AttackRange > 1)
            {
                destination = FindOptimalRangedPosition(grid, playerTile, enemyTile, allEnemies);
            }
            // Melee enemies: flank the player
            else
            {
                destination = FindOptimalMeleePosition(grid, playerTile, enemyTile, allEnemies);
            }
            
            // Low HP: try to retreat
            float healthPercent = CurrentHealth / MaxHealth;
            if (healthPercent < 0.25f && Behavior != CreatureBehavior.Aggressive)
            {
                destination = FindRetreatPosition(grid, playerTile, enemyTile, allEnemies);
            }
            
            // If no smart destination, fall back to direct approach
            if (destination == null || destination == enemyTile)
            {
                destination = FindDirectApproach(grid, playerTile, enemyTile, allEnemies);
            }
            
            // Move toward destination
            if (destination.HasValue && destination.Value != enemyTile)
            {
                MoveOneStepToward(grid, destination.Value, enemyTile, allEnemies, playerTile);
            }
        }
        
        /// <summary>
        /// Find optimal position for ranged attackers (maintain distance)
        /// </summary>
        private Point? FindOptimalRangedPosition(WorldGrid grid, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies)
        {
            int currentDist = Pathfinder.GetDistance(enemyTile, playerTile);
            int idealDist = AttackRange - 1;  // Stay at attack range - 1
            
            // If too close, back away
            if (currentDist < idealDist)
            {
                return FindRetreatPosition(grid, playerTile, enemyTile, allEnemies);
            }
            
            // If too far, approach but stop at range
            if (currentDist > AttackRange)
            {
                // Find tile that's within attack range
                var candidates = GetTilesAtDistance(playerTile, idealDist, idealDist + 1);
                return FindBestUnoccupiedTile(grid, candidates, enemyTile, allEnemies, playerTile);
            }
            
            // In range - strafe sideways if possible to avoid being predictable
            if (_random.NextDouble() < 0.3)
            {
                return FindStrafePosition(grid, playerTile, enemyTile, allEnemies);
            }
            
            return enemyTile; // Stay put
        }
        
        /// <summary>
        /// Find optimal melee attack position (flank the player)
        /// </summary>
        private Point? FindOptimalMeleePosition(WorldGrid grid, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies)
        {
            // Get tiles adjacent to player
            var adjacentTiles = new List<Point>
            {
                new Point(playerTile.X + 1, playerTile.Y),
                new Point(playerTile.X - 1, playerTile.Y),
                new Point(playerTile.X, playerTile.Y + 1),
                new Point(playerTile.X, playerTile.Y - 1),
                new Point(playerTile.X + 1, playerTile.Y + 1),
                new Point(playerTile.X - 1, playerTile.Y - 1),
                new Point(playerTile.X + 1, playerTile.Y - 1),
                new Point(playerTile.X - 1, playerTile.Y + 1)
            };
            
            // Already adjacent? Stay put
            if (adjacentTiles.Contains(enemyTile))
            {
                return enemyTile;
            }
            
            // Find best unoccupied adjacent tile (prefer flanking - opposite side from other enemies)
            Point? bestTile = null;
            int bestScore = int.MinValue;
            
            foreach (var tile in adjacentTiles)
            {
                if (!IsValidAndWalkable(grid, tile)) continue;
                if (IsTileOccupied(tile, grid.TileSize, allEnemies, playerTile)) continue;
                
                // Score: prefer tiles with fewer adjacent enemies (flanking)
                int score = 100;
                
                // Distance penalty (prefer closer tiles)
                int dist = Pathfinder.GetDistance(enemyTile, tile);
                score -= dist * 5;
                
                // Flanking bonus: prefer tiles opposite from other enemies
                int adjacentEnemyCount = CountAdjacentEnemies(tile, grid.TileSize, allEnemies);
                score += (3 - adjacentEnemyCount) * 20;  // Fewer adjacent enemies = better
                
                // Diagonal bonus (harder for player to deal with diagonals)
                if (tile.X != playerTile.X && tile.Y != playerTile.Y)
                {
                    score += 10;
                }
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTile = tile;
                }
            }
            
            return bestTile ?? FindDirectApproach(grid, playerTile, enemyTile, allEnemies);
        }
        
        /// <summary>
        /// Find retreat position when low on health
        /// </summary>
        private Point? FindRetreatPosition(WorldGrid grid, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies)
        {
            Point? bestTile = null;
            int bestDist = Pathfinder.GetDistance(enemyTile, playerTile);
            
            // Check all 8 directions
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    
                    Point tile = new Point(enemyTile.X + dx, enemyTile.Y + dy);
                    if (!IsValidAndWalkable(grid, tile)) continue;
                    if (IsTileOccupied(tile, grid.TileSize, allEnemies, playerTile)) continue;
                    
                    int dist = Pathfinder.GetDistance(tile, playerTile);
                    if (dist > bestDist)
                    {
                        bestDist = dist;
                        bestTile = tile;
                    }
                }
            }
            
            return bestTile;
        }
        
        /// <summary>
        /// Find strafe position (move sideways relative to player)
        /// </summary>
        private Point? FindStrafePosition(WorldGrid grid, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies)
        {
            // Calculate perpendicular directions
            int dx = playerTile.X - enemyTile.X;
            int dy = playerTile.Y - enemyTile.Y;
            
            // Perpendicular is (-dy, dx) or (dy, -dx)
            Point[] strafeTiles = new Point[]
            {
                new Point(enemyTile.X - Math.Sign(dy), enemyTile.Y + Math.Sign(dx)),
                new Point(enemyTile.X + Math.Sign(dy), enemyTile.Y - Math.Sign(dx))
            };
            
            // Shuffle to randomize
            if (_random.NextDouble() > 0.5)
            {
                (strafeTiles[0], strafeTiles[1]) = (strafeTiles[1], strafeTiles[0]);
            }
            
            foreach (var tile in strafeTiles)
            {
                if (IsValidAndWalkable(grid, tile) && !IsTileOccupied(tile, grid.TileSize, allEnemies, playerTile))
                {
                    return tile;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Simple direct approach when smart positioning fails
        /// </summary>
        private Point? FindDirectApproach(WorldGrid grid, Point playerTile, Point enemyTile, List<EnemyEntity> allEnemies)
        {
            // Calculate direction to player
            int dx = Math.Sign(playerTile.X - enemyTile.X);
            int dy = Math.Sign(playerTile.Y - enemyTile.Y);
            
            // Try direct diagonal first
            Point[] candidates = new Point[]
            {
                new Point(enemyTile.X + dx, enemyTile.Y + dy),  // Diagonal toward player
                new Point(enemyTile.X + dx, enemyTile.Y),       // Horizontal
                new Point(enemyTile.X, enemyTile.Y + dy),       // Vertical
                new Point(enemyTile.X + dx, enemyTile.Y - dy),  // Other diagonal
                new Point(enemyTile.X - dx, enemyTile.Y + dy),  // Other diagonal
            };
            
            foreach (var tile in candidates)
            {
                if (tile == enemyTile) continue;
                if (!IsValidAndWalkable(grid, tile)) continue;
                if (IsTileOccupied(tile, grid.TileSize, allEnemies, playerTile)) continue;
                
                // Don't step on player
                if (tile == playerTile) continue;
                
                return tile;
            }
            
            return null;
        }
        
        /// <summary>
        /// Move one step toward destination, avoiding obstacles
        /// </summary>
        private void MoveOneStepToward(WorldGrid grid, Point destination, Point enemyTile, List<EnemyEntity> allEnemies, Point playerTile)
        {
            // Direct movement if adjacent
            int dist = Pathfinder.GetDistance(enemyTile, destination);
            if (dist <= 1)
            {
                if (IsValidAndWalkable(grid, destination) && !IsTileOccupied(destination, grid.TileSize, allEnemies, playerTile))
                {
                    Position = new Vector2(destination.X * grid.TileSize, destination.Y * grid.TileSize);
                    CurrentPath.Clear();
                    return;
                }
            }
            
            // Need pathfinding
            if (CurrentPath.Count == 0)
            {
                var path = Pathfinder.FindPath(grid, enemyTile, destination);
                if (path != null && path.Count > 0)
                {
                    CurrentPath = path;
                }
            }
            
            // Move along path
            if (CurrentPath.Count > 0)
            {
                Point nextTile = CurrentPath[0];
                
                // Check if blocked
                if (nextTile == playerTile || IsTileOccupied(nextTile, grid.TileSize, allEnemies, playerTile))
                {
                    CurrentPath.Clear();
                    
                    // Try adjacent tiles as fallback
                    var fallback = FindDirectApproach(grid, playerTile, enemyTile, allEnemies);
                    if (fallback.HasValue)
                    {
                        Position = new Vector2(fallback.Value.X * grid.TileSize, fallback.Value.Y * grid.TileSize);
                    }
                    return;
                }
                
                Position = new Vector2(nextTile.X * grid.TileSize, nextTile.Y * grid.TileSize);
                CurrentPath.RemoveAt(0);
            }
        }
        
        // === HELPER METHODS ===
        
        private bool IsValidAndWalkable(WorldGrid grid, Point tile)
        {
            if (tile.X < 0 || tile.X >= grid.Width || tile.Y < 0 || tile.Y >= grid.Height)
                return false;
            var t = grid.GetTile(tile.X, tile.Y);
            return t != null && t.IsWalkable;
        }
        
        private bool IsTileOccupied(Point tile, int tileSize, List<EnemyEntity> allEnemies, Point playerTile)
        {
            // Check player tile
            if (tile == playerTile) return true;
            
            // Check other enemies
            return IsTileOccupiedByEnemy(tile, tileSize, allEnemies);
        }
        
        private int CountAdjacentEnemies(Point tile, int tileSize, List<EnemyEntity> allEnemies)
        {
            int count = 0;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Point adjacent = new Point(tile.X + dx, tile.Y + dy);
                    if (IsTileOccupiedByEnemy(adjacent, tileSize, allEnemies))
                        count++;
                }
            }
            return count;
        }
        
        private List<Point> GetTilesAtDistance(Point center, int minDist, int maxDist)
        {
            var tiles = new List<Point>();
            for (int x = center.X - maxDist; x <= center.X + maxDist; x++)
            {
                for (int y = center.Y - maxDist; y <= center.Y + maxDist; y++)
                {
                    int dist = Pathfinder.GetDistance(new Point(x, y), center);
                    if (dist >= minDist && dist <= maxDist)
                    {
                        tiles.Add(new Point(x, y));
                    }
                }
            }
            return tiles;
        }
        
        private Point? FindBestUnoccupiedTile(WorldGrid grid, List<Point> candidates, Point currentTile, List<EnemyEntity> allEnemies, Point playerTile)
        {
            Point? best = null;
            int bestDist = int.MaxValue;
            
            foreach (var tile in candidates)
            {
                if (!IsValidAndWalkable(grid, tile)) continue;
                if (IsTileOccupied(tile, grid.TileSize, allEnemies, playerTile)) continue;
                
                int dist = Pathfinder.GetDistance(currentTile, tile);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = tile;
                }
            }
            
            return best;
        }
        
        /// <summary>
        /// Check if a tile is occupied by another enemy
        /// </summary>
        private bool IsTileOccupiedByEnemy(Point tile, int tileSize, List<EnemyEntity> allEnemies)
        {
            if (allEnemies == null) return false;
            
            foreach (var other in allEnemies)
            {
                if (other == this) continue;
                if (!other.IsAlive) continue;
                
                Point otherTile = new Point(
                    (int)(other.Position.X / tileSize),
                    (int)(other.Position.Y / tileSize)
                );
                
                if (otherTile == tile) return true;
            }
            return false;
        }
        
        /// <summary>
        /// Calculate hit chance for this enemy at a specific distance
        /// Ranged enemies have penalties at close range
        /// </summary>
        public float GetHitChance(int distance)
        {
            float baseAccuracy = Accuracy;
            
            // Only ranged enemies have distance penalties
            if (AttackRange <= 1)
            {
                return baseAccuracy;  // Melee - no penalty
            }
            
            // Ranged enemy accuracy modifiers
            float distanceModifier = 1f;
            
            if (distance <= 1)
            {
                // Point blank - hard to shoot at melee range
                distanceModifier = 0.5f;
            }
            else if (distance == 2)
            {
                // Very close
                distanceModifier = 0.7f;
            }
            else if (distance == 3)
            {
                // Close
                distanceModifier = 0.9f;
            }
            else if (distance > AttackRange)
            {
                // Beyond range
                float overRange = distance - AttackRange;
                distanceModifier = Math.Max(0.1f, 1f - (overRange * 0.25f));
            }
            // else: optimal range
            
            return Math.Clamp(baseAccuracy * distanceModifier, 0.1f, 0.95f);
        }
        
        private void AttackPlayer(PlayerEntity player)
        {
            // Start attack animation
            StartAttackAnimation(player.Position, 64);  // Assume 64 tile size
            
            // Calculate distance for accuracy
            int tileSize = 64;
            Point myTile = GetTilePosition(tileSize);
            Point playerTile = new Point((int)(player.Position.X / tileSize), (int)(player.Position.Y / tileSize));
            int distance = Math.Max(Math.Abs(myTile.X - playerTile.X), Math.Abs(myTile.Y - playerTile.Y));
            
            // Calculate hit chance based on distance (ranged penalty at close range)
            float hitChance = GetHitChance(distance);
            
            // Modify by player's dodge
            float playerDodgeBonus = 1f - player.Stats.Body.GetMovementModifier();  // Injured legs = less dodge
            hitChance *= (1f + playerDodgeBonus * 0.3f);
            hitChance = Math.Clamp(hitChance, 0.1f, 0.95f);
            
            float roll = (float)_random.NextDouble();
            
            if (roll <= hitChance)
            {
                // Hit! Use body part damage system
                var damageResult = player.Stats.TakeDamageToBody(Damage, DamageType.Physical);
                player.TriggerHitFlash();  // Visual feedback
                
                string partHit = damageResult.HitPart?.Name ?? "body";
                string armorInfo = damageResult.ArmorReduction > 0 ? $" (armor blocked {damageResult.ArmorReduction:F0})" : "";
                
                System.Diagnostics.Debug.WriteLine($">>> {Name} hits player's {partHit} for {damageResult.FinalDamage:F0} damage!{armorInfo} HP: -{damageResult.HPLost:F0} (hit chance: {hitChance:P0}) <<<");
                
                // Fire damage event for floating text
                OnDamageDealtToPlayer?.Invoke(player.Position + new Vector2(16, 0), damageResult.FinalDamage, damageResult.IsCriticalHit);
                
                // Check for instant death on critical hit
                if (damageResult.IsInstantDeath && !damageResult.CanRelocateOrgan)
                {
                    System.Diagnostics.Debug.WriteLine($">>> CRITICAL PART DESTROYED! {partHit} <<<");
                }
            }
            else
            {
                // Miss!
                System.Diagnostics.Debug.WriteLine($">>> {Name} misses! (hit chance: {hitChance:P0}) <<<");
                
                // Fire miss event for floating text
                OnMissPlayer?.Invoke(player.Position + new Vector2(16, 0));
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
            
            // Fire damage event for floating text
            OnEnemyTakeDamage?.Invoke(Position + new Vector2(16, 0), amount);
            
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
        
        // ============================================
        // SPECIAL ABILITIES
        // ============================================
        
        /// <summary>
        /// Check if enemy should use ability instead of normal attack
        /// </summary>
        public bool ShouldUseAbility()
        {
            if (PrimaryAbility == EnemyAbility.None) return false;
            if (AbilityCooldown > 0) return false;
            return _random.NextDouble() < AbilityChance;
        }
        
        /// <summary>
        /// Reduce ability cooldown at start of turn
        /// </summary>
        public void TickCooldown()
        {
            if (AbilityCooldown > 0)
                AbilityCooldown--;
        }
        
        /// <summary>
        /// Use the primary ability (called from CombatManager)
        /// Returns the ability result info
        /// </summary>
        public AbilityResult UseAbility(PlayerEntity target, int tileSize)
        {
            var result = new AbilityResult
            {
                Ability = PrimaryAbility,
                User = this,
                Success = false
            };
            
            switch (PrimaryAbility)
            {
                case EnemyAbility.AcidSpit:
                    result = UseAcidSpit(target, tileSize);
                    break;
                    
                case EnemyAbility.PsionicBlast:
                    result = UsePsionicBlast(target, tileSize);
                    break;
                    
                case EnemyAbility.Knockback:
                    result = UseKnockback(target, tileSize);
                    break;
                    
                case EnemyAbility.Ambush:
                    result = UseAmbush(target, tileSize);
                    break;
                    
                case EnemyAbility.SpawnSwarmling:
                    result = UseSpawnSwarmling(tileSize);
                    break;
            }
            
            if (result.Success)
            {
                AbilityCooldown = AbilityCooldownMax;
            }
            
            return result;
        }
        
        private AbilityResult UseAcidSpit(PlayerEntity target, int tileSize)
        {
            var result = new AbilityResult
            {
                Ability = EnemyAbility.AcidSpit,
                User = this,
                Message = $"{Name} spits acid!"
            };
            
            // Check range
            float distance = Vector2.Distance(Position, target.Position) / tileSize;
            if (distance > AttackRange)
            {
                result.Message = $"{Name} is too far to spit acid!";
                return result;
            }
            
            // Roll to hit
            if (_random.NextDouble() < Accuracy)
            {
                float damage = Damage * 0.8f;  // Slightly less than melee
                target.Stats.TakeDamage(damage, DamageType.Acid);
                
                // Apply Burning status (acid burns)
                GameServices.StatusEffects.ApplyEffect(
                    target.Stats.StatusEffects,
                    StatusEffectType.Burning,
                    3f,
                    false,
                    Name
                );
                
                result.Success = true;
                result.Damage = damage;
                result.Message = $"{Name} spits acid at you for {damage:F0} damage! You're burning!";
            }
            else
            {
                result.Message = $"{Name}'s acid spit misses!";
            }
            
            return result;
        }
        
        private AbilityResult UsePsionicBlast(PlayerEntity target, int tileSize)
        {
            var result = new AbilityResult
            {
                Ability = EnemyAbility.PsionicBlast,
                User = this,
                Message = $"{Name} focuses its mind!"
            };
            
            float distance = Vector2.Distance(Position, target.Position) / tileSize;
            if (distance > AttackRange)
            {
                result.Message = $"{Name} can't reach your mind!";
                return result;
            }
            
            // Psionic attacks are hard to dodge
            if (_random.NextDouble() < Accuracy + 0.1f)
            {
                float damage = Damage;
                target.Stats.TakeDamage(damage, DamageType.Psychic);
                
                // Chance to stun
                if (_random.NextDouble() < 0.4f)
                {
                    GameServices.StatusEffects.ApplyEffect(
                        target.Stats.StatusEffects,
                        StatusEffectType.Stunned,
                        1f,
                        false,
                        Name
                    );
                    result.Message = $"{Name}'s psionic blast hits for {damage:F0}! You're stunned!";
                }
                else
                {
                    result.Message = $"{Name}'s psionic blast hits for {damage:F0}!";
                }
                
                result.Success = true;
                result.Damage = damage;
            }
            else
            {
                result.Message = $"You resist {Name}'s psionic attack!";
            }
            
            return result;
        }
        
        private AbilityResult UseKnockback(PlayerEntity target, int tileSize)
        {
            var result = new AbilityResult
            {
                Ability = EnemyAbility.Knockback,
                User = this,
                Message = $"{Name} swings with tremendous force!"
            };
            
            float distance = Vector2.Distance(Position, target.Position) / tileSize;
            if (distance > 1.5f)  // Melee range
            {
                result.Message = $"{Name} can't reach you!";
                return result;
            }
            
            if (_random.NextDouble() < Accuracy)
            {
                float damage = Damage * 1.2f;  // Heavy hit
                target.Stats.TakeDamage(damage, DamageType.Physical);
                
                // Calculate knockback direction
                Vector2 direction = target.Position - Position;
                if (direction.Length() > 0)
                {
                    direction.Normalize();
                    int knockbackTiles = _random.Next(1, 3);  // 1-2 tiles
                    result.KnockbackDirection = direction;
                    result.KnockbackDistance = knockbackTiles * tileSize;
                }
                
                result.Success = true;
                result.Damage = damage;
                result.Message = $"{Name} smashes you for {damage:F0} and sends you flying!";
            }
            else
            {
                result.Message = $"{Name}'s heavy swing misses!";
            }
            
            return result;
        }
        
        private AbilityResult UseAmbush(PlayerEntity target, int tileSize)
        {
            var result = new AbilityResult
            {
                Ability = EnemyAbility.Ambush,
                User = this,
                Message = $"{Name} strikes from the shadows!"
            };
            
            float distance = Vector2.Distance(Position, target.Position) / tileSize;
            if (distance > 1.5f)
            {
                result.Message = $"{Name} stalks closer...";
                return result;
            }
            
            // Ambush is high accuracy
            if (_random.NextDouble() < Accuracy + 0.15f)
            {
                // Double damage if stealthed
                float multiplier = IsStealthed ? 2.0f : 1.0f;
                float damage = Damage * multiplier;
                target.Stats.TakeDamage(damage, DamageType.Physical);
                
                if (IsStealthed)
                {
                    result.Message = $"{Name} ambushes you for {damage:F0} CRITICAL damage!";
                    RevealFromStealth();
                }
                else
                {
                    result.Message = $"{Name} strikes for {damage:F0} damage!";
                }
                
                result.Success = true;
                result.Damage = damage;
            }
            else
            {
                RevealFromStealth();
                result.Message = $"{Name} reveals itself but misses!";
            }
            
            return result;
        }
        
        private AbilityResult UseSpawnSwarmling(int tileSize)
        {
            var result = new AbilityResult
            {
                Ability = EnemyAbility.SpawnSwarmling,
                User = this,
                Message = $"{Name} spawns a swarmling!"
            };
            
            if (SpawnCount >= MaxSpawns)
            {
                result.Message = $"{Name} can't spawn more creatures!";
                return result;
            }
            
            // Find spawn position adjacent to mother
            Point myTile = GetTilePosition(tileSize);
            Point[] offsets = { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) };
            Point spawnTile = myTile;
            
            foreach (var offset in offsets)
            {
                spawnTile = new Point(myTile.X + offset.X, myTile.Y + offset.Y);
                break;  // Just use first available for now
            }
            
            result.Success = true;
            result.SpawnPosition = new Vector2(spawnTile.X * tileSize, spawnTile.Y * tileSize);
            result.SpawnType = EnemyType.Swarmling;
            SpawnCount++;
            
            return result;
        }
        
        /// <summary>
        /// Enter stealth (for Stalker enemies)
        /// </summary>
        public void EnterStealth()
        {
            if (Type == EnemyType.Stalker)
            {
                IsStealthed = true;
                System.Diagnostics.Debug.WriteLine($">>> {Name} fades into shadows <<<");
            }
        }
        
        /// <summary>
        /// Reveal from stealth
        /// </summary>
        public void RevealFromStealth()
        {
            WasStealthed = IsStealthed;
            IsStealthed = false;
        }
        
        /// <summary>
        /// Check if this enemy can be seen (not stealthed or player has detection)
        /// </summary>
        public bool IsVisible(float playerPerception)
        {
            if (!IsStealthed) return true;
            
            // High perception can see stealthed enemies
            return playerPerception >= 8;  // PER 8+ can detect
        }
        
        /// <summary>
        /// Apply regeneration (for HiveMother)
        /// </summary>
        public void ApplyRegeneration()
        {
            if (SecondaryAbility == EnemyAbility.Regenerate)
            {
                float healAmount = MaxHealth * 0.05f;  // 5% per turn
                CurrentHealth = Math.Min(MaxHealth, CurrentHealth + healAmount);
                System.Diagnostics.Debug.WriteLine($">>> {Name} regenerates {healAmount:F0} HP <<<");
            }
        }
        
        /// <summary>
        /// Handle explosion on death (for Swarmling)
        /// Returns damage dealt if exploded
        /// </summary>
        public float OnDeath()
        {
            if (PrimaryAbility == EnemyAbility.Explode)
            {
                System.Diagnostics.Debug.WriteLine($">>> {Name} explodes! <<<");
                return 8f;  // Explosion damage
            }
            return 0f;
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
                    
                // === SPECIAL ABILITY ENEMIES ===
                
                case EnemyType.Spitter:
                    // Acid glands are valuable for dark science
                    loot.Add(new Item("mutant_meat", ItemQuality.Normal, _random.Next(1, 3)));
                    if (_random.NextDouble() < 0.5)
                    {
                        loot.Add(new Item("anomaly_shard", ItemQuality.Normal, _random.Next(1, 3)));
                    }
                    if (_random.NextDouble() < 0.3)
                    {
                        loot.Add(new Item("mutagen", ItemQuality.Normal, 1));
                    }
                    break;
                    
                case EnemyType.Psionic:
                    // Rare brain tissue and anomaly materials
                    if (_random.NextDouble() < 0.6)
                    {
                        loot.Add(new Item("brain_tissue", ItemQuality.Normal, 1));
                    }
                    if (_random.NextDouble() < 0.4)
                    {
                        loot.Add(new Item("anomaly_shard", ItemQuality.Normal, _random.Next(2, 5)));
                    }
                    if (_random.NextDouble() < 0.2)
                    {
                        loot.Add(new Item("mutagen", ItemQuality.Normal, _random.Next(1, 2)));
                    }
                    break;
                    
                case EnemyType.Brute:
                    // Heavy hitter drops meat, leather, and sometimes good gear
                    loot.Add(new Item("mutant_meat", ItemQuality.Normal, _random.Next(4, 8)));
                    loot.Add(new Item("leather", ItemQuality.Normal, _random.Next(3, 6)));
                    if (_random.NextDouble() < 0.3)
                    {
                        loot.Add(new Item("bone", ItemQuality.Normal, _random.Next(3, 6)));
                    }
                    if (_random.NextDouble() < 0.2)
                    {
                        string[] weapons = { "pipe_club", "machete" };
                        loot.Add(new Item(weapons[_random.Next(weapons.Length)]));
                    }
                    break;
                    
                case EnemyType.Stalker:
                    // Rare stealth-related drops
                    loot.Add(new Item("leather", ItemQuality.Normal, _random.Next(2, 4)));
                    if (_random.NextDouble() < 0.4)
                    {
                        loot.Add(new Item("sinew", ItemQuality.Normal, _random.Next(2, 4)));
                    }
                    if (_random.NextDouble() < 0.25)
                    {
                        loot.Add(new Item("anomaly_shard", ItemQuality.Normal, _random.Next(1, 3)));
                    }
                    if (_random.NextDouble() < 0.15)
                    {
                        loot.Add(new Item("knife_combat"));
                    }
                    break;
                    
                case EnemyType.HiveMother:
                    // Mother drops lots of organic materials
                    loot.Add(new Item("mutant_meat", ItemQuality.Normal, _random.Next(5, 10)));
                    loot.Add(new Item("bone", ItemQuality.Normal, _random.Next(4, 8)));
                    if (_random.NextDouble() < 0.5)
                    {
                        loot.Add(new Item("sinew", ItemQuality.Normal, _random.Next(3, 6)));
                    }
                    if (_random.NextDouble() < 0.4)
                    {
                        loot.Add(new Item("mutagen", ItemQuality.Normal, _random.Next(1, 3)));
                    }
                    if (_random.NextDouble() < 0.25)
                    {
                        loot.Add(new Item("brain_tissue", ItemQuality.Normal, 1));
                    }
                    break;
                    
                case EnemyType.Swarmling:
                    // Small creature, minimal drops
                    if (_random.NextDouble() < 0.4)
                    {
                        loot.Add(new Item("bone", ItemQuality.Normal, 1));
                    }
                    if (_random.NextDouble() < 0.2)
                    {
                        loot.Add(new Item("mutant_meat", ItemQuality.Normal, 1));
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
