using System.IO;
using BepInEx;
using BepInEx.Configuration;
using KKAPI.Maker;
using UnityEngine;

namespace HS2DynamicDialogue
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.antivirus25.hs2.dynamicdialogue";
        public const string Name = "HS2 Dynamic Dialogue";
        public const string Version = "0.4.0";

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _voiceEnabled;
        private ConfigEntry<int> _voiceEveryNReactions;
        private ConfigEntry<bool> _autoPoseEnabled;
        private ConfigEntry<float> _autoPoseInterval;
        private ConfigEntry<float> _poseTransition;
        private ConfigEntry<float> _dialogueCooldown;
        private DialogueEngine _dialogueEngine;
        private CharacterStateObserver _observer;
        private VoicePlayback _voicePlayback;
        private AutoPoseController _autoPose;
        private FacialExpressionController _facialExpressions;
        private string _visibleLine;
        private float _hideAt;
        private float _nextDialogueAt;
        private bool _stopped;

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true, "Enable dynamic dialogue.");
            _voiceEnabled = Config.Bind(
                "Voice",
                "UseJapaneseSampleVoice",
                true,
                "Play an existing Japanese sample voice for the current personality.");
            _voiceEveryNReactions = Config.Bind(
                "Voice",
                "PlayEveryNReactionsV2",
                1,
                "Play Japanese sample audio every N dialogue reactions.");
            _autoPoseEnabled = Config.Bind(
                "Pose",
                "AutoChangePose",
                true,
                "Automatically advance the Character Maker pose.");
            _autoPoseInterval = Config.Bind(
                "Pose",
                "IntervalSeconds",
                20f,
                "Seconds between automatic pose changes (minimum 5).");
            _poseTransition = Config.Bind(
                "Pose",
                "TransitionSeconds",
                1.5f,
                "Natural crossfade duration between Gravure animations.");
            _dialogueCooldown = Config.Bind(
                "Dialogue",
                "CooldownSeconds",
                4f,
                "Minimum time between dialogue reactions.");

            var dialoguePath = Path.Combine(Paths.ConfigPath, "HS2DynamicDialogue", "dialogues.en.v4.json");
            _dialogueEngine = new DialogueEngine(dialoguePath, Logger);
            _observer = new CharacterStateObserver(Logger);
            _voicePlayback = new VoicePlayback(this, Logger);
            _autoPose = new AutoPoseController(Logger);
            _facialExpressions = new FacialExpressionController(Logger);
            _observer.ContextChanged += OnContextChanged;
            _autoPose.PoseChanged += OnPoseChanged;

            MakerAPI.MakerFinishedLoading += OnMakerFinishedLoading;
            MakerAPI.MakerExiting += OnMakerExiting;
            AccessoriesApi.AccessoryKindChanged += OnAccessoryKindChanged;

            Logger.LogInfo(Name + " " + Version + " loaded.");
            Logger.LogInfo("Dialogue file: " + dialoguePath);
        }

        private void Start()
        {
            if (!_enabled.Value)
            {
                Logger.LogInfo("Plugin is disabled in configuration.");
                return;
            }

            Logger.LogInfo("Core initialized with verified HS2API maker hooks.");
        }

        private void Update()
        {
            if (!_enabled.Value || _stopped)
                return;

            _observer.Tick();
            _voicePlayback.Tick();
            if (_autoPoseEnabled.Value)
                _autoPose.Tick(_autoPoseInterval.Value, _poseTransition.Value);
        }

        private void OnGUI()
        {
            if (!MakerAPI.InsideAndLoaded)
                return;

            var stopLabel = _stopped ? "DIALOGUE START" : "DIALOGUE STOP";
            if (GUI.Button(new Rect((Screen.width - 170f) / 2f, 20f, 170f, 42f), stopLabel))
            {
                _stopped = !_stopped;
                if (_stopped)
                    StopEverything();
                else
                    ResumeEverything();
            }

            if (!string.IsNullOrEmpty(_visibleLine) && Time.unscaledTime < _hideAt)
            {
                const float width = 620f;
                const float height = 72f;
                var area = new Rect((Screen.width - width) / 2f, Screen.height - 130f, width, height);
                GUI.Box(area, string.Empty);
                GUI.Label(new Rect(area.x + 18f, area.y + 16f, area.width - 36f, area.height - 24f), _visibleLine);
            }
        }

        private void OnMakerFinishedLoading(object sender, System.EventArgs eventArgs)
        {
            _observer.Reset();
            _autoPose.Reset();
            _facialExpressions.Reset();
            _stopped = false;
        }

        private void OnMakerExiting(object sender, System.EventArgs eventArgs)
        {
            _observer.Reset();
            _autoPose.Reset();
            _voicePlayback.Stop();
            _facialExpressions.Reset();
            _visibleLine = null;
        }

        private void OnAccessoryKindChanged(object sender, AccessorySlotEventArgs eventArgs)
        {
            _observer.PublishAccessoryChanged();
        }

        private void OnContextChanged(CharacterContext context)
        {
            if (_stopped)
                return;

            var character = MakerAPI.GetCharacterControl();
            var timidExposed = context.Tags.Contains("timid") &&
                (context.Tags.Contains("underwear") || context.Tags.Contains("nude"));
            _autoPose.SetCoveringMode(timidExposed);
            _facialExpressions.Apply(character, context);

            if (Time.unscaledTime < _nextDialogueAt)
                return;

            var line = _dialogueEngine.Select(context);
            if (string.IsNullOrEmpty(line))
                return;

            _nextDialogueAt = Time.unscaledTime + Mathf.Max(1f, _dialogueCooldown.Value);
            _voicePlayback.Stop();

            if (_voiceEnabled.Value &&
                _voicePlayback.TryPlay(
                    character,
                    Mathf.Max(1, _voiceEveryNReactions.Value),
                    delegate { ShowLine(line); },
                    delegate { ShowLine(line); }))
            {
                return;
            }

            ShowLine(line);
        }

        private void ShowLine(string line)
        {
            if (_stopped)
                return;

            _visibleLine = line;
            _hideAt = Time.unscaledTime + 5f;
            Logger.LogInfo("Dialogue: " + line);
        }

        private void OnPoseChanged()
        {
            var character = MakerAPI.GetCharacterControl();
            OnContextChanged(CharacterStateObserver.BuildContext(
                "pose_changed",
                character));
        }

        private void StopEverything()
        {
            _visibleLine = null;
            _voicePlayback.Stop();
            _autoPose.Reset();
            _facialExpressions.Reset();
            Logger.LogInfo("Dynamic dialogue stopped by user.");
        }

        private void ResumeEverything()
        {
            _observer.Reset();
            _autoPose.Reset();
            _nextDialogueAt = 0f;
            Logger.LogInfo("Dynamic dialogue resumed by user.");
        }

        private void OnDestroy()
        {
            MakerAPI.MakerFinishedLoading -= OnMakerFinishedLoading;
            MakerAPI.MakerExiting -= OnMakerExiting;
            AccessoriesApi.AccessoryKindChanged -= OnAccessoryKindChanged;

            if (_observer != null)
                _observer.ContextChanged -= OnContextChanged;
            if (_autoPose != null)
                _autoPose.PoseChanged -= OnPoseChanged;
            if (_voicePlayback != null)
                _voicePlayback.Stop();
            if (_facialExpressions != null)
                _facialExpressions.Reset();
        }
    }
}
