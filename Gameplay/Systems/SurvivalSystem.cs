// Gameplay/Systems/SurvivalSystem.cs
// Manages survival needs: Hunger, Thirst, Rest, Temperature

using System;
using System.Collections.Generic;
using MyRPG.Data;

namespace MyRPG.Gameplay.Systems
{
    // ============================================
    // SURVIVAL NEEDS
    // ============================================
    
    public class SurvivalNeeds
    {
        // Current values (0-100)
        public float Hunger { get; private set; } = 100f;      // 100 = full, 0 = starving
        public float Thirst { get; private set; } = 100f;      // 100 = hydrated, 0 = dehydrated
        public float Rest { get; private set; } = 100f;        // 100 = rested, 0 = exhausted
        public float Temperature { get; private set; } = 50f;  // 50 = comfortable, 0 = freezing, 100 = overheating
        
        // Ideal temperature range
        public const float TEMP_COMFORTABLE_MIN = 35f;
        public const float TEMP_COMFORTABLE_MAX = 65f;
        
        // Thresholds
        public const float THRESHOLD_SATISFIED = 75f;
        public const float THRESHOLD_PECKISH = 50f;
        public const float THRESHOLD_HUNGRY = 25f;
        public const float THRESHOLD_CRITICAL = 10f;
        
        // Drain rates (per real second in exploration, per turn in combat)
        private float _hungerDrainRate = 0.5f;     // ~3.3 minutes to go from 100 to 0
        private float _thirstDrainRate = 0.8f;     // ~2 minutes to go from 100 to 0
        private float _restDrainRate = 0.3f;       // ~5.5 minutes to go from 100 to 0
        
        // Modifiers from traits/mutations
        public float HungerRateModifier { get; set; } = 1.0f;
        public float ThirstRateModifier { get; set; } = 1.0f;
        public float RestRateModifier { get; set; } = 1.0f;
        
        // Status
        public NeedLevel HungerLevel => GetNeedLevel(Hunger);
        public NeedLevel ThirstLevel => GetNeedLevel(Thirst);
        public NeedLevel RestLevel => GetNeedLevel(Rest);
        public TemperatureStatus TempStatus => GetTempStatus(Temperature);
        
        // Events
        public event Action<NeedType, NeedLevel> OnNeedLevelChanged;
        public event Action<NeedType> OnNeedCritical;
        
        // ============================================
        // UPDATE
        // ============================================
        
        /// <summary>
        /// Update needs in real-time (exploration mode)
        /// </summary>
        public void UpdateRealTime(float deltaTime, float ambientTemp = 50f)
        {
            // Store old levels
            var oldHunger = HungerLevel;
            var oldThirst = ThirstLevel;
            var oldRest = RestLevel;
            
            // Drain needs
            Hunger = Math.Max(0, Hunger - _hungerDrainRate * HungerRateModifier * deltaTime);
            Thirst = Math.Max(0, Thirst - _thirstDrainRate * ThirstRateModifier * deltaTime);
            Rest = Math.Max(0, Rest - _restDrainRate * RestRateModifier * deltaTime);
            
            // Temperature moves toward ambient
            float tempDiff = ambientTemp - Temperature;
            Temperature += tempDiff * 0.1f * deltaTime;
            Temperature = Math.Clamp(Temperature, 0f, 100f);
            
            // Check for level changes
            CheckLevelChange(NeedType.Hunger, oldHunger, HungerLevel);
            CheckLevelChange(NeedType.Thirst, oldThirst, ThirstLevel);
            CheckLevelChange(NeedType.Rest, oldRest, RestLevel);
        }
        
        /// <summary>
        /// Update needs per combat turn
        /// </summary>
        public void UpdateTurnBased()
        {
            // Much smaller drain per turn
            Hunger = Math.Max(0, Hunger - 0.5f * HungerRateModifier);
            Thirst = Math.Max(0, Thirst - 1.0f * ThirstRateModifier);
            // Rest doesn't drain much in combat (adrenaline)
        }
        
        private void CheckLevelChange(NeedType type, NeedLevel oldLevel, NeedLevel newLevel)
        {
            if (oldLevel != newLevel)
            {
                OnNeedLevelChanged?.Invoke(type, newLevel);
                
                if (newLevel == NeedLevel.Critical)
                {
                    OnNeedCritical?.Invoke(type);
                    System.Diagnostics.Debug.WriteLine($">>> CRITICAL: {type} is critically low! <<<");
                }
            }
        }
        
