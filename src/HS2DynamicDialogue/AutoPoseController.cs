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

        public event Action PoseChanged;

        public AutoPoseController(ManualLogSource log)
        {
            _log = log;
        }

        public void Tick(float intervalSeconds)
        {
            if (!MakerAPI.InsideAndLoaded)
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
            try
            {
                if (!CharaCustom.CustomBase.IsInstance())
                    return;

                CharaCustom.CustomBase.Instance.ChangeAnimationNext(1);
                _log.LogDebug("Character Maker pose advanced automatically.");

                var handler = PoseChanged;
                if (handler != null)
                    handler();
            }
            catch (Exception exception)
            {
                _log.LogWarning("Automatic pose change failed: " + exception.Message);
            }
        }

        public void Reset()
        {
            _nextChange = 0f;
        }
    }
}
