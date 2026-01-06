// Gameplay/Systems/TutorialSystem.cs
// Tutorial hint system for guiding new players through game mechanics

using System;
using System.Collections.Generic;
using System.Linq;

namespace MyRPG.Gameplay.Systems
{
    // ============================================
    // ENUMS
    // ============================================

    public enum TutorialTrigger
    {
        OnGameStart,        // First frame of gameplay
        OnFirstCombat,      // Combat starts
        OnFirstPickup,      // Item picked up
        OnFirstCraft,       // Item crafted
        OnFirstBuild,       // Structure placed
        OnFirstTrade,       // Trading opened
        OnMutationAvailable,// Mutation selection triggered
        OnQuestComplete,    // Any quest completed
        OnTimePassed,       // Time threshold reached
        OnQuestSpecific     // Specific quest completed
    }

    public enum HintStyle
    {
        Modal,  // Pauses game, requires dismiss
        Toast   // Corner notification, auto-dismiss
    }

    // ============================================
    // TUTORIAL HINT DEFINITION
    // ============================================

    public class TutorialHint
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public TutorialTrigger Trigger { get; set; }
        public HintStyle Style { get; set; }
        public string TriggerData { get; set; }  // Quest ID, time threshold, etc.
        public int Priority { get; set; } = 0;   // Higher = show first

