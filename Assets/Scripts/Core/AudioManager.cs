using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Blob3D.Core
{
    /// <summary>
    /// Manages all game audio: BGM and SE.
    /// Generates sounds procedurally — no audio files required.
    /// Features: spatial audio, volume ducking, pitch variation, sound debounce.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Volume")]
        [SerializeField] private float bgmVolume = 0.3f;
        [SerializeField] private float seVolume = 0.6f;

        [Header("Ducking")]
        [SerializeField] private float duckVolume = 0.1f;
        [SerializeField] private float duckFadeDuration = 0.3f;
        [SerializeField] private float duckHoldDuration = 1.0f;

        [Header("Pitch Variation")]
        [SerializeField] private float pitchVariationMin = 0.9f;
        [SerializeField] private float pitchVariationMax = 1.1f;

        [Header("Sound Debounce")]
        [SerializeField] private float debounceInterval = 0.05f;

        private AudioSource bgmSource;
        private List<AudioSource> seSources = new List<AudioSource>();
        private const int MaxSESources = 12;

        // Cached procedural clips
        private AudioClip feedPopClip;
        private AudioClip blobAbsorbClip;
        private AudioClip powerUpClip;
        private AudioClip dashClip;
        private AudioClip shieldBlockClip;
        private AudioClip comboTingClip;
        private AudioClip gameOverClip;
        private AudioClip menuClickClip;
        private AudioClip timerWarningClip;
        private AudioClip victoryClip;
        private AudioClip bgmClip;

        // Debounce tracking: clip instance ID -> last play time
        private Dictionary<int, float> lastPlayTimes = new Dictionary<int, float>();

        // Ducking coroutine reference for cancellation
        private Coroutine duckCoroutine;
        private float bgmTargetVolume;

        // Spatial audio pool for 3D positioned sounds
        private List<AudioSource> spatialSources = new List<AudioSource>();
        private const int MaxSpatialSources = 6;

        private void Awake()
        {
            // Proper singleton check including self-reference
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            bgmTargetVolume = bgmVolume;

            // Create BGM source
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.volume = bgmVolume;
            bgmSource.playOnAwake = false;

            // Create 2D SE source pool
            for (int i = 0; i < MaxSESources; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.volume = seVolume;
                src.spatialBlend = 0f; // 2D
                seSources.Add(src);
            }

            // Create 3D spatial SE source pool as child objects
            for (int i = 0; i < MaxSpatialSources; i++)
            {
                var child = new GameObject($"SpatialAudio_{i}");
                child.transform.SetParent(transform);
                var src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.volume = seVolume;
                src.spatialBlend = 1f; // Fully 3D
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 2f;
                src.maxDistance = 50f;
                src.spread = 60f;
                spatialSources.Add(src);
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
            bgmTargetVolume = v;
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
            foreach (var src in spatialSources)
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

        /// <summary>Play feed pop at a world position for spatial effect.</summary>
        public void PlayFeedPopAt(Vector3 position, float nutrition = 0.5f)
        {
            float pitch = Mathf.Lerp(1.2f, 0.8f, nutrition);
            PlaySESpatial(feedPopClip, position, pitch, 0.4f);
        }

        /// <summary>Play blob absorption sound (heavier). Triggers BGM ducking.</summary>
        public void PlayBlobAbsorb()
        {
            PlaySE(blobAbsorbClip, RandomPitch(), 0.7f);
            DuckBGM();
        }

        /// <summary>Play blob absorption at a world position with ducking.</summary>
        public void PlayBlobAbsorbAt(Vector3 position)
        {
            PlaySESpatial(blobAbsorbClip, position, RandomPitch(), 0.7f);
            DuckBGM();
        }

        public void PlayPowerUp() => PlaySE(powerUpClip, 1f, 0.6f);

        /// <summary>Play power-up collection at a world position.</summary>
        public void PlayPowerUpAt(Vector3 position) => PlaySESpatial(powerUpClip, position, 1f, 0.6f);

        public void PlayDash() => PlaySE(dashClip, RandomPitch(), 0.5f);

        /// <summary>Play shield block metallic clang with pitch variation.</summary>
        public void PlayShieldBlock() => PlaySE(shieldBlockClip, RandomPitch(), 0.6f);

        /// <summary>Play shield block at a world position.</summary>
        public void PlayShieldBlockAt(Vector3 position) => PlaySESpatial(shieldBlockClip, position, RandomPitch(), 0.6f);

        /// <summary>Play combo increment ting with rising pitch based on combo count.</summary>
        public void PlayComboTing(int comboCount = 1)
        {
            // Rising pitch: higher combo = higher pitch, clamped to reasonable range
            float pitch = Mathf.Clamp(0.8f + comboCount * 0.1f, 0.8f, 2.0f);
            PlaySE(comboTingClip, pitch, 0.5f);
        }

        /// <summary>Play game over sound with BGM ducking for dramatic effect.</summary>
        public void PlayGameOver()
        {
            PlaySE(gameOverClip, 1f, 0.8f);
            DuckBGM(2.0f); // Extended duck for dramatic moment
        }

        /// <summary>Play subtle menu click.</summary>
        public void PlayMenuClick() => PlaySE(menuClickClip, RandomPitch(), 0.3f);

        public void PlayTimerWarning() => PlaySE(timerWarningClip, 1f, 0.5f);

        /// <summary>Play a celebratory ascending arpeggio for round victory.</summary>
        public void PlayVictory() => PlaySE(victoryClip, 1f, 0.8f);

        // ---- Volume Ducking ----

        /// <summary>Temporarily lower BGM volume for dramatic emphasis.</summary>
        public void DuckBGM(float holdOverride = -1f)
        {
            if (duckCoroutine != null) StopCoroutine(duckCoroutine);
            float hold = holdOverride > 0 ? holdOverride : duckHoldDuration;
            duckCoroutine = StartCoroutine(DuckBGMCoroutine(hold));
        }

        private IEnumerator DuckBGMCoroutine(float holdTime)
        {
            // Fade down
            float startVol = bgmSource.volume;
            float target = duckVolume;
            float elapsed = 0f;
            while (elapsed < duckFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, target, elapsed / duckFadeDuration);
                yield return null;
            }
            bgmSource.volume = target;

            // Hold at ducked volume
            yield return new WaitForSecondsRealtime(holdTime);

            // Fade back up
            elapsed = 0f;
            while (elapsed < duckFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(target, bgmTargetVolume, elapsed / duckFadeDuration);
                yield return null;
            }
            bgmSource.volume = bgmTargetVolume;
            duckCoroutine = null;
        }

        // ---- Internal Playback ----

        /// <summary>Generate a randomized pitch within configured range for natural variation.</summary>
        private float RandomPitch()
        {
            return Random.Range(pitchVariationMin, pitchVariationMax);
        }

        /// <summary>Check debounce: returns true if clip was played too recently.</summary>
        private bool IsDebounced(AudioClip clip)
        {
            if (clip == null) return true;
            int id = clip.GetInstanceID();
            if (lastPlayTimes.TryGetValue(id, out float lastTime))
            {
                if (Time.unscaledTime - lastTime < debounceInterval)
                    return true;
            }
            lastPlayTimes[id] = Time.unscaledTime;
            return false;
        }

        /// <summary>Play a 2D sound effect with debounce protection.</summary>
        private void PlaySE(AudioClip clip, float pitch, float volume)
        {
            if (clip == null) return;
            if (IsDebounced(clip)) return;

            foreach (var src in seSources)
            {
                if (!src.isPlaying)
                {
                    src.clip = clip;
                    src.pitch = pitch;
                    src.volume = volume * seVolume;
                    src.spatialBlend = 0f;
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

        /// <summary>Play a 3D positional sound at the given world position with debounce.</summary>
        private void PlaySESpatial(AudioClip clip, Vector3 position, float pitch, float volume)
        {
            if (clip == null) return;
            if (IsDebounced(clip)) return;

            AudioSource chosen = null;
            foreach (var src in spatialSources)
            {
                if (!src.isPlaying)
                {
                    chosen = src;
                    break;
                }
            }
            // Steal the first if all busy
            if (chosen == null) chosen = spatialSources[0];

            chosen.transform.position = position;
            chosen.clip = clip;
            chosen.pitch = pitch;
            chosen.volume = volume * seVolume;
            chosen.Play();
        }

        // ---- Procedural Audio Generation ----

        private void GenerateAllClips()
        {
            feedPopClip = GenerateChirpyPop(0.08f, 800f, 1400f);
            blobAbsorbClip = GenerateDeepGulp(0.3f);
            powerUpClip = GenerateSparklingChime(0.35f);
            dashClip = GenerateFilteredWhoosh(0.18f);
            shieldBlockClip = GenerateMetallicClang(0.25f);
            comboTingClip = GenerateRisingTing(0.12f);
            gameOverClip = GenerateDescendingTone(0.7f);
            menuClickClip = GenerateSubtleClick(0.04f);
            timerWarningClip = GenerateBeep(0.1f, 880f);
            victoryClip = GenerateVictoryFanfare(0.5f);
            bgmClip = GenerateAmbientBGM(8f); // 8 second loop
        }

        /// <summary>
        /// Short chirpy pop with rising frequency sine burst.
        /// Used for feed eating — bright, satisfying micro-feedback.
        /// </summary>
        private AudioClip GenerateChirpyPop(float duration, float freqStart, float freqEnd)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                // Rising frequency sweep for chirpy character
                float freq = Mathf.Lerp(freqStart, freqEnd, t * t);
                // Snappy attack with exponential decay
                float envelope = Mathf.Exp(-t * 12f) * Mathf.Clamp01(t / 0.02f);
                // Accumulate phase for clean frequency sweep
                phase += freq / sampleRate;
                float sample = Mathf.Sin(2f * Mathf.PI * phase) * envelope * 0.5f;
                // Add a subtle second harmonic for brightness
                sample += Mathf.Sin(4f * Mathf.PI * phase) * envelope * 0.15f;
                data[i] = sample;
            }
            var clip = AudioClip.Create("FeedPop", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Deep satisfying gulp with descending sine and low-pass filtering.
        /// Used for blob absorption — weighty, organic feel.
        /// </summary>
        private AudioClip GenerateDeepGulp(float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            float phase1 = 0f;
            float phase2 = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                // Descending frequency for "gulp" feel
                float freq = 300f * Mathf.Exp(-t * 3f) + 60f;
                // Wobble modulation for organic character
                float wobble = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 8f * t);
                float modFreq = freq * wobble;
                // Envelope: quick attack, sustained body, gradual decay
                float envelope = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.3f);
                // Phase accumulation for both fundamental and sub
                phase1 += modFreq / sampleRate;
                phase2 += (modFreq * 0.5f) / sampleRate;
                float sample = Mathf.Sin(2f * Mathf.PI * phase1) * envelope * 0.4f;
                // Sub-harmonic for depth
                sample += Mathf.Sin(2f * Mathf.PI * phase2) * envelope * 0.25f;
                data[i] = sample;
            }

            // Multi-pass low-pass filter for smooth, deep sound
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 1; i < samples - 1; i++)
                {
                    data[i] = data[i - 1] * 0.25f + data[i] * 0.5f + data[i + 1] * 0.25f;
                }
            }

            var clip = AudioClip.Create("BlobAbsorb", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Sparkling chime with harmonic overtones.
        /// Used for power-up collection — bright, magical cascade.
        /// </summary>
        private AudioClip GenerateSparklingChime(float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            // Harmonic series based on C5 for rich, sparkling tone
            float fundamental = 523.25f;
            float[] harmonicRatios = { 1f, 2f, 3f, 4f, 5f, 6f };
            float[] harmonicAmps = { 0.20f, 0.15f, 0.10f, 0.08f, 0.05f, 0.03f };
            // Staggered note entries for cascade effect
            float[] noteDelays = { 0f, 0.04f, 0.08f, 0.12f };
            float[] noteFreqs = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float val = 0f;

                for (int n = 0; n < noteFreqs.Length; n++)
                {
                    if (t < noteDelays[n]) continue;
                    float localT = t - noteDelays[n];
                    // Shimmer attack, exponential decay
                    float attack = Mathf.Clamp01(localT / 0.003f);
                    float decay = Mathf.Exp(-localT * 5f);
                    float env = attack * decay;

                    // Render harmonics for each note
                    for (int h = 0; h < harmonicRatios.Length; h++)
                    {
                        float hFreq = noteFreqs[n] * harmonicRatios[h];
                        // Higher harmonics decay faster for natural bell timbre
                        float hDecay = Mathf.Exp(-localT * (5f + h * 3f));
                        val += Mathf.Sin(2f * Mathf.PI * hFreq * localT) * harmonicAmps[h] * hDecay * attack;
                    }
                }
                data[i] = val;
            }
            var clip = AudioClip.Create("PowerUp", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Filtered noise burst with frequency shaping.
        /// Used for dash — aggressive whoosh with directional sweep.
        /// </summary>
        private AudioClip GenerateFilteredWhoosh(float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            // Generate shaped noise
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                // Asymmetric envelope: fast attack, medium decay
                float envelope = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.5f) * (1f - t * 0.5f);
                // Blend noise with a swept sine for tonal character
                float sweepFreq = Mathf.Lerp(200f, 800f, t);
                float noise = Random.Range(-1f, 1f);
                float tone = Mathf.Sin(2f * Mathf.PI * sweepFreq * (float)i / sampleRate) * 0.3f;
                data[i] = (noise * 0.7f + tone) * envelope * 0.35f;
            }

            // Aggressive low-pass filtering for smooth whoosh texture
            for (int pass = 0; pass < 4; pass++)
            {
                for (int i = 2; i < samples - 2; i++)
                {
                    data[i] = (data[i - 2] + data[i - 1] * 2f + data[i] * 3f + data[i + 1] * 2f + data[i + 2]) / 9f;
                }
            }

            var clip = AudioClip.Create("Dash", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Metallic clang using ring modulation of two sine waves.
        /// Used for shield block — sharp, inharmonic metallic impact.
        /// </summary>
        private AudioClip GenerateMetallicClang(float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            // Ring modulation: multiply two sines at non-harmonic ratios
            float freqA = 740f;   // Carrier
            float freqB = 587f;   // Modulator (non-harmonic ratio for metallic timbre)
            float freqC = 1320f;  // High partial for brightness

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float tNorm = (float)i / samples;
                // Sharp attack, ringing decay
                float attack = Mathf.Clamp01(tNorm / 0.005f);
                float decay = Mathf.Exp(-tNorm * 8f);
                float env = attack * decay;

                // Ring modulation: carrier * modulator produces sum/difference frequencies
                float carrier = Mathf.Sin(2f * Mathf.PI * freqA * t);
                float modulator = Mathf.Sin(2f * Mathf.PI * freqB * t);
                float ringMod = carrier * modulator;
                // Additional high partial for shimmer
                float highPartial = Mathf.Sin(2f * Mathf.PI * freqC * t) * Mathf.Exp(-tNorm * 12f);

                data[i] = (ringMod * 0.5f + highPartial * 0.2f) * env;
            }
            var clip = AudioClip.Create("ShieldBlock", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Short rising-pitch ting for combo increments.
        /// Base pitch is shifted upward via PlayComboTing based on combo count.
        /// </summary>
        private AudioClip GenerateRisingTing(float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                // Slight upward frequency sweep within the ting
                float freq = Mathf.Lerp(1200f, 1600f, t);
                // Very snappy: instant attack, fast ring-out
                float envelope = Mathf.Exp(-t * 15f);
                phase += freq / sampleRate;
                // Pure sine + third harmonic for bell-like quality
                float sample = Mathf.Sin(2f * Mathf.PI * phase) * 0.4f;
                sample += Mathf.Sin(6f * Mathf.PI * phase) * 0.12f;
                data[i] = sample * envelope;
            }
            var clip = AudioClip.Create("ComboTing", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Descending tone with sub-harmonics for dramatic game over.
        /// Slower, more foreboding than the original.
        /// </summary>
        private AudioClip GenerateDescendingTone(float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            float phase1 = 0f;
            float phase2 = 0f;
            float phase3 = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                // Exponential descent for dramatic feel
                float freq = 440f * Mathf.Exp(-t * 2.5f) + 55f;
                // Gradual fade with slight sustain in the middle
                float envelope = (1f - t) * Mathf.Clamp01(1.2f - t);

                phase1 += freq / sampleRate;
                phase2 += (freq * 0.5f) / sampleRate;
                phase3 += (freq * 0.25f) / sampleRate;

                // Fundamental
                float sample = Mathf.Sin(2f * Mathf.PI * phase1) * 0.4f;
                // Sub-octave for weight
                sample += Mathf.Sin(2f * Mathf.PI * phase2) * 0.3f;
                // Sub-sub for rumble
                sample += Mathf.Sin(2f * Mathf.PI * phase3) * 0.15f;
                // Slight detuned layer for unease
                sample += Mathf.Sin(2f * Mathf.PI * phase1 * 1.01f) * 0.1f;

                data[i] = sample * envelope;
            }
            var clip = AudioClip.Create("GameOver", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Ultra-short click for menu interaction.
        /// Minimal, non-intrusive tactile feedback.
        /// </summary>
        private AudioClip GenerateSubtleClick(float duration)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                // Extremely fast exponential decay — just a transient
                float envelope = Mathf.Exp(-t * 40f);
                // High frequency click with a bit of body
                float sample = Mathf.Sin(2f * Mathf.PI * 3000f * (float)i / sampleRate) * 0.3f;
                sample += Mathf.Sin(2f * Mathf.PI * 1500f * (float)i / sampleRate) * 0.2f;
                data[i] = sample * envelope;
            }
            var clip = AudioClip.Create("MenuClick", samples, 1, sampleRate, false);
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
        /// Celebratory ascending arpeggio: C5 -> E5 -> G5 -> C6.
        /// Each note ~0.1s with slight overlap for a bright fanfare feel.
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
