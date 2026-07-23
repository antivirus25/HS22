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
        public const string Version = "0.1.0";

        private ConfigEntry<bool> _enabled;
        private DialogueEngine _dialogueEngine;
        private CharacterStateObserver _observer;
        private string _visibleLine;
        private float _hideAt;

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true, "Enable dynamic dialogue.");

            var dialoguePath = Path.Combine(Paths.ConfigPath, "HS2DynamicDialogue", "dialogues.es.json");
            _dialogueEngine = new DialogueEngine(dialoguePath, Logger);
            _observer = new CharacterStateObserver(Logger);
            _observer.ContextChanged += OnContextChanged;

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
            if (!_enabled.Value)
                return;

            _observer.Tick();
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_visibleLine) || Time.unscaledTime >= _hideAt)
                return;

            const float width = 620f;
            const float height = 72f;
            var area = new Rect((Screen.width - width) / 2f, Screen.height - 130f, width, height);
            GUI.Box(area, string.Empty);
            GUI.Label(new Rect(area.x + 18f, area.y + 16f, area.width - 36f, area.height - 24f), _visibleLine);
        }

        private void OnMakerFinishedLoading(object sender, System.EventArgs eventArgs)
        {
            _observer.Reset();
        }

        private void OnMakerExiting(object sender, System.EventArgs eventArgs)
        {
            _observer.Reset();
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
        }

        private void OnDestroy()
        {
            MakerAPI.MakerFinishedLoading -= OnMakerFinishedLoading;
            MakerAPI.MakerExiting -= OnMakerExiting;
            AccessoriesApi.AccessoryKindChanged -= OnAccessoryKindChanged;

            if (_observer != null)
                _observer.ContextChanged -= OnContextChanged;
        }
    }
}