        // ============================================
        // RESTORATION
        // ============================================
        
        public void RestoreHunger(float amount)
        {
            Hunger = Math.Min(100f, Hunger + amount);
            System.Diagnostics.Debug.WriteLine($">>> Hunger restored by {amount:F0} (now {Hunger:F0}/100) <<<");
        }
        
        public void RestoreThirst(float amount)
        {
            Thirst = Math.Min(100f, Thirst + amount);
            System.Diagnostics.Debug.WriteLine($">>> Thirst restored by {amount:F0} (now {Thirst:F0}/100) <<<");
        }
        
        public void RestoreRest(float amount)
        {
            Rest = Math.Min(100f, Rest + amount);
            System.Diagnostics.Debug.WriteLine($">>> Rest restored by {amount:F0} (now {Rest:F0}/100) <<<");
        }
        
        public void SetTemperature(float temp)
        {
            Temperature = Math.Clamp(temp, 0f, 100f);
        }
        
        // ============================================
        // STAT PENALTIES
        // ============================================
        
        /// <summary>
        /// Get speed modifier based on needs (1.0 = normal)
        /// </summary>
        public float GetSpeedModifier()
        {
            float modifier = 1.0f;
            
            // Hunger penalties
            modifier *= HungerLevel switch
            {
                NeedLevel.Hungry => 0.9f,
                NeedLevel.Starving => 0.75f,
                NeedLevel.Critical => 0.5f,
                _ => 1.0f
            };
            
            // Rest penalties
            modifier *= RestLevel switch
            {
                NeedLevel.Hungry => 0.95f,      // "Tired"
                NeedLevel.Starving => 0.8f,     // "Exhausted"
                NeedLevel.Critical => 0.6f,     // "Barely standing"
                _ => 1.0f
            };
            
            // Temperature penalties
            modifier *= TempStatus switch
            {
                TemperatureStatus.Cold => 0.9f,
                TemperatureStatus.Freezing => 0.7f,
                TemperatureStatus.Hot => 0.95f,
                TemperatureStatus.Overheating => 0.8f,
                _ => 1.0f
            };
            
            return modifier;
        }
        
        /// <summary>
        /// Get damage modifier based on needs (1.0 = normal)
        /// </summary>
        public float GetDamageModifier()
        {
            float modifier = 1.0f;
            
            // Hunger affects damage output
            modifier *= HungerLevel switch
            {
                NeedLevel.Hungry => 0.95f,
                NeedLevel.Starving => 0.8f,
                NeedLevel.Critical => 0.6f,
                _ => 1.0f
            };
            
            // Exhaustion affects combat
            modifier *= RestLevel switch
            {
                NeedLevel.Starving => 0.9f,
                NeedLevel.Critical => 0.7f,
                _ => 1.0f
            };
            
            return modifier;
        }
        
        /// <summary>
        /// Get accuracy modifier based on needs (1.0 = normal)
        /// </summary>
        public float GetAccuracyModifier()
        {
            float modifier = 1.0f;
            
            // Thirst affects concentration
            modifier *= ThirstLevel switch
            {
                NeedLevel.Hungry => 0.95f,
                NeedLevel.Starving => 0.85f,
                NeedLevel.Critical => 0.7f,
                _ => 1.0f
            };
            
            // Exhaustion affects accuracy a lot
            modifier *= RestLevel switch
            {
                NeedLevel.Hungry => 0.9f,
                NeedLevel.Starving => 0.75f,
                NeedLevel.Critical => 0.5f,
                _ => 1.0f
            };
            
            return modifier;
        }
        
        /// <summary>
        /// Get health drain per second from survival needs
        /// </summary>
        public float GetHealthDrain()
        {
            float drain = 0f;
            
            // Critical hunger = slow health drain
            if (HungerLevel == NeedLevel.Critical)
                drain += 0.5f;
            
            // Critical thirst = faster health drain
            if (ThirstLevel == NeedLevel.Critical)
                drain += 1.0f;
            
            // Extreme temperature
            if (TempStatus == TemperatureStatus.Freezing)
                drain += 0.75f;
            if (TempStatus == TemperatureStatus.Overheating)
                drain += 0.5f;
            
            return drain;
        }
        
