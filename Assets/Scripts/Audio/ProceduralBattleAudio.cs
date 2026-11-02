using UnityEngine;

namespace SlugOut.Audio
{
    public class ProceduralBattleAudio : MonoBehaviour
    {
        public static ProceduralBattleAudio Instance { get; private set; }

        private AudioSource _audioSource;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;
        }

        public void PlayMatchSound(int count)
        {
            float pitch = Mathf.Clamp(1.0f + (count - 3) * 0.15f, 0.8f, 2.0f);
            PlayBeep(520f * pitch, 0.12f);
        }

        public void PlayBlasterShot()
        {
            PlayNoiseSweep(800f, 200f, 0.2f);
        }

        public void PlayVelocimorphSonicBoom()
        {
            PlayNoiseSweep(300f, 80f, 0.45f);
        }

        public void PlayImpact()
        {
            PlayNoiseSweep(400f, 60f, 0.25f);
        }

        public void PlayHeal()
        {
            PlayBeep(780f, 0.22f);
        }

        private void PlayBeep(float frequency, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (float)i / sampleCount;
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create("SynthBeep", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            _audioSource.PlayOneShot(clip);
        }

        private void PlayNoiseSweep(float startFreq, float endFreq, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float progress = (float)i / sampleCount;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, progress);
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.2f;
                float sine = Mathf.Sin(2 * Mathf.PI * currentFreq * t);
                samples[i] = (sine * 0.8f + noise) * envelope * 0.4f;
            }

            AudioClip clip = AudioClip.Create("SynthSweep", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            _audioSource.PlayOneShot(clip);
        }
    }
}
