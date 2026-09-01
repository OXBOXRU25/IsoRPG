using UnityEngine;

namespace IsoRPG.Quests
{
    /// <summary>
    /// Голос НПС: короткая фраза при обращении.
    ///
    /// Заказано Павлом 01.09.2026: «по клику на НПС он произносит случайно одну
    /// из двух фраз; ушли, побили мобов, вернулись, кликнули — снова произнёс.
    /// Чтобы не было такого, что он нон-стоп их произносит, если специально
    /// накликивать, как в WoW».
    ///
    /// Отсюда две вещи, обе важные:
    ///
    /// - **откат**: пока он не истёк, повторный клик молчит. Без него НПС
    ///   тараторит при каждом тычке и превращается в шарманку;
    /// - **не подряд одна и та же**: фраз всего две, и повтор одной и той же
    ///   слышен сразу. Запоминаем прошлую и берём другую.
    ///
    /// Клипы лежат в ресурсах, а не полем в сцене: их кладёт художник, и
    /// добавить третью фразу должно быть можно, не трогая ни код, ни сцену.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class NpcVoice : MonoBehaviour
    {
        /// <summary>Где лежат фразы. Все клипы папки берутся как варианты.</summary>
        private const string ClipsFolder = "Voice/Npc";

        [Tooltip("Сколько молчать после фразы, секунд. В больших РПГ это единицы секунд: реплика должна успеть договориться.")]
        public float Cooldown = 6f;

        private AudioSource source;
        private AudioClip[] clips;
        private int lastIndex = -1;
        private float silentUntil;

        private void Awake()
        {
            source = GetComponent<AudioSource>();

            // Голос звучит от самого НПС: в изометрии это единственный способ
            // понять, кто заговорил, когда рядом стоят двое.
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 3f;
            source.maxDistance = 25f;
            source.rolloffMode = AudioRolloffMode.Linear;

            clips = Resources.LoadAll<AudioClip>(ClipsFolder);
        }

        /// <summary>Сказать фразу. Молчит, пока не истёк откат.</summary>
        public void Speak()
        {
            if (clips == null || clips.Length == 0) return;
            if (Time.time < silentUntil) return;
            if (source.isPlaying) return;

            int index = Pick();
            lastIndex = index;

            source.clip = clips[index];
            source.Play();

            // Откат считаем от конца фразы, а не от начала: иначе длинная
            // реплика съедает почти всю паузу и НПС отвечает встык.
            silentUntil = Time.time + source.clip.length + Cooldown;
        }

        /// <summary>Случайная фраза, но не та же, что прозвучала прошлый раз.</summary>
        private int Pick()
        {
            if (clips.Length == 1) return 0;

            int index = Random.Range(0, clips.Length);
            if (index == lastIndex) index = (index + 1) % clips.Length;

            return index;
        }
    }
}