        /// <summary>
        /// Get list of active survival debuffs for display
        /// </summary>
        public List<string> GetActiveDebuffs()
        {
            var debuffs = new List<string>();
            
            if (HungerLevel == NeedLevel.Hungry) debuffs.Add("Hungry");
            else if (HungerLevel == NeedLevel.Starving) debuffs.Add("Starving!");
            else if (HungerLevel == NeedLevel.Critical) debuffs.Add("STARVING!");
            
            if (ThirstLevel == NeedLevel.Hungry) debuffs.Add("Thirsty");
            else if (ThirstLevel == NeedLevel.Starving) debuffs.Add("Dehydrated!");
            else if (ThirstLevel == NeedLevel.Critical) debuffs.Add("DEHYDRATED!");
            
            if (RestLevel == NeedLevel.Hungry) debuffs.Add("Tired");
            else if (RestLevel == NeedLevel.Starving) debuffs.Add("Exhausted!");
            else if (RestLevel == NeedLevel.Critical) debuffs.Add("EXHAUSTED!");
            
            if (TempStatus == TemperatureStatus.Cold) debuffs.Add("Cold");
            else if (TempStatus == TemperatureStatus.Freezing) debuffs.Add("Freezing!");
            else if (TempStatus == TemperatureStatus.Hot) debuffs.Add("Hot");
            else if (TempStatus == TemperatureStatus.Overheating) debuffs.Add("Overheating!");
            
            return debuffs;
        }
        
        // ============================================
        // HELPERS
        // ============================================
        
        private NeedLevel GetNeedLevel(float value)
        {
            if (value >= THRESHOLD_SATISFIED) return NeedLevel.Satisfied;
            if (value >= THRESHOLD_PECKISH) return NeedLevel.Peckish;
            if (value >= THRESHOLD_HUNGRY) return NeedLevel.Hungry;
            if (value >= THRESHOLD_CRITICAL) return NeedLevel.Starving;
            return NeedLevel.Critical;
        }
        
        private TemperatureStatus GetTempStatus(float temp)
        {
            if (temp < 15f) return TemperatureStatus.Freezing;
            if (temp < TEMP_COMFORTABLE_MIN) return TemperatureStatus.Cold;
            if (temp > 85f) return TemperatureStatus.Overheating;
            if (temp > TEMP_COMFORTABLE_MAX) return TemperatureStatus.Hot;
            return TemperatureStatus.Comfortable;
        }
        
        // ============================================
        // DEBUG
        // ============================================
        
        public string GetStatusReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== SURVIVAL NEEDS ===");
            report.AppendLine($"Hunger: {Hunger:F0}/100 [{HungerLevel}]");
            report.AppendLine($"Thirst: {Thirst:F0}/100 [{ThirstLevel}]");
            report.AppendLine($"Rest:   {Rest:F0}/100 [{RestLevel}]");
            report.AppendLine($"Temp:   {Temperature:F0}/100 [{TempStatus}]");
            report.AppendLine();
            report.AppendLine("--- Modifiers ---");
            report.AppendLine($"Speed:    {GetSpeedModifier():P0}");
            report.AppendLine($"Damage:   {GetDamageModifier():P0}");
            report.AppendLine($"Accuracy: {GetAccuracyModifier():P0}");
            
            var debuffs = GetActiveDebuffs();
            if (debuffs.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("--- Active Debuffs ---");
                foreach (var d in debuffs)
                    report.AppendLine($"  • {d}");
            }
            