        public TutorialHint(string id, string title, string description, TutorialTrigger trigger, HintStyle style)
        {
            Id = id;
            Title = title;
            Description = description;
            Trigger = trigger;
            Style = style;
        }
    }

    // ============================================
    // TUTORIAL SYSTEM
    // ============================================

    public class TutorialSystem
    {
        // Hint definitions
        private Dictionary<string, TutorialHint> _hints = new Dictionary<string, TutorialHint>();

        // State tracking
        private HashSet<string> _shownHints = new HashSet<string>();
        private Queue<TutorialHint> _pendingHints = new Queue<TutorialHint>();
        private TutorialHint _currentHint = null;
        private float _toastTimer = 0f;

        // Settings
        public bool TutorialsEnabled { get; set; } = true;

        // Constants
        private const float TOAST_DURATION = 5f;

        // Events
        public event Action<TutorialHint> OnHintShown;
        public event Action<TutorialHint> OnHintDismissed;

        // Properties
        public TutorialHint CurrentHint => _currentHint;
        public bool HasActiveHint => _currentHint != null;
        public bool IsModalActive => _currentHint != null && _currentHint.Style == HintStyle.Modal;
        public float ToastTimer => _toastTimer;
        public float ToastDuration => TOAST_DURATION;

        public TutorialSystem()
        {
            InitializeHints();
        }

        // ============================================
        // HINT DEFINITIONS
        // ============================================

        private void InitializeHints()
        {
            // ==================
            // MODAL HINTS (Critical - pause game)
            // ==================

            AddHint(new TutorialHint(
                "hint_movement",
                "MOVEMENT",
                "Welcome to the Wasteland!\n\n" +
                "Use WASD to pan the camera around the world.\n\n" +
                "Click on any tile to move your character there. A path will be calculated automatically.\n\n" +
                "Press Q to zoom out, E to zoom in.",
                TutorialTrigger.OnGameStart,
                HintStyle.Modal
            ) { Priority = 100 });

            AddHint(new TutorialHint(
                "hint_combat",
                "COMBAT BASICS",
                "You've entered combat!\n\n" +
                "Combat is turn-based. Each turn you have Action Points (AP) to spend on attacks and abilities.\n\n" +
                "Press SPACE to end your turn when you're done.\n\n" +
                "Keep an eye on your health - retreat if things get dangerous!",
                TutorialTrigger.OnFirstCombat,
                HintStyle.Modal
            ) { Priority = 90 });

            AddHint(new TutorialHint(
                "hint_combat_targeting",
                "TARGETING",
                "Use TAB to cycle through nearby enemies.\n\n" +
                "Click on an enemy to attack them (costs 2 AP).\n\n" +
                "The red highlight shows your current target. Position matters - try to flank enemies for better chances!",
                TutorialTrigger.OnFirstCombat,
                HintStyle.Modal
            ) { Priority = 89, TriggerData = "after_combat_basics" });

            AddHint(new TutorialHint(
                "hint_inventory",
                "INVENTORY",
                "You picked up an item!\n\n" +
                "Press I to open your inventory. From there you can equip weapons and armor, use consumables, and manage your gear.\n\n" +
                "Press G near items on the ground to pick them up.\n\n" +
                "Watch your carry weight - drop items you don't need!",
                TutorialTrigger.OnFirstPickup,
                HintStyle.Modal
            ) { Priority = 85 });

            AddHint(new TutorialHint(
                "hint_mutations",
                "MUTATIONS",
                "You can choose a new mutation!\n\n" +
                "Press M to open the mutation selection screen.\n\n" +
                "Mutations are PERMANENT and define your character's evolution. Choose wisely!\n\n" +
                "Every 4 levels, you get to pick freely from all available mutations.",
                TutorialTrigger.OnMutationAvailable,
                HintStyle.Modal
            ) { Priority = 80 });

            // ==================
            // TOAST HINTS (Minor - non-blocking)
            // ==================

            AddHint(new TutorialHint(
                "hint_survival",
                "SURVIVAL NEEDS",
                "Press H to view your survival needs.\n\nManage your Hunger, Thirst, and Rest to stay effective in the wasteland.",
                TutorialTrigger.OnTimePassed,
                HintStyle.Toast
            ) { Priority = 50, TriggerData = "120" });  // 2 minutes

            AddHint(new TutorialHint(
                "hint_building",
                "BUILDING",
                "Press B to open the build menu.\n\nConstruct a base to store items, rest safely, and craft advanced gear.",
                TutorialTrigger.OnQuestSpecific,
                HintStyle.Toast
            ) { Priority = 45, TriggerData = "main_03_survival" });

            AddHint(new TutorialHint(
                "hint_crafting",
                "CRAFTING",
                "Press C near a workstation to craft items.\n\nDifferent stations unlock different recipes.",
                TutorialTrigger.OnFirstCraft,
                HintStyle.Toast
            ) { Priority = 40 });

            AddHint(new TutorialHint(
                "hint_research",
                "RESEARCH",
                "Press R to open the research tree.\n\nUnlock new recipes, structures, and abilities through research.",
                TutorialTrigger.OnQuestSpecific,
                HintStyle.Toast
            ) { Priority = 35, TriggerData = "main_05_homestead" });

            AddHint(new TutorialHint(
                "hint_trading",
                "TRADING",
                "Press T near an NPC to trade.\n\nMerchants offer different goods based on their faction.",
                TutorialTrigger.OnFirstTrade,
                HintStyle.Toast
            ) { Priority = 30 });

            AddHint(new TutorialHint(
                "hint_quests",
                "QUEST LOG",
                "Press J to view your quest log.\n\nTrack active quests and see available rewards.",
                TutorialTrigger.OnQuestComplete,
                HintStyle.Toast
            ) { Priority = 25 });

            AddHint(new TutorialHint(
                "hint_save",
                "SAVING",
                "Press F5 to quick save, F9 to quick load.\n\nSave often - the wasteland is unforgiving!",
                TutorialTrigger.OnTimePassed,
                HintStyle.Toast
            ) { Priority = 20, TriggerData = "300" });  // 5 minutes

            System.Diagnostics.Debug.WriteLine($">>> TutorialSystem: Initialized {_hints.Count} hints <<<");
        }

        private void AddHint(TutorialHint hint)
        {
            _hints[hint.Id] = hint;
        }

        // ============================================
        // TRIGGER METHODS
        // ============================================

        /// <summary>
        /// Try to trigger hints for a specific event
        /// </summary>
        public void TryTrigger(TutorialTrigger trigger, string data = null)
        {
            if (!TutorialsEnabled) return;

            var matchingHints = _hints.Values
                .Where(h => h.Trigger == trigger)
                .Where(h => !_shownHints.Contains(h.Id))
                .Where(h => MatchesTriggerData(h, data))
                .OrderByDescending(h => h.Priority)
                .ToList();

            foreach (var hint in matchingHints)
            {
                // Special case: combat_targeting comes after combat_basics
                if (hint.Id == "hint_combat_targeting" && !_shownHints.Contains("hint_combat"))
                {
                    continue;
                }

                QueueHint(hint);
            }

            // Show next hint if none active
            if (_currentHint == null)
            {
                ShowNextHint();
            }
        }

        /// <summary>
        /// Trigger time-based hints
        /// </summary>
        public void UpdateTimeTriggers(float totalGameTime)
        {
            if (!TutorialsEnabled) return;

            foreach (var hint in _hints.Values)
            {
                if (hint.Trigger != TutorialTrigger.OnTimePassed) continue;
                if (_shownHints.Contains(hint.Id)) continue;

                if (float.TryParse(hint.TriggerData, out float threshold))
                {
                    if (totalGameTime >= threshold)
                    {
                        QueueHint(hint);
                    }
                }
            }

            if (_currentHint == null)
            {
                ShowNextHint();
            }
        }

        private bool MatchesTriggerData(TutorialHint hint, string data)
        {
            // No data requirement
            if (string.IsNullOrEmpty(hint.TriggerData)) return true;

            // Time-based handled separately
            if (hint.Trigger == TutorialTrigger.OnTimePassed) return false;

            // Quest-specific check
            if (hint.Trigger == TutorialTrigger.OnQuestSpecific)
            {
                return hint.TriggerData == data;
            }

            return true;
        }

        private void QueueHint(TutorialHint hint)
        {
            if (_shownHints.Contains(hint.Id)) return;
            if (_pendingHints.Any(h => h.Id == hint.Id)) return;

            _pendingHints.Enqueue(hint);
            System.Diagnostics.Debug.WriteLine($">>> Tutorial queued: {hint.Id} <<<");
        }

        private void ShowNextHint()
        {
            if (_pendingHints.Count == 0) return;

            _currentHint = _pendingHints.Dequeue();
            _shownHints.Add(_currentHint.Id);

            if (_currentHint.Style == HintStyle.Toast)
            {
                _toastTimer = TOAST_DURATION;
            }

            OnHintShown?.Invoke(_currentHint);
            System.Diagnostics.Debug.WriteLine($">>> Tutorial shown: {_currentHint.Id} <<<");
        }

        // ============================================
        // UPDATE & DISMISS
        // ============================================

        /// <summary>
        /// Update toast timer
        /// </summary>
        public void Update(float deltaTime)
        {
            if (_currentHint == null) return;

            if (_currentHint.Style == HintStyle.Toast)
            {
                _toastTimer -= deltaTime;
                if (_toastTimer <= 0)
                {
                    DismissCurrentHint();
                }
            }
        }

        /// <summary>
        /// Dismiss the current hint
        /// </summary>
        public void DismissCurrentHint()
        {
            if (_currentHint == null) return;

            var dismissed = _currentHint;
            _currentHint = null;
            _toastTimer = 0f;

            OnHintDismissed?.Invoke(dismissed);
            System.Diagnostics.Debug.WriteLine($">>> Tutorial dismissed: {dismissed.Id} <<<");

            // Show next queued hint
            ShowNextHint();
        }

        /// <summary>
        /// Disable all tutorials
        /// </summary>
        public void DisableAllTutorials()
        {
            TutorialsEnabled = false;
            _currentHint = null;
            _pendingHints.Clear();
            System.Diagnostics.Debug.WriteLine(">>> Tutorials disabled <<<");
        }

        // ============================================
        // SAVE/LOAD SUPPORT
        // ============================================

        public List<string> GetShownHintIds()
        {
            return _shownHints.ToList();
        }

        public void RestoreShownHints(List<string> hintIds, bool tutorialsEnabled)
        {
            _shownHints = new HashSet<string>(hintIds ?? new List<string>());
            TutorialsEnabled = tutorialsEnabled;
        }

        /// <summary>
        /// Reset for new game
        /// </summary>
        public void Reset()
        {
            _shownHints.Clear();
            _pendingHints.Clear();
            _currentHint = null;
            _toastTimer = 0f;
            TutorialsEnabled = true;
            System.Diagnostics.Debug.WriteLine(">>> TutorialSystem RESET <<<");
        }

        // ============================================
        // QUERY METHODS
        // ============================================

        public bool HasShownHint(string hintId)
        {
            return _shownHints.Contains(hintId);
        }

        public int GetShownCount()
        {
            return _shownHints.Count;
        }

        public int GetTotalCount()
        {
            return _hints.Count;
        }
    }
}
