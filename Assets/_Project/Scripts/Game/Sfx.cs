using System.Collections.Generic;
using UnityEngine;

namespace Sokoban.Game
{
    /// <summary>
    /// 播放短音效。音效文件放在 Resources/Audio，按文件名调用，例如 Sfx.Play("push")。
    /// 总音量由 AudioListener.volume（设置界面的滑条）控制。
    /// </summary>
    public static class Sfx
    {
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static AudioSource _source;

        public static void Init(GameObject host)
        {
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        public static void Play(string name, float volume = 1f)
        {
            if (_source == null) return;

            if (!Clips.TryGetValue(name, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + name);
                if (clip == null) Debug.LogWarning($"Missing sound: Resources/Audio/{name}.wav");
                Clips[name] = clip;
            }
            if (clip != null) _source.PlayOneShot(clip, volume);
        }
    }
}
