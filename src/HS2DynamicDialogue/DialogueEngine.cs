using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using BepInEx.Logging;

namespace HS2DynamicDialogue
{
    public sealed class DialogueEngine
    {
        private readonly string _databasePath;
        private readonly ManualLogSource _log;
        private readonly System.Random _random = new System.Random();
        private readonly Queue<string> _recentLines = new Queue<string>();
        private DialogueDatabase _database = new DialogueDatabase();

        public DialogueEngine(string databasePath, ManualLogSource log)
        {
            _databasePath = databasePath;
            _log = log;
            EnsureDefaultDatabase();
            Reload();
        }

        private void EnsureDefaultDatabase()
        {
            if (File.Exists(_databasePath))
                return;

            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("HS2DynamicDialogue.dialogues.en.json"))
            {
                if (stream == null)
                {
                    _log.LogWarning("Embedded dialogue database was not found.");
                    return;
                }

                using (var output = File.Create(_databasePath))
                    stream.CopyTo(output);
            }
        }

        public void Reload()
        {
            if (!File.Exists(_databasePath))
            {
                _log.LogWarning("Dialogue database not found: " + _databasePath);
                return;
            }

            try
            {
                var json = File.ReadAllText(_databasePath);
                var serializer = new DataContractJsonSerializer(typeof(DialogueDatabase));
                using (var input = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    _database = serializer.ReadObject(input) as DialogueDatabase;

                if (_database == null || _database.rules == null || _database.rules.Count == 0)
                {
                    _log.LogWarning("Dialogue file contained no usable rules; loading built-in English defaults.");
                    _database = CreateFallbackDatabase();
                }

                _log.LogInfo("Loaded " + _database.rules.Count + " dialogue rules.");
            }
            catch (Exception exception)
            {
                _log.LogError("Could not load dialogue database: " + exception);
                _database = CreateFallbackDatabase();
                _log.LogInfo("Loaded " + _database.rules.Count + " built-in fallback rules.");
            }
        }

        private static DialogueDatabase CreateFallbackDatabase()
        {
            var database = new DialogueDatabase();
            database.rules.Add(new DialogueRule
            {
                id = "clothing-change-default",
                trigger = "clothing_changed",
                personality = "*",
                priority = 1,
                lines = new List<string>
                {
                    "This outfit changes my whole look.",
                    "Give me a moment to see how this feels.",
                    "That is an interesting choice. What do you think?"
                }
            });
            database.rules.Add(new DialogueRule
            {
                id = "accessory-change-default",
                trigger = "accessory_changed",
                personality = "*",
                priority = 1,
                lines = new List<string>
                {
                    "That little detail makes a surprising difference.",
                    "I think this accessory suits me.",
                    "It gives the outfit a different personality."
                }
            });
            database.rules.Add(new DialogueRule
            {
                id = "pose-change-default",
                trigger = "pose_changed",
                personality = "*",
                priority = 1,
                lines = new List<string>
                {
                    "How does this pose look?",
                    "This angle shows the outfit more clearly.",
                    "Let me try something a little more relaxed."
                }
            });
            return database;
        }

        public string Select(CharacterContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            var matches = _database.rules
                .Where(rule => Matches(rule, context))
                .OrderByDescending(rule => rule.priority)
                .ToList();

            if (matches.Count == 0)
                return null;

            var bestPriority = matches[0].priority;
            var candidates = matches
                .Where(rule => rule.priority == bestPriority)
                .SelectMany(rule => rule.lines)
                .Where(line => !string.IsNullOrWhiteSpace(line) && !_recentLines.Contains(line))
                .Distinct()
                .ToList();

            if (candidates.Count == 0)
                candidates = matches.SelectMany(rule => rule.lines).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

            if (candidates.Count == 0)
                return null;

            var selected = candidates[_random.Next(candidates.Count)];
            _recentLines.Enqueue(selected);
            while (_recentLines.Count > 20)
                _recentLines.Dequeue();

            return selected;
        }

        private static bool Matches(DialogueRule rule, CharacterContext context)
        {
            var triggerMatches = string.Equals(rule.trigger, context.Trigger, StringComparison.OrdinalIgnoreCase);
            var personalityMatches = rule.personality == "*" ||
                string.Equals(rule.personality, context.Personality, StringComparison.OrdinalIgnoreCase);
            var tagsMatch = rule.tags == null || rule.tags.All(context.Tags.Contains);

            return triggerMatches && personalityMatches && tagsMatch;
        }
    }
}
