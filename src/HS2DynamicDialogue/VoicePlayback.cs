using System;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace HS2DynamicDialogue
{
    public sealed class VoicePlayback
    {
        private readonly ManualLogSource _log;
        private AIChara.ChaControl _character;
        private AudioSource _source;
        private int _reactionCount;
        private readonly System.Random _random = new System.Random();
        private readonly MethodInfo _updateBlendShapeVoice =
            typeof(AIChara.ChaControl).GetMethod(
                "UpdateBlendShapeVoice",
                BindingFlags.Instance | BindingFlags.NonPublic);

        public VoicePlayback(ManualLogSource log)
        {
            _log = log;
        }

        public void TryPlay(AIChara.ChaControl character, int everyNReactions)
        {
            _reactionCount++;
            if (everyNReactions > 1 && _reactionCount % everyNReactions != 0)
                return;

            if (character == null || character.fileParam == null ||
                !Manager.Voice.initialized || Manager.Voice.instance == null)
                return;

            try
            {
                var personality = character.fileParam.personality;
                VoiceInfo.Param voiceInfo;
                if (!Manager.Voice.infoTable.TryGetValue(personality, out voiceInfo))
                {
                    voiceInfo = Manager.Voice.infoTable.Values
                        .FirstOrDefault(item => item.No == personality);
                }

                if (voiceInfo == null || string.IsNullOrEmpty(voiceInfo.samplebundle) ||
                    string.IsNullOrEmpty(voiceInfo.sampleasset))
                {
                    _log.LogWarning("No Japanese sample voice found for personality " + personality + ".");
                    return;
                }

                var loader = new Manager.Voice.Loader
                {
                    no = voiceInfo.No,
                    pitch = character.fileParam.voicePitch + (float)(_random.NextDouble() * 0.06 - 0.03),
                    bundle = voiceInfo.samplebundle,
                    asset = voiceInfo.sampleasset,
                    fadeTime = 0f,
                    settingNo = 0,
                    voiceTrans = character.transform
                };

                _source = Manager.Voice.OncePlayChara(loader);
                _character = character;
                if (_source != null)
                {
                    _character.SetVoiceTransform(_source);
                    _log.LogDebug("Playing Japanese sample voice: " + voiceInfo.sampleasset);
                }
            }
            catch (Exception exception)
            {
                _log.LogWarning("Could not play Japanese sample voice: " + exception.Message);
                _source = null;
                _character = null;
            }
        }

        public void Tick()
        {
            if (_source == null || _character == null)
                return;

            if (_source.isPlaying)
            {
                if (_updateBlendShapeVoice != null)
                    _updateBlendShapeVoice.Invoke(_character, null);
                return;
            }

            _source = null;
            _character = null;
        }

        public void Stop()
        {
            try
            {
                if (_character != null)
                    Manager.Voice.Stop(_character.transform);
            }
            catch (Exception exception)
            {
                _log.LogDebug("Voice stop failed: " + exception.Message);
            }

            _source = null;
            _character = null;
        }
    }
}
