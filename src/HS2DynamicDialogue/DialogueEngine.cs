using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace HS2DynamicDialogue
{
    public sealed class DialogueEngine
    {
        private readonly string _databasePath;
        private readonly ManualLogSource _log;
        private readonly Random _random = new Random();
        private DialogueDatabase _database = new DialogueDatabase();
        private string _lastLine;

        public DialogueEngine(string databasePath, ManualLogSource log)
        {
            _databasePath = databasePath;
            _log = log;
            Reload();
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
                _database = JsonUtility.FromJson<DialogueDatabase>(json) ?? new DialogueDatabase();
                _log.LogInfo("Loaded " + _database.rules.Count + " dialogue rules.");
            }
            catch (Exception exception)
            {
                _log.LogError("Could not load dialogue database: " + exception);
            }
        }

        public string Select(CharacterContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

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
                .Where(line => !string.IsNullOrWhiteSpace(line) && line != _lastLine)
                .Distinct()
                .ToList();

            if (candidates.Count == 0)
                candidates = matches.SelectMany(rule => rule.lines).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

            if (candidates.Count == 0)
                return null;

            _lastLine = candidates[_random.Next(candidates.Count)];
            return _lastLine;
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
