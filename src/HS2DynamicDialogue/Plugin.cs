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
        public const string Version = "0.2.0";

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _voiceEnabled;
        private ConfigEntry<int> _voiceEveryNReactions;
        private ConfigEntry<bool> _autoPoseEnabled;
        private ConfigEntry<float> _autoPoseInterval;
        private DialogueEngine _dialogueEngine;
        private CharacterStateObserver _observer;
        private VoicePlayback _voicePlayback;
        private AutoPoseController _autoPose;
        private string _visibleLine;
        private float _hideAt;
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
                "PlayEveryNReactions",
                3,
                "Play Japanese sample audio every N dialogue reactions.");
            _autoPoseEnabled = Config.Bind(
                "Pose",
                "AutoChangePose",
                true,
                "Automatically advance the Character Maker pose.");
            _autoPoseInterval = Config.Bind(
                "Pose",
                "IntervalSeconds",
                15f,
                "Seconds between automatic pose changes (minimum 5).");

            var dialoguePath = Path.Combine(Paths.ConfigPath, "HS2DynamicDialogue", "dialogues.en.v2.json");
            _dialogueEngine = new DialogueEngine(dialoguePath, Logger);
            _observer = new CharacterStateObserver(Logger);
            _voicePlayback = new VoicePlayback(Logger);
            _autoPose = new AutoPoseController(Logger);
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
                _autoPose.Tick(_autoPoseInterval.Value);
        }

        private void OnGUI()
        {
            if (!MakerAPI.InsideAndLoaded)
                return;

            var stopLabel = _stopped ? "START" : "STOP";
            if (GUI.Button(new Rect(Screen.width - 125f, 20f, 105f, 42f), stopLabel))
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
            _stopped = false;
        }

        private void OnMakerExiting(object sender, System.EventArgs eventArgs)
        {
            _observer.Reset();
            _autoPose.Reset();
            _voicePlayback.Stop();
            _visibleLine = null;
        }

        private void OnAccessoryKindChanged(object sender, AccessorySlotEventArgs eventArgs)
        {
            _observer.PublishAccessoryChanged();
        }

        private void OnContextChanged(CharacterContext context)
        {
            var line = _dialogueEngine.Select(context);
            if (string.IsNullOrEmpty(line))
                return;

            _visibleLine = line;
            _hideAt = Time.unscaledTime + 5f;
            Logger.LogInfo("Dialogue: " + line);

            if (_voiceEnabled.Value)
                _voicePlayback.TryPlay(
                    MakerAPI.GetCharacterControl(),
                    Mathf.Max(1, _voiceEveryNReactions.Value));
        }

        private void OnPoseChanged()
        {
            var character = MakerAPI.GetCharacterControl();
            OnContextChanged(new CharacterContext
            {
                Trigger = "pose_changed",
                Personality = character == null || character.fileParam == null
                    ? "*"
                    : "personality:" + character.fileParam.personality
            });
        }

        private void StopEverything()
        {
            _visibleLine = null;
            _voicePlayback.Stop();
            _autoPose.Reset();
            Logger.LogInfo("Dynamic dialogue stopped by user.");
        }

        private void ResumeEverything()
        {
            _observer.Reset();
            _autoPose.Reset();
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
        }
    }
}
