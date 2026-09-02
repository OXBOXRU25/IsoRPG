using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.Audio
{
    /// <summary>Голоса зверей, которые можно повесить на занятие вне боя.</summary>
    public enum BeastVoice
    {
        WolfHowl,
        WolfSnarl,
        BoarGrunt,
        BossRoar,
        MushroomIdle,
    }

    /// <summary>
    /// Голос у праздного занятия.
    ///
    /// Заведено 02.09.2026 под волчий вой. Вой лежал в банке звуков с
    /// прошлого захода и не играл ни разу: повода не было. Играть его по
    /// таймеру «просто так» — значит услышать вой из-под ног дерущегося
    /// волка, а это ломает сцену сильнее, чем тишина.
    ///
    /// Повод нашёлся в самом наборе: у волка есть клип `Howl`. Значит вой —
    /// это не звук, а занятие, такое же как еда и отдых, и заводить его надо
    /// там же, где остальные занятия, — в <see cref="IdleBehaviour"/>. Тогда
    /// зверь садится, задирает морду и воет; звук и картинка совпадают, и
    /// повод получается сам собой: волк спокоен и никого не видит.
    ///
    /// Компонент общий: завтра так же зазвучит любое занятие любого зверя,
    /// у которого набор принесёт свой клип.
    /// </summary>
    public sealed class RestVoice : MonoBehaviour
    {
        [Tooltip("Номер занятия из Rest, у которого есть голос.")]
        [SerializeField] private int kind = 4;

        [Tooltip("Какой звук играть.")]
        [SerializeField] private BeastVoice voice = BeastVoice.WolfHowl;

        [Tooltip("Задержка до звука: клип начинается с замаха, а голос идёт не с первого кадра.")]
        [SerializeField] private float delay = 0.35f;

        private IdleBehaviour idle;

        /// <summary>Настройка из задания сборки.</summary>
        public void Setup(int restKind, BeastVoice sound)
        {
            kind = restKind;
            voice = sound;
        }

        private void Awake() => idle = GetComponent<IdleBehaviour>();

        private void OnEnable()
        {
            if (idle != null) idle.RestBegan += OnRest;
        }

        private void OnDisable()
        {
            if (idle != null) idle.RestBegan -= OnRest;
        }

        private void OnRest(int began)
        {
            if (began != kind) return;

            // Задержка через корутину, а не таймером в Update: событие редкое
            // (раз в десятки секунд), и держать ради него проверку в кадре у
            // каждого волка — ровно та цена, которую в ММО платить не надо.
            if (isActiveAndEnabled) StartCoroutine(PlaySoon());
        }

        private System.Collections.IEnumerator PlaySoon()
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            switch (voice)
            {
                case BeastVoice.WolfHowl: Sfx.WolfHowl(transform.position); break;
                case BeastVoice.WolfSnarl: Sfx.WolfSnarl(transform.position); break;
                case BeastVoice.BoarGrunt: Sfx.BoarGrunt(transform.position); break;
                case BeastVoice.BossRoar: Sfx.BossRoar(transform.position); break;
                case BeastVoice.MushroomIdle: Sfx.MushroomIdle(transform.position); break;
            }
        }
    }
}
