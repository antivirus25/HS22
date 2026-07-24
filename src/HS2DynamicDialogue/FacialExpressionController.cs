using System;
using BepInEx.Logging;

namespace HS2DynamicDialogue
{
    public sealed class FacialExpressionController
    {
        private readonly ManualLogSource _log;
        private readonly System.Random _random = new System.Random();
        private AIChara.ChaControl _character;
        private bool _captured;
        private int _eyebrows;
        private int _eyes;
        private int _mouth;
        private float _eyesOpen;
        private float _mouthOpen;
        private float _blush;

        public FacialExpressionController(ManualLogSource log)
        {
            _log = log;
        }

        public void Apply(AIChara.ChaControl character, CharacterContext context)
        {
            if (character == null || context == null)
                return;

            Capture(character);
            var exposed = context.Tags.Contains("underwear") ||
                          context.Tags.Contains("nude");
            var timid = context.Tags.Contains("timid");

            try
            {
                if (timid && exposed)
                {
                    character.ChangeEyebrowPtn(10, true);
                    character.ChangeEyesPtn(1, true);
                    character.ChangeEyesOpenMax(
                        context.Tags.Contains("nude") ? 0.55f : 0.72f);
                    character.ChangeMouthPtn(18, true);
                    character.ChangeMouthOpenMax(
                        context.Tags.Contains("nude") ? 0.32f : 0.2f);
                    character.ChangeHohoAkaRate(
                        context.Tags.Contains("nude") ? 0.85f : 0.6f);
                    return;
                }

                if (context.Trigger == "pose_changed")
                {
                    var soft = _random.Next(2) == 0;
                    character.ChangeEyebrowPtn(soft ? 0 : 10, true);
                    character.ChangeEyesPtn(soft ? 0 : 1, true);
                    character.ChangeEyesOpenMax(soft ? 1f : 0.85f);
                    character.ChangeMouthPtn(soft ? 0 : 18, true);
                    character.ChangeMouthOpenMax(soft ? 0.08f : 0.14f);
                    character.ChangeHohoAkaRate(soft ? 0.1f : 0.25f);
                }
                else if (context.Tags.Contains("underwear"))
                {
                    character.ChangeEyebrowPtn(10, true);
                    character.ChangeEyesPtn(1, true);
                    character.ChangeMouthPtn(18, true);
                    character.ChangeHohoAkaRate(0.35f);
                }
                else
                {
                    character.ChangeEyebrowPtn(0, true);
                    character.ChangeEyesPtn(0, true);
                    character.ChangeMouthPtn(0, true);
                    character.ChangeHohoAkaRate(0.05f);
                }
            }
            catch (Exception exception)
            {
                _log.LogWarning("Facial expression update failed: " + exception.Message);
            }
        }

        private void Capture(AIChara.ChaControl character)
        {
            if (_captured && _character == character)
                return;

            _character = character;
            _eyebrows = character.GetEyebrowPtn();
            _eyes = character.GetEyesPtn();
            _mouth = character.GetMouthPtn();
            _eyesOpen = character.GetEyesOpenMax();
            _mouthOpen = character.GetMouthOpenMax();
            _blush = character.hohoAkaRate;
            _captured = true;
        }

        public void Reset()
        {
            if (!_captured || _character == null)
                return;

            try
            {
                _character.ChangeEyebrowPtn(_eyebrows, true);
                _character.ChangeEyesPtn(_eyes, true);
                _character.ChangeEyesOpenMax(_eyesOpen);
                _character.ChangeMouthPtn(_mouth, true);
                _character.ChangeMouthOpenMax(_mouthOpen);
                _character.ChangeHohoAkaRate(_blush);
            }
            catch
            {
                // Character may already have been destroyed while leaving Maker.
            }

            _captured = false;
            _character = null;
        }
    }
}
