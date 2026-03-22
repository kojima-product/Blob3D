using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Blob3D.Core
{
    /// <summary>
    /// Manages all game audio: BGM and SE.
    /// Generates sounds procedurally — no audio files required.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Volume")]
        [SerializeField] private float bgmVolume = 0.3f;
        [SerializeField] private float seVolume = 0.6f;

        private AudioSource bgmSource;
        private List<AudioSource> seSources = new List<AudioSource>();
        private const int MaxSESources = 8;

        // Cached procedural clips
        private AudioClip feedPopClip;
        private AudioClip blobAbsorbClip;
        private AudioClip powerUpClip;
        private AudioClip dashClip;
        private AudioClip gameOverClip;
        private AudioClip timerWarningClip;
        private AudioClip victoryClip;
        private AudioClip bgmClip;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // Create BGM source
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.volume = bgmVolume;
            bgmSource.playOnAwake = false;

            // Create SE source pool
            for (int i = 0; i < MaxSESources; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.volume = seVolume;
                seSources.Add(src);
            }

            GenerateAllClips();
        }

        // ---- Public API ----

        public void PlayBGM()
        {
            bgmSource.clip = bgmClip;
            bgmSource.pitch = 1f;
            bgmSource.Play();
        }

        public void StopBGM() => bgmSource.Stop();

        /// <summary>Set BGM volume (0-1) and apply to source immediately.</summary>
        public void SetBGMVolume(float v)
        {
            bgmVolume = v;
            if (bgmSource != null) bgmSource.volume = v;
        }

        /// <summary>Set SE volume (0-1). Updates currently playing sources retroactively.</summary>
        public void SetSEVolume(float v)
        {
            seVolume = v;
            // Apply new volume to any currently playing SE sources
            foreach (var src in seSources)
            {
                if (src.isPlaying)
                    src.volume = seVolume;
            }
        }

        /// <summary>Speed up BGM for final countdown</summary>
        public void SetBGMUrgent(bool urgent)
        {
            bgmSource.pitch = urgent ? 1.2f : 1f;
        }

        /// <summary>Play feed absorption pop. Pitch varies by nutrition value.</summary>
        public void PlayFeedPop(float nutrition = 0.5f)
        {
            float pitch = Mathf.Lerp(1.2f, 0.8f, nutrition);
            PlaySE(feedPopClip, pitch, 0.4f);
        }

        /// <summary>Play blob absorption sound (heavier)</summary>
        public void PlayBlobAbsorb()
        {
            PlaySE(blobAbsorbClip, Random.Range(0.85f, 1.1f), 0.7f);
        }

        public void PlayPowerUp() => PlaySE(powerUpClip, 1f, 0.6f);
        public void PlayDash() => PlaySE(dashClip, 1f, 0.5f);
        public void PlayGameOver() => PlaySE(gameOverClip, 1f, 0.8f);
        public void PlayTimerWarning() => PlaySE(timerWarningClip, 1f, 0.5f);

        /// <summary>Play a celebratory ascending arpeggio for round victory.</summary>
        public void PlayVictory() => PlaySE(victoryClip, 1f, 0.8f);

        private void PlaySE(AudioClip clip, float pitch, float volume)
        {
            if (clip == null) return;
            foreach (var src in seSources)
            {
                if (!src.isPlaying)
                {
                    src.clip = clip;
                    src.pitch = pitch;
                    src.volume = volume * seVolume;
                    src.Play();
                    return;
                }
            }
            // All busy — steal the first
            seSources[0].clip = clip;
            seSources[0].pitch = pitch;
            seSources[0].volume = volume * seVolume;
            seSources[0].Play();
        }

        // ---- Procedural Audio Generation ----

        private void GenerateAllClips()
        {
            feedPopClip = GeneratePop(0.08f, 800f, 1200f);
            blobAbsorbClip = GenerateSlurp(0.25f);
            powerUpClip = GenerateChime(0.3f);
            dashClip = GenerateWhoosh(0.15f);
            gameOverClip = GenerateDescend(0.6f);
            timerWarningClip = GenerateBeep(0.1f, 880f);
            victoryClip = GenerateVictoryFanfare(0.5f);
            bgmClip = GenerateAmbientBGM(8f); // 8 second loop
        }

        private AudioClip GeneratePop(float duration, float freqStart, float freqEnd)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = Mathf.Lerp(freqStart, freqEnd, t);
                float envelope = (1f - t) * (1f - t); // Quick decay
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t * duration) * envelope * 0.5f;
            }
            var clip = AudioClip.Create("FeedPop", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateSlurp(float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = 200f + 400f * Mathf.Sin(t * Mathf.PI * 3f); // Wobbling frequency
                float envelope = Mathf.Sin(t * Mathf.PI); // Fade in and out
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t * duration / samples * i / (float)sampleRate) * envelope * 0.4f;
                // Add noise for organic feel
                data[i] += Random.Range(-0.05f, 0.05f) * envelope;
            }
            var clip = AudioClip.Create("BlobAbsorb", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateChime(float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            float[] freqs = { 523f, 659f, 784f, 1047f }; // C5, E5, G5, C6
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float val = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    float delay = f * 0.05f;
                    if (t > delay)
                    {
                        float localT = t - delay;
                        val += Mathf.Sin(2f * Mathf.PI * freqs[f] * localT) * (1f - localT / duration) * 0.2f;
                    }
                }
                data[i] = val;
            }
            var clip = AudioClip.Create("PowerUp", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateWhoosh(float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Sin(t * Mathf.PI);
                // Filtered noise for whoosh
                data[i] = Random.Range(-1f, 1f) * envelope * 0.3f;
            }
            // Simple low-pass by averaging neighbors
            for (int i = 1; i < samples - 1; i++)
            {
                data[i] = (data[i - 1] + data[i] + data[i + 1]) / 3f;
            }
            var clip = AudioClip.Create("Dash", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateDescend(float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = Mathf.Lerp(440f, 110f, t * t); // Descending pitch
                float envelope = 1f - t;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * (float)i / sampleRate) * envelope * 0.5f;
                // Add sub-harmonics
                data[i] += Mathf.Sin(2f * Mathf.PI * freq * 0.5f * (float)i / sampleRate) * envelope * 0.3f;
            }
            var clip = AudioClip.Create("GameOver", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateBeep(float duration, float freq)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = t < 0.1f ? t / 0.1f : (t > 0.8f ? (1f - t) / 0.2f : 1f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * (float)i / sampleRate) * envelope * 0.4f;
            }
            var clip = AudioClip.Create("TimerWarning", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Generate a celebratory ascending arpeggio: C5 -> E5 -> G5 -> C6.
        /// Each note is ~0.1s with slight overlap for a bright fanfare feel.
        /// </summary>
        private AudioClip GenerateVictoryFanfare(float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6
            float noteLength = 0.1f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float val = 0f;
                for (int n = 0; n < freqs.Length; n++)
                {
                    float noteStart = n * noteLength;
                    if (t >= noteStart)
                    {
                        float localT = t - noteStart;
                        // Quick attack, gentle decay
                        float attack = Mathf.Clamp01(localT / 0.005f);
                        float decay = Mathf.Exp(-localT * 6f);
                        float envelope = attack * decay;
                        // Fundamental + octave harmonic for brightness
                        val += Mathf.Sin(2f * Mathf.PI * freqs[n] * localT) * envelope * 0.25f;
                        val += Mathf.Sin(2f * Mathf.PI * freqs[n] * 2f * localT) * envelope * 0.08f;
                    }
                }
                data[i] = val;
            }
            var clip = AudioClip.Create("Victory", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateAmbientBGM(float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            // Layered sine drones for ambient background
            float[] baseFreqs = { 65.4f, 98f, 130.8f, 164.8f }; // C2, G2, C3, E3
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float val = 0f;
                for (int f = 0; f < baseFreqs.Length; f++)
                {
                    float lfo = 1f + 0.01f * Mathf.Sin(2f * Mathf.PI * (0.1f + f * 0.05f) * t);
                    val += Mathf.Sin(2f * Mathf.PI * baseFreqs[f] * lfo * t) * 0.08f;
                }
                // Subtle rhythm pulse
                float pulse = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                data[i] = val * pulse;
            }
            var clip = AudioClip.Create("BGM", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
