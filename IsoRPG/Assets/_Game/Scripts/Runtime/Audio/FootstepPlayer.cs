using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.Audio
{
    /// <summary>
    /// Шаги под движение.
    ///
    /// Ритм считается от скорости, а не от событий анимации. События точнее —
    /// звук ложится ровно на касание ноги, — но требуют разметки каждого клипа
    /// руками, а клипов у нас сто тридцать девять. Скорость даёт девяносто
    /// процентов результата за ноль работы, и разницу слышно только если
    /// специально вслушиваться.
    ///
    /// Поверхность определяется лучом вниз: на камне и на траве шаг звучит
    /// по-разному, и это единственное, что отличает «персонаж идёт» от
    /// «персонаж идёт ГДЕ-ТО».
    /// </summary>
    public sealed class FootstepPlayer : MonoBehaviour
    {
        [Tooltip("Сколько метров между шагами. Меньше — чаще шлёпает.")]
        [SerializeField] private float stride = 1.35f;

        [Tooltip("Громкость шага. Он звучит постоянно, поэтому тише всего остального.")]
        [SerializeField] private float volume = 0.18f;

        private NavMeshAgent agent;
        private float travelled;
        private Vector3 lastPosition;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            lastPosition = transform.position;
        }

        private void Update()
        {
            // Меряем фактически пройденное, а не скорость агента: при обходе
            // препятствия и на поворотах агент может «хотеть» двигаться,
            // фактически стоя на месте — и персонаж шагал бы стоя.
            float step = Vector3.Distance(transform.position, lastPosition);
            lastPosition = transform.position;

            if (step < 0.0005f) return;

            travelled += step;
            if (travelled < stride) return;

            travelled = 0f;
            PlayStep();
        }

        private void PlayStep()
        {
            var bank = Sfx.Bank;
            if (bank == null) return;

            var set = OnStone() ? bank.stepStone : bank.stepGrass;
            // Разброс шире обычного: шаг повторяется чаще любого другого
            // звука, и одинаковость слышна на нём сильнее всего.
            Sfx.Play(set, transform.position, volume, 0.14f);
        }

        /// <summary>
        /// Под ногами камень или земля.
        ///
        /// Луч вниз от пояса: от ступней он начинался бы внутри пола и мог
        /// не найти ничего вообще.
        /// </summary>
        private bool OnStone()
        {
            if (!Physics.Raycast(transform.position + Vector3.up * 0.9f, Vector3.down,
                                 out var hit, 2f, ~0, QueryTriggerInteraction.Ignore))
                return false;

            // Пол руин — часть окружения из набора Dungeon, трава — земля
            // сцены. Различаем по имени: заводить слои ради одного звука
            // дороже, чем сравнить строку раз в полтора метра пути.
            string name = hit.collider.name.ToLower();

            return name.Contains("floor") || name.Contains("wall") ||
                   name.Contains("stairs") || name.Contains("tile");
        }
    }
}
