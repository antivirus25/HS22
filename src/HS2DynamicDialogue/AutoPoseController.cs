using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using KKAPI.Maker;
using UnityEngine;

namespace HS2DynamicDialogue
{
    public sealed class AutoPoseController
    {
        private const string GravureGuid = "mikke.gravureAI";

        private readonly ManualLogSource _log;
        private readonly System.Random _random = new System.Random();
        private float _nextChange;
        private object _gravure;
        private FieldInfo _groupsField;
        private FieldInfo _groupIndexField;
        private FieldInfo _animationIndexField;
        private FieldInfo _groupNamesField;
        private string _lastAnimation;
        private bool _covering;

        public event Action PoseChanged;

        public AutoPoseController(ManualLogSource log)
        {
            _log = log;
            ResolveGravure();
        }

        public void Tick(float intervalSeconds, float transitionSeconds)
        {
            if (!MakerAPI.InsideAndLoaded)
                return;
            if (_covering)
                return;

            var interval = Mathf.Max(5f, intervalSeconds);
            if (_nextChange <= 0f)
            {
                _nextChange = Time.unscaledTime + interval;
                return;
            }

            if (Time.unscaledTime < _nextChange)
                return;

            _nextChange = Time.unscaledTime + interval;
            TryChangeGravureAnimation(Mathf.Clamp(transitionSeconds, 0.25f, 4f));
        }

        private void ResolveGravure()
        {
            try
            {
                BepInEx.PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue(GravureGuid, out info) ||
                    info.Instance == null)
                {
                    _log.LogWarning("Gravure plugin was not found; automatic animation is disabled.");
                    return;
                }

                _gravure = info.Instance;
                var type = _gravure.GetType();
                _groupsField = type.GetField(
                    "gravureAnims",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                _groupIndexField = type.GetField(
                    "animeControllerIndex1D",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                _animationIndexField = type.GetField(
                    "gravureIndex",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                _groupNamesField = type.GetField(
                    "AnimGroups",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                _log.LogInfo("Gravure animation catalog integration enabled.");
            }
            catch (Exception exception)
            {
                _log.LogWarning("Could not connect to Gravure: " + exception.Message);
                _gravure = null;
            }
        }

        private void TryChangeGravureAnimation(float transitionSeconds)
        {
            try
            {
                if (_gravure == null)
                {
                    ResolveGravure();
                    if (_gravure == null)
                        return;
                }

                var groups = _groupsField.GetValue(_gravure) as string[][];
                var groupNames = _groupNamesField.GetValue(_gravure) as string[];
                var groupIndex = (int)_groupIndexField.GetValue(_gravure);
                if (groups == null || groupIndex < 0 || groupIndex >= groups.Length ||
                    groups[groupIndex] == null || groups[groupIndex].Length == 0)
                    return;

                var groupName = groupNames != null && groupIndex < groupNames.Length
                    ? groupNames[groupIndex]
                    : string.Empty;
                if (!string.Equals(groupName, "Gravure", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(groupName, "Pose", StringComparison.OrdinalIgnoreCase))
                {
                    _log.LogDebug(
                        "Skipping unsafe automatic Gravure group: " + groupName + ".");
                    return;
                }

                var animations = groups[groupIndex];
                var candidates = Array.FindAll(
                    animations,
                    animation => !string.IsNullOrEmpty(animation) && animation != _lastAnimation);
                if (candidates.Length == 0)
                    candidates = animations;

                var selected = candidates[_random.Next(candidates.Length)];
                _lastAnimation = selected;
                _animationIndexField.SetValue(_gravure, Array.IndexOf(animations, selected));

                var character = MakerAPI.GetCharacterControl();
                if (character == null)
                    return;

                var position = character.transform.position;
                var rotation = character.transform.rotation;
                if (character.animBody != null)
                    character.animBody.applyRootMotion = false;
                character.setAnimPtnCrossFade(selected, transitionSeconds, 0, 0f);
                character.transform.position = position;
                character.transform.rotation = rotation;
                _log.LogDebug(
                    "Crossfading to Gravure animation " + selected +
                    " over " + transitionSeconds + " seconds.");

                var handler = PoseChanged;
                if (handler != null)
                    handler();
            }
            catch (Exception exception)
            {
                _log.LogWarning("Gravure animation change failed: " + exception.Message);
            }
        }

        public void Reset()
        {
            _nextChange = 0f;
            _covering = false;
        }

        public void SetCoveringMode(bool enabled)
        {
            if (_covering == enabled)
                return;

            _covering = enabled;
            _nextChange = 0f;
            if (!enabled || !CharaCustom.CustomBase.IsInstance())
                return;

            try
            {
                var character = MakerAPI.GetCharacterControl();
                if (character == null)
                    return;

                var position = character.transform.position;
                var rotation = character.transform.rotation;
                if (character.animBody != null)
                    character.animBody.applyRootMotion = false;

                // Pose 37 is the hands-in-front pose visible in the user's Maker setup.
                CharaCustom.CustomBase.Instance.ChangeAnimationNo(37, false);
                character.transform.position = position;
                character.transform.rotation = rotation;
                _log.LogInfo("Timid covering pose activated.");
            }
            catch (Exception exception)
            {
                _log.LogWarning("Could not activate timid covering pose: " + exception.Message);
            }
        }
    }
}
