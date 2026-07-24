using System;
using BepInEx.Logging;
using KKAPI.Maker;
using UnityEngine;

namespace HS2DynamicDialogue
{
    public sealed class AutoPoseController
    {
        private readonly ManualLogSource _log;
        private float _nextChange;
        private float _pendingSince;
        private bool _changePending;
        private bool _covering;

        public event Action PoseChanged;

        public AutoPoseController(ManualLogSource log)
        {
            _log = log;
        }

        public void RestoreVanillaPose()
        {
            if (!MakerAPI.InsideAndLoaded || !CharaCustom.CustomBase.IsInstance())
                return;

            try
            {
                // This goes through the Maker's own pose loader. It restores the matching
                // controller, MotionIK data and animation for the current character.
                CharaCustom.CustomBase.Instance.ChangeAnimationNo(0, true);
                _log.LogInfo("Original Character Maker pose controller restored.");
            }
            catch (Exception exception)
            {
                _log.LogWarning("Could not restore the original Maker pose: " + exception.Message);
            }
        }

        public void Tick(float intervalSeconds)
        {
            if (!MakerAPI.InsideAndLoaded || _covering)
                return;

            var interval = Mathf.Max(5f, intervalSeconds);
            if (_nextChange <= 0f)
            {
                _nextChange = Time.unscaledTime + interval;
                return;
            }

            if (!_changePending && Time.unscaledTime >= _nextChange)
            {
                _changePending = true;
                _pendingSince = Time.unscaledTime;
            }

            if (!_changePending)
                return;

            // Wait for the end of the current loop when possible. This avoids changing
            // pose in the middle of a hand or body movement.
            if (!IsNearAnimationBoundary() && Time.unscaledTime - _pendingSince < 4f)
                return;

            AdvanceOriginalPose();
            _changePending = false;
            _nextChange = Time.unscaledTime + interval;
        }

        private static bool IsNearAnimationBoundary()
        {
            var character = MakerAPI.GetCharacterControl();
            if (character == null || character.animBody == null)
                return true;

            var state = character.animBody.GetCurrentAnimatorStateInfo(0);
            var cycle = state.normalizedTime - Mathf.Floor(state.normalizedTime);
            return cycle >= 0.88f || cycle <= 0.04f;
        }

        private void AdvanceOriginalPose()
        {
            if (!CharaCustom.CustomBase.IsInstance())
                return;

            try
            {
                // ChangeAnimationNext uses HS2's category 500/501 list and reloads the
                // official controller and MotionIK data. No Gravure animation is touched.
                CharaCustom.CustomBase.Instance.ChangeAnimationNext(1);
                _log.LogDebug("Advanced to the next original Character Maker pose.");

                var handler = PoseChanged;
                if (handler != null)
                    handler();
            }
            catch (Exception exception)
            {
                _log.LogWarning("Original Maker pose change failed: " + exception.Message);
            }
        }

        public void Reset()
        {
            _nextChange = 0f;
            _pendingSince = 0f;
            _changePending = false;
            _covering = false;
        }

        public void SetCoveringMode(bool enabled)
        {
            if (_covering == enabled)
                return;

            _covering = enabled;
            _nextChange = 0f;
            _changePending = false;
            if (!CharaCustom.CustomBase.IsInstance())
                return;

            try
            {
                if (enabled)
                {
                    // Original female Maker pose 37 resembles covering the front of the body.
                    CharaCustom.CustomBase.Instance.ChangeAnimationNo(37, false);
                    _log.LogInfo("Timid covering pose activated from the original pose list.");
                }
                else
                {
                    CharaCustom.CustomBase.Instance.ChangeAnimationNext(1);
                    _log.LogInfo("Timid covering pose released.");
                }
            }
            catch (Exception exception)
            {
                _log.LogWarning("Could not change the timid covering pose: " + exception.Message);
            }
        }
    }
}
