using System.IO;
using BepInEx;
using BepInEx.Configuration;
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

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true, "Enable dynamic dialogue.");

            var dialoguePath = Path.Combine(Paths.ConfigPath, "HS2DynamicDialogue", "dialogues.es.json");
            _dialogueEngine = new DialogueEngine(dialoguePath, Logger);

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

            // HS2 maker hooks are intentionally added in the next milestone after
            // validating the exact BetterRepack R16 assemblies and event signatures.
            Logger.LogInfo("Core initialized. Waiting for verified Character Maker hooks.");
        }
    }
}