            return report.ToString();
        }
    }
    
    // ============================================
    // TEMPERATURE STATUS
    // ============================================
    
    public enum TemperatureStatus
    {
        Freezing,       // < 15 - Taking damage
        Cold,           // 15-35 - Slowed
        Comfortable,    // 35-65 - No effect
        Hot,            // 65-85 - Minor slow
        Overheating     // > 85 - Taking damage
    }
    
    // ============================================
    // SURVIVAL SYSTEM (manages environment)
    // ============================================
    
    public class SurvivalSystem
    {
        // Time tracking
        public float GameHour { get; private set; } = 8f; // Start at 8:00 AM
        public int GameDay { get; private set; } = 1;
        public Season CurrentSeason { get; private set; } = Season.Spring;
        public TimeOfDay CurrentTimeOfDay => GetTimeOfDay(GameHour);
        
        // Time speed (game hours per real second)
        public float TimeSpeed { get; set; } = 0.05f; // ~20 real seconds = 1 game hour
        
        // World temperature
        public float AmbientTemperature => CalculateAmbientTemp();
        
        // Season duration in game days
        public const int DAYS_PER_SEASON = 15;
        
        // Base temperatures by season
        private readonly Dictionary<Season, (float day, float night)> _seasonTemps = new Dictionary<Season, (float, float)>
        {
            { Season.Spring, (55f, 40f) },
            { Season.Summer, (75f, 55f) },
            { Season.Autumn, (50f, 35f) },
            { Season.Winter, (30f, 15f) }
        };
        
        // Events
        public event Action<TimeOfDay> OnTimeOfDayChanged;
        public event Action<Season> OnSeasonChanged;
        public event Action OnNewDay;
        
        private TimeOfDay _lastTimeOfDay;
        
        public SurvivalSystem()
        {
            _lastTimeOfDay = CurrentTimeOfDay;
        }
        
        // ============================================
        // UPDATE
        // ============================================
        
        public void Update(float deltaTime)
        {
            float oldHour = GameHour;
            
            GameHour += TimeSpeed * deltaTime;
            
            // New day
            if (GameHour >= 24f)
            {
                GameHour -= 24f;
                GameDay++;
                OnNewDay?.Invoke();
                
                // Check for season change
                if (GameDay > DAYS_PER_SEASON)
                {
                    GameDay = 1;
                    CurrentSeason = (Season)(((int)CurrentSeason + 1) % 4);
                    OnSeasonChanged?.Invoke(CurrentSeason);
                    System.Diagnostics.Debug.WriteLine($">>> SEASON CHANGED: Now {CurrentSeason} <<<");
                }
            }
            
            // Check time of day change
            var newTimeOfDay = CurrentTimeOfDay;
            if (newTimeOfDay != _lastTimeOfDay)
            {
                _lastTimeOfDay = newTimeOfDay;
                OnTimeOfDayChanged?.Invoke(newTimeOfDay);
                System.Diagnostics.Debug.WriteLine($">>> Time of day: {newTimeOfDay} <<<");
            }
        }
        
        // ============================================
        // TEMPERATURE
        // ============================================
        
        private float CalculateAmbientTemp()
        {
            var (dayTemp, nightTemp) = _seasonTemps[CurrentSeason];
            
            // Interpolate based on time of day
            float timeOfDayFactor = GetTimeOfDayTempFactor();
            
            return nightTemp + (dayTemp - nightTemp) * timeOfDayFactor;
        }
        
        private float GetTimeOfDayTempFactor()
        {
            // 0 = coldest (night), 1 = warmest (midday)
            // Peak warmth at 14:00, coldest at 4:00
            float normalizedHour = (GameHour - 4f) / 24f;
            if (normalizedHour < 0) normalizedHour += 1f;
            
            // Use sine wave for smooth transition
            return (float)(Math.Sin((normalizedHour - 0.25f) * Math.PI * 2) + 1) / 2f;
        }
        
        // ============================================
        // TIME HELPERS
        // ============================================
        
        private TimeOfDay GetTimeOfDay(float hour)
        {
            if (hour >= 5 && hour < 7) return TimeOfDay.Dawn;
            if (hour >= 7 && hour < 12) return TimeOfDay.Morning;
            if (hour >= 12 && hour < 17) return TimeOfDay.Afternoon;
            if (hour >= 17 && hour < 20) return TimeOfDay.Evening;
            if (hour >= 20 && hour < 21) return TimeOfDay.Dusk;
            return TimeOfDay.Night;
        }
        
        public bool IsNight => CurrentTimeOfDay == TimeOfDay.Night || CurrentTimeOfDay == TimeOfDay.Dusk;
        public bool IsDay => !IsNight;
        
        public string GetTimeString()
        {
            int hours = (int)GameHour;
            int minutes = (int)((GameHour - hours) * 60);
            return $"{hours:D2}:{minutes:D2}";
        }
        
        public string GetDateString()
        {
            return $"Day {GameDay}, {CurrentSeason}";
        }
        
        // ============================================
        // DEBUG
        // ============================================
        
        public string GetStatusReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== WORLD TIME ===");
            report.AppendLine($"Time: {GetTimeString()} ({CurrentTimeOfDay})");
            report.AppendLine($"Date: {GetDateString()}");
            report.AppendLine($"Temperature: {AmbientTemperature:F1}°");
            report.AppendLine($"Night: {IsNight}");
            
            return report.ToString();
        }
    }
}
