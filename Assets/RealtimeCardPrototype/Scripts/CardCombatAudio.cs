using UnityEngine;

namespace RealtimeCardPrototype
{
    internal static class CardCombatAudio
    {
        private const string ResourceFolder = "CardAudio/";
        private const float BlockRepeatDelay = 0.045f;
        private const float ShockChainRepeatDelay = 0.12f;
        private const float FocusBubbleRepeatDelay = 1.5f;
        private const float PierceRepeatDelay = 0.075f;
        private const float PowerChargeRepeatDelay = 0.09f;
        private const float BattleMusicVolume = 0.16f;

        private static AudioSource source;
        private static AudioClip strike;
        private static AudioClip heavy;
        private static AudioClip blast;
        private static AudioClip block;
        private static AudioClip shock;
        private static AudioClip shockChain;
        private static AudioClip poison;
        private static AudioClip focusBubble;
        private static AudioClip turretShot;
        private static AudioClip pierce;
        private static AudioClip powerPlacement;
        private static AudioClip powerCharge;
        private static AudioClip placeholder;
        private static float lastBlockTime;
        private static float lastShockChainTime;
        private static float lastFocusBubbleTime;
        private static float lastPierceTime;
        private static float lastPowerChargeTime;

        internal static void Initialize(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            lastBlockTime = float.NegativeInfinity;
            lastShockChainTime = float.NegativeInfinity;
            lastFocusBubbleTime = float.NegativeInfinity;
            lastPierceTime = float.NegativeInfinity;
            lastPowerChargeTime = float.NegativeInfinity;
            strike = Resources.Load<AudioClip>(ResourceFolder + "Strike");
            heavy = Resources.Load<AudioClip>(ResourceFolder + "Heavy");
            blast = Resources.Load<AudioClip>(ResourceFolder + "Blast");
            block = Resources.Load<AudioClip>(ResourceFolder + "Block");
            shock = Resources.Load<AudioClip>(ResourceFolder + "Shock");
            shockChain = Resources.Load<AudioClip>(ResourceFolder + "ShockChain");
            poison = Resources.Load<AudioClip>(ResourceFolder + "Poison");
            focusBubble = Resources.Load<AudioClip>(ResourceFolder + "FocusBubble");
            turretShot = Resources.Load<AudioClip>(ResourceFolder + "TurretShot");
            pierce = Resources.Load<AudioClip>(ResourceFolder + "Pierce");
            powerPlacement = Resources.Load<AudioClip>(ResourceFolder + "PowerPlacement");
            powerCharge = Resources.Load<AudioClip>(ResourceFolder + "PowerCharge");
            placeholder = Resources.Load<AudioClip>(ResourceFolder + "Placeholder");

            GameObject musicObject = new GameObject("Battle Music");
            musicObject.transform.SetParent(host.transform, false);
            AudioSource music = musicObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.loop = true;
            music.volume = BattleMusicVolume;
            music.clip = Resources.Load<AudioClip>(ResourceFolder + "BattleMusic");
            music.Play();
        }

        internal static void PlayAttack(bool isHeavy)
        {
            source.PlayOneShot(isHeavy ? heavy : strike, isHeavy ? 0.49f : 0.44f);
        }

        internal static void PlayBlast()
        {
            source.PlayOneShot(blast, 0.48f);
        }

        internal static void PlayBlock()
        {
            float now = Time.unscaledTime;
            if (now - lastBlockTime < BlockRepeatDelay)
            {
                return;
            }

            lastBlockTime = now;
            source.PlayOneShot(block, 0.43f);
        }

        internal static void PlayPlaceholder(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Strike:
                case CardKind.Heavy:
                case CardKind.Blast:
                case CardKind.Block:
                case CardKind.Shock:
                case CardKind.Poison:
                case CardKind.Focus:
                case CardKind.Turret:
                case CardKind.Pierce:
                case CardKind.Power:
                    break;
                default:
                    source.PlayOneShot(placeholder, 0.21f);
                    break;
            }
        }

        internal static void PlayShock()
        {
            source.PlayOneShot(shock, 0.40f);
        }

        internal static void PlayShockChain()
        {
            float now = Time.unscaledTime;
            if (now - lastShockChainTime < ShockChainRepeatDelay)
            {
                return;
            }

            lastShockChainTime = now;
            source.PlayOneShot(shockChain, 0.25f);
        }

        internal static void PlayPoison()
        {
            source.PlayOneShot(poison, 0.29f);
        }

        internal static void PlayFocusBubble()
        {
            float now = Time.unscaledTime;
            if (now - lastFocusBubbleTime < FocusBubbleRepeatDelay)
            {
                return;
            }

            lastFocusBubbleTime = now;
            source.PlayOneShot(focusBubble, 0.15f);
        }

        internal static void PlayTurretShot()
        {
            source.PlayOneShot(turretShot, 0.44f);
        }

        internal static void PlayPierce()
        {
            float now = Time.unscaledTime;
            if (now - lastPierceTime < PierceRepeatDelay)
            {
                return;
            }

            lastPierceTime = now;
            source.PlayOneShot(pierce, 0.37f);
        }

        internal static void PlayPowerPlacement()
        {
            source.PlayOneShot(powerPlacement, 0.24f);
        }

        internal static void PlayPowerCharge()
        {
            float now = Time.unscaledTime;
            if (now - lastPowerChargeTime < PowerChargeRepeatDelay)
            {
                return;
            }

            lastPowerChargeTime = now;
            source.PlayOneShot(powerCharge, 0.34f);
        }
    }
}
