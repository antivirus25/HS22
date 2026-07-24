using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace HS2DynamicDialogue
{
    public sealed class VoicePlayback
    {
        private sealed class VoiceCatalog
        {
            public AssetBundle Bundle;
            public string[] AssetNames;
        }

        private readonly MonoBehaviour _host;
        private readonly ManualLogSource _log;
        private readonly Dictionary<string, VoiceCatalog> _catalogs =
            new Dictionary<string, VoiceCatalog>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> _recentAssets = new Queue<string>();
        private readonly System.Random _random = new System.Random();

        private AIChara.ChaControl _character;
        private AudioSource _source;
        private int _reactionCount;
        private int _generation;

        public VoicePlayback(MonoBehaviour host, ManualLogSource log)
        {
            _host = host;
            _log = log;
        }

        public bool TryPlay(
            AIChara.ChaControl character,
            int everyNReactions,
            Action onAudioStarted,
            Action onAudioUnavailable)
        {
            _reactionCount++;
            if (everyNReactions > 1 && _reactionCount % everyNReactions != 0)
                return false;

            if (character == null || character.fileParam == null)
                return false;

            var bundlePaths = FindAdvVoiceBundles(character.fileParam.personality);
            if (bundlePaths.Count == 0)
            {
                _log.LogWarning(
                    "No ADV voice bundle found for personality " +
                    character.fileParam.personality + ".");
                return false;
            }

            Stop();
            var generation = _generation;
            var bundlePath = bundlePaths[_random.Next(bundlePaths.Count)];
            VoiceCatalog catalog;
            if (_catalogs.TryGetValue(bundlePath, out catalog))
            {
                PlayFromCatalog(
                    catalog,
                    character,
                    generation,
                    onAudioStarted,
                    onAudioUnavailable);
            }
            else
            {
                _host.StartCoroutine(LoadAndPlay(
                    bundlePath,
                    character,
                    generation,
                    onAudioStarted,
                    onAudioUnavailable));
            }

            return true;
        }

        private IEnumerator LoadAndPlay(
            string bundlePath,
            AIChara.ChaControl character,
            int generation,
            Action onAudioStarted,
            Action onAudioUnavailable)
        {
            var request = AssetBundle.LoadFromFileAsync(bundlePath);
            yield return request;

            if (generation != _generation)
                yield break;

            var bundle = request.assetBundle;
            if (bundle == null)
            {
                _log.LogWarning("Could not load voice bundle: " + bundlePath);
                if (onAudioUnavailable != null)
                    onAudioUnavailable();
                yield break;
            }

            var names = bundle.GetAllAssetNames()
                .Where(name =>
                    name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var catalog = new VoiceCatalog
            {
                Bundle = bundle,
                AssetNames = names
            };
            _catalogs[bundlePath] = catalog;
            _log.LogInfo("Indexed " + names.Length + " ADV voice clips from " + bundlePath + ".");

            PlayFromCatalog(
                catalog,
                character,
                generation,
                onAudioStarted,
                onAudioUnavailable);
        }

        private void PlayFromCatalog(
            VoiceCatalog catalog,
            AIChara.ChaControl character,
            int generation,
            Action onAudioStarted,
            Action onAudioUnavailable)
        {
            if (catalog.AssetNames == null || catalog.AssetNames.Length == 0)
            {
                if (onAudioUnavailable != null)
                    onAudioUnavailable();
                return;
            }

            var candidates = catalog.AssetNames
                .Where(name => !_recentAssets.Contains(name))
                .ToArray();
            if (candidates.Length == 0)
                candidates = catalog.AssetNames;

            var assetName = candidates[_random.Next(candidates.Length)];
            _host.StartCoroutine(LoadClipAndPlay(
                catalog,
                assetName,
                character,
                generation,
                onAudioStarted,
                onAudioUnavailable));
        }

        private IEnumerator LoadClipAndPlay(
            VoiceCatalog catalog,
            string assetName,
            AIChara.ChaControl character,
            int generation,
            Action onAudioStarted,
            Action onAudioUnavailable)
        {
            var request = catalog.Bundle.LoadAssetAsync<AudioClip>(assetName);
            yield return request;

            if (generation != _generation)
                yield break;

            var clip = request.asset as AudioClip;
            if (clip == null)
            {
                _log.LogWarning("Could not load ADV voice clip: " + assetName);
                if (onAudioUnavailable != null)
                    onAudioUnavailable();
                yield break;
            }

            _recentAssets.Enqueue(assetName);
            while (_recentAssets.Count > 30)
                _recentAssets.Dequeue();

            _character = character;
            _source = character.gameObject.AddComponent<AudioSource>();
            _source.clip = clip;
            _source.pitch = character.fileParam.voicePitch;
            _source.spatialBlend = 0f;
            _source.Play();
            character.SetVoiceTransform(_source);

            _log.LogDebug("Playing ADV voice: " + assetName);
            if (onAudioStarted != null)
                onAudioStarted();
        }

        public void Tick()
        {
            if (_source == null)
                return;

            if (_source.isPlaying)
                return;

            UnityEngine.Object.Destroy(_source);
            _source = null;
            _character = null;
        }

        public void Stop()
        {
            _generation++;
            if (_source != null)
            {
                _source.Stop();
                UnityEngine.Object.Destroy(_source);
            }

            _source = null;
            _character = null;
        }

        private static List<string> FindAdvVoiceBundles(int personality)
        {
            var personalityFolder = Path.Combine(
                Paths.GameRootPath,
                "abdata",
                "sound",
                "data",
                "pcm",
                "c" + personality.ToString("00"));

            var result = new List<string>();
            foreach (var category in new[] { "adv", "etc" })
            {
                foreach (var name in new[] { "30.unity3d", "50.unity3d" })
                {
                    var path = Path.Combine(personalityFolder, category, name);
                    if (File.Exists(path))
                        result.Add(path);
                }
            }

            return result;
        }
    }
}
