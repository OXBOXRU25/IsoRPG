using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Летящий снаряд: стрела, болт, сгусток магии.
    ///
    /// Урон наносится в момент попадания, а не выстрела — и это не
    /// придирка к реализму. Время полёта делает дистанцию настоящей:
    /// стрелок, начавший выстрел, уже не может его отменить, а игрок,
    /// который рванул в сторону, имеет шанс разорвать дистанцию до того,
    /// как прилетит. Мгновенный урон превратил бы лучника в такого же
    /// «подошёл и бьёт», только издалека.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        private Targetable target;
        private GameObject shooter;
        private Vector3 direction;
        private float travelLeft;
        private int damage;
        private HitResult result;
        private float speed;
        private float expireTime;

        /// <summary>
        /// На каком расстоянии от цели считаем попадание. Полразмера тела:
        /// стрела должна задеть, а не воткнуться в геометрический центр.
        /// </summary>
        private const float HitRadius = 0.6f;

        /// <summary>
        /// Запустить снаряд. Урон уже посчитан: бросок на крит и промах
        /// делается в момент выстрела, а не прилёта — иначе результат
        /// зависел бы от того, успел ли игрок отбежать, и стрелок бил бы
        /// сильнее по убегающим.
        /// </summary>
        public static void Spawn(GameObject model, Vector3 from, Targetable victim,
                                 GameObject owner, int amount, HitResult hit, float flightSpeed)
        {
            if (victim == null) return;

            var go = new GameObject("Projectile");
            go.transform.position = from;

            if (model != null)
            {
                var visual = Instantiate(model, go.transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                // Коллайдеры снимаем: попадание считаем расстоянием, а
                // физическое тело стрелы только мешало бы кликам по земле.
                foreach (var collider in visual.GetComponentsInChildren<Collider>())
                    Destroy(collider);
            }

            var projectile = go.AddComponent<Projectile>();
            projectile.target = victim;
            projectile.shooter = owner;
            projectile.damage = amount;
            projectile.result = hit;
            projectile.speed = flightSpeed;
            // Куда целиться. Стрела летит по прямой, поэтому стрелок берёт
            // упреждение: считает, где цель окажется, пока стрела летит.
            //
            // Наведение в полёте выглядит неправильно и обесценивает
            // движение: убегать бессмысленно, если стрела всё равно
            // повернёт. С упреждением дистанция становится настоящей —
            // резкая смена направления уводит из-под выстрела, а бег по
            // прямой не спасает.
            Vector3 aim = victim.transform.position + Vector3.up * 1.0f;
            float roughTime = Vector3.Distance(from, aim) / Mathf.Max(flightSpeed, 0.01f);

            var victimAgent = victim.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Vector3 victimVelocity = victimAgent != null ? victimAgent.velocity : Vector3.zero;

            // Упреждение неполное. Идеальное означало бы попадание всегда, и
            // уклонение снова перестало бы работать.
            aim += victimVelocity * roughTime * 0.75f;

            projectile.direction = (aim - from).normalized;

            // Дальность полёта с запасом: если цель ушла, стрела должна
            // пролететь мимо и исчезнуть, а не остановиться в воздухе.
            projectile.travelLeft = Vector3.Distance(from, aim) + 4f;

            // Страховка от вечно летящих снарядов: цель может исчезнуть,
            // уехать за навигацию или оказаться недостижимой.
            projectile.expireTime = Time.time + 6f;
        }

        private void Update()
        {
            if (Time.time > expireTime)
            {
                Destroy(gameObject);
                return;
            }

            float step = speed * Time.deltaTime;

            // Препятствие на пути — стрела гаснет о него. Проверяем отрезок
            // текущего шага, а не всю линию: цель может быть за стеной уже
            // сейчас, но стрела до стены ещё не долетела, и гасить её раньше
            // времени было бы враньём в другую сторону.
            if (Physics.Raycast(transform.position, direction, out var block,
                                step, ~0, QueryTriggerInteraction.Ignore))
            {
                // Живые тела не считаются стеной — так же, как при проверке
                // линии огня. Иначе стрела вязла бы в союзниках стрелка.
                if (block.collider != null && block.collider.GetComponentInParent<Targetable>() == null)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            transform.position += direction * step;
            transform.rotation = Quaternion.LookRotation(direction);
            travelLeft -= step;

            // Попадание считаем близостью к цели, а не прилётом в точку:
            // целились с упреждением, и цель могла оказаться чуть в стороне
            // от расчётной точки, но всё равно на пути стрелы.
            if (target != null && target.IsAlive)
            {
                Vector3 toVictim = target.transform.position + Vector3.up * 1.0f - transform.position;

                if (toVictim.sqrMagnitude <= HitRadius * HitRadius)
                {
                    Hit();
                    return;
                }
            }

            // Пролетела своё и никого не задела — промах. Это законный исход:
            // ради него и делалось упреждение вместо наведения.
            if (travelLeft <= 0f) Destroy(gameObject);
        }

        private void Hit()
        {
            // Цель могла умереть, пока стрела летела — добивать труп нельзя:
            // в лог полетело бы попадание по покойнику.
            if (target != null && target.IsAlive && target.Health != null)
            {
                // Показываем то, что дошло после брони, а не то, чем стреляли.
                int actual = target.Health.TakeDamage(damage, shooter);
                DamagePopup.Show(target.OverheadPoint, actual, result);
                ReportToLog(actual);
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Пишем в лог только то, что касается игрока: его выстрелы и
        /// выстрелы по нему. Перестрелки монстров между собой забили бы
        /// журнал чужими строками.
        /// </summary>
        private void ReportToLog(int amount)
        {
            if (target == null || shooter == null) return;

            var self = shooter.GetComponent<Targetable>();
            bool weArePlayer = self != null && self.Faction == Faction.Player;

            if (weArePlayer) CombatLog.DamageDealt(target.DisplayName, amount, result);
            else if (target.Faction == Faction.Player)
                CombatLog.DamageTaken(self != null ? self.DisplayName : "Противник", amount);
        }
    }
}
