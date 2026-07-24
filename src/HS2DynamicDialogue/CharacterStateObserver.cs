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
            if (character.fileStatus != null && character.fileStatus.clothesState != null)
            {
                foreach (var state in character.fileStatus.clothesState)
                    builder.Append(state).Append('|');
            }

            var fingerprint = builder.ToString();
            if (_lastClothingFingerprint == null)
            {
                _lastClothingFingerprint = fingerprint;
                return;
            }

            if (fingerprint == _lastClothingFingerprint)
                return;

            _lastClothingFingerprint = fingerprint;
            Publish("clothing_changed", character);
        }

        public void PublishAccessoryChanged()
        {
            if (!MakerAPI.InsideAndLoaded)
                return;

            var character = MakerAPI.GetCharacterControl();
            Publish("accessory_changed", character);
        }

        public void Reset()
        {
            _lastClothingFingerprint = null;
            _nextPoll = 0f;
        }

        public static CharacterContext BuildContext(
            string trigger,
            AIChara.ChaControl character)
        {
            var context = new CharacterContext
            {
                Trigger = trigger,
                Personality = character == null || character.fileParam == null
                    ? "*"
                    : "personality:" + character.fileParam.personality
            };

            if (character == null)
                return context;

            AddWardrobeTag(context, character);
            if (IsTimid(character))
                context.Tags.Add("timid");

            return context;
        }

        private static void AddWardrobeTag(
            CharacterContext context,
            AIChara.ChaControl character)
        {
            var states = character.fileStatus == null
                ? null
                : character.fileStatus.clothesState;
            if (states == null || states.Length < 4)
            {
                context.Tags.Add("dressed");
                return;
            }

            var outerOff = states[0] >= 2 && states[1] >= 2;
            var underwearOff = states[2] >= 2 && states[3] >= 2;
            if (outerOff && underwearOff)
                context.Tags.Add("nude");
            else if (outerOff)
                context.Tags.Add("underwear");
            else
                context.Tags.Add("dressed");
        }

        private static bool IsTimid(AIChara.ChaControl character)
        {
            if (character.fileParam == null || !Manager.Voice.initialized)
                return false;

            VoiceInfo.Param info;
            if (!Manager.Voice.infoTable.TryGetValue(
                character.fileParam.personality,
                out info) || info == null)
                return false;

            var name = ((info.Personality ?? string.Empty) + " " +
                        (info.EnUS ?? string.Empty)).ToLowerInvariant();
            return name.Contains("timid") || name.Contains("shy");
        }

        private void Publish(string trigger, AIChara.ChaControl character)
        {
            var handler = ContextChanged;
            if (handler == null)
                return;

            var context = BuildContext(trigger, character);
            _log.LogDebug(
                "Context changed: " + trigger + ", " + context.Personality +
                ", tags=" + string.Join(",", context.Tags.ToArray()));
            handler(context);
        }
    }
}
