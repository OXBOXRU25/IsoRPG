using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Праздное поведение зверя вне боя: пасётся, садится, спит.
    ///
    /// Заведено 02.09.2026 по проверке Павлона: «у него есть анимация
    /// кормления — значит он должен периодически останавливаться и делать вид,
    /// что ест, верно?» Верно, и не делал: состояния в контроллере были, а
    /// параметр `Rest` никто не выставлял, так что они не играли ни разу.
    ///
    /// Правило простое: стоит на месте и никого не видит — через случайную
    /// паузу занимается своим делом. Кто-то появился или сам пошёл — бросает
    /// и возвращается к ходу.
    ///
    /// Цена: одно сравнение раз в полсекунды. Это ММО, в кадре такое считать
    /// нельзя.
    /// </summary>
    public sealed class IdleBehaviour : MonoBehaviour
    {
        private static readonly int RestHash = Animator.StringToHash("Rest");

        [Tooltip("Через сколько секунд простоя начинает заниматься своим делом.")]
        [SerializeField] private Vector2 pause = new Vector2(6f, 16f);

        [Tooltip("Сколько длится занятие.")]
        [SerializeField] private Vector2 length = new Vector2(5f, 12f);

        /// <summary>Потолок числа занятий. У кабана их четыре, у волка тоже.</summary>
        private const int MaxKinds = 6;

        [Tooltip("Что умеет: 1 есть, 2 сидеть, 3 спать, 4 своё. Пусто — спросим у контроллера сами.")]
        [SerializeField] private int[] kinds;

        /// <summary>
        /// Занятие началось. Номер тот же, что и в `Rest`.
        ///
        /// Нужен голосу: волчий вой — это не звук сам по себе, а занятие
        /// «повыть», у которого в наборе есть свой клип. Слушает
        /// <see cref="IsoRPG.Audio.RestVoice"/>.
        /// </summary>
        public event System.Action<int> RestBegan;

        /// <summary>Список занятий от задания сборки. Оно знает его точно, из кода контроллера.</summary>
        public void SetKinds(int[] value) => kinds = value;

        private Animator animator;
        private NavMeshAgent agent;
        private TargetSelector targets;
        private Health health;

        private float nextCheck;
        private float until;
        private int current;
        private bool able;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>(true);
            agent = GetComponent<NavMeshAgent>();
            targets = GetComponent<TargetSelector>();
            health = GetComponent<Health>();

            able = animator != null && Has(RestHash);

            if (able) Discover();

            // Первая пауза со случайным сдвигом: иначе стадо садится и встаёт
            // разом, как по команде, и это видно сразу.
            nextCheck = Time.time + Random.Range(pause.x, pause.y);
        }

        /// <summary>
        /// Спросить у контроллера, какие занятия он умеет.
        ///
        /// Раньше список стоял числом в поле, и он всегда врал в одну сторону:
        /// у босса-кабана было три занятия, у гриба ноль, а компонент всем
        /// подряд слал 1, 2 и 3. Правило теперь по признаку класса: входное
        /// состояние каждого занятия зовётся `Rest_N`, и кто его назвал —
        /// тот его и умеет.
        ///
        /// Считается один раз при старте; в поле `kinds` можно вписать список
        /// руками, и тогда опрос не делается вовсе.
        /// </summary>
        private void Discover()
        {
            if (kinds != null && kinds.Length > 0) return;

            var found = new System.Collections.Generic.List<int>();

            for (int i = 1; i <= MaxKinds; i++)
                if (animator.HasState(0, Animator.StringToHash("Rest_" + i))) found.Add(i);

            kinds = found.ToArray();

            // Параметр есть, а занятий нет — значит контроллер собран
            // наполовину. Молчать нельзя: снаружи это неотличимо от
            // «зверь просто не успел проголодаться».
            if (kinds.Length == 0)
            {
                able = false;
                Debug.LogWarning($"[IsoRPG] У «{name}» есть параметр Rest, но нет ни одного " +
                                 "состояния Rest_N — праздное поведение играть нечем.");
            }
        }

        private void Update()
        {
            if (!able) return;
            if (Time.time < nextCheck) return;

            nextCheck = Time.time + 0.5f;

            bool busy = targets != null && targets.Current != null;
            bool moving = agent != null && agent.isActiveAndEnabled && agent.velocity.sqrMagnitude > 0.05f;
            bool alive = health == null || health.IsAlive;

            // Мёртвый не ест, дерущийся не спит, идущий не сидит.
            if (busy || moving || !alive)
            {
                Stop();
                return;
            }

            if (current != 0)
            {
                if (Time.time >= until) Stop();
                return;
            }

            // Дошли до сюда — стоит, никого не видит, ничем не занят.
            current = kinds != null && kinds.Length > 0
                ? kinds[Random.Range(0, kinds.Length)]
                : 1;

            until = Time.time + Random.Range(length.x, length.y);
            animator.SetInteger(RestHash, current);

            RestBegan?.Invoke(current);
        }

        private void Stop()
        {
            if (current == 0) return;

            current = 0;
            animator.SetInteger(RestHash, 0);

            // После занятия — новая долгая пауза, иначе зверь садится снова
            // через полсекунды и дёргается.
            nextCheck = Time.time + Random.Range(pause.x, pause.y);
        }

        private bool Has(int hash)
        {
            foreach (var p in animator.parameters)
                if (p.nameHash == hash) return true;

            return false;
        }
    }
}
