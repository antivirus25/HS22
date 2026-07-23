using System;
using System.Collections.Generic;

namespace HS2DynamicDialogue
{
    [Serializable]
    public sealed class DialogueDatabase
    {
        public List<DialogueRule> rules = new List<DialogueRule>();
    }

    [Serializable]
    public sealed class DialogueRule
    {
        public string id = string.Empty;
        public string trigger = string.Empty;
        public string personality = "*";
        public List<string> tags = new List<string>();
        public List<string> lines = new List<string>();
        public int priority;
    }

    public sealed class CharacterContext
    {
        public string Trigger { get; set; }
        public string Personality { get; set; }
        public ISet<string> Tags { get; set; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
