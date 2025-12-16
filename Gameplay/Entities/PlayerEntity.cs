// Gameplay/Entities/PlayerEntity.cs
// Player entity with full CharacterStats integration

using Microsoft.Xna.Framework;
using System.Collections.Generic;
using MyRPG.Gameplay.World;
using MyRPG.Gameplay.Character;
using MyRPG.Gameplay.Systems;
using MyRPG.Data;

namespace MyRPG.Gameplay.Entities
{
    public class PlayerEntity
    {
        // Position and Movement
        public Vector2 Position;
        public List<Point> CurrentPath = new List<Point>();
        
        // Character Stats (replaces old Speed and StatusEffects)
        public CharacterStats Stats { get; private set; }
        
        // Backwards compatibility - expose Speed through Stats
        public float Speed => Stats?.Speed ?? 200f;
        
        // Legacy StatusEffects list - now points to Stats.StatusEffects
        public List<string> StatusEffects 
        { 
            get 
            {
                // Convert new status effects to old string format for compatibility
                var list = new List<string>();
                if (Stats != null)
                {
                    foreach (var effect in Stats.StatusEffects)
                    {
                        list.Add(effect.Type.ToString());
                    }
                }
                return list;
            }
        }
        
        // Is character initialized?
        public bool IsInitialized { get; private set; } = false;
        
        /// <summary>
        /// Initialize the player with a random character build
        /// </summary>
        public void Initialize()
        {
            // Create CharacterStats using GameServices
            Stats = new CharacterStats(
                GameServices.Mutations,
                GameServices.Traits,
                GameServices.StatusEffects
            );
            
            // Generate a random character build
            var build = GameServices.Traits.GenerateRandomBuild();
            
            // Apply the build (choose Tinker for now, can be player choice later)
            Stats.ApplyCharacterBuild(build, SciencePath.Tinker);
            
            IsInitialized = true;
            
            System.Diagnostics.Debug.WriteLine(">>> PLAYER INITIALIZED <<<");
            System.Diagnostics.Debug.WriteLine(Stats.GetStatusReport());
        }
        
        /// <summary>
        /// Initialize with a specific build and science path
        /// </summary>
        public void Initialize(CharacterBuild build, SciencePath sciencePath)
        {
            Stats = new CharacterStats(
                GameServices.Mutations,
                GameServices.Traits,
                GameServices.StatusEffects
            );
            
            Stats.ApplyCharacterBuild(build, sciencePath);
            
            IsInitialized = true;
            
            System.Diagnostics.Debug.WriteLine(">>> PLAYER INITIALIZED WITH CUSTOM BUILD <<<");
            System.Diagnostics.Debug.WriteLine(Stats.GetStatusReport());
        }

        public void Update(float deltaTime, WorldGrid grid)
        {
            if (!IsInitialized) return;
            
            // Update status effects (real-time mode)
            GameServices.StatusEffects.UpdateEffectsRealTime(Stats.StatusEffects, deltaTime);
            
            // Apply regeneration if any
            if (Stats.RegenRate > 0)
            {
                Stats.Heal(Stats.RegenRate * deltaTime);
            }
            
            // Apply damage over time effects
            float dot = GameServices.StatusEffects.GetDamageOverTime(Stats.StatusEffects);
            if (dot > 0)
            {
                Stats.TakeDamage(dot * deltaTime, DamageType.Physical);
            }

            // MOVEMENT LOGIC
            if (CurrentPath != null && CurrentPath.Count > 0)
            {
                Point nextTile = CurrentPath[0];
                Vector2 targetPos = new Vector2(nextTile.X * grid.TileSize, nextTile.Y * grid.TileSize);

                Vector2 direction = targetPos - Position;
                if (direction != Vector2.Zero) direction.Normalize();

                // Use Stats.Speed (already includes all modifiers!)
                float currentSpeed = Stats.Speed;

                Position += direction * currentSpeed * deltaTime;

                if (Vector2.Distance(Position, targetPos) < 4f)
                {
                    Position = targetPos;
                    CurrentPath.RemoveAt(0);
                    CheckTileInteraction(grid, nextTile);
                }
            }
        }

        private void CheckTileInteraction(WorldGrid grid, Point tilePos)
        {
            Tile tile = grid.Tiles[tilePos.X, tilePos.Y];

            if (tile.Type == TileType.Water)
            {
                // Apply Wet status using new system
                if (!GameServices.StatusEffects.HasEffect(Stats.StatusEffects, StatusEffectType.Wet))
                {
                    GameServices.StatusEffects.ApplyEffect(
                        Stats.StatusEffects, 
                        StatusEffectType.Wet, 
                        10f,  // 10 seconds duration
                        false, // real-time mode
                        "WaterTile"
                    );
                    System.Diagnostics.Debug.WriteLine(">>> ENTERED WATER! STATUS: WET <<<");
                }
            }
            else if (tile.Type == TileType.Dirt)
            {
                // Remove Wet when on land
                if (GameServices.StatusEffects.HasEffect(Stats.StatusEffects, StatusEffectType.Wet))
                {
                    GameServices.StatusEffects.RemoveEffect(Stats.StatusEffects, StatusEffectType.Wet);
                    System.Diagnostics.Debug.WriteLine(">>> BACK ON LAND. DRYING OFF. <<<");
                }
            }
        }
        
        /// <summary>
        /// Apply lightning damage - triggers chain reaction if Wet!
        /// </summary>
        public void ApplyLightning(float damage)
        {
            // This will trigger the chain: Wet + Electrified = Stunned
            GameServices.StatusEffects.ApplyEffect(
                Stats.StatusEffects,
                StatusEffectType.Electrified,
                3f,
                false,
                "Lightning"
            );
            
            Stats.TakeDamage(damage, DamageType.Electric);
        }
        
        /// <summary>
        /// Check if player has a specific status effect
        /// </summary>
        public bool HasStatus(StatusEffectType type)
        {
            return GameServices.StatusEffects.HasEffect(Stats.StatusEffects, type);
        }
        
        /// <summary>
        /// Check if player is alive
        /// </summary>
        public bool IsAlive
        {
            get
            {
                if (Stats == null) return false;
                if (Stats.CurrentHealth <= 0) return false;
                if (Stats.Body != null && !Stats.Body.IsAlive) return false;
                return true;
            }
        }
    }
}
