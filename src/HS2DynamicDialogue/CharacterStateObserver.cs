using System;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using KKAPI.Maker;
using UnityEngine;

namespace HS2DynamicDialogue
{
    public sealed class CharacterStateObserver
    {
        private const float PollIntervalSeconds = 0.5f;

        private readonly ManualLogSource _log;
        private float _nextPoll;
        private string _lastClothingFingerprint;

        public event Action<CharacterContext> ContextChanged;

        public CharacterStateObserver(ManualLogSource log)
        {
            _log = log;
        }

        public void Tick()
        {
            if (!MakerAPI.InsideAndLoaded || Time.unscaledTime < _nextPoll)
                return;

            _nextPoll = Time.unscaledTime + PollIntervalSeconds;
            var character = MakerAPI.GetCharacterControl();
            if (character == null || character.nowCoordinate == null ||
                character.nowCoordinate.clothes == null)
                return;

            var parts = character.nowCoordinate.clothes.parts;
            if (parts == null)
                return;

            var builder = new StringBuilder();
            foreach (var part in parts)
                builder.Append(part == null ? -1 : part.id).Append('|');

            var fingerprint = builder.ToString();
            if (_lastClothingFingerprint == null)
            {
                _lastClothingFingerprint = fingerprint;
                return;
            }

            if (fingerprint == _lastClothingFingerprint)
                return;

            _lastClothingFingerprint = fingerprint;
            Publish("clothing_changed", character.fileParam == null
                ? "*"
                : "personality:" + character.fileParam.personality);
        }

        public void PublishAccessoryChanged()
        {
            if (!MakerAPI.InsideAndLoaded)
                return;

            var character = MakerAPI.GetCharacterControl();
            Publish("accessory_changed", character == null || character.fileParam == null
                ? "*"
                : "personality:" + character.fileParam.personality);
        }

        public void Reset()
        {
            _lastClothingFingerprint = null;
            _nextPoll = 0f;
        }

        private void Publish(string trigger, string personality)
        {
            var handler = ContextChanged;
            if (handler == null)
                return;

            _log.LogDebug("Context changed: " + trigger + ", " + personality);
            handler(new CharacterContext
            {
                Trigger = trigger,
                Personality = personality
            });
        }
    }
}
